# Multiplayer Netcode Specification

## Lobby

### Joining
* Player sends C2S_PlayerIdentity
* Server periodically sends S2C_LobbyState

### Chatting
* Player sends C2S_LobbyChatMessage
* Lobby broadcasts S2C_LobbyChatMessage

### Creating Games
* Player sends C2S_CreateSession
* Server broadcasts S2C_LobbyState

### Joining Games
* Player sends C2S_JoinSession
* Server broadcasts S2C_LobbyState

### Leaving Games
* Player sends C2S_LeaveSession
* Server broadcasts S2C_LobbyState

### Ready Up
* Player sends C2S_LobbyPlayerReadyState
* Server broadcasts S2C_LobbyState

### Starting Games
* Room creator client sends C2S_LobbyStartRace
* Server sends S2C_RaceStarted to joined clients


* Server waits 20 seconds for all players to send C2S_RaceLoaded
* Server sends S2C_RaceCanStart
* Enter in-game state

### Spectating
* Player sends C2S_JoinAsSpectator
* Enter in-game state as spectator (only receives S2C_PlayerState updates)

## In-Game (Netcode v1 non-deterministic)
V1 is a dumb relay without rollback. The client just sends
positional and state updates to the server, which relays
them to all other clients.

* Clients send C2S_PlayerState
* Server broadcasts S2C_PlayerState

#### Finishing Game
* Client sends C2S_GameFinished
  * First-come first-served full trust basis
* Server broadcasts S2C_GameFinished
* Return to lobby state

### In-Game (Netcode v2 deterministic with rollback)
V2 is a deterministic lockstep netcode with rollback.

* Clients send C2S_InputFrames - last confirmed frame + inputs for next frames
* Server broadcasts S2C_AuthoritativeFrame - authoritative game state at specific frame + player inputs

* Client predicts current frame based on previous frames + prediction
* Client rollbacks to authoritative frame when received + re-simulates to current frame

## Future Work
* Thread per session on server for scalability
* Only send lobby updates to changed sessions