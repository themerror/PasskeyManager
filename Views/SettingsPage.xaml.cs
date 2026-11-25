using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.Models;
using PasskeyManager.Services;
using PasskeyManager.ViewModels;

namespace PasskeyManager.Views;

/// <summary>Plugin, vault, language, and destructive-action settings page.</summary>
public sealed partial class SettingsPage : Page
{
    private bool _isReady;
    private bool _changingRegistration;

    public MainPageViewModel ViewModel => MainPageViewModel.Shared;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += SettingsPage_Loaded;
        Unloaded += (_, _) => _isReady = false;
    }

    private void SettingsPage_Loaded(object sender, RoutedEventArgs e)
    {
        var current = LanguageService.CurrentLanguage;
        LanguageSelector.SelectedItem = LanguageSelector.Items
            .OfType<ComboBoxItem>()
            .FirstOrDefault(item => string.Equals(item.Tag?.ToString(), current, StringComparison.OrdinalIgnoreCase));
        _isReady = true;
    }

    private async void RegisteredToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (!_isReady || _changingRegistration)
        {
            return;
        }
        _changingRegistration = true;
        try
        {
            await ViewUtilities.RunSafelyAsync(() => ViewModel.SetRegistrationAsync(RegisteredToggle.IsOn));
        }
        finally
        {
            _changingRegistration = false;
        }
    }

    private async void MinimizeUiToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (_isReady)
        {
            await ViewUtilities.RunSafelyAsync(() => ViewModel.SetMinimizeUiAsync(MinimizeUiToggle.IsOn));
        }
    }

    private async void OpenWindowsSettingsButton_Click(object sender, RoutedEventArgs e) =>
        await ViewUtilities.OpenWindowsSettingsAsync();

    private async void DeleteAllButton_Click(object sender, RoutedEventArgs e)
    {
        var dialog = ViewUtilities.CreateDialog(
            XamlRoot,
            AppStrings.Get("DialogDeleteAllTitle"),
            AppStrings.Get("DialogDeleteAllMessage"),
            AppStrings.Get("DialogDeleteAllPrimary"),
            AppStrings.Get("DialogCancel"));
        if (await dialog.ShowAsync() == ContentDialogResult.Primary)
        {
            await ViewUtilities.RunSafelyAsync(ViewModel.DeleteAllAsync);
        }
    }

    private void LanguageSelector_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (!_isReady || LanguageSelector.SelectedItem is not ComboBoxItem selected)
        {
            return;
        }
        var language = selected.Tag?.ToString();
        if (string.IsNullOrWhiteSpace(language) || language == LanguageService.CurrentLanguage)
        {
            return;
        }

        LanguageService.SetLanguage(language);
        ViewModel.RefreshLocalizedText();
        ViewModel.ShowStatus(
            AppStrings.Get("LanguageChangedTitle"),
            AppStrings.Get("LanguageChangedMessage"),
            StatusSeverity.Informational);
        App.ReloadLocalizedViews();
    }
}
