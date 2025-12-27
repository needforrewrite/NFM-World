using NFMWorld.Util;
using NFMWorld.Mad.UI.yoga;
using NFMWorld.DriverInterface;
using System.Diagnostics;

namespace NFMWorld.Mad.UI.Elements;

public class Timer : Node
{
    public Stopwatch Stopwatch;

    public Timer()
    {
        Stopwatch = new Stopwatch();
    }

    public override void RenderContent(Vector2 position, Vector2 size)
    {
        G.SetColor(new Color(0, 0, 0));
        G.DrawStringStroke($"Time: {Stopwatch.Elapsed.Minutes:D2}:{Stopwatch.Elapsed.Seconds:D2}.{Stopwatch.Elapsed.Milliseconds / 10:D3}", (int)position.X, (int)position.Y);
        G.SetColor(new Color(255, 255, 255));
        G.DrawString($"Time: {Stopwatch.Elapsed.Minutes:D2}:{Stopwatch.Elapsed.Seconds:D2}.{Stopwatch.Elapsed.Milliseconds / 10:D3}", (int)position.X, (int)position.Y);
    }
}