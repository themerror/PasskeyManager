using Microsoft.Windows.Globalization;
using System.Globalization;

namespace PasskeyManager.Services;

/// <summary>
/// Reads and changes the app's language override. Windows persists the override
/// across launches, and newly created XAML pages immediately use the selection.
/// </summary>
public static class LanguageService
{
    public const string English = "en-US";
    public const string SimplifiedChinese = "zh-CN";

    public static string CurrentLanguage
    {
        get
        {
            var overrideLanguage = ApplicationLanguages.PrimaryLanguageOverride;
            if (!string.IsNullOrWhiteSpace(overrideLanguage))
            {
                return overrideLanguage;
            }
            return CultureInfo.CurrentUICulture.Name.StartsWith("zh", StringComparison.OrdinalIgnoreCase)
                ? SimplifiedChinese
                : English;
        }
    }

    public static void SetLanguage(string language)
    {
        if (language is not (English or SimplifiedChinese))
        {
            throw new ArgumentOutOfRangeException(nameof(language));
        }
        ApplicationLanguages.PrimaryLanguageOverride = language;
    }
}
