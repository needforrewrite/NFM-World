using System.Numerics;
using System.Reflection;
using NFMWorld.Sentry;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Rad;

namespace NFMWorld.Library.Test;

/// <summary>
/// FX must stay alive for a client race and actually emit geometry: a construction failure, an
/// accidental dispose after setup, or a puff that never gets a lifetime all leave the rocket mode
/// silent and dust-free, and nothing else reports it.
/// </summary>
[TestClass]
public class RocketFxTests
{
    private static readonly string CarName = $"fx-test/{Guid.NewGuid():N}";

    [ClassInitialize]
    public static void InitSentry(TestContext _)
    {
        try
        {
            SentrySdk.Init(options =>
                {
                    options.Dsn = "https://public@localhost/1";
                    options.TracesSampleRate = 0;
                }
            );
        }
        catch (InvalidOperationException)
        {
        }
    }

    [TestMethod]
    public void BoostEmitsDustGeometryAfterSetup()
    {
        TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        // The IBackend getter throws when unset, so no previous value can be captured; install
        // the dummy and leave it, matching PluginSandboxTests.
        var backend = new RecordingBackend();
        IBackend.Backend = backend;
        var plugin = GamemodePlugin.Load("nfmm/rocket", "data/gamemodes/nfmm/rocket/gamemode.dll")!;
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        BackendGameSparker.dynamic_models.Add(CarName, (0, rad));
        var gamemode = plugin.CreateClient(
            new ClientGamemodeParameters
            {
                Players =
                [
                    new ClientSidePlayerInfo
                    {
                        PlayerName = "H", CarName = CarName, Color = default, IsBot = false, IsClientPlayer = true
                    }
                ]
            },
            new Ctx()
        );

        try
        {
            gamemode.Begin();
            var fx = GetFx(gamemode);
            Assert.IsNotNull(fx, "client setup must leave the FX live");

            for (var i = 0; i < GamemodeUtils.CountdownInterval * 4; i++)
                gamemode.GameTick();

            var car = gamemode.ClientPlayer!.Car!;
            car.Control.SetGamemodeAction(1, true);
            gamemode.GameTick();

            Assert.IsTrue(LivePuffs(fx) > 0, "holding boost must spawn dust puffs");

            // The puffs must stay around the wheels. Inheriting the car's velocity threw them
            // hundreds of units behind within a few ticks, so only a distant trail was visible.
            for (var i = 0; i < 6; i++)
                gamemode.GameTick();
            var maxDistance = FarthestPuffDistance(fx, car);
            Assert.IsTrue(
                maxDistance < 400f,
                $"dust must stay near the car (farthest puff was {maxDistance:F0} units away)"
            );

            // Render builds the triangles; the batch receives them through IGraphics3D.
            gamemode.Render();
            Assert.IsTrue(
                backend.TriangleVertices > 0,
                $"boost dust must reach DrawTriangles (saw {backend.TriangleVertices} vertices)"
            );
            Assert.IsTrue(backend.TriangleIndices > 0, "the dust fan must be indexed into triangles");

            // Release boost, then let the puffs run out their life.
            car.Control.SetGamemodeAction(1, false);
            for (var i = 0; i < 40; i++)
                gamemode.GameTick();
            Assert.AreEqual(0, LivePuffs(fx), "puffs must expire once boost stops");
        }
        finally
        {
            gamemode.End();
            BackendGameSparker.dynamic_models.Remove(CarName);
            plugin.Unload();
        }
    }

    private static object? GetFx(object gamemode) =>
        gamemode.GetType().GetField("_fx", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(gamemode);

    private static int LivePuffs(object fx)
    {
        var states = (Array)fx.GetType()
            .GetField("_states", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(fx)!;
        var total = 0;

        foreach (var state in states)
        {
            var stages = (int[])state.GetType()
                .GetField("DustStage", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(state)!;
            total += stages.Count(s => s > 0);
        }

        return total;
    }

    /// <summary>How far the farthest live puff is from the car, in world XZ units.</summary>
    private static float FarthestPuffDistance(object fx, BackendCar car)
    {
        var states = (Array)fx.GetType()
            .GetField("_states", BindingFlags.NonPublic | BindingFlags.Instance)!
            .GetValue(fx)!;
        var cx = (float)car.Position.X;
        var cz = (float)car.Position.Z;
        var farthest = 0f;

        foreach (var state in states)
        {
            var stages = (int[])state.GetType()
                .GetField("DustStage", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(state)!;
            var xs = (float[])state.GetType()
                .GetField("DustX", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(state)!;
            var zs = (float[])state.GetType()
                .GetField("DustZ", BindingFlags.Public | BindingFlags.Instance)!
                .GetValue(state)!;

            for (var i = 0; i < stages.Length; i++)
            {
                if (stages[i] == 0)

                    continue;
                var dx = xs[i] - cx;
                var dz = zs[i] - cz;
                farthest = System.MathF.Max(farthest, System.MathF.Sqrt(dx * dx + dz * dz));
            }
        }

        return farthest;
    }

    /// <summary>Counts what reaches the 3D batch, so "it renders" is an assertion, not a guess.</summary>
    private sealed class RecordingBackend : DummyBackend
    {
        public int TriangleVertices => _recorder.Vertices;
        public int TriangleIndices => _recorder.Indices;

        public override IGraphics3D Graphics3D => _recorder;

        private readonly Recorder _recorder = new();

        private sealed class Recorder : IGraphics3D
        {
            public int Vertices;
            public int Indices;

            public void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color)
            {
            }

            public void DrawTriangles(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices)
            {
                Vertices += positions.Length;
                Indices += indices.Length;
            }

            public void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<int> indices, Color color)
            {
            }

            public void DrawLines(ReadOnlySpan<Vector3> positions, ReadOnlySpan<Color> colors, ReadOnlySpan<int> indices)
            {
            }
        }
    }

    private sealed class Ctx(string stageName = "fx-test") : IGamemodeContext
    {
        private BackendStage? _stage;
        public BackendStage CurrentStage => _stage ??= new BackendStage(stageName);
        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks { get; } = new Cb();

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class Cb : IClientCallbacks
    {
        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => throw new NotSupportedException();
    }
}
