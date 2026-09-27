using System.Numerics;
using Maxine.Extensions.Mathematics;

namespace NFMWorld.DriverInterface.DriverInterface;

public interface IGraphics
{
    Vector2 Viewport { get; }

    // Length proportional to radius of a cubic bezier handle for 90deg arcs
    private const float NVG_KAPPA90 = 0.5522847493f;
    
    IImage LoadImage(string file);
    IImage LoadImage(ReadOnlyMemory<byte> file);

    void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos);
    void SetColor(Color c);

    void FillPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
    {
        if (n < 1) return;
        BeginPath();
        MoveTo(x[0], y[0]);
        for (int i = 1; i < n; i++)
        {
            LineTo(x[i], y[i]);
        }
        ClosePath();
        Fill();
    }

    void DrawPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n)
    {
        if (n < 1) return;
        BeginPath();
        MoveTo(x[0], y[0]);
        for (int i = 1; i < n; i++)
        {
            LineTo(x[i], y[i]);
        }
        ClosePath();
        Stroke();
    }

    void FillRect(int x, int y, int width, int height)
    {
        BeginPath();
        Rect(x, y, width, height);
        Fill();
    }

    void Rect(int x, int y, int width, int height)
    {
        MoveTo(x, y);
        LineTo(x, y + height);
        LineTo(x + width, y + height);
        LineTo(x + width, y);
        ClosePath();
    }

    void FillRoundedRect(int x, int y, int width, int height, float radTopLeft, float radTopRight,
        float radBottomRight, float radBottomLeft)
    {
        BeginPath();
        RoundedRectVarying(x, y, width, height, radTopLeft, radTopRight, radBottomRight, radBottomLeft);
        Fill();
    }

    void RoundedRectVarying(int x, int y, int width, int height, float radTopLeft, float radTopRight,
        float radBottomRight, float radBottomLeft)
    {
        if (radTopLeft < 0.1f && radTopRight < 0.1f && radBottomRight < 0.1f && radBottomLeft < 0.1f)
        {
            Rect(x, y, width, height);
        }
        else
        {
            var halfw = Math.Abs(width) * 0.5f;
            var halfh = Math.Abs(height) * 0.5f;
            var rxBL = Math.Min(radBottomLeft, halfw) * Math.Sign(width);
            var ryBL = Math.Min(radBottomLeft, halfh) * Math.Sign(height);
            var rxBR = Math.Min(radBottomRight, halfw) * Math.Sign(width);
            var ryBR = Math.Min(radBottomRight, halfh) * Math.Sign(height);
            var rxTR = Math.Min(radTopRight, halfw) * Math.Sign(width);
            var ryTR = Math.Min(radTopRight, halfh) * Math.Sign(height);
            var rxTL = Math.Min(radTopLeft, halfw) * Math.Sign(width);
            var ryTL = Math.Min(radTopLeft, halfh) * Math.Sign(height);

            MoveTo(x, y + ryTL);
            LineTo(x, y + height - ryBL);
            BezierTo(x, y + height - ryBL * (1 - NVG_KAPPA90), x + rxBL * (1 - NVG_KAPPA90), y + height, x + rxBL, y + height);
            LineTo(x + width - rxBR, y + height);
            BezierTo(x + width - rxBR * (1 - NVG_KAPPA90), y + height, x + width, y + height - ryBR * (1 - NVG_KAPPA90), x + width, y + height - ryBR);
            LineTo(x + width, y + ryTR);
            BezierTo(x + width, y + ryTR * (1 - NVG_KAPPA90), x + width - rxTR * (1 - NVG_KAPPA90), y, x + width - rxTR, y);
            LineTo(x + rxTL, y);
            BezierTo(x + rxTL * (1 - NVG_KAPPA90), y, x, y + ryTL * (1 - NVG_KAPPA90), x, y + ryTL);
            ClosePath();
        }
    }

    void DrawLine(int x1, int y1, int x2, int y2)
    {
        BeginPath();
        MoveTo(x1, y1);
        LineTo(x2, y2);
        Stroke();
    }
    float Alpha { set; }
    float Scale { get; set; }
    void DrawImage(IImage image, int x, int y);
    void SetFont(Font font);
    IFontMetrics GetFontMetrics();
    IFontMetrics GetFontMetrics(Font font);
    void DrawString(ReadOnlySpan<char> text, int x, int y);
    void DrawStringAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top);
    void DrawStringStrokeAligned(ReadOnlySpan<char> text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1);
    
    void DrawStringAligned(ReadOnlySpan<char> text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top) 
        => DrawStringAligned(text, 0, 0, areaWidth, areaHeight, hAlign, vAlign);

    void DrawStringStrokeAligned(ReadOnlySpan<char> text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        => DrawStringStrokeAligned(text, 0, 0, areaWidth, areaHeight, hAlign, vAlign, effectAmount);

    void DrawStringStroke(ReadOnlySpan<char> text, int x, int y, int effectAmount = 1)
    {
    }

    void FillOval(int x, int y, int width, int height)
    {
        BeginPath();
        Ellipse(x + width / 2f, y + height / 2f, width / 2f, height / 2f);
        Fill();
    }
    
    void Ellipse(float cx, float cy, float rx, float ry)
    {
        MoveTo(cx - rx, cy);
        BezierTo(cx - rx, cy + ry * NVG_KAPPA90, cx - rx * NVG_KAPPA90, cy + ry, cx, cy + ry);
        BezierTo(cx + rx * NVG_KAPPA90, cy + ry, cx + rx, cy + ry * NVG_KAPPA90, cx + rx, cy);
        BezierTo(cx + rx, cy - ry * NVG_KAPPA90, cx + rx * NVG_KAPPA90, cy - ry, cx, cy - ry);
        BezierTo(cx - rx * NVG_KAPPA90, cy - ry, cx - rx, cy - ry * NVG_KAPPA90, cx - rx, cy);
        ClosePath();
    }

    // Mimics ctx.ellipse(cx, cy, rx, ry, rotation, a0, a1, ccw).
    // If there's already a current point in the path, connects to the
    // arc's start with a straight line (same as Canvas2D); otherwise moves to it.
    // `hasCurrentPoint` — you track this yourself, same as canvas tracks
    // whether beginPath()/moveTo() has been called.
    void Ellipse(float cx, float cy, float rx, float ry,
        float rotation, float a0, float a1, bool ccw,
        ref bool hasCurrentPoint)
    {
        const float NVG_PI = 3.14159265358979323846f;
        
        const float TWO_PI = NVG_PI * 2.0f;

        // Normalize sweep direction/length the way Canvas2D's ellipse() does.
        float da = a1 - a0;
        if (ccw)
        {
            if (da > 0.0f) da -= TWO_PI;
            if (da < -TWO_PI) da = -TWO_PI;
        } else
        {
            if (da < 0.0f) da += TWO_PI;
            if (da > TWO_PI) da = TWO_PI;
        }

        // Split into <=90 degree segments for a good bezier approximation.
        int nsegs = (int)MathF.Ceiling(MathF.Abs(da) / (NVG_PI * 0.5f));
        if (nsegs < 1) nsegs = 1;
        float delta = da / nsegs;

        float cosr = MathF.Cos(rotation), sinr = MathF.Sin(rotation);

        // local (unit-circle) point -> world space, applying rx/ry scale + rotation
        static void NVG_ELLIPSE_XF(float lx, float ly, out float ox, out float oy, float cx, float cy, float rx, float ry, float cosr, float sinr)
        {
            (ox) = cx + ((lx) * rx * cosr - (ly) * ry * sinr);
            (oy) = cy + ((lx) * rx * sinr + (ly) * ry * cosr);
        }

        NVG_ELLIPSE_XF(MathF.Cos(a0), MathF.Sin(a0), out var x0, out var y0, cx, cy, rx, ry, cosr, sinr);

        if (hasCurrentPoint)
        {
            LineTo(x0, y0);
        }
        else
        {
            MoveTo(x0, y0);
            hasCurrentPoint = true;
        }

        for (int i = 0; i < nsegs; i++) {
            float @as = a0 + i * delta;
            float ae = @as + delta;

            float ux0 = MathF.Cos(@as), uy0 = MathF.Sin(@as);
            float ux1 = MathF.Cos(ae), uy1 = MathF.Sin(ae);

            // unit-circle tangents (derivative of cos/sin)
            float tx0 = -uy0, ty0 = ux0;
            float tx1 = -uy1, ty1 = ux1;

            float kappa = (4.0f / 3.0f) * MathF.Tan(delta / 4.0f);

            float c1x = ux0 + kappa * tx0, c1y = uy0 + kappa * ty0;
            float c2x = ux1 - kappa * tx1, c2y = uy1 - kappa * ty1;

            NVG_ELLIPSE_XF(c1x, c1y, out var p1x, out var p1y, cx, cy, rx, ry, cosr, sinr);
            NVG_ELLIPSE_XF(c2x, c2y, out var p2x, out var p2y, cx, cy, rx, ry, cosr, sinr);
            NVG_ELLIPSE_XF(ux1, uy1, out var p3x, out var p3y, cx, cy, rx, ry, cosr, sinr);

            BezierTo(p1x, p1y, p2x, p2y, p3x, p3y);
        }
    }

    void DrawOval(int x, int y, int width, int height)
    {
        BeginPath();
        Ellipse(x + width / 2f, y + height / 2f, width / 2f, height / 2f);
        Stroke();
    }
    
    void DrawRoundedRect(int x, int y, int width, int height, float radTopLeft, float radTopRight,
        float radBottomRight, float radBottomLeft)
    {
        BeginPath();
        RoundedRectVarying(x, y, width, height, radTopLeft, radTopRight, radBottomRight, radBottomLeft);
        Stroke();
    }

    void DrawRect(int x1, int y1, int width, int height)
    {
        BeginPath();
        Rect(x1, y1, width, height);
        Stroke();
    }

    void DrawImage(IImage image, int x, int y, int width, int height);

    void SetAntialiasing(bool useAntialias)
    {
        // empty
    }

    void SetStrokeWidth(float width = 1f)
    {
        // empty
    }

    public void BeginPath();

    public void MoveTo(float x, float y);

    public void LineTo(float x, float y);

    public void BezierTo(float c1x, float c1y, float c2x, float c2y, float x, float y);

    public void ClosePath();

    public void MarkHole();

    public void Stroke();

    public void Fill();
    
    void Arc(float cx, float cy, float arcRadius, float startAngleDeg, float endAngleDeg, bool clockWise);

    void LineCapButt();

    void SaveState();

    void RestoreState();

    void Scissor(float x, float y, float w, float h);

    void IntersectScissor(float x, float y, float w, float h);

    void ResetScissor();
    
    private static CornerRadius ClampRadius(CornerRadius r, float w, float h)
    {
        return new CornerRadius(Math.Max(0, Math.Min(r.Rx, w / 2)), Math.Max(0, Math.Min(r.Ry, h / 2)));
    }
    
    private void RoundedRectPath(float x, float y, float w, float h, CornerRadius tl, CornerRadius tr, CornerRadius br, CornerRadius bl, bool reverse, bool hole = false)
    {
        bool hasCurrentPoint = false;
        
        tl = ClampRadius(tl, w, h);
        tr = ClampRadius(tr, w, h);
        br = ClampRadius(br, w, h);
        bl = ClampRadius(bl, w, h);

        void Corner(float cx, float cy, float rx, float ry, float a0, float a1, bool ccw, ref bool hasCurrentPoint)
        {
            if (rx <= 0 || ry <= 0) LineTo(cx, cy);
            else Ellipse(cx, cy, rx, ry, 0, a0, a1, ccw, ref hasCurrentPoint);
        }

        if (!reverse) {
            MoveTo(x + tl.Rx, y);
            hasCurrentPoint = true;
            if (hole) MarkHole();
            LineTo(x + w - tr.Rx, y);
            Corner(x + w - tr.Rx, y + tr.Ry, tr.Rx, tr.Ry, -MathF.PI / 2, 0, false, ref hasCurrentPoint);
            LineTo(x + w, y + h - br.Ry);
            Corner(x + w - br.Rx, y + h - br.Ry, br.Rx, br.Ry, 0, MathF.PI / 2, false, ref hasCurrentPoint);
            LineTo(x + bl.Rx, y + h);
            Corner(x + bl.Rx, y + h - bl.Ry, bl.Rx, bl.Ry, MathF.PI / 2, MathF.PI, false, ref hasCurrentPoint);
            LineTo(x, y + tl.Ry);
            Corner(x + tl.Rx, y + tl.Ry, tl.Rx, tl.Ry, MathF.PI, 1.5f * MathF.PI, false, ref hasCurrentPoint);
        } else {
            MoveTo(x + tl.Rx, y);
            hasCurrentPoint = true;
            if (hole) MarkHole();
            Corner(x + tl.Rx, y + tl.Ry, tl.Rx, tl.Ry, 1.5f * MathF.PI, MathF.PI, true, ref hasCurrentPoint);
            LineTo(x, y + h - bl.Ry);
            Corner(x + bl.Rx, y + h - bl.Ry, bl.Rx, bl.Ry, MathF.PI, MathF.PI / 2, true, ref hasCurrentPoint);
            LineTo(x + w - br.Rx, y + h);
            Corner(x + w - br.Rx, y + h - br.Ry, br.Rx, br.Ry, MathF.PI / 2, 0, true, ref hasCurrentPoint);
            LineTo(x + w, y + tr.Ry);
            Corner(x + w - tr.Rx, y + tr.Ry, tr.Rx, tr.Ry, 0, -MathF.PI / 2, true, ref hasCurrentPoint);
        }
        ClosePath();
    }

    void DrawVariableBorderRect(float x, float y, float width, float height, float top, float right, float bottom, float left, CornerRadius tl, CornerRadius tr, CornerRadius br, CornerRadius bl)
    {
        var inner_tl = new CornerRadius(Math.Max(0, tl.Rx - left),  Math.Max(0, tl.Ry - top));
        var inner_tr = new CornerRadius(Math.Max(0, tr.Rx - right), Math.Max(0, tr.Ry - top));
        var inner_br = new CornerRadius(Math.Max(0, br.Rx - right), Math.Max(0, br.Ry - bottom));
        var inner_bl = new CornerRadius(Math.Max(0, bl.Rx - left),  Math.Max(0, bl.Ry - bottom));

        BeginPath();
        RoundedRectPath(x, y, width, height, tl, tr, br, bl, false);
        RoundedRectPath(
            x + left, y + top,
            Math.Max(0, width - left - right),
            Math.Max(0, height - top - bottom),
            inner_tl, inner_tr, inner_br, inner_bl,
            true,
            true
        );
        Fill();
    }

    void FillVariableBorderRect(float x, float y, float width, float height, float top, float right, float bottom, float left, CornerRadius tl, CornerRadius tr, CornerRadius br, CornerRadius bl)
    {
        var inner_tl = new CornerRadius(Math.Max(0, tl.Rx - left),  Math.Max(0, tl.Ry - top));
        var inner_tr = new CornerRadius(Math.Max(0, tr.Rx - right), Math.Max(0, tr.Ry - top));
        var inner_br = new CornerRadius(Math.Max(0, br.Rx - right), Math.Max(0, br.Ry - bottom));
        var inner_bl = new CornerRadius(Math.Max(0, bl.Rx - left),  Math.Max(0, bl.Ry - bottom));

        BeginPath();
        RoundedRectPath(
            x + left, y + top,
            Math.Max(0, width - left - right),
            Math.Max(0, height - top - bottom),
            inner_tl, inner_tr, inner_br, inner_bl,
            false // NOT reversed — we want a normal filled shape, not a hole
        );
        Fill();
    }
}

public readonly record struct CornerRadius(float Rx, float Ry)
{
    public static implicit operator CornerRadius(float radius) => new(radius, radius);
}