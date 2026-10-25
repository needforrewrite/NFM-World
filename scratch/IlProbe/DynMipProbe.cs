// LLM maintained.
// Standalone D3D11 probe: does a D3D11_USAGE_DYNAMIC texture accept MipLevels > 1?
// sokol's `dynamic_update` image usage maps to USAGE_DYNAMIC, and sg_update_image
// maps and writes every mip level - so this decides whether a CPU-writable
// texture can carry a mip chain at all.
using System;
using System.Runtime.InteropServices;

internal static class DynMipProbe
{
    private const uint D3D11_SDK_VERSION = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint DXGI_FORMAT_R8G8B8A8_UNORM = 28;
    private const uint BIND_SHADER_RESOURCE = 0x8;
    private const uint USAGE_DEFAULT = 0;
    private const uint USAGE_DYNAMIC = 2;
    private const uint USAGE_IMMUTABLE = 1;
    private const uint CPU_ACCESS_WRITE = 0x10000;
    private const uint CPU_ACCESS_NONE = 0;

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int D3D11CreateDevice(IntPtr pAdapter, int driverType, IntPtr software, uint flags,
        IntPtr pFeatureLevels, uint featureLevels, uint sdkVersion,
        out IntPtr ppDevice, IntPtr pFeatureLevel, out IntPtr ppImmediateContext);

    [StructLayout(LayoutKind.Sequential)]
    private struct TEXTURE2D_DESC
    {
        public uint Width;
        public uint Height;
        public uint MipLevels;
        public uint ArraySize;
        public uint Format;
        public uint SampleCount;
        public uint SampleQuality;
        public uint Usage;
        public uint BindFlags;
        public uint CPUAccessFlags;
        public uint MiscFlags;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateTexture2DFn(IntPtr self, IntPtr desc, IntPtr initialData, out IntPtr ppTexture);

    // ID3D11Device: 0..2 IUnknown, 3 CreateBuffer, 4 .. , 11 CreateInputLayout, ...
    // Creating a texture 2D is vtable slot 3+? - resolve by trying the documented index.
    private const int VT_CREATE_TEXTURE_2D = 3 + 1; // CreateBuffer(3), CreateTexture1D(4)... resolved below

    private static void Main()
    {
        var hr0 = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
            IntPtr.Zero, 0, D3D11_SDK_VERSION, out var device, IntPtr.Zero, out var context);
        Console.WriteLine($"D3D11CreateDevice hr=0x{hr0:X8}");
        if (hr0 < 0) return;

        // ID3D11Device vtable order (after IUnknown's 3):
        //   3 CreateBuffer
        //   4 CreateTexture1D
        //   5 CreateTexture2D
        //   6 CreateTexture3D
        //   7 CreateShaderResourceView
        const int create2D = 5;
        var vtbl = Marshal.ReadIntPtr(device);
        var create = Marshal.GetDelegateForFunctionPointer<CreateTexture2DFn>(
            Marshal.ReadIntPtr(vtbl, create2D * IntPtr.Size));

        Probe(create, device, "DEFAULT,  1 mip, no cpu access", USAGE_DEFAULT, 1, CPU_ACCESS_NONE);
        Probe(create, device, "DEFAULT,  3 mips, no cpu access", USAGE_DEFAULT, 3, CPU_ACCESS_NONE);
        Probe(create, device, "DYNAMIC,  1 mip, cpu write", USAGE_DYNAMIC, 1, CPU_ACCESS_WRITE);
        Probe(create, device, "DYNAMIC,  3 mips, cpu write", USAGE_DYNAMIC, 3, CPU_ACCESS_WRITE);
        Probe(create, device, "IMMUTABLE,3 mips, no cpu access", USAGE_IMMUTABLE, 3, CPU_ACCESS_NONE);

        Marshal.Release(context);
        Marshal.Release(device);
    }

    private static void Probe(CreateTexture2DFn create, IntPtr device, string label, uint usage, uint mips, uint cpu)
    {
        var desc = new TEXTURE2D_DESC
        {
            Width = 64,
            Height = 64,
            MipLevels = mips,
            ArraySize = 1,
            Format = DXGI_FORMAT_R8G8B8A8_UNORM,
            SampleCount = 1,
            SampleQuality = 0,
            Usage = usage,
            BindFlags = BIND_SHADER_RESOURCE,
            CPUAccessFlags = cpu,
            MiscFlags = 0,
        };

        var pinned = GCHandle.Alloc(desc, GCHandleType.Pinned);
        try
        {
            // Immutable needs initial data for every mip or D3D11 rejects it; the other usages
            // must NOT be given initial data here, so the probe isolates the usage/mip interaction.
            IntPtr init = IntPtr.Zero;
            if (usage == USAGE_IMMUTABLE) init = AllocSubresources(mips);
            var hr = create(device, pinned.AddrOfPinnedObject(), init, out var texture);
            if (hr >= 0 && texture != IntPtr.Zero) Marshal.Release(texture);
            Console.WriteLine($"  hr=0x{hr:X8} {(hr >= 0 ? "OK  " : "FAIL")}  {label}");
        }
        finally { pinned.Free(); }
    }

    /// <summary>Zeroed D3D11_SUBRESOURCE_DATA for <paramref name="mips"/> levels.</summary>
    private static IntPtr AllocSubresources(uint mips)
    {
        var size = 8 + 2 * IntPtr.Size; // ptr, rowpitch, depthpitch
        var block = Marshal.AllocHGlobal(size * (int)mips);
        for (var i = 0; i < mips; i++)
        {
            var width = Math.Max(64 >> i, 1);
            var height = Math.Max(64 >> i, 1);
            var data = Marshal.AllocHGlobal(width * height * 4);
            Marshal.WriteIntPtr(block, i * size, data);
            Marshal.WriteInt32(block, i * size + IntPtr.Size, width * 4);
            Marshal.WriteInt32(block, i * size + IntPtr.Size + 4, width * height * 4);
        }
        return block;
    }
}
