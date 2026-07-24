using System.Runtime.InteropServices;

namespace ScreenStudio.Native.Encoding;

// Media Foundation COM interfaces for [InterfaceType(InterfaceIsIUnknown)].
// IMPORTANT: the CLR automatically prepends IUnknown's 3 methods (QueryInterface/AddRef/Release)
// to an InterfaceIsIUnknown vtable, so we must NOT redeclare them — doing so would shift every
// method by 3 slots and produce E_NOINTERFACE. Vtable order below mirrors mfobjects.h / mfidl.h.

[ComImport, Guid("2CD2D921-C447-44A7-A13C-4ADAB24B7F3B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFAttributes
{
    // IMFAttributes members (IUnknown slots provided automatically by the runtime):
    [PreserveSig] int GetItem([In] ref Guid guidKey, [In] nint pvValue);
    [PreserveSig] int GetItemType([In] ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem([In] ref Guid guidKey, nint pvValue, out int pbResult);
    [PreserveSig] int Compare(nint pTheirs, int matchType, out int pbResult);
    [PreserveSig] int GetUINT32([In] ref Guid guidKey, out uint punValue);
    [PreserveSig] int GetUINT64([In] ref Guid guidKey, out ulong punValue);
    [PreserveSig] int GetDouble([In] ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength([In] ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString([In] ref Guid guidKey, [Out] char[] pwszValue, int cchBufSize, out int pcchLength);
    [PreserveSig] int GetAllocatedString([In] ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob([In] ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, out IntPtr ppv);
    [PreserveSig] int SetItem([In] ref Guid guidKey, [In] nint pvValue);
    [PreserveSig] int DeleteItem([In] ref Guid guidKey);
    [PreserveSig] int SetUINT32([In] ref Guid guidKey, uint unValue);
    [PreserveSig] int SetUINT64([In] ref Guid guidKey, ulong unValue);
    [PreserveSig] int SetDouble([In] ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
    [PreserveSig] int SetString([In] ref Guid guidKey, [In] string wszValue);
    [PreserveSig] int SetBlob([In] ref Guid guidKey, [In] byte[] pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown([In] ref Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnk);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pGuidKey, out IntPtr pValue);
    [PreserveSig] int CopyAllItems([In] IntPtr pDest);
}

[ComImport, Guid("045FA593-8799-42B8-8759-1D61E3FE9D22"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaBuffer
{
    [PreserveSig] int Lock(out IntPtr ppbBuffer, out int pcbMaxLength, out int pcbCurrentLength);
    [PreserveSig] int Unlock();
    [PreserveSig] int GetCurrentLength(out int pcbCurrentLength);
    [PreserveSig] int SetCurrentLength(int cbCurrentLength);
    [PreserveSig] int GetMaxLength(out int pcbMaxLength);
}

[ComImport, Guid("BDDE0E11-1C19-46A6-9C6E-DC09632EC489"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSample
{
    [PreserveSig] int GetSampleFlags(out int pdwSampleFlags);
    [PreserveSig] int SetSampleFlags(int dwSampleFlags);
    [PreserveSig] int GetSampleTime(out long phnsSampleTime);
    [PreserveSig] int SetSampleTime(long hnsSampleTime);
    [PreserveSig] int GetSampleDuration(out long phnsSampleDuration);
    [PreserveSig] int SetSampleDuration(long hnsSampleDuration);
    [PreserveSig] int GetBufferCount(out int pdwBufferCount);
    [PreserveSig] int GetBufferByIndex(int dwIndex, out IMFMediaBuffer ppBuffer);
    [PreserveSig] int ConvertToContiguousBuffer(out IMFMediaBuffer ppBuffer);
    [PreserveSig] int AddBuffer(IMFMediaBuffer pBuffer);
    [PreserveSig] int RemoveBufferByIndex(int dwIndex);
    [PreserveSig] int RemoveAllBuffers();
    [PreserveSig] int GetTotalLength(out int pcbTotalLength);
    [PreserveSig] int CopyToBuffer(IMFMediaBuffer pBuffer);
}

// IMFMediaType derives from IMFAttributes. We only use IMFAttributes methods, so declare
// it as a distinct interface with the same shape (the COM object implements both; we QI via
// the cast which succeeds because the GUID below is the real IMFMediaType IID and the object
// answers QI for IMFAttributes).
[ComImport, Guid("44AEEDF9-2472-49A6-BF39-7CC25F0F8A3B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFMediaType
{
    // Same IMFAttributes members (IUnknown auto-prepended):
    [PreserveSig] int GetItem([In] ref Guid guidKey, [In] nint pvValue);
    [PreserveSig] int GetItemType([In] ref Guid guidKey, out int pType);
    [PreserveSig] int CompareItem([In] ref Guid guidKey, nint pvValue, out int pbResult);
    [PreserveSig] int Compare(nint pTheirs, int matchType, out int pbResult);
    [PreserveSig] int GetUINT32([In] ref Guid guidKey, out uint punValue);
    [PreserveSig] int GetUINT64([In] ref Guid guidKey, out ulong punValue);
    [PreserveSig] int GetDouble([In] ref Guid guidKey, out double pfValue);
    [PreserveSig] int GetGUID([In] ref Guid guidKey, out Guid pguidValue);
    [PreserveSig] int GetStringLength([In] ref Guid guidKey, out int pcchLength);
    [PreserveSig] int GetString([In] ref Guid guidKey, [Out] char[] pwszValue, int cchBufSize, out int pcchLength);
    [PreserveSig] int GetAllocatedString([In] ref Guid guidKey, out IntPtr ppwszValue, out int pcchLength);
    [PreserveSig] int GetBlobSize([In] ref Guid guidKey, out int pcbBlobSize);
    [PreserveSig] int GetBlob([In] ref Guid guidKey, [Out] byte[] pBuf, int cbBufSize, out int pcbBlobSize);
    [PreserveSig] int GetAllocatedBlob([In] ref Guid guidKey, out IntPtr ppBuf, out int pcbSize);
    [PreserveSig] int GetUnknown([In] ref Guid guidKey, [In] ref Guid riid, out IntPtr ppv);
    [PreserveSig] int SetItem([In] ref Guid guidKey, [In] nint pvValue);
    [PreserveSig] int DeleteItem([In] ref Guid guidKey);
    [PreserveSig] int SetUINT32([In] ref Guid guidKey, uint unValue);
    [PreserveSig] int SetUINT64([In] ref Guid guidKey, ulong unValue);
    [PreserveSig] int SetDouble([In] ref Guid guidKey, double fValue);
    [PreserveSig] int SetGUID([In] ref Guid guidKey, [In] ref Guid guidValue);
    [PreserveSig] int SetString([In] ref Guid guidKey, [In] string wszValue);
    [PreserveSig] int SetBlob([In] ref Guid guidKey, [In] byte[] pBuf, int cbBufSize);
    [PreserveSig] int SetUnknown([In] ref Guid guidKey, [In, MarshalAs(UnmanagedType.IUnknown)] object pUnk);
    [PreserveSig] int LockStore();
    [PreserveSig] int UnlockStore();
    [PreserveSig] int GetCount(out int pcItems);
    [PreserveSig] int GetItemByIndex(int unIndex, out Guid pGuidKey, out IntPtr pValue);
    [PreserveSig] int CopyAllItems([In] IntPtr pDest);
}

[ComImport, Guid("321EE827-CA8F-49CE-99B2-8395F6BD4D3B"), InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMFSinkWriter
{
    [PreserveSig] int AddStream(IMFMediaType pMediaType, out int pdwStreamIndex);
    [PreserveSig] int SetInputMediaType(int dwStreamIndex, IMFMediaType pInputMediaType, IntPtr pEncodingParameters);
    [PreserveSig] int BeginWriting();
    [PreserveSig] int WriteSample(int dwStreamIndex, IMFSample pSample);
    [PreserveSig] int SendStreamTick(int dwStreamIndex, long llTimestamp, int dwAttributeFlags);
    [PreserveSig] int PlaceEncodingParameters(int dwStreamIndex, int dwAttributeFlags);
    [PreserveSig] int NotifyEndOfSegment(int dwStreamIndex);
    [PreserveSig] int Flush(int dwStreamIndex);
    [PreserveSig] int DoFinalize();
    [PreserveSig] int GetServiceForStream(int dwStreamIndex, ref Guid guidService, ref Guid riid, out IntPtr ppv);
    [PreserveSig] int GetStatistics(int dwStreamIndex, out IntPtr pStatistics);
}
