using ImGuiNET;
using NFMWorld.Util;

namespace NFMWorld.Mad;

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

    public virtual void Render()
    {
    }

    public virtual void RenderImgui()
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

    public virtual void WindowSizeChanged(int width, int height)
    {
    }

    public virtual void RenderAfterSkia()
    {
    }
}