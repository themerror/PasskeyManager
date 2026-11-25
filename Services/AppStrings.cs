using Microsoft.Windows.ApplicationModel.Resources;
using System.Globalization;

namespace PasskeyManager.Services;

/// <summary>
/// Loads user-facing text from Strings/&lt;locale&gt;/Resources.resw.
/// Windows App SDK resource loading works on both UI and COM/RPC threads.
/// </summary>
public static class AppStrings
{

    public static string Get(string key)
    {
        try
        {
            var loader = new ResourceLoader();
            var value = loader.GetString(key);
            return string.IsNullOrWhiteSpace(value) ? key : value;
        }
        catch (Exception)
        {
            return key;
        }
    }

    public static string Format(string key, params object[] args) =>
        string.Format(CultureInfo.CurrentCulture, Get(key), args);
}
