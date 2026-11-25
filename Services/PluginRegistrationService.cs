using System.Globalization;
using System.Runtime.InteropServices;
using PasskeyManager.Interop;

namespace PasskeyManager.Services;

/// <summary>
/// Registers / unregisters / updates the plugin authenticator with the Windows
/// WebAuthN platform and refreshes its registration state. Ported from the
/// official PasskeyManager C++ sample (PluginRegistrationManager.cpp).
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

        var clsid = PluginConstants.PluginClsid;
        var options = new WebAuthnPluginAddAuthenticatorOptions
        {
            PwszAuthenticatorName = Marshal.StringToCoTaskMemUni(PluginConstants.PluginName),
            Rclsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>()),
            PwszPluginRpId = Marshal.StringToCoTaskMemUni(PluginConstants.PluginRpId),
            PwszLightThemeLogoSvg = 0, // Logo optional -> null
            PwszDarkThemeLogoSvg = 0,
            CbAuthenticatorInfo = (uint)authenticatorInfo.Length,
            PbAuthenticatorInfo = Marshal.AllocHGlobal(authenticatorInfo.Length),
            CSupportedRpIds = 0,
            PpwszSupportedRpIds = 0,
        };

        nint response = 0;
        try
        {
            Marshal.StructureToPtr(clsid, options.Rclsid, false);
            Marshal.Copy(authenticatorInfo, 0, options.PbAuthenticatorInfo, authenticatorInfo.Length);

            int hr = WebAuthnNative.WebAuthNPluginAddAuthenticator(options, out response);
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
            if (response != 0)
            {
                WebAuthnNative.WebAuthNPluginFreeAddAuthenticatorResponse(response);
            }

            Marshal.FreeHGlobal(options.PwszAuthenticatorName);
            Marshal.FreeHGlobal(options.Rclsid);
            Marshal.FreeHGlobal(options.PwszPluginRpId);
            Marshal.FreeHGlobal(options.PbAuthenticatorInfo);
        }
    }

    public int Unregister()
    {
        return WebAuthnNative.WebAuthNPluginRemoveAuthenticator(PluginConstants.PluginClsid);
    }

    public int Update()
    {
        byte[] authenticatorInfo = BuildAuthenticatorInfo();

        var clsid = PluginConstants.PluginClsid;
        var updateDetails = new WebAuthnPluginUpdateAuthenticatorDetails
        {
            PwszAuthenticatorName = Marshal.StringToCoTaskMemUni(PluginConstants.PluginName),
            Rclsid = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>()),
            RclsidNew = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>()),
            PwszLightThemeLogoSvg = 0, // Logo optional -> null
            PwszDarkThemeLogoSvg = 0,
            CbAuthenticatorInfo = (uint)authenticatorInfo.Length,
            PbAuthenticatorInfo = Marshal.AllocHGlobal(authenticatorInfo.Length),
            CSupportedRpIds = 0,
            PpwszSupportedRpIds = 0,
        };

        try
        {
            Marshal.StructureToPtr(clsid, updateDetails.Rclsid, false);
            Marshal.StructureToPtr(clsid, updateDetails.RclsidNew, false);
            Marshal.Copy(authenticatorInfo, 0, updateDetails.PbAuthenticatorInfo, authenticatorInfo.Length);

            return WebAuthnNative.WebAuthNPluginUpdateAuthenticatorDetails(updateDetails);
        }
        finally
        {
            Marshal.FreeHGlobal(updateDetails.PwszAuthenticatorName);
            Marshal.FreeHGlobal(updateDetails.Rclsid);
            Marshal.FreeHGlobal(updateDetails.RclsidNew);
            Marshal.FreeHGlobal(updateDetails.PbAuthenticatorInfo);
        }
    }

    public int Refresh(out bool isRegistered, out int state)
    {
        isRegistered = false;
        state = (int)AuthenticatorState.Disabled;

        int hr = WebAuthnNative.WebAuthNPluginGetAuthenticatorState(
            PluginConstants.PluginClsid,
            out AuthenticatorState pluginState);
        if (hr != PluginConstants.S_OK)
        {
            return hr;
        }

        isRegistered = true;
        state = (int)pluginState;
        return PluginConstants.S_OK;
    }

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
