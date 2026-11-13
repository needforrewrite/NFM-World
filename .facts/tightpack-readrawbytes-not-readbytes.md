# TightPack: ReadRawBytes, not ReadBytes, after a collection header

`TightPackReader.ReadBytes(Span<byte>)` reads its **own** 4-byte little-endian length prefix before copying.
`TryReadCollectionHeader(out int length)` also consumes a 4-byte length prefix. Calling `ReadBytes` after
`TryReadCollectionHeader` therefore re-reads the first 4 payload bytes as a length — the reader reports a
collection of ~67M elements with only a handful of bytes remaining, and deserialization throws.

The pooled byte-payload formatter (`PooledReadOnlyMemoryByteFormatter.Deserialize`) had exactly this bug
after the TightPack migration. It broke every in-race game-master packet that carries a payload:

- `S2C_ServerEvent` (opcode `-8`) — delivers `CountdownGoEvent`, finish events, etc.
- `S2C_ServerState` (opcode `-10`)
- `C2S_ClientEvent` (opcode `10`) — same formatter on the server receive path.

Symptom in-game: multiplayer PvP cars cannot move, power/damage/lap/speed stay zeroed, while the countdown
the client animates **locally** still runs 3-2-1. Lobby round-trip tests missed it because they only cover
lobby packets, none of which carry a pooled byte payload.

Fix: added `TightPackReader.ReadRawBytes(int length) => GetSpan(length)` (no prefix), and the formatter now
does `reader.ReadRawBytes(length).CopyTo(buffer)`. The old `ReadBytes(Span<byte>)` remains for callers that
want the self-prefixed form; it has no remaining TightPack call sites.

Regression coverage: `NFMWorld.Library.Test/GamePacketRoundTripTests.cs` round-trips all three payload-bearing
packets through the real opcode deserializers.