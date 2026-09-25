using Silk.NET.Core.Contexts;
using Silk.NET.OpenGLES;
using Silk.NET.OpenGLES.Extensions.ANGLE;

namespace NFMWorld.Graphics.OpenGL;

public class OpenGLGraphicsDevice : IGraphicsDevice
{
    private readonly GL _gl;

    private OpenGLGraphicsDevice(GL gl, int backBufferWidth, int backBufferHeight)
    {
        _gl = gl;
    }

    public ISwapchain Swapchain { get; }

    /// <inheritdoc />
    public bool HasBottomLeftFramebufferOrigin => true;

    // Create a GL context for the desired window and pass SDL_GL_GetProcAddress to Silk.NET to resolve GL entrypoints.
    public static OpenGLGraphicsDevice Create(Func<string, nint> getProcAddress, int backBufferWidth, int backBufferHeight)
    {
        return new OpenGLGraphicsDevice(GL.GetApi(getProcAddress), backBufferWidth, backBufferHeight);
        
    }
    
    public ICommandBuffer AcquireCommandBuffer()
    {
        throw new NotImplementedException();
    }

    public void Submit(ICommandBuffer commandBuffer)
    {
        throw new NotImplementedException();
    }

    public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        throw new NotImplementedException();
    }

    public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> initialData = default)
    {
        throw new NotImplementedException();
    }

    public IRenderTarget CreateRenderTarget(RenderTargetDesc desc)
    {
        throw new NotImplementedException();
    }

    public ISampler CreateSampler(SamplerDesc desc)
    {
        throw new NotImplementedException();
    }

    public IPipelineState CreatePipeline(PipelineDesc desc)
    {
        throw new NotImplementedException();
    }

    public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
    {
        throw new NotImplementedException();
    }

    public void Dispose()
    {
        throw new NotImplementedException();
    }
}