using AvaGithubDesktop.Core.Models;

namespace AvaGithubDesktop.Core.Services;

public interface IAppSettingsStore
{
    AppSettings Current { get; }

    AppSettings Update(Func<AppSettings, AppSettings> update);
}
