using System.Runtime.InteropServices;

namespace PasskeyManager.Interop;

// Layout notes: all structures are x64, LayoutKind.Sequential, CharSet.None.
// Native pointers are nint, DWORD is uint, LONG is int, BOOL is int (per spec).
// REFGUID/REFCLSID members inside structs are stored as nint pointing at a GUID
// (matching the C header semantics where REFGUID == const GUID*).

internal enum PluginLockStatus : int
{
    PluginLocked = 0,
    PluginUnlocked = 1,
}

internal enum AuthenticatorState : int
{
    Disabled = 0,
    Enabled = 1,
}

internal enum WebAuthnPluginRequestType : int
{
    Ctap2Cbor = 0x1,
}

internal enum WebAuthnPluginPerformUvOperationType : int
{
    PerformUserVerification = 1,
    GetUserVerificationCount = 2,
    GetPublicKey = 3,
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginOperationRequest
{
    public nint HWnd;                    // HWND
    public Guid TransactionId;           // GUID (by value)
    public uint CbRequestSignature;      // DWORD
    public nint PbRequestSignature;      // byte*
    public int RequestType;              // WEBAUTHN_PLUGIN_REQUEST_TYPE
    public uint CbEncodedRequest;        // DWORD
    public nint PbEncodedRequest;        // byte*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginOperationResponse
{
    public uint CbEncodedResponse;       // DWORD
    public nint PbEncodedResponse;       // byte*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginCancelOperationRequest
{
    public Guid TransactionId;           // GUID (by value)
    public uint CbRequestSignature;      // DWORD
    public nint PbRequestSignature;      // byte*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginAddAuthenticatorOptions
{
    public nint PwszAuthenticatorName;   // LPCWSTR
    public nint Rclsid;                  // REFCLSID (pointer to CLSID)
    public nint PwszPluginRpId;          // LPCWSTR
    public nint PwszLightThemeLogoSvg;   // LPCWSTR (optional)
    public nint PwszDarkThemeLogoSvg;    // LPCWSTR (optional)
    public uint CbAuthenticatorInfo;     // DWORD
    public nint PbAuthenticatorInfo;     // const BYTE*
    public uint CSupportedRpIds;         // DWORD
    public nint PpwszSupportedRpIds;     // const LPCWSTR*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginAddAuthenticatorOptions2
{
    public nint PwszAuthenticatorName;   // LPCWSTR
    public nint PClsid;                  // const CLSID*
    public nint PwszPluginRpId;          // LPCWSTR
    public nint PwszLightThemeLogoSvg;   // LPCWSTR (optional)
    public nint PwszDarkThemeLogoSvg;    // LPCWSTR (optional)
    public uint CbAuthenticatorInfo;     // DWORD
    public nint PbAuthenticatorInfo;     // const BYTE*
    public uint CSupportedRpIds;         // DWORD
    public nint PpwszSupportedRpIds;     // const LPCWSTR*
    public nint PwszUserVerificationKeyName; // LPCWSTR (optional)
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginAddAuthenticatorResponse
{
    public uint CbOpSignPubKey;          // DWORD
    public nint PbOpSignPubKey;          // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginUpdateAuthenticatorDetails
{
    public nint PwszAuthenticatorName;   // LPCWSTR
    public nint Rclsid;                  // REFCLSID
    public nint RclsidNew;               // REFCLSID
    public nint PwszLightThemeLogoSvg;   // LPCWSTR (optional, NULL removes)
    public nint PwszDarkThemeLogoSvg;    // LPCWSTR (optional, NULL removes)
    public uint CbAuthenticatorInfo;     // DWORD
    public nint PbAuthenticatorInfo;     // const BYTE*
    public uint CSupportedRpIds;         // DWORD
    public nint PpwszSupportedRpIds;     // const LPCWSTR*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginUpdateAuthenticatorDetails2
{
    public nint PwszAuthenticatorName;   // LPCWSTR
    public nint PClsid;                  // const CLSID*
    public nint PClsidNew;               // const CLSID*
    public nint PwszLightThemeLogoSvg;   // LPCWSTR
    public nint PwszDarkThemeLogoSvg;    // LPCWSTR
    public uint CbAuthenticatorInfo;     // DWORD
    public nint PbAuthenticatorInfo;     // const BYTE*
    public uint CSupportedRpIds;         // DWORD
    public nint PpwszSupportedRpIds;     // const LPCWSTR*
    public nint PwszUserVerificationKeyName; // LPCWSTR (optional, NULL removes)
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginCredentialDetails
{
    public uint CbCredentialId;          // DWORD
    public nint PbCredentialId;          // const BYTE*
    public nint PwszRpId;                // LPCWSTR
    public nint PwszRpName;              // LPCWSTR
    public uint CbUserId;                // DWORD
    public nint PbUserId;                // const BYTE*
    public nint PwszUserName;            // LPCWSTR
    public nint PwszUserDisplayName;     // LPCWSTR
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginUserVerificationRequest
{
    public nint HWnd;                    // HWND
    public nint RguidTransactionId;      // REFGUID (pointer to GUID)
    public nint PwszUsername;            // LPCWSTR
    public nint PwszDisplayHint;         // LPCWSTR
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnPluginUserVerificationRequest2
{
    public nint HWnd;                    // HWND
    public nint PGuidTransactionId;      // const GUID*
    public nint PwszUsername;            // LPCWSTR
    public nint PwszDisplayHint;         // LPCWSTR
    public uint CbBufferToSign;          // DWORD
    public nint PbBufferToSign;          // PBYTE
}

// ---- webauthn.h types ----

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnRpEntityInformation
{
    public uint DwVersion;               // DWORD
    public nint PwszId;                  // PCWSTR
    public nint PwszName;                // PCWSTR
    public nint PwszIcon;                // PCWSTR
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnUserEntityInformation
{
    public uint DwVersion;               // DWORD
    public uint CbId;                    // DWORD
    public nint PbId;                    // PBYTE
    public nint PwszName;                // PCWSTR
    public nint PwszIcon;                // PCWSTR
    public nint PwszDisplayName;         // PCWSTR
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCoseCredentialParameter
{
    public uint DwVersion;               // DWORD
    public nint PwszCredentialType;      // LPCWSTR
    public int LAlg;                     // LONG
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCoseCredentialParameters
{
    public uint CCredentialParameters;   // DWORD
    public nint PCredentialParameters;   // PWEBAUTHN_COSE_CREDENTIAL_PARAMETER
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCredential
{
    public uint DwVersion;               // DWORD
    public uint CbId;                    // DWORD
    public nint PbId;                    // PBYTE
    public nint PwszCredentialType;      // LPCWSTR
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCredentials
{
    public uint CCredentials;            // DWORD
    public nint PCredentials;            // PWEBAUTHN_CREDENTIAL
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCredentialEx
{
    public uint DwVersion;               // DWORD
    public uint CbId;                    // DWORD
    public nint PbId;                    // PBYTE
    public nint PwszCredentialType;      // LPCWSTR
    public uint DwTransports;            // DWORD
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCredentialList
{
    public uint CCredentials;            // DWORD
    public nint PpCredentials;           // PWEBAUTHN_CREDENTIAL_EX*
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnExtension
{
    public nint PwszExtensionIdentifier; // LPCWSTR
    public uint CbExtension;             // DWORD
    public nint PvExtension;             // PVOID
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnExtensions
{
    public uint CExtensions;             // DWORD
    public nint PExtensions;             // PWEBAUTHN_EXTENSION
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnHmacSecretSalt
{
    public uint CbFirst;                 // DWORD
    public nint PbFirst;                 // PBYTE
    public uint CbSecond;                // DWORD
    public nint PbSecond;                // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnX5c
{
    public uint CbData;                  // DWORD
    public nint PbData;                  // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCommonAttestation
{
    public uint DwVersion;               // DWORD
    public nint PwszAlg;                 // PCWSTR
    public int LAlg;                     // LONG
    public uint CbSignature;             // DWORD
    public nint PbSignature;             // PBYTE
    public uint CX5c;                    // DWORD
    public nint PX5c;                    // PWEBAUTHN_X5C
    public nint PwszVer;                 // PCWSTR
    public uint CbCertInfo;              // DWORD
    public nint PbCertInfo;              // PBYTE
    public uint CbPubArea;               // DWORD
    public nint PbPubArea;               // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCredentialAttestation
{
    public uint DwVersion;               // DWORD (up to v8)
    public nint PwszFormatType;          // PCWSTR
    public uint CbAuthenticatorData;     // DWORD
    public nint PbAuthenticatorData;     // PBYTE
    public uint CbAttestation;           // DWORD
    public nint PbAttestation;           // PBYTE
    public uint DwAttestationDecodeType; // DWORD
    public nint PvAttestationDecode;     // PVOID (PWEBAUTHN_COMMON_ATTESTATION when decode type == COMMON)
    public uint CbAttestationObject;     // DWORD
    public nint PbAttestationObject;     // PBYTE
    public uint CbCredentialId;          // DWORD
    public nint PbCredentialId;          // PBYTE
    public WebAuthnExtensions Extensions;
    public uint DwUsedTransport;         // DWORD (v2+)
    public int BEpAtt;                   // BOOL (v3+)
    public int BLargeBlobSupported;      // BOOL
    public int BResidentKey;             // BOOL
    public int BPrfEnabled;              // BOOL (v5+)
    public uint CbUnsignedExtensionOutputs; // DWORD (v6+)
    public nint PbUnsignedExtensionOutputs;
    public nint PHmacSecret;             // PWEBAUTHN_HMAC_SECRET_SALT (v7+)
    public int BThirdPartyPayment;       // BOOL
    public uint DwTransports;            // DWORD (v8+)
    public uint CbClientDataJSON;        // DWORD
    public nint PbClientDataJSON;        // PBYTE
    public uint CbRegistrationResponseJSON; // DWORD
    public nint PbRegistrationResponseJSON; // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnAssertion
{
    public uint DwVersion;               // DWORD (up to v6)
    public uint CbAuthenticatorData;     // DWORD
    public nint PbAuthenticatorData;     // PBYTE
    public uint CbSignature;             // DWORD
    public nint PbSignature;             // PBYTE
    public WebAuthnCredential Credential;
    public uint CbUserId;                // DWORD
    public nint PbUserId;                // PBYTE
    public WebAuthnExtensions Extensions;
    public uint CbCredLargeBlob;         // DWORD
    public nint PbCredLargeBlob;         // PBYTE
    public uint DwCredLargeBlobStatus;   // DWORD
    public nint PHmacSecret;             // PWEBAUTHN_HMAC_SECRET_SALT (v3+)
    public uint DwUsedTransport;         // DWORD (v4+)
    public uint CbUnsignedExtensionOutputs; // DWORD (v5+)
    public nint PbUnsignedExtensionOutputs;
    public uint CbClientDataJSON;        // DWORD (v6+)
    public nint PbClientDataJSON;        // PBYTE
    public uint CbAuthenticationResponseJSON; // DWORD
    public nint PbAuthenticationResponseJSON; // PBYTE
}

// ---- webauthnplugin.h CTAP CBOR types (VERSION_1 / current) ----

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborAuthenticatorOptions
{
    public uint DwVersion;               // DWORD
    public int LUp;                      // LONG
    public int LUv;                      // LONG
    public int LRequireResidentKey;      // LONG
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborEccPublicKey
{
    public uint DwVersion;               // DWORD
    public int LKty;                     // LONG
    public int LAlg;                     // LONG
    public int LCrv;                     // LONG
    public uint CbX;                     // DWORD
    public nint PbX;                     // PBYTE
    public uint CbY;                     // DWORD
    public nint PbY;                     // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborHmacSaltExtension
{
    public uint DwVersion;               // DWORD
    public nint PKeyAgreement;           // PWEBAUTHN_CTAPCBOR_ECC_PUBLIC_KEY
    public uint CbEncryptedSalt;         // DWORD
    public nint PbEncryptedSalt;         // PBYTE
    public uint CbSaltAuth;              // DWORD
    public nint PbSaltAuth;              // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborMakeCredentialRequest
{
    public uint DwVersion;               // DWORD (v1 / current)
    public uint CbRpId;                  // DWORD
    public nint PbRpId;                  // PBYTE
    public uint CbClientDataHash;        // DWORD
    public nint PbClientDataHash;        // PBYTE
    public nint PRpInformation;          // PCWEBAUTHN_RP_ENTITY_INFORMATION
    public nint PUserInformation;        // PCWEBAUTHN_USER_ENTITY_INFORMATION
    public WebAuthnCoseCredentialParameters WebAuthNCredentialParameters;
    public WebAuthnCredentialList CredentialList;
    public uint CbCborExtensionsMap;     // DWORD
    public nint PbCborExtensionsMap;     // PBYTE
    public nint PAuthenticatorOptions;   // PWEBAUTHN_CTAPCBOR_AUTHENTICATOR_OPTIONS
    public int FEmptyPinAuth;            // BOOL
    public uint CbPinAuth;               // DWORD
    public nint PbPinAuth;               // PBYTE
    public int LHmacSecretExt;           // LONG
    public nint PHmacSecretMcExtension;  // PWEBAUTHN_CTAPCBOR_HMAC_SALT_EXTENSION
    public int LPrfExt;                  // LONG
    public uint CbHmacSecretSaltValues;  // DWORD
    public nint PbHmacSecretSaltValues;  // PBYTE
    public uint DwCredProtect;           // DWORD
    public uint DwPinProtocol;           // DWORD
    public uint DwEnterpriseAttestation; // DWORD
    public uint CbCredBlobExt;           // DWORD
    public nint PbCredBlobExt;           // PBYTE
    public int LLargeBlobKeyExt;         // LONG
    public uint DwLargeBlobSupport;      // DWORD
    public int LMinPinLengthExt;         // LONG
    public uint CbJsonExt;               // DWORD
    public nint PbJsonExt;               // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborGetAssertionRequest
{
    public uint DwVersion;               // DWORD (v1 / current)
    public nint PwszRpId;                // PCWSTR
    public uint CbRpId;                  // DWORD
    public nint PbRpId;                  // PBYTE
    public uint CbClientDataHash;        // DWORD
    public nint PbClientDataHash;        // PBYTE
    public WebAuthnCredentialList CredentialList;
    public uint CbCborExtensionsMap;     // DWORD
    public nint PbCborExtensionsMap;     // PBYTE
    public nint PAuthenticatorOptions;   // PWEBAUTHN_CTAPCBOR_AUTHENTICATOR_OPTIONS
    public int FEmptyPinAuth;            // BOOL
    public uint CbPinAuth;               // DWORD
    public nint PbPinAuth;               // PBYTE
    public nint PHmacSaltExtension;      // PWEBAUTHN_CTAPCBOR_HMAC_SALT_EXTENSION
    public uint CbHmacSecretSaltValues;  // DWORD
    public nint PbHmacSecretSaltValues;  // PBYTE
    public uint DwPinProtocol;           // DWORD
    public int LCredBlobExt;             // LONG
    public int LLargeBlobKeyExt;         // LONG
    public uint DwCredLargeBlobOperation; // DWORD
    public uint CbCredLargeBlobCompressed; // DWORD
    public nint PbCredLargeBlobCompressed; // PBYTE
    public uint DwCredLargeBlobOriginalSize; // DWORD
    public uint CbJsonExt;               // DWORD
    public nint PbJsonExt;               // PBYTE
}

[StructLayout(LayoutKind.Sequential)]
internal struct WebAuthnCtapCborGetAssertionResponse
{
    public WebAuthnAssertion WebAuthNAssertion;  // WEBAUTHN_ASSERTION (up to v6)
    public nint PUserInformation;        // PCWEBAUTHN_USER_ENTITY_INFORMATION (optional)
    public uint DwNumberOfCredentials;   // DWORD
    public int LUserSelected;            // LONG
    public uint CbLargeBlobKey;          // DWORD
    public nint PbLargeBlobKey;          // PBYTE
    public uint CbUnsignedExtensionOutputs; // DWORD
    public nint PbUnsignedExtensionOutputs; // PBYTE
}
