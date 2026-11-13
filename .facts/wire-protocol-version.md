# Wire protocol version

`Protocol.Version` (`NFMWorld.Library/Mad/Multiplayer/Protocol.cs`) is the single wire-version number for
gameplay sockets and the lobby→game-master HTTP API. Bump it on any packet layout/meaning or opcode-table
change; adding a packet is only a bump if an older peer would keep acting after failing to send its half.

## How it is enforced

- **Gameplay sockets** — `C2S_Handshake` (opcode 14) is sent by `BaseMultiplayerClientTransport` on every
  transition to `Connected`; the reply `S2C_Handshake` (opcode −12) is intercepted in `ReceivePacket` and never
  queued for the caller. `BaseMultiplayerServerTransport` handles the handshake itself and refuses any other
  opcode from an undeclared connection, so no handler ever sees a packet from a build whose layout it does not
  share. A refusal is `S2C_Handshake { Accepted = false, Error }` then a disconnect.
- **The refusal must arrive before the close.** Each transport chains the close behind queued sends:
  `WsClient.CloseAsync` continues the send task, ENet queues a deferred-close marker (a `null` entry in its
  send queue → `DisconnectLater(0)`), Steam uses `connection.Close(linger: true)`.
- **Client timeout** — an older server ignores the handshake opcode, so silence is the signal. The client sends
  three declarations 5 s apart (`HandshakeRetryDelayMs`, `MaxHandshakeAttempts`), then sets `HandshakeError`,
  `StopReconnecting()` and `State = Disconnected`.
- **Consumers** — `LobbyPhase` gates auth/actions on `HandshakeAccepted` and shows an "Update required" popup on
  `HandshakeError`; `NetworkRaceHost` holds `C2S_RaceLoaded` and reports the refusal as a failed race start.
- **Lobby→GM HTTP** — `X-Nfmw-Protocol` (`Protocol.HeaderName`) on every request; the GM checks it **before
  reading the body** and returns `409` with `StatusDescription = "protocol N"`. HMAC alone would still verify
  across revisions, hence a separate header.

## Traps

- ENet reuses peer ids after a disconnect, so every transport must call `ForgetConnection(id)` where it raises
  `ClientDisconnected`; otherwise the next peer inherits a handshake it never sent.
- `Peer.DisconnectNow` discards queued packets, which is why ENet defers the close instead.
- `Logging.Warning/Error(message, exception:)` needs an interpolated string; a plain literal is `CS9205`.
- `TightPack`'s `writer.Write(sbyte)` comes from `CommunityToolkit.HighPerformance`; tests framing packets need
  that using plus `CommunityToolkit.HighPerformance.Buffers` for `ArrayPoolBufferWriter<byte>`.

## Tests

- `NFMWorld.Library.Test/ProtocolVersionTests.cs` — in-process `TestServer`/`TestClient` stubs over
  `ReceivePacket`: first-packet ordering, accept, refuse + disconnect, pre-handshake refusal, accept-after-
  handshake, client accept/refusal, forget-connection.
- `NFMWorld.Library.Test/ProtocolVersionTransportTests.cs` — real WebSocket and `HttpListener`: refusal
  decoded by a raw `ClientWebSocket`, unversioned packet never reaching a handler, real client accepted and
  then able to send.
