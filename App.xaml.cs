using Microsoft.UI.Xaml;
using PasskeyManager.Services;
using PasskeyManager.Views;
using PasskeyManager.ViewModels;

namespace PasskeyManager;

public partial class App : Application
{
    public static Window Window { get; private set; } = null!;
    public static Microsoft.UI.Dispatching.DispatcherQueue DispatcherQueue { get; private set; } = null!;
    public static nint WindowHandle => WinRT.Interop.WindowNative.GetWindowHandle(Window);

    /// <summary>Reloads resource-backed XAML and dynamic ViewModel strings.</summary>
    public static void ReloadLocalizedViews()
    {
        MainPageViewModel.Shared.RefreshLocalizedText();
        if (Window is MainWindow mainWindow)
        {
            mainWindow.NavigateToMainPage();
        }
    }

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        DispatcherQueue = Microsoft.UI.Dispatching.DispatcherQueue.GetForCurrentThread();
        var pluginMode = Environment.GetCommandLineArgs().Any(argument =>
            argument.Equals("-PluginActivated", StringComparison.OrdinalIgnoreCase));

        if (pluginMode)
        {
            try
            {
                PluginComServer.Start();
                // A hidden window keeps the desktop application's dispatcher alive while
                // Windows activates IPluginAuthenticator on COM/RPC threads.
                Window = new MainWindow();
                Window.Activate();
                Window.AppWindow.Hide();
                PluginLog.Add(AppStrings.Get("LogPluginBackgroundActivated"), "Info");
            }
            catch (Exception ex)
            {
                PluginLog.Add(AppStrings.Format("LogPluginInitializationFailed", ex), "Error");
                Exit();
            }
            return;
        }

        try
        {
            Window = new MainWindow();
            Window.Activate();
        }
        catch (Exception ex)
        {
            PluginLog.Add(ex.ToString(), "Error");
            throw;
        }
    }
}
