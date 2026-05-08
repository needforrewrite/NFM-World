using NFMWorld.Util;

namespace NFMWorld.Gameplay;

public abstract class BasePhase
{
    protected bool MouseDownThisFrame { get; private set; }

    public virtual void BeginGameTick()
    {
    }

    public virtual void GameTick()
    {
    }

    public virtual void EndGameTick()
    {
        MouseDownThisFrame = false;
    }

    /// <summary>
    /// Use <see cref="G"/> here to draw 2D overlays.
    /// Use <see cref="Scene"/> here to draw 3D content.
    /// </summary>
    public virtual void Render()
    {
    }

    /// <summary>
    /// Use ImGui methods in here.
    /// </summary>
    public virtual void RenderImgui()
    {
    }

    /// <summary>
    /// Renders after 2D overlays. Use to draw 3D content over 2D content.
    /// </summary>
    public virtual void Render3DOverlays()
    {
    }

    public virtual void Enter()
    {
    }

    public virtual void Exit()
    {
    }

    public virtual void KeyPressed(Keys key, bool imguiWantsKeyboard)
    {
    }

    public virtual void KeyReleased(Keys key, bool imguiWantsKeyboard)
    {
    }
    
    public virtual void MouseMoved(int x, int y, bool imguiWantsMouse)
    {
    }

    public virtual void MousePressed(int x, int y, bool imguiWantsMouse)
    {
        if (!imguiWantsMouse)
            MouseDownThisFrame = true;
    }

    public virtual void MouseReleased(int x, int y, bool imguiWantsMouse)
    {
    }

    public virtual void MouseScrolled(int delta, bool imguiWantsMouse)
    {
    }

    public virtual void WindowSizeChanged(int width, int height)
    {
    }
}