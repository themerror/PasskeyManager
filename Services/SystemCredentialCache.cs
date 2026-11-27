using System.Runtime.InteropServices;
using PasskeyManager.Interop;
using PasskeyManager.Models;
using Windows.Win32;
using Windows.Win32.Security.Authentication.WebAuthn;

namespace PasskeyManager.Services;

/// <summary>
/// Syncs credential metadata with the platform's plugin credential cache used
/// for browser autofill scenarios. Ported from PluginCredentialManager.cpp
/// (WebAuthNPluginAuthenticator*Credentials APIs). StoredCredential.KeyName is
/// not part of the native cache structure.
/// P/Invoke bindings and native types are CsWin32-generated
/// (Windows.Win32.PInvoke / Windows.Win32.Security.Authentication.WebAuthn,
/// see NativeMethods.txt).
/// </summary>
public sealed class SystemCredentialCache
{
    public unsafe int GetAll(out List<StoredCredential> credentials)
    {
        credentials = [];

        Guid clsid = PluginConstants.PluginClsid;
        int hr = PInvoke.WebAuthNPluginAuthenticatorGetAllCredentials(
            in clsid,
            out uint count,
            out WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS* arrayPtr);
        if (hr == PluginConstants.HResultErrorFileNotFound)
        {
            // No cached credentials is a success with an empty list.
            return PluginConstants.S_OK;
        }
        if (hr != PluginConstants.S_OK)
        {
            return hr;
        }

        try
        {
            for (uint i = 0; i < count; i++)
            {
                WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS native = arrayPtr[i];

                // Skip invalid entries, mirroring the C++ sample validation.
                if (native.pbCredentialId == null || native.cbCredentialId == 0 ||
                    native.pbUserId == null || native.cbUserId == 0 ||
                    native.pwszRpId.Value == null || native.pwszRpName.Value == null ||
                    native.pwszUserName.Value == null || native.pwszUserDisplayName.Value == null)
                {
                    continue;
                }

                credentials.Add(new StoredCredential
                {
                    CredentialId = CopyBytes(native.pbCredentialId, native.cbCredentialId),
                    UserId = CopyBytes(native.pbUserId, native.cbUserId),
                    RpId = native.pwszRpId.ToString() ?? string.Empty,
                    RpName = native.pwszRpName.ToString() ?? string.Empty,
                    UserName = native.pwszUserName.ToString() ?? string.Empty,
                    DisplayName = native.pwszUserDisplayName.ToString() ?? string.Empty,
                    KeyName = string.Empty,
                    CreatedAt = DateTimeOffset.Now,
                });
            }

            return PluginConstants.S_OK;
        }
        finally
        {
            // Single unified free for the whole returned array.
            PInvoke.WebAuthNPluginAuthenticatorFreeCredentialDetailsArray(count, arrayPtr);
        }
    }

    public unsafe int Add(IEnumerable<StoredCredential> credentials) => InvokeWithNativeArray(credentials, AddNative);

    public unsafe int Remove(IEnumerable<StoredCredential> credentials) => InvokeWithNativeArray(credentials, RemoveNative);

    private static unsafe int AddNative(in Guid clsid, uint count, WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS* details) =>
        PInvoke.WebAuthNPluginAuthenticatorAddCredentials(in clsid, new ReadOnlySpan<WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS>(details, (int)count));

    private static unsafe int RemoveNative(in Guid clsid, uint count, WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS* details) =>
        PInvoke.WebAuthNPluginAuthenticatorRemoveCredentials(in clsid, new ReadOnlySpan<WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS>(details, (int)count));

    public unsafe int RemoveAll()
    {
        Guid clsid = PluginConstants.PluginClsid;
        return PInvoke.WebAuthNPluginAuthenticatorRemoveAllCredentials(in clsid);
    }

    private unsafe delegate int NativeArrayInvoker(in Guid clsid, uint count, WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS* details);

    private static unsafe int InvokeWithNativeArray(
        IEnumerable<StoredCredential> credentials,
        NativeArrayInvoker invoke)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(invoke);

        var list = credentials.ToList();
        if (list.Count == 0)
        {
            return PluginConstants.S_OK;
        }

        int structSize = sizeof(WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS);

        // Single unmanaged block: contiguous struct array followed by all
        // byte/wchar payload. The whole block stays valid for the call.
        nint block = 0;
        try
        {
            int payloadSize = ComputePayloadSize(list);
            block = Marshal.AllocHGlobal(checked(list.Count * structSize + payloadSize));

            nint payload = block + list.Count * structSize;
            for (int i = 0; i < list.Count; i++)
            {
                var cred = list[i];
                var native = (WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS*)(block + i * structSize);
                *native = new WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS
                {
                    cbCredentialId = (uint)cred.CredentialId.Length,
                    pbCredentialId = WriteBytes(ref payload, cred.CredentialId),
                    pwszRpId = WriteString(ref payload, cred.RpId),
                    pwszRpName = WriteString(ref payload, cred.RpName),
                    cbUserId = (uint)cred.UserId.Length,
                    pbUserId = WriteBytes(ref payload, cred.UserId),
                    pwszUserName = WriteString(ref payload, cred.UserName),
                    pwszUserDisplayName = WriteString(ref payload, cred.DisplayName),
                };
            }

            return invoke(PluginConstants.PluginClsid, (uint)list.Count, (WEBAUTHN_PLUGIN_CREDENTIAL_DETAILS*)block);
        }
        finally
        {
            if (block != 0)
            {
                Marshal.FreeHGlobal(block);
            }
        }
    }

    private static int ComputePayloadSize(List<StoredCredential> list)
    {
        int size = 0;
        foreach (var cred in list)
        {
            size += cred.CredentialId.Length;
            size += cred.UserId.Length;
            size += StringByteSize(cred.RpId);
            size += StringByteSize(cred.RpName);
            size += StringByteSize(cred.UserName);
            size += StringByteSize(cred.DisplayName);
        }
        return size;
    }

    private static int StringByteSize(string value) => (value.Length + 1) * sizeof(char);

    private static unsafe byte* WriteBytes(ref nint payload, byte[] data)
    {
        if (data.Length == 0)
        {
            return null;
        }

        nint dest = payload;
        Marshal.Copy(data, 0, dest, data.Length);
        payload += data.Length;
        return (byte*)dest;
    }

    private static unsafe Windows.Win32.Foundation.PCWSTR WriteString(ref nint payload, string value)
    {
        nint dest = payload;
        Marshal.Copy((value + '\0').ToCharArray(), 0, dest, value.Length + 1);
        payload += (value.Length + 1) * sizeof(char);
        return (char*)dest;
    }

    private static unsafe byte[] CopyBytes(byte* src, uint count)
    {
        var bytes = new byte[count];
        Marshal.Copy((nint)src, bytes, 0, (int)count);
        return bytes;
    }
}
