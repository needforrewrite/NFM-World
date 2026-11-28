using System.Numerics;
using System.Runtime.InteropServices;
using FixedMathSharp;
using Maxine.Extensions.Mathematics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;

namespace NFMWorld.Gamemode.Rocket;

/// <summary>
/// Boost dust and jump FX. The dust follows the game's own <c>Dust.cs</c> recipe: a ring of
/// puffs, each an 8-vertex triangle fan hugging the ground, expanding outward over its life and
/// fading to clear, inheriting the wheel's speed. Jump pops one larger warm fan at the car's
/// centre. Drawn through the plugin-visible <see cref="IBackend.Backend.Graphics3D"/> batch and
/// sounded through <see cref="IBackend.Backend.GetSound"/>.
/// </summary>
public sealed class RocketFx : IDisposable
{
    // The original dust uses a ring of 20 puffs with an 8-stage life; 32 covers a boosting car
    // emitting one puff per rear wheel per tick (63 tps) without overwriting a live puff.
    private const int PuffCapacity = 32;
    private const int PuffLifeStages = 8;
    private const int PuffVertices = 8;

    private readonly List<BackendCar> _cars;
    private readonly CarVisualState[] _states;

    private readonly ISoundClip? _boostLoop;
    private readonly ISoundClip? _jumpSound;
    private readonly List<ISoundClip> _jumpInstances = [];

    private sealed class CarVisualState
    {
        // Boost dust in world space. Stage 1 is the birth frame; 0 means dead.
        public float[] DustX = new float[PuffCapacity];
        public float[] DustY = new float[PuffCapacity];
        public float[] DustZ = new float[PuffCapacity];
        public int[] DustStage = new int[PuffCapacity];
        public float[] DustScx = new float[PuffCapacity];
        public float[] DustScz = new float[PuffCapacity];
        public float[,] DustMag = new float[PuffCapacity, PuffVertices];
        public int DustCursor;

        // One-shot jump burst: the same fan, larger, at the car's contact point.
        public float BurstX, BurstZ;
        public int BurstStage;
        public float[] BurstMag = new float[PuffVertices];

        public bool Boosting;
        public ISoundClip? BoostSoundInstance;
    }

    /// <param name="cars">The roster's cars, in player-index order.</param>
    public RocketFx(IEnumerable<BackendCar> cars)
    {
        _cars = [.. cars];
        _states = new CarVisualState[_cars.Count];
        for (var i = 0; i < _cars.Count; i++)
            _states[i] = new CarVisualState();

        // Plugin-visible IBackend surface: the sounds ship inside this plugin's own folder
        // (data/gamemodes/<id>/sound/) and load through the game's VFS data root.
        _boostLoop = IBackend.Backend.GetSound("data/gamemodes/nfmm/rocket/sound/rocket_boost.wav");
        _jumpSound = IBackend.Backend.GetSound("data/gamemodes/nfmm/rocket/sound/rocket_jump.wav");
    }

    /// <summary>
    /// Advances dust and refreshes boost-loop state. Call once per game tick, after physics;
    /// <paramref name="boosting"/> is indexed by player index.
    /// </summary>
    public void GameTick(IReadOnlyList<bool> boosting)
    {
        for (var i = 0; i < _states.Length; i++)
        {
            var state = _states[i];
            var car = _cars[i];

            if (car.HasLeftRace)
            {
                state.Boosting = false;
                state.BoostSoundInstance?.Stop();

                continue;
            }

            var wantBoost = i < boosting.Count && boosting[i];

            if (wantBoost && !state.Boosting)
            {
                state.Boosting = true;
                state.BoostSoundInstance?.Stop();
                state.BoostSoundInstance?.Dispose();
                // A spatial loop follows the car; a plain clip would play from the world centre.
                state.BoostSoundInstance = _boostLoop?.CreateSpatialInstance(followPosition: true);
                state.BoostSoundInstance?.SetPosition(CarCenter(car));
                state.BoostSoundInstance?.Loop();
            }
            else if (!wantBoost && state.Boosting)
            {
                state.Boosting = false;
                state.BoostSoundInstance?.Stop();
            }

            if (state.BoostSoundInstance is { } loop)
                loop.SetPosition(CarCenter(car));

            EmitDust(car, state, wantBoost);
            StepDust(state);
            if (state.BurstStage > 0 && state.BurstStage < PuffLifeStages)
                state.BurstStage++;
            else if (state.BurstStage >= PuffLifeStages)
                state.BurstStage = 0;
        }
    }

    /// <summary>Plays the jump sound and pops the burst. Call from the jump path, once per jump.</summary>
    public void PlayJump(BackendCar car)
    {
        var state = FindState(car);

        if (state is not null)
        {
            state.BurstX = (float)car.Position.X;
            state.BurstZ = (float)car.Position.Z;
            state.BurstStage = 1;
            for (var v = 0; v < PuffVertices; v++)
                state.BurstMag[v] = 15f + Random.Shared.NextSingle() * 15f;
        }

        var instance = _jumpSound?.CreateSpatialInstance(followPosition: true);

        if (instance is null)

            return;
        instance.SetPosition(CarCenter(car));
        instance.Play();
        _jumpInstances.Add(instance);
    }

    private CarVisualState? FindState(BackendCar car)
    {
        for (var i = 0; i < _cars.Count; i++)

            if (ReferenceEquals(_cars[i], car))

                return _states[i];

        return null;
    }

    private static Vector3 CarCenter(BackendCar car) => new(
        (float)car.Position.X,
        (float)car.Position.Y + 20f,
        (float)car.Position.Z
    );

    private static void EmitDust(BackendCar car, CarVisualState state, bool boosting)
    {
        if (!boosting || car.Wheels.Count < 4)

            return;

        // One puff per rear wheel per tick. The wheel's definition position is model space,
        // rotated to world by the engine's own transform so the puff sits under the tire.

        for (var w = 2; w < 4; w++)
        {
            var slot = state.DustCursor;
            state.DustCursor = (state.DustCursor + 1) % PuffCapacity;

            var world = car.CarPhysics.ModelOffsetToWorld(car.Wheels[w].Position);
            state.DustX[slot] = (float)car.Position.X + (float)world.X;
            state.DustZ[slot] = (float)car.Position.Z + (float)world.Z;
            // The puff hugs the ground: Y is clamped to the world floor (positive-down).
            state.DustY[slot] = System.Math.Min((float)car.Position.Y + (float)world.Y, World.Ground);
            state.DustStage[slot] = 1;
            // Puffs barely move from where they spawn: a small, fixed backward scatter, not the
            // car's own velocity. Inheriting it (even halved) threw each puff hundreds of units
            // behind within a few ticks, so nothing was ever visible near the wheels. The old
            // line particles drifted ~14 units over their whole life and read as trailing dust.
            var nose = (Vector3)car.CarPhysics.NoseDirection;
            state.DustScx[slot] = -nose.X * 0.6f;
            state.DustScz[slot] = -nose.Z * 0.6f;
            for (var v = 0; v < PuffVertices; v++)
                state.DustMag[slot, v] = 1.5f + Random.Shared.NextSingle() * 50f;
        }
    }

    private static void StepDust(CarVisualState state)
    {
        for (var d = 0; d < PuffCapacity; d++)
        {
            if (state.DustStage[d] == 0)

                continue;

            // Smooth each radius against its neighbours so the fan is blobby, not spiky - the
            // original dust does the same average over its 8 magnitudes.

            for (var v = 0; v < PuffVertices; v++)
            {
                var prev = v == 0 ? PuffVertices - 1 : v - 1;
                var next = v == PuffVertices - 1 ? 0 : v + 1;
                state.DustMag[d, v] = (state.DustMag[d, prev] + state.DustMag[d, next]) / 2f
                                      + state.DustMag[d, v];
                state.DustMag[d, v] /= 2f;
            }

            // Drift the puff backwards, fading with the stage the way the original divides the
            // velocity by (stage + 1).
            state.DustX[d] += state.DustScx[d] / (state.DustStage[d] + 1);
            state.DustZ[d] += state.DustScz[d] / (state.DustStage[d] + 1);
            // Grow slowly: the puff should stay compact over its short life, not billow out.
            for (var v = 0; v < PuffVertices - 1; v++)
                state.DustMag[d, v] += 0.4f + Random.Shared.NextSingle() * 1.2f;
            state.DustMag[d, PuffVertices - 1] = state.DustMag[d, PuffVertices - 2];

            if (state.DustStage[d] >= PuffLifeStages)
                state.DustStage[d] = 0;
            else
                state.DustStage[d]++;
        }
    }

    private List<Vector3> positions = [];
    private List<Color> colors = [];
    private List<int> indices = [];

    /// <summary>
    /// Draws the dust and jump burst. Call from the gamemode's Render() - the host flushes the
    /// 3D batch right after, so anything drawn here lands in the frame.
    /// </summary>
    public void Render()
    {
        positions.Clear();
        colors.Clear();
        indices.Clear();
        // The warm palette from the earlier line particles: bright yellow fresh, orange as it
        // cools, dark ember when spent. The jump burst starts at the same bright tone.
        var mag = new float[PuffVertices];

        for (var c = 0; c < _states.Length; c++)
        {
            var state = _states[c];

            for (var d = 0; d < PuffCapacity; d++)
            {
                if (state.DustStage[d] == 0)

                    continue;
                for (var v = 0; v < PuffVertices; v++)
                    mag[v] = state.DustMag[d, v];

                AppendFan(
                    state.DustX[d],
                    state.DustY[d],
                    state.DustZ[d],
                    mag,
                    DustColor(state.DustStage[d]),
                    FadeAlpha(state.DustStage[d]),
                    positions,
                    colors,
                    indices
                );
            }

            if (state.BurstStage > 0)
            {
                AppendFan(
                    state.BurstX,
                    World.Ground,
                    state.BurstZ,
                    state.BurstMag,
                    DustColor(state.BurstStage),
                    FadeAlpha(state.BurstStage),
                    positions,
                    colors,
                    indices
                );
            }
        }

        if (indices.Count > 0)
            TheGraphics.G3D.DrawTriangles(
                CollectionsMarshal.AsSpan(positions),
                CollectionsMarshal.AsSpan(colors),
                CollectionsMarshal.AsSpan(indices)
            );

        // One-shot jump sounds whose playback ended: dispose so the handle goes back.

        for (var i = _jumpInstances.Count - 1; i >= 0; i--)
        {
            if (_jumpInstances[i].IsActive)

                continue;
            _jumpInstances[i].Dispose();
            _jumpInstances.RemoveAt(i);
        }
    }

    /// <summary>
    /// The exhaust palette: bright yellow while fresh, orange as it cools, dark ember when spent.
    /// Stage 1 is the birth frame.
    /// </summary>
    private static Color DustColor(int stage)
    {
        var age = (stage - 1) / (float)PuffLifeStages;

        return age < 0.4f
            ? new Color(255, 220, 110)
            : age < 0.7f ? new Color(255, 140, 50)
            : new Color(180, 80, 30);
    }

    /// <summary>Alpha for a puff at <paramref name="stage"/> (1 = fresh): fades out over its life.</summary>
    private static byte FadeAlpha(int stage) => (byte)(160 * (PuffLifeStages - stage + 1) / PuffLifeStages);

    /// <summary>
    /// Appends an 8-vertex ground-hugging triangle fan: one centre vertex plus a ring of outer
    /// vertices at per-vertex radii, fanned into triangles. All vertices share the given alpha.
    /// </summary>
    private static void AppendFan(
        float cx,
        float cy,
        float cz,
        ReadOnlySpan<float> mag,
        Color baseColor,
        byte alpha,
        List<Vector3> positions,
        List<Color> colors,
        List<int> indices
    )
    {
        var withAlpha = new Color(baseColor.R, baseColor.G, baseColor.B, alpha);
        var center = positions.Count;
        positions.Add(new Vector3(cx, cy, cz));
        colors.Add(withAlpha);

        for (var v = 0; v < PuffVertices; v++)
        {
            var angle = v * 3.14159265f / 4f;
            positions.Add(new Vector3(
                cx + mag[v] * System.MathF.Cos(angle),
                cy,
                cz + mag[v] * System.MathF.Sin(angle)
            )
            );
            colors.Add(withAlpha);
        }

        for (var v = 0; v < PuffVertices; v++)
        {
            var next = v + 1 == PuffVertices ? 0 : v + 1;
            indices.Add(center);
            indices.Add(center + 1 + v);
            indices.Add(center + 1 + next);
        }
    }

    public void Dispose()
    {
        foreach (var state in _states)
        {
            state.BoostSoundInstance?.Stop();
            state.BoostSoundInstance?.Dispose();
        }

        foreach (var instance in _jumpInstances)
            instance.Dispose();
        _jumpInstances.Clear();

        _boostLoop?.Dispose();
        _jumpSound?.Dispose();
    }
}
