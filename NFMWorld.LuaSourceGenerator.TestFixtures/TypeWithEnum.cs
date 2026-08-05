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
    public TestColor Color { get; set; } = TestColor.Red;
    public TestColor ReadOnlyColor => TestColor.Blue;
    public TestColor? NullableColor { get; set; }

    public TestColor GetColor() => Color;
    public void SetColor(TestColor color) => Color = color;
    public bool IsPrimary(TestColor color) => color is TestColor.Red or TestColor.Green or TestColor.Blue;
    public TestColor DefaultColor => TestColor.Red;
}
