namespace NFMWorld.Reactor.Sx;

/// <summary>
/// Mounts a component tree and owns the scope its effects live in.
/// </summary>
/// <remarks>
/// There is no diffing: a "component" is a method that builds nodes, and it runs once.
/// Everything after that is effects created during the mount, which is why the only thing
/// this class does is supply the scope and hand it to the root's owner.
/// </remarks>
public static class Renderer
{
    private static RenderScope? _current;

    /// <summary>
    /// The scope a node being built right now belongs to, or null outside a mount.
    /// </summary>
    internal static RenderScope? CurrentScope => _current;

    /// <summary>Runs <paramref name="build"/> with <paramref name="scope"/> as the current mount scope.</summary>
    internal static T RunInScope<T>(RenderScope scope, Func<T> build)
    {
        var previous = _current;
        _current = scope;

        try
        {
            return build();
        }
        finally
        {
            _current = previous;
        }
    }

    /// <summary>
    /// Mounts <paramref name="build"/> and returns the root node. The caller disposes the
    /// returned <see cref="MountedUi"/> to tear the tree down.
    /// </summary>
    public static MountedUi Mount(Func<Node> build)
    {
        var scope = new RenderScope();

        var root = RunInScope(scope, () =>
        {
            var node = build();

            // Anything the root created before it had a mounting scope belongs to this one.
            node.AdoptPendingScope(scope);
            return node;
        });

        return new MountedUi(root, scope);
    }

    /// <summary>Runs every effect a write since the last call invalidated.</summary>
    public static void Flush() => UiScheduler.Instance.Flush();
}

/// <summary>A mounted tree and the effects it owns.</summary>
public sealed class MountedUi(Node root, RenderScope scope) : IDisposable
{
    public Node Root { get; } = root;

    public void Dispose() => scope.Dispose();
}
