namespace NFMWorld.Graphics.Core;

/// <summary>
/// Represents a GPU texture resource.
/// </summary>
public interface ITexture : IDisposable
{
    uint Width { get; }
    uint Height { get; }
    TextureFormat Format { get; }
    TextureUsageFlags UsageFlags { get; }
}

/// <summary>
/// Represents a GPU buffer resource (vertex, index, etc.).
/// </summary>
public interface IBuffer : IDisposable
{
    uint Size { get; }
    BufferUsageFlags UsageFlags { get; }
}

/// <summary>
/// Represents a compiled GPU shader.
/// </summary>
public interface IShader : IDisposable
{
}

/// <summary>
/// Represents a texture sampler.
/// </summary>
public interface ISampler : IDisposable
{
}

/// <summary>
/// Represents a compiled graphics pipeline (shader + fixed-function state).
/// </summary>
public interface IGraphicsPipeline : IDisposable
{
}

/// <summary>
/// A fence for GPU synchronization.
/// </summary>
public interface IFence
{
}
