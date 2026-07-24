using System.Runtime.InteropServices;
using Windows.Graphics.DirectX.Direct3D11;

namespace ScreenStudio.Native.Capture;

/// <summary>Bridges a native D3D11 device to the WinRT IDirect3DDevice that Windows Graphics
/// Capture requires. The bridge is the documented CreateDirect3D11DeviceFromDXGIDevice helper,
/// reached through the Direct3D11 interop's IDirect3DDxgiInterfaceAccess.
///
/// STATUS: compile-verified only. WGC needs an interactive desktop session to run; this host
/// has none, so the runtime path is not exercised here. On a real desktop Windows the bridge
/// is the standard one used by every WGC sample.</summary>
internal static class Direct3D11Helper
{
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const int D3D11_SDK_VERSION = 7;

    private static readonly Guid IID_IDirect3DDevice = new("A37624AB-8D5F-4650-9F3B-9D8411DE9AE1");
    private static readonly Guid IID_IDXGIDevice = new("77db9708-4e0e-4d56-9e12-4f2e8530ede0");
    private static readonly Guid IID_IDirect3DDxgiInterfaceAccess = new("A9B3D012-3DF2-4EE3-B8D1-9CE9C90BDB46");

    [DllImport("d3d11.dll")]
    private static extern int D3D11CreateDevice(
        IntPtr idxgiAdapter, int driverType, IntPtr software, uint flags,
        [In] int[]? featureLevels, uint featureLevelsCount, int sdkVersion,
        out IntPtr ppDevice, out int pFeatureLevel, out IntPtr ppImmediateContext);

    /// <summary>Create a hardware D3D11 device and return its DXGI interface pointer.</summary>
    public static IntPtr CreateD3D11Device(out IntPtr device)
    {
        var featureLevels = new[] {
            unchecked((int)0xb000), // 11_0
            unchecked((int)0xa000), // 10_1
            unchecked((int)0x9000), // 10_0
        };
        int hr = D3D11CreateDevice(
            IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
            featureLevels, (uint)featureLevels.Length, D3D11_SDK_VERSION,
            out device, out _, out _);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;

        var dxgiIid = IID_IDXGIDevice;
        hr = Marshal.QueryInterface(device, ref dxgiIid, out IntPtr dxgiDevice);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        return dxgiDevice;
    }

    /// <summary>Wrap a DXGI device as a WinRT IDirect3DDevice. Uses the static helper
    /// exposed on the Direct3D11Device interop projection (CreateDirect3D11DeviceFromDXGIDevice).</summary>
    public static IDirect3DDevice CreateDirect3D11DeviceFromDXGIDevice(IntPtr dxgiDevice)
    {
        var iidInterop = IID_IDirect3DDxgiInterfaceAccess;
        int hr = RoGetActivationFactory(
            "Windows.Graphics.DirectX.Direct3D11.Direct3D11Device",
            ref iidInterop,
            out IntPtr factoryPtr);
        if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
        try
        {
            var access = (IDirect3DDxgiInterfaceAccess)Marshal.GetObjectForIUnknown(factoryPtr);
            var devIid = IID_IDirect3DDevice;
            hr = access.GetInterface(dxgiDevice, ref devIid, out IntPtr devPtr);
            if (hr < 0) throw Marshal.GetExceptionForHR(hr)!;
            return (IDirect3DDevice)Marshal.GetObjectForIUnknown(devPtr);
        }
        finally { Marshal.Release(factoryPtr); }
    }

    [DllImport("combase.dll", PreserveSig = true)]
    private static extern int RoGetActivationFactory(
        [MarshalAs(UnmanagedType.HString)] string activatableId,
        ref Guid iid,
        out IntPtr factory);

    [ComImport, Guid("A9B3D012-3DF2-4EE3-B8D1-9CE9C90BDB46"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IDirect3DDxgiInterfaceAccess
    {
        [PreserveSig] int GetInterface([In] IntPtr pSource, [In] ref Guid iid, out IntPtr ppv);
    }
}
