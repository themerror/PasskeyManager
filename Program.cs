using System.Runtime.InteropServices;

namespace PasskeyManager;

internal static class Program
{
    private const int RpcETooLate = unchecked((int)0x80010119);

    [STAThread]
    private static void Main(string[] args)
    {
        // Match the official sample's COM security setup before WinUI starts its STA loop.
        var hr = CoInitializeSecurity(
            nint.Zero,
            -1,
            nint.Zero,
            nint.Zero,
            0,
            3,
            nint.Zero,
            0,
            nint.Zero);
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

    [DllImport("ole32.dll")]
    private static extern int CoInitializeSecurity(
        nint securityDescriptor,
        int authenticationServices,
        nint authenticationServiceArray,
        nint reserved1,
        int authenticationLevel,
        int impersonationLevel,
        nint authenticationList,
        int capabilities,
        nint reserved3);
}
