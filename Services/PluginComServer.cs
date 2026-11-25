using PasskeyManager.Interop;
using System.Runtime.InteropServices;

namespace PasskeyManager.Services;

[ComImport]
[ComVisible(false)]
[Guid("00000001-0000-0000-C000-000000000046")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IClassFactory
{
    void CreateInstance([MarshalAs(UnmanagedType.Interface)] object? outer, ref Guid iid, out nint instance);
    void LockServer([MarshalAs(UnmanagedType.Bool)] bool lockServer);
}

[ComVisible(true)]
[ClassInterface(ClassInterfaceType.None)]
internal sealed class PluginClassFactory : IClassFactory
{
    private static readonly Guid IUnknown = new("00000000-0000-0000-C000-000000000046");
    private static readonly Guid PluginInterface = new(IPluginAuthenticator.Iid);

    public void CreateInstance([MarshalAs(UnmanagedType.Interface)] object? outer, ref Guid iid, out nint instance)
    {
        if (outer is not null)
        {
            throw new COMException(string.Empty, unchecked((int)0x80040110));
        }

        var plugin = new PluginAuthenticator();
        PluginLog.Add(AppStrings.Get("LogComInstanceCreated"), "Info");
        if (iid == IUnknown)
        {
            instance = Marshal.GetIUnknownForObject(plugin);
            return;
        }
        if (iid != PluginInterface)
        {
            throw new InvalidCastException();
        }

        instance = Marshal.GetComInterfaceForObject(plugin, typeof(IPluginAuthenticator), CustomQueryInterfaceMode.Ignore);
        if (instance == nint.Zero)
        {
            throw new InvalidCastException();
        }
    }

    public void LockServer([MarshalAs(UnmanagedType.Bool)] bool lockServer)
    {
        // The hidden WinUI window keeps this multiple-use server alive.
    }
}

internal static class PluginComServer
{
    private const int ClsctxLocalServer = 0x4;
    private const int RegclsMultipleUse = 0x1;
    private const int RegclsSuspended = 0x4;
    private static PluginClassFactory? _factory;
    private static int _cookie;

    public static void Start()
    {
        if (_factory is not null)
        {
            return;
        }

        _factory = new PluginClassFactory();
        var clsid = PluginConstants.PluginClsid;
        var hr = CoRegisterClassObject(
            ref clsid,
            _factory,
            ClsctxLocalServer,
            RegclsMultipleUse | RegclsSuspended,
            out _cookie);
        if (hr < 0)
        {
            _factory = null;
            Marshal.ThrowExceptionForHR(hr);
        }

        hr = CoResumeClassObjects();
        if (hr < 0)
        {
            CoRevokeClassObject(_cookie);
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
            CoRevokeClassObject(_cookie);
            _cookie = 0;
        }
        _factory = null;
    }

    [DllImport("ole32.dll")]
    private static extern int CoRegisterClassObject(
        ref Guid clsid,
        [MarshalAs(UnmanagedType.IUnknown)] object classObject,
        int context,
        int flags,
        out int cookie);

    [DllImport("ole32.dll")]
    private static extern int CoResumeClassObjects();

    [DllImport("ole32.dll")]
    private static extern int CoRevokeClassObject(int cookie);
}
