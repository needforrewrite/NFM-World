# phy-addons stage parity: starts, pulse, flames, fset/fchk, piles, converging walls

Read when touching `StageLoader` parsing, spawn placement, the stage editor's serialisation, or the
PvP `walls` option.

- **Spawn overrides** are stored as `StageStart(axis, playerCount, values)` in file order. phy applies
  a line only when its count equals `xt.nplayers`; `startheight` is a wildcard (`PlayerCount == 0`,
  value = `250 - height`). `BackendStage.GetStartTransform(index, playerCount)` walks `Starts` and
  lets later lines win. The Y it returns is the spawn *plane*; a caller subtracts the car's `GroundAt`
  and assigns `Rotation` after the `BackendCar` is constructed (the `HandleAerialFall` pattern).
  `GetStartPosition` was deleted: it only knew the stock 3-column grid.
- **`pulsegrnd`** can't be baked: `Ground` and `GroundPolys` upload immutable vertex colours. The
  factor `a + b·sin(t·c)` therefore rides `Ground.fx`'s `Tint` uniform, and `World.GroundColor` is
  scaled for `CGround` polys (drawn with that value as a uniform). The renderer keeps the un-pulsed
  `GroundColor`/`GroundPolysColor` pair; the editor must call `RefreshGroundPulseBase()` after live
  ground edits or the pulse keeps scaling the previous pair.
- **Flames** use per-channel snap divisors (`flameSnapDiv`, stock 4/3/2). The old code hardcoded
  `Snap/400`, `Snap/300`, `Snap/200`, which is only correct for the stock divisors.
  `flameColor3` is an extension: phy hardcodes the pre-fire ember colour at 230,230,230.
- **`fset`/`fchk`** are not about decimals — phy's `getint` would reject a fractional value. They
  swap the argument order: absolute height at index 3, angle at index 4.
- **Piles** (`pile(seed,w,h,x,z)`) are procedural clusters, not model files. `StagePileBuilder` ports
  the `ContO` constructor (seeded blob, 4 side planes + canopy, 5 sloped boxes). `URandom(seed)` is
  the Java LCG, so generation is identical across peers and runs; `PiecePlacement` keeps the
  seed/size so the editor writes `pile(...)` back.
- **Converging walls** (`Walls.java`) shrink the four boundary walls after N minutes:
  `areaMul = pow(0.5, max(((mins + 5 - minutesToGo)/5 - 1)*3, 0))`, `sideMul = sqrt(areaMul)`, each
  side clamped to `MIN_DIST = 3600`. Out-of-bounds damage is `maxmag/800` per original tick, scaled
  by the stage's track-damage multiplier. The stage centre is a random wall-option piece (`set` ids
  28/29/33/34/35/53/55); a stable FNV hash of the stage name picks it, because the client and the
  game master must agree and `string.GetHashCode` is randomised per process.
- **Moving walls and the collision quadtree**: `BackendStage` builds the quad tree once, with no
  removal. The moving boundaries are appended per query in `RetrievePointCollidables`, with a high
  index so they sort after the static geometry; the wall *pieces* are moved as `StageObject`
  positions, which `StageObjectGameObject.GameTick` copies into the transform.
- **Sandbox**: `StartTransform` and `ConvergingWalls` are `Visible.IncludingAllMembersOfType`
  (`[PluginVisible]` alone does not expose record-generated properties), and `HudMessageKind` had to
  be made plugin-visible for `BaseClientGamemode.Announce`. `AiPluginTests`
  `BeginPvp_WithConvergingWalls_PassesTheSandbox` is the regression that proves the PvP wall path
  survives the policy.
