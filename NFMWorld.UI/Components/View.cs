using System.ComponentModel;
using System.Diagnostics;
using System.Text;
using NFMWorld.Lua;
using NFMWorldLibrary.Util;

namespace NFMWorld.Reactor;

/// <summary>
/// Represents a container node that can hold multiple child nodes and arrange them according to the Flexbox layout
/// algorithm.
/// </summary>
[DebuggerDisplay("{DebugToString()}"), LuaVisible]
public partial class View : Component
{
    public ComponentChildCollection Children { get; }

    public override ReadOnlyLuaArray<Node> VisualChildren { get; }

    // ── Visual children API ────────────────────────────────────────────
    public override bool CanHaveChildren => true;
    public override void AddChild(Node child) => Children.Add(child);
    public override void InsertAt(int index, Node child) => Children.Insert(index, child);
    public override void RemoveAt(int index) => Children.RemoveAt(index);

    public View()
    {
        Children = new ComponentChildCollection(this);
        VisualChildren = new ReadOnlyLuaArray<Node>(Children);
    }

    [EditorBrowsable(EditorBrowsableState.Never)]
    public override string DebugToString()
    {
        var sb = new StringBuilder();
        sb.Append($"View(Name={Name}, LayoutX={LayoutX}, LayoutY={LayoutY}, LayoutWidth={LayoutWidth}, LayoutHeight={LayoutHeight})");
        foreach (var child in Children)
        {
            sb.AppendLine();
            sb.Append('{');
            sb.Append((child is Component node ? node.DebugToString() : child.ToString() ?? "").Replace("\n", "\n  "));
            sb.Append('}');
        }
        return sb.ToString();
    }
}