using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud.WorldSkin;

/// <summary>Destination arrow: points at the next checkpoint (or the targeted car), eased toward <see cref="IHudData.ArrowAngle"/>.</summary>
internal sealed class WorldArrow : Component
{
    private const float DegreesPerSecond = 300;

    private static ReadOnlySpan<float> Xs => [0, -0.5f, -0.2f, -0.2f, 0.2f, 0.2f, 0.5f];
    private static ReadOnlySpan<float> Ys => [-0.5f, 0, 0, 0.5f, 0.5f, 0, 0];

    private readonly IHudData _race;
    private float _shown;
    private long _lastTicks;

    public WorldArrow(IHudData race, float size)
    {
        _race = race;
        Styles = Sx.Styles(width: size, height: size, flexShrink: 0, pointerEvents: false);
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        if (_race.ArrowAngle is not { } target)
        {
            _lastTicks = 0;
            return;
        }

        var now = System.Environment.TickCount64;
        if (_lastTicks == 0)
            _shown = target;
        else
        {
            var step = DegreesPerSecond * (now - _lastTicks) / 1000f;
            _shown += Math.Clamp(target - _shown, -step, step);
        }

        _lastTicks = now;

        var radians = _shown * MathF.PI / 180;
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);
        Span<int> px = stackalloc int[7];
        Span<int> py = stackalloc int[7];
        for (var i = 0; i < 7; i++)
        {
            px[i] = (int)(position.X + size.X / 2 + (Xs[i] * cos - Ys[i] * sin) * size.X);
            py[i] = (int)(position.Y + size.Y / 2 + (Xs[i] * sin + Ys[i] * cos) * size.Y);
        }

        G.SaveState();
        try
        {
            G.SetColor(_race.ArrowLocked ? Theme.Warning : Theme.MenuBlue);
            G.FillPolygon(px, py, 7);
            G.SetColor(Theme.TextPrimary);
            G.SetStrokeWidth(MathF.Max(1, size.X / 40));
            G.DrawPolygon(px, py, 7);
        }
        finally
        {
            G.RestoreState();
        }
    }
}
