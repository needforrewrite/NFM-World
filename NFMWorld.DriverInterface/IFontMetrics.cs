using System.Numerics;

namespace NFMWorld.DriverInterface.DriverInterface;

public interface IFontMetrics
{
    public Vector2 MeasureText(ReadOnlySpan<char> text);
    public float LineHeight { get; }
}