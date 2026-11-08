namespace NFMWorldLibrary.Multiplayer.Packets;

public enum PacketDirection
{
    ClientToServer,
    ServerToClient
}

[AttributeUsage(AttributeTargets.Class | AttributeTargets.Struct, Inherited = false)]
public class PacketAttribute(PacketDirection direction, sbyte opcode) : Attribute
{
    public PacketDirection Direction { get; } = direction;
    public sbyte Opcode { get; } = opcode;
}

public class PacketClientToServerAttribute(sbyte opcode) : PacketAttribute(PacketDirection.ClientToServer, opcode);

public class PacketServerToClientAttribute(sbyte opcode) : PacketAttribute(PacketDirection.ServerToClient, opcode);
