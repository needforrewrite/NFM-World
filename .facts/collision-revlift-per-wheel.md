# Collision revlift must be applied once per wheel

`CarPhysics.Collide` walks all 16 wheel pairs and, for every contacting pair, subtracts the
dominant car's `Revlift` from its own wheel's `Scy` and the victim's `Lift` from the victim's
wheel. A tall car whose wheels all overlap a small car's wheel meets several pairs at once, so
that wheel got the impulse 2–4× while its neighbours got it once — measured on drmonster vs
2000tornados: `Scy` went to `[-128, -32, -32, -32]` (4× on one wheel).

That unevenness alone is old behaviour, but the plane-fit orientation pass
(`FitTerrainAngles` over the four wheel positions) reads a single low wheel as a tilted surface
and snaps `Pxy`/`Pzy` to it. The car pitches nose-up or nose-down out of a plain rear-end hit,
which is what the plane-fit regression looked like. The original game never showed it because
its landing adjustment used `Scy` differences only to nudge angles while airborne; it never
reconstructed the whole car orientation from wheel heights.

Fix: per-wheel counters (`ownLiftCount`/`otherLiftCount`) in `Collide` gate both the `Scy`
impulse and the matching `Regy` damage so each wheel takes the kick at most once per collision
pass. `CarPhysicsCollisionTests.Collide_DominantCarTakesOneVerticalImpulsePerWheel` asserts a
single `Revlift * recoil` drop per wheel; without the gate wheel 0 takes 4×.

Related: `_effective` (stage-adjusted stats) is only populated by `Drive`, so a test that calls
`Collide` on a freshly built car sees all-zero `Moment` and never enters the impulse branch —
drive each car once first.
