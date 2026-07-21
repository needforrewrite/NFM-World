using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

/// <summary>
/// Manages a stack of phases with deferred disposal.
///
/// - <see cref="SetRoot"/> initializes the stack with a root phase that is never popped.
/// - <see cref="Push"/> pushes a new phase onto the stack (exiting the current one).
/// - <see cref="Pop"/> removes the top phase and returns to the previous one.
/// - <see cref="Replace"/> atomically swaps the top phase (for debug commands / editor entry).
///
/// Popped phases are queued for deferred disposal and actually disposed
/// when <see cref="FlushDisposals"/> is called (typically at end-of-frame).
/// This avoids disposal-during-event-handler and disposal-during-CEF-callback bugs.
/// </summary>
public class PhaseManager
{
    private readonly List<BasePhase> _stack = [];
    private readonly List<BasePhase> _disposalQueue = [];

    /// <summary>
    /// The currently active phase (top of stack).
    /// </summary>
    public BasePhase Current =>
        _stack.Count > 0 ? _stack[^1] : throw new InvalidOperationException("PhaseManager has no active phase.");

    /// <summary>
    /// The root phase (bottom of stack), or null if none has been set.
    /// </summary>
    public BasePhase? Root => _stack.Count > 0 ? _stack[0] : null;

    /// <summary>
    /// Number of phases currently on the stack.
    /// </summary>
    public int Count => _stack.Count;

    /// <summary>
    /// Read-only view of the phase stack for debugging.
    /// </summary>
    public IReadOnlyList<BasePhase> Stack => _stack;

    /// <summary>
    /// Initializes the stack with a root phase. Clears any existing stack first.
    /// The root phase is never disposed during normal navigation.
    /// </summary>
    public void SetRoot(BasePhase root)
    {
        // Dispose any existing stack before reinitializing
        foreach (var phase in _stack)
        {
            phase.Exit();
            QueueDisposal(phase);
        }
        _stack.Clear();
        FlushDisposals();

        _stack.Add(root);
        root.Enter();
        Logging.Info($"PhaseManager: root set to {root.GetType().Name}");
    }

    /// <summary>
    /// Pushes a new phase onto the top of the stack.
    /// Calls <see cref="BasePhase.Exit"/> on the current top (if any)
    /// and <see cref="BasePhase.Enter"/> on the new phase.
    /// The old top phase is NOT disposed — it stays alive on the stack.
    /// </summary>
    public void Push(BasePhase phase)
    {
        if (_stack.Count > 0)
        {
            _stack[^1].Exit();
        }

        _stack.Add(phase);
        phase.Enter();
        Logging.Info($"PhaseManager: pushed {phase.GetType().Name} (depth {_stack.Count})");
    }

    /// <summary>
    /// Pops the top phase from the stack and queues it for deferred disposal.
    /// Calls <see cref="BasePhase.Enter"/> on the new top phase.
    /// Throws <see cref="InvalidOperationException"/> if attempting to pop the root phase.
    /// </summary>
    /// <returns>The popped phase.</returns>
    public BasePhase Pop()
    {
        if (_stack.Count <= 1)
        {
            throw new InvalidOperationException(
                "Cannot pop the root phase. Use Replace or PopToRoot instead, or push a new phase first.");
        }

        var popped = _stack[^1];
        popped.Exit();
        _stack.RemoveAt(_stack.Count - 1);

        QueueDisposal(popped);

        // Enter the new top (which was previously on the stack but exited)
        _stack[^1].Enter();
        Logging.Info($"PhaseManager: popped {popped.GetType().Name} (depth {_stack.Count})");
        return popped;
    }

    /// <summary>
    /// Pops all phases above the root, queuing each for deferred disposal.
    /// The root phase is re-entered.
    /// </summary>
    public void PopToRoot()
    {
        while (_stack.Count > 1)
        {
            var popped = _stack[^1];
            popped.Exit();
            _stack.RemoveAt(_stack.Count - 1);
            QueueDisposal(popped);
        }

        _stack[0].Enter();
        Logging.Info($"PhaseManager: popped to root (depth {_stack.Count})");
    }

    /// <summary>
    /// Atomically replaces the current top phase.
    /// Equivalent to Pop + Push but avoids re-entering the underlying phase.
    /// Used for debug commands, editor entry points, and backward-compatible SetPhase.
    /// </summary>
    public void Replace(BasePhase phase)
    {
        if (_stack.Count > 0)
        {
            var old = _stack[^1];
            old.Exit();
            _stack.RemoveAt(_stack.Count - 1);
            QueueDisposal(old);
        }

        _stack.Add(phase);
        phase.Enter();
        Logging.Info($"PhaseManager: replaced with {phase.GetType().Name} (depth {_stack.Count})");
    }

    /// <summary>
    /// Calls <see cref="BasePhase.Dispose"/> on all queued phases and clears the queue.
    /// Must be called at end-of-frame to finalize disposal of popped/replaced phases.
    /// </summary>
    /// <returns>The number of phases disposed.</returns>
    public int FlushDisposals()
    {
        var count = _disposalQueue.Count;
        if (count == 0)
            return 0;

        foreach (var phase in _disposalQueue)
        {
            phase.Dispose();
            Logging.Debug($"PhaseManager: disposed {phase.GetType().Name}");
        }

        _disposalQueue.Clear();
        return count;
    }

    /// <summary>
    /// Flushes disposals and disposes all phases remaining on the stack.
    /// For final shutdown only.
    /// </summary>
    public void Shutdown()
    {
        // Flush any queued disposals first
        FlushDisposals();

        // Pop and dispose everything
        while (_stack.Count > 0)
        {
            var phase = _stack[^1];
            phase.Exit();
            _stack.RemoveAt(_stack.Count - 1);
            phase.Dispose();
        }

        Logging.Info("PhaseManager: shutdown complete");
    }

    private void QueueDisposal(BasePhase phase)
    {
        // Guard against double-disposal: if a phase is somehow queued twice,
        // only queue it once.
        if (!_disposalQueue.Contains(phase))
        {
            _disposalQueue.Add(phase);
        }
    }
}
