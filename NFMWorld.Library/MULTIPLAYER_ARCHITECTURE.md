# NFMWorld Multiplayer Architecture

## Overview

Three services: Lobby, Game Master (dumb relay for v1), and Worker (replay validation for v2).

```
┌──────────┐  WebSocket        ┌──────────┐  HMAC HTTP      ┌──────────┐
│  Client  │ ←───────────────→ │  Lobby   │ ←─────────────→ │  Game    │
│          │                   │          │                 │  Master  │
└────┬─────┘                   └──────────┘                 └────┬─────┘
     │                        matchmaking,                   │  ENet UDP
     │                        chat, sessions,                │  relay
     │                        race start                     │
     │                                                       │
     └──────── ENet UDP (in-game, to assigned GM) ───────────┘
              C2S_PlayerState → relayed direct as S2C_PlayerState
              C2S_RaceLoaded, C2S_GameFinished
              S2C_RaceCanStart, S2C_PlayerState, S2C_GameFinished
```

v1: Game Master is a dumb UDP relay. Clients are authoritative — state is forwarded directly.
v2: Worker project handles replay-based cheat validation (code in repo, not active).

## Service Descriptions

### Lobby (`NFMWorld.Server.Lobby`)
**Role:** Matchmaking, chat, and session management.

| Protocol | Port | Purpose |
|---|---|---|
| WebSocket | `LOBBY_PORT` (default 7000) | Client connections (System.Net.HttpListener) |
| HTTP | `LOBBY_HTTP_ENDPOINT` (default 7001) | Race results from Game Masters |

**Key classes:**
- `GameOrchestrator` — coordinator delegating to managers
- `SessionManager` — create/join/leave sessions, timeouts
- `ChatManager` — chat messages and system broadcasts
- `PlayerRegistry` — player identity and connection state
- `LobbyStateBroadcaster` — periodic `S2C_LobbyState` snapshots (1s)
- `GameMasterRegistry` — SRV or dev-mode GM discovery, health tracking, round-robin
- `GameMasterHttpClient` — HMAC-signed HTTP to Game Masters

### Game Master (`NFMWorld.Server.Game`)
**Role:** Race session relay — validates join tokens, relays packets between clients.

| Protocol | Port | Purpose |
|---|---|---|
| ENet UDP | `GM_GAME_PORT` (default 7002) | Client in-game connections |
| HTTP | `GM_HTTP_ENDPOINT` (default 7003) | Lobby API (`/create-race`) |

**Key classes:**
- `RaceOrchestrator` — validates join tokens, relays `C2S_PlayerState` → `S2C_PlayerState` to other clients, handles `C2S_GameFinished` (first-come first-served)
- `WorkerManager` — kept in repo for v2 replay validation; not used in active v1 path

### Worker (`NFMWorld.Server.Game.Worker`)
**Role:** Per-race replay validation (v2). Not active in v1 relay path.

Code kept in repo for future use: `RaceWorker` wraps `RaceGamemode` for headless simulation. The Worker project and `WorkerManager` are compiled but not instantiated by the v1 relay.

## Player Identity

Players are identified by two IDs:

| ID | Type | Scope | Assigned by |
|---|---|---|---|
| Client index | `uint` | Transport-level (ENet/WebSocket connection) | Transport |
| Player ID | `Guid` | Application-level (lobby, packets, sessions) | Lobby (`PlayerRegistry`) |

The `PlayerRegistry` maps between them. All packets use `Guid` player IDs for addressing. `S2C_PlayerState.PlayerId` is a `Guid`. `S2C_LobbyState.PlayerInfo` carries both the transport index and the GUID.

---

## Communication Protocols

### Client ↔ Lobby (WebSocket)

`WebSocketMultiplayerClientTransport` (System.Net.WebSockets.ClientWebSocket) connects to `ws://host:port/game`.
Server side: `WebSocketMultiplayerServerTransport` (System.Net.HttpListener + HttpListenerWebSocketContext).

| Packet | Opcode | Direction | Purpose |
|---|---|---|---|
| `C2S_PlayerIdentity` | 6 | C→S | Set name, vehicle, color |
| `C2S_CreateSession` | 1 | C→S | Create a game session |
| `C2S_JoinSession` | 2 | C→S | Join existing session |
| `C2S_LeaveSession` | 3 | C→S | Leave current session |
| `C2S_LobbyChatMessage` | 4 | C→S | Send chat message |
| `C2S_LobbyStartRace` | 5 | C→S | Start the race |
| `C2S_LobbyPlayerReadyState` | 9 | C→S | Toggle ready state |
| `S2C_LobbyState` | -2 | S→C | Full lobby snapshot (1s interval) |
| `S2C_LobbyChatMessage` | -1 | S→C | Chat broadcast |
| `S2C_RaceStarted` | -6 | S→C | Race starting — contains GM address + join token |

### Client ↔ Game Master (ENet UDP)

The client creates a new `ENetMultiplayerClientTransport` from the `S2C_RaceStarted.JoinInfo`. The lobby WebSocket stays alive.

| Packet | Opcode | Direction | Purpose |
|---|---|---|---|
| `C2S_RaceLoaded` | 8 | C→S | Client loaded, contains join token |
| `C2S_PlayerState` | 7 | C→S | Player position/input (63 TPS) |
| `C2S_GameFinished` | 10 | C→S | Client reports race finish |
| `S2C_RaceCanStart` | -4 | S→C | All players loaded, race begins |
| `S2C_RaceFailedToStart` | -5 | S→C | Load timeout |
| `S2C_PlayerState` | -3 | S→C | Other players' state |
| `S2C_GameFinished` | -7 | S→C | Race results |

### Lobby ↔ Game Master (HMAC HTTP)

Authenticated with HMAC-SHA256. See [HMAC Authentication](#hmac-authentication).

| Endpoint | Direction | Body | Response |
|---|---|---|---|
| `POST /create-race` | Lobby → GM | `Lobby2RaceServer_CreateRace` (MemoryPack) | `Lobby2RaceServer_CreateRaceResponse` (join tokens) |
| `POST /race-ended` | GM → Lobby | `RaceServer2Lobby_RaceResults` (MemoryPack) | 200 OK |

### Game Master ↔ Worker (SharedMemory RPC) — v2 only

Not active in v1 relay. Uses `SharedMemory` NuGet (`RpcBuffer`) for lock-free Controller↔Worker communication. `RpcBridge` wraps it with typed MemoryPack messages.

Types in `NFMWorld.Server.SharedMemory/`: `RpcBridge`, `RpcMessage`, `PlayerInputBatch`, `GameStateSnapshot`.

---

## HMAC Authentication

Lobby → Game Master HTTP requests are signed with HMAC-SHA256.

### Wire format

```
POST /create-race
Authorization: HMAC-SHA256 keyId=primary,ts=1720900000,sig=a1b2c3...
Content-Type: application/octet-stream
[BODY: MemoryPack binary]
```

### Signature

```
stringToSign = "{method}\n{path}\n{unixTimestamp}\n{hex(SHA256(body))}"
signature    = HMAC-SHA256(secretKey, stringToSign)
```

### Key rotation

1. Add new key to Game Master: `GM_HMAC_KEYS=primary=oldkey,primary-v2=newkey`
2. Update Lobby: `HMAC_KEY_ID=primary-v2` `HMAC_SECRET_KEY=newkey`
3. Remove old key from Game Master once no in-flight requests remain

### Generating keys

```bash
dotnet run --project NFMWorld.GenerateHmacKey [keyId]
```

---

## Game Master Discovery

The Lobby discovers Game Masters via the `GAME_MASTER_DOMAINS` env var.

### Dev mode (no SRV records)

```
GAME_MASTER_DOMAINS=localhost:7002+7003
```

Format: `host:udpPort+httpPort`. The `+` character triggers dev-mode parsing.

### Production (SRV records)

```
GAME_MASTER_DOMAINS=gm1.nfmw.example.com,gm2.nfmw.example.com
```

Each domain is resolved via A/AAAA records (SRV support planned via `DnsClient`). HTTP is on standard port 80/443; game UDP is on port 7000 (configurable via SRV in the future).

### Mixed

```
GAME_MASTER_DOMAINS=localhost:7002+7003,gm1.nfmw.example.com
```

---

## Client Connection Flow

1. Client: `connect <lobby_host> <lobby_port>` → WebSocket to Lobby
2. Client sends `C2S_PlayerIdentity` on connect
3. Lobby sends `S2C_LobbyState` every 1s
4. Player creates/joins session, chats, readies up
5. Room creator sends `C2S_LobbyStartRace`
6. Lobby selects a healthy Game Master, sends `POST /create-race` (HMAC-signed)
7. Lobby sends `S2C_RaceStarted` with GM's game address + per-player join token
8. Client creates new ENet UDP transport → Game Master
9. Client sends `C2S_RaceLoaded` with join token
10. When all loaded → Game Master sends `S2C_RaceCanStart`
11. During race: client sends `C2S_PlayerState` at 63 TPS → Game Master relays directly as `S2C_PlayerState` to other clients
12. Race ends: first client to send `C2S_GameFinished` wins (v1 first-come first-served) → Game Master broadcasts `S2C_GameFinished`
13. Client transitions back to LobbyPhase (WebSocket transport kept alive)

---

## Server Setup Guide

### Prerequisites

- .NET 10 SDK
- All projects: `dotnet restore nfm-world.slnx`

### 1. Generate HMAC keys

```bash
dotnet run --project NFMWorld.GenerateHmacKey
```

Save the output — you'll need `HMAC_KEY_ID`, `HMAC_SECRET_KEY`, and `GM_HMAC_KEYS`.

### 2. Start the Game Master

```powershell
$env:GM_HMAC_KEYS = "primary=LC7aXjNZ5mcB2AJxwXZ+OZjAKyEaCjV6pTXIpRAmrjY="
$env:GM_HTTP_ENDPOINT = "http://localhost:7003/"
$env:GM_GAME_PORT = "7002"

dotnet run --project NFMWorld.Server.Game
```

Or set env vars externally and:
```bash
dotnet run --project NFMWorld.Server.Game
```

### 3. Start the Lobby

```powershell
$env:HMAC_KEY_ID = "primary"
$env:HMAC_SECRET_KEY = "LC7aXjNZ5mcB2AJxwXZ+OZjAKyEaCjV6pTXIpRAmrjY="
$env:GAME_MASTER_DOMAINS = "localhost:7002+7003"
$env:LOBBY_PORT = "7000"
$env:LOBBY_HTTP_ENDPOINT = "http://localhost:7001/"

dotnet run --project NFMWorld.Server.Lobby
```

### 4. Connect the client

In the game's dev console:
```
connect localhost 7000
```

### Full environment variable reference

#### Lobby

| Variable | Default | Purpose |
|---|---|---|
| `LOBBY_PORT` | `7000` | WebSocket port for clients |
| `LOBBY_HTTP_ENDPOINT` | `http://localhost:7001/` | HTTP endpoint for race results |
| `GAME_MASTER_DOMAINS` | `localhost` | GM addresses (dev or SRV format) |
| `HMAC_KEY_ID` | `primary` | Key ID for HMAC signing |
| `HMAC_SECRET_KEY` | *(required)* | Base64 HMAC secret |

#### Game Master

| Variable | Default | Purpose |
|---|---|---|
| `GM_GAME_PORT` | `7002` | ENet UDP port for players |
| `GM_HTTP_ENDPOINT` | `http://localhost:7003/` | HTTP endpoint for Lobby API |
| `GM_HMAC_KEYS` | *(required)* | `keyId=base64secret,...` pairs |
| `WORKER_BINARY_PATH` | `dotnet` | Path to Worker binary, or `dotnet` for dev |

#### Worker (v2 only — receives config via CLI args)

```
--shm-name nfmw-race-{guid} --stage nfmm/trackname --gamemode 0 --players {base64}
```

---

## Solution Structure

```
NFMWorld.Server.Lobby/           Lobby server
  GameOrchestrator.cs             Coordinator
  SessionManager.cs               Session CRUD + timeouts
  ChatManager.cs                  Chat messages
  PlayerRegistry.cs               Player identity (Guid) + transport index mapping
  LobbyStateBroadcaster.cs        Periodic lobby state snapshots
  GameMasterRegistry.cs           GM discovery (SRV + dev mode)
  GameMasterHttpClient.cs         HMAC-signed HTTP to GMs
  Program.cs                      Entry point

NFMWorld.Server.Game/            Game Master (dumb relay v1)
  RaceOrchestrator.cs             Join token validation, direct packet relay
  WorkerManager.cs                (v2 only — Worker process lifecycle)
  Program.cs                      Entry point + HttpListener

NFMWorld.Server.Game.Worker/     Worker (v2 replay validation)
  RaceWorker.cs                   Simulation loop wrapping RaceGamemode
  Program.cs                      Entry point, RPC handler, 63 TPS loop

NFMWorld.Server.SharedMemory/    Shared RPC types (v2)
  RpcBridge.cs                    Wrapper around SharedMemory.RpcBuffer
  RpcMessage.cs                   Message envelope + opcodes
  RpcMessages.cs                  PlayerInputBatch, GameStateSnapshot

NFMWorld.Library/                Shared types
  Mad/Multiplayer/HmacAuth.cs     HMAC signing + verification
  Mad/Multiplayer/Packets/        All packet definitions (C2S, S2C)
  Mad/Multiplayer/HttpMessages/   HTTP message types

NFMWorld.Multiplayer.Base/      Transport layer
  WebSockets/                     System.Net WebSocket server + client
  ENet/                           ENet UDP transport
  Steam/                          Steamworks transport
NFMWorld.GenerateHmacKey/       HMAC key generation tool
```

---

## Known Limitations & Future Work

- **v1 relay only**: Game Master is a dumb UDP relay. Clients are fully trusted. Cheat protection is v2.
- **v2 replay validation**: Worker infrastructure (`WorkerManager`, `RaceWorker`, `SharedMemory`) is built and ready for replay-based cheat validation.
- **SRV records**: Currently uses A/AAAA lookup. Planned: `DnsClient` NuGet for full SRV support.
- **Login/Archive**: Out of scope. `C2S_PlayerIdentity` is accepted without validation.
- **Race results to Lobby**: `POST /race-ended` endpoint exists but Game Master doesn't send results yet.
- **Players addressed by Guid**: Application-level player IDs are Guids, mapped from transport-level `uint` indices by `PlayerRegistry`.

NFMWorld.Server.Game.Worker/     Worker (per-race simulation)
  RaceWorker.cs                   Simulation loop wrapping RaceGamemode
  Program.cs                      Entry point, RPC handler, 63 TPS loop

NFMWorld.Server.SharedMemory/    Shared RPC types
  RpcBridge.cs                    Wrapper around SharedMemory.RpcBuffer
  RpcMessage.cs                   Message envelope + opcodes
  RpcMessages.cs                  PlayerInputBatch, GameStateSnapshot

NFMWorld.Library/                Shared types
  Mad/Multiplayer/HmacAuth.cs     HMAC signing + verification
  Mad/Multiplayer/Packets/        All packet definitions (C2S, S2C)
  Mad/Multiplayer/HttpMessages/   HTTP message types

NFMWorld.Multiplayer.Base/      Transport abstractions (unchanged)
NFMWorld.GenerateHmacKey/       HMAC key generation tool
```

---

## Known Limitations & Future Work

- **SRV records**: Currently uses A/AAAA lookup with default ports. Planned: `DnsClient` NuGet for full SRV support.
- **Worker process path**: Dev mode uses `dotnet run --project` with a relative path from the Game Master's output directory. Production needs `WORKER_BINARY_PATH` pointing to a published binary.
- **Login/Archive**: Out of scope. `C2S_PlayerIdentity` is accepted without validation.
- **v2 netcode**: Full-trust v1. Worker logs inputs for future replay validation. `IRaceValidator` hook planned.
- **Race results to Lobby**: `POST /race-ended` endpoint exists but Game Master doesn't populate final standings yet.
