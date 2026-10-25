using NFMWorld.Graphics;
using NFMWorld.Platform.SDL3;

namespace NFMWorld;

public interface IGameHost
{
    SdlImGuiRenderer? ImguiRenderer { get; }
    
    /// <summary>Compatibility shim for <c>Mad/UI/SettingsMenu.cs</c>'s existing settings-application logic. See <see cref="GraphicsSettingsShim"/>.</summary>
    public GraphicsSettingsShim Graphics { get; }

    /// <summary>The SDL3 window this game owns.</summary>
    public SdlWindow Window { get; }
    
    /// <summary>Replaces FNA's <c>Game.IsFixedTimeStep</c> - read by the manual loop in <see cref="Main"/>.</summary>
    public bool IsFixedTimeStep { get; set; }

    /// <summary>Replaces FNA's <c>Game.TargetElapsedTime</c> - read by the manual loop in <see cref="Main"/> and by <see cref="Draw"/>'s alpha calculation.</summary>
    public TimeSpan TargetElapsedTime { get; set; }

    /// <summary>Whether the window currently has input focus. Replaces FNA's <c>Game.IsActive</c>.</summary>
    bool IsActive { get; }

    void SetFullScreenScissor(ICommandBuffer cb);
}