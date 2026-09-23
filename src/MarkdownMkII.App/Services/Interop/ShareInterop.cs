using System.Runtime.InteropServices;
using Windows.ApplicationModel.DataTransfer;
using WinRT;

namespace MarkdownMkII.Services.Interop;

/// <summary>
/// HWND interop per DataTransferManager, stesso schema di Snappy Desktop.
/// </summary>
internal static class ShareInterop
{
    private static readonly Guid DataTransferManagerIid = Guid.Parse("A5CAEE9B-8708-49D1-8D36-67D25A8DA00C");

    [ComImport]
    [Guid("3A3DCD6C-3EAB-43DC-BCDE-45671CE800C8")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDataTransferManagerInterop
    {
        IntPtr GetForWindow([In] IntPtr appWindow, [In] ref Guid riid);

        void ShowShareUIForWindow(IntPtr appWindow);
    }

    public static DataTransferManager GetForWindow(IntPtr hwnd)
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        var iid = DataTransferManagerIid;
        var abi = interop.GetForWindow(hwnd, ref iid);
        return MarshalInterface<DataTransferManager>.FromAbi(abi);
    }

    public static void ShowShareUI(IntPtr hwnd)
    {
        var interop = DataTransferManager.As<IDataTransferManagerInterop>();
        interop.ShowShareUIForWindow(hwnd);
    }
}
