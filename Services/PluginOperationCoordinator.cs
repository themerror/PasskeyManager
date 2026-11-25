using PasskeyManager.Models;
using PasskeyManager.Views;

namespace PasskeyManager.Services;

internal sealed record OperationDecision(bool Confirmed, StoredCredential? Credential, bool VaultGesturePerformed)
{
    public static OperationDecision Cancelled { get; } = new(false, null, false);
}

/// <summary>
/// Coordinates one native WebAuthn operation at a time. COM/RPC threads wait on an
/// async decision while all XAML interaction remains on the WinUI dispatcher.
/// </summary>
internal static class PluginOperationCoordinator
{
    private static readonly object Gate = new();
    private static TaskCompletionSource<OperationDecision>? _pending;
    private static PluginOperationWindow? _window;
    private static Guid _transactionId;

    public static nint WindowHandle
    {
        get { lock (Gate) { return _window?.Handle ?? nint.Zero; } }
    }

    public static Guid TransactionId
    {
        get { lock (Gate) { return _transactionId; } }
    }

    public static Task<OperationDecision> ConfirmMakeCredentialAsync(
        Guid transactionId,
        nint callerWindow,
        string rpId,
        string rpName,
        string userName)
    {
        lock (Gate)
        {
            _transactionId = transactionId;
        }

        if (RelyingPartyRules.IsLearningSite(rpId)
            && PluginSettings.IsSilentOperation()
            && !PluginSettings.IsVaultLocked())
        {
            return Task.FromResult(new OperationDecision(true, null, false));
        }
        return ShowOperationAsync(transactionId, callerWindow, true, rpId, rpName, userName, []);
    }

    public static Task<OperationDecision> ConfirmGetAssertionAsync(
        Guid transactionId,
        nint callerWindow,
        string rpId,
        IReadOnlyList<StoredCredential> credentials)
    {
        lock (Gate)
        {
            _transactionId = transactionId;
        }

        if (RelyingPartyRules.IsLearningSite(rpId)
            && PluginSettings.IsSilentOperation()
            && !PluginSettings.IsVaultLocked()
            && credentials.Count == 1)
        {
            return Task.FromResult(new OperationDecision(true, credentials[0], false));
        }
        return ShowOperationAsync(transactionId, callerWindow, false, rpId, rpId, string.Empty, credentials);
    }

    public static void Complete(int hresult)
    {
        PluginOperationWindow? window;
        lock (Gate)
        {
            window = _window;
            _window = null;
            _pending = null;
            _transactionId = Guid.Empty;
        }
        void Close() => window?.Close();
        if (App.DispatcherQueue.HasThreadAccess)
        {
            Close();
        }
        else
        {
            App.DispatcherQueue.TryEnqueue(Close);
        }
        PluginLog.Add(
            hresult >= 0 ? AppStrings.Get("LogOperationSucceeded") : AppStrings.Format("LogOperationFailedFormat", hresult),
            hresult >= 0 ? "Success" : "Error",
            hresult);
    }

    public static bool Cancel(Guid transactionId)
    {
        lock (Gate)
        {
            if (_transactionId != transactionId)
            {
                return false;
            }
            _pending?.TrySetResult(OperationDecision.Cancelled);
        }
        return true;
    }

    private static Task<OperationDecision> ShowOperationAsync(
        Guid transactionId,
        nint callerWindow,
        bool isMakeCredential,
        string rpId,
        string rpName,
        string userName,
        IReadOnlyList<StoredCredential> credentials)
    {
        var completion = new TaskCompletionSource<OperationDecision>(TaskCreationOptions.RunContinuationsAsynchronously);
        lock (Gate)
        {
            _pending = completion;
        }

        if (!App.DispatcherQueue.TryEnqueue(() =>
        {
            var window = new PluginOperationWindow(isMakeCredential, rpId, rpName, userName, credentials, callerWindow);
            lock (Gate)
            {
                _window = window;
            }

            async void Confirm()
            {
                window.SetBusy(AppStrings.Get("OperationWaitingHello"));
                var unlockedByGesture = false;
                if (PluginSettings.IsVaultLocked())
                {
                    try
                    {
                        unlockedByGesture = await WindowsHelloService.UnlockVaultAsync();
                    }
                    catch (Exception ex)
                    {
                        window.StatusText.Text = ex.Message;
                        window.ConfirmButton.IsEnabled = true;
                        return;
                    }
                    if (!unlockedByGesture)
                    {
                        window.StatusText.Text = AppStrings.Get("OperationHelloRetry");
                        window.ConfirmButton.IsEnabled = true;
                        return;
                    }
                }

                if (!isMakeCredential && window.SelectedCredential is null)
                {
                    window.StatusText.Text = AppStrings.Get("OperationSelectCredential");
                    window.ConfirmButton.IsEnabled = true;
                    return;
                }
                completion.TrySetResult(new OperationDecision(true, window.SelectedCredential, unlockedByGesture));
            }

            window.ConfirmButton.Click += (_, _) => Confirm();
            window.CancelButton.Click += (_, _) => completion.TrySetResult(OperationDecision.Cancelled);
            window.Window.Closed += (_, _) => completion.TrySetResult(OperationDecision.Cancelled);
            window.Show();
        }))
        {
            completion.TrySetResult(OperationDecision.Cancelled);
        }
        return completion.Task;
    }

}
