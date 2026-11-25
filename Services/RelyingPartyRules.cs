using System.Net;

namespace PasskeyManager.Services;

/// <summary>
/// Separates explicitly supported learning sites from production relying parties.
/// Production RP IDs always receive an additional confirmation and warning.
/// </summary>
public static class RelyingPartyRules
{
    private static readonly HashSet<string> LearningHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        "webauthn.io",
        "webauthn.me",
        "webauthn.guide",
        "passkeys.dev",
        "localhost",
        "example.com",
        "example.org",
        "example.net"
    };

    public static bool IsLearningSite(string rpId)
    {
        if (string.IsNullOrWhiteSpace(rpId))
        {
            return false;
        }

        var host = rpId.Trim().TrimEnd('.');
        if (LearningHosts.Contains(host)
            || host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".test", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".example", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return IPAddress.TryParse(host, out var address) && IPAddress.IsLoopback(address);
    }
}
