using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Windows.Graphics;
using Windows.Win32;
using Windows.Win32.Foundation;

namespace PasskeyManager.Views;

/// <summary>
/// Hosts the workspace and keeps the master-detail layout at a usable size.
/// </summary>
public sealed partial class MainWindow : Window
{
    private const int DefaultWidthDip = 1280;
    private const int DefaultHeightDip = 800;
    private const int MinimumWidthDip = 720;
    private const int MinimumHeightDip = 560;


    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // AppWindow dimensions use physical pixels rather than device-independent pixels.
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = PInvoke.GetDpiForWindow((HWND)hwnd) / 96.0;
        AppWindow.Resize(new SizeInt32(
            (int)(DefaultWidthDip * scale),
            (int)(DefaultHeightDip * scale)));
        if (AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.PreferredMinimumWidth = (int)(MinimumWidthDip * scale);
            presenter.PreferredMinimumHeight = (int)(MinimumHeightDip * scale);
        }

        NavigateToMainPage();
    }

    /// <summary>Recreates the navigation shell after the language override changes.</summary>
    public void NavigateToMainPage() => RootFrame.Navigate(typeof(MainPage));
}
