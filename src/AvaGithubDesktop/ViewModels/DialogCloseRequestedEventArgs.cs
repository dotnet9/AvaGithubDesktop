namespace AvaGithubDesktop.ViewModels;

public sealed class DialogCloseRequestedEventArgs<TResult> : EventArgs
{
    public DialogCloseRequestedEventArgs(TResult result)
    {
        Result = result;
    }

    public TResult Result { get; }
}
