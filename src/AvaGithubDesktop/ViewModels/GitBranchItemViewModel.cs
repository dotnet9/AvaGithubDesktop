using System.Reactive;
using AvaGithubDesktop.Core.Models;
using ReactiveUI;

namespace AvaGithubDesktop.ViewModels;

public sealed class GitBranchItemViewModel : ViewModelBase
{
    private readonly Func<GitBranchItemViewModel, Task> _renameAsync;
    private readonly Func<GitBranchItemViewModel, Task> _copyNameAsync;
    private readonly Func<GitBranchItemViewModel, Task> _deleteAsync;

    public GitBranchItemViewModel(
        GitBranchItem branch,
        Func<GitBranchItemViewModel, Task> renameAsync,
        Func<GitBranchItemViewModel, Task> copyNameAsync,
        Func<GitBranchItemViewModel, Task> deleteAsync)
    {
        Branch = branch;
        _renameAsync = renameAsync;
        _copyNameAsync = copyNameAsync;
        _deleteAsync = deleteAsync;

        RenameCommand = ReactiveCommand.CreateFromTask(RenameAsync);
        CopyNameCommand = ReactiveCommand.CreateFromTask(CopyNameAsync);
        DeleteCommand = ReactiveCommand.CreateFromTask(DeleteAsync);
    }

    public GitBranchItem Branch { get; }

    public string Name => Branch.Name;

    public string Upstream => Branch.Upstream;

    public string RelativeDate => Branch.RelativeDate;

    public bool IsCurrent => Branch.IsCurrent;

    public string DisplayDetail => Branch.DisplayDetail;

    public bool CanRename => true;

    public bool CanDelete => !IsCurrent;

    public ReactiveCommand<Unit, Unit> RenameCommand { get; }

    public ReactiveCommand<Unit, Unit> CopyNameCommand { get; }

    public ReactiveCommand<Unit, Unit> DeleteCommand { get; }

    private Task RenameAsync()
    {
        return _renameAsync(this);
    }

    private Task CopyNameAsync()
    {
        return _copyNameAsync(this);
    }

    private Task DeleteAsync()
    {
        return _deleteAsync(this);
    }
}
