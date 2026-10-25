using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// Dev-only phase that hosts the Yoga UI test scene. Owns the UI root, renders
/// it each frame, and forwards mouse/keyboard input to it. Enter via the
/// <c>yoga_test</c> console command.
/// </summary>
public sealed class YogaTestPhase : BasePhase
{
    private readonly View _root;
    private int _counter = 420;
    private LuaVector2 _mouseDownPos;
    private MouseButton? _dragButton;

    public YogaTestPhase()
    {
        // Pure NanoVG UI — no CEF page or bridge.
        CefBridge = null;
        _root = TestView.Render(ref _counter);
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);
        _root.Update();
        _root.LayoutAndRender(G.Viewport);
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (imguiWantsMouse)
            return;

        var pos = new LuaVector2(x, y);

        FocusManager.DispatchMouseMove(_root, new BaseMouseMoveEvent(pos, buttons, ctrlKey, altKey, shiftKey));

        if (_dragButton is { } dragButton)
        {
            _root.DispatchMouseDragged(new BaseMouseDragEvent(_mouseDownPos, pos, (byte)dragButton, buttons, ctrlKey, altKey, shiftKey));
        }
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (imguiWantsMouse)
            return;

        _mouseDownPos = new LuaVector2(x, y);
        _dragButton = button;

        var pos = new LuaVector2(x, y);
        var @event = new BaseMouseEvent(pos, button, buttons, ctrlKey, altKey, shiftKey);
        FocusManager.HandleMousePressed(_root, @event);
        _root.DispatchMousePressed(@event);
    }

    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (imguiWantsMouse)
            return;

        _dragButton = null;

        var pos = new LuaVector2(x, y);
        var @event = new BaseMouseEvent(pos, button, buttons, ctrlKey, altKey, shiftKey);
        FocusManager.HandleMouseReleased();
        _root.DispatchMouseReleased(@event);
    }

    public override void MouseScrolled(int x, int y, int delta, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (imguiWantsMouse)
            return;

        var pos = new LuaVector2(x, y);
        _root.DispatchMouseScrolled(new BaseMouseWheelEvent(new LuaVector3(0f, delta, 0f), pos, buttons, ctrlKey, altKey, shiftKey));
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        if (imguiWantsKeyboard)
            return;

        if (key == Key.Tab)
        {
            FocusManager.FocusNext(_root);
        }

        _root.DispatchKeyPressed(new KeyboardEvent(key, key, keys));
    }
}
