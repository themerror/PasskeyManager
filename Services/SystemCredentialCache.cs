using System.Runtime.InteropServices;
using PasskeyManager.Interop;
using PasskeyManager.Models;

namespace PasskeyManager.Services;

/// <summary>
/// Syncs credential metadata with the platform's plugin credential cache used
/// for browser autofill scenarios. Ported from PluginCredentialManager.cpp
/// (WebAuthNPluginAuthenticator*Credentials APIs). StoredCredential.KeyName is
/// not part of the native cache structure.
/// </summary>
public sealed class SystemCredentialCache
{
    public int GetAll(out List<StoredCredential> credentials)
    {
        credentials = [];

        int hr = WebAuthnNative.WebAuthNPluginAuthenticatorGetAllCredentials(
            PluginConstants.PluginClsid,
            out uint count,
            out nint arrayPtr);
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
            int structSize = Marshal.SizeOf<WebAuthnPluginCredentialDetails>();
            for (uint i = 0; i < count; i++)
            {
                var native = Marshal.PtrToStructure<WebAuthnPluginCredentialDetails>(
                    arrayPtr + (nint)(i * (uint)structSize));

                // Skip invalid entries, mirroring the C++ sample validation.
                if (native.PbCredentialId == 0 || native.CbCredentialId == 0 ||
                    native.PbUserId == 0 || native.CbUserId == 0 ||
                    native.PwszRpId == 0 || native.PwszRpName == 0 ||
                    native.PwszUserName == 0 || native.PwszUserDisplayName == 0)
                {
                    continue;
                }

                credentials.Add(new StoredCredential
                {
                    CredentialId = CopyBytes(native.PbCredentialId, native.CbCredentialId),
                    UserId = CopyBytes(native.PbUserId, native.CbUserId),
                    RpId = Marshal.PtrToStringUni(native.PwszRpId) ?? string.Empty,
                    RpName = Marshal.PtrToStringUni(native.PwszRpName) ?? string.Empty,
                    UserName = Marshal.PtrToStringUni(native.PwszUserName) ?? string.Empty,
                    DisplayName = Marshal.PtrToStringUni(native.PwszUserDisplayName) ?? string.Empty,
                    KeyName = string.Empty,
                    CreatedAt = DateTimeOffset.Now,
                });
            }

            return PluginConstants.S_OK;
        }
        finally
        {
            // Single unified free for the whole returned array.
            WebAuthnNative.WebAuthNPluginAuthenticatorFreeCredentialDetailsArray(count, arrayPtr);
        }
    }

    public int Add(IEnumerable<StoredCredential> credentials)
    {
        return InvokeWithNativeArray(
            credentials,
            (clsid, count, ptr) => WebAuthnNative.WebAuthNPluginAuthenticatorAddCredentials(in clsid, count, ptr));
    }

    public int Remove(IEnumerable<StoredCredential> credentials)
    {
        return InvokeWithNativeArray(
            credentials,
            (clsid, count, ptr) => WebAuthnNative.WebAuthNPluginAuthenticatorRemoveCredentials(in clsid, count, ptr));
    }

    public int RemoveAll()
    {
        return WebAuthnNative.WebAuthNPluginAuthenticatorRemoveAllCredentials(PluginConstants.PluginClsid);
    }

    private static int InvokeWithNativeArray(
        IEnumerable<StoredCredential> credentials,
        Func<Guid, uint, nint, int> invoke)
    {
        ArgumentNullException.ThrowIfNull(credentials);
        ArgumentNullException.ThrowIfNull(invoke);

        var list = credentials.ToList();
        if (list.Count == 0)
        {
            return PluginConstants.S_OK;
        }

        int structSize = Marshal.SizeOf<WebAuthnPluginCredentialDetails>();

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

                var native = new WebAuthnPluginCredentialDetails
                {
                    CbCredentialId = (uint)cred.CredentialId.Length,
                    PbCredentialId = WriteBytes(ref payload, cred.CredentialId),
                    PwszRpId = WriteString(ref payload, cred.RpId),
                    PwszRpName = WriteString(ref payload, cred.RpName),
                    CbUserId = (uint)cred.UserId.Length,
                    PbUserId = WriteBytes(ref payload, cred.UserId),
                    PwszUserName = WriteString(ref payload, cred.UserName),
                    PwszUserDisplayName = WriteString(ref payload, cred.DisplayName),
                };

                Marshal.StructureToPtr(native, block + i * structSize, false);
            }

            return invoke(PluginConstants.PluginClsid, (uint)list.Count, block);
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

    private static nint WriteBytes(ref nint payload, byte[] data)
    {
        if (data.Length == 0)
        {
            return 0;
        }

        nint dest = payload;
        Marshal.Copy(data, 0, dest, data.Length);
        payload += data.Length;
        return dest;
    }

    private static nint WriteString(ref nint payload, string value)
    {
        nint dest = payload;
        Marshal.Copy((value + '\0').ToCharArray(), 0, dest, value.Length + 1);
        payload += (value.Length + 1) * sizeof(char);
        return dest;
    }

    private static byte[] CopyBytes(nint src, uint count)
    {
        var bytes = new byte[count];
        Marshal.Copy(src, bytes, 0, (int)count);
        return bytes;
    }
}
