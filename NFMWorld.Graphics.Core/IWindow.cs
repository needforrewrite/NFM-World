namespace NFMWorld.Graphics.Core;

/// <summary>
/// Abstraction over a platform window.
/// </summary>
public interface IWindow
{
    uint Width { get; }
    uint Height { get; }
    TextureFormat SwapchainFormat { get; }
    void RegisterSizeChangeCallback(Action<uint, uint> callback);
}
