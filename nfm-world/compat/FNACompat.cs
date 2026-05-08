using System.Runtime.InteropServices;
using Microsoft.Xna.Framework;
using MoonWorks.Graphics;
using GpuBuffer = MoonWorks.Graphics.Buffer;
using RealGraphicsDevice = MoonWorks.Graphics.GraphicsDevice;

namespace nfm_world.compat;

// ============================================================
// FNA → MoonWorks compatibility shims for 3D rendering pipeline.
// These types allow existing rendering code to compile while 
// the full port to MoonWorks command-buffer model is completed.
// Each type is marked [Obsolete] to track porting progress.
// ============================================================

#pragma warning disable CS0618 // Type or member is obsolete

// ============================================================
// RealGraphicsDevice wrapper
// ============================================================

/// <summary>Wraps MoonWorks GraphicsDevice with FNA-style immediate-mode state machine API.</summary>
[Obsolete("Port to MoonWorks command-buffer rendering")]
public class GraphicsDeviceCompat
{
    public RealGraphicsDevice Device { get; }

    public GraphicsDeviceCompat(RealGraphicsDevice device) { Device = device; }

    public static implicit operator RealGraphicsDevice(GraphicsDeviceCompat c) => c.Device;
    public static implicit operator GraphicsDeviceCompat(RealGraphicsDevice d) => new(d);

    // Delegate MoonWorks API through to real device
    public CommandBuffer AcquireCommandBuffer() => Device.AcquireCommandBuffer();
    public void Submit(CommandBuffer cb) => Device.Submit(cb);
    public Fence SubmitAndAcquireFence(CommandBuffer cb) => Device.SubmitAndAcquireFence(cb);
    public void WaitForFence(Fence fence) => Device.WaitForFence(fence);
    public void Wait() => Device.Wait();

    // FNA state machine properties (no-op)
    public BlendState BlendState { get; set; } = BlendState.Opaque;
    public DepthStencilState DepthStencilState { get; set; } = DepthStencilState.Default;
    public RasterizerState RasterizerState { get; set; } = RasterizerState.CullCounterClockwise;
    public SamplerStateCollection SamplerStates { get; } = new();
    public TextureCollection Textures { get; } = new();
    public Viewport Viewport { get; set; }
    public Microsoft.Xna.Framework.Rectangle ScissorRectangle { get; set; }

    // Buffer binding (no-op)
    public void SetVertexBuffer(VertexBuffer vb) { }
    public void SetVertexBuffers(VertexBuffer a, VertexBufferBinding b) { }
    public void SetVertexBuffers(params VertexBufferBinding[] bindings) { }
    public IndexBuffer Indices { get; set; }

    // Render target (no-op)
    public void SetRenderTarget(RenderTarget2D target) { }
    public void SetRenderTargets(params RenderTargetBinding[] targets) { }
    public void SetRenderTargets(params RenderTarget2D[] targets) { }
    public RenderTargetBinding[] GetRenderTargets() => [];

    // Clear (no-op)
    public void Clear(Color color) { }
    public void Clear(ClearOptions options, Color color, float depth, int stencil) { }

    // Draw calls (no-op)
    public void DrawPrimitives(PrimitiveType type, int startVertex, int primitiveCount) { }
    public void DrawIndexedPrimitives(PrimitiveType type, int baseVertex, int minVertex,
        int numVertices, int startIndex, int primitiveCount) { }
    public void DrawInstancedPrimitives(PrimitiveType type, int baseVertex, int minVertex,
        int numVertices, int startIndex, int primitiveCount, int instanceCount) { }
    public void DrawUserPrimitives<T>(PrimitiveType type, T[] data, int vertexOffset,
        int primitiveCount) where T : struct { }
    public void DrawUserPrimitives<T>(PrimitiveType type, T[] data, int vertexOffset,
        int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct { }
    public void DrawUserIndexedPrimitives<T>(PrimitiveType type, T[] vertexData,
        int vertexOffset, int numVertices, short[] indexData, int indexOffset,
        int primitiveCount) where T : struct { }
    public void DrawUserIndexedPrimitives<T>(PrimitiveType type, T[] vertexData,
        int vertexOffset, int numVertices, short[] indexData, int indexOffset,
        int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct { }
    public void DrawUserIndexedPrimitives<T>(PrimitiveType type, T[] vertexData,
        int vertexOffset, int numVertices, int[] indexData, int indexOffset,
        int primitiveCount) where T : struct { }
    public void DrawUserIndexedPrimitives<T>(PrimitiveType type, T[] vertexData,
        int vertexOffset, int numVertices, int[] indexData, int indexOffset,
        int primitiveCount, VertexDeclaration vertexDeclaration) where T : struct { }
}

// ============================================================
// GraphicsDeviceManager stub
// ============================================================

[Obsolete("Port to MoonWorks Window/SwapchainComposition")]
public class GraphicsDeviceManager
{
    public bool SynchronizeWithVerticalRetrace { get; set; } = true;
    public int PreferredBackBufferWidth { get; set; } = 1280;
    public int PreferredBackBufferHeight { get; set; } = 720;
    public bool IsFullScreen { get; set; }
    public void ApplyChanges() { }
}

// ============================================================
// Effect types
// ============================================================

/// <summary>Stub for FNA's Effect.</summary>
[Obsolete("Port to MoonWorks GraphicsPipeline + Shader")]
public class Effect : IDisposable
{
    public EffectParameterCollection Parameters { get; } = new();
    public EffectTechniqueCollection Techniques { get; } = new();
    public EffectTechnique CurrentTechnique { get; set; }

    public Effect(RealGraphicsDevice device, byte[] effectCode) { }
    public Effect() { }

    public void Dispose() { }
}

[Obsolete("Port to MoonWorks uniform push")]
public class EffectParameter
{
    public void SetValue(float value) { }
    public void SetValue(int value) { }
    public void SetValue(bool value) { }
    public void SetValue(Vector2 value) { }
    public void SetValue(Vector3 value) { }
    public void SetValue(Vector4 value) { }
    public void SetValue(Matrix value) { }
    public void SetValue(Texture texture) { }
    public void SetValue(float[] value) { }
    public void SetValue(Matrix[] value) { }
}

[Obsolete("Port to MoonWorks uniform structs")]
public class EffectParameterCollection
{
    private readonly Dictionary<string, EffectParameter> _params = new();
    
    public EffectParameter? this[string name]
    {
        get
        {
            _params.TryGetValue(name, out var param);
            if (param == null)
            {
                param = new EffectParameter();
                _params[name] = param;
            }
            return param;
        }
    }
}

[Obsolete("Port to MoonWorks GraphicsPipeline")]
public class EffectTechnique
{
    public EffectPassCollection Passes { get; } = new();
}

[Obsolete("Port to MoonWorks GraphicsPipeline")]
public class EffectTechniqueCollection
{
    private readonly Dictionary<string, EffectTechnique> _techniques = new();
    
    public EffectTechnique this[string name]
    {
        get
        {
            if (!_techniques.TryGetValue(name, out var tech))
            {
                tech = new EffectTechnique();
                _techniques[name] = tech;
            }
            return tech;
        }
    }
    
    public EffectTechnique this[int index] => _techniques.Values.ElementAtOrDefault(index) ?? new EffectTechnique();
}

[Obsolete("Port to MoonWorks render pass")]
public class EffectPass
{
    public void Apply() { }
}

[Obsolete("Port to MoonWorks render pass")]
public class EffectPassCollection : List<EffectPass>
{
    public EffectPassCollection() { Add(new EffectPass()); }
}

// ============================================================
// Buffer types
// ============================================================

[Obsolete("Port to MoonWorks Buffer")]
public class VertexBuffer : IDisposable
{
    public int VertexCount { get; }
    public string Name { get; set; }
    public object Tag { get; set; }
    
    public VertexBuffer(RealGraphicsDevice device, VertexDeclaration declaration, int vertexCount, BufferUsage usage)
    {
        VertexCount = vertexCount;
    }
    
    public VertexBuffer(RealGraphicsDevice device, Type vertexType, int vertexCount, BufferUsage usage)
    {
        VertexCount = vertexCount;
    }

    public void SetData<T>(T[] data) where T : struct { }
    public void SetData<T>(T[] data, int startIndex, int elementCount) where T : struct { }
    public void SetDataEXT<T>(ReadOnlySpan<T> data) where T : struct { }
    public void SetDataEXT<T>(List<T> data) where T : struct { }
    public void SetDataPointerEXT(int offsetInBytes, nint data, int dataLength, SetDataOptions options) { }
    public VertexDeclaration VertexDeclaration { get; set; }
    public void Dispose() { }
}

[Obsolete("Port to MoonWorks Buffer")]
public class DynamicVertexBuffer : VertexBuffer
{
    public DynamicVertexBuffer(RealGraphicsDevice device, VertexDeclaration declaration, int vertexCount, BufferUsage usage)
        : base(device, declaration, vertexCount, usage) { }
    
    public DynamicVertexBuffer(RealGraphicsDevice device, Type vertexType, int vertexCount, BufferUsage usage)
        : base(device, vertexType, vertexCount, usage) { }
    
    public void SetData<T>(T[] data, int startIndex, int elementCount, SetDataOptions options) where T : struct { }
}

[Obsolete("Port to MoonWorks Buffer")]  
public class IndexBuffer : IDisposable
{
    public int IndexCount { get; }
    public string Name { get; set; }
    public object Tag { get; set; }
    
    public IndexBuffer(RealGraphicsDevice device, Type indexType, int indexCount, BufferUsage usage)
    {
        IndexCount = indexCount;
    }
    
    public IndexBuffer(RealGraphicsDevice device, IndexElementSize elementSize, int indexCount, BufferUsage usage)
    {
        IndexCount = indexCount;
    }

    public void SetData<T>(T[] data) where T : struct { }
    public void SetData<T>(T[] data, int startIndex, int elementCount) where T : struct { }
    public void SetDataEXT<T>(ReadOnlySpan<T> data) where T : struct { }
    public void SetDataPointerEXT(int offsetInBytes, nint data, int dataLength, SetDataOptions options) { }
    public void Dispose() { }
}

[Obsolete("Port to MoonWorks Buffer")]
public class DynamicIndexBuffer : IndexBuffer
{
    public DynamicIndexBuffer(RealGraphicsDevice device, IndexElementSize elementSize, int indexCount, BufferUsage usage)
        : base(device, elementSize, indexCount, usage) { }

    public void SetData<T>(T[] data, int startIndex, int elementCount, SetDataOptions options) where T : struct { }
}

// ============================================================
// Vertex types
// ============================================================

[Obsolete("Port to MoonWorks VertexInputState")]
public class VertexDeclaration
{
    public int VertexStride { get; }
    
    public VertexDeclaration(int vertexStride, params VertexElement[] elements)
    {
        VertexStride = vertexStride;
    }
    
    public VertexDeclaration(params VertexElement[] elements)
    {
        VertexStride = elements.Length > 0 ? elements.Max(e => e.Offset + 4) : 0;
    }
}

[Obsolete("Port to MoonWorks VertexInputState")]
public struct VertexElement(int offset, VertexElementFormat format, VertexElementUsage usage, int usageIndex)
{
    public int Offset = offset;
    public VertexElementFormat Format = format;
    public VertexElementUsage Usage = usage;
    public int UsageIndex = usageIndex;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct VertexPositionColor : MoonWorks.Graphics.IVertexType
{
    public Vector3 Position;
    public Color Color;

    public VertexPositionColor(Vector3 position, Color color)
    {
        Position = position;
        Color = color;
    }

    public static ReadOnlySpan<MoonWorks.Graphics.VertexElementFormat> Formats =>
    [
        MoonWorks.Graphics.VertexElementFormat.Float3,
        MoonWorks.Graphics.VertexElementFormat.Ubyte4Norm
    ];

    public static ReadOnlySpan<uint> Offsets => [0, 12];

    public static readonly VertexDeclaration VertexDeclaration = new(
        new VertexElement(0, VertexElementFormat.Float3, VertexElementUsage.Position, 0),
        new VertexElement(12, VertexElementFormat.Float4, VertexElementUsage.Color, 0)
    );
}

[Obsolete("Port to MoonWorks VertexInputState")]
public struct VertexBufferBinding(VertexBuffer buffer, int offset = 0, int instanceFrequency = 0)
{
    public VertexBuffer VertexBuffer = buffer;
    public int VertexOffset = offset;
    public int InstanceFrequency = instanceFrequency;
}

// ============================================================
// Render targets
// ============================================================

[Obsolete("Port to MoonWorks Texture with TextureUsageFlags.ColorTarget")]
public class RenderTarget2D : IDisposable
{
    public Texture Texture { get; }
    public int Width { get; }
    public int Height { get; }
    
    public RenderTarget2D(RealGraphicsDevice device, int width, int height, bool mipMap = false, 
        SurfaceFormat format = SurfaceFormat.Color, DepthFormat depthFormat = DepthFormat.None,
        int preferredMultiSampleCount = 0,
        RenderTargetUsage usage = RenderTargetUsage.DiscardContents)
    {
        Width = width;
        Height = height;
        Texture = Texture.Create2D(device, (uint)width, (uint)height,
            MapSurfaceFormat(format),
            TextureUsageFlags.ColorTarget | TextureUsageFlags.Sampler);
    }

    private static TextureFormat MapSurfaceFormat(SurfaceFormat format) => format switch
    {
        SurfaceFormat.Color => TextureFormat.R8G8B8A8Unorm,
        SurfaceFormat.Single => TextureFormat.R32Float,
        _ => TextureFormat.R8G8B8A8Unorm,
    };

    public static implicit operator Texture(RenderTarget2D rt) => rt.Texture;
    public static implicit operator RenderTarget2D(Texture tex) => new(tex);

    // Internal constructor from raw Texture (for implicit conversion)
    private RenderTarget2D(Texture tex)
    {
        Texture = tex;
        Width = (int)tex.Width;
        Height = (int)tex.Height;
    }
    
    public void Dispose() => Texture?.Dispose();
}

[Obsolete("Port to MoonWorks render pass")]
public struct RenderTargetBinding(RenderTarget2D target)
{
    public RenderTarget2D RenderTarget = target;
    
    public static implicit operator RenderTargetBinding(RenderTarget2D target) => new(target);
}

// ============================================================
// FNA state classes
// ============================================================

[Obsolete("Port to MoonWorks ColorTargetBlendState in GraphicsPipeline")]
public class BlendState
{
    public static readonly BlendState Opaque = new();
    public static readonly BlendState AlphaBlend = new();
    public static readonly BlendState NonPremultiplied = new();
    public static readonly BlendState Additive = new();
}

[Obsolete("Port to MoonWorks DepthStencilState in GraphicsPipeline")]
public class DepthStencilState
{
    public static readonly DepthStencilState Default = new() { DepthBufferEnable = true, DepthBufferWriteEnable = true };
    public static readonly DepthStencilState DepthRead = new() { DepthBufferEnable = true, DepthBufferWriteEnable = false };
    public static readonly DepthStencilState None = new() { DepthBufferEnable = false, DepthBufferWriteEnable = false };

    public bool DepthBufferEnable { get; set; }
    public bool DepthBufferWriteEnable { get; set; }
    public CompareFunction DepthBufferFunction { get; set; } = CompareFunction.LessEqual;
}

[Obsolete("Port to MoonWorks RasterizerState in GraphicsPipeline")]
public class RasterizerState
{
    public static readonly RasterizerState CullNone = new() { CullMode = CullMode.None };
    public static readonly RasterizerState CullClockwise = new() { CullMode = CullMode.CullClockwiseFace };
    public static readonly RasterizerState CullCounterClockwise = new() { CullMode = CullMode.CullCounterClockwiseFace };

    public CullMode CullMode { get; set; } = CullMode.CullCounterClockwiseFace;
    public bool ScissorTestEnable { get; set; }
}

public enum CullMode
{
    None,
    CullClockwiseFace,
    CullCounterClockwiseFace
}

[Obsolete("Port to MoonWorks Sampler")]
public class SamplerState
{
    public static readonly SamplerState PointClamp = new();
    public static readonly SamplerState PointWrap = new();
    public static readonly SamplerState LinearClamp = new();
    public static readonly SamplerState LinearWrap = new();
    public static readonly SamplerState AnisotropicClamp = new();
    public static readonly SamplerState AnisotropicWrap = new();
}

[Obsolete("Port to MoonWorks Sampler binding")]
public class SamplerStateCollection
{
    private readonly SamplerState[] _states = new SamplerState[16];
    public SamplerState this[int index]
    {
        get => _states[index] ?? SamplerState.LinearClamp;
        set => _states[index] = value;
    }
}

[Obsolete("Port to MoonWorks texture binding")]
public class TextureCollection
{
    private readonly Texture[] _textures = new Texture[16];
    public Texture this[int index]
    {
        get => _textures[index];
        set => _textures[index] = value;
    }
}

// ============================================================
// BasicEffect
// ============================================================

[Obsolete("Port to MoonWorks GraphicsPipeline + custom shaders")]
public class BasicEffect : Effect
{
    public Matrix World { get; set; }
    public Matrix View { get; set; }
    public Matrix Projection { get; set; }
    public bool VertexColorEnabled { get; set; }
    public bool LightingEnabled { get; set; }
    public bool TextureEnabled { get; set; }
    public Vector3 DiffuseColor { get; set; }
    public float Alpha { get; set; } = 1f;
    public DirectionalLight DirectionalLight0 => new();
    public DirectionalLight DirectionalLight1 => new();
    public DirectionalLight DirectionalLight2 => new();
    
    public BasicEffect(RealGraphicsDevice device) : base() { }
}

// ============================================================
// Misc types
// ============================================================

[Obsolete("Not needed — use NvgSharp for 2D rendering")]
public class SpriteBatch : IDisposable
{
    public SpriteBatch(RealGraphicsDevice device) { }
    public void Begin(SpriteSortMode sortMode = SpriteSortMode.Deferred, BlendState blendState = null, SamplerState samplerState = null, DepthStencilState depthStencilState = null, RasterizerState rasterizerState = null) { }
    public void Draw(Texture texture, Microsoft.Xna.Framework.Rectangle destRect, Color color) { }
    public void Draw(Texture texture, Microsoft.Xna.Framework.Rectangle destRect, Microsoft.Xna.Framework.Rectangle? sourceRect, Color color) { }
    public void End() { }
    public void Dispose() { }
}

[Obsolete("Port to MoonWorks")]
public enum SpriteSortMode { Deferred, Immediate, Texture, BackToFront, FrontToBack }

[Obsolete("Port to MoonWorks uniform data")]
public struct DirectionalLight
{
    public Vector3 Direction;
    public Vector3 DiffuseColor;
    public Vector3 SpecularColor;
    public bool Enabled;
}

public struct Viewport
{
    public int X, Y, Width, Height;
    public float MinDepth, MaxDepth;
    public Viewport(int x, int y, int w, int h) { X = x; Y = y; Width = w; Height = h; MinDepth = 0; MaxDepth = 1; }
    
    public Vector3 Unproject(Vector3 source, Matrix projection, Matrix view, Matrix world)
    {
        var matrix = Matrix.Invert(world * view * projection);
        var v = new Vector3(
            (source.X - X) / Width * 2f - 1f,
            -((source.Y - Y) / Height * 2f - 1f),
            (source.Z - MinDepth) / (MaxDepth - MinDepth)
        );
        v = Vector3.Transform(v, matrix);
        float w2 = (source.X * matrix.M14 + source.Y * matrix.M24 + source.Z * matrix.M34 + matrix.M44);
        if (Math.Abs(w2) > float.Epsilon) v /= w2;
        return v;
    }
}

// ============================================================
// Enums
// ============================================================

[Obsolete("Port to MoonWorks BufferUsageFlags")]
public enum BufferUsage { None, WriteOnly }

[Obsolete("Port to MoonWorks enums")]
public enum SetDataOptions { None, Discard, NoOverwrite }

[Obsolete("Port to MoonWorks TextureFormat")]
public enum SurfaceFormat { Color, Single, Vector2, Vector4, HalfVector2, HalfVector4 }

[Obsolete("Port to MoonWorks TextureFormat")]
public enum DepthFormat { None, Depth16, Depth24, Depth24Stencil8 }

[Obsolete("Port to MoonWorks")]
public enum RenderTargetUsage { DiscardContents, PreserveContents, PlatformContents }

[Obsolete("Port to MoonWorks VertexElementFormat")]
public enum VertexElementFormat
{
    Single, Vector2, Vector3, Vector4, Color, Byte4,
    Short2, Short4, NormalizedShort2, NormalizedShort4,
    HalfVector2, HalfVector4, Float2 = Vector2, Float3 = Vector3, Float4 = Vector4
}

[Obsolete("Port to MoonWorks")]
public enum VertexElementUsage
{
    Position, Color, TextureCoordinate, Normal, Binormal, Tangent,
    BlendIndices, BlendWeight, Depth, Fog, PointSize, Sample, TessellateFactor
}

[Obsolete("Port to MoonWorks")]
public enum ClearOptions { Target = 1, DepthBuffer = 2, Stencil = 4 }

[Obsolete("Port to MoonWorks CompareOp")]
public enum CompareFunction { Always, Never, Less, LessEqual, Equal, Greater, GreaterEqual, NotEqual }

#pragma warning restore CS0618
