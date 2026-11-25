using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.ViewModels;

namespace PasskeyManager.Views;

/// <summary>Native plugin activity log page.</summary>
public sealed partial class ActivityPage : Page
{
    public MainPageViewModel ViewModel => MainPageViewModel.Shared;

    public ActivityPage() => InitializeComponent();

    private async void ClearActivityButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.RunSafelyAsync(ViewModel.ClearActivityAsync);
}
