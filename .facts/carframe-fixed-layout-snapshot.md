# CarFrame is a fixed-layout snapshot: adding a field breaks every stored recording

`CarFrame` (`NFMWorld.Library/Mad/CarFrame.cs`) is `[TightPackable]` with a sequential `Pack = 1`
layout. TightPack writes it **member by member, not as a memory image**. `LayoutProver` accepts
`Pack` (it lowers the alignment cap for the struct that declares it), but a `Pack` on the outer
struct does not reach the structs nested inside it: `CarFrame` holds padded tuples — `(byte Lap,
ushort CheckpointInlap)` has one padding byte, and the tuples mixing `sbyte`/`fix64` members are
padded too — so the whole image still has holes and the proof is refused. The padding bytes never
reach the wire. The field list and its order are still the format — there is no tolerance for a
layout change in either direction.

Three things read those bytes:

- `SavedTimeTrial.DemoData.Ticks` — every `.timetrial` recording, including the stage and car
  embedded in it, and every replay the archive stores and validates.
- `PlayerState.CarFrame` — the `S2C_PlayerState` multiplayer packet.
- `TimeTrialSimulator` / `NFMWorld.Archive.Validator`, which replay a recording tick by tick.

`SavedTimeTrial.CURRENT_VERSION` gates compatibility. It is **7** since the MemoryPack →
TightPack migration: the wire format changed wholesale, so a recording written by a MemoryPack
build is refused rather than misparsed.

- `ReplayBrowserPhase.IsCompatible` refuses a mismatched recording on click, not only in the list
  label.
- `NFMWorld.Archive.Validator` (`Program.cs`, exit code 2) rejects a version-mismatched upload.
- `TimeTrialSubmissionsTests` uses `SavedTimeTrial.CURRENT_VERSION` instead of a literal, so the
  next bump does not silently leave a stale constant behind.

The camera fields exist because the follow camera is driven from `CarPhysics.Cxz`
(`RaceCameraDirector.Update`), which only physics writes; a replay has no physics, so without the
recorded value every replay camera pointed due north. `ApplyToCar` writes them back.

Verify with `NFMWorld/UiHarness/replay-camera.json` (`view: replay-camera`), which applies recorded
headings through the real `RaceCameraDirector` and prints the resulting camera position.
`PlayerStateSerializationTests` covers a `CarFrame`-carrying `PlayerState` round trip directly.
