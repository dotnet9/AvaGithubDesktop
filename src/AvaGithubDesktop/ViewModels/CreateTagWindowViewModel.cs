using System.Reactive;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.Core.Services;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class CreateTagWindowViewModel : ViewModelBase
{
    private readonly GitCommitItem _targetCommit;
    private readonly IReadOnlySet<string> _existingTagNames;
    private readonly IAppLocalizer _localizer;
    private string _tagName = string.Empty;
    private string _message = string.Empty;

    public CreateTagWindowViewModel(
        GitCommitItem targetCommit,
        IReadOnlySet<string> existingTagNames,
        IAppLocalizer localizer)
    {
        _targetCommit = targetCommit;
        _existingTagNames = existingTagNames;
        _localizer = localizer;

        CancelCommand = ReactiveCommand.Create(() => RequestClose(null));
        CreateCommand = ReactiveCommand.Create(CreateTag, this.WhenAnyValue(model => model.CanCreateTag));
    }

    public event EventHandler<DialogCloseRequestedEventArgs<TagCreationRequest?>>? CloseRequested;

    public ReactiveCommand<Unit, Unit> CancelCommand { get; }

    public ReactiveCommand<Unit, Unit> CreateCommand { get; }

    public string Title => _localizer.Get(AvaGithubDesktopL.CreateTagTitle);

    public string TagNameLabel => _localizer.Get(AvaGithubDesktopL.CreateTagNameLabel);

    public string TagNamePlaceholder => _localizer.Get(AvaGithubDesktopL.CreateTagNamePlaceholder);

    public string MessageLabel => _localizer.Get(AvaGithubDesktopL.CreateTagMessageLabel);

    public string MessagePlaceholder => _localizer.Get(AvaGithubDesktopL.CreateTagMessagePlaceholder);

    public string TargetTitle => _localizer.Get(AvaGithubDesktopL.CreateTagTargetTitle);

    public string TargetDescription =>
        _localizer.Format(AvaGithubDesktopL.CreateTagTargetDescriptionFormat, _targetCommit.ShortSha, _targetCommit.Summary);

    public string TagName
    {
        get => _tagName;
        set
        {
            this.RaiseAndSetIfChanged(ref _tagName, value);
            RaiseTagNameStateChanged();
        }
    }

    public string Message
    {
        get => _message;
        set => this.RaiseAndSetIfChanged(ref _message, value);
    }

    public string TagNameError =>
        TagNameValidator.GetValidationError(TagName, _existingTagNames, _localizer);

    public bool HasTagNameError => !string.IsNullOrWhiteSpace(TagNameError);

    public bool CanCreateTag => !HasTagNameError;

    private void CreateTag()
    {
        RequestClose(new TagCreationRequest(TagName.Trim(), Message.Trim(), _targetCommit.Sha));
    }

    private void RequestClose(TagCreationRequest? request)
    {
        CloseRequested?.Invoke(this, new DialogCloseRequestedEventArgs<TagCreationRequest?>(request));
    }

    private void RaiseTagNameStateChanged()
    {
        this.RaisePropertyChanged(nameof(TagNameError));
        this.RaisePropertyChanged(nameof(HasTagNameError));
        this.RaisePropertyChanged(nameof(CanCreateTag));
    }
}
