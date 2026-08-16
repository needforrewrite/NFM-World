using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

[LuaVisible]
public enum TestColor
{
    Red,
    Green,
    Blue,
    Yellow = 100
}

[LuaVisible]
public partial class TypeWithEnum
{
    [LuaName] public TestColor Color { get; set; } = TestColor.Red;
    [LuaName] public TestColor ReadOnlyColor => TestColor.Blue;
    [LuaName] public TestColor? NullableColor { get; set; }

    [LuaName] public TestColor GetColor() => Color;
    [LuaName] public void SetColor(TestColor color) => Color = color;
    [LuaName] public bool IsPrimary(TestColor color) => color is TestColor.Red or TestColor.Green or TestColor.Blue;
    [LuaName] public TestColor DefaultColor => TestColor.Red;
    [LuaName] public TestColor? GetNullableColor(bool returnValue) => returnValue ? TestColor.Blue : null;
}
