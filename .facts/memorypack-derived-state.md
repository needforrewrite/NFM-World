# MemoryPack derived state: compute lazily, never in an initializer

`[MemoryPackable]` types are rebuilt through their `[MemoryPackConstructor]` (often a
parameterless `private X() : this([])`) and their `[MemoryPackOrder]` properties are assigned
*afterwards*. A value computed in a field/property initializer therefore keeps whatever the empty
constructor arguments produced.

`Rad3d.MaxRadius` was `{ get; } = CalculateMaxRadius(polys)`:

- `RadParser` uses the real primary constructor, so shipped `.rad` cars and stage pieces were fine.
- MemoryPack paths were not: `.radpack` assets, archive/stock content, and the stage and car
  embedded in a `.timetrial`. All of those got `MaxRadius == 0`.
- `Mesh.MaxRadius = rad.MaxRadius`, so `MeshedGameObject.SubmitDraws` built
  `new BoundingSphere(MatrixWorld.Translation, 0)`, and `RenderQueue.AddInstanced` culled any piece
  whose *origin* left the frustum. Symptom: cars and stage pieces vanished while still on screen,
  most obvious in replay playback because the viewer always loads the embedded stage/car.

Fix: derive it lazily and cache keyed on the `Polys` array reference (`NFMWorld.Library/Mad/Rad/Rad3d.cs`).
Damage mutating polys in place deliberately does not invalidate it — the radius delta is negligible
and `Mesh` copies the value when it is built.

Check this whenever a serialized type grows a computed member. `Rad3dCollisionMesh.Aabb` and
`Rad3dWheelDef.Sparkat`/`Ground` are safe only because those are record structs that MemoryPack
deserializes through the real constructor. `Rad3dPoly._hashCode` and `Rad3d._hashCode`/
`_visualHashCode` are still computed in the constructor, so deserialized instances hash as if empty —
a correctness-neutral collision, but a dictionary-key bucket collapse.

Regression test: `NFMWorld.Library.Test/RadpackContentTests.RoundTrip_Model_KeepsComputedBounds`.
