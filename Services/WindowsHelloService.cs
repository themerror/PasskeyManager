using Windows.Security.Credentials.UI;

namespace PasskeyManager.Services;

public static class WindowsHelloService
{
    public static async Task<UserConsentVerificationResult> VerifyAsync(string message)
    {
        var availability = await UserConsentVerifier.CheckAvailabilityAsync();
        if (availability != UserConsentVerifierAvailability.Available)
        {
            throw new InvalidOperationException(AppStrings.Format("HelloUnavailableFormat", availability));
        }

        return await UserConsentVerifier.RequestVerificationAsync(message);
    }

    public static async Task<bool> UnlockVaultAsync()
    {
        if (!PluginSettings.IsVaultLocked())
        {
            return true;
        }

        var result = await VerifyAsync(AppStrings.Get("HelloUnlockMessage"));
        if (result != UserConsentVerificationResult.Verified)
        {
            return false;
        }

        PluginSettings.SetVaultLocked(false);
        return true;
    }
}
