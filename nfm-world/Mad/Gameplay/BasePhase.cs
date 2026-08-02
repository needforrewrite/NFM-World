using NFMWorld.DriverInterface;
using NFMWorld.UI.Cef;
using NFMWorld.UI.Cef.Bridges;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorld.Gameplay;

public abstract class BasePhase : IDisposable
{
    /// <summary>
    /// Whether CEF input should be forwarded while this phase is active.
    /// Defaults to the bridge's preference (<see cref="PhaseBridge.EnableInput"/>)
    /// if a bridge is set, otherwise false. Override for custom logic.
    /// </summary>
    public virtual bool EnableCefInput => CefBridge?.EnableInput ?? false;

    /// <summary>
    /// The phase's CEF bridge, if any. Subclasses set this in their constructor
    /// or Enter(). The bridge is registered during Enter() and unregistered during Exit().
    /// </summary>
    public PhaseBridge? CefBridge { get; protected set; } = new DummyBridge();

    /// <summary>
    /// Whether the mouse was pressed this game tick. Reset at the end of a game tick.
    /// </summary>
    protected bool MouseDownThisFrame { get; private set; }
    
    /// <summary>
    /// Invoked each frame by WorldGame.Update() when this phase has an active CefBridge.
    /// Override to push per-frame state (e.g., HUD data) to JS.
    /// </summary>
    public virtual void PushCefState()
    {
    }

    /// <summary>
    /// Invoked at the beginning of a game tick.
    /// </summary>
    public virtual void BeginGameTick()
    {
    }

    /// <summary>
    /// Invoked at the middle of a game tick.
    /// </summary>
    public virtual void GameTick()
    {
    }

    /// <summary>
    /// Invoked at the end of a game tick.
    /// </summary>
    public virtual void EndGameTick()
    {
        MouseDownThisFrame = false;

        // Per-frame CEF state push: let the current phase push its state to JS.
        if (CefBridge != null)
        {
            PushCefState();
        }
    }

    /// <summary>
    /// Use <see cref="G"/> here to draw 2D overlays.
    /// Use <see cref="Scene"/> here to draw 3D content.
    /// </summary>
    public virtual void Render(float alpha)
    {
        // UI rendering handled by CEF overlay
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

    /// <summary>
    /// Invoked when <see cref="GameSparker.SetPhase"/> is called with the phase.
    /// </summary>
    public virtual void Enter()
    {
        // Register the phase's CEF bridge if one is set
        if (CefBridge != null && GameSparker.CefRenderer != null)
        {
            CefBridge.Register(GameSparker.CefRenderer);
        }

        // Enable/disable CEF input based on phase preference
        if (GameSparker.CefRenderer != null)
        {
            GameSparker.CefRenderer.SetInputEnabled(EnableCefInput);
        }

        // Consume the current keyboard state to prevent key bleeding.
        // When a phase transition is triggered by a key press (e.g., Enter on
        // stage select → garage), the same physical key-down must not be
        // forwarded to CEF as a new RawKeyDown for the incoming phase's page.
        GameSparker.CefRenderer?.ConsumeKeyboardState();
    }

    /// <summary>
    /// Invoked when <see cref="GameSparker.SetPhase"/> was called with the phase before, and is now being called with
    /// a new phase.
    /// </summary>
    public virtual void Exit()
    {
        // Unregister the phase's CEF bridge
        CefBridge?.Unregister();

        // Disposal is now handled by PhaseManager.FlushDisposals() at end-of-frame.
        // Phases on the stack are kept alive; popped phases are queued for deferred disposal.
    }

    /// <summary>
    /// Invoked when a key is pressed.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="imguiWantsKeyboard">If Imgui wants the keyboard.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        // CEF handles input
    }

    public virtual void KeyTyped(char character, bool imguiWantsKeyboard)
    {
        // CEF handles input
    }

    /// <summary>
    /// Invoked when a key is released.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="imguiWantsKeyboard">If Imgui wants the keyboard.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    { // CEF handles input
    }

    /// <summary>
    /// Invoked when the mouse is moved.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="imguiWantsMouse">If Imgui wants the mouse.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    { // CEF handles input
    }

    /// <summary>
    /// Invoked when a mouse button is pressed.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="imguiWantsMouse">If Imgui wants the mouse.</param>
    /// <param name="button">The button that was pressed.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        if (!imguiWantsMouse) { MouseDownThisFrame = true; }
    }

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x">The X mouse position.</param>
    /// <param name="y">The Y mouse position.</param>
    /// <param name="imguiWantsMouse">If Imgui wants the mouse.</param>
    /// <param name="button">The button that was released.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button, MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    { // CEF handles input
    }

    /// <summary>
    /// Invoked when a mouse button is released.
    /// </summary>
    /// <param name="x"></param>
    /// <param name="y"></param>
    /// <param name="delta">The delta Y change.</param>
    /// <param name="imguiWantsMouse">If Imgui wants the mouse.</param>
    /// <param name="buttons">The state of all buttons.</param>
    /// <param name="ctrlKey">Whether the Control key is being held.</param>
    /// <param name="shiftKey">Whether the Shift key is being held.</param>
    /// <param name="altKey">Whether the Alt key is being held.</param>
    public virtual void MouseScrolled(int x, int y, int delta, bool imguiWantsMouse, MouseButtons buttons, bool ctrlKey,
        bool shiftKey, bool altKey)
    {
        // CEF handles scroll input
    }

    public virtual void WindowSizeChanged(int width, int height)
    {
    }

    private void ReleaseUnmanagedResources()
    {
    }

    protected virtual void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
        if (disposing)
        {
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    ~BasePhase()
    {
        Dispose(false);
    }
}