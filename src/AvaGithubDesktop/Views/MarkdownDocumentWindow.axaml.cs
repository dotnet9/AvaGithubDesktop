using CodeWF.AvaloniaControls.Controls;

namespace AvaGithubDesktop.Views;

public partial class MarkdownDocumentWindow : CodeWFWindow
{
    public MarkdownDocumentWindow()
    {
        InitializeComponent();
    }

    public MarkdownDocumentWindow(string title, string markdown, string? imageBasePath = null)
        : this()
    {
        Title = title;
        TitleText.Text = title;
        DocumentMarkdownViewer.Markdown = markdown;
        DocumentMarkdownViewer.ImageBasePath = imageBasePath;
    }
}
