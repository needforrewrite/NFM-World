// Milestone 3 smoke test: create the window through NFMWorld.Platform.SDL3's SdlWindow, create an
// FNA3D-backed IGraphicsDevice against its native handle, then load nfm-world's REAL compiled
// Line.fx (see the BuildLineShader MSBuild target in this project's .csproj) as an IShaderModule,
// build a pipeline from it, and draw one hardware-instanced line - proving out the FNA3D
// Effect-blob-as-shader-module design end-to-end (device creation, clearing and presentation were
// already proven in Milestones 1-2). Not wired into WorldGame yet (Milestone 5).
using System.Numerics;
using System.Runtime.InteropServices;
using NFMWorld.FNA3D;
using NFMWorld.Graphics;
using NFMWorld.Graphics.FNA3D;
using NFMWorld.Platform.SDL3;
using NFMWorld.Shaders;
using SDL3;
using ClearOptions = NFMWorld.Graphics.ClearOptions;

const int width = 1280;
const int height = 720;

// All three built-in FNA3D drivers (SDLGPU/Vulkan, D3D11, OpenGL) work fine here - the earlier
// per-frame "Could not claim window for FNA3D renderer" (SDLGPU) and DXGI swapchain failures
// (D3D11) both traced back to FNA3DSwapchain.Present() passing FNA3D_SwapBuffers a NULL
// overrideWindowHandle. That's fine for the OpenGL/D3D11 drivers (which cache a default window
// from device creation) but not SDLGPU, which has no such fallback and casts the parameter
// straight to SDL_Window* every call - fixed by always passing the real window handle through.
// Default driver order (SDLGPU, then D3D11, then OpenGL) is left alone; set FNA3D_FORCE_DRIVER
// via SDL.SDL_SetHint or the environment variable of the same name to pin one for testing.
SDL.SDL_SetLogPriorities(SDL.SDL_LogPriority.SDL_LOG_PRIORITY_VERBOSE);

// FNA3D_PrepareWindowAttributes queries the current video driver, so SDL must already be
// initialized before calling it - SdlWindow.Create() also calls SDL_Init, but that's a harmless,
// ref-counted no-op the second time.
if (!SDL.SDL_Init(SDL.SDL_InitFlags.SDL_INIT_VIDEO))
{
    Console.Error.WriteLine($"SDL_Init failed: {SDL.SDL_GetError()}");
    return 1;
}

var windowFlags = (SDL.SDL_WindowFlags)FNA3DInterop.PrepareWindowAttributes();
using var window = SdlWindow.Create("NFMWorld.Graphics.FNA3D smoke test", width, height, extraFlags: windowFlags);

using var device = FNA3DGraphicsDevice.Create(window.Handle, width, height, vsync: true);

window.Resized += (w, h) => Console.WriteLine($"Window resized to {w}x{h}");
window.KeyChanged += (key, down, repeat) =>
{
    if (down && !repeat)
        Console.WriteLine($"Key {key} down");
};
window.TextInput += c => Console.Write(c);

// ── Line.fx pipeline setup ──

var lineFxb = File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "data", "shaders", "Line.fxb"));
var lineShaderModule = FNA3DGraphicsDevice.LoadEffectModule(lineFxb);

// Stream 0: per-vertex line-quad geometry (4 real, distinct vertices per line - Line.fx's
// "instancing" is over the world transform/flags in stream 1, not over line endpoints). Field
// order/formats match the game's own layout exactly (nfm-world/Mad/Renderer/Renderable/Mesh/
// RenderElements/LineMesh.cs's LineMeshVertexAttribute.VertexDeclaration) - notably Color is a
// packed Byte4Normalized value, not a float3, even though the HLSL side declares "float3 Color :
// COLOR0" (the input assembler unpacks bytes to normalized floats before the shader ever runs).
var geometryLayout = new VertexLayoutDesc(
    Attributes:
    [
        new VertexAttributeDesc("POSITION", 0, 0, VertexAttributeFormat.Float3),    // PositionA
        new VertexAttributeDesc("POSITION", 1, 12, VertexAttributeFormat.Float3),   // PositionB
        new VertexAttributeDesc("TEXCOORD", 0, 24, VertexAttributeFormat.Float1),   // Side
        new VertexAttributeDesc("NORMAL", 0, 28, VertexAttributeFormat.Float3),     // Normal
        new VertexAttributeDesc("POSITION", 2, 40, VertexAttributeFormat.Float3),   // Centroid
        new VertexAttributeDesc("COLOR", 0, 52, VertexAttributeFormat.Byte4Normalized), // Color
        new VertexAttributeDesc("TEXCOORD", 1, 56, VertexAttributeFormat.Float1),   // DecalOffset
    ],
    StrideInBytes: 60);

// Stream 1: per-instance world matrix (4 TEXCOORD registers, one per row) + flag/parameter packs.
var instanceLayout = new VertexLayoutDesc(
    Attributes:
    [
        new VertexAttributeDesc("TEXCOORD", 3, 0, VertexAttributeFormat.Float4),  // world row 0
        new VertexAttributeDesc("TEXCOORD", 4, 16, VertexAttributeFormat.Float4), // world row 1
        new VertexAttributeDesc("TEXCOORD", 5, 32, VertexAttributeFormat.Float4), // world row 2
        new VertexAttributeDesc("TEXCOORD", 6, 48, VertexAttributeFormat.Float4), // world row 3
        new VertexAttributeDesc("TEXCOORD", 7, 64, VertexAttributeFormat.Float4), // parameters
        new VertexAttributeDesc("TEXCOORD", 8, 80, VertexAttributeFormat.Float4), // parameters2
    ],
    StrideInBytes: 96,
    InstanceStepRate: 1);

var pipeline = device.CreatePipeline(new PipelineDesc(
    VertexShader: lineShaderModule,
    PixelShader: lineShaderModule,
    VertexLayouts: [geometryLayout, instanceLayout],
    BlendState: BlendStateDesc.Opaque,
    DepthStencilState: DepthStencilStateDesc.Default,
    RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None },
    Topology: PrimitiveTopology.TriangleList));

var uniforms = pipeline.Reflection.Uniforms.ToDictionary(u => u.Name);
Console.WriteLine($"Pipeline built: {((FNA3DPipelineState)pipeline).PassCount} pass(es), {uniforms.Count} reflected uniforms");

// One line from (-5,0,0) to (5,0,0), expanded into a quad by Side in the vertex shader.
var pointA = new Vector3(-5, 0, 0);
var pointB = new Vector3(5, 0, 0);
var centroid = (pointA + pointB) * 0.5f;
var normal = new Vector3(0, 0, 1);

// Stride 60 bytes/vertex: PositionA(12) PositionB(12) Side(4) Normal(12) Centroid(12) Color(4, packed) DecalOffset(4).
static void WriteVertex(Span<byte> dest, Vector3 a, Vector3 b, float side, Vector3 normal, Vector3 centroid)
{
    var o = 0;
    foreach (var f in (ReadOnlySpan<float>)[a.X, a.Y, a.Z, b.X, b.Y, b.Z, side, normal.X, normal.Y, normal.Z, centroid.X, centroid.Y, centroid.Z])
    {
        BitConverter.TryWriteBytes(dest[o..], f);
        o += 4;
    }
    dest[o] = 255; dest[o + 1] = 255; dest[o + 2] = 255; dest[o + 3] = 255; o += 4; // Color = white
    BitConverter.TryWriteBytes(dest[o..], 0f); // DecalOffset
}

const int vertexStride = 60;
Span<byte> vertexData = new byte[vertexStride * 4];
WriteVertex(vertexData[(vertexStride * 0)..], pointA, pointB, -1f, normal, centroid);
WriteVertex(vertexData[(vertexStride * 1)..], pointA, pointB, 1f, normal, centroid);
WriteVertex(vertexData[(vertexStride * 2)..], pointA, pointB, -2f, normal, centroid);
WriteVertex(vertexData[(vertexStride * 3)..], pointA, pointB, 2f, normal, centroid);

Span<ushort> indexData = [0, 1, 2, 2, 1, 3];

// Transposed for the same reason nfm-world/Mad/Renderer/InstanceData.cs transposes: fxc packs a
// matrix bound across vertex-stream TEXCOORD registers assuming column-major storage, but this
// data is raw memory (not an Effect parameter upload, which XNA/FNA transpose automatically), so
// the CPU side has to pre-transpose it for `mul(vector, world)` to compute the right transform.
var world = Matrix4x4.Transpose(Matrix4x4.Identity);
// parameters: getsShadowed=0, alphaOverride=1, isFullbright=0, glow=0 (see Mad.fxh VS_UnpackParameters)
// parameters2: layer=0 (see Mad.fxh VS_UnpackParameters2)
Span<float> instanceData =
[
    world.M11, world.M12, world.M13, world.M14,
    world.M21, world.M22, world.M23, world.M24,
    world.M31, world.M32, world.M33, world.M34,
    world.M41, world.M42, world.M43, world.M44,
    0f, 1f, 0f, 0f,
    0f, 0f, 0f, 0f,
];

var vertexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexData.Length), vertexData);
var indexBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexData.Length * sizeof(ushort), IndexFormat.UInt16), MemoryMarshal.AsBytes(indexData));
var instanceBuffer = device.CreateBuffer(new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, instanceData.Length * sizeof(float)), MemoryMarshal.AsBytes(instanceData));

var t = 0f;
var verified = false;
while (!window.ShouldQuit)
{
    window.PumpEvents();

    t += 0.01f;
    var clearColor = new ColorRgba(0.5f + 0.5f * MathF.Sin(t), 0.2f, 0.4f);
    var cb = device.AcquireCommandBuffer();
    cb.Clear(ClearOptions.Color | ClearOptions.Depth, clearColor);

    cb.SetViewport(new Viewport(0, 0, width, height));
    cb.SetPipeline(pipeline);

    var view = Matrix4x4.CreateLookAt(new Vector3(MathF.Sin(t) * 15f, 8f, MathF.Cos(t) * 15f), Vector3.Zero, Vector3.UnitY);
    var projection = Matrix4x4.CreatePerspectiveFieldOfView(MathF.PI / 4f, width / (float)height, 0.1f, 1000f);
    var viewProj = view * projection;

    void SetMatrix(string name, in Matrix4x4 m)
    {
        if (!uniforms.TryGetValue(name, out var u)) return;
        Span<float> f = [m.M11, m.M12, m.M13, m.M14, m.M21, m.M22, m.M23, m.M24, m.M31, m.M32, m.M33, m.M34, m.M41, m.M42, m.M43, m.M44];
        cb.SetUniform(u.Offset, MemoryMarshal.AsBytes(f));
    }

    void SetFloats(string name, params float[] values)
    {
        if (!uniforms.TryGetValue(name, out var u)) return;
        cb.SetUniform(u.Offset, MemoryMarshal.AsBytes(values.AsSpan()));
    }

    // HLSL bool parameters are 4-byte ints, and FNA's EffectParameter.SetValue(bool) writes a
    // real int - not a float bit pattern - so match that.
    void SetBool(string name, bool value)
    {
        if (!uniforms.TryGetValue(name, out var u)) return;
        Span<int> packed = [value ? 1 : 0];
        cb.SetUniform(u.Offset, MemoryMarshal.AsBytes(packed));
    }

    SetMatrix("View", view);
    SetMatrix("Projection", projection);
    SetMatrix("ViewProj", viewProj);
    SetBool("IsFullbright", true); // skip diffuse/shadow/snap entirely - not wired up in this smoke test
    SetBool("UseBaseColor", true);
    SetFloats("BaseColor", 1f, 0.35f, 0.1f);
    SetFloats("FogColor", 0f, 0f, 0f);
    SetFloats("FogDistance", 1000f); // must be non-zero - VS_ApplyFog divides by it
    SetFloats("FogLogDensity", 0f); // 0 => exp2(0) = 1 => fog leaves color unchanged
    SetFloats("Alpha", 1f); // MainVS writes min(alphaOverride, Alpha) - defaults to 0 (invisible) otherwise
    SetFloats("Darken", 1f); // defaults to 0 (VS_Darken would crush the color to black) otherwise
    SetFloats("HalfThickness", 6f);
    SetFloats("Resolution", width, height);
    SetFloats("DistantOutlineDistanceFalloffWithCutoffMask", 0f);
    SetFloats("DistantOutlineClassicCutoffMask", 0f);
    SetFloats("DistantOutlineDistanceFalloffMask", 0f);

    cb.SetVertexBuffer(0, vertexBuffer, vertexStride);
    cb.SetVertexBuffer(1, instanceBuffer, 96);
    cb.SetIndexBuffer(indexBuffer);
    cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: 2, instanceCount: 1);

    device.Submit(cb);

    // One-shot framebuffer readback: proves the line actually rasterized rather than relying on
    // someone eyeballing the window. FNA3D_ReadBackbuffer only reads the current backbuffer
    // contents, so this has to happen before Swapchain.Present().
    if (!verified)
    {
        verified = true;
        VerifyLineDrawn(device, clearColor);
    }

    device.Swapchain.Present();
}

return 0;

static void VerifyLineDrawn(FNA3DGraphicsDevice device, ColorRgba clearColor)
{
    const int w = 1280, h = 720;
    var pixels = new byte[w * h * 4];
    // Unmanaged staging buffer rather than a pinned array, so the smoke test needs no
    // AllowUnsafeBlocks - FNA3D writes through the pointer, then it's copied back out.
    var staging = Marshal.AllocHGlobal(pixels.Length);
    try
    {
        FNA3D.FNA3D_ReadBackbuffer(device.Handle, 0, 0, w, h, staging, pixels.Length);
        Marshal.Copy(staging, pixels, 0, pixels.Length);
    }
    finally
    {
        Marshal.FreeHGlobal(staging);
    }

    // The clear color is the only other thing on screen, so "differs from it" is a sufficient
    // test for "something was rasterized". The base color is (1.0, 0.35, 0.1) - count exact-ish
    // matches separately to confirm it's the line and not stray garbage.
    var clearR = (byte)(clearColor.R * 255f);
    var clearG = (byte)(clearColor.G * 255f);
    var clearB = (byte)(clearColor.B * 255f);

    int distinct = 0, baseColored = 0;
    int minX = w, minY = h, maxX = -1, maxY = -1;
    for (var y = 0; y < h; y++)
    for (var x = 0; x < w; x++)
    {
        var i = (y * w + x) * 4;
        var r = pixels[i]; var g = pixels[i + 1]; var b = pixels[i + 2];

        if (Math.Abs(r - clearR) <= 8 && Math.Abs(g - clearG) <= 8 && Math.Abs(b - clearB) <= 8)
            continue; // background

        distinct++;
        if (Math.Abs(r - 255) <= 40 && Math.Abs(g - 89) <= 40 && Math.Abs(b - 26) <= 40)
            baseColored++;
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
    }

    Console.WriteLine($"PIXEL CHECK: {distinct} non-background px, {baseColored} base-colored");
    Console.WriteLine(distinct > 0
        ? $"  drawn region: x {minX}..{maxX}, y {minY}..{maxY}"
        : "  NOTHING WAS DRAWN - the line did not rasterize.");
    // A line spanning the viewport should be wide and thin, not a full-screen rectangle.
    if (distinct > 0 && (maxX - minX) < w / 4)
        Console.WriteLine("  WARNING: drawn region is narrow - geometry may be mis-transformed.");
}
