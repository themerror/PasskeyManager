using System.Runtime.InteropServices;

namespace PasskeyManager.Interop;

[ComImport]
[Guid(Iid)]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPluginAuthenticator
{
    public const string Iid = "d26bcf6f-b54c-43ff-9f06-d5bf148625f7";

    [PreserveSig]
    int MakeCredential(nint request, nint response);

    [PreserveSig]
    int GetAssertion(nint request, nint response);

    [PreserveSig]
    int CancelOperation(nint request);

    [PreserveSig]
    int GetLockStatus(out int lockStatus);
}
