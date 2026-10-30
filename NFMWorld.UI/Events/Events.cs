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

[PluginVisible]
public readonly partial record struct MouseEvent(
    [property: PluginVisible] Vector2 Position,
    [property: PluginVisible] MouseButton Button,
    [property: PluginVisible] MouseButtons Buttons,
    [property: PluginVisible] bool CtrlKey,
    [property: PluginVisible] bool MetaKey,
    [property: PluginVisible] bool ShiftKey,
    [property: PluginVisible] Vector2 RelativePosition
);

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