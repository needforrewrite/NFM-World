using System.Diagnostics.CodeAnalysis;
using Avalonia.Metadata;
using NFMWorld.DriverInterface;
using NFMWorld.Util;

namespace NFMWorld.UI.Yoga;

public class TextRun : Node
{
    private IFontMetrics? _fontMetrics;
    public Color Color { get; set; } = new Color(255, 255, 255);
    public Color? StrokeColor { get; set; } = null;
    public Font Font
    {
        get;
        set
        {
            field = value;
            SetFontMetrics();
            RelayoutText();
        }
    } = new Font(FontFamily.DroidSans, FontStyle.Plain, 18);

    [Content]
    public string? Text
    {
        get;
        set
        {
            field = value;
            RelayoutText();
        }
    }

    [MemberNotNull(nameof(_fontMetrics))]
    private void SetFontMetrics()
    {
        G.SetFont(Font with { Size = Font.Size }); // Does not use scale here
        _fontMetrics = G.GetFontMetrics();
    }

    private void RelayoutText()
    {
        if (_fontMetrics == null)
        {
            SetFontMetrics();
        }
        Width = _fontMetrics!.StringWidth(Text ?? string.Empty);
        Height = _fontMetrics!.Height(Text ?? string.Empty);
    }

    public TextHorizontalAlignment HorizontalAlignment { get; set; } = TextHorizontalAlignment.Left;
    public TextVerticalAlignment VerticalAlignment { get; set; } = TextVerticalAlignment.Top;

    protected override void RenderContent(Vector2 position, Vector2 size)
    {
        base.RenderContent(position, size);

        if (string.IsNullOrEmpty(Text))
        {
            return;
        }
        
        G.SetFont(Font with { Size = Font.Size * G.Scale });
        if (StrokeColor != null)
        {
            G.SetColor((Color)StrokeColor);
            G.DrawStringStrokeAligned(Text, (int)position.X, (int)position.Y, (int)size.X, (int)size.Y, HorizontalAlignment, VerticalAlignment);
        }

        G.SetColor(Color);
        G.DrawStringAligned(Text, (int)position.X, (int)position.Y, (int)size.X, (int)size.Y, HorizontalAlignment, VerticalAlignment);
    }

    public new Action<TextRun> Ref
    {
        set => value(this);
    }
}