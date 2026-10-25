// LLM maintained.
// Standalone D3D11 probe: must the input layout's declared semantic name match the
// compiled shader's own input signature?
//
// Why this matters here: sokol's D3D11 backend builds D3D11_INPUT_ELEMENT_DESC from
// desc->layout.attrs[i] for format/slot/offset, but takes SemanticName/SemanticIndex
// from shd->d3d11.attrs[i] - which the backend fills from the sg_shader_desc's
// hlsl_sem_name/hlsl_sem_index (sokol_gfx.h:14455 and :14248). So the semantic name
// comes from the SHADER DESC, not from the compiled bytecode.
//
// spirv-cross renames every vertex input to TEXCOORD<location> in the HLSL it emits
// (see out.Vertex.hlsl: v_Position : TEXCOORD0, never POSITION0). So a generated
// bundle's compiled signature carries no POSITION/COLOR/NORMAL semantics at all,
// while the app's layouts name exactly those. This probe settles whether D3D11
// tolerates that mismatch or rejects the input layout outright.
//
// The fixture is uniform `float4` inputs and every layout element is declared
// R32G32B32A32_FLOAT, so the ONLY thing varying between cases is the semantic name.
// An earlier revision declared float3 for all elements, which made even the
// control case fail on a genuine format mismatch and proved nothing.
using System;
using System.Runtime.InteropServices;
using System.Text;

internal static class Program
{
    private const uint D3D11_SDK_VERSION = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;
    private const uint FMT_R32G32B32A32_FLOAT = 2;
    private const uint PER_VERTEX = 0;

    [DllImport("d3d11.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern int D3D11CreateDevice(IntPtr pAdapter, int driverType, IntPtr software, uint flags,
        IntPtr pFeatureLevels, uint featureLevels, uint sdkVersion,
        out IntPtr ppDevice, IntPtr pFeatureLevel, out IntPtr ppImmediateContext);

    [DllImport("d3dcompiler_47.dll", CallingConvention = CallingConvention.Cdecl)]
    private static extern int D3DCompile(IntPtr pSrcData, nuint srcDataSize, IntPtr pSourceName, IntPtr pDefines,
        IntPtr pInclude, [MarshalAs(UnmanagedType.LPStr)] string pEntrypoint,
        [MarshalAs(UnmanagedType.LPStr)] string pTarget, uint flags1, uint flags2,
        out IntPtr ppCode, out IntPtr ppErrorMsgs);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT_ELEMENT
    {
        public IntPtr SemanticName;
        public uint SemanticIndex;
        public uint Format;
        public uint InputSlot;
        public uint AlignedByteOffset;
        public uint InputSlotClass;
        public uint InstanceDataStepRate;
    }

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate int CreateInputLayoutFn(IntPtr self, IntPtr descs, uint count, IntPtr bytecode,
        nuint bytecodeLength, out IntPtr ppLayout);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate nuint GetBufferSizeFn(IntPtr self);

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    private delegate IntPtr GetBufferPointerFn(IntPtr self);

    // ID3D11Device: 0..2 IUnknown, 3 CreateBuffer, 4..10 textures/views, 11 CreateInputLayout.
    private const int VT_CREATE_INPUT_LAYOUT = 11;

    // The shape spirv-cross emits, reproduced exactly: both inputs are float4 and both
    // are named TEXCOORD<n>, position included. Nothing here says POSITION or COLOR.
    private const string SpirvCrossShape = """
        struct SPIRV_Cross_Input
        {
            float4 v_Position : TEXCOORD0;
            float4 v_Color : TEXCOORD1;
        };
        float4 main(SPIRV_Cross_Input stage_input) : SV_POSITION
        {
            return stage_input.v_Position + stage_input.v_Color;
        }
        """;

    private static void Main()
    {
        var hr0 = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
            IntPtr.Zero, 0, D3D11_SDK_VERSION, out var device, IntPtr.Zero, out var context);
        Console.WriteLine($"D3D11CreateDevice hr=0x{hr0:X8}");
        if (hr0 < 0) return;

        var vs = Compile(SpirvCrossShape, "vs_5_0");
        if (vs == IntPtr.Zero) { Console.WriteLine("fixture failed to compile"); return; }

        // The control first: if this one does not pass, the probe is broken and nothing
        // the other cases say means anything.
        // A DXBC container begins with the ASCII magic "DXBC". Reading it off the data
        // pointer proves the pointer is the bytecode and not the COM object - the first two
        // revisions of this probe passed the interface pointer straight to CreateInputLayout,
        // which is why every case including the control failed.
        var data = GetBufferPointer(vs);
        var magic = Marshal.ReadInt32(data);
        Console.WriteLine($"blob magic = 0x{magic:X8} (" +
            $"{(char)(magic & 0xFF)}{(char)((magic >> 8) & 0xFF)}{(char)((magic >> 16) & 0xFF)}{(char)((magic >> 24) & 0xFF)})");
        Console.WriteLine($"GetBufferSize = {GetBufferSize(vs)} bytes");

        Console.WriteLine("all elements R32G32B32A32_FLOAT; signature is float4 TEXCOORD0, float4 TEXCOORD1");
        Probe(device, vs, "CONTROL  TEXCOORD0/TEXCOORD1  (names match the signature)",
            [("TEXCOORD", 0, 0), ("TEXCOORD", 1, 16)]);

        Probe(device, vs, "the app's layout: POSITION0/COLOR0  (names do NOT match)",
            [("POSITION", 0, 0), ("COLOR", 0, 16)]);

        Probe(device, vs, "Mixed: POSITION0/TEXCOORD1",
            [("POSITION", 0, 0), ("TEXCOORD", 1, 16)]);

        Probe(device, vs, "Superset: TEXCOORD0/TEXCOORD1/TEXCOORD2",
            [("TEXCOORD", 0, 0), ("TEXCOORD", 1, 16), ("TEXCOORD", 2, 32)]);

        Marshal.Release(context);
        Marshal.Release(device);
    }

    private static void Probe(IntPtr device, IntPtr vs, string label, (string Name, uint Index, uint Offset)[] elements)
    {
        var strings = new IntPtr[elements.Length];
        var elementSize = Marshal.SizeOf<INPUT_ELEMENT>();
        var descs = Marshal.AllocHGlobal(elementSize * elements.Length);
        try
        {
            for (var i = 0; i < elements.Length; i++)
            {
                strings[i] = Marshal.StringToHGlobalAnsi(elements[i].Name);
                var element = new INPUT_ELEMENT
                {
                    SemanticName = strings[i],
                    SemanticIndex = elements[i].Index,
                    Format = FMT_R32G32B32A32_FLOAT,
                    InputSlot = 0,
                    AlignedByteOffset = elements[i].Offset,
                    InputSlotClass = PER_VERTEX,
                    InstanceDataStepRate = 0,
                };
                Marshal.StructureToPtr(element, descs + i * elementSize, false);
            }

            // sokol creates the layout against the VS blob it compiled.
            var vtbl = Marshal.ReadIntPtr(device);
            var create = Marshal.GetDelegateForFunctionPointer<CreateInputLayoutFn>(
                Marshal.ReadIntPtr(vtbl, VT_CREATE_INPUT_LAYOUT * IntPtr.Size));
            var hr = create(device, descs, (uint)elements.Length, GetBufferPointer(vs), GetBufferSize(vs), out var layout);
            if (hr >= 0 && layout != IntPtr.Zero) Marshal.Release(layout);
            Console.WriteLine($"  hr=0x{hr:X8} {(hr >= 0 ? "ACCEPTED" : "REJECTED")}  {label}");
        }
        finally
        {
            foreach (var s in strings) Marshal.FreeHGlobal(s);
            Marshal.FreeHGlobal(descs);
        }
    }

    private static nuint GetBufferSize(IntPtr blob)
    {
        // ID3DBlob: 0..2 IUnknown, 3 GetBufferPointer, 4 GetBufferSize.
        var vtbl = Marshal.ReadIntPtr(blob);
        var fn = Marshal.GetDelegateForFunctionPointer<GetBufferSizeFn>(Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size));
        return fn(blob);
    }

    private static IntPtr GetBufferPointer(IntPtr blob)
    {
        // ID3DBlob slot 3. The blob follows the DXBC container format, so the returned
        // pointer starts with the "DXBC" magic.
        var vtbl = Marshal.ReadIntPtr(blob);
        var fn = Marshal.GetDelegateForFunctionPointer<GetBufferPointerFn>(Marshal.ReadIntPtr(vtbl, 3 * IntPtr.Size));
        return fn(blob);
    }

    private static IntPtr Compile(string source, string target)
    {
        var bytes = Encoding.ASCII.GetBytes(source);
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var hr = D3DCompile(pinned.AddrOfPinnedObject(), (nuint)bytes.Length, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, "main", target, 0, 0, out var code, out var errors);
            if (hr < 0)
            {
                var message = errors == IntPtr.Zero ? "(no error blob)" : Marshal.PtrToStringAnsi(errors);
                Console.WriteLine($"compile failed hr=0x{hr:X8}: {message}");
                return IntPtr.Zero;
            }
            return code;
        }
        finally { pinned.Free(); }
    }
}
