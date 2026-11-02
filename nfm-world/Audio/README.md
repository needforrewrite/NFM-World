# Car audio

Each `MadSfx` owns spatial instances of the shared `SfxLibrary` samples. Engines,
air, impacts, scrapes, tires, road/dirt skids and destruction sounds use these
instances; countdown, checkpoint, power-up and music playback remain non-positional.
Power-up notifications play only for the locally controlled car.
The fix-hoop sound plays once per repair animation as a spatial sound that follows
the repaired car, including opponents.
In-race music/SFX mute bindings (M/N by default) toggle their output independently
without changing the configured volume sliders. Mute All still takes precedence.
`wasted.wav` loops during the visual white-spark phase. It stops and
`firewasted.wav` plays once when the orange flames begin. Repair resets this phase.
Stereo car samples are downmixed to mono once and shared between instances.

`ClientStage.GameTick` updates the listener from the camera. Loops and road/dirt
skid sounds follow their car; impacts stay at their contact position throughout
playback, including delayed crashes. Other one-shots keep their starting position.
Desktop voices use F3DAudio
speaker matrices; browser voices use an HRTF PannerNode. `SoundSpatialization`
defines their common distance curve in game units: full volume through 1200,
inverse-distance falloff, then a fade from 12000 to silence at 18000. Global SFX
volume multiplies this gain. Doppler and geometry occlusion are not applied.

Car disposal stops and releases its instances. Completed sounds leave the active
voice lists. Pausing affects SFX independently of music. If F3DAudio exports are
unavailable, desktop audio logs an error and keeps distance attenuation.

Crash notifications from both cars and damage axes are grouped by the physics
contact point before playback. The strongest impact selects one crash/lowcrash
sound at that point. Distinct contacts retain their cooldowns and positions;
unrelated cars' impacts remain independent.
An additional crash/lowcrash sound involving the same car waits 0.1 seconds,
rounded up to a physics tick. Each car has at most one pending follow-up; further
points in that collision burst select its strongest impact without extending the
delay. A contact must stop reporting impacts before it can retrigger. Unrelated
cars are not delayed.
Tire-landing notifications from multiple wheels are grouped per car and share a
short landing cooldown, including when front and rear wheels land on different ticks.
Remote and AI cars also emit crash, scrape and skid notifications. Collision
responses, damage and deterministic random draws remain unchanged.
Skid squeals require meaningful lateral tire slip;
small steering corrections and longitudinal wheel-speed differences do not
qualify. Skid playback stops when the slide ends.
Drift air sounds require average wheel slip of at least 35% and 25 lateral speed
units, sustained for 0.1 seconds. Ordinary grounded turns retain normal engine
playback; partial wheel contact does not trigger airborne sounds. The low-speed
burnout sound remains separate.

Checks without building or starting the game:

```
node NFMWorld.Browser/tests/audio-spatial.test.mjs
python NFMWorld.Audio.FAudio.Smoke/spatial_math.py
```

The JavaScript harness checks node routing and independent playback using a fake
AudioContext. The native check exercises the shipped library's mixing math without
creating an audio device. C# distance-curve tests live in `SoundSpatializationTests`.
In-game direction, volume balance, pause/garage/race transitions and macOS playback
still require listening checks.
