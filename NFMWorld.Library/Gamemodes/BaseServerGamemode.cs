namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Base class for server-side gamemodes with default no-op implementations.
/// Override only the methods your gamemode needs.
/// </summary>
public abstract class BaseServerGamemode : IServerGamemode
{
    /// <inheritdoc />
    public abstract string GamemodeId { get; }

    /// <inheritdoc />
    public virtual void Begin(IServerGamemodeContext context) { }

    /// <inheritdoc />
    public virtual void StartRace() { }

    /// <inheritdoc />
    public virtual void End() { }

    /// <inheritdoc />
    public virtual void GameTick() { }

    /// <inheritdoc />
    public virtual void OnClientEvent(Guid clientId, ReadOnlySpan<byte> payload) { }

    /// <inheritdoc />
    public virtual GameStateSnapshot? GetStateSnapshot() => null;

    /// <inheritdoc />
    public virtual void SetEventBroadcaster(Action<ReadOnlyMemory<byte>> broadcast)
    {
        _broadcast = broadcast;
    }

    /// <summary>Broadcast an event to all connected clients.</summary>
    protected void BroadcastEvent(ReadOnlyMemory<byte> payload)
        => _broadcast?.Invoke(payload);

    private Action<ReadOnlyMemory<byte>>? _broadcast;
}
