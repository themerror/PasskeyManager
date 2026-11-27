using PasskeyManager.Interop;
using System.Runtime.InteropServices;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.Security.Authentication.WebAuthn;
using Windows.Win32.System.Com;

namespace PasskeyManager.Services;

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class PluginClassFactory : IClassFactory
{
    private static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid PluginInterface = new("d26bcf6f-b54c-43ff-9f06-d5bf148625f7");

    public unsafe void CreateInstance([MarshalAs(UnmanagedType.IUnknown)] object outer, Guid* iid, out void* instance)
    {
        instance = null;
        if (outer is not null)
        {
            throw new COMException(string.Empty, unchecked((int)0x80040110));
        }

        var plugin = new PluginAuthenticator();
        PluginLog.Add(AppStrings.Get("LogComInstanceCreated"), "Info");
        if (*iid == IUnknown)
        {
            instance = (void*)Marshal.GetIUnknownForObject(plugin);
            return;
        }
        if (*iid != PluginInterface)
        {
            throw new InvalidCastException();
        }

        instance = (void*)Marshal.GetComInterfaceForObject(plugin, typeof(IPluginAuthenticator), CustomQueryInterfaceMode.Ignore);
        if (instance == null)
        {
            throw new InvalidCastException();
        }
    }

    public void LockServer(BOOL fLock)
    {
        // The hidden WinUI window keeps this multiple-use server alive.
    }
}

internal static class PluginComServer
{
    private static PluginClassFactory? _factory;
    private static uint _cookie;

    public static void Start()
    {
        if (_factory is not null)
        {
            return;
        }

        _factory = new PluginClassFactory();
        var clsid = PluginConstants.PluginClsid;
        var hr = PInvoke.CoRegisterClassObject(
            clsid,
            _factory,
            CLSCTX.CLSCTX_LOCAL_SERVER,
            REGCLS.REGCLS_MULTIPLEUSE | REGCLS.REGCLS_SUSPENDED,
            out _cookie);
        if (hr < 0)
        {
            _factory = null;
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = PInvoke.CoResumeClassObjects();
        if (hr < 0)
        {
            PInvoke.CoRevokeClassObject(_cookie);
            _factory = null;
            _cookie = 0;
            Marshal.ThrowExceptionForHR(hr);
        }
        PluginLog.Add(AppStrings.Get("LogComFactoryRegistered"), "Success");
    }

    public static void Stop()
    {
        if (_cookie != 0)
        {
            PInvoke.CoRevokeClassObject(_cookie);
            _cookie = 0;
        }
        _factory = null;
    }

}
