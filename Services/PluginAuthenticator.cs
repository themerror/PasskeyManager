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

    public int MakeCredential(nint requestPointer, nint responsePointer)
    {
        return RunOperation(requestPointer, responsePointer, MakeCredentialCore);
    }

    public int GetAssertion(nint requestPointer, nint responsePointer)
    {
        return RunOperation(requestPointer, responsePointer, GetAssertionCore);
    }

    public int CancelOperation(nint requestPointer)
    {
        if (requestPointer == nint.Zero)
        {
            return InvalidArgument;
        }
        var request = Marshal.PtrToStructure<WebAuthnPluginCancelOperationRequest>(requestPointer);
        return PluginOperationCoordinator.Cancel(request.TransactionId) ? 0 : NteNotFound;
    }

    public int GetLockStatus(out int lockStatus)
    {
        lockStatus = PluginSettings.IsVaultLocked()
            ? (int)PluginLockStatus.PluginLocked
            : (int)PluginLockStatus.PluginUnlocked;
        return 0;
    }

    private static int RunOperation(
        nint requestPointer,
        nint responsePointer,
        Func<nint, nint, int> operation)
    {
        if (requestPointer == nint.Zero || responsePointer == nint.Zero)
        {
            return InvalidArgument;
        }

        Marshal.StructureToPtr(new WebAuthnPluginOperationResponse(), responsePointer, false);
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
        var request = Marshal.PtrToStructure<WebAuthnPluginOperationRequest>(requestPointer);
        PluginLog.Add(AppStrings.Get("LogMakeCredentialCallback"), "Info");
        ValidateRequest(request);
        var requestBytes = CopyBytes(request.PbEncodedRequest, request.CbEncodedRequest);
        VerifyPlatformRequest(request, requestBytes);

        Check(WebAuthnNative.WebAuthNDecodeMakeCredentialRequest(
            request.CbEncodedRequest,
            request.PbEncodedRequest,
            out nint decodedPointer));
        try
        {
            var decoded = Marshal.PtrToStructure<WebAuthnCtapCborMakeCredentialRequest>(decodedPointer);
            if (decoded.PRpInformation == nint.Zero || decoded.PUserInformation == nint.Zero)
            {
                return Unexpected;
            }
            var rp = Marshal.PtrToStructure<WebAuthnRpEntityInformation>(decoded.PRpInformation);
            var user = Marshal.PtrToStructure<WebAuthnUserEntityInformation>(decoded.PUserInformation);
            var rpId = Marshal.PtrToStringUni(rp.PwszId) ?? throw new HResultException(InvalidArgument);
            var rpName = Marshal.PtrToStringUni(rp.PwszName) ?? rpId;
            var userName = Marshal.PtrToStringUni(user.PwszName) ?? string.Empty;
            var displayName = Marshal.PtrToStringUni(user.PwszDisplayName) ?? userName;
            var userId = CopyBytes(user.PbId, user.CbId);
            if (userId.Length is 0 or > 64)
            {
                return InvalidArgument;
            }
            if (string.Equals(rpId, PluginConstants.PluginRpId, StringComparison.OrdinalIgnoreCase))
            {
                return NteNotSupported;
            }

            var decision = PluginOperationCoordinator.ConfirmMakeCredentialAsync(
                request.TransactionId,
                request.HWnd,
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

            var rpIdBytes = CopyBytes(decoded.PbRpId, decoded.CbRpId);
            var keyName = WebAuthnCrypto.CreateKeyName();
            using var key = WebAuthnCrypto.CreateKey(keyName);
            var authenticatorData = WebAuthnCrypto.CreateAuthenticatorData(
                key,
                true,
                rpIdBytes,
                out var credentialId);

            nint encodedResponse;
            uint encodedResponseLength;
            var format = Marshal.StringToCoTaskMemUni("none");
            try
            {
                fixed (byte* authenticatorDataPointer = authenticatorData)
                {
                    var attestation = new WebAuthnCredentialAttestation
                    {
                        DwVersion = 8,
                        PwszFormatType = format,
                        CbAuthenticatorData = (uint)authenticatorData.Length,
                        PbAuthenticatorData = (nint)authenticatorDataPointer
                    };
                    Check(WebAuthnNative.WebAuthNEncodeMakeCredentialResponse(
                        attestation,
                        out encodedResponseLength,
                        out encodedResponse));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(format);
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
                Marshal.FreeCoTaskMem(encodedResponse);
                throw;
            }

            var cacheHResult = new SystemCredentialCache().Add(new List<StoredCredential> { stored });
            PluginLog.Add(
                cacheHResult >= 0
                    ? AppStrings.Format("LogCredentialCreatedFormat", rpName)
                    : AppStrings.Format("LogCredentialCacheFailedFormat", cacheHResult),
                cacheHResult >= 0 ? "Success" : "Warning",
                cacheHResult);
            Marshal.StructureToPtr(new WebAuthnPluginOperationResponse
            {
                CbEncodedResponse = encodedResponseLength,
                PbEncodedResponse = encodedResponse
            }, responsePointer, false);
            return 0;
        }
        finally
        {
            WebAuthnNative.WebAuthNFreeDecodedMakeCredentialRequest(decodedPointer);
        }
    }

    private static unsafe int GetAssertionCore(nint requestPointer, nint responsePointer)
    {
        var request = Marshal.PtrToStructure<WebAuthnPluginOperationRequest>(requestPointer);
        PluginLog.Add(AppStrings.Get("LogGetAssertionCallback"), "Info");
        ValidateRequest(request);
        var requestBytes = CopyBytes(request.PbEncodedRequest, request.CbEncodedRequest);
        VerifyPlatformRequest(request, requestBytes);

        Check(WebAuthnNative.WebAuthNDecodeGetAssertionRequest(
            request.CbEncodedRequest,
            request.PbEncodedRequest,
            out nint decodedPointer));
        try
        {
            var decoded = Marshal.PtrToStructure<WebAuthnCtapCborGetAssertionRequest>(decodedPointer);
            var rpId = Marshal.PtrToStringUni(decoded.PwszRpId) ?? throw new HResultException(InvalidArgument);
            var allowList = ReadAllowList(decoded.CredentialList);
            CredentialStore.Shared.Reload();
            var matches = CredentialStore.Shared.FindMatching(rpId, allowList);
            if (matches.Count == 0)
            {
                return NteNotFound;
            }

            var decision = PluginOperationCoordinator.ConfirmGetAssertionAsync(
                request.TransactionId,
                request.HWnd,
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

            var rpIdBytes = CopyBytes(decoded.PbRpId, decoded.CbRpId);
            var clientDataHash = CopyBytes(decoded.PbClientDataHash, decoded.CbClientDataHash);
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

            nint encodedResponse;
            uint encodedResponseLength;
            var credentialType = Marshal.StringToCoTaskMemUni(PluginConstants.CredentialTypePublicKey);
            try
            {
                fixed (byte* authDataPointer = authenticatorData)
                fixed (byte* signaturePointer = signature)
                fixed (byte* credentialIdPointer = selected.CredentialId)
                fixed (byte* userIdPointer = selected.UserId)
                {
                    var userEntity = new WebAuthnUserEntityInformation
                    {
                        DwVersion = 1,
                        CbId = (uint)selected.UserId.Length,
                        PbId = (nint)userIdPointer
                    };
                    var assertion = new WebAuthnAssertion
                    {
                        DwVersion = 6,
                        CbAuthenticatorData = (uint)authenticatorData.Length,
                        PbAuthenticatorData = (nint)authDataPointer,
                        CbSignature = (uint)signature.Length,
                        PbSignature = (nint)signaturePointer,
                        Credential = new WebAuthnCredential
                        {
                            DwVersion = 1,
                            CbId = (uint)selected.CredentialId.Length,
                            PbId = (nint)credentialIdPointer,
                            PwszCredentialType = credentialType
                        },
                        CbUserId = (uint)selected.UserId.Length,
                        PbUserId = (nint)userIdPointer
                    };
                    var ctapResponse = new WebAuthnCtapCborGetAssertionResponse
                    {
                        WebAuthNAssertion = assertion,
                        PUserInformation = (nint)(&userEntity),
                        DwNumberOfCredentials = 1
                    };
                    Check(WebAuthnNative.WebAuthNEncodeGetAssertionResponse(
                        ctapResponse,
                        out encodedResponseLength,
                        out encodedResponse));
                }
            }
            finally
            {
                Marshal.FreeCoTaskMem(credentialType);
            }

            Marshal.StructureToPtr(new WebAuthnPluginOperationResponse
            {
                CbEncodedResponse = encodedResponseLength,
                PbEncodedResponse = encodedResponse
            }, responsePointer, false);
            PluginLog.Add(AppStrings.Format("LogAssertionCreatedFormat", selected.RpName), "Success");
            return 0;
        }
        finally
        {
            WebAuthnNative.WebAuthNFreeDecodedGetAssertionRequest(decodedPointer);
        }
    }

    private static void ValidateRequest(WebAuthnPluginOperationRequest request)
    {
        if (request.RequestType != (int)WebAuthnPluginRequestType.Ctap2Cbor
            || request.PbEncodedRequest == nint.Zero
            || request.CbEncodedRequest == 0)
        {
            throw new HResultException(InvalidArgument);
        }
    }

    private static void VerifyPlatformRequest(WebAuthnPluginOperationRequest request, byte[] requestBytes)
    {
        var clsid = PluginConstants.PluginClsid;
        Check(WebAuthnNative.WebAuthNPluginGetOperationSigningPublicKey(
            clsid,
            out uint keyLength,
            out nint keyPointer));
        try
        {
            var publicKey = CopyBytes(keyPointer, keyLength);
            var signature = CopyBytes(request.PbRequestSignature, request.CbRequestSignature);
            if (!WebAuthnCrypto.VerifySignature(requestBytes, publicKey, signature))
            {
                PluginLog.Add(AppStrings.Format("LogPlatformSignatureFailed", publicKey.Length, ReadMagic(publicKey)), "Error", NteBadSignature);
                throw new HResultException(NteBadSignature);
            }
            PluginLog.Add(AppStrings.Get("LogPlatformSignatureSucceeded"), "Success");
        }
        finally
        {
            if (keyPointer != nint.Zero)
            {
                WebAuthnNative.WebAuthNPluginFreePublicKeyResponse(keyPointer);
            }
        }
    }

    private static void PerformUserVerification(
        WebAuthnPluginOperationRequest operationRequest,
        byte[] encodedRequest,
        string userName)
    {
        var clsid = PluginConstants.PluginClsid;
        Check(WebAuthnNative.WebAuthNPluginGetUserVerificationCount(clsid, out _));
        Check(WebAuthnNative.WebAuthNPluginGetUserVerificationPublicKey(
            clsid,
            out uint publicKeyLength,
            out nint publicKeyPointer));
        try
        {
            var transactionId = operationRequest.TransactionId;
            var transactionPointer = Marshal.AllocHGlobal(Marshal.SizeOf<Guid>());
            var userNamePointer = Marshal.StringToCoTaskMemUni(userName);
            var hintPointer = Marshal.StringToCoTaskMemUni("CSharp Passkey Manager");
            nint responsePointer = nint.Zero;
            try
            {
                Marshal.StructureToPtr(transactionId, transactionPointer, false);
                var pluginWindow = PluginOperationCoordinator.WindowHandle;
                var uvRequest = new WebAuthnPluginUserVerificationRequest
                {
                    HWnd = pluginWindow != nint.Zero ? pluginWindow : operationRequest.HWnd,
                    RguidTransactionId = transactionPointer,
                    PwszUsername = userNamePointer,
                    PwszDisplayHint = hintPointer
                };
                Check(WebAuthnNative.WebAuthNPluginPerformUserVerification(
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
                if (responsePointer != nint.Zero)
                {
                    WebAuthnNative.WebAuthNPluginFreeUserVerificationResponse(responsePointer);
                }
                Marshal.FreeHGlobal(transactionPointer);
                Marshal.FreeCoTaskMem(userNamePointer);
                Marshal.FreeCoTaskMem(hintPointer);
            }
        }
        finally
        {
            if (publicKeyPointer != nint.Zero)
            {
                WebAuthnNative.WebAuthNPluginFreePublicKeyResponse(publicKeyPointer);
            }
        }
    }

    private static uint ReadMagic(byte[] publicKey) =>
        publicKey.Length >= 4 ? BitConverter.ToUInt32(publicKey, 0) : 0;

    private static List<byte[]> ReadAllowList(WebAuthnCredentialList list)
    {
        var result = new List<byte[]>();
        if (list.CCredentials == 0 || list.PpCredentials == nint.Zero)
        {
            return result;
        }
        for (uint index = 0; index < list.CCredentials; index++)
        {
            var pointer = Marshal.ReadIntPtr(list.PpCredentials + (nint)(index * (uint)nint.Size));
            if (pointer == nint.Zero)
            {
                continue;
            }
            var credential = Marshal.PtrToStructure<WebAuthnCredentialEx>(pointer);
            if (credential.PbId != nint.Zero && credential.CbId > 0)
            {
                result.Add(CopyBytes(credential.PbId, credential.CbId));
            }
        }
        return result;
    }

    private static byte[] CopyBytes(nint pointer, uint length)
    {
        if (length == 0)
        {
            return [];
        }
        if (pointer == nint.Zero || length > 1024 * 1024)
        {
            throw new HResultException(InvalidArgument);
        }
        var result = new byte[length];
        Marshal.Copy(pointer, result, 0, checked((int)length));
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
