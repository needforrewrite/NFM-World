using System.ComponentModel;
using WorldXaml.UI.Yoga.Xaml;

namespace NFMWorld.Util;

[TypeConverter(typeof(FontTypeConverter))]
public readonly record struct Font(FontFamily FontFamily, FontStyle Style, float Size);

[Flags]
public enum FontStyle : byte
{
    Plain = 0,
    Bold = 1,
    Italic = 2,
}

public enum FontFamily : byte
{
    DroidSans,
    Adventure,
    AdventureHollow,
    RobotoMono
}