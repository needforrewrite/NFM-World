using NFMWorld.DriverInterface;



namespace NFMWorld.Gameplay;

public abstract class BasePhase : IDisposable
{
    /// <summary>
    /// Whether the UI should receive input while this phase is active. Menu phases
    /// return true; race phases forward input only while paused.
    /// </summary>
    public virtual bool EnableUiInput => true;

    /// <summary>
    /// The UI this phase shows, or null for a phase with no UI (the editors, which draw
    /// themselves). A phase owns its tree: it is built once on <see cref="Enter"/> and
    /// torn down on <see cref="Exit"/>, so a phase buried in the stack contributes nothing.
    /// </summary>
    public virtual NFMWorld.Reactor.Node? BuildUi() => null;

    /// <summary>
    /// Whether the mouse was pressed this game tick. Reset at the end of a game tick.
    /// </summary>
    protected bool MouseDownThisFrame { get; private set; }
    
    /// <summary>
    /// Invoked each frame by the game loop. Override to write this frame's state into
    /// the UI's signals (e.g. HUD telemetry).
    /// </summary>
    public virtual void PushUiState()
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

        PushUiState();
    }

    /// <summary>
    /// Use <see cref="G"/> here to draw 2D overlays.
    /// Use <see cref="Scene"/> here to draw 3D content.
    /// </summary>
    public virtual void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
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
    public virtual void Render3DOverlays(NFMWorld.Graphics.ICommandBuffer cb)
    {
    }

    /// <summary>
    /// Invoked when <see cref="GameSparker.SetPhase"/> is called with the phase.
    /// </summary>
    public virtual void Enter()
    {
        // Navigation is driven by the phase stack: whichever phase is on top owns the UI.
        // Phases are re-entered when an overlay above them pops, so this also restores
        // the view on the way back.
        GameSparker.UiRenderer?.ShowPhase(this);
    }

    /// <summary>
    /// Invoked when <see cref="GameSparker.SetPhase"/> was called with the phase before, and is now being called with
    /// a new phase.
    /// </summary>
    public virtual void Exit()
    {
        // The phase above is taking the screen; this one's tree goes with it. It is
        // rebuilt from BuildUi when this phase is re-entered, which is also what makes
        // the re-entry free of stale state.
        GameSparker.UiRenderer?.HidePhase(this);

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
    }

    public virtual void KeyTyped(char character, bool imguiWantsKeyboard)
    {
    }

    /// <summary>
    /// Invoked when a key is released.
    /// </summary>
    /// <param name="key">The key.</param>
    /// <param name="imguiWantsKeyboard">If Imgui wants the keyboard.</param>
    /// <param name="keys">The state of all keys.</param>
    public virtual void KeyReleased(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
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
    {
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
    {
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