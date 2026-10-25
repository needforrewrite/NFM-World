// LLM maintained.
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int D3DCompile(
        IntPtr pSrcData, nuint srcDataSize, IntPtr pSourceName, IntPtr pDefines, IntPtr pInclude,
        [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint, [MarshalAs(UnmanagedType.LPStr)] string pTarget,
        uint flags1, uint flags2, out IntPtr ppCode, out IntPtr ppErrorMsgs);

    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int D3DDisassemble(IntPtr pSrcData, nuint srcDataSize, uint flags,
        IntPtr szComments, out IntPtr ppDisassembly);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr LoadLibraryA(string name);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nuint GetSizeFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetPtrFn(IntPtr self);

    private static string OutPath = "out.dxbc";

    private const uint PACK_COLUMN_MAJOR = 1u << 4;

    private static void Main(string[] args)
    {
        var src = File.ReadAllText(args[0]);
        var target = args.Length > 1 ? args[1] : "vs_5_0";
        if (args.Length > 2) OutPath = args[2];
        var bytes = Encoding.ASCII.GetBytes(src);
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var hr = D3DCompile(pinned.AddrOfPinnedObject(), (nuint)bytes.Length, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, "main", target, PACK_COLUMN_MAJOR, 0, out var blob, out var err);
            Console.WriteLine($"compile hr=0x{hr:X8}");
            if (err != IntPtr.Zero) Console.WriteLine(Marshal.PtrToStringAnsi(err));
            if (hr < 0) return;

            // ID3DBlob vtable: QueryInterface(0), AddRef(1), Release(2), GetBufferPointer(3), GetBufferSize(4).
            var vtbl = Marshal.ReadIntPtr(blob);
            var getSize = Marshal.GetDelegateForFunctionPointer<GetSizeFn>(Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size));
            var blobSize = getSize(blob);

            // Dump the raw DXBC so the input-signature chunk (ISGN) can be parsed directly -
            // D3DDisassemble is unavailable in some d3dcompiler builds.
            var getPtr = Marshal.GetDelegateForFunctionPointer<GetPtrFn>(Marshal.ReadIntPtr(vtbl, 3 * IntPtr.Size));
            var blobPtr = getPtr(blob);
            var raw = new byte[(int)blobSize];
            Marshal.Copy(blobPtr, raw, 0, raw.Length);
            File.WriteAllBytes(OutPath, raw);
            Console.WriteLine($"wrote {raw.Length} bytes to {OutPath}");
        }
        finally { pinned.Free(); }
    }
}
