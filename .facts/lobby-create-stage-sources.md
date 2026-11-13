---
name: lobby-create-stage-sources
description: "The lobby create dialog's stage list is UiState.Lobby.CreateStages, filled and searched by LobbyPhase; LOCAL/ONLINE switching must not lose the chosen stage."
metadata:
  node_type: memory
  type: project
---

The create dialog mounts `StageSelectView.Compact` over `UiState.Lobby.CreateStages`. The phase
owns that state (`OnOpenCreate` fills LOCAL from `LobbyStageSelection.LocalStages`), because the
ONLINE side is a network search through `GarageController.ContentClient` (`"Stage"`, `Eligible`
only → `StageOption("archive/<hash>", Name, Author)`). The view only forwards actions:
`SetStage`, `SetStageSearch`, `SetStageSource`, `SearchStagesOnline`.

Rules:

- `LobbyStageSelection.Select` is the gate: it returns false for a local-only (`user/`) stage and
  sets `Restriction`. `LobbyPhase.OnSelectStage` only writes `Lobby.SelectedStage` when it is true,
  so a refused pick cannot become the session's stage.
- Switching source must not lose the pick. `ShowLocal` appends the chosen archive stage as its own
  row and `WithSelection` re-adds it after a search filter; otherwise the highlight points at
  nothing while `Lobby.SelectedStage` still holds that stage.
- Archive search is Enter-only (`OnlineSearch`); typing only stores the text. A typed canonical
  archive id short-circuits the request.
- The phase's default stage in `Publish` comes from `LocalStages`, not `GameSparker`'s first stage,
  which can be a `user/` stage the lobby refuses.
- `OnCloseCreate`/`OnCreate`/`Dispose` cancel the search generation; a late HTTP result must not
  write the closed dialog's state.

Covered by `LobbyStageSelectionTests` (pure state transitions) and the `lobby-create` harness
(`local` → `online` → pick → back to `local` → `online` captures).
