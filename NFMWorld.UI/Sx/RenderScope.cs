using SignalsDotnet;

namespace NFMWorld.Reactor.Sx;

/// <summary>
/// What a mounted component owns: every effect and disposable a mount created, released
/// together when the subtree goes away.
/// </summary>
public sealed class RenderScope : IDisposable
{
    private readonly List<IDisposable> _resources = [];
    private RenderScope? _owner;
    private bool _disposed;

    /// <summary>Whether this scope has been disposed; mounting into one is a bug.</summary>
    public bool IsDisposed => _disposed;

    /// <summary>Adopts a resource, disposing it with this scope.</summary>
    public T Own<T>(T resource) where T : IDisposable
    {
        if (_disposed)
            throw new ObjectDisposedException(nameof(RenderScope));

        _resources.Add(resource);
        return resource;
    }

    /// <summary>
    /// Adopts a scope created before its node was mounted, disposing it with this one.
    /// </summary>
    /// <remarks>Independently disposed children detach so remounts do not accumulate dead scopes.</remarks>
    public void Adopt(RenderScope scope)
    {
        if (ReferenceEquals(scope, this))
            return;

        if (_disposed)
        {
            scope.Dispose();
            return;
        }

        if (scope._disposed || ReferenceEquals(scope._owner, this))
            return;

        scope.Detach();
        scope._owner = this;
        _resources.Add(scope);
    }

    /// <summary>
    /// Runs <paramref name="compute"/> now and again whenever a signal it read changes,
    /// writing its result through <paramref name="apply"/>.
    /// </summary>
    /// <remarks>
    /// The comparison against the previous value is what keeps a per-frame push cheap:
    /// a HUD write that does not change a given leaf does not touch that node at all.
    ///
    /// The effect's initial run is shielded from the enclosing computation, so a child's
    /// signal reads cannot subscribe a parent slot and cause it to rebuild the subtree.
    /// </remarks>
    public Effect Bind<T>(Func<T> compute, Action<T> apply)
    {
        var hasValue = false;
        var current = default(T)!;

        using var _ = Signal.UntrackedScope();
        return Own(
            new Effect(() =>
                {
                    var next = compute();
                    if (hasValue && EqualityComparer<T>.Default.Equals(next, current))
                        return;

                    hasValue = true;
                    current = next;
                    apply(next);
                }
            )
        );
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Remove each resource before disposal: child scopes or callbacks may detach others.
        while (_resources.Count > 0)
        {
            var index = _resources.Count - 1;
            var resource = _resources[index];
            _resources.RemoveAt(index);
            resource.Dispose();
        }

        Detach();
    }

    private void Detach()
    {
        if (_owner is not { } owner)
            return;

        _owner = null;
        owner._resources.Remove(this);
    }
}
