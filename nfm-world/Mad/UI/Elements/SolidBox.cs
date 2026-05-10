using WorldXaml.UI.Yoga;

namespace NFMWorld.UI;

/// <summary>
/// Represents a box element with solid colors for border, background, and content.
/// </summary>
public class SolidBox : Box
{
    public Color BorderColor { get; set; } = new Color(0, 0, 0, 255);
    public Color BackgroundColor { get; set; } = new Color(150, 255, 150, 255);
    public Color ContentColor { get; set; } = new Color(0, 0, 0, 255);

    protected override void RenderBackground(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        G.SetColor(BackgroundColor);
        G.FillRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }
    
    protected override void RenderBorder(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        G.SetColor(BorderColor);
        G.DrawRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }

    protected override void RenderContent(System.Numerics.Vector2 position, System.Numerics.Vector2 size)
    {
        G.SetColor(ContentColor);
        G.FillRect((int) position.X, (int) position.Y, (int) size.X, (int) size.Y);
    }
}