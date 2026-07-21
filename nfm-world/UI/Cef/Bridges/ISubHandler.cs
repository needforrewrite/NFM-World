using System.Text.Json;
using NFMWorld.DriverInterface;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Interface for message handlers that can be composed into a <see cref="PhaseBridge"/>
/// as sub-handlers. Sub-handlers receive JS→C# messages before the parent bridge's
/// <see cref="PhaseBridge.OnMessage"/> fallthrough, and can consume key presses
/// for input-capture workflows (e.g., key rebinding).
///
/// Implementations (e.g., SettingsHandler) are not standalone phases — they
/// are activated/deactivated in sync with their parent bridge's lifecycle.
/// </summary>
public interface ISubHandler
{
    /// <summary>
    /// Try to handle an incoming JS→C# message. Return <c>true</c> if the
    /// message was consumed; otherwise the parent bridge's OnMessage will
    /// receive it.
    /// </summary>
    bool TryHandleMessage(string type, JsonElement? args);

    /// <summary>
    /// Called when the parent <see cref="PhaseBridge"/> is registered with a
    /// <see cref="CefRenderer"/>. The sub-handler should store the renderer
    /// reference for Push calls and subscribe to any global events.
    /// </summary>
    void OnActivated(CefRenderer renderer);

    /// <summary>
    /// Called when the parent <see cref="PhaseBridge"/> is unregistered.
    /// The sub-handler should unsubscribe from global events and release
    /// the renderer reference.
    /// </summary>
    void OnDeactivated();

    /// <summary>
    /// Try to consume a key press for input-capture workflows (e.g., key
    /// rebinding). Return <c>true</c> if the key was consumed; the phase
    /// should not process it further.
    /// </summary>
    bool TryHandleKeyPress(Key key);
}
