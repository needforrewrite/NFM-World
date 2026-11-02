using System.Diagnostics;
using System.Globalization;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

internal sealed class CustomHud : Component
{
    private sealed record Element(HudElement Layout, Color Color, Color Background, Color Outline,
        FontFamily Font, Func<IHudData, string> Text, IImage? Image);

    private readonly HudSkinDefinition _skin;
    private readonly IHudData _data;
    private readonly Element[] _elements;
    private long _lastFrame = Stopwatch.GetTimestamp();
    private float _clock;

    public CustomHud(HudSkinPackage package, IHudData data)
    {
        _skin = package.Definition;
        _data = data;
        _elements = _skin.Elements.Select(e => new Element(e, ParseColor(e.Color), ParseColor(e.Background),
            ParseColor(e.Outline), Enum.Parse<FontFamily>(e.Font), HudText.Compile(e.Text),
            e.Type == "image" ? HudImages.TryLoad($"{package.Directory}/{e.Source}") : null)).ToArray();
    }

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        var now = Stopwatch.GetTimestamp();
        if (!_data.Paused) _clock += Math.Clamp((float)Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds, 0, 0.1f);
        _lastFrame = now;
        var scale = MathF.Min(size.X / _skin.ReferenceWidth, size.Y / _skin.ReferenceHeight) * SettingsMenu.HudScale;
        if (scale <= 0) return;
        G.SaveState();
        G.IntersectScissor(position.X, position.Y, size.X, size.Y);
        var bounds = HudSafeZone.Bounds(position, size);
        position = new Vector2(bounds.X, bounds.Y);
        size = new Vector2(bounds.Width, bounds.Height);
        try
        {
            foreach (var element in _elements)
            {
                var layout = element.Layout;
                if (!layout.IsVisible(_data)) continue;
                var origin = Anchor(layout.Anchor, position, size);
                var x = origin.X + layout.X * scale;
                var y = origin.Y + layout.Y * scale;
                var width = layout.Width * scale;
                var height = layout.Height * scale;
                switch (layout.Type)
                {
                    case "text":
                        DrawText(element.Text(_data), element, x, y, width, scale);
                        break;
                    case "image":
                        if (element.Image is { } image)
                            G.DrawImage(image, (int)x, (int)y, (int)width, (int)height);
                        break;
                    case "bar":
                        Bar(element, x, y, width, height);
                        break;
                    case "arrow":
                        Arrow(element, x, y, width, height, scale);
                        break;
                    case "standings":
                        Standings(element, x, y, width, height, scale);
                        break;
                }
            }
        }
        finally
        {
            G.RestoreState();
        }
    }

    private void Bar(Element element, float x, float y, float width, float height)
    {
        var layout = element.Layout;
        var amount = layout.Value == "damage" ? _data.DamageFraction : _data.PowerFraction;
        G.SetColor(element.Background);
        G.FillRect((int)x, (int)y, (int)width, (int)height);
        var color = element.Color;
        if (layout.CriticalDamage && layout.Value == "damage")
        {
            var green = (int)(244 - 233 * Math.Clamp((amount * 98 - 33) / 65, 0, 1));
            if (amount > 70f / 98 && (int)(_clock * 6) % 2 == 0) green = 170;
            color = new Color(244, (byte)green, 11);
        }
        G.SetColor(color);
        if (amount <= 0) return;
        if (layout.Vertical)
            G.FillRect((int)x, (int)(y + height * (1 - amount)), (int)width, (int)(height * amount));
        else
            G.FillRect((int)x, (int)y, (int)(width * amount), (int)height);
    }

    private void Arrow(Element element, float x, float y, float width, float height, float scale)
    {
        if (_data.ArrowAngle is not { } angle) return;
        ReadOnlySpan<float> xs = [0, -0.5f, -0.2f, -0.2f, 0.2f, 0.2f, 0.5f];
        ReadOnlySpan<float> ys = [-0.5f, 0, 0, 0.5f, 0.5f, 0, 0];
        Span<int> px = stackalloc int[7];
        Span<int> py = stackalloc int[7];
        var radians = angle * MathF.PI / 180;
        var sin = MathF.Sin(radians);
        var cos = MathF.Cos(radians);
        for (var i = 0; i < 7; i++)
        {
            px[i] = (int)(x + width / 2 + (xs[i] * cos - ys[i] * sin) * width);
            py[i] = (int)(y + height / 2 + (xs[i] * sin + ys[i] * cos) * height);
        }
        G.SetColor(element.Color);
        G.FillPolygon(px, py, 7);
        if (element.Layout.OutlineWidth <= 0) return;
        G.SetColor(element.Outline);
        G.SetStrokeWidth(element.Layout.OutlineWidth * scale);
        G.DrawPolygon(px, py, 7);
    }

    private void Standings(Element element, float x, float y, float width, float height, float scale)
    {
        var rowHeight = (element.Layout.FontSize + 12) * scale;
        var bottom = y + height;
        foreach (var racer in _data.Racers)
        {
            if (y + rowHeight > bottom) break;
            if (racer.IsClient)
            {
                G.SetColor(element.Background);
                G.FillRect((int)x, (int)y, (int)width, (int)rowHeight);
            }
            DrawText($"{racer.Position}. {racer.Name}{(racer.Wasted ? " [WASTED]" : "")}", element, x, y, width, scale);
            y += rowHeight;
        }
    }

    private static void DrawText(string text, Element element, float x, float y, float width, float scale)
    {
        G.SetFont(new Font(element.Font, FontStyle.Bold, element.Layout.FontSize * scale));
        G.SetColor(element.Color);
        var metrics = G.GetFontMetrics();
        var textWidth = metrics.MeasureText(text).X;
        x += element.Layout.Align switch { "center" => (width - textWidth) / 2, "right" => width - textWidth, _ => 0 };
        G.DrawStringOutlined(text, (int)x, (int)(y + metrics.LineHeight), element.Outline, element.Layout.OutlineWidth * scale);
    }

    private static Vector2 Anchor(string anchor, Vector2 position, Vector2 size) => position + (anchor switch
    {
        "topCenter" => new Vector2(size.X / 2, 0),
        "topRight" => new Vector2(size.X, 0),
        "center" => size / 2,
        "bottomLeft" => new Vector2(0, size.Y),
        "bottomCenter" => new Vector2(size.X / 2, size.Y),
        "bottomRight" => size,
        _ => Vector2.Zero
    });

    private static Color ParseColor(string color) => new(
        byte.Parse(color.AsSpan(1, 2), NumberStyles.HexNumber),
        byte.Parse(color.AsSpan(3, 2), NumberStyles.HexNumber),
        byte.Parse(color.AsSpan(5, 2), NumberStyles.HexNumber),
        color.Length == 9 ? byte.Parse(color.AsSpan(7, 2), NumberStyles.HexNumber) : (byte)255);
}
