using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.Services;
using PasskeyManager.ViewModels;

namespace PasskeyManager.Views;

/// <summary>Navigation shell. Feature pages are hosted independently in ContentFrame.</summary>
public sealed partial class MainPage : Page
{
    private bool _isReady;

    public MainPageViewModel ViewModel => MainPageViewModel.Shared;

    public MainPage()
    {
        InitializeComponent();
        Loaded += MainPage_Loaded;
        Unloaded += MainPage_Unloaded;
    }

    public static Visibility BoolToVisibility(bool value) => value ? Visibility.Visible : Visibility.Collapsed;
    public static Visibility InvertBoolToVisibility(bool value) => value ? Visibility.Collapsed : Visibility.Visible;


    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        if (_isReady)
        {
            return;
        }
        await ViewUtilities.RunSafelyAsync(ViewModel.InitializeAsync);
        _isReady = true;
        RootNavView.SelectedItem = RootNavView.MenuItems.OfType<NavigationViewItem>().FirstOrDefault();
        UpdateVaultButton();
        App.Window.Activated += Window_Activated;
    }

    private void MainPage_Unloaded(object sender, RoutedEventArgs e)
    {
        App.Window.Activated -= Window_Activated;
        _isReady = false;
    }

    private void Window_Activated(object sender, WindowActivatedEventArgs args)
    {
        if (_isReady)
        {
            ViewModel.RefreshRegistration();
        }
    }

    private void NavigationView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        var tag = (args.SelectedItem as NavigationViewItem)?.Tag?.ToString() ?? "Overview";
        var pageType = tag switch
        {
            "Passkeys" => typeof(PasskeysPage),
            "Activity" => typeof(ActivityPage),
            "Settings" => typeof(SettingsPage),
            _ => typeof(OverviewPage)
        };
        if (ContentFrame.Content?.GetType() != pageType)
        {
            ContentFrame.Navigate(pageType);
        }
    }

    private async void VaultButton_Click(object sender, RoutedEventArgs e)
    {
        await ViewUtilities.RunSafelyAsync(async () =>
        {
            if (ViewModel.IsVaultUnlocked)
            {
                await ViewModel.LockAsync();
            }
            else
            {
                await ViewModel.UnlockAsync();
            }
            UpdateVaultButton();
        });
    }

    private void UpdateVaultButton()
    {
        VaultButton.IsEnabled = !ViewModel.IsBusy;
        ToolTipService.SetToolTip(
            VaultButton,
            AppStrings.Get(ViewModel.IsVaultUnlocked ? "VaultLockTooltip" : "VaultUnlockTooltip"));
    }
}
