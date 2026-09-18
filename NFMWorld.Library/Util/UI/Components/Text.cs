using System.Diagnostics;
using System.Numerics;
using Microsoft.UI.Reactor.Layout;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;
using ObservableCollections;

namespace NFMWorld.Reactor;

public enum BreakType
{
    None,
    Word,
    Character
}

public enum OverflowBehavior
{
    None,
    Stretch,
    ContinueVertically,
    ContinueHorizontally
}

public interface IReceivesTextInvalidation
{
    public void InvalidateText();
}

/// <summary>
/// A text element. Inside of Text, only other Text and <see cref="TextNode"/> can be nested. Nested <see cref="Text"/>
/// and <see cref="TextNode"/> inherit the styles from their parent elements.
/// </summary>
public class Text : Component, IRichTextContainer, IReceivesTextInvalidation
{
    protected bool Invalidated { get; private set; } = true;
    public ComplexTextMetrics.RichTextContainer? LaidOutComplexText;

    public override bool DebugIsContentfulNode => true;
    
    public ComponentChildCollection Children { get; }

    IEnumerable<IRichTextElement> IRichTextContainer.Children => Children.OfType<IRichTextElement>();

    public override ReadOnlyLuaArray<Node> VisualChildren { get; }

    // ── Visual children API ────────────────────────────────────────────
    public override bool CanHaveChildren => true;
    public override void AddChild(Node child) => Children.Add(child);
    public override void InsertAt(int index, Node child) => Children.Insert(index, child);
    public override void RemoveAt(int index) => Children.RemoveAt(index);
    
    Color? IRichTextElement.Background => Styles.BackgroundColor;
    Color? IRichTextElement.Foreground => TextStyles.ForegroundColor;
    Color? IRichTextElement.Stroke => TextStyles.StrokeColor;
    FontFamily? IRichTextElement.FontFamily => TextStyles.FontFamily;
    float? IRichTextElement.FontSize => TextStyles.FontSize;
    FontStyle? IRichTextElement.FontStyle => TextStyles.FontStyle;

    public Text()
    {
        Children = new ComponentChildCollection(this);
        VisualChildren = new ReadOnlyLuaArray<Node>(Children);

        Children.CollectionChanged += OnChildrenChanged;

        NodeInternal.MeasureFunction += Measure;
    }

    private void OnChildrenChanged(in NotifyCollectionChangedEventArgs<Node> e)
    {
        InvalidateText();
    }

    public TextStyles TextStyles
    {
        get;
        set
        {
            if (field != value)
            {
                field = value;
                InvalidateText();
            }
        }
    } = new();

    public string? TextContent
    {
        set
        {
            if (Children is [TextNode textNode])
            {
                textNode.Text = value;
            }
            else
            {
                while (Children.Count > 0)
                {
                    RemoveAt(0);
                }
                AddChild(new TextNode { Text = value });
            }
        }
    }

    protected override void OnStylesChanged()
    {
        base.OnStylesChanged();
        InvalidateText();
    }

    private YogaSize Measure(YogaNode node, float availableWidth, YogaMeasureMode widthMode, float availableHeight, YogaMeasureMode heightMode)
    {
        RelayoutText(new Vector2(availableWidth, availableHeight), out var width, out var height);
        return new YogaSize(width, height);
    }

    [ClientOnly]
    protected void RelayoutText(Vector2 size, out float width, out float height)
    {
        var flattened = ComplexTextMetrics.FlattenText(Children.OfType<IRichTextElement>());
        
        var font = new Font(TextStyles.FontFamily, TextStyles.FontStyle, TextStyles.FontSize);
        if (TextStyles.OverflowBehavior is not OverflowBehavior.Stretch and not OverflowBehavior.None && TextStyles.BreakType is not BreakType.None)
        {
            flattened = ComplexTextMetrics.LayoutText(font, flattened, new Vector2(size.X, size.Y), TextStyles.BreakType, TextStyles.OverflowBehavior);
        }
        var measurements = ComplexTextMetrics.MeasureRichText(flattened, font);

        width = measurements.Size.X;
        height = measurements.Size.Y;
        
        LaidOutComplexText = measurements;

        Invalidated = false;
    }

    protected virtual void OnInvalidated()
    {
    }

    [ClientOnly]
    protected override void RenderBackground(LuaVector2 position, LuaVector2 size)
    {
        base.RenderBackground(position, size);
    }

    protected override bool OnPostLayout()
    {
        base.OnPostLayout();
        
        if (HasNewLayout && TextStyles.OverflowBehavior is not Reactor.OverflowBehavior.Stretch and not Reactor.OverflowBehavior.None && TextStyles.BreakType is not Reactor.BreakType.None)
        {
            Invalidated = true;
            HasNewLayout = false;
        }
        
        if (Invalidated)
        {
            OnInvalidated();
            RelayoutText(LayoutContentSize, out _, out _);
            return true;
        }

        return false;
    }

    [ClientOnly]
    protected override void RenderContent(LuaVector2 position, LuaVector2 size)
    {
        base.RenderContent(position, size);

        Debug.Assert(LaidOutComplexText != null, "Complex text layout should have been calculated in RelayoutText method.");

        if (LaidOutComplexText.Value.Elements.Count == 0)
        {
            return;
        }

        var basePosition = (Vector2)position;
        ComplexTextMetrics.AlignBounds(LaidOutComplexText.Value.Size, (int)size.X, (int)size.Y, TextStyles.HorizontalAlignment, TextStyles.VerticalAlignment, ref basePosition.X, ref basePosition.Y);

        foreach (var element in LaidOutComplexText.Value.Elements)
        {
            G.SetFont(element.Font with { Size = (element.FontSize ?? TextStyles.FontSize) * G.Scale });
            if ((element.Background ?? Styles.BackgroundColor) is { } background)
            {
                G.SetColor(background);
                G.FillRect((int)basePosition.X, (int)basePosition.Y, (int)element.Size.X, (int)element.Size.Y);
            }

            float yOff = 0;
            if (TextStyles.VerticalAlignment == TextVerticalAlignment.Center)
            {
                yOff = (G.GetFontMetrics().LineHeight / 2.0f);
            }
            else if (TextStyles.VerticalAlignment == TextVerticalAlignment.Top)
            {
                yOff = G.GetFontMetrics().LineHeight;
            }

            int x = (int)(basePosition.X + (element.Position.X * G.Scale));
            int y = (int)(basePosition.Y + (element.Position.Y * G.Scale) + yOff);

            if ((element.Stroke ?? TextStyles.StrokeColor) is { } stroke)
            {
                G.SetColor(stroke);
                G.DrawStringStroke(element.Text, x, y);
            }
            
            G.SetColor(element.Foreground ?? TextStyles.ForegroundColor);
            G.DrawString(element.Text, x, y);
        }
    }

    public void InvalidateText()
    {
        Invalidated = true;
        NodeInternal.MarkDirty();
    }
}