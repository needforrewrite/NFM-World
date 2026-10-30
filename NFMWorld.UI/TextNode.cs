using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.PluginSystem;

namespace NFMWorld.Reactor;

/// <summary>
/// A node containing text. This is a leaf node; it contains no children.
/// </summary>
[PluginVisible]
public partial class TextNode : Node, IReceivesTextInvalidation, IRichTextLeaf
{
    [PluginVisible]
    public override IReadOnlyList<Node> VisualChildren => Array.Empty<Node>();

    [PluginVisible]
    public string? Text
    {
        get;
        set
        {
            field = value;
            InvalidateText();
        }
    }

    public void InvalidateText()
    {
        if (VisualParent is IReceivesTextInvalidation invalidation)
        {
            invalidation.InvalidateText();
        }
    }

    Color? IRichTextElement.Background => null;
    Color? IRichTextElement.Foreground => null;
    Color? IRichTextElement.Stroke => null;
    FontFamily? IRichTextElement.FontFamily => null;
    float? IRichTextElement.FontSize => null;
    FontStyle? IRichTextElement.FontStyle => null;
    public IReadOnlyList<IRichTextElement> Children { get; }
}