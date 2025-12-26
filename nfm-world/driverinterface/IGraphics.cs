#nullable enable

using NFMWorld.Util;

namespace NFMWorld.DriverInterface;

public interface IGraphics
{
    void SetLinearGradient(int x, int y, int width, int height, Color[] colors, float[]? colorPos);
    void SetColor(Color c);
    void FillPolygon(Span<int> x, Span<int> y, int n);
    void DrawPolygon(Span<int> x, Span<int> y, int n);
    void FillRect(int x1, int y1, int width, int height);
    void DrawLine(int x1, int y1, int x2, int y2);
    void SetAlpha(float f);
    void DrawImage(IImage image, int x, int y);
    void SetFont(Font font);
    IFontMetrics GetFontMetrics();
    void DrawString(string text, int x, int y);
    void DrawStringAligned(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign);
    void DrawStringStrokeAligned(string text, int areaWidth, int areaHeight, TextHorizontalAlignment hAlign, TextVerticalAlignment vAlign, int effectAmount = 1);
    void DrawStringStroke(string text, int x, int y, int effectAmount = 1)
    {
    }
    void FillOval(int p0, int p1, int p2, int p3);
    void FillRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei);
    void DrawRoundRect(int x, int y, int wid, int hei, int arcWid, int arcHei);
    void DrawRect(int x1, int y1, int width, int height);
    void DrawImage(IImage image, int x, int y, int width, int height);

    void SetAntialiasing(bool useAntialias)
    {
        // empty
    }
}