using System.Numerics;
using NFMWorld.DriverInterface;
using NFMWorld.PluginSystem;

namespace NFMWorld.ClayDom.Events;

[PluginVisible]
public readonly partial record struct BaseMouseDragEvent(
    [property: PluginVisible] Vector2 DragStart,
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] byte Button,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey
);

[PluginVisible]
public readonly partial record struct BaseMouseEvent(
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButton Button,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool AltKey,
    [property: PluginVisible] bool ShiftKey
);

[PluginVisible]
public readonly partial record struct BaseMouseMoveEvent(
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool AltKey,
    [property: PluginVisible] bool ShiftKey
);

[PluginVisible]
public readonly partial record struct BaseMouseWheelEvent(
    [property: PluginVisible] Vector3 Delta,
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey
);

[PluginVisible]
public readonly partial record struct KeyboardEvent(
    [property: PluginVisible] Key KeyChar,
    [property: PluginVisible] Key KeyCode,
    [property: PluginVisible] Keys Keys
);

[PluginVisible]
public readonly partial record struct KeyboardTypingEvent(
    [property: PluginVisible] char KeyChar
);

[PluginVisible]
public readonly partial record struct MouseDragEvent(
    [property: PluginVisible] Vector2 DragStart,
    [property: PluginVisible] Vector2 RelativeDragStart,
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] byte Button,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey,
    [property: PluginVisible] Vector2 RelativePosition
);

/// <summary>
/// One dispatch's propagation state, shared by every node the event reaches.
/// </summary>
/// <remarks>
/// A class, so <see cref="MouseEvent.StopPropagation"/> is visible to the dispatcher while
/// the event itself stays a copyable struct - the alternative, a mutable flag on the
/// struct, would be written to a copy. One of these per press, not per node.
/// </remarks>
public sealed class MouseEventDispatch
{
    public bool Stopped { get; private set; }

    /// <summary>Stops the event from reaching any further ancestor.</summary>
    public void StopPropagation() => Stopped = true;
}

[PluginVisible]
public readonly partial record struct MouseEvent(
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButton Button,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey,
    [property: PluginVisible] Vector2 RelativePosition,
    [property: PluginVisible] MouseEventDispatch? Dispatch = null
)
{
    /// <summary>
    /// Stops this event bubbling to ancestor nodes. Inside a handler, a press that the
    /// node has handled (a modal backdrop, a button in a dialog) should call this so the
    /// click does not also reach an ancestor with its own press handler.
    /// </summary>
    public void StopPropagation() => Dispatch?.StopPropagation();
}

[PluginVisible]
public readonly partial record struct MouseMoveEvent(
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey,
    [property: PluginVisible] Vector2 RelativePosition
);

[PluginVisible]
public readonly partial record struct MouseWheelEvent(
    [property: PluginVisible] Vector3 Delta,
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey,
    [property: PluginVisible] Vector2 RelativePosition
);
