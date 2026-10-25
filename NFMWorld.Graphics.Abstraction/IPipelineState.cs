using NFMWorld.Shaders;

namespace NFMWorld.Graphics;

public enum BlendFactor { Zero, One, SourceAlpha, InverseSourceAlpha, DestinationAlpha, InverseDestinationAlpha }
public enum BlendOperation { Add, Subtract, ReverseSubtract, Min, Max }

[Flags]
public enum ColorWriteMask { None = 0, Red = 1, Green = 2, Blue = 4, Alpha = 8, All = Red | Green | Blue | Alpha }

public readonly record struct BlendStateDesc(
    bool Enabled,
    BlendFactor SourceColor = BlendFactor.One,
    BlendFactor DestinationColor = BlendFactor.Zero,
    BlendOperation ColorOperation = BlendOperation.Add,
    BlendFactor SourceAlpha = BlendFactor.One,
    BlendFactor DestinationAlpha = BlendFactor.Zero,
    BlendOperation AlphaOperation = BlendOperation.Add,
    ColorWriteMask ColorWriteMask = ColorWriteMask.All)
{
    public static readonly BlendStateDesc Opaque = new(Enabled: false);
    public static readonly BlendStateDesc AlphaBlend = new(Enabled: true, SourceColor: BlendFactor.One, DestinationColor: BlendFactor.InverseSourceAlpha);

    /// <summary>Matches XNA's <c>BlendState.NonPremultiplied</c> - for straight (non-premultiplied) alpha, as opposed to <see cref="AlphaBlend"/>'s premultiplied-alpha assumption.</summary>
    public static readonly BlendStateDesc NonPremultiplied = new(Enabled: true,
        SourceColor: BlendFactor.SourceAlpha, DestinationColor: BlendFactor.InverseSourceAlpha,
        SourceAlpha: BlendFactor.SourceAlpha, DestinationAlpha: BlendFactor.InverseSourceAlpha);

    public static readonly BlendStateDesc ColorWriteNone = new(Enabled: false, ColorWriteMask: ColorWriteMask.None);
}

public enum CompareFunction { Always, Never, Less, LessEqual, Equal, NotEqual, GreaterEqual, Greater }
public enum StencilOperation { Keep, Zero, Replace, Increment, Decrement, IncrementSaturate, DecrementSaturate, Invert }

public readonly record struct DepthStencilStateDesc(
    bool DepthTestEnabled = true,
    bool DepthWriteEnabled = true,
    CompareFunction DepthCompare = CompareFunction.LessEqual,
    bool StencilTestEnabled = false,
    bool TwoSidedStencil = false,
    int StencilReadMask = int.MaxValue,
    int StencilWriteMask = int.MaxValue,
    int ReferenceStencil = 0,
    CompareFunction StencilFunction = CompareFunction.Always,
    StencilOperation StencilFail = StencilOperation.Keep,
    StencilOperation StencilDepthFail = StencilOperation.Keep,
    StencilOperation StencilPass = StencilOperation.Keep,
    CompareFunction CcwStencilFunction = CompareFunction.Always,
    StencilOperation CcwStencilFail = StencilOperation.Keep,
    StencilOperation CcwStencilDepthFail = StencilOperation.Keep,
    StencilOperation CcwStencilPass = StencilOperation.Keep)
{
    // new() here = the default struct, not the constructor
    public static readonly DepthStencilStateDesc Default = new(
        DepthTestEnabled: true);

    public static readonly DepthStencilStateDesc None = new(DepthTestEnabled: false, DepthWriteEnabled: false);
}

public enum CullMode { None, Front, Back }
public enum FillMode { Solid, Wireframe }

public readonly record struct RasterizerStateDesc(
    CullMode CullMode = CullMode.Back,
    FillMode FillMode = FillMode.Solid,
    bool ScissorTestEnabled = false)
{
    /// <summary>XNA's <c>RasterizerState.CullCounterClockwise</c> - see <see cref="DepthStencilStateDesc.Default"/> for why the arguments are spelled out instead of <c>new()</c>.</summary>
    public static readonly RasterizerStateDesc Default = new(CullMode: CullMode.Back, FillMode: FillMode.Solid, ScissorTestEnabled: false);
}

/// <summary>An integer scissor rectangle in render-target pixel coordinates, set via <see cref="ICommandBuffer.SetScissorRect"/> when a pipeline's <see cref="RasterizerStateDesc.ScissorTestEnabled"/> is true.</summary>
public readonly record struct ScissorRect(int X, int Y, int Width, int Height);

/// <summary>
/// Bundles everything that used to be set piecemeal per-draw against FNA's <c>GraphicsDevice</c>
/// (shader Effect, BlendState, DepthStencilState, RasterizerState) into a single object created
/// once at load time - mirroring a D3D11/Vulkan pipeline state object.
/// </summary>
/// <summary>
/// <paramref name="VertexLayouts"/> is indexed by vertex buffer slot (the same slot passed to
/// <see cref="ICommandBuffer.SetVertexBuffer"/>) - most pipelines use a single stream at slot 0,
/// but hardware-instanced draws bind a second, per-instance stream at slot 1 with its own
/// <see cref="VertexLayoutDesc.InstanceStepRate"/>.
/// </summary>
/// <param name="TechniqueName">
/// Selects which named technique of the underlying Effect blob this pipeline binds to
/// (null = the first technique in the file). Most shaders only declare one technique, so
/// this can be left null; shaders with more than one (e.g. Poly.fx's "CreateShadowMap" vs.
/// "Basic") need a separate <see cref="IPipelineState"/> per technique, each created with its
/// own <see cref="TechniqueName"/>.
/// </param>
public readonly record struct PipelineDesc(
    IShaderModule VertexShader,
    IShaderModule PixelShader,
    IReadOnlyList<VertexLayoutDesc> VertexLayouts,
    BlendStateDesc BlendState,
    DepthStencilStateDesc DepthStencilState,
    RasterizerStateDesc RasterizerState,
    PrimitiveTopology Topology = PrimitiveTopology.TriangleList,
    string? TechniqueName = null);

public interface IPipelineState : IDisposable
{
    PipelineDesc Desc { get; }

    /// <summary>
    /// The reflection actually usable against this pipeline for <see cref="ICommandBuffer.SetUniform"/>
    /// slot lookups. For most backends this is just Desc.VertexShader/PixelShader's own Reflection,
    /// but the FNA3D backend can only produce real per-parameter reflection once its Effect blob has
    /// been parsed against a live device - which happens at pipeline creation, not shader-module
    /// creation - so that backend's IShaderModule.Reflection is an empty stub and this is the one
    /// callers should actually use.
    /// </summary>
    ShaderReflection Reflection { get; }
}
