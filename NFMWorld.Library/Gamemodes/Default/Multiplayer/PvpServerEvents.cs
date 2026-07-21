using MemoryPack;
using Microsoft.Xna.Framework;
using NFMWorldLibrary.Multiplayer.Packets.C2S;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Union of all server-bound events for the PvP racing gamemode.
/// Each client gamemode serializes one of these into the
/// <see cref="C2S_ClientEvent.Payload"/> byte array.
/// </summary>
[MemoryPackable]
[MemoryPackUnion(0, typeof(PvpCheckpointEvent))]
public partial interface IPvpServerEvent;

/// <summary>
/// Sent by the client when the local player crosses a checkpoint.
/// </summary>
[MemoryPackable]
public partial class PvpCheckpointEvent : IPvpServerEvent
{
    /// <summary>Index of the checkpoint that was crossed.</summary>
    [MemoryPackOrder(0)]
    public int CheckpointIndex;

    /// <summary>Current lap number (0-based) at the time of crossing.</summary>
    [MemoryPackOrder(1)]
    public int Lap;

    /// <summary>Client tick counter for ordering.</summary>
    [MemoryPackOrder(2)]
    public uint ClientTick;
}
