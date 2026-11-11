---
tags:
- architecture
- networking
- enet
- multiplayer
---
Native `ENet-CSharp` 2.4.8 was replaced by `NFMWorld.Multiplayer.ENet` (namespace `ENet`, fully managed, referenced by `NFMWorld.Library` and `NFMWorld.Multiplayer.Base`).

Port source is nxrighthere/ENet-CSharp's amalgamated fork (`Source/Native/enet.h`), not stock lsalzman enet. Fork differences that matter if diffing:
- `ENET_PROTOCOL_HEADER_FLAG_SENT_TIME` is **bit 14 (0x4000)**; compression/bit 15 are gone.
- `ENET_HOST_DEFAULT_MTU` = 1280; `ENET_PEER_DEFAULT_ROUND_TRIP_TIME` = 1; ping interval 250.
- Reliable commands with a payload queue in `outgoingSendReliableCommands` and are merged with `outgoingCommands` by `queueTime`.
- Timeouts reset `roundTripTimeout` from the live RTT (no doubling); `ENET_EVENT_TYPE_DISCONNECT_TIMEOUT` maps to `EventType.Timeout`.
- All protocol integers are big-endian.

Implementation files: `Host.cs` (host + protocol), `Peer.cs` (peer/channels/queues), `Protocol.cs` (packed command layouts), `Packet.cs`, `Address.cs`, `EnetList.cs`. Sockets: servers dual-stack IPv6, clients bind an ephemeral port (required for `ReceiveFrom`), IPv4 peers normalized.

Wire compatibility was verified against the real native library in both directions (connect, reliable, 6000-byte fragmentation, unsequenced, disconnect). Regression test: `NFMWorld.Library.Test/ManagedEnetTests.cs` (loopback; must pump both hosts, `Service(0)` only does one send/receive exchange).
