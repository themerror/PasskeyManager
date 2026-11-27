using System.Globalization;
using System.Runtime.InteropServices;
using PasskeyManager.Interop;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Authentication.WebAuthn;

namespace PasskeyManager.Services;

/// <summary>
/// Registers / unregisters / updates the plugin authenticator with the Windows
/// WebAuthN platform and refreshes its registration state. Ported from the
/// official PasskeyManager C++ sample (PluginRegistrationManager.cpp).
/// P/Invoke bindings and native types are CsWin32-generated
/// (Windows.Win32.PInvoke / Windows.Win32.Security.Authentication.WebAuthn,
/// see NativeMethods.txt).
/// </summary>
public sealed class PluginRegistrationService
{
    /// <summary>Authenticator info CBOR prefix (up to and including the h'AAGUID' value header).</summary>
    private const string AuthenticatorInfoHexPart1 =
        "A60182684649444F5F325F30684649444F5F325F310282637072666B686D61632D7365637265740350";

    /// <summary>Authenticator info CBOR suffix (options, transports, algorithms).</summary>
    private const string AuthenticatorInfoHexPart2 =
        "04A362726BF5627570F5627576F5098168696E7465726E616C0A81A263616C672664747970656A7075626C69632D6B6579";

    public int Register()
    {
        byte[] authenticatorInfo = BuildAuthenticatorInfo();

        Guid clsid = PluginConstants.PluginClsid;
        unsafe
        {
            Guid* pClsid = &clsid;
            var options = new WEBAUTHN_PLUGIN_ADD_AUTHENTICATOR_OPTIONS
            {
                pwszAuthenticatorName = ToNativeString(PluginConstants.PluginName),
                rclsid = pClsid,
                pwszPluginRpId = ToNativeString(PluginConstants.PluginRpId),
                pwszLightThemeLogoSvg = default, // Logo optional -> null
                pwszDarkThemeLogoSvg = default,
                cbAuthenticatorInfo = (uint)authenticatorInfo.Length,
                pbAuthenticatorInfo = null,
                cSupportedRpIds = 0,
                ppwszSupportedRpIds = null,
            };

            WEBAUTHN_PLUGIN_ADD_AUTHENTICATOR_RESPONSE* response = null;
            try
            {
                options.pbAuthenticatorInfo = (byte*)Marshal.AllocHGlobal(authenticatorInfo.Length);
                Marshal.Copy(authenticatorInfo, 0, (nint)options.pbAuthenticatorInfo, authenticatorInfo.Length);

                int hr = PInvoke.WebAuthNPluginAddAuthenticator(in options, out response);
                if (hr != PluginConstants.S_OK)
                {
                    return hr;
                }

                // The operation signing public key is intentionally not persisted
                // to the registry in this implementation; it is owned by the lane
                // that handles plugin operation signing.
                return PluginConstants.S_OK;
            }
            finally
            {
                // Always free the add response and temporary unmanaged memory.
                if (response != null)
                {
                    PInvoke.WebAuthNPluginFreeAddAuthenticatorResponse(response);
                }

                Marshal.FreeHGlobal((nint)(char*)options.pwszAuthenticatorName);
                Marshal.FreeHGlobal((nint)(char*)options.pwszPluginRpId);
                if (options.pbAuthenticatorInfo != null)
                {
                    Marshal.FreeHGlobal((nint)options.pbAuthenticatorInfo);
                }
            }
        }
    }

    public int Unregister()
    {
        Guid clsid = PluginConstants.PluginClsid;
        return PInvoke.WebAuthNPluginRemoveAuthenticator(in clsid);
    }

    public int Update()
    {
        byte[] authenticatorInfo = BuildAuthenticatorInfo();

        Guid clsid = PluginConstants.PluginClsid;
        unsafe
        {
            Guid* pClsid = &clsid;
            var updateDetails = new WEBAUTHN_PLUGIN_UPDATE_AUTHENTICATOR_DETAILS
            {
                pwszAuthenticatorName = ToNativeString(PluginConstants.PluginName),
                rclsid = pClsid,
                rclsidNew = pClsid,
                pwszLightThemeLogoSvg = default, // Logo optional -> null
                pwszDarkThemeLogoSvg = default,
                cbAuthenticatorInfo = (uint)authenticatorInfo.Length,
                pbAuthenticatorInfo = null,
                cSupportedRpIds = 0,
                ppwszSupportedRpIds = null,
            };

            try
            {
                updateDetails.pbAuthenticatorInfo = (byte*)Marshal.AllocHGlobal(authenticatorInfo.Length);
                Marshal.Copy(authenticatorInfo, 0, (nint)updateDetails.pbAuthenticatorInfo, authenticatorInfo.Length);

                return PInvoke.WebAuthNPluginUpdateAuthenticatorDetails(in updateDetails);
            }
            finally
            {
                Marshal.FreeHGlobal((nint)(char*)updateDetails.pwszAuthenticatorName);
                if (updateDetails.pbAuthenticatorInfo != null)
                {
                    Marshal.FreeHGlobal((nint)updateDetails.pbAuthenticatorInfo);
                }
            }
        }
    }

    public int Refresh(out bool isRegistered, out int state)
    {
        isRegistered = false;
        state = (int)AUTHENTICATOR_STATE.AuthenticatorState_Disabled;

        Guid clsid = PluginConstants.PluginClsid;
        int hr = PInvoke.WebAuthNPluginGetAuthenticatorState(in clsid, out AUTHENTICATOR_STATE pluginState);
        if (hr != PluginConstants.S_OK)
        {
            return hr;
        }

        isRegistered = true;
        state = (int)pluginState;
        return PluginConstants.S_OK;
    }

    /// <summary>Allocates a CoTaskMem UTF-16 string usable as a PCWSTR field value.</summary>
    private static unsafe PCWSTR ToNativeString(string value) =>
        (char*)Marshal.StringToCoTaskMemUni(value);

    /// <summary>
    /// Builds the CTAP CBOR authenticatorGetInfo blob:
    /// {1: ["FIDO_2_0","FIDO_2_1"], 2: ["prf","hmac-secret"], 3: h'AAGUID',
    ///  4: {"rk": true, "up": true, "uv": true}, 9: ["internal"], 10: [{"alg": -7, "type": "public-key"}]}
    /// using this lane's AAGUID value.
    /// </summary>
    private static byte[] BuildAuthenticatorInfo()
    {
        string hex = AuthenticatorInfoHexPart1
            + PluginConstants.PluginAaguidHex
            + AuthenticatorInfoHexPart2;

        var bytes = new byte[hex.Length / 2];
        for (int i = 0; i < bytes.Length; i++)
        {
            bytes[i] = byte.Parse(hex.AsSpan(i * 2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        return bytes;
    }
}
