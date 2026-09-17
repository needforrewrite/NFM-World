using NFMWorld.Shaders;

namespace NFMWorld.Graphics;

public enum BlendFactor { Zero, One, SourceAlpha, InverseSourceAlpha, DestinationAlpha, InverseDestinationAlpha }
public enum BlendOperation { Add, Subtract, ReverseSubtract, Min, Max }

public readonly record struct BlendStateDesc(
    bool Enabled,
    BlendFactor SourceColor = BlendFactor.One,
    BlendFactor DestinationColor = BlendFactor.Zero,
    BlendOperation ColorOperation = BlendOperation.Add,
    BlendFactor SourceAlpha = BlendFactor.One,
    BlendFactor DestinationAlpha = BlendFactor.Zero,
    BlendOperation AlphaOperation = BlendOperation.Add)
{
    public static readonly BlendStateDesc Opaque = new(Enabled: false);
    public static readonly BlendStateDesc AlphaBlend = new(Enabled: true, SourceColor: BlendFactor.One, DestinationColor: BlendFactor.InverseSourceAlpha);
}

public enum CompareFunction { Always, Never, Less, LessEqual, Equal, NotEqual, GreaterEqual, Greater }

public readonly record struct DepthStencilStateDesc(
    bool DepthTestEnabled = true,
    bool DepthWriteEnabled = true,
    CompareFunction DepthCompare = CompareFunction.LessEqual)
{
    public static readonly DepthStencilStateDesc Default = new();
    public static readonly DepthStencilStateDesc None = new(DepthTestEnabled: false, DepthWriteEnabled: false);
}

public enum CullMode { None, Front, Back }
public enum FillMode { Solid, Wireframe }

public readonly record struct RasterizerStateDesc(
    CullMode CullMode = CullMode.Back,
    FillMode FillMode = FillMode.Solid)
{
    public static readonly RasterizerStateDesc Default = new();
}

/// <summary>
/// Bundles everything that used to be set piecemeal per-draw against FNA's <c>GraphicsDevice</c>
/// (shader Effect, BlendState, DepthStencilState, RasterizerState) into a single object created
/// once at load time - mirroring a D3D11/Vulkan pipeline state object.
/// </summary>
public readonly record struct PipelineDesc(
    IShaderModule VertexShader,
    IShaderModule PixelShader,
    VertexLayoutDesc VertexLayout,
    BlendStateDesc BlendState,
    DepthStencilStateDesc DepthStencilState,
    RasterizerStateDesc RasterizerState,
    PrimitiveTopology Topology = PrimitiveTopology.TriangleList);

public interface IPipelineState : IDisposable
{
    PipelineDesc Desc { get; }
}
