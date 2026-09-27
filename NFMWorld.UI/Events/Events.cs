using NFMWorld.DriverInterface;
using NFMWorld.Lua;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorld.ClayDom.Events;

[LuaVisible]
public readonly partial record struct BaseMouseDragEvent(
    [property: LuaName] LuaVector2 DragStart,
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] byte Button,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey
);

[LuaVisible]
public readonly partial record struct BaseMouseEvent(
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButton Button,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool AltKey,
    [property: LuaName] bool ShiftKey
);

[LuaVisible]
public readonly partial record struct BaseMouseMoveEvent(
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool AltKey,
    [property: LuaName] bool ShiftKey
);

[LuaVisible]
public readonly partial record struct BaseMouseWheelEvent(
    [property: LuaName] LuaVector3 Delta,
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey
);

[LuaVisible]
public readonly partial record struct KeyboardEvent(
    [property: LuaName] Key KeyChar,
    [property: LuaName] Key KeyCode,
    [property: LuaName] Keys Keys
);

[LuaVisible]
public readonly partial record struct KeyboardTypingEvent(
    [property: LuaName] char KeyChar
);

[LuaVisible]
public readonly partial record struct MouseDragEvent(
    [property: LuaName] LuaVector2 DragStart,
    [property: LuaName] LuaVector2 RelativeDragStart,
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] byte Button,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey,
    [property: LuaName] LuaVector2 RelativePosition
);

[LuaVisible]
public readonly partial record struct MouseEvent(
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButton Button,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey,
    [property: LuaName] LuaVector2 RelativePosition
);

[LuaVisible]
public readonly partial record struct MouseMoveEvent(
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey,
    [property: LuaName] LuaVector2 RelativePosition
);

[LuaVisible]
public readonly partial record struct MouseWheelEvent(
    [property: LuaName] LuaVector3 Delta,
    [property: LuaName] LuaVector2 Position,
    [property: LuaName] MouseButtons Buttons,
    [property: LuaName] bool CtrlKey,
    [property: LuaName] bool MetaKey,
    [property: LuaName] bool ShiftKey,
    [property: LuaName] LuaVector2 RelativePosition
);