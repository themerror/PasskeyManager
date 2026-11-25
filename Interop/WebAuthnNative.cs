using System.Runtime.InteropServices;

namespace PasskeyManager.Interop;

/// <summary>
/// P/Invoke bindings for the Windows WebAuthN plugin APIs (webauthn.dll),
/// ported from webauthnplugin.h / pluginauthenticator.h / webauthn.h.
/// WinAPI calling convention (default), CharSet.Unicode where strings are marshalled.
/// </summary>
internal static class WebAuthnNative
{
    private const string Dll = "webauthn.dll";

    // Registration / state / update / removal.

    [DllImport(Dll)]
    public static extern int WebAuthNPluginGetAuthenticatorState(
        in Guid rclsid,
        out AuthenticatorState pluginAuthenticatorState);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAddAuthenticator(
        in WebAuthnPluginAddAuthenticatorOptions pPluginAddAuthenticatorOptions,
        out nint ppPluginAddAuthenticatorResponse); // PWEBAUTHN_PLUGIN_ADD_AUTHENTICATOR_RESPONSE*

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAddAuthenticator2(
        in WebAuthnPluginAddAuthenticatorOptions2 pPluginAddAuthenticatorOptions,
        out nint ppPluginAddAuthenticatorResponse);

    [DllImport(Dll)]
    public static extern void WebAuthNPluginFreeAddAuthenticatorResponse(
        nint pPluginAddAuthenticatorResponse);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginRemoveAuthenticator(
        in Guid rclsid);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginUpdateAuthenticatorDetails(
        in WebAuthnPluginUpdateAuthenticatorDetails pPluginUpdateAuthenticatorDetails);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginUpdateAuthenticatorDetails2(
        in WebAuthnPluginUpdateAuthenticatorDetails2 pPluginUpdateAuthenticatorDetails);

    // Credential metadata cache for browser autofill scenarios.

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAuthenticatorAddCredentials(
        in Guid rclsid,
        uint cCredentialDetails,
        nint pCredentialDetails); // PCWEBAUTHN_PLUGIN_CREDENTIAL_DETAILS

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAuthenticatorRemoveCredentials(
        in Guid rclsid,
        uint cCredentialDetails,
        nint pCredentialDetails);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAuthenticatorRemoveAllCredentials(
        in Guid rclsid);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginAuthenticatorGetAllCredentials(
        in Guid rclsid,
        out uint pcCredentialDetails,
        out nint ppCredentialDetailsArray); // PWEBAUTHN_PLUGIN_CREDENTIAL_DETAILS*

    [DllImport(Dll)]
    public static extern void WebAuthNPluginAuthenticatorFreeCredentialDetailsArray(
        uint cCredentialDetails,
        nint pCredentialDetailsArray);

    // Hello UV APIs for plugins.

    [DllImport(Dll)]
    public static extern int WebAuthNPluginPerformUserVerification(
        in WebAuthnPluginUserVerificationRequest pPluginUserVerification,
        out uint pcbResponse,
        out nint ppbResponse);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginPerformUserVerification2(
        in WebAuthnPluginUserVerificationRequest2 pPluginUserVerification,
        out uint pcbResponse,
        out nint ppbResponse);

    [DllImport(Dll)]
    public static extern void WebAuthNPluginFreeUserVerificationResponse(
        nint ppbResponse);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginGetUserVerificationCount(
        in Guid rclsid,
        out uint pdwVerificationCount);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginGetUserVerificationPublicKey(
        in Guid rclsid,
        out uint pcbPublicKey,
        out nint ppbPublicKey);

    [DllImport(Dll)]
    public static extern int WebAuthNPluginGetOperationSigningPublicKey(
        in Guid rclsid,
        out uint pcbOpSignPubKey,
        out nint ppbOpSignPubKey);

    [DllImport(Dll)]
    public static extern void WebAuthNPluginFreePublicKeyResponse(
        nint pbPublicKey);

    // CTAP CBOR encode / decode / free.

    [DllImport(Dll)]
    public static extern int WebAuthNEncodeMakeCredentialResponse(
        in WebAuthnCredentialAttestation pCredentialAttestation,
        out uint pcbResp,
        out nint ppbResp);

    [DllImport(Dll)]
    public static extern int WebAuthNDecodeMakeCredentialRequest(
        uint cbEncoded,
        nint pbEncoded,
        out nint ppMakeCredentialRequest); // PWEBAUTHN_CTAPCBOR_MAKE_CREDENTIAL_REQUEST*

    [DllImport(Dll)]
    public static extern void WebAuthNFreeDecodedMakeCredentialRequest(
        nint pMakeCredentialRequest);

    [DllImport(Dll)]
    public static extern int WebAuthNDecodeGetAssertionRequest(
        uint cbEncoded,
        nint pbEncoded,
        out nint ppGetAssertionRequest); // PWEBAUTHN_CTAPCBOR_GET_ASSERTION_REQUEST*

    [DllImport(Dll)]
    public static extern void WebAuthNFreeDecodedGetAssertionRequest(
        nint pGetAssertionRequest);

    [DllImport(Dll)]
    public static extern int WebAuthNEncodeGetAssertionResponse(
        in WebAuthnCtapCborGetAssertionResponse pGetAssertionResponse,
        out uint pcbResp,
        out nint ppbResp);
}
