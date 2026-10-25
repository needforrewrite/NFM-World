// LLM maintained.
// Standalone D3D11 probe: does the shader compiler's actual generated HLSL compile under
// D3DCompile, and does it bind against the app's actual vertex layout?
//
// This is the end-to-end check for the two fixes that make a generated bundle usable on the
// sokol D3D11 backend:
//
//   1. The bundle now carries HLSL (the vendored sokol.dll is a D3D11-only build that cannot
//      consume SPIR-V), so the HLSL has to survive D3DCompile.
//   2. The HLSL now declares the source's original semantics, restored through spirv-cross's
//      vertex-attribute remap, instead of spirv-cross's default TEXCOORD<location> naming -
//      because D3D11 matches the input layout's semantic name against the compiled shader's
//      own signature, and the app's layouts name POSITION/COLOR/NORMAL.
//
// The fixture is the real thing, not a paraphrase: the HLSL is the file the compiler wrote
// (`Line.Vertex.hlsl`), and the layout is NFMWorld's own `LineMesh.VertexLayout`, copied
// verbatim. A mismatch in either direction shows up as a failed CreateInputLayout.
//
// Usage: IlProbeLine <generated-hlsl-path>
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;

internal static class LineEndToEnd
{
    private const uint D3D11_SDK_VERSION = 7;
    private const int D3D_DRIVER_TYPE_HARDWARE = 1;

    // DXGI_FORMAT values used by the app's layouts. The component count has to match the
    // shader signature's for CreateInputLayout to accept the element, so these are not
    // interchangeable: the real Line.fx declares `float Side : TEXCOORD0` and
    // `float DecalOffset : TEXCOORD1`, which are one component, not three.
    private const uint FMT_R32_FLOAT = 41;
    private const uint FMT_R32G32B32_FLOAT = 6;
    private const uint FMT_R32G32B32A32_FLOAT = 2;
    private const uint FMT_R8G8B8A8_UNORM = 28;

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

    private const int VT_CREATE_INPUT_LAYOUT = 11;

    /// <summary>
    /// NFMWorld's <c>LineMesh.LineMeshVertexAttribute.VertexLayout</c>, copied verbatim
    /// (nfm-world/Mad/Renderer/Renderable/Mesh/RenderElements/LineMesh.cs:234).
    /// </summary>
    private static readonly (string Semantic, int Slot, int Offset, uint Format)[] LineMeshLayout =
    [
        ("POSITION", 0, 0, FMT_R32G32B32_FLOAT),
        ("POSITION", 1, 12, FMT_R32G32B32_FLOAT),
        ("TEXCOORD", 0, 24, FMT_R32_FLOAT),
        ("NORMAL", 0, 28, FMT_R32G32B32_FLOAT),
        ("POSITION", 2, 40, FMT_R32G32B32_FLOAT),
        ("COLOR", 0, 52, FMT_R8G8B8A8_UNORM),
        ("TEXCOORD", 1, 56, FMT_R32_FLOAT),
    ];

    /// <summary>
    /// The bundle's instance stream, NFMWorld's <c>InstanceData.VertexLayout</c>, copied verbatim
    /// (nfm-world/Mad/Renderer/InstanceData.cs:17). Six TEXCOORD registers at slot 1, stepping once
    /// per instance.
    /// </summary>
    private static readonly (string Semantic, int Slot, int Offset, uint Format)[] InstanceLayout =
    [
        ("TEXCOORD", 3, 0, FMT_R32G32B32A32_FLOAT),
        ("TEXCOORD", 4, 16, FMT_R32G32B32A32_FLOAT),
        ("TEXCOORD", 5, 32, FMT_R32G32B32A32_FLOAT),
        ("TEXCOORD", 6, 48, FMT_R32G32B32A32_FLOAT),
        ("TEXCOORD", 7, 64, FMT_R32G32B32A32_FLOAT),
        ("TEXCOORD", 8, 80, FMT_R32G32B32A32_FLOAT),
    ];

    private static string _sourcePath = "";

    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.WriteLine("usage: IlProbeLine <generated-hlsl-path>");
            return 2;
        }

        _sourcePath = args[0];
        var source = File.ReadAllText(_sourcePath);
        Console.WriteLine($"==> {args[0]} ({source.Length} chars)");

        var hr0 = D3D11CreateDevice(IntPtr.Zero, D3D_DRIVER_TYPE_HARDWARE, IntPtr.Zero, 0,
            IntPtr.Zero, 0, D3D11_SDK_VERSION, out var device, IntPtr.Zero, out var context);
        Console.WriteLine($"D3D11CreateDevice hr=0x{hr0:X8}");
        if (hr0 < 0) return 1;

        var vs = Compile(source, "vs_5_0");
        if (vs == IntPtr.Zero)
        {
            Console.WriteLine("RESULT: the generated HLSL does not compile");
            return 1;
        }

        var magic = Marshal.ReadInt32(GetBufferPointer(vs));
        Console.WriteLine($"compiled: {GetBufferSize(vs)} bytes of DXBC (magic 0x{magic:X8})");
        Console.WriteLine();

        // The control first. A probe that only ever reports success proves nothing - the version
        // of this probe that first ran here had a wrong fixture and passed for the wrong reason.
        // This one takes the same shader and rewrites the input struct's semantics back to what
        // spirv-cross emits without the remap (TEXCOORD<location>), which is the behavior this
        // whole change exists to fix, and confirms that shape really is rejected. If the control
        // is ever ACCEPTED, the instrument is broken and the passing case below means nothing.
        RunControl(device);

        // sokol builds ONE input layout containing every attribute of every stream, not one per
        // stream (_sg_d3d11_create_pipeline fills a single d3d11_comps array indexed by
        // attr_index across all slots). Reproducing that is the point: a layout covering only one
        // of the two streams would be rejected for the shader's other inputs, which says nothing
        // about what the app actually does.
        //
        // The check that matters: the app's real layouts against this shader's real signature.
        var elements = new List<(string Semantic, int Slot, int Offset, uint Format, bool PerInstance)>();
        foreach (var e in LineMeshLayout) elements.Add((e.Semantic, e.Slot, e.Offset, e.Format, false));
        foreach (var e in InstanceLayout) elements.Add((e.Semantic, e.Slot, e.Offset, e.Format, true));

        var ok = Probe(device, vs, "LineMesh slot 0 + InstanceData slot 1, as sokol combines them", elements);

        Marshal.Release(context);
        Marshal.Release(device);
        Console.WriteLine();
        Console.WriteLine(ok
            ? "RESULT: the app's layouts bind to the generated shader"
            : "RESULT: the combined layout was REJECTED against the generated shader");
        return ok ? 0 : 1;
    }

    /// <summary>
    /// Confirms the pre-fix naming really does fail, using the same real shader and the same real
    /// layout. Reproduces spirv-cross's default by renaming each input in the generated HLSL's
    /// input struct to <c>TEXCOORD&lt;location&gt;</c> in declaration order - which is exactly what
    /// the compiler emitted before the vertex-attribute remap was added.
    /// </summary>
    private static void RunControl(IntPtr device)
    {
        var source = File.ReadAllText(_sourcePath);
        var prefix = source[..source.IndexOf("SPIRV_Cross_Input", StringComparison.Ordinal)];
        var rest = source[source.IndexOf("SPIRV_Cross_Input", StringComparison.Ordinal)..];

        var lines = rest.Split('\n').ToList();
        var location = 0;
        for (var i = 0; i < lines.Count; i++)
        {
            var line = lines[i];
            var trimmed = line.Trim();
            if (trimmed.StartsWith("};")) break;
            var colon = line.IndexOf(':');
            if (colon < 0 || !trimmed.EndsWith(";")) continue;
            lines[i] = line[..(colon + 1)] + $" TEXCOORD{location};";
            location++;
        }

        var renamed = prefix + string.Join('\n', lines);
        var vs = Compile(renamed, "vs_5_0");
        if (vs == IntPtr.Zero)
        {
            Console.WriteLine("why the control could not compile the renamed shader");
            return;
        }

        var elements = new List<(string Semantic, int Slot, int Offset, uint Format, bool PerInstance)>();
        foreach (var e in LineMeshLayout) elements.Add((e.Semantic, e.Slot, e.Offset, e.Format, false));
        foreach (var e in InstanceLayout) elements.Add((e.Semantic, e.Slot, e.Offset, e.Format, true));

        var ok = Probe(device, vs, $"[CONTROL] spirv-cross naming ({location} inputs renamed TEXCOORD0..{location - 1})", elements);
        Console.WriteLine(ok
            ? "  <-- the control was ACCEPTED, so this probe cannot tell the two cases apart"
            : "  control rejected as expected, so the acceptance below is meaningful");
        Console.WriteLine();
    }

    private static string[] SignatureOf(string source)
    {
        var names = new List<string>();
        var lines = source.Split('\n');
        var inStruct = false;
        foreach (var line in lines)
        {
            if (line.Contains("SPIRV_Cross_Input")) { inStruct = true; continue; }
            if (!inStruct) continue;
            var trimmed = line.Trim();
            if (trimmed.StartsWith("};")) break;
            var colon = trimmed.IndexOf(':');
            if (colon < 0 || !trimmed.EndsWith(";")) continue;
            names.Add(trimmed[(colon + 1)..].TrimEnd(';', ' ', '\r'));
        }
        return names.ToArray();
    }

    private static bool Probe(IntPtr device, IntPtr vs, string label,
        List<(string Semantic, int Slot, int Offset, uint Format, bool PerInstance)> elements)
    {
        var strings = new IntPtr[elements.Count];
        var elementSize = Marshal.SizeOf<INPUT_ELEMENT>();
        var descs = Marshal.AllocHGlobal(elementSize * elements.Count);
        try
        {
            for (var i = 0; i < elements.Count; i++)
            {
                var e = elements[i];
                strings[i] = Marshal.StringToHGlobalAnsi(e.Semantic);
                var element = new INPUT_ELEMENT
                {
                    SemanticName = strings[i],
                    SemanticIndex = (uint)e.Slot,
                    Format = e.Format,
                    InputSlot = e.PerInstance ? 1u : 0u,
                    AlignedByteOffset = (uint)e.Offset,
                    InputSlotClass = e.PerInstance ? 1u : PER_VERTEX,
                    InstanceDataStepRate = e.PerInstance ? 1u : 0u,
                };
                Marshal.StructureToPtr(element, descs + i * elementSize, false);
            }

            var vtbl = Marshal.ReadIntPtr(device);
            var create = Marshal.GetDelegateForFunctionPointer<CreateInputLayoutFn>(
                Marshal.ReadIntPtr(vtbl, VT_CREATE_INPUT_LAYOUT * IntPtr.Size));
            var hr = create(device, descs, (uint)elements.Count, GetBufferPointer(vs), GetBufferSize(vs), out var layout);
            if (hr >= 0 && layout != IntPtr.Zero) Marshal.Release(layout);
            var ok = hr >= 0;
            Console.WriteLine($"  hr=0x{hr:X8} {(ok ? "ACCEPTED" : "REJECTED")}  {label}");
            Console.WriteLine($"      declares: {string.Join(", ", elements.Select(e => $"{e.Semantic}{e.Slot}@{(e.PerInstance ? "inst" : "vert")}{e.Offset}"))}");
            return ok;
        }
        finally
        {
            foreach (var s in strings) Marshal.FreeHGlobal(s);
            Marshal.FreeHGlobal(descs);
        }
    }

    private static nuint GetBufferSize(IntPtr blob)
    {
        var vtbl = Marshal.ReadIntPtr(blob);
        var fn = Marshal.GetDelegateForFunctionPointer<GetBufferSizeFn>(Marshal.ReadIntPtr(vtbl, 4 * IntPtr.Size));
        return fn(blob);
    }

    private static IntPtr GetBufferPointer(IntPtr blob)
    {
        var vtbl = Marshal.ReadIntPtr(blob);
        var fn = Marshal.GetDelegateForFunctionPointer<GetBufferPointerFn>(Marshal.ReadIntPtr(vtbl, 3 * IntPtr.Size));
        return fn(blob);
    }

    private static IntPtr Compile(string source, string target)
    {
        var bytes = Encoding.UTF8.GetBytes(source);
        var pinned = GCHandle.Alloc(bytes, GCHandleType.Pinned);
        try
        {
            var hr = D3DCompile(pinned.AddrOfPinnedObject(), (nuint)bytes.Length, IntPtr.Zero, IntPtr.Zero,
                IntPtr.Zero, "main", target, 0, 0, out var code, out var errors);
            if (hr < 0)
            {
                var message = errors == IntPtr.Zero ? "(no error blob)" : Marshal.PtrToStringAnsi(errors);
                Console.WriteLine($"D3DCompile failed hr=0x{hr:X8}:\n{message}");
                return IntPtr.Zero;
            }
            return code;
        }
        finally { pinned.Free(); }
    }
}
