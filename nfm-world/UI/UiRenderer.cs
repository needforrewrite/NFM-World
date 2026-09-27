using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using Lua;
using NFMWorld;
using NFMWorld.ClayDom.Events;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace WorldXaml.UI.Yoga;

public class UiRenderer : IDisposable
{
    private uint _maxEvent = 0;

    private readonly Dictionary<string, MessageHandler> _toCsharpHandlers = new();
    private readonly ConcurrentDictionary<uint, (string Event, Action<LuaValue> Handler)> _toLuaHandlers = new();
    private LuaState _state;
    private string _currentPhaseId = "main-menu";

    public View? ActiveRoot { get; private set; }

    public UiRenderer()
    {
        Reload();
    }

    private Action OnEvent(string @event, Action<LuaValue> callback)
    {
        var key = _maxEvent++;
        _toLuaHandlers[key] = (@event, callback);
        return () => _toLuaHandlers.TryRemove(KeyValuePair.Create(key, (@event, callback)));
    }

    private void Call(string method, LuaValue payload)
    {
        // Dispatch to the first registered handler.
        // Only one phase is active at a time, so we dispatch to whichever handler is registered.
        foreach (var handler in _toCsharpHandlers.Values)
        {
            handler(method, payload);
            break;
        }
    }

    private void SetActiveRoot(View view)
    {
        FocusManager.ActiveNode = null;
        FocusManager.FocusedNode = null;
        FocusManager.ClearHover();
        ActiveRoot = view;
    }

    public void Update(GameTime gameTime)
    {
    }

    public void Render()
    {
        NodeDebugger.YogaRoot = ActiveRoot;
        ActiveRoot?.LayoutAndRender(G.Viewport);
    }

    /// <summary>
    /// Delegate for handling Lua → C# messages.
    /// </summary>
    public delegate void MessageHandler(string method, LuaValue payload);

    /// <summary>
    /// Register a per-phase message handler. Only one handler per phase ID is allowed.
    /// </summary>
    public void Register(string phaseId, MessageHandler handler)
    {
        _toCsharpHandlers[phaseId] = handler;
    }

    /// <summary>
    /// Unregister a per-phase message handler.
    /// </summary>
    public void Unregister(string phaseId)
    {
        _toCsharpHandlers.Remove(phaseId);
    }

    /// <summary>
    /// Push an event from C# to Lua for a specific phase.
    /// </summary>
    public void PushToLua(string phaseId, string eventType, LuaValue payload)
    {
        var fullEventId = $"{phaseId}:{eventType}";

        foreach (var (_, (@event, handler)) in _toLuaHandlers)
        {
            if (@event == fullEventId)
            {
                handler(payload);
            }
        }
    }

    public void Navigate(string phaseId)
    {
        _currentPhaseId = phaseId;
        PushToLua("nfmw", "navigate", phaseId);
    }

    public void Dispose()
    {

    }

    [MemberNotNull(nameof(_state))]
    public void Reload()
    {
        // Drop all hooks registered by the previous Lua state (via UiLib.onEvent) and
        // any per-phase C# message handlers. Otherwise stale hooks from the discarded
        // state remain in _toLuaHandlers and get dispatched by PushToLua, invoking
        // Lua callbacks against an abandoned LuaState.
        _toLuaHandlers.Clear();
        _maxEvent = 0;

        _state = LuaHelpers.OpenState();
        NFMWorld.LuaSourceGenerator.Generator.NFMWorld.Game.LuaVisibleTypeRegistry.RegisterAll(_state);
        LuaUiLibrary.Register(_state, SetActiveRoot, Call, OnEvent);
        _state.DoFile("data/uis/router.luau");
        Navigate(_currentPhaseId);
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