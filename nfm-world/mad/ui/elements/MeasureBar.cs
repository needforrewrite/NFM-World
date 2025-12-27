using NFMWorld.Util;
using NFMWorld.Mad.UI.yoga;
using NFMWorld.DriverInterface;

namespace NFMWorld.Mad.UI.Elements;

public class MeasureBar : Node
{
    public Color BarColor { get; set; } = new Color(255, 255, 255);
    /// <summary>
    /// 1f = full, 0f = empty
    /// </summary>
    public float BarFillAmount { get; set; } = 0f;
    public IImage BarImage { get; set; } = null!;

    public override void RenderBackground(Vector2 position, Vector2 size)
    {

    }
    
    public override void RenderBorder(Vector2 position, Vector2 size)
    {

    }

    public override void RenderContent(Vector2 position, Vector2 size)
    {
        G.DrawImage(BarImage, (int)position.X, (int)position.Y);
        G.SetColor(BarColor);
        G.FillRect((int)(position.X + 63), (int)(position.Y + 4), (int)(BarFillAmount * 99), 9);
    }
}