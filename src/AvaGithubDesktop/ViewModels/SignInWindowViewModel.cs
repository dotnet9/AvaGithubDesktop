using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class SignInWindowViewModel : ViewModelBase
{
    private readonly IGitHubAccountService _gitHubAccountService;
    private readonly IRepositoryShellService _repositoryShellService;
    private CancellationTokenSource? _signInCancellationTokenSource;
    private string _endpoint;
    private string _errorText = string.Empty;
    private string _userCode = string.Empty;
    private string _verificationUri = string.Empty;
    private bool _isSigningIn;

    public SignInWindowViewModel(
        string title,
        string description,
        string endpointLabel,
        string endpointWatermark,
        string browserButtonText,
        string deviceCodeLabel,
        string waitingForBrowserText,
        string deviceCodeInstructionFormat,
        string cancelText,
        string signingInText,
        string signInFailedFormat,
        string endpoint,
        IGitHubAccountService gitHubAccountService,
        IRepositoryShellService repositoryShellService)
    {
        Title = title;
        Description = description;
        EndpointLabel = endpointLabel;
        EndpointWatermark = endpointWatermark;
        BrowserButtonText = browserButtonText;
        DeviceCodeLabel = deviceCodeLabel;
        WaitingForBrowserText = waitingForBrowserText;
        DeviceCodeInstructionFormat = deviceCodeInstructionFormat;
        CancelText = cancelText;
        SigningInText = signingInText;
        SignInFailedFormat = signInFailedFormat;
        _endpoint = endpoint;
        _gitHubAccountService = gitHubAccountService;
        _repositoryShellService = repositoryShellService;

        var canStartSignIn = this.WhenAnyValue(model => model.IsSigningIn, isSigningIn => !isSigningIn);
        BeginBrowserSignInCommand = ReactiveCommand.CreateFromTask(BeginBrowserSignInAsync, canStartSignIn);
        CancelCommand = ReactiveCommand.Create(Cancel);
    }

    public event EventHandler<DialogCloseRequestedEventArgs<GitHubAccount?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> BeginBrowserSignInCommand { get; }

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public string Title { get; }

    public string Description { get; }

    public string EndpointLabel { get; }

    public string EndpointWatermark { get; }

    public string BrowserButtonText { get; }

    public string DeviceCodeLabel { get; }

    public string WaitingForBrowserText { get; }

    public string DeviceCodeInstructionFormat { get; }

    public string CancelText { get; }

    public string SigningInText { get; }

    public string SignInFailedFormat { get; }

    public string Endpoint
    {
        get => _endpoint;
        set => this.RaiseAndSetIfChanged(ref _endpoint, value);
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

    public string UserCode
    {
        get => _userCode;
        private set
        {
            this.RaiseAndSetIfChanged(ref _userCode, value);
            this.RaisePropertyChanged(nameof(HasDeviceCode));
            this.RaisePropertyChanged(nameof(DeviceCodeInstructionText));
        }
    }

    public string VerificationUri
    {
        get => _verificationUri;
        private set => this.RaiseAndSetIfChanged(ref _verificationUri, value);
    }

    public bool HasDeviceCode => !string.IsNullOrWhiteSpace(UserCode);

    public string DeviceCodeInstructionText =>
        string.IsNullOrWhiteSpace(UserCode)
            ? string.Empty
            : string.Format(DeviceCodeInstructionFormat, UserCode);

    public bool IsSigningIn
    {
        get => _isSigningIn;
        private set
        {
            this.RaiseAndSetIfChanged(ref _isSigningIn, value);
            this.RaisePropertyChanged(nameof(BrowserButtonDisplayText));
        }
    }

    public string BrowserButtonDisplayText => IsSigningIn ? SigningInText : BrowserButtonText;

    private async Task BeginBrowserSignInAsync()
    {
        IsSigningIn = true;
        ErrorText = string.Empty;
        _signInCancellationTokenSource?.Dispose();
        _signInCancellationTokenSource = new CancellationTokenSource();

        try
        {
            var authorization = await _gitHubAccountService.BeginDeviceAuthorizationAsync(
                Endpoint,
                _signInCancellationTokenSource.Token);
            UserCode = authorization.UserCode;
            VerificationUri = authorization.VerificationUri;

            // Device Flow 不需要 client secret，适合桌面客户端；令牌交换仍统一由账号服务处理。
            await _repositoryShellService.OpenUrlAsync(authorization.VerificationUriComplete);
            var account = await _gitHubAccountService.CompleteDeviceSignInAsync(
                authorization,
                _signInCancellationTokenSource.Token);
            RequestClose(account);
        }
        catch (OperationCanceledException)
        {
            RequestClose(null);
        }
        catch (Exception ex)
        {
            ErrorText = string.Format(SignInFailedFormat, ex.Message);
        }
        finally
        {
            IsSigningIn = false;
        }
    }

    private void Cancel()
    {
        _signInCancellationTokenSource?.Cancel();
        RequestClose(null);
    }

    private void RequestClose(GitHubAccount? account)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<GitHubAccount?>(account));
    }
}
