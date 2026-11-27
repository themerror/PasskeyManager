using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.System.Com;

namespace PasskeyManager;

internal static class Program
{
    private const int RpcETooLate = unchecked((int)0x80010119);

    [STAThread]
    private static unsafe void Main(string[] args)
    {
        // Match the official sample's COM security setup before WinUI starts its STA loop.
        var hr = PInvoke.CoInitializeSecurity(
            default,
            -1,
            default,
            RPC_C_AUTHN_LEVEL.RPC_C_AUTHN_LEVEL_DEFAULT,
            RPC_C_IMP_LEVEL.RPC_C_IMP_LEVEL_IMPERSONATE,
            null,
            EOLE_AUTHENTICATION_CAPABILITIES.EOAC_NONE);
        if (hr < 0 && hr != RpcETooLate)
        {
            Marshal.ThrowExceptionForHR(hr);
        }

        try
        {
            XamlGeneratedProgram.XamlGeneratedMain();
        }
        catch (Exception ex)
        {
            try
            {
                var path = Path.Combine(
                    Windows.Storage.ApplicationData.Current.LocalFolder.Path,
                    "startup-error.log");
                File.AppendAllText(path, ex.ToString() + Environment.NewLine);
            }
            catch
            {
                // Preserve the original startup exception.
            }
            throw;
        }
    }

}
