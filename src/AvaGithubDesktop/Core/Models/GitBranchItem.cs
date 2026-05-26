namespace AvaGithubDesktop.Core.Models;

public sealed record GitBranchItem(
    string Name,
    string Upstream,
    string RelativeDate,
    bool IsCurrent)
{
    public string DisplayDetail => string.IsNullOrWhiteSpace(Upstream) || Upstream == "-"
        ? RelativeDate
        : $"{Upstream} · {RelativeDate}";
}
