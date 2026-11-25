using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using System.Runtime.InteropServices;
using Windows.Graphics;

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

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint hWnd);

    public MainWindow()
    {
        InitializeComponent();

        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);
        AppWindow.SetIcon("Assets/AppIcon.ico");

        // AppWindow dimensions use physical pixels rather than device-independent pixels.
        var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
        var scale = GetDpiForWindow(hwnd) / 96.0;
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
