using NFMWorld.Graphics;
using NFMWorld.Platform.SDL3;

namespace NFMWorld;

/// <summary>
/// Milestone 5 Stage A compatibility shim. Reproduces just the handful of
/// <c>Microsoft.Xna.Framework.GraphicsDeviceManager</c> members that <c>Mad/UI/SettingsMenu.cs</c>'s
/// existing settings-application logic (<c>ApplySettings</c>) reads and writes, so that file keeps
/// compiling and behaving reasonably without needing its own conversion in this stage.
/// </summary>
/// <remarks>
/// Backed by <see cref="SdlWindow"/> and <see cref="ISwapchain"/>. Size and multisample count are
/// both drawable properties, so they are applied by rebuilding the drawable rather than by any
/// per-draw state call - and a rebuild is a device operation, so <see cref="WorldGame"/> owns the
/// moment it happens (see <see cref="DesiredMultiSampleCount"/>).
/// <para>
/// That is enough for the size on every backend and for the sample count on sokol and FNA3D, but
/// not for the sample count on the two GL renderers: there it lives in the window's pixel format,
/// which only a new context can change, so it is reported through
/// <see cref="RequiresRestartForMultiSampling"/> instead of being silently accepted.
/// </para>
/// <para>
/// Vsync is applied, but not here: it is an argument to the present call rather than a property of
/// the drawable, so <c>WorldGame</c> pushes it onto whichever backend is in play on every frame.
/// </para>
/// </remarks>
public sealed class GraphicsSettingsShim(SdlWindow window, ISwapchain swapchain)
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

    /// <summary>
    /// The multisample count the settings ask for, normalized: 0 when multisampling is off, and 0
    /// for a requested count of 1, which the UI's "MSAA 1x" entry uses to mean off.
    /// </summary>
    /// <remarks>
    /// A request only - the hardware may clamp it, so the count actually in effect is
    /// <see cref="AppliedMultiSampleCount"/>. Applying it is <see cref="WorldGame"/>'s job, because
    /// rebuilding the backbuffer cannot happen while a command buffer is live, and this shim's
    /// writers run from ImGui callbacks that execute inside the frame with one acquired.
    /// </remarks>
    public int DesiredMultiSampleCount
    {
        get
        {
            if (!PreferMultiSampling) return 0;
            var requested = GraphicsDevice.PresentationParameters.MultiSampleCount;
            return requested <= 1 ? 0 : requested;
        }
    }

    /// <summary>The multisample count the drawable actually has, after hardware clamping.</summary>
    public int AppliedMultiSampleCount => swapchain.MultiSampleCount;

    /// <summary>
    /// Whether the user's MSAA setting differs from what the drawable has <em>and</em> this backend
    /// cannot close the gap without a new context.
    ///
    /// Both halves are needed. The difference alone is not enough, because a backend that clamps a
    /// request will report a permanent difference that has nothing left to apply - sokol refuses
    /// anything above one, and reporting a restart for that would be a lie the user could act on
    /// forever. The backend's own answer alone is not enough either, because a restart is only worth
    /// asking for when there is actually something pending.
    ///
    /// The GL renderers are the case this exists for: their sample count is baked into the window's
    /// pixel format at context creation, so a mid-session change can only take effect on the next
    /// launch, and without this the setting would appear to apply and silently not.
    /// </summary>
    public bool RequiresRestartForMultiSampling =>
        swapchain.MultiSampleChangeRequiresRestart && DesiredMultiSampleCount != AppliedMultiSampleCount;

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
