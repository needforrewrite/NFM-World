using NFMWorld.PluginSystem;

namespace NFMWorld.Reactor;

[PluginVisible]
public abstract partial class Node
{
    #region Parent/child tree

    [PluginVisible]
    public Node? VisualParent
    {
        get;
        set;
    }

    /// <summary>
    /// True once this node (or an ancestor) has been removed from the visual tree.
    /// Hover tracking uses this to drop stale references without firing events.
    /// </summary>
    internal bool IsDisposed { get; set; }

    /// <summary>
    /// Gets the visual children of this visual element.
    /// </summary>
    [PluginVisible]
    public abstract IReadOnlyList<Node> VisualChildren { get; }

    public Node()
    {
    }

    #endregion
}
