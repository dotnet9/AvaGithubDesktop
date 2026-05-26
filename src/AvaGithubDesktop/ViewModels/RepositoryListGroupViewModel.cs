using System.Collections.ObjectModel;

namespace AvaGithubDesktop.ViewModels;

public sealed class RepositoryListGroupViewModel
{
    public RepositoryListGroupViewModel(string header, IEnumerable<RepositoryListItemViewModel> items)
    {
        Header = header;
        Items = new ObservableCollection<RepositoryListItemViewModel>(items);
    }

    public string Header { get; }

    public ObservableCollection<RepositoryListItemViewModel> Items { get; }
}
