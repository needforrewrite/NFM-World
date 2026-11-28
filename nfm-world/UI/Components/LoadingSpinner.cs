using System.Diagnostics;
using System.Numerics;
using NFMWorld.Reactor;
using static NFMWorld.DriverInterface.DriverInterface.TheGraphics;

namespace NFMWorld.UI.Components;

internal sealed class LoadingSpinner : View
{
    public float Radius { get; init; } = 12;

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var center = position + size * 0.5f;
        var radius = MathF.Min(Radius * G.Scale, MathF.Min(size.X, size.Y) * 0.25f);

        if (radius <= 0)

            return;
        var angle = (float)(Stopwatch.GetTimestamp() / (double)Stopwatch.Frequency % 1) * 360;

        G.SaveState();
        G.SetStrokeWidth(MathF.Max(1, 2 * G.Scale));
        G.SetColor(new Color(128, 170, 255, 45));
        G.BeginPath();
        G.Arc(center.X, center.Y, radius, 0, 360, true);
        G.Stroke();
        G.SetColor(Theme.MenuBlue);
        G.BeginPath();
        G.Arc(center.X, center.Y, radius, angle, angle + 100, true);
        G.Stroke();
        G.RestoreState();
    }
}
