using Microsoft.Win32;

namespace PasskeyManager.Services;

/// <summary>
/// Owns persisted vault/UI flags. COM callbacks and the interactive UI share these
/// values through HKCU so separate activation processes observe the same state.
/// </summary>
public static class PluginSettings
{
    private const string RegistryPath = @"Software\CSharpPasskeyManager";
    private const string VaultLockedValue = "VaultLocked";
    private const string SilentOperationValue = "SilentOperation";

    public static bool IsVaultLocked() => ReadBoolean(VaultLockedValue, false);

    public static void SetVaultLocked(bool value) => WriteBoolean(VaultLockedValue, value);

    public static bool IsSilentOperation() => ReadBoolean(SilentOperationValue, true);

    public static void SetSilentOperation(bool value) => WriteBoolean(SilentOperationValue, value);

    private static bool ReadBoolean(string name, bool defaultValue)
    {
        using var key = Registry.CurrentUser.OpenSubKey(RegistryPath);
        return key?.GetValue(name) is int value ? value != 0 : defaultValue;
    }

    private static void WriteBoolean(string name, bool value)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RegistryPath);
        key.SetValue(name, value ? 1 : 0, RegistryValueKind.DWord);
    }
}
