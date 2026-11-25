namespace PasskeyManager.Interop;

/// <summary>
/// Well-known constants for the C# passkey plugin interop lane.
/// Values mirror the official PasskeyManager C++ sample (PluginManagement).
/// </summary>
internal static class PluginConstants
{
    /// <summary>COM class ID of the plugin authenticator (CLSID).</summary>
    public static readonly Guid PluginClsid = new("ee74bbe8-3ef3-425a-b5e7-20b2cb259454");

    /// <summary>AAGUID reported in the CTAP CBOR authenticatorGetInfo blob.</summary>
    public static readonly Guid PluginAaguid = new("2b0ec8c2-fd5a-47b2-ad40-8f6526ed17fa");

    public const string PluginName = "CSharp Passkey Manager";
    public const string PluginRpId = "csharp-passkeys.example";
    public const string KeyNamePrefix = "csharp-passkeys/";

    /// <summary>AAGUID rendered as upper-case, dash-free hex (big-endian order) for the CBOR blob.</summary>
    public static string PluginAaguidHex => PluginAaguid.ToString("N").ToUpperInvariant();

    /// <summary>HRESULT_FROM_WIN32(ERROR_FILE_NOT_FOUND)</summary>
    public const int HResultErrorFileNotFound = unchecked((int)0x80070002);

    public const int S_OK = 0;

    // WEBAUTHN_CREDENTIAL_TYPE_PUBLIC_KEY
    public const string CredentialTypePublicKey = "public-key";

    // WEBAUTHN_COSE_ALGORITHM_ECDSA_P256_WITH_SHA256
    public const int CoseAlgorithmEcdsaP256WithSha256 = -7;
}
