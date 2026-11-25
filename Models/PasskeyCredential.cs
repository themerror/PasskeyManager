using CommunityToolkit.Mvvm.ComponentModel;
using PasskeyManager.Services;
using System.Globalization;

namespace PasskeyManager.Models;

public partial class PasskeyCredential : ObservableObject
{
    public string CredentialId { get; set; } = string.Empty;
    public byte[] CredentialIdBytes { get; set; } = [];
    public byte[] UserId { get; set; } = [];
    public string KeyName { get; set; } = string.Empty;
    public string RpName { get; set; } = string.Empty;
    public string RpId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public bool HasLocalMetadata { get; set; }

    [ObservableProperty]
    public partial bool IsAutofillEnabled { get; set; }

    public string CredentialIdDisplay => CredentialId.Length <= 16
        ? CredentialId
        : $"{CredentialId[..10]}…{CredentialId[^6..]}";

    public string CreatedAtDisplay => CreatedAt == default
        ? AppStrings.Get("CredentialCacheOnly")
        : CreatedAt.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string CacheStatusText => AppStrings.Get(IsAutofillEnabled ? "CredentialAutofill" : "CredentialLocalOnly");
    public string Initials => string.IsNullOrWhiteSpace(RpName) ? "?" : RpName.Trim()[0].ToString().ToUpperInvariant();
    public string StorageDescription => HasLocalMetadata ? "Windows CNG" : AppStrings.Get("CredentialMetadataMissing");

    public void NotifyCacheChanged() => OnPropertyChanged(nameof(CacheStatusText));

    public override string ToString() => $"{RpName} — {UserName}";

    public static PasskeyCredential FromStored(StoredCredential credential, bool isAutofillEnabled, bool hasLocalMetadata) =>
        new()
        {
            CredentialId = Convert.ToBase64String(credential.CredentialId),
            CredentialIdBytes = credential.CredentialId.ToArray(),
            UserId = credential.UserId.ToArray(),
            KeyName = credential.KeyName,
            RpName = credential.RpName,
            RpId = credential.RpId,
            UserName = credential.UserName,
            DisplayName = credential.DisplayName,
            CreatedAt = hasLocalMetadata ? credential.CreatedAt : default,
            HasLocalMetadata = hasLocalMetadata,
            IsAutofillEnabled = isAutofillEnabled
        };
}
