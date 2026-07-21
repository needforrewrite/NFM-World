using Microsoft.Xna.Framework.Graphics;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorld.Util;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Multiplayer.Packets.C2S;
using NFMWorldLibrary.Multiplayer.Packets.S2C;
using NFMWorldLibrary.Util;
using S2C_PlayerState = NFMWorldLibrary.Multiplayer.Packets.S2C.S2C_PlayerState;

namespace NFMWorld.Gameplay;

public class InMultiplayerRacePhase : BaseRacePhase
{
    private readonly IMultiplayerClientTransport _transport;
    private readonly Guid _joinToken;
    private readonly MatchGameplayInfo _session;

    public InMultiplayerRacePhase(
        GraphicsDevice graphicsDevice,
        IMultiplayerClientTransport transport,
        MatchGameplayInfo session,
        Guid clientPlayerId,
        Guid joinToken
    )
        : base(
            graphicsDevice,
            session.StageName,
            GetGameModeFactory(session),
            session.Players
                .Select(c => new PlayerParameters
                {
                    CarName = c.Value.Vehicle,
                    Color = c.Value.Color,
                    PlayerName = c.Value.Name,
                    IsBot = false,
                    IsClientPlayer = c.Value.Id == clientPlayerId
                })
                .ToArray()
        )
    {
        _transport = transport;
        _joinToken = joinToken;
        _session = session;
        // Set initial race state once at construction; Enter() no longer resets it.
        RaceState = RaceState.WaitingToStart;

        // Inject event sender so the client gamemode can send events to the server.
        GamemodeInstance?.SetEventSender(payload =>
            _transport.SendPacketToServer(new C2S_ClientEvent { Payload = payload.ToArray() }, reliable: true));
    }
    private static BaseGamemodeFactory GetGameModeFactory(MatchGameplayInfo matchGameplayInfo)
    {
        switch (matchGameplayInfo.Gamemode)
        {
            case DefaultGamemodes.Racing:
                return new PvpGamemodeFactory(PvpConstraint.Racing);
            case DefaultGamemodes.Wasting:
                return new PvpGamemodeFactory(PvpConstraint.Wasting);
            case DefaultGamemodes.Both:
                return new PvpGamemodeFactory(PvpConstraint.Both);
            case DefaultGamemodes.Football:
                return new FootballGamemodeFactory();
            case DefaultGamemodes.Sandbox:
                return new SandboxGamemodeFactory();
            default:
                throw new ArgumentOutOfRangeException(nameof(matchGameplayInfo.Gamemode), matchGameplayInfo.Gamemode, "Unknown gamemode");
        }
    }

    private uint _ticks = 0;
    private UnlimitedArray<uint> _lastTick = [];
    private bool _sentRaceLoaded;

    public override void Enter()
    {
        // RaceState initialized in constructor; gamemode created by BaseRacePhase constructor.
        // Enter only handles display activation (CEF bridge, camera, music).
        base.Enter();
    }

    public override void Exit()
    {
        // Transport teardown moved to Dispose(). Exit only handles display deactivation.
        base.Exit();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _transport.Stop();
        }

        base.Dispose(disposing);
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"race state: {RaceState}");

        // Send C2S_RaceLoaded once transport is connected
        if (!_sentRaceLoaded && _transport.State == ClientState.Connected)
        {
            _sentRaceLoaded = true;
            _transport.SendPacketToServer(new C2S_RaceLoaded { JoinToken = _joinToken });
        }

        foreach (var packet in _transport.GetNewPackets())
        {
            switch (packet)
            {
                case S2C_RaceCanStart raceCanStart:
                    RaceState = RaceState.InProgress;
                    break;
                case S2C_RaceFailedToStart raceFailedToStart:
                    RaceState = RaceState.FailedToStart;
                    break;
                case S2C_PlayerState playerState:
                    Console.WriteLine($"[Client] Received player state for {playerState.PlayerId} at tick {playerState.State.Ticks}");
                    Console.WriteLine(string.Join(", ", _session.Players.Select(e => $"{e.Value.Name} ({e.Value.Id})")));
                    var carIndex = _session.Players
                        .First(e => e.Value.Id == playerState.PlayerId)
                        .Key;
                    var car = CarsInRace[carIndex];
                    if (playerState.State.Ticks <= _lastTick[carIndex])
                        break;
                    _lastTick[carIndex] = playerState.State.Ticks;
                    PlayerState.ApplyTo(playerState.State, car);
                    break;
                case S2C_ServerEvent serverEvent:
                    try
                    {
                        GamemodeInstance?.OnServerEvent(serverEvent.Payload.Span);
                    }
                    finally
                    {
                        serverEvent.Dispose();
                    }
                    break;
                case S2C_GameFinished gameFinished:
                    GamemodeInstance?.SetServerResults(gameFinished.Results);
                    RaceState = RaceState.Finished;
                    break;
            }
        }

        base.GameTick();

        if (RaceState == RaceState.InProgress)
        {
            var myCar = CarsInRace.FirstOrDefault(c => c.Player.IsClientPlayer);
            if (myCar is not null)
            {
                _transport.SendPacketToServer(new C2S_PlayerState()
                {
                    State = PlayerState.CreateFrom(_ticks++, myCar)
                }, false);
            }

        }
    }

    public override void Render(float alpha)
    {
        base.Render(alpha);
        if (RaceState == RaceState.WaitingToStart)
        {
            G.SetFont(new Font(FontFamily.DroidSans, FontStyle.Plain, 26));
            G.SetColor(new Color(255, 255, 255));
            G.DrawStringAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);

            G.SetColor(new Color(0, 0, 0));
            G.DrawStringStrokeAligned("Waiting for other players to load...", 0, 150, (int)G.Viewport.X, (int)G.Viewport.Y, TextHorizontalAlignment.Center);
        }
    }
}