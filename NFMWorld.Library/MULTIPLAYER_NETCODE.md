# NFMW Multiplayer Architecture

- Archive/Login Server: Ran by Jacher. Written in Rust. (https://github.com/needforrewrite/nfmw-archive)
    - HTTP server
    - Handles authentication and account management
    - Handles uploading assets (maps, cars, gamemodes, etc.) to the asset server
    - Provides an HTTP FS we can use to fetch assets from the asset server in RADPACK format
- Lobby: Ran by Jacher/Maxine. Written in C#. (NFMWorld.Server.Lobby)
    - WebSocket server with HTTP to receive race-end responses from the game server
    - Handles matchmaking and lobby management
    - Handles chat and lobby state updates
    - Handles starting games and handing off to a given race server
    - Keeps a list of game servers that it has access to alongside access keys
- Game Server Master: Ran by anyone. Written in C# (NFMWorld.Server.Game)
    - UDP server with an HTTP server for the lobby to request match creation
    - Handles gameplay connectivity in races
    - Doesn't do gameplay logic itself
    - Access key protected, so only the lobby can request a new game server to be created
    - Manages a Game Server Slave process per race
    - Talks to the slave via SharedMemory.RpcBuffer
- Game Server Slave: Ran by anyone. Written in C# (NFMWorld.Server.Game.Slave)
    - Talks to the master process via SharedMemory.RpcBuffer
    - Runs the game simulation and handles all gameplay logic

# Multiplayer Netcode Specification

## Lobby

### Joining

* Player sends C2S_PlayerIdentity
    - [X] Client
    - [X] Server
* Server periodically sends S2C_LobbyState
    - [X] Client
    - [X] Server

### Chatting

* Player sends C2S_LobbyChatMessage
    - [X] Client
    - [X] Server
* Lobby broadcasts S2C_LobbyChatMessage
    - [X] Client
    - [X] Server

### Creating Games

* Player sends C2S_CreateSession
    - [X] Client
    - [X] Server
* Server broadcasts S2C_LobbyState
    - [X] Client
    - [X] Server

### Joining Games

* Player sends C2S_JoinSession
    - [X] Client
    - [X] Server
* Server broadcasts S2C_LobbyState
    - [X] Client
    - [X] Server

### Leaving Games

* Player sends C2S_LeaveSession
    - [X] Client
    - [X] Server
* Server broadcasts S2C_LobbyState
    - [X] Client
    - [X] Server

### Ready Up

* Player sends C2S_LobbyPlayerReadyState
    - [ ] Client
    - [ ] Server
* Server broadcasts S2C_LobbyState
    - [ ] Client
    - [ ] Server

### Starting Games

* Room creator client sends C2S_LobbyStartRace
    - [X] Client
    - [X] Server
* Server sends S2C_RaceStarted to joined clients
    - [X] Client
    - [X] Server

* Server waits 20 seconds for all players to send C2S_RaceLoaded
    - [X] Client
    - [X] Server
* Server sends S2C_RaceCanStart
    - [X] Client
    - [X] Server
* If timeout, server sends S2C_RaceFailedToStart
    - [X] Client
    - [X] Server
* Enter in-game state
    - [X] Client
    - [X] Server

### Spectating

* Player sends C2S_JoinAsSpectator
    - [ ] Client
    - [ ] Server
* Enter in-game state as spectator (only receives S2C_PlayerState updates)
    - [ ] Client
    - [ ] Server

### Cleaning up finished sessions

* Server periodically removes sessions that have been finished for more than 5 minutes
    - [ ] Client
    - [ ] Server

## In-Game (Netcode v1)

V1 is a dumb relay. The client just sends positional and state updates to the server, which relays them to all other
clients.

* Clients send C2S_PlayerState
    - [x] Client
    - [x] Server
* Server broadcasts S2C_PlayerState
    - [x] Client
    - [x] Server

#### Finishing Game

* Client sends C2S_GameFinished
    - [ ] Client
    - [ ] Server

    * First-come first-served full trust basis
* Server broadcasts S2C_GameFinished
    - [ ] Client
    - [ ] Server
* Return to lobby state
    - [ ] Client

#### Disconnecting

* Client sends C2S_SelfDisconnect
    - [ ] Client
    - [ ] Server
* Server broadcasts S2C_PlayerState with disconnect=true
    - [ ] Client
    - [ ] Server

### In-Game (Netcode v2 with cheat protection)

* On race finish, client sends C2S_GameFinished with replay data
    * Replay data includes all player inputs as well as the state of other cars as is visible to the player on each
      frame
        * Other cars's state may not be deterministic due to lag, so is serialized fully. Player inputs are always
          deterministic
    * Server runs a validation run on the replay data to ensure that the player did not cheat. If the validation fails,
      the player is kicked from the server and the race continues.

    - [ ] Client
    - [ ] Server

## Future Work

* Process per gameplay server for crash resilience
* Only send lobby updates to changed sessions
