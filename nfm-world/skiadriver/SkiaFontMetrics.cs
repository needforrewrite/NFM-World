using NFMWorld.DriverInterface;
using SkiaSharp;

namespace NFMWorld;

internal class SkiaFontMetrics(SKFont font) : IFontMetrics
{
    public int StringWidth(string astring)
    {
        return (int)font.MeasureText(astring);
    }

    public int Height(string astring)
    {
        return (int)font.Metrics.Ascent;
    }
}