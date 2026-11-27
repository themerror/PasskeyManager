using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Authentication.WebAuthn;
using PasskeyManager.Interop;
using PasskeyManager.Models;
using System.Runtime.InteropServices;

namespace PasskeyManager.Services;

/// <summary>
/// Native COM entry point called by Windows. It validates signed platform requests,
/// obtains Windows Hello UV, uses CNG keys and returns officially encoded WebAuthn responses.
/// </summary>
[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class PluginAuthenticator : IPluginAuthenticator
{
    private const int ErrorBusy = unchecked((int)0x800700AA);
    private const int InvalidArgument = unchecked((int)0x80070057);
    private const int Unexpected = unchecked((int)0x8000FFFF);
    private const int NteBadSignature = unchecked((int)0x80090006);
    private const int NteNotFound = unchecked((int)0x80090011);
    private const int NteNotSupported = unchecked((int)0x80090029);
    private const int NteUserCancelled = unchecked((int)0x80090036);
    private static int _operationInProgress;

    public unsafe WEBAUTHN_PLUGIN_OPERATION_RESPONSE MakeCredential(WEBAUTHN_PLUGIN_OPERATION_REQUEST* requestPointer)
    {
        var response = new WEBAUTHN_PLUGIN_OPERATION_RESPONSE();
        var hresult = RunOperation((nint)requestPointer, (nint)(&response), MakeCredentialCore);
        if (hresult < 0)
        {
            throw new COMException($"MakeCredential failed with HRESULT 0x{hresult:X8}", hresult);
        }
        return response;
    }

    public unsafe WEBAUTHN_PLUGIN_OPERATION_RESPONSE GetAssertion(WEBAUTHN_PLUGIN_OPERATION_REQUEST* requestPointer)
    {
        var response = new WEBAUTHN_PLUGIN_OPERATION_RESPONSE();
        var hresult = RunOperation((nint)requestPointer, (nint)(&response), GetAssertionCore);
        if (hresult < 0)
        {
            throw new COMException($"GetAssertion failed with HRESULT 0x{hresult:X8}", hresult);
        }
        return response;
    }

    public unsafe void CancelOperation(WEBAUTHN_PLUGIN_CANCEL_OPERATION_REQUEST* requestPointer)
    {
        if (requestPointer is null)
        {
            throw new COMException("Request pointer is null.", InvalidArgument);
        }
        var request = Marshal.PtrToStructure<WEBAUTHN_PLUGIN_CANCEL_OPERATION_REQUEST>((nint)requestPointer);
        var hresult = PluginOperationCoordinator.Cancel(request.transactionId) ? 0 : NteNotFound;
        if (hresult < 0)
        {
            throw new COMException($"CancelOperation failed with HRESULT 0x{hresult:X8}", hresult);
        }
    }

    public PLUGIN_LOCK_STATUS GetLockStatus() =>
        PluginSettings.IsVaultLocked() ? PLUGIN_LOCK_STATUS.PluginLocked : PLUGIN_LOCK_STATUS.PluginUnlocked;

    private static int RunOperation(
        nint requestPointer,
        nint responsePointer,
        Func<nint, nint, int> operation)
    {
        if (requestPointer == nint.Zero || responsePointer == nint.Zero)
        {
            return InvalidArgument;
        }

        Marshal.StructureToPtr(new WEBAUTHN_PLUGIN_OPERATION_RESPONSE(), responsePointer, false);
        if (Interlocked.CompareExchange(ref _operationInProgress, 1, 0) != 0)
        {
            return ErrorBusy;
        }

        var hresult = 0;
        try
        {
            hresult = operation(requestPointer, responsePointer);
            return hresult;
        }
        catch (HResultException ex)
        {
            hresult = ex.HResult;
            return hresult;
        }
        catch (Exception ex)
        {
            hresult = Marshal.GetHRForException(ex);
            PluginLog.Add(AppStrings.Format("LogUnhandledPluginException", ex.GetType().Name, ex.Message), "Error", hresult);
            return hresult;
        }
        finally
        {
            PluginOperationCoordinator.Complete(hresult);
            Volatile.Write(ref _operationInProgress, 0);
        }
    }

    private static unsafe int MakeCredentialCore(nint requestPointer, nint responsePointer)
    {
        var request = Marshal.PtrToStructure<WEBAUTHN_PLUGIN_OPERATION_REQUEST>(requestPointer);
        PluginLog.Add(AppStrings.Get("LogMakeCredentialCallback"), "Info");
        ValidateRequest(request);
        var requestBytes = CopyBytes(request.pbEncodedRequest, request.cbEncodedRequest);
        VerifyPlatformRequest(request, requestBytes);

        Check(PInvoke.WebAuthNDecodeMakeCredentialRequest(
            requestBytes,
            out WEBAUTHN_CTAPCBOR_MAKE_CREDENTIAL_REQUEST* decodedPointer));
        try
        {
            var decoded = Marshal.PtrToStructure<WEBAUTHN_CTAPCBOR_MAKE_CREDENTIAL_REQUEST>((nint)decodedPointer);
            if (decoded.pRpInformation is null || decoded.pUserInformation is null)
            {
                return Unexpected;
            }
            var rp = Marshal.PtrToStructure<WEBAUTHN_RP_ENTITY_INFORMATION>((nint)decoded.pRpInformation);
            var user = Marshal.PtrToStructure<WEBAUTHN_USER_ENTITY_INFORMATION>((nint)decoded.pUserInformation);
            var rpId = rp.pwszId.ToString() ?? throw new HResultException(InvalidArgument);
            var rpName = rp.pwszName.ToString() ?? rpId;
            var userName = user.pwszName.ToString() ?? string.Empty;
            var displayName = user.pwszDisplayName.ToString() ?? userName;
            var userId = CopyBytes(user.pbId, user.cbId);
            if (userId.Length is 0 or > 64)
            {
                return InvalidArgument;
            }
            if (string.Equals(rpId, PluginConstants.PluginRpId, StringComparison.OrdinalIgnoreCase))
            {
                return NteNotSupported;
            }

            var decision = PluginOperationCoordinator.ConfirmMakeCredentialAsync(
                request.transactionId,
                request.hWnd,
                rpId,
                rpName,
                userName).GetAwaiter().GetResult();
            if (!decision.Confirmed)
            {
                return NteUserCancelled;
            }
            if (!decision.VaultGesturePerformed)
            {
                PerformUserVerification(request, requestBytes, userName);
            }

            var rpIdBytes = CopyBytes(decoded.pbRpId, decoded.cbRpId);
            var keyName = WebAuthnCrypto.CreateKeyName();
            using var key = WebAuthnCrypto.CreateKey(keyName);
            var authenticatorData = WebAuthnCrypto.CreateAuthenticatorData(
                key,
                true,
                rpIdBytes,
                out var credentialId);

            byte* encodedResponse;
            uint encodedResponseLength;
            var format = (char*)Marshal.StringToCoTaskMemUni("none");
            try
            {
                fixed (byte* authenticatorDataPointer = authenticatorData)
                {
                    var attestation = new WEBAUTHN_CREDENTIAL_ATTESTATION
                    {
                        dwVersion = 8,
                        pwszFormatType = format,
                        cbAuthenticatorData = (uint)authenticatorData.Length,
                        pbAuthenticatorData = authenticatorDataPointer
                    };
                    Check(PInvoke.WebAuthNEncodeMakeCredentialResponse(
                        attestation,
                        out encodedResponseLength,
                        out encodedResponse));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem((nint)format);
            }

            var stored = new StoredCredential
            {
                CredentialId = credentialId,
                UserId = userId,
                RpId = rpId,
                RpName = rpName,
                UserName = userName,
                DisplayName = displayName,
                KeyName = keyName,
                CreatedAt = DateTimeOffset.Now
            };
            try
            {
                CredentialStore.Shared.AddOrUpdate(stored);
            }
            catch
            {
                WebAuthnCrypto.DeleteKey(keyName);
                Marshal.FreeCoTaskMem((nint)encodedResponse);
                throw;
            }

            var cacheHResult = new SystemCredentialCache().Add(new List<StoredCredential> { stored });
            PluginLog.Add(
                cacheHResult >= 0
                    ? AppStrings.Format("LogCredentialCreatedFormat", rpName)
                    : AppStrings.Format("LogCredentialCacheFailedFormat", cacheHResult),
                cacheHResult >= 0 ? "Success" : "Warning",
                cacheHResult);
            Marshal.StructureToPtr(new WEBAUTHN_PLUGIN_OPERATION_RESPONSE
            {
                cbEncodedResponse = encodedResponseLength,
                pbEncodedResponse = encodedResponse
            }, responsePointer, false);
            return 0;
        }
        finally
        {
            PInvoke.WebAuthNFreeDecodedMakeCredentialRequest(decodedPointer);
        }
    }

    private static unsafe int GetAssertionCore(nint requestPointer, nint responsePointer)
    {
        var request = Marshal.PtrToStructure<WEBAUTHN_PLUGIN_OPERATION_REQUEST>(requestPointer);
        PluginLog.Add(AppStrings.Get("LogGetAssertionCallback"), "Info");
        ValidateRequest(request);
        var requestBytes = CopyBytes(request.pbEncodedRequest, request.cbEncodedRequest);
        VerifyPlatformRequest(request, requestBytes);

        Check(PInvoke.WebAuthNDecodeGetAssertionRequest(
            requestBytes,
            out WEBAUTHN_CTAPCBOR_GET_ASSERTION_REQUEST* decodedPointer));
        try
        {
            var decoded = Marshal.PtrToStructure<WEBAUTHN_CTAPCBOR_GET_ASSERTION_REQUEST>((nint)decodedPointer);
            var rpId = decoded.pwszRpId.ToString() ?? throw new HResultException(InvalidArgument);
            var allowList = ReadAllowList(decoded.CredentialList);
            CredentialStore.Shared.Reload();
            var matches = CredentialStore.Shared.FindMatching(rpId, allowList);
            if (matches.Count == 0)
            {
                return NteNotFound;
            }

            var decision = PluginOperationCoordinator.ConfirmGetAssertionAsync(
                request.transactionId,
                request.hWnd,
                rpId,
                matches).GetAwaiter().GetResult();
            if (!decision.Confirmed || decision.Credential is null)
            {
                return NteUserCancelled;
            }
            var selected = decision.Credential;
            if (!decision.VaultGesturePerformed)
            {
                PerformUserVerification(request, requestBytes, selected.UserName);
            }

            var rpIdBytes = CopyBytes(decoded.pbRpId, decoded.cbRpId);
            var clientDataHash = CopyBytes(decoded.pbClientDataHash, decoded.cbClientDataHash);
            using var key = WebAuthnCrypto.OpenKey(selected.KeyName);
            var authenticatorData = WebAuthnCrypto.CreateAuthenticatorData(
                key,
                false,
                rpIdBytes,
                out var credentialId);
            if (!credentialId.AsSpan().SequenceEqual(selected.CredentialId))
            {
                return Unexpected;
            }
            var signature = WebAuthnCrypto.SignAssertion(key, authenticatorData, clientDataHash);

            byte* encodedResponse;
            uint encodedResponseLength;
            var credentialType = (char*)Marshal.StringToCoTaskMemUni(PluginConstants.CredentialTypePublicKey);
            try
            {
                fixed (byte* authDataPointer = authenticatorData)
                fixed (byte* signaturePointer = signature)
                fixed (byte* credentialIdPointer = selected.CredentialId)
                fixed (byte* userIdPointer = selected.UserId)
                {
                    var userEntity = new WEBAUTHN_USER_ENTITY_INFORMATION
                    {
                        dwVersion = 1,
                        cbId = (uint)selected.UserId.Length,
                        pbId = userIdPointer
                    };
                    var assertion = new WEBAUTHN_ASSERTION
                    {
                        dwVersion = 6,
                        cbAuthenticatorData = (uint)authenticatorData.Length,
                        pbAuthenticatorData = authDataPointer,
                        cbSignature = (uint)signature.Length,
                        pbSignature = signaturePointer,
                        Credential = new WEBAUTHN_CREDENTIAL
                        {
                            dwVersion = 1,
                            cbId = (uint)selected.CredentialId.Length,
                            pbId = credentialIdPointer,
                            pwszCredentialType = credentialType
                        },
                        cbUserId = (uint)selected.UserId.Length,
                        pbUserId = userIdPointer
                    };
                    var ctapResponse = new WEBAUTHN_CTAPCBOR_GET_ASSERTION_RESPONSE
                    {
                        WebAuthNAssertion = assertion,
                        pUserInformation = &userEntity,
                        dwNumberOfCredentials = 1
                    };
                    Check(PInvoke.WebAuthNEncodeGetAssertionResponse(
                        ctapResponse,
                        out encodedResponseLength,
                        out encodedResponse));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem((nint)credentialType);
            }

            Marshal.StructureToPtr(new WEBAUTHN_PLUGIN_OPERATION_RESPONSE
            {
                cbEncodedResponse = encodedResponseLength,
                pbEncodedResponse = encodedResponse
            }, responsePointer, false);
            PluginLog.Add(AppStrings.Format("LogAssertionCreatedFormat", selected.RpName), "Success");
            return 0;
        }
        finally
        {
            PInvoke.WebAuthNFreeDecodedGetAssertionRequest(decodedPointer);
        }
    }

    private static unsafe void ValidateRequest(WEBAUTHN_PLUGIN_OPERATION_REQUEST request)
    {
        if (request.requestType != WEBAUTHN_PLUGIN_REQUEST_TYPE.WEBAUTHN_PLUGIN_REQUEST_TYPE_CTAP2_CBOR
            || request.pbEncodedRequest is null
            || request.cbEncodedRequest == 0)
        {
            throw new HResultException(InvalidArgument);
        }
    }

    private static unsafe void VerifyPlatformRequest(WEBAUTHN_PLUGIN_OPERATION_REQUEST request, byte[] requestBytes)
    {
        var clsid = PluginConstants.PluginClsid;
        Check(PInvoke.WebAuthNPluginGetOperationSigningPublicKey(
            clsid,
            out uint keyLength,
            out byte* keyPointer));
        try
        {
            var publicKey = CopyBytes(keyPointer, keyLength);
            var signature = CopyBytes(request.pbRequestSignature, request.cbRequestSignature);
            if (!WebAuthnCrypto.VerifySignature(requestBytes, publicKey, signature))
            {
                PluginLog.Add(AppStrings.Format("LogPlatformSignatureFailed", publicKey.Length, ReadMagic(publicKey)), "Error", NteBadSignature);
                throw new HResultException(NteBadSignature);
            }
            PluginLog.Add(AppStrings.Get("LogPlatformSignatureSucceeded"), "Success");
        }
        finally
        {
            if (keyPointer is not null)
            {
                PInvoke.WebAuthNPluginFreePublicKeyResponse(keyPointer);
            }
        }
    }

    private static unsafe void PerformUserVerification(
        WEBAUTHN_PLUGIN_OPERATION_REQUEST operationRequest,
        byte[] encodedRequest,
        string userName)
    {
        var clsid = PluginConstants.PluginClsid;
        Check(PInvoke.WebAuthNPluginGetUserVerificationCount(clsid, out _));
        Check(PInvoke.WebAuthNPluginGetUserVerificationPublicKey(
            clsid,
            out uint publicKeyLength,
            out byte* publicKeyPointer));
        try
        {
            var transactionId = operationRequest.transactionId;
            var transactionPointer = (Guid*)Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            var userNamePointer = (char*)Marshal.StringToCoTaskMemUni(userName);
            var hintPointer = (char*)Marshal.StringToCoTaskMemUni("CSharp Passkey Manager");
            byte* responsePointer = null;
            try
            {
                Marshal.StructureToPtr(transactionId, (nint)transactionPointer, false);
                var pluginWindow = PluginOperationCoordinator.WindowHandle;
                var uvRequest = new WEBAUTHN_PLUGIN_USER_VERIFICATION_REQUEST
                {
                    hwnd = pluginWindow != nint.Zero ? (HWND)pluginWindow : operationRequest.hWnd,
                    rguidTransactionId = transactionPointer,
                    pwszUsername = userNamePointer,
                    pwszDisplayHint = hintPointer
                };
                Check(PInvoke.WebAuthNPluginPerformUserVerification(
                    uvRequest,
                    out uint responseLength,
                    out responsePointer));
                var publicKey = CopyBytes(publicKeyPointer, publicKeyLength);
                var response = CopyBytes(responsePointer, responseLength);
                if (!WebAuthnCrypto.VerifySignature(encodedRequest, publicKey, response))
                {
                    throw new HResultException(NteBadSignature);
                }
                PluginLog.Add(AppStrings.Get("LogHelloUvSucceeded"), "Success");
            }
            finally
            {
                if (responsePointer is not null)
                {
                    PInvoke.WebAuthNPluginFreeUserVerificationResponse(responsePointer);
                }
                Marshal.FreeHGlobal((nint)transactionPointer);
                Marshal.FreeCoTaskMem((nint)userNamePointer);
                Marshal.FreeCoTaskMem((nint)hintPointer);
            }
        }
        finally
        {
            if (publicKeyPointer is not null)
            {
                PInvoke.WebAuthNPluginFreePublicKeyResponse(publicKeyPointer);
            }
        }
    }

    private static uint ReadMagic(byte[] publicKey) =>
        publicKey.Length >= 4 ? BitConverter.ToUInt32(publicKey, 0) : 0;

    private static unsafe List<byte[]> ReadAllowList(WEBAUTHN_CREDENTIAL_LIST list)
    {
        var result = new List<byte[]>();
        if (list.cCredentials == 0 || list.ppCredentials is null)
        {
            return result;
        }
        for (uint index = 0; index < list.cCredentials; index++)
        {
            var pointer = list.ppCredentials[index];
            if (pointer is null)
            {
                continue;
            }
            var credential = Marshal.PtrToStructure<WEBAUTHN_CREDENTIAL_EX>((nint)pointer);
            if (credential.pbId is not null && credential.cbId > 0)
            {
                result.Add(CopyBytes(credential.pbId, credential.cbId));
            }
        }
        return result;
    }

    private static unsafe byte[] CopyBytes(byte* pointer, uint length)
    {
        if (length == 0)
        {
            return [];
        }
        if (pointer is null || length > 1024 * 1024)
        {
            throw new HResultException(InvalidArgument);
        }
        var result = new byte[length];
        Marshal.Copy((nint)pointer, result, 0, checked((int)length));
        return result;
    }

    private static void Check(int hresult)
    {
        if (hresult < 0)
        {
            throw new HResultException(hresult);
        }
    }

    private sealed class HResultException : Exception
    {
        public HResultException(int hresult) : base($"HRESULT 0x{hresult:X8}")
        {
            HResult = hresult;
        }
    }
}
