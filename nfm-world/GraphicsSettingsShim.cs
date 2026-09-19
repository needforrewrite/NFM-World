using NFMWorld.Platform.SDL3;

namespace NFMWorld;

/// <summary>
/// Milestone 5 Stage A compatibility shim. Reproduces just the handful of
/// <c>Microsoft.Xna.Framework.GraphicsDeviceManager</c> members that <c>Mad/UI/SettingsMenu.cs</c>'s
/// existing settings-application logic (<c>ApplySettings</c>) reads and writes, so that file keeps
/// compiling and behaving reasonably without needing its own conversion in this stage.
/// </summary>
/// <remarks>
/// Backed by <see cref="SdlWindow"/> where there's a real equivalent today (fullscreen, backbuffer
/// size). Vsync and MSAA have no live-reconfigure path on <c>FNA3DGraphicsDevice</c> yet - those are
/// stored but not applied. TODO(Milestone 5 Stage B): wire these through once the graphics
/// abstraction supports reconfiguring (or recreating) an existing device, and fold this shim back
/// into a real settings object at that point.
/// </remarks>
public sealed class GraphicsSettingsShim(SdlWindow window)
{
    public bool SynchronizeWithVerticalRetrace { get; set; } = true;
    public bool PreferMultiSampling { get; set; }

    public bool IsFullScreen
    {
        get => window.Fullscreen;
        set => window.Fullscreen = value;
    }

    public int PreferredBackBufferWidth { get; set; } = window.Width;
    public int PreferredBackBufferHeight { get; set; } = window.Height;

    public GraphicsDeviceShim GraphicsDevice { get; } = new();

    public void ApplyChanges()
    {
        if (window.Width != PreferredBackBufferWidth || window.Height != PreferredBackBufferHeight)
            window.SetSize(PreferredBackBufferWidth, PreferredBackBufferHeight);
    }
}

/// <summary>Stands in for <c>Microsoft.Xna.Framework.Graphics.GraphicsDevice</c>'s tiny slice that <see cref="GraphicsSettingsShim"/> needs to expose.</summary>
public sealed class GraphicsDeviceShim
{
    public PresentationParametersShim PresentationParameters { get; } = new();
}

/// <summary>Stands in for <c>Microsoft.Xna.Framework.Graphics.PresentationParameters</c>'s tiny slice that <see cref="GraphicsDeviceShim"/> needs to expose.</summary>
public sealed class PresentationParametersShim
{
    public int MultiSampleCount { get; set; }
}
