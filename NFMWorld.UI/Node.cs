using NFMWorld.PluginSystem;
using NFMWorld.Reactor.Sx;

using Renderer = NFMWorld.Reactor.Sx.Renderer;

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

    /// <summary>
    /// Runs <paramref name="register"/> against the scope this node was mounted into,
    /// adopting whatever effects it creates.
    /// </summary>
    /// <remarks>
    /// A node is normally created inside its parent's builder, so the parent's scope is
    /// still current. A node created as a component method's return value and passed up
    /// has no mounting parent yet; it runs against an unowned scope, which the mount
    /// then adopts.
    /// </remarks>
    private RenderScope? _pendingScope;

    /// <summary>Raised when this node leaves the tree, so flow components can release their rows.</summary>
    internal Action? Disposing { get; set; }

    internal void Bind(Action<RenderScope> register)
    {
        if (Renderer.CurrentScope is { } scope)
        {
            register(scope);
            return;
        }

        _pendingScope ??= new RenderScope();
        register(_pendingScope);
    }

    /// <summary>Adopts a scope this node created before it was mounted.</summary>
    internal void Own(RenderScope scope)
    {
        if (Renderer.CurrentScope is { } current)
        {
            current.Adopt(scope);
            return;
        }

        _pendingScope ??= new RenderScope();
        _pendingScope.Adopt(scope);
    }

    /// <summary>Hands this node's not-yet-mounted effects to its mounting scope.</summary>
    internal void AdoptPendingScope(RenderScope scope)
    {
        if (_pendingScope is null)
            return;

        var pending = _pendingScope;
        _pendingScope = null;
        scope.Adopt(pending);
    }
}
