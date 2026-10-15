using System.Globalization;
using CodeWF.Toolkit.Lang;

namespace AvaGithubDesktop.Core.Services;

public sealed class AppLocalizer : IAppLocalizer
{
    public event EventHandler<EventArgs>? CultureChanged
    {
        add => I18nManager.Instance.CultureChanged += value;
        remove => I18nManager.Instance.CultureChanged -= value;
    }

    public CultureInfo Culture => I18nManager.Instance.Culture ?? CultureInfo.CurrentCulture;

    public void SetCulture(string cultureName)
    {
        I18nManager.Instance.Culture = new CultureInfo(cultureName);
    }

    public string Get(string key)
    {
        return I18nManager.Instance.GetResource(key);
    }

    public string Format(string key, params object?[] args)
    {
        return string.Format(Culture, Get(key), args);
    }

}
