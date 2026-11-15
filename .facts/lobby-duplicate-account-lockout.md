# Lobby duplicate-account lockout

The "one account, one connection" rule used to refuse the **newest** connection, which locked a
player out permanently as soon as their old lobby socket died without a close:

- The lobby WebSocket can drop during a race. The client transport reconnects on its own, but
  `LobbyPhase` is not ticked while `RacePhase` is on top, so the refusal packet sat unread in the
  queue and surfaced only when the race popped — "booted to the main menu" right after pressing
  Enter on the results screen.
- The dead connection never left `PlayerRegistry`: nothing aborted it. `WsClient.SendCoreAsync`
  swallowed the send exception, and `CloseCoreAsync` waited on `CloseAsync` with no timeout, so
  the receive loop blocked indefinitely. The account seat was held for the life of the process —
  it survived a full client restart, and every retry was refused the same way.

Fix (both halves are load-bearing):

- `GameOrchestrator.VerifyAndAnswerAsync` now has the newest connection take the account over and
  refuses the older one with `"Your account signed in on another connection."` (no
  `SessionRejected`, so the token survives).
- `WebSocketMultiplayerClientTransport` does not reconnect after a `PolicyViolation` close; retrying
  would take the seat back and evict the connection that holds it, ping-ponging.
- Server-side `WsClient` aborts the socket after a failed send and after an unanswered close
  (5 s), so a vanished peer actually leaves the registry.

Tests: `LobbyAuthenticationTests.SecondConnection_TakesTheAccountOver_AndEvictsTheFirst`,
`EvictedConnection_NoLongerHoldsTheAccount`, and the real-socket
`LobbyHandshakeTests.Takeover_EvictsTheOlderConnection_AndTheRefusalReachesItBeforeTheClose`.
