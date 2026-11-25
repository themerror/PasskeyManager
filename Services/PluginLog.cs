namespace PasskeyManager.Services;

public static class PluginLog
{
    private static readonly object Gate = new();
    private static readonly List<PluginLogEntry> Entries = [];

    public static event Action<PluginLogEntry>? EntryAdded;

    public static IReadOnlyList<PluginLogEntry> Snapshot()
    {
        lock (Gate)
        {
            return Entries.ToArray();
        }
    }

    public static void Add(string message, string kind = "Info", int? hresult = null)
    {
        var entry = new PluginLogEntry(DateTimeOffset.Now, message, kind, hresult);
        lock (Gate)
        {
            Entries.Insert(0, entry);
            if (Entries.Count > 200)
            {
                Entries.RemoveAt(Entries.Count - 1);
            }
            Persist(entry);
        }
        EntryAdded?.Invoke(entry);
    }

    public static void Clear()
    {
        lock (Gate)
        {
            Entries.Clear();
        }
    }
    private static void Persist(PluginLogEntry entry)
    {
        try
        {
            // Diagnostics contain operation names/HRESULTs only, never keys, signatures,
            // credential IDs or encoded WebAuthn request/response buffers.
            var path = Path.Combine(Windows.Storage.ApplicationData.Current.LocalFolder.Path, "plugin.log");
            if (File.Exists(path) && new FileInfo(path).Length > 1024 * 1024)
            {
                File.Move(path, path + ".previous", true);
            }
            File.AppendAllText(path, $"{entry.Timestamp:O} [{entry.Kind}] {entry.Message} HR={entry.HResult?.ToString("X8") ?? "-"}{Environment.NewLine}");
        }
        catch (IOException)
        {
            // Logging must never fail a WebAuthn operation.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must never fail a WebAuthn operation.
        }
    }
}

public sealed record PluginLogEntry(DateTimeOffset Timestamp, string Message, string Kind, int? HResult);
