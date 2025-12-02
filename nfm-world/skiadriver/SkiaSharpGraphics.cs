using NFMWorld.DriverInterface;
using NFMWorld.Util;
using Silk.NET.Maths;
using SkiaSharp;

namespace NFMWorld;

internal class SkiaSharpGraphics(SKCanvas canvas) : IGraphics
{
    private float sx = 1;
    private float sy = 1;

    public Vector2D<float> Ratio
    {
        set
        {
            //canvas.Scale(1/sx, 1/sy);
            //canvas.Scale(value.X, value.Y);
            sx = value.X;
            sy = value.Y;
            //_paint.StrokeWidth = (sx + sy) / 3;
        }
    }

    private readonly SKPaint _paint = new()
    {
        IsAntialias = false,
    };
    private SKFont _font = new();
    private Dictionary<(string FamilyName, byte Flags), SKTypeface> _typefaceCache = new();

    public void SetColor(Color c)
    {
        _paint.Color = new SKColor(c.R, c.G, c.B, c.A);
    }

    public void FillPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 2)
        {
            return;
        }
        _paint.Style = SKPaintStyle.Fill;
        
        using var path = new SKPath();
        
        // calculate centroid
        float cx, cy;
        {
            int x = 0;
            int y = 0;
            for (int i = 0; i < nPoints; i++) {
                x += xPoints[i];
                y += yPoints[i];
            }
            cx = (float) x / nPoints;
            cy = (float) y / nPoints;
        }
        float sx = xPoints[0] < cx ? xPoints[0] - 0.5f : xPoints[0] + 0.5f;
        float sy = yPoints[0] < cy ? yPoints[0] - 0.5f : yPoints[0] + 0.5f;
        
        path.MoveTo(sx, sy);
        
        for (int i = 1; i < nPoints; i++) {
            int x = xPoints[i];
            int y = yPoints[i];
            if (xPoints[i - 1] == x && yPoints[i - 1] == y) continue;
            path.LineTo(
                x < cx ? x - 0.5f : x + 0.5f,
                y < cy ? y - 0.5f : y + 0.5f
            );
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void DrawPolygon(Span<int> xPoints, Span<int> yPoints, int nPoints)
    {
        if (nPoints <= 1 || (nPoints == 2 && xPoints[0] == xPoints[1] && yPoints[0] == yPoints[1]))
        {
            return;
        }
        
        _paint.Style = SKPaintStyle.Stroke;
        
        using var path = new SKPath();
        path.MoveTo(xPoints[0], yPoints[0]);
        
        for (int i = 1; i < nPoints; i++) {
            if (xPoints[i] == xPoints[i - 1] && yPoints[i] == yPoints[i - 1]) continue;
            path.LineTo(xPoints[i], yPoints[i]);
        }
        path.LineTo(xPoints[0], yPoints[0]);

        canvas.DrawPath(path, _paint);
    }

    public void FillRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawLine(int x1, int y1, int x2, int y2)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawLine(x1, y1, x2, y2, _paint);
    }

    public void SetAlpha(float f)
    {
        _paint.Color = _paint.Color.WithAlpha((byte)(f * 255));
    }

    public void DrawImage(IImage image, int x, int y)
    {
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, x, y);
    }

    public void SetFont(Font font)
    {
        if (_typefaceCache.TryGetValue((font.FontName, (byte)font.Flags), out var typeface))
        {
            _font.Typeface = typeface;
        }
        else
        {
            _font.Typeface = _typefaceCache[(font.FontName, (byte)font.Flags)] = SKTypeface.FromFamilyName(
                font.FontName,
                font.Flags == Font.BOLD ? SKFontStyleWeight.Bold : SKFontStyleWeight.Normal,
                SKFontStyleWidth.Normal,
                font.Flags == Font.ITALIC ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright
            );
        }
        
        _font.Size = font.Size;
    }

    public IFontMetrics GetFontMetrics()
    {
        return new SkiaFontMetrics(_font);
    }

    public void DrawString(string text, int x, int y)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawText(text, x, y, SKTextAlign.Left, _font, _paint);
    }

    public void FillOval(int p0, int p1, int p2, int p3)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawOval(p0, p1, p2, p3, _paint);
    }

    public void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRoundRect(x, y, wid, hei, arcWid, arcHei, _paint);
    }

    public void DrawRect(int x1, int y1, int width, int height)
    {
        _paint.Style = SKPaintStyle.Stroke;
        canvas.DrawRect(x1, y1, width, height, _paint);
    }

    public void DrawImage(IImage image, int x, int y, int width, int height)
    {
        _paint.Style = SKPaintStyle.Fill;
        canvas.DrawImage(((MadSharpSKImage)image).SkImage, new SKRect(x, y, width + x, height + y));
    }

    public void SetAntialiasing(bool useAntialias)
    {
        _paint.IsAntialias = useAntialias;
    }
}