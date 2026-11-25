namespace PasskeyManager.Models;

/// <summary>
/// Public WebAuthn metadata. The corresponding private key remains in Windows CNG.
/// </summary>
public sealed class StoredCredential
{
    public byte[] CredentialId { get; set; } = [];
    public byte[] UserId { get; set; } = [];
    public string RpId { get; set; } = string.Empty;
    public string RpName { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string KeyName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;

    public string CredentialKey => Convert.ToBase64String(CredentialId);

    public StoredCredential Clone() => new()
    {
        CredentialId = CredentialId.ToArray(),
        UserId = UserId.ToArray(),
        RpId = RpId,
        RpName = RpName,
        UserName = UserName,
        DisplayName = DisplayName,
        KeyName = KeyName,
        CreatedAt = CreatedAt
    };
}
