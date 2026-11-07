using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorldLibrary.Hud;

/// <summary>
/// Turns client-side race state into the two lines of status text the HUD shows: a status line
/// (checkpoints, wasted cars, warnings) and a stunt line above it.
/// </summary>
/// <remarks>
/// Ported from phy-addons' <c>xtGraphics.stat()</c>, which tracked all of this with a handful of
/// shared counters (<c>tcnt</c>, <c>auscnt</c>, <c>pwcnt</c>) and blink flags mutated from one
/// 800-line method. Here each message is just a slot with a tick budget: a source diffs the state
/// it cares about, posts a line, and the slot expires on its own. Durations are seconds, so the
/// 21.4 tps counters phy used do not need translating.
/// </remarks>
public sealed class RaceAnnouncer
{
    private const float StatusSeconds = 3f;
    private const float ArrowSeconds = 2f;

    /// <summary>phy warned again no sooner than its 300-tick counter could cycle, at 21.4 tps.</summary>
    private const float PowerLowCooldownSeconds = 300f / 21.4f;

    /// <summary>
    /// Heading away from the next checkpoint by more than this counts as wrong way. phy read the
    /// same thing off the guidance arrow's rotation; the car's own heading does not depend on
    /// which camera is showing.
    /// </summary>
    private const float WrongWayDegrees = 130f;

    /// <summary>How long the nose must stay off-course before it counts as a wrong way.</summary>
    private const float WrongWaySeconds = 1.5f;

    /// <summary>
    /// Close enough to arm. phy's 400 is in tens of world units, so this is its real reach.
    /// </summary>
    private const float CheckpointNear = 4000f;

    /// <summary>
    /// Far enough to count as past it. A car that was close and then is not, without crossing,
    /// missed it; one that starts this far away has simply not arrived yet.
    /// </summary>
    private const float CheckpointFar = 8000f;


    private readonly record struct Slot(string Text, HudMessageKind Kind, int TicksLeft);

    private Slot? _status;
    private Slot? _stunt;

    private int _lastTotalCheckpoint;
    private bool _lastNewcar;
    private bool _lastArrace;
    private int _lastArrowLock = -1;
    private int _powerLowCooldown;
    private int _wrongWayTicks;
    /// <summary>True once the car has been close to the next checkpoint without crossing it.</summary>
    private bool _approachedCheckpoint;
    /// <summary>The checkpoint total that arming refers to; crossing one re-arms from scratch.</summary>
    private int _armedTotalCheckpoint = -1;
    /// <summary>Checkpoint total the last warning covered; -1 until one has been given.</summary>
    private int _lastMissedTotalCheckpoint = -1;
    private readonly Dictionary<int, bool> _wasted = [];

    /// <summary>The status line, or null when nothing is showing.</summary>
    public HudMessage? Status => _status is { TicksLeft: > 0 } s ? new HudMessage(s.Text, s.Kind) : null;

    /// <summary>The stunt line, or null when nothing is showing.</summary>
    public HudMessage? Stunt => _stunt is { TicksLeft: > 0 } s ? new HudMessage(s.Text, s.Kind) : null;

    public void Reset()
    {
        _status = null;
        _stunt = null;
        _lastTotalCheckpoint = 0;
        _lastNewcar = false;
        _lastArrace = false;
        _lastArrowLock = -1;
        _powerLowCooldown = 0;
        _wrongWayTicks = 0;
        _approachedCheckpoint = false;
        _armedTotalCheckpoint = -1;
        _lastMissedTotalCheckpoint = -1;
        _wasted.Clear();
    }

    /// <summary>Runs one game tick: expires the slots, then posts whatever the state has to say.</summary>
    public void Tick(BackendCar client, BaseClientGamemode gamemode)
    {
        Expire();
        if (_powerLowCooldown > 0) _powerLowCooldown--;

        CheckCheckpoint(client, gamemode);
        CheckOpponents(gamemode);
        CheckOwnState(client, gamemode);
        CheckStunt(client);
        CheckArrow(client, gamemode);
    }

    private void Expire()
    {
        if (_status is { } status)
            _status = status with { TicksLeft = status.TicksLeft - 1 };
        if (_stunt is { } stunt)
            _stunt = stunt with { TicksLeft = stunt.TicksLeft - 1 };
    }

    private void Show(string text, HudMessageKind kind, float seconds = StatusSeconds)
        => _status = new Slot(text, kind, SecondsToTicks(seconds));

    private static int SecondsToTicks(float seconds)
        => Math.Max(1, (int)MathF.Ceiling(seconds * Physics.TargetTps));

    private static int SecondsToTicks(float seconds, int minimum) => Math.Max(minimum, SecondsToTicks(seconds));

    private static int PowerLowCooldownTicks => SecondsToTicks(PowerLowCooldownSeconds);

    private static int WrongWayTicks => SecondsToTicks(WrongWaySeconds);

    private void CheckCheckpoint(BackendCar client, BaseClientGamemode gamemode)
    {
        if (client.TotalCheckpoint == _lastTotalCheckpoint) return;
        _lastTotalCheckpoint = client.TotalCheckpoint;

        var laps = gamemode.CurrentStage.Nlaps;
        if (client.CurrentCheckpoint == 0 && client.CurrentLap > 0)
        {
            var remaining = laps - client.CurrentLap;
            Show(remaining <= 1 ? "Final lap!!!" : $"{remaining} laps to go!", HudMessageKind.Info);
            return;
        }

        Show("Checkpoint!", HudMessageKind.Info);
    }

    private void CheckOpponents(BaseClientGamemode gamemode)
    {
        foreach (var player in gamemode.Players)
        {
            if (player.Car is not { } car) continue;
            var wasted = car.Wasted;
            if (!_wasted.TryGetValue(player.Index, out var wasWasted))
            {
                _wasted[player.Index] = wasted;
                continue;
            }

            if (wasted == wasWasted) continue;
            _wasted[player.Index] = wasted;
            if (!wasted) continue;

            if (player.Info.IsClientPlayer)
            {
                Show("You have been wasted!", HudMessageKind.Danger);
                continue;
            }

            Show(WastedText(car, gamemode), HudMessageKind.Info);
        }
    }

    private string WastedText(BackendCar car, BaseClientGamemode gamemode)
    {
        // Player names, not car names: in a race every car can be the same model.
        var name = NameOf(car, gamemode);
        var killer = car.CarPhysics.LastDamageFrom;
        if (killer < 0) return $"{name} has been wasted!";
        if (gamemode.ClientPlayer.Car is { } own && own.CarPhysics.Im == killer)
            return $"You wasted {name}!";
        foreach (var player in gamemode.Players)
        {
            if (player.Car is not { } other || other.CarPhysics.Im != killer) continue;
            return $"{name} has been wasted by {NameOf(other, gamemode)}!";
        }

        return $"{name} has been wasted!";
    }

    private static string NameOf(BackendCar car, BaseClientGamemode gamemode)
    {
        foreach (var player in gamemode.Players)
        {
            if (ReferenceEquals(player.Car, car))
                return player.Info.PlayerName;
        }

        return car.Stats.Name;
    }

    private void CheckOwnState(BackendCar client, BaseClientGamemode gamemode)
    {
        var physics = client.CarPhysics;

        if (physics.Newcar && !_lastNewcar)
            Show("Car Fixed", HudMessageKind.Info);
        _lastNewcar = physics.Newcar;

        if (physics.CapsizedCounter != 0)
        {
            Show("Bad Landing!", HudMessageKind.Warning);
            return;
        }

        if (physics.Power < 45 && physics.Mtouch && _powerLowCooldown == 0)
        {
            Show("Power low, perform stunt!", HudMessageKind.Warning);
            _powerLowCooldown = PowerLowCooldownTicks;
        }

        CheckWrongWay(client, gamemode);
        CheckMissedCheckpoint(client, gamemode);
    }

    private void CheckWrongWay(BackendCar client, BaseClientGamemode gamemode)
    {
        if (IsWrongWay(client, gamemode))
        {
            if (++_wrongWayTicks == WrongWayTicks) Show("Wrong Way!", HudMessageKind.Danger);
        }
        else
        {
            _wrongWayTicks = 0;
        }
    }

    /// <summary>
    /// Warns once per checkpoint when the car got close to it, then drove away without crossing.
    /// </summary>
    /// <remarks>
    /// phy checked the same two distances inside the physics step, against its focus point. Here
    /// the watched checkpoint is the one the HUD already aims at, and only ground contact counts,
    /// so flying over a checkpoint does not read as a miss.
    /// </remarks>
    private void CheckMissedCheckpoint(BackendCar client, BaseClientGamemode gamemode)
    {
        var checkpoints = gamemode.CurrentStage.Checkpoints;
        if (checkpoints.Count == 0) return;

        if (client.TotalCheckpoint != _armedTotalCheckpoint)
        {
            _armedTotalCheckpoint = client.TotalCheckpoint;
            _approachedCheckpoint = false;
        }

        var next = checkpoints[Math.Clamp((int)client.CurrentCheckpoint, 0, checkpoints.Count - 1)];
        var dx = (float)(client.Position.X - next.Position.X);
        var dz = (float)(client.Position.Z - next.Position.Z);
        var distanceSquared = dx * dx + dz * dz;

        if (client.CarPhysics.Mtouch && distanceSquared < CheckpointNear * CheckpointNear)
            _approachedCheckpoint = true;

        if (!_approachedCheckpoint) return;
        if (distanceSquared <= CheckpointFar * CheckpointFar) return;
        if (client.TotalCheckpoint == _lastMissedTotalCheckpoint) return;

        _lastMissedTotalCheckpoint = client.TotalCheckpoint;
        Show("Checkpoint Missed!", HudMessageKind.Warning);
    }

    /// <summary>
    /// True while the car's nose points far enough away from the next checkpoint. phy derived
    /// this from the guidance arrow's on-screen rotation; the car's own heading is the same
    /// signal without depending on which camera is showing.
    /// </summary>
    private static bool IsWrongWay(BackendCar client, BaseClientGamemode gamemode)
    {
        var checkpoints = gamemode.CurrentStage.Checkpoints;
        if (checkpoints.Count == 0) return false;
        var next = checkpoints[Math.Clamp((int)client.CurrentCheckpoint, 0, checkpoints.Count - 1)];

        var dx = (float)next.Position.X - (float)client.Position.X;
        var dz = (float)next.Position.Z - (float)client.Position.Z;
        if (dx * dx + dz * dz < 1) return false;

        // The car's forward direction is (-sin cxz, cos cxz).
        var heading = (float)client.CarPhysics.Cxz * MathF.PI / 180f;
        var forwardX = -MathF.Sin(heading);
        var forwardZ = MathF.Cos(heading);
        var length = MathF.Sqrt(dx * dx + dz * dz);
        var dot = (forwardX * dx + forwardZ * dz) / length;
        return dot < MathF.Cos(WrongWayDegrees * MathF.PI / 180f);
    }

    private void CheckArrow(BackendCar client, BaseClientGamemode gamemode)
    {
        var control = client.Control;
        if (control.Arrace != _lastArrace)
        {
            _lastArrace = control.Arrace;
            Show($"Arrow now pointing at {(control.Arrace ? "CARS" : "TRACK")}", HudMessageKind.Info, ArrowSeconds);
        }

        if (control.ArrowLock == _lastArrowLock) return;
        _lastArrowLock = control.ArrowLock;
        var name = PlayerName(control.ArrowLock, gamemode);
        Show(name is null ? "Arrow Unlocked!" : $"Arrow Locked on > {name}", HudMessageKind.Info, ArrowSeconds);
    }

    private static string? PlayerName(int index, BaseClientGamemode gamemode)
    {
        if (index < 0) return null;
        foreach (var player in gamemode.Players)
        {
            if (player.Index == index) return player.Info.PlayerName;
        }

        return null;
    }

    private void CheckStunt(BackendCar client)
    {
        var physics = client.CarPhysics;
        // trcnt hits 10 exactly once per combo; the stunt values are still set at that point
        // and cleared after, so this is the one tick the whole combo is readable.
        if (physics.TabletopCounter != 10) return;
        if (DescribeStunt(physics) is not { } text) return;
        _stunt = new Slot(text, HudMessageKind.Info, SecondsToTicks(Math.Min(6f, 2f + text.Length / 20f)));

        // phy posted both at once: the stunt name on its own line and the power it earned on the
        // status line. Full power reads as a max rather than a percentage.
        var percent = (int)(100f * (float)physics.Powerup / 98f);
        Show(percent >= 98 ? "Power To The MAX" : $"Power Up {percent}%", HudMessageKind.Info);
    }

    private static readonly string[][] Adjectives =
    [
        ["Cool", "Alright", "Nice"],
        ["Wicked", "Amazing", "Super"],
        ["Awesome", "Ripping", "Radical"],
        ["What the...?", "You're a super star!!!!", "Who are you again...?"],
        ["surf style", "off the lip", "bounce back"]
    ];

    private static readonly string[] Exclamations = ["!", "!!", "!!!"];

    /// <summary>
    /// phy's stunt combo wording: a loop/spin name plus the flip angle, decorated with an
    /// adjective picked by how much power the stunt earned.
    /// </summary>
    public static string? DescribeStunt(CarPhysics physics)
    {
        var body = StuntName(physics);
        if (body.Length == 0) return null;

        var tier = physics.Powerup > 150 ? 3 : physics.Powerup > 40 ? 2 : physics.Powerup > 20 ? 1 : 0;
        var adjective = Adjectives[tier][System.Random.Shared.Next(3)];
        return tier == 3 ? adjective : $"{adjective} {body}{Exclamations[tier]}";
    }

    private static string StuntName(CarPhysics physics)
    {
        var loop = LoopName(physics);
        var spin = SpinName(physics, loop.Length == 0);
        var flip = FlipDegrees(physics.TotalStuntXz, out var beyond);

        var text = loop;
        if (flip != 0)
        {
            if (loop.Length == 0 && spin.Length == 0)
            {
                text = $"{flip}";
            }
            else
            {
                if (spin.Length > 0) text += loop.Length == 0 ? $" {spin}" : $" with {spin}";
                text += $" by {flip}";
            }

            if (beyond) text += " and beyond";
        }
        else if (spin.Length > 0)
        {
            text += loop.Length == 0 ? $" {spin}" : $" by {spin}";
        }

        return text.Trim();
    }

    private static string LoopName(CarPhysics physics)
    {
        var zy = (float)physics.TotalStuntZy;
        var i = 0;
        for (; zy > 225; i++) zy -= 360;
        for (; zy < -225; zy += 360) i--;

        var loop = i switch
        {
            1 => "Forward loop",
            2 => "double Forward",
            3 => "triple Forward",
            >= 4 => "massive Forward looping",
            -1 => "Backloop",
            -2 => "double Back",
            -3 => "triple Back",
            <= -4 => "massive Back looping",
            _ => physics.ForwardTabletop || physics.BackwardsTabletop ? "Tabletop" : ""
        };

        if (loop.Length == 0) return "";
        if (i > 0 && physics.BackwardsTabletop) return $"Hanged {loop}";
        if (i < 0 && physics.ForwardTabletop) return $"Hanged {loop}";
        return loop;
    }

    private static string SpinName(CarPhysics physics, bool loopEmpty)
    {
        var xy = MathF.Abs((float)physics.TotalStuntXy);
        var i = 0;
        for (; xy > 270; i++) xy -= 360;
        if (i == 0 && physics.RightTabletop) return loopEmpty ? "Tabletop" : "Flipside";
        return i switch
        {
            1 => "Rollspin",
            2 => "double Rollspin",
            3 => "triple Rollspin",
            >= 4 => "massive Roll spinning",
            _ => ""
        };
    }

    private static int FlipDegrees(fix64 total, out bool beyond)
    {
        var xz = MathF.Abs((float)total);
        var degrees = 0;
        beyond = false;
        while (xz > 90)
        {
            xz -= 180;
            degrees += 180;
            if (degrees <= 900) continue;
            degrees = 900;
            beyond = true;
            break;
        }

        return degrees;
    }
}
