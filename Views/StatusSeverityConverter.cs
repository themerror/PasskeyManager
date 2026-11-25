using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Data;
using PasskeyManager.Models;

namespace PasskeyManager.Views;

/// <summary>Maps the UI-independent VM status to WinUI's InfoBarSeverity.</summary>
public sealed class StatusSeverityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language) => value switch
    {
        StatusSeverity.Success => InfoBarSeverity.Success,
        StatusSeverity.Warning => InfoBarSeverity.Warning,
        StatusSeverity.Error => InfoBarSeverity.Error,
        _ => InfoBarSeverity.Informational
    };

    public object ConvertBack(object value, Type targetType, object parameter, string language) =>
        throw new NotSupportedException();
}
