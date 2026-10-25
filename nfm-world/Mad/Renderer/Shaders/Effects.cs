using System.Diagnostics.CodeAnalysis;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorld.Shaders.Generated;

namespace NFMWorld;

/// <remarks>
/// Milestone 5 Stage B: every shader (<see cref="LineModule"/>, <see cref="GroundModule"/>/
/// <see cref="MountainsModule"/>/<see cref="SkyModule"/>, <see cref="PolyModule"/>, and
/// <see cref="ParticleModule"/> - the replacement for the four old <c>BasicEffect</c>-based particle
/// shaders) is converted to the new command-buffer graphics abstraction. Each is now a bundle
/// produced by the shader compiler at build time (<c>NFMWorld.ShaderCompiler</c>) rather than an
/// FNA3D Effect blob, so the module is created here by handing the bundle's stages and reflection to
/// <see cref="IGraphicsDevice.CreateShaderModule"/> instead of reading a committed <c>.fxb</c>.
///
/// A bundle is one <em>program</em>, not one file: the GL backend has no technique concept (it links
/// one program per vertex/pixel entry-point pair), so a file declaring several techniques yields
/// several bundles. That is why Poly's "Basic" and "CreateShadowMap" are two modules here rather
/// than one module with <see cref="PipelineDesc.TechniqueName"/> set, and why
/// <see cref="ParticleModule"/> needs three pipelines despite one module - differing blend/depth
/// state per consumer, baked into the pipeline instead of set per-draw as <c>BasicEffect</c>-era code
/// did.
/// </remarks>
internal static class Effects
{
    public static IShaderModule LineModule { get => CheckNotNull(field); private set; }
    public static IPipelineState LinePipeline { get => CheckNotNull(field); private set; }
    public static LineBasicParameters LineParameters { get => CheckNotNull(field); private set; }

    /// <summary>Line.fx with the depth test kept but depth writes off - the editor's translucent line overlays (see <see cref="PolyDepthReadPipeline"/>).</summary>
    public static IPipelineState LineDepthReadPipeline { get => CheckNotNull(field); private set; }
    public static LineBasicParameters LineDepthReadParameters { get => CheckNotNull(field); private set; }

    public static IShaderModule GroundModule { get => CheckNotNull(field); private set; }
    public static IPipelineState GroundPipeline { get => CheckNotNull(field); private set; }
    public static GroundFullbrightParameters GroundParameters { get => CheckNotNull(field); private set; }

    public static IShaderModule MountainsModule { get => CheckNotNull(field); private set; }
    public static IPipelineState MountainsPipeline { get => CheckNotNull(field); private set; }
    public static MountainsFullbrightParameters MountainsParameters { get => CheckNotNull(field); private set; }

    public static IShaderModule SkyModule { get => CheckNotNull(field); private set; }
    public static IPipelineState SkyPipeline { get => CheckNotNull(field); private set; }
    public static SkyFullbrightParameters SkyParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// Poly.fx declares two techniques ("Basic" for the real color pass, "CreateShadowMap" for
    /// depth-only shadow-cascade rendering), which the compiler emits as two bundles and therefore
    /// two modules here. Each has its own resolved parameter slots
    /// (<see cref="PolyParameters"/> vs <see cref="PolyShadowParameters"/>), taken from that bundle's
    /// own reflection - the two happen to be identical today, but nothing guarantees it.
    /// </summary>
    public static IShaderModule PolyModule { get => CheckNotNull(field); private set; }
    public static IShaderModule PolyShadowModule { get => CheckNotNull(field); private set; }
    public static IPipelineState PolyPipeline { get => CheckNotNull(field); private set; }
    public static PolyBasicParameters PolyParameters { get => CheckNotNull(field); private set; }
    public static IPipelineState PolyShadowPipeline { get => CheckNotNull(field); private set; }
    public static PolyCreateShadowMapParameters PolyShadowParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// Two more pipelines off the same <see cref="PolyModule"/> for the model/stage editors'
    /// translucent overlays (selected per-mesh via <see cref="Mesh.OverlayMode"/>), replacing the
    /// old per-draw <c>BlendState.AlphaBlend</c> + <c>DepthStencilState</c> save/set/restore pairs:
    /// <see cref="PolyDepthReadPipeline"/> keeps the depth test but stops writing depth (polygon
    /// highlights and the wheel overlay - visible through, but still occluded by nearer geometry),
    /// and <see cref="PolyNoDepthPipeline"/> drops depth entirely (the reference-car ghost, which the
    /// editor clears the depth buffer for so it always draws in front).
    /// </summary>
    /// <remarks>
    /// Blending is <see cref="BlendStateDesc.NonPremultiplied"/>, matching every other consumer of
    /// this shader shape in the engine - Poly.fx writes <c>min(alphaOverride, Alpha)</c> straight
    /// rather than premultiplied. The pre-migration editor paired these with XNA's premultiplied
    /// <c>BlendState.AlphaBlend</c>, which would over-brighten a translucent overlay; that code path
    /// had never been visually verified, so this is a deliberate correction rather than a regression.
    /// </remarks>
    public static IPipelineState PolyDepthReadPipeline { get => CheckNotNull(field); private set; }
    public static PolyBasicParameters PolyDepthReadParameters { get => CheckNotNull(field); private set; }
    public static IPipelineState PolyNoDepthPipeline { get => CheckNotNull(field); private set; }
    public static PolyBasicParameters PolyNoDepthParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// Replaces the four <c>BasicEffect(LightingEnabled = false, VertexColorEnabled = true)</c>
    /// instances Chips/Dust/FixFlare/Flames/FixHoop used to share (FNA3D has no BasicEffect
    /// equivalent - see <c>data/shaders/Particle.fx</c>'s doc comment). All five consumers share
    /// one shader module and one resolved <see cref="ParticleFullbrightParameters"/> (the uniform
    /// slots don't depend on blend/depth state), but need three different fixed-function-state
    /// pipelines because the old per-consumer <c>GraphicsDevice.BlendState</c>/
    /// <c>DepthStencilState</c> overrides differed and the new model bakes that state into the
    /// pipeline rather than setting it per-draw: <see cref="ParticleOpaquePipeline"/> (Chips,
    /// Flames - opaque, default depth), <see cref="ParticleDepthReadPipeline"/> (Dust -
    /// non-premultiplied blend, depth-tested but not written), <see cref="ParticleNoDepthPipeline"/>
    /// (FixFlare - non-premultiplied blend, no depth test/write).
    /// </summary>
    public static IShaderModule ParticleModule { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleOpaquePipeline { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleDepthReadPipeline { get => CheckNotNull(field); private set; }
    public static IPipelineState ParticleNoDepthPipeline { get => CheckNotNull(field); private set; }
    public static ParticleFullbrightParameters ParticleParameters { get => CheckNotNull(field); private set; }

    /// <summary>
    /// <see cref="NFMWorld.Debug"/>'s collision/wireframe/gizmo overlays - two more pipelines off
    /// the same <see cref="ParticleModule"/> (no new shader needed, just different topology/blend/
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

    /// <summary>
    /// Rasterizer state shared by every pipeline in this file: no culling (NFM's geometry is
    /// two-sided - see the per-pipeline comments in <see cref="Initialize"/>) and scissor testing
    /// enabled, which is what lets the stage editor clip its 3D view to the ImGui viewport rect.
    /// </summary>
    /// <remarks>
    /// Scissor testing is on for the *game's* pipelines too, because the abstraction has no
    /// per-draw "set a global rasterizer state" call - a scene reaches whatever pipelines its
    /// renderables were built with, so clipping a whole scene means the pipelines opt in. That is
    /// safe as long as every place that binds a render target or sets a viewport also sets a scissor
    /// rect (see <see cref="WorldGame.SetFullScreenScissor"/>): a scissor rect covering the whole
    /// target is a no-op, and the rect has to be *set* rather than left alone because the ImGui
    /// renderer leaves its last per-draw rect bound.
    /// </remarks>
    private static readonly RasterizerStateDesc ScissorRasterizer =
        RasterizerStateDesc.Default with { CullMode = CullMode.None, ScissorTestEnabled = true };

    private static T CheckNotNull<T>(T? field)
    {
        return field ?? ThrowException();

        T ThrowException()
        {
            throw new ArgumentNullException(nameof(field), $"Call {nameof(Effects)}.{nameof(Initialize)} before use.");
        }
    }

    [MemberNotNull(
        nameof(LineModule), nameof(LinePipeline), nameof(LineDepthReadPipeline),
        nameof(GroundModule), nameof(GroundPipeline),
        nameof(MountainsModule), nameof(MountainsPipeline),
        nameof(SkyModule), nameof(SkyPipeline),
        nameof(PolyModule), nameof(PolyShadowModule), nameof(PolyPipeline), nameof(PolyShadowPipeline),
        nameof(PolyDepthReadPipeline), nameof(PolyNoDepthPipeline),
        nameof(ParticleModule), nameof(ParticleOpaquePipeline), nameof(ParticleDepthReadPipeline), nameof(ParticleNoDepthPipeline),
        nameof(DebugLinePipeline), nameof(DebugGhostFillPipeline), nameof(ShadowMapSampler))]
    public static void Initialize(IGraphicsDevice graphicsDevice)
    {
        // Each bundle is created once and its module is then used for BOTH stages: the GL backend
        // links one program per vertex/pixel entry-point pair and the bundle's reflection describes
        // that linked program rather than either stage alone - see GlGraphicsDevice.LoadProgram.
        // The parameters are bound from the *program*, not the pipeline, because they are the
        // program's own reflected slots - which is why the pipelines built off one module below
        // share a parameter instance instead of each resolving its own.
        var line = LineBasic.Create();
        LineModule = Module(graphicsDevice, line.Vertex, line.Pixel, line.Reflection);
        LineParameters = line.Bind();
        LinePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: LineModule,
            PixelShader: LineModule,
            VertexLayouts: [LineMesh.LineMeshVertexAttribute.VertexLayout, InstanceData.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: ScissorRasterizer));
        LineDepthReadPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: LineModule,
            PixelShader: LineModule,
            VertexLayouts: [LineMesh.LineMeshVertexAttribute.VertexLayout, InstanceData.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
            RasterizerState: ScissorRasterizer));
        LineDepthReadParameters = line.Bind();

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
        var groundRasterizerState = ScissorRasterizer;

        var ground = GroundFullbright.Create();
        GroundModule = Module(graphicsDevice, ground.Vertex, ground.Pixel, ground.Reflection);
        GroundParameters = ground.Bind();
        GroundPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: GroundModule,
            PixelShader: GroundModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: groundDepthState,
            RasterizerState: groundRasterizerState));

        var mountains = MountainsFullbright.Create();
        MountainsModule = Module(graphicsDevice, mountains.Vertex, mountains.Pixel, mountains.Reflection);
        MountainsParameters = mountains.Bind();
        MountainsPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: MountainsModule,
            PixelShader: MountainsModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: groundDepthState,
            RasterizerState: groundRasterizerState));

        // Sky: the original FNA code set CullMode.None and DepthStencilState.None (test AND write
        // disabled) - it's an unbounded background dome, drawn first, that should never occlude or
        // be occluded by depth-tested geometry.
        var sky = SkyFullbright.Create();
        SkyModule = Module(graphicsDevice, sky.Vertex, sky.Pixel, sky.Reflection);
        SkyParameters = sky.Bind();
        SkyPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: SkyModule,
            PixelShader: SkyModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: ScissorRasterizer));

        // Poly (stage geometry - walls, ramps, everything Submesh draws): the original FNA code
        // set RasterizerState.CullNone for the whole draw (both the Basic and CreateShadowMap
        // passes) - NFM's polygon data isn't guaranteed consistently wound/one-sided, so this
        // isn't optional the way it might be for other meshes. Two techniques means two bundles,
        // so the two passes are separate programs rather than one program selected per pipeline.
        var polyBasic = PolyBasic.Create();
        PolyModule = Module(graphicsDevice, polyBasic.Vertex, polyBasic.Pixel, polyBasic.Reflection);
        PolyParameters = polyBasic.Bind();
        var polyShadow = PolyCreateShadowMap.Create();
        PolyShadowModule = Module(graphicsDevice, polyShadow.Vertex, polyShadow.Pixel, polyShadow.Reflection);
        PolyShadowParameters = polyShadow.Bind();

        var polyVertexLayouts = new[] { Mesh.VertexPositionNormalColorCentroid.VertexLayout, InstanceData.VertexLayout };
        var polyRasterizerState = ScissorRasterizer;
        PolyPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: PolyModule,
            PixelShader: PolyModule,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: polyRasterizerState));
        PolyShadowPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: PolyShadowModule,
            PixelShader: PolyShadowModule,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: polyRasterizerState));

        // Editor overlay variants of the Basic technique - see PolyDepthReadPipeline's doc comment.
        // Both reuse polyBasic's parameters: same program, so the same reflected slots.
        PolyDepthReadPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: PolyModule,
            PixelShader: PolyModule,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
            RasterizerState: polyRasterizerState));
        PolyDepthReadParameters = polyBasic.Bind();
        PolyNoDepthPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: PolyModule,
            PixelShader: PolyModule,
            VertexLayouts: polyVertexLayouts,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: polyRasterizerState));
        PolyNoDepthParameters = polyBasic.Bind();

        var particle = ParticleFullbright.Create();
        ParticleModule = Module(graphicsDevice, particle.Vertex, particle.Pixel, particle.Reflection);
        ParticleParameters = particle.Bind();
        ParticleOpaquePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: ParticleModule,
            PixelShader: ParticleModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: ScissorRasterizer));
        ParticleDepthReadPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: ParticleModule,
            PixelShader: ParticleModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default with { DepthWriteEnabled = false },
            RasterizerState: ScissorRasterizer));
        ParticleNoDepthPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: ParticleModule,
            PixelShader: ParticleModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: ScissorRasterizer));

        DebugLinePipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: ParticleModule,
            PixelShader: ParticleModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            Topology: PrimitiveTopology.LineList,
            BlendState: BlendStateDesc.Opaque,
            DepthStencilState: DepthStencilStateDesc.None,
            RasterizerState: ScissorRasterizer));
        DebugGhostFillPipeline = graphicsDevice.CreatePipeline(new PipelineDesc(
            VertexShader: ParticleModule,
            PixelShader: ParticleModule,
            VertexLayouts: [PositionColorVertex.VertexLayout],
            Topology: PrimitiveTopology.TriangleList,
            BlendState: BlendStateDesc.NonPremultiplied,
            DepthStencilState: DepthStencilStateDesc.Default,
            RasterizerState: ScissorRasterizer));

        // PointClamp, matching the old scene-wide default the pre-migration code force-set on all
        // 16 sampler slots (see Scene.cs's TODO on the equivalent removed call).
        ShadowMapSampler = graphicsDevice.CreateSampler(new SamplerDesc(
            Filter: TextureFilter.Point, AddressU: TextureAddressMode.Clamp, AddressV: TextureAddressMode.Clamp));
    }

    /// <summary>
    /// Turns a bundle's two stages and its reflection into the module the pipelines take, through
    /// whichever backend is running.
    ///
    /// This used to name the GL backend directly, on the reasoning that creating a module from
    /// source is a backend-specific act and so could not go through the abstraction. That reasoning
    /// was half right - it is backend-specific - but the conclusion did not follow:
    /// <see cref="IGraphicsDevice.CreateShaderModule"/> is the factory, and because
    /// <see cref="Initialize"/> already receives the device, the same helper can serve every
    /// backend without knowing which one it has. The backend compiles whichever of the bundle's
    /// forms it wants (GL the ES GLSL, sokol's D3D11 build the HLSL, and so on), which is
    /// information only the backend has.
    ///
    /// Keeping it in one helper is what makes that a single-site change rather than seven.
    /// </summary>
    private static IShaderModule Module(IGraphicsDevice graphicsDevice, ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        graphicsDevice.CreateShaderModule(vertex, pixel, reflection);
}
