using NFMWorld.Lua;

namespace NFMWorldLibrary.Util;

/// <summary>
/// A Lua-visible 2D rectangle (position + size), mirroring <see cref="LuaVector2"/>.
/// Used to expose screen-space regions (e.g. a component's current scissor/clip rect)
/// to Lua UI code.
/// </summary>
[LuaVisible]
public readonly partial record struct LuaRect(
    [property: LuaName] float X,
    [property: LuaName] float Y,
    [property: LuaName] float Width,
    [property: LuaName] float Height);
