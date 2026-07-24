using System.Runtime.InteropServices;
using Windows.Graphics.Capture;

namespace ScreenStudio.Native.Capture;

/// <summary>Obtains a GraphicsCaptureItem via the IGraphicsCaptureItemInterop COM factory.
/// CreateFromMonitor / CreateFromWindow are not directly on the projected type; they're
/// reached by QI'ing the static activation factory for IGraphicsCaptureItemInterop.</summary>
internal static class GraphicsCaptureItemInterop
{
    private static readonly Guid IID_IGraphicsCaptureItemInterop = new("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356");
    private static readonly Guid IID_IGraphicsCaptureItem = typeof(GraphicsCaptureItem).GUID;

    private static IGraphicsCaptureItemInterop GetFactory()
    {
        var iid = IID_IGraphicsCaptureItemInterop;
        int hr = RoGetActivationFactory(
            "Windows.Graphics.Capture.GraphicsCaptureItem",
            ref iid,
            out IntPtr factoryPtr);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        return (IGraphicsCaptureItemInterop)Marshal.GetObjectForIUnknown(factoryPtr);
    }

    public static GraphicsCaptureItem CreateFromPrimaryMonitor()
    {
        // The monitor interop takes an HMONITOR. Get the primary monitor handle.
        var mon = MonitorFromWindow(IntPtr.Zero, 1 /*MONITOR_DEFAULTTOPRIMARY*/);
        var factory = GetFactory();
        int hr = factory.CreateForMonitor(mon, IID_IGraphicsCaptureItem, out IntPtr itemPtr);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(itemPtr);
    }

    public static GraphicsCaptureItem CreateFromWindow(IntPtr hwnd)
    {
        var factory = GetFactory();
        int hr = factory.CreateForWindow(hwnd, IID_IGraphicsCaptureItem, out IntPtr itemPtr);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        return (GraphicsCaptureItem)Marshal.GetObjectForIUnknown(itemPtr);
    }

    [DllImport("user32.dll")]
    private static extern IntPtr MonitorFromWindow(IntPtr hwnd, int flags);

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        [MarshalAs(UnmanagedType.HString)] string activatableId,
        ref Guid iid,
        out IntPtr factory);

    [ComImport, Guid("3628E81B-3CAC-4C60-B7F4-23CE0E0C3356"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IGraphicsCaptureItemInterop
    {
        [PreserveSig] int CreateForWindow([In] IntPtr window, [In] ref Guid iid, out IntPtr result);
        [PreserveSig] int CreateForMonitor([In] IntPtr monitor, [In] ref Guid iid, out IntPtr result);
    }
}
