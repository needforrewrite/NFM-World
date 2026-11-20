# Collision lift is per contacting pair, and the tilt is a plane-fit problem

The original game's `Mad.colide` applies every vertical collision impulse once per contacting
wheel pair. A wheel that overlaps several of the other car's wheels therefore takes the kick
several times — measured on drmonster rear-ending 2000tornados, one wheel takes 4x
(`Scy` = `[-128, -32, -32, -32]`).

Two successive fixes went wrong here, and the second is what "wasting feels odd, cars are glued
to the ground, R1 barely lifts anyone" was:

1. `de5b8189` gated each impulse to once per wheel. That removed the 2-4x overlap, but the
   vertical traction clamp in `Drive` pulls `Scy` toward `speedy` at `_effective.Grip` per tick.
   A single 1x kick (lift 30) is smaller than that clamp for a grippy car, so it was erased in
   the same tick and the car never rose. Measured single hit, lift 30: victim grip 20 rises 37,
   victim grip 100 rises **9** — R1 has `grip(100)`, which is exactly why it "pushes fine but
   barely lifts anyone". Push is unaffected because it goes through `Scx`/`Scz`, not the clamp.
2. A shared per-wheel counter for the victim lift and the revlift damage made it worse: the
   `Regy` gate sits inside the `Scx`/`Scz` branches, the lift impulse outside them, so a wheel
   firing only one branch had its lift silently skipped.

The real cause of the tilt the gate was added to fix is the **plane fit**: it reconstructed the
car's orientation from all four wheel heights, so a kicked wheel (collision velocity) read as
terrain slope and pitched the car. The original never fit orientation from wheel heights.

Fix: restore the stock per-pair impulses (no gates at all). With 3-4 wheels on a surface, fit
orientation from only the grounded wheels. With fewer, the fit still runs (it is what tips
nose/tail/side balances over), but its per-tick change is clamped to the original's per-pair
rule (`Mad.java` ~1270): a stretched wheel pair turns at most 1° per original tick, a
compressed one by `acos(length / rest)`. A revlift kick lifts the contacting end, which only
stretches the pairs, so it turns slowly. Fitting only grounded wheels was not enough: the
dominant car's kicked front pair leaves 2 grounded, which fell back to the snapping fit and
pitched drmonster to 80°; clamped it peaks at 16°. Lift (victim) measured R1 -> Max Revenge
rise 472 (was 9).

`CarPhysicsCollisionTests` covers it: `Collide_AppliesRevliftOncePerContactingWheelPair`,
`Collide_DominantCarLiftsVictimEvenWhenOnlyZBranchFires`, and
`Collide_RevliftKickDoesNotStandTheDominantCarOnEnd` (pitch stays under 45°).
