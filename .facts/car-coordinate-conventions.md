# Car coordinate conventions

Everything a gamemode does with car space must agree with these, or it ships inverted.
All of it was established by measurement (`CarPhysicsArcadeHookTests`, `RocketArcadeBehaviourTests`),
not by reading the trig — the trig reads plausibly either way and cost three round trips to
get wrong. **When in doubt, assert against `CarPhysics.NoseDirection` in a test rather than
reasoning about signs.**

## World axes

- **Y is positive downward.** `World.Ground` is a floor constant (250) and a car spawns at
  `World.Ground - GroundAt`. So "up" is a *negative* Y impulse, and rising means the position
  value *decreases*.
- **At yaw 0 the car's nose points +Z.** Driving forward moves the car towards +Z.
- **Screen-right is +X at yaw 0.** `FollowCamera` sits at `Z - followDistance` looking towards
  +Z, so the camera's right-hand direction is world +X. This is the rule to check any
  left/right question against.

## Yaw

- `Rotation.Xz` (yaw) **increases counter-clockwise**: adding to it swings the nose from +Z
  towards **-X**, which the player sees as turning **left**.
- Grounded steering agrees: `control.Right` *decreases* `WheelXz`/`Xz`.
- Therefore `CarPhysics.TurnYaw(degrees)` takes a **screen-relative** amount and negates
  internally — positive = the player's right. Do not add to `Xz` directly.

## Model space (the car's own frame)

- **+Z is the nose, +Y is down, +X is the car's right.**
- `CarPhysics.ToWorldAxis(x, y, z)` rotates a model axis to world with the exact `UMath.Rot`
  sequence `Drive` uses for the wheel contact points: xy by `Pxy`, yz by `Pzy`, then xz by yaw.
  Using the same transform is what keeps a derived axis agreeing with the rendered car — a
  hand-written trig form will disagree at pitch/roll extremes and on flips.
- `AddLocalImpulse(forward, right, up)` maps `(right, -up, forward)` through it: only **up** is
  negated (because model +Y is down). Negating `right` as well inverts every sideways kick.
- `NoseDirection` is model +Z through the same transform. It is **not** `Mxz`: `Mxz` is derived
  from wheel velocity, so after a spin or flip it describes where the car is *sliding*, not
  where it *faces*. Anything driven along the heading (boost, steering assist) must use
  `NoseDirection`.

## Stunt / airborne state

- `Drive` zeroes `StuntState` whenever `Mtouch` is set (i.e. every grounded tick), so a
  gamemode that wants a jump to start a stunt must set **both** `Mtouch = false` and
  `StuntState = 1`. Setting `StuntState` alone is wiped on the same tick. There is no engine
  helper for this; it is the gamemode's job.
- `StuntState`: 0 grounded, 1 take-off (next tick promotes it to 2), 2 airborne, -1 wall-riding.
- Wheel velocities are per-wheel (`Scx/Scy/Scz`); `SummedWheelVelocity` is the plugin-safe
  accessor because `InlineArray4` element access is refused by the sandbox.
