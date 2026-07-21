using MemoryPack;

namespace NFMWorld.Server.SharedMemory;

/// <summary>
/// Top-level message envelope for Controller↔Worker RPC communication.
/// The <see cref="Payload"/> contains a MemoryPack-serialized inner message
/// whose type depends on <see cref="Type"/>.
/// </summary>
[MemoryPackable]
public partial struct RpcMessage
{
    /// <summary>Message type discriminator.</summary>
    [MemoryPackOrder(0)]
    public RpcMessageType Type { get; set; }

    /// <summary>MemoryPack-serialized inner message payload.</summary>
    [MemoryPackOrder(1)]
    public byte[]? Payload { get; set; }

    public static RpcMessage Create<T>(RpcMessageType type, T inner)
    {
        return new RpcMessage
        {
            Type = type,
            Payload = MemoryPackSerializer.Serialize(inner)
        };
    }

    public T Deserialize<T>()
    {
        return MemoryPackSerializer.Deserialize<T>(Payload)!;
    }
}

/// <summary>
/// Message types for Controller↔Worker RPC communication.
/// </summary>
public enum RpcMessageType : byte
{
    /// <summary>Heartbeat / connectivity check. Response is a Ping with the same payload.</summary>
    Ping = 0,

    /// <summary>
    /// Controller → Worker: batched player inputs for one simulation tick.
    /// Payload: <see cref="PlayerInputBatch"/>.
    /// Response: <see cref="RpcMessageType.GameState"/> with current game state.
    /// </summary>
    PlayerInputs = 1,

    /// <summary>
    /// Worker → Controller (response): game state after processing inputs.
    /// Payload: <see cref="GameStateSnapshot"/>.
    /// </summary>
    GameState = 2,

    /// <summary>
    /// Worker → Controller (or response to PlayerInputs when race is finished): race is complete.
    /// Payload: <see cref="RaceCompleteReport"/>.
    /// </summary>
    RaceComplete = 3,

    /// <summary>
    /// Controller → Worker: terminate the simulation gracefully.
    /// Response: Shutdown acknowledgement.
    /// </summary>
    Shutdown = 4,

    /// <summary>An error occurred processing the request.</summary>
    Error = 255,
}
