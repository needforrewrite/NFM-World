using NFMWorld.DriverInterface;
using NFMWorld.Util;

namespace NFMWorld;

public static class G
{
    private static IGraphics Graphics => IBackend.Backend.Graphics;

    public static float Scale
    {
        get => IBackend.Backend.Scale;
        set => IBackend.Backend.Scale = value;
    }

    public static void SetColor(Color c) => Graphics.SetColor(c);

    public static void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos) => Graphics.SetLinearGradient(x, y, width, height, colors, colorPos);

    public static void FillPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n) => Graphics.FillPolygon(x, y, n);

    public static void DrawPolygon(ReadOnlySpan<int> x, ReadOnlySpan<int> y, int n) => Graphics.DrawPolygon(x, y, n);

    public static void FillRect(int x1, int y1, int width, int height) => Graphics.FillRect(x1, y1, width, height);

    public static void DrawLine(int x1, int y1, int x2, int y2) => Graphics.DrawLine(x1, y1, x2, y2);

    public static void SetAlpha(float f) => Graphics.SetAlpha(f);

    public static void DrawImage(IImage? image, int x, int y)
    {
        if (image == null)
        {
            return;
        }

        Graphics.DrawImage(image, x, y);
    }

    public static void SetFont(Font p0) => Graphics.SetFont(p0);

    public static IFontMetrics GetFontMetrics() => Graphics.GetFontMetrics();

    public static void DrawString(string text, int x, int y) => Graphics.DrawString(text, x, y);
    public static void DrawStringAligned(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top) 
        => Graphics.DrawStringAligned(text, 0, 0, areaWidth, areaHeight, hAlign, vAlign);
    public static void DrawStringAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top) 
        => Graphics.DrawStringAligned(text, x, y, areaWidth, areaHeight, hAlign, vAlign);
    
    public static void DrawStringStrokeAligned(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        => Graphics.DrawStringStrokeAligned(text, 0, 0, areaWidth, areaHeight, hAlign, vAlign, effectAmount);

    public static void DrawStringStrokeAligned(string text, int x, int y, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign = TextHorizontalAlignment.Left, TextVerticalAlignment vAlign = TextVerticalAlignment.Top, int effectAmount = 1)
        => Graphics.DrawStringStrokeAligned(text, x, y, areaWidth, areaHeight, hAlign, vAlign, effectAmount);

    public static void DrawStringStroke(string text, int x, int y, int effectAmount = 1) => Graphics.DrawStringStroke(text, x, y, effectAmount);

    public static void FillOval(int p0, int p1, int p2, int p3) => Graphics.FillOval(p0, p1, p2, p3);

    public static void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei) => Graphics.FillRoundRect(x, y, wid, hei, arcWid, arcHei);

    public static void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei) => Graphics.DrawRoundRect(x, y, wid, hei, arcWid, arcHei);

    public static void DrawRect(int x1, int y1, int width, int height) => Graphics.DrawRect(x1, y1, width, height);

    public static void DrawImage(IImage? bggo, int p1, int i429, int p3, int i, object o)
    {
        if (bggo == null)
        {
            return;
        }

        Graphics.DrawImage(bggo, p1, i429, p3, i);
    }

    public static void DrawImage(IImage? image, int x, int y, int wid, int hei)
    {
        if (image == null)
        {
            return;
        }

        Graphics.DrawImage(image, x, y, wid, hei);
    }

    public static void SetAntialiasing(bool useAntialias) => Graphics.SetAntialiasing(useAntialias);
    public static string LayoutText(string text, float width, float height, BreakType breakType = BreakType.Word, OverflowBehavior overflowBehavior = OverflowBehavior.ContinueHorizontally)
    {
        return Graphics.LayoutText(text, width, height, breakType);
    }

    public static Vector2 Viewport => IBackend.Backend.Viewport;
}