using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class SignInWindowViewModel : ViewModelBase
{
    private readonly Func<string, Task> _openTokenPageAsync;
    private string _endpoint;
    private string _token = string.Empty;
    private string _errorText = string.Empty;

    public SignInWindowViewModel(
        string title,
        string description,
        string endpointLabel,
        string endpointWatermark,
        string tokenLabel,
        string tokenWatermark,
        string tokenHelp,
        string openTokenPageText,
        string cancelText,
        string signInText,
        string tokenRequiredText,
        string openTokenPageFailedFormat,
        string endpoint,
        Func<string, Task> openTokenPageAsync)
    {
        Title = title;
        Description = description;
        EndpointLabel = endpointLabel;
        EndpointWatermark = endpointWatermark;
        TokenLabel = tokenLabel;
        TokenWatermark = tokenWatermark;
        TokenHelp = tokenHelp;
        OpenTokenPageText = openTokenPageText;
        CancelText = cancelText;
        SignInText = signInText;
        TokenRequiredText = tokenRequiredText;
        OpenTokenPageFailedFormat = openTokenPageFailedFormat;
        _endpoint = endpoint;
        _openTokenPageAsync = openTokenPageAsync;

        OpenTokenPageCommand = ReactiveCommand.CreateFromTask(OpenTokenPageAsync);
        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        SignInCommand = ReactiveCommand.Create(SignIn);
    }

    public event EventHandler<DialogCloseRequestedEventArgs<GitHubSignInRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> OpenTokenPageCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> SignInCommand { get; }

    public string Title { get; }

    public string Description { get; }

    public string EndpointLabel { get; }

    public string EndpointWatermark { get; }

    public string TokenLabel { get; }

    public string TokenWatermark { get; }

    public string TokenHelp { get; }

    public string OpenTokenPageText { get; }

    public string CancelText { get; }

    public string SignInText { get; }

    public string TokenRequiredText { get; }

    public string OpenTokenPageFailedFormat { get; }

    public string Endpoint
    {
        get => _endpoint;
        set => this.RaiseAndSetIfChanged(ref _endpoint, value);
    }

    public string Token
    {
        get => _token;
        set => this.RaiseAndSetIfChanged(ref _token, value);
    }

    public string ErrorText
    {
        get => _errorText;
        private set
        {
            this.RaiseAndSetIfChanged(ref _errorText, value);
            this.RaisePropertyChanged(nameof(HasError));
        }
    }

    public bool HasError => !string.IsNullOrWhiteSpace(ErrorText);

    private void SignIn()
    {
        if (string.IsNullOrWhiteSpace(Token))
        {
            ErrorText = TokenRequiredText;
            return;
        }

        ErrorText = string.Empty;
        RequestClose(new GitHubSignInRequest(Endpoint, Token));
    }

    private async Task OpenTokenPageAsync()
    {
        try
        {
            ErrorText = string.Empty;
            await _openTokenPageAsync(Endpoint);
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(OpenTokenPageFailedFormat, ex.Message);
        }
    }

    private void RequestClose(GitHubSignInRequest? result)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<GitHubSignInRequest?>(result));
    }
}
