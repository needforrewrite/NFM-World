# CarFrame is a fixed-layout snapshot: adding a field breaks every stored recording

`CarFrame` (`NFMWorld.Library/Mad/CarFrame.cs`) is `[MemoryPackable]` with a sequential `Pack = 1`
layout, and the generated formatter is `writer.WriteUnmanaged(value)` / `ReadUnmanaged(out value)`.
The bytes are the struct's memory layout, not a field list, so there is no tolerance for a layout
change in either direction.

Three things read those bytes:

- `SavedTimeTrial.DemoData.Ticks` — every `.timetrial` recording, including the stage and car
  embedded in it, and every replay the archive stores and validates.
- `PlayerState.CarFrame` — the `S2C_PlayerState` multiplayer packet.
- `TimeTrialSimulator` / `NFMWorld.Archive.Validator`, which replay a recording tick by tick.

Adding `CameraAngles` (`Cxz`, `StaticXz`, 16 bytes) therefore bumped
`SavedTimeTrial.CURRENT_VERSION` 5 → 6. An older file still deserializes without an exception; the
extra fields just read whatever followed, so the replay looks wrong (camera heading stuck, garbage
positions) rather than failing loudly. That is why:

- `ReplayBrowserPhase.IsCompatible` refuses a mismatched recording on click, not only in the list
  label.
- `NFMWorld.Archive.Validator` (`Program.cs`, exit code 2) rejects a version-mismatched upload.
- `TimeTrialSubmissionsTests` now uses `SavedTimeTrial.CURRENT_VERSION` instead of a literal, so the
  next bump does not silently leave a stale constant behind.

The camera fields exist because the follow camera is driven from `CarPhysics.Cxz`
(`RaceCameraDirector.Update`), which only physics writes; a replay has no physics, so without the
recorded value every replay camera pointed due north. `ApplyToCar` writes them back.

Verify with `NFMWorld/UiHarness/replay-camera.json` (`view: replay-camera`), which applies recorded
headings through the real `RaceCameraDirector` and prints the resulting camera position.
