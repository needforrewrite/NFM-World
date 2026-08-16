using Lua;
using MemoryPack;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Wire format for events between Lua gamemode scripts and their hosts.
/// The payload is a JSON object serialized from a Lua table, keeping
/// event schemas free-form for script authors.
/// </summary>
[MemoryPackable]
public readonly partial struct LuaEventEnvelope
{
    /// <summary>Event type discriminator, e.g. "checkpoint".</summary>
    [MemoryPackOrder(0)]
    public required string Type { get; init; }

    /// <summary>JSON-encoded event payload (UTF-8).</summary>
    [MemoryPackOrder(1), LuaValueMemoryPackFormatter]
    public required LuaValue Payload { get; init; }
}
