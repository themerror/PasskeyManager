using System.Security.Cryptography;

namespace PasskeyManager.Services;

/// <summary>
/// WebAuthn cryptographic helpers backed by Windows CNG (Microsoft Software Key Storage
/// Provider, ECDSA P-256). Private keys never leave CNG; only ECC public blobs are
/// exported for credential IDs and signature verification.
/// </summary>
public static class WebAuthnCrypto
{
    public const string KeyNamePrefix = "csharp-passkeys/";

    // AAGUID for this authenticator: 2b0ec8c2-fd5a-47b2-ad40-8f6526ed17fa
    private static readonly Guid Aaguid = new("2b0ec8c2-fd5a-47b2-ad40-8f6526ed17fa");

    private const int CngEccPublicBlobMagic = 0x314B4345;       // BCRYPT_ECCPUBLIC_BLOB ('ECK1')
    private const int CngEcdsaP256PublicBlobMagic = 0x31534345; // BCRYPT_ECDSA_PUBLIC_P256_MAGIC ('ECS1')
    private const int P256KeySize = 32;                    // cbKey for P-256

    public static string GetKeyName(ReadOnlySpan<byte> userId)
    {
        // Same bytes as Convert.ToHexString, but allocation-free over the span.
        return string.Concat(KeyNamePrefix, Convert.ToHexString(userId).ToLowerInvariant());
    }

    public static string CreateKeyName() => string.Concat(KeyNamePrefix, Guid.NewGuid().ToString("N"));

    public static CngKey CreateKey(string keyName)
    {
        DeleteKey(keyName);

        var keyParams = new CngKeyCreationParameters
        {
            KeyCreationOptions = CngKeyCreationOptions.None,
            ExportPolicy = CngExportPolicies.AllowPlaintextExport,
            KeyUsage = CngKeyUsages.Signing,
            Provider = CngProvider.MicrosoftSoftwareKeyStorageProvider,
        };

        return CngKey.Create(CngAlgorithm.ECDsaP256, keyName, keyParams);
    }

    public static CngKey CreateKey(ReadOnlySpan<byte> userId) => CreateKey(GetKeyName(userId));

    public static CngKey OpenKey(string keyName) =>
        CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider);

    public static CngKey OpenKey(ReadOnlySpan<byte> userId) => OpenKey(GetKeyName(userId));

    public static void DeleteKey(ReadOnlySpan<byte> userId) => DeleteKey(GetKeyName(userId));

    public static void DeleteKey(string keyName)
    {
        try
        {
            using CngKey key = CngKey.Open(keyName, CngProvider.MicrosoftSoftwareKeyStorageProvider);
            key.Delete();
        }
        catch (CryptographicException)
        {
            // Key does not exist — nothing to delete.
        }
    }

    /// <summary>
    /// Builds the authenticatorData structure (WebAuthn §6.1). On MakeCredential the
    /// attested credential data (AAGUID + credential ID) is appended.
    /// </summary>
    public static byte[] CreateAuthenticatorData(
        CngKey key,
        bool makeCredential,
        ReadOnlySpan<byte> rpIdUtf8,
        out byte[] credentialId)
    {
        // Export the persisted public key as a BCRYPT_ECCPUBLIC_BLOB:
        // ULONG magic, ULONG cbKey, cbKey bytes X, cbKey bytes Y.
        byte[] blob = key.Export(CngKeyBlobFormat.EccPublicBlob);
        if (blob.Length < 8)
        {
            throw new CryptographicException("Unexpectedly short ECC public blob.");
        }

        int magic = BitConverter.ToInt32(blob, 0);
        int cbKey = BitConverter.ToInt32(blob, 4);
        if (cbKey != P256KeySize
            || (magic != CngEccPublicBlobMagic && magic != CngEcdsaP256PublicBlobMagic))
        {
            throw new CryptographicException($"Unexpected ECC public blob (magic=0x{magic:X8}, cbKey={cbKey}).");
        }
        if (blob.Length < 8 + (2 * cbKey))
        {
            throw new CryptographicException("ECC public blob is truncated.");
        }

        byte[] x = new byte[cbKey];
        byte[] y = new byte[cbKey];
        Buffer.BlockCopy(blob, 8, x, 0, cbKey);
        Buffer.BlockCopy(blob, 8 + cbKey, y, 0, cbKey);

        // Credential ID = SHA-256(X || Y).
        credentialId = SHA256.HashData(blob.AsSpan(8, 2 * cbKey));

        byte[] rpIdHash = SHA256.HashData(rpIdUtf8);

        // Authenticator data is a byte string assembled manually (not CBOR) so that
        // byte order is fully controlled; the CBOR COSE map below is also hand-encoded.
        using var data = new MemoryStream();
        data.Write(rpIdHash);                                     // 32 bytes, SHA-256(RP ID)
        data.WriteByte(makeCredential ? (byte)0x5d : (byte)0x1d); // flags: UP|UV|AT / UP|UV|ED + backup bits
        data.WriteByte(0);                                        // signCount (uint32 BE, zeroed)
        data.WriteByte(0);
        data.WriteByte(0);
        data.WriteByte(0);

        if (makeCredential)
        {
            // AAGUID, 16 bytes big-endian (Guid "N" format: no dashes, network order).
            data.Write(Convert.FromHexString(Aaguid.ToString("N")));

            // credentialId length, uint16 big-endian.
            data.WriteByte((byte)(credentialId.Length >> 8));
            data.WriteByte((byte)(credentialId.Length & 0xFF));
            data.Write(credentialId);

            // Hand-encoded CBOR COSE key (fixed map of 5):
            //   A5                map(5)
            //   01 02             1: 2    kty = EC2
            //   03 26             3: -7   alg = ES256
            //   20 01            -1: 1    crv = P-256
            //   21 58 20 <X>     -2: bstr X
            //   22 58 20 <Y>     -3: bstr Y
            data.WriteByte(0xA5);
            data.WriteByte(0x01); data.WriteByte(0x02);
            data.WriteByte(0x03); data.WriteByte(0x26);
            data.WriteByte(0x20); data.WriteByte(0x01);
            data.WriteByte(0x21); data.WriteByte(0x58); data.WriteByte(0x20); data.Write(x);
            data.WriteByte(0x22); data.WriteByte(0x58); data.WriteByte(0x20); data.Write(y);
        }

        return data.ToArray();
    }

    /// <summary>
    /// Signs authenticatorData || clientDataHash (each already SHA-256'd conceptually
    /// by the WebAuthn client); the signature is over SHA-256 of the concatenation.
    /// </summary>
    public static byte[] SignAssertion(
        CngKey key,
        ReadOnlySpan<byte> authenticatorData,
        ReadOnlySpan<byte> clientDataHash)
    {
        byte[] toSign = new byte[authenticatorData.Length + clientDataHash.Length];
        authenticatorData.CopyTo(toSign);
        clientDataHash.CopyTo(toSign.AsSpan(authenticatorData.Length));

        byte[] hash = SHA256.HashData(toSign);

        using var ecdsa = new ECDsaCng(key);
        ecdsa.HashAlgorithm = CngAlgorithm.Sha256;
        // Explicit DER sequence format as required by WebAuthn clients.
        return ecdsa.SignHash(hash, DSASignatureFormat.Rfc3279DerSequence);
    }

    /// <summary>
    /// Verifies a signature against an imported public key blob. Returns false for
    /// invalid key blobs or signatures; cryptographic service errors still throw.
    /// </summary>
    public static bool VerifySignature(
        ReadOnlySpan<byte> data,
        byte[] publicKeyBlob,
        byte[] signature)
    {
        byte[] hash = SHA256.HashData(data);

        CngKey? key = null;
        try
        {
            // Prefer the concrete BLOB type identified by the official CNG magic.
            uint magic = publicKeyBlob.Length >= 4 ? BitConverter.ToUInt32(publicKeyBlob, 0) : 0;
            var format = magic switch
            {
                0x31415352 => new CngKeyBlobFormat("RSAPUBLICBLOB"), // BCRYPT_RSAPUBLIC_BLOB
                0x314B4345 => CngKeyBlobFormat.EccPublicBlob, // BCRYPT_ECCPUBLIC_MAGIC
                _ => CngKeyBlobFormat.GenericPublicBlob
            };
            key = CngKey.Import(
                publicKeyBlob,
                format,
                CngProvider.MicrosoftSoftwareKeyStorageProvider);
        }
        catch (CryptographicException)
        {
            key?.Dispose();
            return false;
        }

        using (key)
        {
            if (key.AlgorithmGroup == CngAlgorithmGroup.Rsa)
            {
                using var rsa = new RSACng(key);
                return rsa.VerifyHash(
                    hash,
                    signature,
                    HashAlgorithmName.SHA256,
                    RSASignaturePadding.Pss);
            }

            if (key.AlgorithmGroup == CngAlgorithmGroup.ECDsa)
            {
                using var ecdsa = new ECDsaCng(key);
                ecdsa.HashAlgorithm = CngAlgorithm.Sha256;
                return ecdsa.VerifyHash(
                    hash,
                    signature,
                    DSASignatureFormat.IeeeP1363FixedFieldConcatenation);
            }

            // Unsupported algorithm group for this authenticator.
            return false;
        }
    }
}
