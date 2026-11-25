using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using PasskeyManager.Models;
using PasskeyManager.Services;
using PasskeyManager.ViewModels;
using Windows.System;

namespace PasskeyManager.Views;

/// <summary>Shared navigation, dialog, and error-handling helpers for independent pages.</summary>
internal static class ViewUtilities
{
    public static async Task RunSafelyAsync(Func<Task> action)
    {
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            PluginLog.Add(ex.ToString(), "Error");
            MainPageViewModel.Shared.ShowStatus(
                AppStrings.Get("OperationFailed"),
                ex.Message,
                StatusSeverity.Error);
        }
    }

    public static ContentDialog CreateDialog(
        XamlRoot root,
        string title,
        string message,
        string primaryButtonText,
        string closeButtonText) => new()
    {
        XamlRoot = root,
        Title = title,
        Content = new TextBlock { Text = message, TextWrapping = TextWrapping.Wrap },
        PrimaryButtonText = primaryButtonText,
        CloseButtonText = closeButtonText,
        DefaultButton = ContentDialogButton.Close
    };

    public static async Task OpenWebAuthnTestSiteAsync()
    {
        await Launcher.LaunchUriAsync(new Uri("https://webauthn.io/"));
        MainPageViewModel.Shared.ShowStatus(
            AppStrings.Get("BrowserOperationTitle"),
            AppStrings.Get("BrowserOperationMessage"),
            StatusSeverity.Informational);
    }

    public static async Task OpenWindowsSettingsAsync()
    {
        var launched = await Launcher.LaunchUriAsync(new Uri("ms-settings:passkeys-advancedoptions"));
        MainPageViewModel.Shared.ShowStatus(
            AppStrings.Get(launched ? "SettingsLaunchSucceeded" : "SettingsLaunchFailed"),
            AppStrings.Get(launched ? "SettingsLaunchMessage" : "SettingsLaunchManual"),
            launched ? StatusSeverity.Informational : StatusSeverity.Warning);
    }
}
