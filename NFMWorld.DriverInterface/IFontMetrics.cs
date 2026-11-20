using System.Numerics;

namespace NFMWorld.DriverInterface.DriverInterface;

public interface IFontMetrics
{
    public Vector2 MeasureText(ReadOnlySpan<char> text);
    public float LineHeight { get; }

    /// <summary>
    /// Baseline distance below the top of the line box, in pixels at the font's own size. Callers
    /// converting a text baseline to this backend's top-left anchor need it; the default suits a
    /// typical font when an implementation does not track it.
    /// </summary>
    public float Ascent => LineHeight * 0.8f;
}
