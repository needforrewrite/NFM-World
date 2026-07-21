using System.Text.Json;
using MemoryPack;
using NFMWorld.DriverInterface;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Abstract base for per-phase C#↔JS bridges. Each phase that uses CEF for UI
/// creates a subclass, registers it with <see cref="CefRenderer"/> on Enter,
/// and unregisters on Exit.
///
/// Usage:
///   public sealed class MainMenuBridge : PhaseBridge
///   {
///       public MainMenuBridge() : base("main-menu") { }
///
///       protected override void OnMessage(string type, JsonElement? args)
///       {
///           switch (type)
///           {
///               case "navigate": OnNavigate(args); break;
///           }
///       }
///   }
///
///   // In MainMenuPhase:
///   _bridge = new MainMenuBridge();
///   _bridge.Register(_cefRenderer);
///   _bridge.Push("account", new { Name = "Player", ... });
/// </summary>
public abstract class PhaseBridge(string phaseId) : IDisposable
{
    /// <summary>
    /// Unique identifier for this phase's bridge. Used as the dispatch key
    /// in CefRenderer's message registry.
    /// </summary>
    public string PhaseId { get; } = phaseId ?? throw new ArgumentNullException(nameof(phaseId));

    /// <summary>
    /// The CefRenderer this bridge is registered with. Set by <see cref="Register"/>.
    /// </summary>
    protected CefRenderer? Renderer { get; private set; }

    /// <summary>
    /// The URL to load when this phase becomes active. Subclasses override this
    /// to return the phase-specific HTML page.
    /// </summary>
    public virtual string? PageUrl { get; } = $"#/{phaseId}";

    /// <summary>
    /// Whether CEF input should be forwarded while this phase is active.
    /// Menu phases typically return true; race phases return false.
    /// </summary>
    public virtual bool EnableInput => true;

    // ── Sub-handler support ─────────────────────────────────────
    // Sub-handlers are composable message/key handlers (e.g., SettingsHandler)
    // that live within a parent bridge. They are activated/deactivated in
    // sync with the parent bridge's Register/Unregister lifecycle.

    /// <summary>
    /// Sub-handlers registered on this bridge. Checked before
    /// <see cref="OnMessage"/> fallthrough on every incoming JS→C# message.
    /// </summary>
    protected readonly List<ISubHandler> SubHandlers = [];

    /// <summary>
    /// Add a sub-handler. If the bridge is already registered, the sub-handler
    /// is activated immediately.
    /// </summary>
    protected void AddSubHandler(ISubHandler handler)
    {
        SubHandlers.Add(handler);
        if (Renderer != null)
            handler.OnActivated(Renderer);
    }

    /// <summary>
    /// Remove a sub-handler. If the bridge is still registered, the sub-handler
    /// is deactivated immediately.
    /// </summary>
    protected void RemoveSubHandler(ISubHandler handler)
    {
        if (SubHandlers.Remove(handler) && Renderer != null)
            handler.OnDeactivated();
    }

    /// <summary>
    /// Forward a key press to all sub-handlers. Returns <c>true</c> if any
    /// sub-handler consumed the key (e.g., during key rebinding capture).
    /// Call from the phase's <c>KeyPressed</c> override.
    /// </summary>
    public bool TryHandleKeyPress(Key key)
    {
        foreach (var handler in SubHandlers)
        {
            if (handler.TryHandleKeyPress(key))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Register this bridge with the given CefRenderer. Called from Phase.Enter().
    /// Navigates to <see cref="PageUrl"/> if non-null. Uses ExecuteJavaScript
    /// for hash-only changes to avoid full page reloads.
    /// </summary>
    public void Register(CefRenderer renderer)
    {
        Renderer = renderer ?? throw new ArgumentNullException(nameof(renderer));
        Renderer.RegisterMessageHandler(PhaseId, DispatchMessage);

        if (PageUrl is { } url)
        {
            Renderer.ExecuteJavaScript($"window.location.href = '{url}';");
        }

        OnRegistered();

        // Activate all sub-handlers now that Renderer is available
        foreach (var handler in SubHandlers)
            handler.OnActivated(Renderer);
    }

    /// <summary>
    /// Unregister this bridge from the CefRenderer. Called from Phase.Exit().
    /// </summary>
    public void Unregister()
    {
        // Deactivate sub-handlers before tearing down
        foreach (var handler in SubHandlers)
            handler.OnDeactivated();

        Renderer?.UnregisterMessageHandler(PhaseId);

        OnUnregistered();
        Renderer = null;
    }

    /// <summary>
    /// Push an event from C# to JS via CefProcessMessage. The JS side receives
    /// this via window.__nfmwDispatch("{PhaseId}:{eventType}", data).
    /// </summary>
    /// <remarks>
    /// This method pushes the value via MemoryPack serialization.
    /// </remarks>
    protected void PushMemoryPack<T>(string eventType, T? data)
    {
        Renderer?.PushToJs(PhaseId, eventType, MemoryPackSerializer.Serialize(data));
    }

    /// <summary>
    /// Push an event from C# to JS via CefProcessMessage. The JS side receives
    /// this via window.__nfmwDispatch("{PhaseId}:{eventType}", data).
    /// </summary>
    /// <remarks>
    /// This method pushes the value via JSON serialization.
    /// </remarks>
    protected void Push(string eventType, object? data)
    {
        Renderer?.PushToJs(PhaseId, eventType, data);
    }

    /// <summary>
    /// Push an event from C# to JS via CefProcessMessage. The JS side receives
    /// this via window.__nfmwDispatch("{PhaseId}:{eventType}", data).
    /// Supports binary payloads (uint8array) natively through the process message.
    /// </summary>
    protected void Push(string eventType, byte[] binary)
    {
        Renderer?.PushToJs(PhaseId, eventType, binary);
    }

    /// <summary>
    /// Called when the JS page sends a message via nfmw.call(methodName, ...).
    /// Subclasses override this to handle phase-specific messages.
    /// </summary>
    /// <param name="type">The method name called from JS.</param>
    /// <param name="args">
    /// The first argument from JS, parsed as a JsonElement if the call included
    /// a JSON-stringifiable object argument; otherwise null.
    /// </param>
    protected abstract void OnMessage(string type, JsonElement? args);

    /// <summary>
    /// Called after the bridge is successfully registered and the page URL
    /// has been navigated to (if any).
    /// </summary>
    protected virtual void OnRegistered() { }

    /// <summary>
    /// Called after the bridge is unregistered, before Renderer is set to null.
    /// </summary>
    protected virtual void OnUnregistered() { }

    /// <summary>
    /// Dispatch an incoming JS message. Packages the raw args into a JsonElement
    /// for subclasses to consume.
    /// </summary>
    private void DispatchMessage(string messageType, string? rawJson)
    {
        JsonElement? parsed = null;
        if (rawJson != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(rawJson);
                parsed = doc.RootElement.Clone();
            }
            catch (JsonException)
            {
                // If parsing fails, pass null — the subclass can handle raw args
                // via the raw string if needed (but most will use the typed path).
            }
        }

        // Try sub-handlers first; if any consumes the message, stop.
        foreach (var handler in SubHandlers)
        {
            if (handler.TryHandleMessage(messageType, parsed))
                return;
        }

        OnMessage(messageType, parsed);
    }

    public virtual void Dispose()
    {
        Unregister();
    }
}
