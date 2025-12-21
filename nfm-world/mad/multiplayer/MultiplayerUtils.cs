using System.Collections.Frozen;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public static class MultiplayerUtils
{
    public static FrozenDictionary<sbyte, Type> OpcodesC2S { get; } = new Dictionary<sbyte, Type>()
    {
        [1] = typeof(C2S_PlayerState),
        [2] = typeof(C2S_LobbyChatMessage),
        [3] = typeof(C2S_LobbyStartRace),
        [4] = typeof(C2S_PlayerIdentity),
    }.ToFrozenDictionary();
    
    public static FrozenDictionary<sbyte, Type> OpcodesS2C { get; } = new Dictionary<sbyte, Type>()
    {
        [1] = typeof(S2C_PlayerState),
        [2] = typeof(S2C_LobbyChatMessage),
        [3] = typeof(S2C_LobbyState),
    }.ToFrozenDictionary();
    
    public static FrozenDictionary<Type, sbyte> OpcodesC2SReverse { get; } = OpcodesC2S.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);
    public static FrozenDictionary<Type, sbyte> OpcodesS2CReverse { get; } = OpcodesS2C.ToFrozenDictionary(kv => kv.Value, kv => kv.Key);

    public static IPacketClientToServer? TryDeserializeC2SPacket(sbyte opcode, ReadOnlySpan<byte> data)
    {
        if (!MultiplayerUtils.OpcodesC2S.TryGetValue(opcode, out var packetType))
        {
            return null;
        }

        var reader = new SpanReader(data);

        if (packetType == typeof(C2S_PlayerState)) return C2S_PlayerState.Read(reader);
        if (packetType == typeof(C2S_LobbyChatMessage)) return C2S_LobbyChatMessage.Read(reader);
        if (packetType == typeof(C2S_LobbyStartRace)) return C2S_LobbyStartRace.Read(reader);
        if (packetType == typeof(C2S_PlayerIdentity)) return C2S_PlayerIdentity.Read(reader);
        
        throw new InvalidDataException($"Unhandled packet type: {packetType.FullName}");
    }
    
    public static IPacketServerToClient? TryDeserializeS2CPacket(sbyte opcode, ReadOnlySpan<byte> data)
    {
        if (!OpcodesS2C.TryGetValue(opcode, out var packetType))
        {
            return null;
        }

        var reader = new SpanReader(data);

        if (packetType == typeof(S2C_PlayerState)) return S2C_PlayerState.Read(reader);
        if (packetType == typeof(S2C_LobbyChatMessage)) return S2C_LobbyChatMessage.Read(reader);
        if (packetType == typeof(S2C_LobbyState)) return S2C_LobbyState.Read(reader);
        
        throw new InvalidDataException($"Unhandled packet type: {packetType.FullName}");
    }
}