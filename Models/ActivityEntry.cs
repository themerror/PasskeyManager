using System.Globalization;

namespace PasskeyManager.Models;

public sealed class ActivityEntry
{
    public DateTimeOffset Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public string Kind { get; set; } = ActivityKinds.Info;

    public string TimestampDisplay => Timestamp.ToLocalTime().ToString("g", CultureInfo.CurrentCulture);

    public string IconGlyph => Kind switch
    {
        ActivityKinds.Success => "\uE73E",
        ActivityKinds.Warning => "\uE7BA",
        ActivityKinds.Error => "\uEA39",
        _ => "\uE946"
    };
}

public static class ActivityKinds
{
    public const string Success = "Success";
    public const string Info = "Info";
    public const string Warning = "Warning";
    public const string Error = "Error";
}
