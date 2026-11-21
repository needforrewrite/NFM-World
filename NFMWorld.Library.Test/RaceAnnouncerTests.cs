using FixedMathSharp;
using f64Vector3 = FixedMathSharp.Vector3d;
using NFMWorld.DriverInterface;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Library.Test;

[TestClass]
public class RaceAnnouncerTests
{
    private static readonly string CarName = $"announcer-test/{Guid.NewGuid():N}";

    [ClassInitialize]
    public static void RegisterCar(TestContext testContext)
    {
        var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
        BackendGameSparker.dynamic_models.Add(CarName, (0, rad));
    }

    [ClassCleanup]
    public static void UnregisterCar() => BackendGameSparker.dynamic_models.Remove(CarName);

    [TestMethod]
    public void DescribeStunt_CountsForwardLoops()
    {
        var physics = new CarPhysics(CarStats.Default, 0, true);
        physics.TotalStuntZy = 360;
        StringAssert.Contains(RaceAnnouncer.DescribeStunt(physics) ?? "", "Forward loop");

        physics.TotalStuntZy = 720;
        StringAssert.Contains(RaceAnnouncer.DescribeStunt(physics) ?? "", "double Forward");

        physics.TotalStuntZy = 1440;
        StringAssert.Contains(RaceAnnouncer.DescribeStunt(physics) ?? "", "massive Forward looping");
    }

    [TestMethod]
    public void DescribeStunt_AppendsTheFlipAngle()
    {
        var physics = new CarPhysics(CarStats.Default, 0, true);
        physics.TotalStuntZy = 360;
        physics.TotalStuntXz = 180;
        StringAssert.Contains(RaceAnnouncer.DescribeStunt(physics) ?? "", " by 180");
    }

    [TestMethod]
    public void DescribeStunt_CapsTheFlipAngleAtNineHundred()
    {
        var physics = new CarPhysics(CarStats.Default, 0, true);
        physics.TotalStuntXz = 2000;
        var text = RaceAnnouncer.DescribeStunt(physics) ?? "";
        StringAssert.Contains(text, "900 and beyond");
    }

    [TestMethod]
    public void DescribeStunt_HangsAForwardTabletopOnABackLoop()
    {
        var physics = new CarPhysics(CarStats.Default, 0, true);
        physics.TotalStuntZy = -360;
        physics.ForwardTabletop = true;
        StringAssert.Contains(RaceAnnouncer.DescribeStunt(physics) ?? "", "Hanged");
    }

    [TestMethod]
    public void DescribeStunt_WithNoStunt_ReturnsNull()
    {
        Assert.IsNull(RaceAnnouncer.DescribeStunt(new CarPhysics(CarStats.Default, 0, true)));
    }

    [TestMethod]
    public void FinishedStunt_PostsTheStuntAndThePowerItEarned()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        var physics = car.CarPhysics;
        announcer.Tick(car, gamemode);

        // Both slots are written on the same tick the combo finishes.
        physics.TotalStuntZy = 720;
        physics.Powerup = 49;
        physics.Power = 70;
        physics.TabletopCounter = 10;
        announcer.Tick(car, gamemode);

        StringAssert.Contains(announcer.Stunt?.Text ?? "", "double Forward");
        Assert.AreEqual("Power Up 50%", announcer.Status?.Text);
    }

    [TestMethod]
    public void FinishedStunt_AtMaximumPower_SaysMax()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        var physics = car.CarPhysics;
        announcer.Tick(car, gamemode);

        physics.TotalStuntZy = 720;
        physics.Powerup = 12;
        physics.Power = 98;
        physics.LatestStunt = CompletedStunt.Capture(physics, 1);
        physics.Power = 97;
        physics.TabletopCounter = 10;
        announcer.Tick(car, gamemode);

        Assert.AreEqual("Power to the MAX", announcer.Status?.Text);
    }

    [TestMethod]
    public void UndescribedStunt_AtMaximumPower_DoesNotSayMaxOrCueSpectatorSound()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(car, gamemode);

        var physics = car.CarPhysics;
        physics.Powerup = 30;
        physics.Power = 98;
        physics.TabletopCounter = 10;
        physics.LatestStunt = CompletedStunt.Capture(physics, 1);
        announcer.Tick(car, gamemode);
        feedback.Tick(car, gamemode);

        Assert.IsNull(announcer.Stunt);
        Assert.IsNull(announcer.Status);
        Assert.IsNull(feedback.Stunt);
        Assert.IsNull(feedback.Status);
        Assert.IsFalse(feedback.PlayPowerUp);
    }

    [TestMethod]
    public void CheckpointPostsAndThenExpires()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);

        car.TotalCheckpoint = 1;
        announcer.Tick(car, gamemode);
        Assert.AreEqual("Checkpoint!", announcer.Status?.Text);

        // The checkpoint flash is half a second, not the status line's default.
        var ticks = (int)MathF.Ceiling(0.5f * Physics.TargetTps);
        for (var i = 0; i < ticks - 1; i++) announcer.Tick(car, gamemode);
        Assert.IsNotNull(announcer.Status);
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    [TestMethod]
    public void LapWrapAnnouncesLapsRemaining()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, laps: 3);
        announcer.Tick(car, gamemode);

        car.CurrentLap = 1;
        car.TotalCheckpoint = 3;
        announcer.Tick(car, gamemode);
        Assert.AreEqual("2 laps to go!", announcer.Status?.Text);

        car.CurrentLap = 2;
        car.TotalCheckpoint = 6;
        announcer.Tick(car, gamemode);
        Assert.AreEqual("Final lap!!!", announcer.Status?.Text);
    }

    [TestMethod]
    public void WastedByAnotherCar_NamesTheKiller()
    {
        var (announcer, gamemode, car) = Create(opponents: 2);
        var victim = gamemode.Players[1].Car!;
        var killer = gamemode.Players[2].Car!;
        announcer.Tick(car, gamemode);

        victim.CarPhysics.LastDamageFrom = killer.CarPhysics.Im;
        victim.CarPhysics.Wasted = true;
        announcer.Tick(car, gamemode);

        var text = announcer.Status?.Text ?? "";
        StringAssert.EndsWith(text, "has been wasted by " + gamemode.Players[2].Info.PlayerName + "!");
        StringAssert.StartsWith(text, gamemode.Players[1].Info.PlayerName);
    }

    [TestMethod]
    public void WastedByTheClient_SaysYouWasted()
    {
        var (announcer, gamemode, car) = Create(opponents: 1);
        var victim = gamemode.Players[1].Car!;
        announcer.Tick(car, gamemode);

        victim.CarPhysics.LastDamageFrom = car.CarPhysics.Im;
        victim.CarPhysics.Wasted = true;
        announcer.Tick(car, gamemode);

        Assert.AreEqual($"You wasted {gamemode.Players[1].Info.PlayerName}!", announcer.Status?.Text);
    }

    [TestMethod]
    public void ClientWastedByAnotherCar_NamesTheKiller()
    {
        var (announcer, gamemode, car) = Create(opponents: 1);
        announcer.Tick(car, gamemode);
        car.CarPhysics.LastDamageFrom = gamemode.Players[1].Car!.CarPhysics.Im;
        car.CarPhysics.Wasted = true;

        announcer.Tick(car, gamemode);

        Assert.AreEqual($"You have been wasted by {gamemode.Players[1].Info.PlayerName}!", announcer.Status?.Text);
    }

    [TestMethod]
    public void WastedWithNoKiller_OmitsTheCredit()
    {
        var (announcer, gamemode, car) = Create(opponents: 1);
        var victim = gamemode.Players[1].Car!;
        announcer.Tick(car, gamemode);

        victim.CarPhysics.LastDamageFrom = -1;
        victim.CarPhysics.Wasted = true;
        announcer.Tick(car, gamemode);

        Assert.AreEqual($"{gamemode.Players[1].Info.PlayerName} has been wasted!", announcer.Status?.Text);
    }

    [TestMethod]
    public void WrongWay_NeedsMoreThanASingleTick()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        announcer.Tick(car, gamemode);

        // Pointed straight away from the checkpoint.
        car.CarPhysics.Cxz = 180;
        for (var i = 0; i < 10; i++) announcer.Tick(car, gamemode);
        Assert.AreNotEqual("Wrong Way!", announcer.Status?.Text, "one tick of a bad heading is not a wrong way");

        for (var i = 0; i < (int)(2 * Physics.TargetTps); i++) announcer.Tick(car, gamemode);
        Assert.AreEqual("Wrong Way!", announcer.Status?.Text);
    }

    [TestMethod]
    public void WrongWay_IsSuppressedWhileTheArrowTargetsCars()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        car.Control.Arrace = true;
        car.CarPhysics.Cxz = 180;

        for (var i = 0; i < (int)(2 * Physics.TargetTps); i++) announcer.Tick(car, gamemode);
        Assert.AreNotEqual("Wrong Way!", announcer.Status?.Text);
    }

    [TestMethod]
    public void CheckpointText_IsSuppressedWhileTheArrowTargetsCars()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        car.Control.Arrace = true;
        announcer.Tick(car, gamemode);

        car.TotalCheckpoint = 1;
        announcer.Tick(car, gamemode);
        Assert.AreNotEqual("Checkpoint!", announcer.Status?.Text);
    }

    [TestMethod]
    public void MissedCheckpointText_IsSuppressedWhileTheArrowTargetsCars()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        var checkpoint = gamemode.CurrentStage.Checkpoints[0];
        car.Control.Arrace = true;
        announcer.Tick(car, gamemode);

        car.CarPhysics.Mtouch = true;
        car.Position = new f64Vector3(checkpoint.Position.X + 200, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);

        car.Position = new f64Vector3(checkpoint.Position.X + 12_000, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);
        Assert.AreNotEqual("Checkpoint Missed!", announcer.Status?.Text);
    }

    [TestMethod]
    public void BadLanding_StartsOnImpactAndClearsWhenRighted()
    {
        var (announcer, gamemode, car) = Create(opponents: 0);
        announcer.Tick(car, gamemode);

        // Tilted mid-air is not a landing yet.
        car.CarPhysics.BadLanding = true;
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);

        car.CarPhysics.Mtouch = true;
        announcer.Tick(car, gamemode);
        Assert.AreEqual("Bad Landing!", announcer.Status?.Text);

        // Back on its wheels: the warning goes on the same tick, not on the status timer.
        car.CarPhysics.BadLanding = false;
        car.CarPhysics.Mtouch = false;
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    [TestMethod]
    public void MissedCheckpoint_WarnsOnceAfterGettingCloseThenLeaving()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        var checkpoint = gamemode.CurrentStage.Checkpoints[0];
        announcer.Tick(car, gamemode);

        PassNear(car, checkpoint, announcer, gamemode);
        Assert.IsNull(announcer.Status, "being near a checkpoint is not a miss");

        car.Position = new f64Vector3(checkpoint.Position.X + 12_000, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);
        Assert.AreEqual("Checkpoint Missed!", announcer.Status?.Text);

        // Nothing new to say until the next checkpoint is crossed.
        for (var i = 0; i < (int)(4 * Physics.TargetTps); i++) announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    [TestMethod]
    public void MissedCheckpoint_DoesNotFireJustBecauseTheCarIsFarAway()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        var checkpoint = gamemode.CurrentStage.Checkpoints[0];

        // The grid is thousands of units from the first checkpoint; that is a start, not a miss.
        car.CarPhysics.Mtouch = true;
        car.Position = new f64Vector3(checkpoint.Position.X + 12_000, checkpoint.Position.Y, checkpoint.Position.Z);
        for (var i = 0; i < (int)Physics.TargetTps; i++) announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    [TestMethod]
    public void MissedCheckpoint_IgnoresAirborneCars()
    {
        var (announcer, gamemode, car) = Create(opponents: 0, checkpoints: true);
        var checkpoint = gamemode.CurrentStage.Checkpoints[0];
        announcer.Tick(car, gamemode);

        // Close but in the air never arms it, and leaving the air afterwards is then not a miss.
        car.CarPhysics.Mtouch = false;
        car.Position = new f64Vector3(checkpoint.Position.X + 500, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);
        car.CarPhysics.Mtouch = true;
        car.Position = new f64Vector3(checkpoint.Position.X + 12_000, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    private static void PassNear(
        BackendCar car,
        StageObject checkpoint,
        RaceAnnouncer announcer,
        BaseClientGamemode gamemode
    )
    {
        car.CarPhysics.Mtouch = true;
        car.Position = new f64Vector3(checkpoint.Position.X + 200, checkpoint.Position.Y, checkpoint.Position.Z);
        announcer.Tick(car, gamemode);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void Spectators_SeeWastedCreditWithoutYouWastedWording(bool joinedAsSpectator)
    {
        var (_, gamemode, own) = Create(opponents: 2, spectator: joinedAsSpectator);
        var viewed = gamemode.Players[1].Car!;
        var victim = gamemode.Players[2].Car!;
        if (!joinedAsSpectator) own.CarPhysics.Wasted = true;
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(viewed, gamemode);
        victim.CarPhysics.LastDamageFrom = own.CarPhysics.Im;
        victim.CarPhysics.Wasted = true;

        feedback.Tick(viewed, gamemode);

        Assert.AreEqual(
            $"{gamemode.Players[2].Info.PlayerName} has been wasted by {gamemode.Players[0].Info.PlayerName}!",
            feedback.Status?.Text
        );
    }

    [TestMethod]
    public void Spectator_AutomaticTargetSwitchDoesNotLoseTheWatchedCarsDeath()
    {
        var (_, gamemode, viewed) = Create(opponents: 1, spectator: true);
        var next = gamemode.Players[1].Car!;
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(viewed, gamemode);
        viewed.CarPhysics.Wasted = true;
        viewed.CarPhysics.LastDamageFrom = next.CarPhysics.Im;

        feedback.Tick(next, gamemode);

        Assert.AreEqual("Human has been wasted by Bot1!", feedback.Status?.Text);
    }

    [TestMethod]
    public void Spectator_CompletedStuntSurvivesClearedCountersAndDoesNotRepeat()
    {
        var (_, gamemode, viewed) = Create(opponents: 0, spectator: true);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(viewed, gamemode);
        var physics = viewed.CarPhysics;
        physics.TotalStuntZy = 720;
        physics.Powerup = 49;
        physics.Power = 70;
        physics.LatestStunt = CompletedStunt.Capture(physics, 1);
        physics.TotalStuntZy = 0;
        physics.TabletopCounter = 0;
        var state = PlayerState.CreateFrom(123, viewed);
        var received = TightPack.TightPackSerializer.Deserialize<PlayerState>(TightPack.TightPackSerializer.Serialize(state));
        physics.LatestStunt = default;
        PlayerState.ApplyTo(received, viewed, 0);
        Assert.IsTrue(physics.UsesRemoteStuntFeedback);

        feedback.Tick(viewed, gamemode);

        StringAssert.Contains(feedback.Stunt?.Text ?? "", "double Forward");
        Assert.AreEqual("Power Up 50%", feedback.Status?.Text);
        Assert.IsTrue(feedback.PlayPowerUp);
        feedback.Tick(viewed, gamemode);
        Assert.IsFalse(feedback.PlayPowerUp);
        for (var tick = 0; tick < 500; tick++) feedback.Tick(viewed, gamemode);
        Assert.IsNull(feedback.Stunt, "an unchanged owner snapshot must not keep replaying the stunt");
        Assert.IsNull(feedback.Status);
        physics.TotalStuntZy = 360;
        physics.Powerup = 12;
        physics.Power = 98;
        physics.LatestStunt = CompletedStunt.Capture(physics, 2);
        feedback.Tick(viewed, gamemode);
        Assert.AreEqual("Power to the MAX", feedback.Status?.Text);
        Assert.IsTrue(feedback.PlayPowerUp);
    }

    [TestMethod]
    public void Spectator_LastCarsDeathStillShowsWithNoRemainingTarget()
    {
        var (_, gamemode, viewed) = Create(opponents: 0, spectator: true);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(viewed, gamemode);
        viewed.CarPhysics.Wasted = true;

        feedback.Tick(null, gamemode);

        Assert.AreEqual("Human has been wasted!", feedback.Status?.Text);
        Assert.IsNull(feedback.NextCheckpoint);
        Assert.IsFalse(feedback.PassedCheckpoint);
    }

    [TestMethod]
    public void Spectator_CheckpointsTrackLapWrapAndTheFinishGate()
    {
        var (_, gamemode, viewed) = Create(opponents: 0, laps: 3, checkpoints: true, spectator: true);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(viewed, gamemode);
        Assert.IsFalse(feedback.PassedCheckpoint);
        Assert.AreEqual((ushort)0, feedback.NextCheckpoint);
        viewed.CurrentLap = 1;
        viewed.TotalCheckpoint = 1;
        feedback.Tick(viewed, gamemode);
        Assert.IsTrue(feedback.PassedCheckpoint, "a one-gate lap still crosses a checkpoint");
        Assert.AreEqual("2 laps to go!", feedback.Status?.Text);
        feedback.Tick(viewed, gamemode);
        Assert.IsFalse(feedback.PassedCheckpoint);
        viewed.CurrentLap = 2;
        viewed.TotalCheckpoint = 2;
        feedback.Tick(viewed, gamemode);
        Assert.IsTrue(feedback.FinishCheckpoint);
        viewed.CurrentLap = 3;
        viewed.TotalCheckpoint = 3;
        feedback.Tick(viewed, gamemode);
        Assert.IsTrue(feedback.PassedCheckpoint);
        Assert.IsNull(feedback.NextCheckpoint, "finished cars have no next gate to highlight");
    }

    [TestMethod]
    public void Spectator_SwitchingTargetsDoesNotReplayTheirExistingProgressOrStunts()
    {
        var (_, gamemode, first) = Create(opponents: 1, checkpoints: true, spectator: true);
        gamemode.CurrentStage.Checkpoints.Add(new StageObject(first.Rad));
        var second = gamemode.Players[1].Car!;
        second.CurrentCheckpoint = 1;
        second.TotalCheckpoint = 3;
        second.CurrentLap = 1;
        second.CarPhysics.TotalStuntZy = 360;
        second.CarPhysics.Powerup = 98;
        second.CarPhysics.LatestStunt = CompletedStunt.Capture(second.CarPhysics, 5);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(first, gamemode);

        feedback.Tick(second, gamemode);

        Assert.IsFalse(feedback.PassedCheckpoint);
        Assert.AreEqual((ushort)1, feedback.NextCheckpoint);
        Assert.IsFalse(feedback.PlayPowerUp);
        Assert.IsNull(feedback.Stunt);
        Assert.IsNull(feedback.Status);
        feedback.Reset();
        feedback.Tick(second, gamemode);
        Assert.IsFalse(feedback.PassedCheckpoint, "entering spectate again must also seed a fresh baseline");
    }

    [TestMethod]
    public void Departure_AnnouncesAnAlreadyWastedOpponentOnce()
    {
        var (announcer, gamemode, car) = Create(opponents: 1);
        var opponent = gamemode.Players[1].Car!;
        opponent.CarPhysics.Wasted = true;
        announcer.Tick(car, gamemode);
        opponent.MarkDeparted(PlayerDepartureReason.TimedOut);
        announcer.Tick(car, gamemode);
        Assert.AreEqual("Bot1 has left the game (timed out)", announcer.Status?.Text);
        opponent.MarkDeparted(PlayerDepartureReason.Disconnected);
        for (var i = 0; i < (int)(Physics.TargetTps * 4); i++) announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
        Assert.AreEqual(PlayerDepartureReason.TimedOut, opponent.DepartureReason);
    }

    [TestMethod]
    public void SpectatorDeparture_AnnouncesEvenWhenTheLastTargetLeaves()
    {
        var (_, gamemode, car) = Create(opponents: 0, spectator: true);
        var feedback = new SpectatorRaceFeedback();
        feedback.Tick(car, gamemode);
        car.MarkDeparted(PlayerDepartureReason.Disconnected);
        feedback.Tick(null, gamemode);
        Assert.AreEqual("Human has left the game (disconnected)", feedback.Status?.Text);
    }

    [TestMethod]
    public void DepartureHistory_DoesNotReplayOldAnnouncements()
    {
        var (announcer, gamemode, car) = Create(opponents: 1);
        gamemode.Players[1].Car!.MarkDeparted(PlayerDepartureReason.Disconnected, announce: false);
        announcer.Tick(car, gamemode);
        Assert.IsNull(announcer.Status);
    }

    private static (RaceAnnouncer Announcer, TestGamemode Gamemode, BackendCar Car) Create(
        int opponents,
        int laps = 3,
        bool checkpoints = false,
        bool spectator = false
    )
    {
        var players = new List<ClientSidePlayerInfo>
        {
            new() { PlayerName = "Human", CarName = CarName, Color = default, IsBot = false, IsClientPlayer = !spectator }
        };
        for (var i = 0; i < opponents; i++)
        {
            players.Add(
                new ClientSidePlayerInfo
                {
                    PlayerName = $"Bot{i + 1}", CarName = CarName, Color = default, IsBot = true, IsClientPlayer = false
                }
            );
        }

        var gamemode = new TestGamemode(players.ToArray(), laps, checkpoints);
        gamemode.Begin();
        return (new RaceAnnouncer(), gamemode, gamemode.Players[0].Car!);
    }

    private sealed class TestGamemode(ClientSidePlayerInfo[] players, int laps, bool checkpoints)
        : BaseClientGamemode(
            new ClientGamemodeParameters { Players = players },
            new TestGamemodeContext(laps, checkpoints)
        )
    {
        public override string GamemodeId => "test/announcer";

        public override void Begin()
        {
            base.Begin();
            for (var i = 0; i < Players.Count; i++)
                Players[i].Car = new BackendCar(Players[i].Info, i, -500 + 400 * i, 0);
        }
    }

    /// <summary>An empty stage; its lap count is set by the context below.</summary>
    private sealed class AnnouncerStage : BackendStage;

    private sealed class TestGamemodeContext(int laps, bool checkpoints) : IGamemodeContext
    {
        public BackendStage CurrentStage { get; } = BuildStage(laps, checkpoints);

        private static BackendStage BuildStage(int laps, bool checkpoints)
        {
            var stage = new AnnouncerStage { Nlaps = (ushort)laps };
            if (checkpoints)
            {
                var rad = new Rad3d([], CarStats.Default, new Rad3dWheelDef[4], null, [], [], false, [], CarName);
                stage.Checkpoints.Add(new StageObject(rad) { Position = new f64Vector3(0, 0, 5000) });
            }

            return stage;
        }

        public RaceState RaceState => RaceState.InProgress;
        public IClientCallbacks ClientCallbacks => new StubCallbacks();

        public void SendServerEvent(ReadOnlySpan<byte> payload)
        {
        }

        public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        {
        }
    }

    private sealed class StubCallbacks : IClientCallbacks
    {
        public void ResetCheckpointGlow()
        {
        }

        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish)
        {
        }

        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => new StubCarCallbacks();

        private sealed class StubCarCallbacks : IClientCarCallbacks
        {
            public bool CastsShadow { get; set; }
            public bool? GetsShadowed { get; set; }
            public float? AlphaOverride { get; set; }
            public bool? Glow { get; set; }
            public bool? Finish { get; set; }
        }
    }
}
