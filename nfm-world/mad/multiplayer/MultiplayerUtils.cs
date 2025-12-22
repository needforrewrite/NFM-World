using System.Collections.Frozen;
using NFMWorld.Mad.packets;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public static class MultiplayerUtils
{
    public static FrozenDictionary<sbyte, (Type Type, Func<ReadOnlyMemory<byte>, IPacketClientToServer> Deserialize)> OpcodesC2S { get; } = new Dictionary<sbyte, (Type Type, Func<ReadOnlyMemory<byte>, IPacketClientToServer>)>
    {
        [1] = (typeof(C2S_PlayerState), static data => DeserializePacket<C2S_PlayerState>(data)),
        [2] = (typeof(C2S_LobbyChatMessage), static data => DeserializePacket<C2S_LobbyChatMessage>(data)),
        [3] = (typeof(C2S_LobbyStartRace), static data => DeserializePacket<C2S_LobbyStartRace>(data)),
        [4] = (typeof(C2S_PlayerIdentity), static data => DeserializePacket<C2S_PlayerIdentity>(data)),
        [5] = (typeof(C2S_CreateSession), static data => DeserializePacket<C2S_CreateSession>(data)),
    }.ToFrozenDictionary();
    
    public static FrozenDictionary<sbyte, (Type Type, Func<ReadOnlyMemory<byte>, IPacketServerToClient> Deserialize)> OpcodesS2C { get; } = new Dictionary<sbyte, (Type Type, Func<ReadOnlyMemory<byte>, IPacketServerToClient>)>
    {
        [-1] = (typeof(S2C_PlayerState), static data => DeserializePacket<S2C_PlayerState>(data)),
        [-2] = (typeof(S2C_LobbyChatMessage), static data => DeserializePacket<S2C_LobbyChatMessage>(data)),
        [-3] = (typeof(S2C_LobbyState), static data => DeserializePacket<S2C_LobbyState>(data)),
        [-4] = (typeof(S2C_RaceStarted), static data => DeserializePacket<S2C_RaceStarted>(data)),
    }.ToFrozenDictionary();
    
    public static FrozenDictionary<Type, sbyte> OpcodesC2SReverse { get; } = OpcodesC2S.ToFrozenDictionary(static kv => kv.Value.Type, static kv => kv.Key);
    public static FrozenDictionary<Type, sbyte> OpcodesS2CReverse { get; } = OpcodesS2C.ToFrozenDictionary(static kv => kv.Value.Type, static kv => kv.Key);

    private static T DeserializePacket<T>(ReadOnlyMemory<byte> data) where T : IReadableWritable<T> => T.Read(data);

    public static IPacketClientToServer? TryDeserializeC2SPacket(sbyte opcode, ReadOnlyMemory<byte> data)
    {
        if (!OpcodesC2S.TryGetValue(opcode, out var packetType))
        {
            return null;
        }

        return packetType.Deserialize(data);
    }
    
    public static IPacketServerToClient? TryDeserializeS2CPacket(sbyte opcode, ReadOnlyMemory<byte> data)
    {
        if (!OpcodesS2C.TryGetValue(opcode, out var packetType))
        {
            return null;
        }

        return packetType.Deserialize(data);
    }
}