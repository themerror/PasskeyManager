using CommunityToolkit.Mvvm.ComponentModel;
using PasskeyManager.Models;
using PasskeyManager.Services;
using System.Collections.ObjectModel;

namespace PasskeyManager.ViewModels;

/// <summary>
/// Presentation state for real local/CNG and Windows cache metadata. No native key
/// operation is embedded in XAML; all actions are exposed as asynchronous VM methods.
/// </summary>
public partial class MainPageViewModel : ObservableObject
{
    private readonly PluginRegistrationService _registration = new();
    private readonly SystemCredentialCache _cache = new();
    private bool _isInitialized;
    private int _authenticatorState;

    private MainPageViewModel()
    {
        PluginLog.EntryAdded += OnPluginLogEntry;
    }

    /// <summary>Single state owner shared by the independently navigated pages.</summary>
    public static MainPageViewModel Shared { get; } = new();

    public ObservableCollection<PasskeyCredential> Credentials { get; } = [];
    public ObservableCollection<PasskeyCredential> FilteredCredentials { get; } = [];
    public ObservableCollection<ActivityEntry> Activities { get; } = [];

    [ObservableProperty] public partial PasskeyCredential? SelectedCredential { get; set; }
    [ObservableProperty] public partial string SearchText { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsVaultUnlocked { get; set; } = true;
    [ObservableProperty] public partial bool IsRegistered { get; set; }
    [ObservableProperty] public partial bool IsMinimizeUiEnabled { get; set; } = true;
    [ObservableProperty] public partial bool IsBusy { get; set; }
    [ObservableProperty] public partial string StatusMessage { get; set; } = string.Empty;
    [ObservableProperty] public partial string StatusTitle { get; set; } = string.Empty;
    [ObservableProperty] public partial bool IsStatusOpen { get; set; }
    [ObservableProperty] public partial StatusSeverity StatusSeverity { get; set; } = StatusSeverity.Informational;
    [ObservableProperty] public partial int LocalCount { get; set; }
    [ObservableProperty] public partial int AutofillCount { get; set; }
    [ObservableProperty] public partial int ActivityCount { get; set; }
    [ObservableProperty] public partial bool HasSelectedCredential { get; set; }
    [ObservableProperty] public partial bool HasCredentials { get; set; }
    [ObservableProperty] public partial bool HasFilteredCredentials { get; set; }
    [ObservableProperty] public partial bool IsEmptySearch { get; set; }
    [ObservableProperty] public partial string TotalSummary { get; set; } = AppStrings.Format("SummaryLocalFormat", 0);
    [ObservableProperty] public partial string CacheSummary { get; set; } = AppStrings.Format("SummaryCacheFormat", 0);
    [ObservableProperty] public partial string VaultSummary { get; set; } = AppStrings.Get("VaultUnlocked");
    [ObservableProperty] public partial string VaultStatusText { get; set; } = AppStrings.Get("VaultStatusUnlocked");
    [ObservableProperty] public partial string RegistrationStatusText { get; set; } = AppStrings.Get("RegistrationQuerying");
    [ObservableProperty] public partial string SelectionHint { get; set; } = AppStrings.Get("SelectionPrompt");

    public void RefreshLocalizedText()
    {
        TotalSummary = AppStrings.Format("SummaryLocalFormat", LocalCount);
        CacheSummary = AppStrings.Format("SummaryCacheFormat", AutofillCount);
        OnIsVaultUnlockedChanged(IsVaultUnlocked);
        OnSelectedCredentialChanged(SelectedCredential);
        UpdateRegistrationText();
    }

    public async Task InitializeAsync()
    {
        if (_isInitialized)
        {
            return;
        }
        _isInitialized = true;
        IsVaultUnlocked = !PluginSettings.IsVaultLocked();
        IsMinimizeUiEnabled = PluginSettings.IsSilentOperation();
        foreach (var entry in PluginLog.Snapshot())
        {
            AddActivity(entry);
        }
        await RefreshAsync();
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    partial void OnSelectedCredentialChanged(PasskeyCredential? value)
    {
        HasSelectedCredential = value is not null;
        SelectionHint = value is null ? AppStrings.Get("SelectionPrompt") : value.RpName;
    }

    partial void OnIsVaultUnlockedChanged(bool value)
    {
        VaultSummary = value ? AppStrings.Get("VaultUnlocked") : AppStrings.Get("VaultLocked");
        VaultStatusText = value ? AppStrings.Get("VaultStatusUnlocked") : AppStrings.Get("VaultStatusLocked");
        if (!value)
        {
            SelectedCredential = null;
        }
    }

    partial void OnIsRegisteredChanged(bool value) => UpdateRegistrationText();

    public void SelectCredential(PasskeyCredential? credential) => SelectedCredential = credential;

    public Task RefreshAsync()
    {
        var selectedId = SelectedCredential?.CredentialId;
        var local = CredentialStore.Shared.GetAll();
        var cacheHResult = _cache.GetAll(out var cached);
        if (cacheHResult < 0)
        {
            ShowHResult(AppStrings.Get("StatusCacheReadFailed"), cacheHResult);
            cached = [];
        }

        var items = new List<PasskeyCredential>();
        foreach (var credential in local)
        {
            var isCached = cached.Any(item => item.CredentialId.AsSpan().SequenceEqual(credential.CredentialId));
            items.Add(PasskeyCredential.FromStored(credential, isCached, true));
        }
        foreach (var credential in cached.Where(cacheItem =>
                     !local.Any(localItem => localItem.CredentialId.AsSpan().SequenceEqual(cacheItem.CredentialId))))
        {
            items.Add(PasskeyCredential.FromStored(credential, true, false));
        }

        ReplaceCollection(Credentials, items);
        ApplyFilter();
        SelectedCredential = Credentials.FirstOrDefault(item => item.CredentialId == selectedId);
        RefreshSummaries();
        RefreshRegistration();
        return Task.CompletedTask;
    }

    public void RefreshRegistration()
    {
        var hr = _registration.Refresh(out var registered, out var state);
        IsRegistered = registered;
        _authenticatorState = state;
        UpdateRegistrationText();
        if (hr < 0 && hr != unchecked((int)0x80090011) && hr != unchecked((int)0x80070002))
        {
            RegistrationStatusText = AppStrings.Format("RegistrationStateQueryFailedFormat", hr);
        }
    }

    public async Task SetRegistrationAsync(bool desired)
    {
        IsBusy = true;
        try
        {
            var hr = await Task.Run(() => desired ? _registration.Register() : _registration.Unregister());
            if (hr < 0)
            {
                ShowHResult(AppStrings.Get(desired ? "RegistrationAddFailed" : "RegistrationRemoveFailed"), hr);
            }
            else
            {
                PluginLog.Add(desired
                    ? AppStrings.Get("LogRegisteredPending")
                    : AppStrings.Get("LogUnregistered"), "Success", hr);
                ShowStatus(
                    AppStrings.Get(desired ? "StatusRegisteredTitle" : "StatusRemovedTitle"),
                    AppStrings.Get(desired ? "StatusRegisteredNext" : "StatusRemovedMessage"),
                    StatusSeverity.Success);
            }
            RefreshRegistration();
            await RefreshAsync();
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task UnlockAsync()
    {
        if (IsBusy || IsVaultUnlocked)
        {
            return;
        }
        IsBusy = true;
        try
        {
            if (await WindowsHelloService.UnlockVaultAsync())
            {
                IsVaultUnlocked = true;
                PluginLog.Add(AppStrings.Get("LogHelloUnlock"), "Success");
                ShowStatus(AppStrings.Get("StatusUnlockedTitle"), AppStrings.Get("StatusUnlockedMessage"), StatusSeverity.Success);
            }
            else
            {
                ShowStatus(AppStrings.Get("StatusUnlockFailedTitle"), AppStrings.Get("StatusUnlockFailedMessage"), StatusSeverity.Warning);
            }
        }
        finally
        {
            IsBusy = false;
        }
    }

    public Task LockAsync()
    {
        PluginSettings.SetVaultLocked(true);
        IsVaultUnlocked = false;
        PluginLog.Add(AppStrings.Get("LogVaultLocked"), "Info");
        ShowStatus(AppStrings.Get("VaultLocked"), AppStrings.Get("StatusVaultLockedMessage"), StatusSeverity.Informational);
        return Task.CompletedTask;
    }

    public Task SetMinimizeUiAsync(bool enabled)
    {
        PluginSettings.SetSilentOperation(enabled);
        PluginLog.Add(AppStrings.Format("LogMinimizeFormat", AppStrings.Get(enabled ? "Enabled" : "Disabled")), "Info");
        return Task.CompletedTask;
    }

    public async Task ToggleAutofillAsync()
    {
        var credential = RequireSelection();
        var hr = credential.IsAutofillEnabled
            ? _cache.Remove(new List<StoredCredential> { ToStored(credential) })
            : _cache.Add(new List<StoredCredential> { ToStored(credential) });
        if (hr < 0)
        {
            ShowHResult(AppStrings.Get("StatusCacheUpdateFailed"), hr);
            return;
        }
        PluginLog.Add(AppStrings.Format("LogCacheToggleFormat", credential.RpName, AppStrings.Get(credential.IsAutofillEnabled ? "CacheRemoved" : "CacheAdded")), "Success");
        await RefreshAsync();
    }

    public async Task SetAllAutofillAsync(bool enabled)
    {
        var local = CredentialStore.Shared.GetAll();
        var hr = enabled ? _cache.Add(local) : _cache.RemoveAll();
        if (hr < 0)
        {
            ShowHResult(AppStrings.Get("StatusCacheBatchFailed"), hr);
            return;
        }
        PluginLog.Add(AppStrings.Get(enabled ? "LogCacheAllAdded" : "LogCacheAllRemoved"), "Success");
        await RefreshAsync();
    }

    public async Task DeleteSelectedAsync()
    {
        var credential = RequireSelection();
        var hr = _cache.Remove(new List<StoredCredential> { ToStored(credential) });
        if (hr < 0)
        {
            ShowHResult(AppStrings.Get("StatusCacheDeleteFailed"), hr);
            return;
        }

        if (credential.HasLocalMetadata)
        {
            CredentialStore.Shared.Remove(new List<byte[]> { credential.CredentialIdBytes });
            WebAuthnCrypto.DeleteKey(credential.KeyName);
        }
        PluginLog.Add(AppStrings.Format("LogCredentialDeletedFormat", credential.RpName), "Warning");
        SelectedCredential = null;
        await RefreshAsync();
        ShowStatus(AppStrings.Get("StatusCredentialDeletedTitle"), AppStrings.Get("StatusCredentialDeletedMessage"), StatusSeverity.Success);
    }

    public async Task DeleteAllAsync()
    {
        var local = CredentialStore.Shared.GetAll();
        var hr = _cache.RemoveAll();
        if (hr < 0)
        {
            ShowHResult(AppStrings.Get("StatusCacheClearFailed"), hr);
            return;
        }
        CredentialStore.Shared.Remove(local.Select(item => item.CredentialId));
        foreach (var credential in local)
        {
            WebAuthnCrypto.DeleteKey(credential.KeyName);
        }
        PluginLog.Add(AppStrings.Get("LogAllCredentialsDeleted"), "Warning");
        await RefreshAsync();
        ShowStatus(AppStrings.Get("StatusAllDeletedTitle"), AppStrings.Get("StatusAllDeletedMessage"), StatusSeverity.Success);
    }

    public Task ClearActivityAsync()
    {
        PluginLog.Clear();
        Activities.Clear();
        ActivityCount = 0;
        return Task.CompletedTask;
    }

    public void ShowStatus(string title, string message, StatusSeverity severity)
    {
        IsStatusOpen = false;
        StatusTitle = title;
        StatusMessage = message;
        StatusSeverity = severity;
        IsStatusOpen = true;
    }

    private void OnPluginLogEntry(PluginLogEntry entry)
    {
        if (App.DispatcherQueue is null || App.DispatcherQueue.HasThreadAccess)
        {
            AddActivity(entry);
        }
        else
        {
            App.DispatcherQueue.TryEnqueue(() => AddActivity(entry));
        }
    }

    private void AddActivity(PluginLogEntry entry)
    {
        Activities.Insert(0, new ActivityEntry
        {
            Timestamp = entry.Timestamp,
            Message = entry.HResult.HasValue && entry.HResult != 0
                ? $"{entry.Message} (0x{entry.HResult:X8})"
                : entry.Message,
            Kind = entry.Kind
        });
        while (Activities.Count > 200)
        {
            Activities.RemoveAt(Activities.Count - 1);
        }
        ActivityCount = Activities.Count;
    }

    private void ApplyFilter()
    {
        var matches = Credentials.Where(credential =>
            string.IsNullOrWhiteSpace(SearchText)
            || credential.RpName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || credential.RpId.Contains(SearchText, StringComparison.OrdinalIgnoreCase)
            || credential.UserName.Contains(SearchText, StringComparison.OrdinalIgnoreCase)).ToList();
        ReplaceCollection(FilteredCredentials, matches);
        HasCredentials = Credentials.Count > 0;
        HasFilteredCredentials = FilteredCredentials.Count > 0;
        IsEmptySearch = HasCredentials && !HasFilteredCredentials;
    }

    private void RefreshSummaries()
    {
        LocalCount = Credentials.Count(item => item.HasLocalMetadata);
        AutofillCount = Credentials.Count(item => item.IsAutofillEnabled);
        ActivityCount = Activities.Count;
        TotalSummary = AppStrings.Format("SummaryLocalFormat", LocalCount);
        CacheSummary = AppStrings.Format("SummaryCacheFormat", AutofillCount);
        HasCredentials = Credentials.Count > 0;
        HasFilteredCredentials = FilteredCredentials.Count > 0;
        IsEmptySearch = HasCredentials && !HasFilteredCredentials;
    }

    private PasskeyCredential RequireSelection() =>
        SelectedCredential ?? throw new InvalidOperationException(AppStrings.Get("ErrorSelectFirst"));

    private static StoredCredential ToStored(PasskeyCredential credential) => new()
    {
        CredentialId = credential.CredentialIdBytes,
        UserId = credential.UserId,
        RpId = credential.RpId,
        RpName = credential.RpName,
        UserName = credential.UserName,
        DisplayName = credential.DisplayName,
        KeyName = credential.KeyName,
        CreatedAt = credential.CreatedAt
    };

    private void UpdateRegistrationText()
    {
        RegistrationStatusText = !IsRegistered
            ? AppStrings.Get("RegistrationNotRegistered")
            : _authenticatorState == 1 ? AppStrings.Get("RegistrationEnabled") : AppStrings.Get("RegistrationPending");
    }

    private void ShowHResult(string title, int hresult) =>
        ShowStatus(title, AppStrings.Format("StatusHResultMessageFormat", hresult), StatusSeverity.Error);

    private static void ReplaceCollection<T>(ObservableCollection<T> target, IEnumerable<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }
}
