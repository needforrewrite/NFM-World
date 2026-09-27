using System.Numerics;
using NFMWorld.ClayDom.Events;
using NFMWorld.Lua;
using NFMWorldLibrary.Util;

namespace NFMWorld.Reactor;

[LuaVisible]
public abstract partial class Node
{
    #region Parent/child tree

    [LuaName]
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
    [LuaName]
    public abstract ReadOnlyLuaArray<Node> VisualChildren { get; }

    public Node()
    {
    }

    #endregion
}
