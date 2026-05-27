using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using AvaGithubDesktop.Core.Models;
using AvaGithubDesktop.ViewModels;
using AvaGithubDesktop.Views;

namespace AvaGithubDesktop.Core.Services;

public sealed class BranchDialogService : IBranchDialogService
{
    private readonly IAppLocalizer _localizer;

    public BranchDialogService(IAppLocalizer localizer)
    {
        _localizer = localizer;
    }

    public async Task<BranchCreationRequest?> ShowCreateBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches,
        string initialName)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new CreateBranchWindowViewModel(
            currentBranch,
            branches,
            initialName,
            _localizer);
        var window = new CreateBranchWindow(viewModel);
        return await window.ShowDialog<BranchCreationRequest?>(owner);
    }

    public async Task<BranchRenameRequest?> ShowRenameBranchDialogAsync(
        string branchName,
        IReadOnlyList<GitBranchItem> branches)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new RenameBranchWindowViewModel(branchName, branches, _localizer);
        var window = new RenameBranchWindow(viewModel);
        return await window.ShowDialog<BranchRenameRequest?>(owner);
    }

    public async Task<BranchMergeRequest?> ShowMergeBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new MergeBranchWindowViewModel(currentBranch, branches, _localizer);
        var window = new MergeBranchWindow(viewModel);
        return await window.ShowDialog<BranchMergeRequest?>(owner);
    }

    public async Task<BranchMergeRequest?> ShowSquashMergeBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new MergeBranchWindowViewModel(currentBranch, branches, _localizer, isSquashMerge: true);
        var window = new MergeBranchWindow(viewModel);
        return await window.ShowDialog<BranchMergeRequest?>(owner);
    }

    public async Task<BranchRebaseRequest?> ShowRebaseBranchDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> branches)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new RebaseBranchWindowViewModel(currentBranch, branches, _localizer);
        var window = new RebaseBranchWindow(viewModel);
        return await window.ShowDialog<BranchRebaseRequest?>(owner);
    }

    public async Task<BranchUpstreamRequest?> ShowSetUpstreamDialogAsync(
        string currentBranch,
        IReadOnlyList<GitBranchItem> remoteBranches)
    {
        if (GetMainWindow() is not { } owner)
        {
            return null;
        }

        var viewModel = new SetUpstreamBranchWindowViewModel(currentBranch, remoteBranches, _localizer);
        var window = new SetUpstreamBranchWindow(viewModel);
        return await window.ShowDialog<BranchUpstreamRequest?>(owner);
    }

    public async Task<bool> ShowDeleteBranchConfirmationAsync(string branchName)
    {
        if (GetMainWindow() is not { } owner)
        {
            return false;
        }

        var viewModel = new DeleteBranchConfirmationWindowViewModel(branchName, _localizer);
        var window = new DeleteBranchConfirmationWindow(viewModel);
        return await window.ShowDialog<bool>(owner);
    }

    private static Window? GetMainWindow()
    {
        return Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime { MainWindow: { } mainWindow }
            ? mainWindow
            : null;
    }
}
