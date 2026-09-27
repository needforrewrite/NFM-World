using NFMWorld.Lua;

namespace NFMWorld.DriverInterface;

[Flags, LuaVisible]
public enum MouseButtons
{
    None = 0,
    Primary = 1,
    Secondary = 2,
    Middle = 4,
    XButton1 = 8,
    XButton2 = 16,
}