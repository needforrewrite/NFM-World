---
name: car-paint-rewrites-shared-rad
description: "Garage paint overrides mutate the shared Rad3d polys, and a live preview needs the mesh's copied colours re-synced."
metadata:
  node_type: memory
  type: project
---

Car body colours (`1stColor(..)`…`4thColor(..)`) are baked into `Rad3dPoly.Color`; `ColNum` is the only
slot tag and the renderer never reads it. `CarPaint` (`NFMWorld.Library/Util/`) applies a chosen colour by
rewriting the polys with that `ColNum` on the `Rad3d` instance itself.

- **The `Rad3d` is shared.** `BackendGameSparker.cars` hands the same instance to the menu, the garage and
  every race, so the rewrite is what carries the paint everywhere. `BackendGameSparker.GetCar` applies the
  saved overrides, and `CarPaint.Apply` is a no-op until the override version changes (per-instance state
  lives in a `ConditionalWeakTable`).
- **The original colours are not recoverable from the file at runtime** (cars arrive from radpacks), so
  `CarPaint` captures a slot's colour before the first rewrite. That capture is what makes changing or
  removing an override work; a rewrite that starts from the current polys would compound.
- **A live `CarVisual` does not see the rewrite.** `Mesh` copies `rad.Polys` at construction (its
  `OriginalPolys` is the rad's *array reference*, so only `Polys` is stale). Copy the colours into
  `visual.Mesh.Polys` and call `RebuildMesh()` — replacing the `BackendCar` also works but rebuilds the whole
  mesh per slider tick.
- **`Rad3d.Colors` is declaration-key order, not slot-indexed**, and a duplicate RGB collapses in the
  parser's dictionary. Derive slots from `Rad3dPoly.ColNum`, never from `Colors[slot]`.
- Shipped cars declare only `1stColor`/`2ndColor`; `3rdColor`/`4thColor` are supported but unused.
