using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.ViewModels;

namespace PasskeyManager.Views;

/// <summary>Overview page for counts, recent activity, and browser-initiated tests.</summary>
public sealed partial class OverviewPage : Page
{
    public MainPageViewModel ViewModel => MainPageViewModel.Shared;

    public OverviewPage() => InitializeComponent();

    private async void NewPasskeyButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.OpenWebAuthnTestSiteAsync();

    private async void SignInButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.OpenWebAuthnTestSiteAsync();
}
