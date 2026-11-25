using PasskeyManager.Models;
using System.Text.Json;
using Windows.Storage;

namespace PasskeyManager.Services;

/// <summary>
/// Persists public credential metadata only. Private keys are owned by Windows CNG.
/// </summary>
public sealed class CredentialStore
{
    private readonly object _gate = new();
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };
    private List<StoredCredential> _credentials = [];
    private bool _loaded;

    public static CredentialStore Shared { get; } = new();

    private static string FilePath { get; } = Path.Combine(
        ApplicationData.Current.LocalFolder.Path,
        "CredentialsDB",
        "credentials.json");

    public List<StoredCredential> GetAll()
    {
        lock (_gate)
        {
            EnsureLoaded();
            return _credentials.Select(item => item.Clone()).ToList();
        }
    }

    public void AddOrUpdate(StoredCredential credential)
    {
        lock (_gate)
        {
            EnsureLoaded();
            _credentials.RemoveAll(item => item.CredentialId.AsSpan().SequenceEqual(credential.CredentialId));
            _credentials.Add(credential.Clone());
            SaveLocked();
        }
    }

    public void Remove(IEnumerable<byte[]> credentialIds)
    {
        var ids = credentialIds.Select(item => item.ToArray()).ToList();
        lock (_gate)
        {
            EnsureLoaded();
            _credentials.RemoveAll(credential => ids.Any(id => id.AsSpan().SequenceEqual(credential.CredentialId)));
            SaveLocked();
        }
    }

    public List<StoredCredential> FindMatching(string rpId, IReadOnlyCollection<byte[]> allowList)
    {
        lock (_gate)
        {
            EnsureLoaded();
            return _credentials
                .Where(item => string.Equals(item.RpId, rpId, StringComparison.Ordinal))
                .Where(item => allowList.Count == 0 || allowList.Any(id => id.AsSpan().SequenceEqual(item.CredentialId)))
                .Select(item => item.Clone())
                .ToList();
        }
    }

    public void Reload()
    {
        lock (_gate)
        {
            _loaded = false;
            EnsureLoaded();
        }
    }

    private void EnsureLoaded()
    {
        if (_loaded)
        {
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
        if (!File.Exists(FilePath))
        {
            _credentials = [];
        }
        else
        {
            var json = File.ReadAllText(FilePath);
            _credentials = JsonSerializer.Deserialize<List<StoredCredential>>(json, _jsonOptions) ?? [];
        }
        _loaded = true;
    }

    private void SaveLocked()
    {
        var json = JsonSerializer.Serialize(_credentials, _jsonOptions);
        var temporaryPath = FilePath + ".tmp";
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, FilePath, true);
    }
}
