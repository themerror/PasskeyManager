using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.Models;
using PasskeyManager.Services;
using PasskeyManager.ViewModels;

namespace PasskeyManager.Views;

/// <summary>Credential search, details, cache management, and deletion page.</summary>
public sealed partial class PasskeysPage : Page
{
    private bool _isReady;

    public MainPageViewModel ViewModel => MainPageViewModel.Shared;

    public PasskeysPage()
    {
        InitializeComponent();
        Loaded += async (_, _) =>
        {
            await ViewUtilities.RunSafelyAsync(ViewModel.RefreshAsync);
            _isReady = true;
        };
        Unloaded += (_, _) => _isReady = false;
    }

    private void PasskeyList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_isReady)
        {
            ViewModel.SelectCredential(PasskeyList.SelectedItem as PasskeyCredential);
        }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        // PropertyChanged two-way binding performs filtering in the shared ViewModel.
    }

    private async void ToggleAutofillButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.RunSafelyAsync(ViewModel.ToggleAutofillAsync);

    private async void AddAllToCacheButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.RunSafelyAsync(() => ViewModel.SetAllAutofillAsync(true));

    private async void RemoveAllFromCacheButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.RunSafelyAsync(() => ViewModel.SetAllAutofillAsync(false));

    private async void SignInButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.OpenWebAuthnTestSiteAsync();

    private async void NewPasskeyButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.OpenWebAuthnTestSiteAsync();

    private async void DeletePasskeyButton_Click(object sender, RoutedEventArgs e)
    {
        var credential = ViewModel.SelectedCredential;
        if (credential is null)
        {
            return;
        }
        var dialog = ViewUtilities.CreateDialog(
            XamlRoot,
            AppStrings.Get("DialogDeleteCredentialTitle"),
            AppStrings.Format("DialogDeleteCredentialMessage", credential.RpName, credential.UserName),
            AppStrings.Get("DialogDeleteCredentialPrimary"),
            AppStrings.Get("DialogCancel"));
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewUtilities.RunSafelyAsync(ViewModel.DeleteSelectedAsync);
        }
    }
}
