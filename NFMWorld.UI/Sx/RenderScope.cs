using SignalsDotnet;

namespace NFMWorld.Reactor.Sx;

/// <summary>
/// What a mounted component owns: every effect and disposable a mount created, released
/// together when the subtree goes away.
/// </summary>
/// <remarks>
/// The Luau renderer needed an isolated root per <c>Switch</c> branch because a disposed
/// effect could not be detached cleanly. Here disposal is just <see cref="Dispose"/>, so a
/// scope is enough and branch switching needs no special case.
/// </remarks>
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
    /// <remarks>
    /// A child scope is owned rather than merely appended, so that when it is disposed on
    /// its own - which is what a <c>Show</c>/<c>Switch</c> remount does - it detaches from
    /// here. Otherwise a subtree toggled repeatedly would leave one dead scope behind in
    /// this list per toggle.
    /// </remarks>
    public void Adopt(RenderScope scope)
    {
        if (ReferenceEquals(scope, this))
            return;

        if (_disposed)
        {
            scope.Dispose();
            return;
        }

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
    /// </remarks>
    public Effect Bind<T>(Func<T> compute, Action<T> apply)
    {
        var initial = compute();
        apply(initial);

        var current = initial;
        return Own(new Effect(() =>
        {
            var next = compute();
            if (EqualityComparer<T>.Default.Equals(next, current))
                return;

            current = next;
            apply(next);
        }));
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        // Reverse order: a parent's effects may read signals a child registered, and
        // children are mounted after their parent. Iterated by index, not foreach, because
        // a child scope's Dispose detaches it from this list.
        for (var i = _resources.Count - 1; i >= 0; i--)
            _resources[i].Dispose();

        _resources.Clear();

        // Dispose() detaches from the owner, so this is a no-op if the owner is what is
        // disposing us, and drops us from it if a remount disposed us individually.
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
