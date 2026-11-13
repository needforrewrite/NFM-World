namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// The wire version, shared by the client, the lobby and the game master.
///
/// Packets are TightPack structs with a fixed layout and hardcoded opcodes, so a build from
/// another revision does not misread them into a bad value - it reads fields that mean something
/// else, or fails part-way through. Both sides declare this number before any other packet and
/// the connection is refused on a mismatch.
///
/// Bump it whenever a packet's layout, a packet's meaning, or the opcode table changes. Adding a
/// packet is only a bump if an older peer would still act on the conversation after failing to
/// send its half of it.
/// </summary>
public static class Protocol
{
    public const int Version = 1;

    /// <summary>Opcode of <c>C2S_Handshake</c>. The transactions are in separate number spaces.</summary>
    public const sbyte ClientHandshakeOpcode = 14;

    /// <summary>Opcode of <c>S2C_Handshake</c>.</summary>
    public const sbyte ServerHandshakeOpcode = -12;

    /// <summary>Header carrying <see cref="Version"/> on the lobby-to-game-master HTTP API.</summary>
    public const string HeaderName = "X-Nfmw-Protocol";
}
