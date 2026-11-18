# Collision lift counters must not share the revlift-damage gate

`CarPhysics.Collide` applies two *independent* vertical impulses when one car dominates
another:

1. the dominant car's **own** revlift kick — its wheels' `Scy -= ownStats.Revlift * recoil`,
   paired with the victim's revlift **damage** (`Regy(liftY * 7)`);
2. the **victim's** own lift kick — the victim's wheels' `Scy -= ownStats.Lift`, applied
   per contacting pair outside both momentum branches.

The once-per-wheel gates added by `de5b8189` (the plane-fit tilt fix) gave both impulses a
**shared** `otherLiftCount`. The gate is harmless when both momentum branches fire (each
impulse is applied at most once per wheel anyway), but the two gates sit in different
places: the `Regy` gate is *inside* the `Scx`- / `Scz`-dominant branches, while the victim
lift gate is *outside* them. A wheel that contacts but only fires one branch — e.g. only
the Z branch when the victim still holds more x-momentum than the attacker, which is the
normal rear/side hit — has its lift gate consumed by the single branch that fires, and the
`Lift` impulse is silently skipped for that wheel.

Symptom: a lift car "pushes fine but barely lifts anyone" — the shove goes through the
momentum branch, the lift impulse never does. Verified in
`CarPhysicsCollisionTests.Collide_DominantCarLiftsVictimEvenWhenOnlyZBranchFires`
(radicalone lift 30 rear-hits 2000tornados, X branch skipped, Z branch fires): with the
shared counter the victim's contacting wheels dropped `0,0,0,0`; after the fix `0,0,30,30`.

The original NFM applies both impulses unconditionally per contacting wheel pair, so the
gate is our deviation either way — the per-wheel cap only exists to stop the tilt, and it
must cap each impulse with its own counter (`ownLiftCount` / `ownRegyCount` for the
dominant car's kick + damage, `victimLiftCount` for the victim's lift).
