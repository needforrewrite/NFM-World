using NFMWorld.Shaders;

namespace NFMWorld.Graphics;

/// <summary>
/// Backend-agnostic entry point for resource creation and command submission. Concrete
/// implementations: <c>NFMWorld.Graphics.FNA3D</c> (first backend, proves the abstraction) and,
/// later, <c>NFMWorld.Graphics.Sokol</c>.
/// </summary>
/// <remarks>
/// Disposable because every backend owns GPU resources - and, on two of the three, an API context
/// that must be released in a specific order relative to the window it was created against. The
/// host holds this type and has to call <see cref="IDisposable.Dispose"/> on it at shutdown, so the
/// obligation belongs here rather than in a convention each backend happens to follow.
/// </remarks>
public interface IGraphicsDevice : IDisposable
{
    ISwapchain Swapchain { get; }

    /// <summary>
    /// Whether this backend's framebuffer space has its origin at the bottom left, as OpenGL's does,
    /// rather than at the top left, as D3D's and Metal's do.
    /// </summary>
    /// <remarks>
    /// This is a property of the API and not of a particular renderer, which is why it is derived
    /// from the device's own type rather than from a list of backend names: a GL-family device is
    /// exactly the one whose uniform blocks are laid out as std140 (<c>GlCommandBuffer</c>'s UBO)
    /// rather than as D3D's merged <c>register(b0)</c> block, and that correspondence is what the
    /// abstraction documents. It deliberately does NOT defer to the concrete backends, so no other
    /// project has to take a dependency just to answer the question.
    ///
    /// The one place this matters today is shadow-map sampling, and it is worth stating why. The
    /// shader that samples a cascade carries <c>shadowTexCoord.y = 1.0f - shadowTexCoord.y</c>,
    /// which is correct for a top-left origin and mirrored for a bottom-left one. The cascade is
    /// written with the same <c>View * Projection</c> product the lookup uses, so the two are
    /// related by exactly that one flip and nothing else - which the OpenGL smoke test demonstrates
    /// by replaying the shader's own lookup and finding the depth one row-mirror away from it.
    /// </remarks>
    bool HasBottomLeftFramebufferOrigin { get; }

    /// <summary>
    /// Only one command buffer may be live at a time; implementations must guard against a
    /// second acquisition before the previous one is submitted.
    /// </summary>
    ICommandBuffer AcquireCommandBuffer();

    /// <summary>Executes the command buffer's recorded work (immediately, for an immediate-mode backend).</summary>
    void Submit(ICommandBuffer commandBuffer);

    IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default);
    ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default);
    IRenderTarget CreateRenderTarget(RenderTargetDesc desc);
    ISampler CreateSampler(SamplerDesc desc);
    IPipelineState CreatePipeline(PipelineDesc desc);

    /// <summary>
    /// Turns one generated program's per-backend sources into the shader module
    /// <see cref="CreatePipeline"/> takes.
    /// </summary>
    /// <remarks>
    /// This is the factory half of <see cref="IShaderModule"/>, and it belongs on the device for the
    /// same reason <see cref="CreateTexture"/> does: compiling a program is a backend-specific act.
    /// A bundle carries the same program as HLSL, MSL, desktop GLSL, ES GLSL and SPIR-V, and each
    /// backend compiles a different one - sokol's D3D11 build takes the HLSL, its Metal build the
    /// MSL, and so on - so the choice cannot be made by the caller, which does not know which backend
    /// it is talking to.
    ///
    /// <paramref name="reflection"/> is part of the signature because sokol needs more than source:
    /// it has to be told every binding the program declares, and the reflection is where that is
    /// recorded.
    ///
    /// Implemented by the backends whose pipeline model is a source-based program (OpenGL, Sokol).
    /// The default throws, because FNA3D's model is a whole precompiled Effect blob rather than a
    /// vertex/pixel source pair - there is no honest implementation for it to provide, and a loud
    /// failure is better than a silent one. Its callers use
    /// <c>FNA3DGraphicsDevice.LoadEffectModule</c> instead.
    /// </remarks>
    IShaderModule CreateShaderModule(ShaderStageSources vertex, ShaderStageSources pixel, ShaderReflection reflection) =>
        throw new NotSupportedException(
            "This graphics backend does not create shader modules from source. Its pipeline model " +
            "uses one precompiled program blob instead (see FNA3DGraphicsDevice.LoadEffectModule).");

    /// <summary>
    /// Synchronous CPU readback of a texture's pixels into <paramref name="destination"/>, which
    /// must hold <c>width * height * bytesPerPixel</c> tightly packed rows (no padding), in the
    /// same top-down order the texture was uploaded in.
    /// </summary>
    /// <remarks>
    /// This blocks on a GPU stall by design - it exists for editor, export and debug paths, never
    /// for per-frame gameplay work. The texture must not be currently bound as a render target:
    /// callers reading an off-screen <see cref="IRenderTarget"/> must bind the swapchain (or
    /// another target) first. Depth-stencil textures have no readable storage in the backends and
    /// are rejected; read the target's <see cref="IRenderTarget.ColorTexture"/> instead.
    /// </remarks>
    void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0);
}
