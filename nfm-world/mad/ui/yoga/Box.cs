using System.Diagnostics;
using Avalonia.Metadata;
using Maxine.Extensions;

namespace NFMWorld.UI.Yoga;

/// <summary>
/// Represents a container node that can hold multiple child nodes.
/// </summary>
[DebuggerDisplay("{DebugToString()}")]
public class Box : Node
{
    [Content]
    public NodeChildCollection Children { get; }

    public Box()
    {
        Children = new NodeChildCollection(this);
    }
    
    public new string DebugToString()
    {
        using var sb = new ValueStringBuilder(stackalloc char[ValueStringBuilder.StackallocCharBufferSizeLimit]);
        sb.Append($"Node(Name={Name}, LayoutX={LayoutX}, LayoutY={LayoutY}, LayoutWidth={LayoutWidth}, LayoutHeight={LayoutHeight})");
        foreach (var child in Children)
        {
            sb.AppendLine();
            sb.Append('{');
            sb.Append(child.DebugToString().Replace("\n", "\n  "));
            sb.Append('}');
        }
        return sb.ToString();
    }

    protected internal override void RescaleRecursive()
    {
        if (Rescale())
        {
            OnScaleChanged();
            foreach (var child in Children)
            {
                child.RescaleRecursive();
            }
        }
    }

    protected internal override void Update()
    {
        base.Update();
        foreach (var child in Children)
        {
            child.Update();
        }
    }

    protected internal override void RenderRecursive(Vector2 root, float rootOpacity = 1)
    {
        _root = root;
        if (Display != YgDisplay.None && Visibility == Visibility.Visible && Opacity > 0f)
        {
            var ownOpacity = rootOpacity * Opacity;
            G.SetAlpha(ownOpacity);
            Render();
            foreach (var child in Children)
            {
                child.RenderRecursive(root + new Vector2(LayoutX, LayoutY), ownOpacity); // todo should this be LayoutContentPosition
            }
            G.SetAlpha(1f);
        }
    }
}