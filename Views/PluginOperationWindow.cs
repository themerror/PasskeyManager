using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using PasskeyManager.Models;
using PasskeyManager.Services;
using System.Runtime.InteropServices;
using Windows.Graphics;

namespace PasskeyManager.Views;

/// <summary>
/// Code-behind for the short-lived MakeCredential/GetAssertion confirmation window.
/// </summary>
internal sealed class PluginOperationWindow
{
    private readonly List<RadioButton> _credentialChoices = [];

    public PluginOperationWindow(
        bool isMakeCredential,
        string rpId,
        string rpName,
        string userName,
        IReadOnlyList<StoredCredential> matchingCredentials,
        nint ownerWindow)
    {
        Window = new Window
        {
            Title = isMakeCredential ? AppStrings.Get("OperationMakeWindowTitle") : AppStrings.Get("OperationGetWindowTitle"),
            SystemBackdrop = new MicaBackdrop()
        };

        var root = new Grid { Padding = new Thickness(28, 24, 28, 24), RowSpacing = 16 };
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var header = new StackPanel { Spacing = 8 };
        header.Children.Add(new TextBlock
        {
            Text = isMakeCredential
                ? AppStrings.Get("OperationMakeHeader")
                : AppStrings.Format("OperationGetHeaderFormat", rpName),
            FontSize = 24,
            FontWeight = Microsoft.UI.Text.FontWeights.SemiBold,
            TextWrapping = TextWrapping.Wrap
        });
        header.Children.Add(new TextBlock
        {
            Text = isMakeCredential
                ? AppStrings.Format("OperationSiteAccountFormat", rpName, userName)
                : AppStrings.Get("OperationHelloDescription"),
            Foreground = (Brush)Application.Current.Resources["TextFillColorSecondaryBrush"],
            TextWrapping = TextWrapping.Wrap
        });
        Grid.SetRow(header, 0);
        root.Children.Add(header);

        if (!RelyingPartyRules.IsLearningSite(rpId))
        {
            var warning = new Border
            {
                Padding = new Thickness(12),
                BorderThickness = new Thickness(1),
                BorderBrush = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                CornerRadius = new CornerRadius(8)
            };
            AutomationProperties.SetAutomationId(warning, "ProductionWarning");
            warning.Child = new TextBlock
            {
                Text = AppStrings.Get("ProductionWarning"),
                Foreground = (Brush)Application.Current.Resources["SystemFillColorCriticalBrush"],
                FontSize = 20,
                FontWeight = Microsoft.UI.Text.FontWeights.Bold,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(warning, 1);
            root.Children.Add(warning);
        }

        var body = new ScrollViewer();
        if (isMakeCredential)
        {
            body.Content = new TextBlock
            {
                Text = AppStrings.Get("OperationMakeBody"),
                TextWrapping = TextWrapping.Wrap,
                VerticalAlignment = VerticalAlignment.Center
            };
        }
        else
        {
            var choices = new StackPanel { Spacing = 8 };
            foreach (var credential in matchingCredentials)
            {
                var choice = new RadioButton
                {
                    Content = $"{credential.RpName}\n{credential.UserName}",
                    Tag = credential,
                    GroupName = "Passkeys",
                    MinWidth = 360
                };
                choice.Checked += (sender, _) =>
                {
                    if (sender is RadioButton { IsChecked: true } radio)
                    {
                        SelectedCredential = radio.Tag as StoredCredential;
                    }
                };
                _credentialChoices.Add(choice);
                choices.Children.Add(choice);
            }
            if (_credentialChoices.Count > 0)
            {
                _credentialChoices[0].IsChecked = true;
                SelectedCredential = matchingCredentials[0];
            }
            body.Content = choices;
        }
        Grid.SetRow(body, 2);
        root.Children.Add(body);

        StatusText = new TextBlock { TextWrapping = TextWrapping.Wrap };
        Grid.SetRow(StatusText, 3);
        root.Children.Add(StatusText);

        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 10
        };
        ConfirmButton = new Button
        {
            Content = isMakeCredential ? AppStrings.Get("OperationSaveButton") : AppStrings.Get("OperationSignInButton"),
            Style = Application.Current.Resources["AccentButtonStyle"] as Style
        };
        CancelButton = new Button { Content = AppStrings.Get("OperationCancelButton") };
        AutomationProperties.SetAutomationId(ConfirmButton, "OperationConfirmButton");
        AutomationProperties.SetAutomationId(CancelButton, "OperationCancelButton");
        buttons.Children.Add(ConfirmButton);
        buttons.Children.Add(CancelButton);
        Grid.SetRow(buttons, 4);
        root.Children.Add(buttons);
        Window.Content = root;

        Handle = WinRT.Interop.WindowNative.GetWindowHandle(Window);
        var appWindow = Window.AppWindow;
        var dpi = GetDpiForWindow(Handle) / 96.0;
        var width = (int)(560 * dpi);
        var height = (int)((RelyingPartyRules.IsLearningSite(rpId) ? 460 : 570) * dpi);
        var area = new RectInt32(0, 0, width, height);
        if (ownerWindow != nint.Zero && GetWindowRect(ownerWindow, out var ownerRect))
        {
            area.X = Math.Max(0, ownerRect.Left + ((ownerRect.Right - ownerRect.Left - width) / 2));
            area.Y = Math.Max(0, ownerRect.Top + ((ownerRect.Bottom - ownerRect.Top - height) / 2));
        }
        appWindow.MoveAndResize(area);
        if (ownerWindow != nint.Zero)
        {
            SetWindowLongPtr(Handle, -6, ownerWindow); // GWLP_HWNDPARENT
        }
        appWindow.SetPresenter(AppWindowPresenterKind.Overlapped);
        if (appWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.IsMinimizable = false;
            presenter.IsMaximizable = false;
        }
    }

    public Window Window { get; }
    public nint Handle { get; }
    public Button ConfirmButton { get; }
    public Button CancelButton { get; }
    public TextBlock StatusText { get; }
    public StoredCredential? SelectedCredential { get; private set; }

    public void Show() => Window.Activate();

    public void Close() => Window.Close();

    public void SetBusy(string message)
    {
        ConfirmButton.IsEnabled = false;
        CancelButton.IsEnabled = true;
        StatusText.Text = message;
    }

    [DllImport("user32.dll")]
    private static extern uint GetDpiForWindow(nint window);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool GetWindowRect(nint window, out NativeRect rect);

    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW", SetLastError = true)]
    private static extern nint SetWindowLongPtr(nint window, int index, nint value);

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }
}
