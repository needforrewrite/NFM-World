using System.Diagnostics.CodeAnalysis;
using NFMWorld.Graphics;
using NFMWorld.Shaders.Generated;

namespace NFMWorld;

/// <remarks>
/// Milestone 5 Stage B: every shader (<see cref="Line"/>, <see cref="Ground"/>/<see cref="Mountains"/>/
/// <see cref="Sky"/>, <see cref="Poly"/>, and <see cref="Particle"/> - the replacement for the four
/// old <c>BasicEffect</c>-based particle shaders) is converted to the new command-buffer graphics
/// abstraction (see the generator's output shape in
/// <c>NFMWorld.ShaderSourceGen/FnaShaderIncrementalGenerator.cs</c> and
/// <c>Shaders/Parameters.cs</c>'s doc comment). <see cref="Poly"/> needed two pipelines from one
/// module (see <see cref="IPipelineState"/>'s <c>TechniqueName</c> doc comment - "CreateShadowMap"
/// vs. "Basic"); <see cref="Particle"/> needed three (see its own doc comment - differing
/// blend/depth state per consumer, baked into the pipeline instead of set per-draw as
/// <c>BasicEffect</c>-era code did).
/// </remarks>
internal static class Effects
{
    public static LineEffect Line { get => CheckNotNull(field); private set; }
    public static IPipelineState LinePipeline { get => CheckNotNull(field); private set; }
    public static LineEffectParameters LineParameters { get => CheckNotNull(field); private set; }

    public static GroundEffect Ground { get => CheckNotNull(field); private set; }
    public static IPipelineState GroundPipeline { get => CheckNotNull(field); private set; }
    public static GroundEffectParameters GroundParameters { get => CheckNotNull(field); private set; }

    public static MountainsEffect Mountains { get => CheckNotNull(field); private set; }
    public static IPipelineState MountainsPipeline { get => CheckNotNull(field); private set; }
    public static MountainsEffectParameters MountainsParameters { get => CheckNotNull(field); private set; }

    public static SkyEffect Sky { get => CheckNotNull(field); private set; }
    public static IPipelineState SkyPipeline { get => CheckNotNull(field); private set; }
    public static SkyEffectParameters SkyParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// Poly.fx declares two techniques ("Basic" for the real color pass, "CreateShadowMap" for
    /// depth-only shadow-cascade rendering) - since <see cref="IPipelineState"/> bakes one
    /// technique per pipeline at creation time (see <see cref="PipelineDesc.TechniqueName"/>),
    /// this is two separate pipelines built from the same <see cref="PolyEffect"/> module, each
    /// with its own resolved <see cref="PolyEffectParameters"/> (slots happen to be identical
    /// today, but aren't guaranteed to be).
    /// </summary>
    public static PolyEffect Poly { get => CheckNotNull(field); private set; }
    public static IPipelineState PolyPipeline { get => CheckNotNull(field); private set; }
    public static PolyEffectParameters PolyParameters { get => CheckNotNull(field); private set; }
    public static IPipelineState PolyShadowPipeline { get => CheckNotNull(field); private set; }
    public static PolyEffectParameters PolyShadowParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// Replaces the four <c>BasicEffect(LightingEnabled = false, VertexColorEnabled = true)</c>
    /// instances Chips/Dust/FixFlare/Flames/FixHoop used to share (FNA3D has no BasicEffect
    /// equivalent - see <c>data/shaders/Particle.fx</c>'s doc comment). All five consumers share
    /// one shader module and one resolved <see cref="ParticleEffectParameters"/> (the uniform
    /// slots don't depend on blend/depth state), but need three different fixed-function-state
    /// pipelines because the old per-consumer <c>GraphicsDevice.BlendState</c>/
    /// <c>DepthStencilState</c> overrides differed and the new model bakes that state into the
    /// pipeline rather than setting it per-draw: <see cref="ParticleOpaquePipeline"/> (Chips,
    /// Flames - opaque, default depth), <see cref="ParticleDepthReadPipeline"/> (Dust -
    /// non-premultiplied blend, depth-tested but not written), <see cref="ParticleNoDepthPipeline"/>
    /// (FixFlare - non-premultiplied blend, no depth test/write).
    /// </summary>
    public static ParticleEffect Particle { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleOpaquePipeline { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleDepthReadPipeline { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleNoDepthPipeline { get => CheckNotNull(field); private set; }
    public static ParticleEffectParameters ParticleParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// <see cref="NFMWorld.Debug"/>'s collision/wireframe/gizmo overlays - two more pipelines off
    /// the same <see cref="Particle"/> module (no new shader needed, just different topology/blend/
    /// depth state): <see cref="DebugLinePipeline"/> (LineList, depth test disabled - "always on
    /// top", matching the old <c>DepthStencilState.None</c> override every line draw used) and
    /// <see cref="DebugGhostFillPipeline"/> (TriangleList, alpha blend, no culling, normal depth -
    /// matching the old <c>BlendState.AlphaBlend</c> + <c>RasterizerState.CullNone</c> override the
    /// ghost-preview fill pass used).
    /// </summary>
    public static IPipelineState DebugLinePipeline { get => CheckNotNull(field); private set; }
    public static IPipelineState DebugGhostFillPipeline { get => CheckNotNull(field); private set; }

    /// <summary>Shared clamp/point sampler for the ShadowMap0/1/2 textures <see cref="Lighting.SetShadowMapParameters"/> binds.</summary>
    public static ISampler ShadowMapSampler { get => CheckNotNull(field); private set; }

    private static T CheckNotNull<T>(T? field)
    {
        return field ?? ThrowException();

        T ThrowException()
        {
            throw new ArgumentNullException(nameof(field), $"Call {nameof(Effects)}.{nameof(Initialize)} before use.");
        }
    }

    [MemberNotNull(
        nameof(Line), nameof(LinePipeline),
        nameof(Ground), nameof(GroundPipeline),
        nameof(Mountains), nameof(MountainsPipeline),
        nameof(Sky), nameof(SkyPipeline),
        nameof(Poly), nameof(PolyPipeline), nameof(PolyShadowPipeline),
        nameof(Particle), nameof(ParticleOpaquePipeline), nameof(ParticleDepthReadPipeline), nameof(ParticleNoDepthPipeline),
        nameof(DebugLinePipeline), nameof(DebugGhostFillPipeline), nameof(ShadowMapSampler))]
    public static void Initialize(IGraphicsDevice graphicsDevice)
    {
        Line = new LineEffect(VFS.ReadAllBytes("./data/shaders/Line.fxb"));
        LinePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Line.Module,
            PixelShader: Line.Module,
            VertexLayouts: [LineMesh.LineMeshVertexAttribute.VertexLayout, InstanceData.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        LineParameters = Line.Bind(LinePipeline);

        // Ground/Mountains/GroundPolys: the original FNA code set DepthStencilState.DepthRead
        // (test enabled, WRITE disabled) for the actual draw, not the ambient Default (write
        // enabled) - matching that here matters, since writing ground-plane depth over whatever
        // else shares its footprint was never intended.
        var groundDepthState = DepthStencilStateDesc.Default with { DepthWriteEnabled = false };

        // ...and CullNone, because these elements never set a rasterizer state in the original
        // either: they inherited the CullNone the Sky left behind (it sets CullNone and never
        // restores it), and the sky is drawn first every frame (RenderBucket.Sky). Now that the
        // state is baked per pipeline rather than leaked, it has to be asked for. NFM's terrain
        // geometry is two-sided, so culling it punches holes in whatever faces away from the
        // camera (e.g. the underside of the cloud polys) rather than rendering them.
        var groundRasterizerState = RasterizerStateDesc.Default with { CullMode = CullMode.None };

        Ground = new GroundEffect(VFS.ReadAllBytes("./data/shaders/Ground.fxb"));
        GroundPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Ground.Module,
            PixelShader: Ground.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: groundDepthState,
            RasterizerState: groundRasterizerState));
        GroundParameters = Ground.Bind(GroundPipeline);

        Mountains = new MountainsEffect(VFS.ReadAllBytes("./data/shaders/Mountains.fxb"));
        MountainsPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Mountains.Module,
            PixelShader: Mountains.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: groundDepthState,
            RasterizerState: groundRasterizerState));
        MountainsParameters = Mountains.Bind(MountainsPipeline);

        // Sky: the original FNA code set CullMode.None and DepthStencilState.None (test AND write
        // disabled) - it's an unbounded background dome, drawn first, that should never occlude or
        // be occluded by depth-tested geometry.
        Sky = new SkyEffect(VFS.ReadAllBytes("./data/shaders/Sky.fxb"));
        SkyPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Sky.Module,
            PixelShader: Sky.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        SkyParameters = Sky.Bind(SkyPipeline);

        // Poly (stage geometry - walls, ramps, everything Submesh draws): the original FNA code
        // set RasterizerState.CullNone for the whole draw (both the Basic and CreateShadowMap
        // passes) - NFM's polygon data isn't guaranteed consistently wound/one-sided, so this
        // isn't optional the way it might be for other meshes.
        Poly = new PolyEffect(VFS.ReadAllBytes("./data/shaders/Poly.fxb"));
        var polyVertexLayouts = new[] { Mesh.VertexPositionNormalColorCentroid.VertexLayout, InstanceData.VertexLayout };
        var polyRasterizerState = RasterizerStateDesc.Default with { CullMode = CullMode.None };
        PolyPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Poly.Module,
            PixelShader: Poly.Module,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: polyRasterizerState,
            TechniqueName: "Basic"));
        PolyParameters = Poly.Bind(PolyPipeline);
        PolyShadowPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Poly.Module,
            PixelShader: Poly.Module,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: polyRasterizerState,
            TechniqueName: "CreateShadowMap"));
        PolyShadowParameters = Poly.Bind(PolyShadowPipeline);

        Particle = new ParticleEffect(VFS.ReadAllBytes("./data/shaders/Particle.fxb"));
        ParticleOpaquePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Particle.Module,
            PixelShader: Particle.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        ParticleDepthReadPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Particle.Module,
            PixelShader: Particle.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        ParticleNoDepthPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Particle.Module,
            PixelShader: Particle.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        ParticleParameters = Particle.Bind(ParticleOpaquePipeline);

        DebugLinePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Particle.Module,
            PixelShader: Particle.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            Topology: PrimitiveTopology.LineList,
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));
        DebugGhostFillPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: Particle.Module,
            PixelShader: Particle.Module,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            Topology: PrimitiveTopology.TriangleList,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: RasterizerStateDesc.Default with { CullMode = CullMode.None }));

        // PointClamp, matching the old scene-wide default the pre-migration code force-set on all
        // 16 sampler slots (see Scene.cs's TODO on the equivalent removed call).
        ShadowMapSampler = graphicsDevice.CreateSampler(new SamplerDesc(
            Filter: TextureFilter.Point, AddressU: TextureAddressMode.Clamp, AddressV: TextureAddressMode.Clamp));
    }
}
