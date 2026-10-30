using NFMWorld.ClayDom.Events;
using NFMWorld;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.Gameplay;
using NFMWorld.UI.ViewModels;

namespace WorldXaml.UI.Yoga;

/// <summary>
/// Owns the mounted UI tree and forwards input to it.
/// </summary>
/// <remarks>
/// Was the Luau host and then a router over hard-coded routes. Since phases are C# too,
/// a phase simply builds its own tree (<see cref="Gameplay.BasePhase.BuildUi"/>) and the
/// phase stack decides which one is on screen - the router was only ever a lookup table
/// standing in for that, so it is gone.
/// </remarks>
public class UiRenderer : IDisposable
{
    private MountedUi? _mounted;
    private object? _owner;

    public View? ActiveRoot { get; private set; }

    public UiRenderer()
    {
        // Effects run once per frame rather than once per write.
        _ = UiScheduler.Instance;
    }

    /// <summary>
    /// Mounts the entering phase's tree, replacing whatever was on screen. A phase with
    /// no UI clears the screen.
    /// </summary>
    public void ShowPhase(object phase)
    {
        if (ReferenceEquals(_owner, phase) && _mounted is not null)
            return;

        Unmount();
        _owner = phase;

        if (phase is not BasePhase basePhase || basePhase.BuildUi() is not { } tree)
            return;

        _mounted = NFMWorld.Reactor.Sx.Renderer.Mount(() => tree);
        ActiveRoot = (View)_mounted.Root;
    }

    /// <summary>Releases the tree if <paramref name="phase"/> is the one showing.</summary>
    public void HidePhase(object phase)
    {
        if (!ReferenceEquals(_owner, phase))
            return;

        Unmount();
        _owner = null;
    }

    /// <summary>Rebuilds the current tree, e.g. after a hot reload.</summary>
    public void Reload()
    {
        var owner = _owner;
        Unmount();
        _owner = null;

        if (owner is not null)
            ShowPhase(owner);
    }

    private void Unmount()
    {
        FocusManager.ClearFocus();
        FocusManager.ClearHover();

        _mounted?.Dispose();
        _mounted = null;
        ActiveRoot = null;
    }

    public void Update(GameTime gameTime)
    {
        // Effects queued during the frame run here, so a frame's writes coalesce.
        UiScheduler.Instance.Flush();
    }

    public void Render()
    {
        NodeDebugger.YogaRoot = ActiveRoot;
        ActiveRoot?.LayoutAndRender(G.Viewport);
    }

    public void Dispose()
    {
        Unmount();
        _owner = null;
    }

    public void HandleKeyPressed(Key key, in Keys keys)
    {
        ActiveRoot?.DispatchKeyPressed(new KeyboardEvent(key, IBackend.Backend.GetKeyFromScancode(key), keys));
    }

    public void HandleKeyReleased(Key key, in Keys keys)
    {
        ActiveRoot?.DispatchKeyReleased(new KeyboardEvent(key, IBackend.Backend.GetKeyFromScancode(key), keys));
    }

    public void HandleKeyTyped(char character)
    {
        ActiveRoot?.DispatchKeyTyped(new KeyboardTypingEvent(character));
    }

    public void HandleMouseMoved(int x, int y, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (ActiveRoot == null) return;
        FocusManager.DispatchMouseMove(ActiveRoot,
            new BaseMouseMoveEvent(new Vector2(x, y), buttons, ctrlKey, altKey, shiftKey));
    }

    public void HandleMouseDragged(int x, int y, int dragStartX, int dragStartY, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (ActiveRoot == null) return;
        ActiveRoot.DispatchMouseDragged(new BaseMouseDragEvent(
            new Vector2(dragStartX, dragStartY),
            new Vector2(x, y),
            (byte)button,
            buttons,
            ctrlKey,
            altKey,
            shiftKey));
    }

    public void HandleMousePressed(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (ActiveRoot == null) return;
        var @event = new BaseMouseEvent(new Vector2(x, y), button, buttons, ctrlKey, altKey, shiftKey);
        // Focus handling lives in FocusManager: it focuses the focusable element
        // under the cursor, or clears focus when the clicked tree has none.
        FocusManager.HandleMousePressed(ActiveRoot, @event);
        ActiveRoot.DispatchMousePressed(@event);
    }

    public void HandleMouseReleased(int x, int y, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (ActiveRoot == null) return;
        var @event = new BaseMouseEvent(new Vector2(x, y), button, buttons, ctrlKey, altKey, shiftKey);
        FocusManager.HandleMouseReleased();
        ActiveRoot.DispatchMouseReleased(@event);
    }

    public void HandleMouseScrolled(int x, int y, int delta, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        ActiveRoot?.DispatchMouseScrolled(new BaseMouseWheelEvent(new System.Numerics.Vector3(0, delta, 0), new Vector2(x, y), buttons, ctrlKey, altKey, shiftKey));
    }
}
