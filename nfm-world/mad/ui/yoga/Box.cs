using NFMWorld.Util;

namespace NFMWorld.Mad.UI.yoga;

public class Box : Node
{
    public Color BorderColor { get; set; } = new Color(0, 0, 0, 255);
    public Color BackgroundColor { get; set; } = new Color(150, 255, 150, 255);

    public override void RenderBackground(Vector2 position, Vector2 size)
    {
        G.SetColor(BackgroundColor);
        G.FillRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }
    
    public override void RenderBorder(Vector2 position, Vector2 size)
    {
        G.SetColor(BorderColor);
        G.DrawRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }

    public override void RenderContent(Vector2 position, Vector2 size)
    {
        G.SetColor(BackgroundColor);
        G.FillRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }
}