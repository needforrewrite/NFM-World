using JetBrains.Annotations;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.DotNet;

[assembly: Gamemode(
    Name = "Rocket League",
    Description = "A physics sandbox: jump, boost and aerial control on a free-play stage."
)]

namespace NFMWorld.Gamemode.Rocket;

/// <summary>
/// The client owns physics. A server gamemode is deliberately absent: this is a sandbox
/// proving custom gamemode + CarPhysics/HUD hooks, not a scored match. The host phase maps
/// the jump/boost bindings onto <see cref="Control.Jump"/>/<see cref="Control.Boost"/> before
/// this tick, and this gamemode turns those held inputs into the arcade hooks.
/// </summary>
[UsedImplicitly]
public sealed class RocketGamemode : PluginGamemode
{
    // Arcade constants, tuned per tick against the stage's default gravity (1.0). JumpImpulse
    // is consumed by CarPhysics.Drive once and gated on the previous tick's ground contact.
    private const int JumpImpulse = 160;
    private const int BoostThrust = 12;
    private const int MaxBoost = 200;
    private const int BoostRegen = 6;
    private const int BoostDrain = 22;
    private const int BoostStart = 100;

    private PhysicsController _physics = null!;
    private int _countdownInner;
    private int _countdown;
    private bool _running;
    private bool _jumpQueued;
    private bool _jumpHeld;
    private float _boost = BoostStart;

    public RocketGamemode(
        ClientGamemodeParameters clientGamemodeParameters,
        IGamemodeContext gamemodeContext,
        string gamemodeId,
        Dictionary<string, object>? config = null
    ) : base(clientGamemodeParameters, gamemodeContext, gamemodeId, config)
    {
    }

    public override void Begin()
    {
        base.Begin();
        _physics = new PhysicsController(Players, CurrentStage);
        _countdownInner = GamemodeUtils.CountdownInterval;
        _countdown = 3;
        _running = false;
        _jumpQueued = false;
        _jumpHeld = false;
        _boost = BoostStart;
        SetupCars();
    }

    public override void Reset()
    {
        base.Reset();
        Begin();
    }

    public override void GameTick()
    {
        base.GameTick();
        if (!_running)
        {
            HudState.CountdownTimer = _countdown;
            if (--_countdownInner <= 0)
            {
                _countdownInner = GamemodeUtils.CountdownInterval;
                _countdown--;
            }

            if (_countdown <= 0)
            {
                _running = true;
                HudState.CountdownTimer = 0;
            }

            return;
        }

        // The host phase writes Control before this tick; the jump hook is edge-triggered so a
        // held key cannot re-fire it mid-air, and the boost hook is held while fuel remains.
        if (ClientPlayer?.Car is { } car)
        {
            var physics = car.CarPhysics;
            var control = car.Control;

            if (control.Jump && !_jumpHeld && physics.WasGrounded)
                _jumpQueued = true;
            _jumpHeld = control.Jump;

            if (_jumpQueued)
            {
                _jumpQueued = false;
                if (physics.WasGrounded)
                    physics.JumpImpulse = JumpImpulse;
            }

            if (control.Boost && _boost > 0)
            {
                _boost = MathF.Max(0, _boost - BoostDrain / NFMWorldLibrary.Physics.TargetTps);
                physics.BoostThrust = BoostThrust;
            }
            else
            {
                _boost = MathF.Min(MaxBoost, _boost + BoostRegen / NFMWorldLibrary.Physics.TargetTps);
            }

            _physics.GameTick();
        }

        HudState.Boost = _boost / MaxBoost;
        HudState.TotalLaps = CurrentStage.Nlaps;
        HudState.TotalRacers = Players.Count;
    }

    private void SetupCars()
    {
        for (var i = 0; i < Players.Count; i++)
        {
            var player = Players[i];
            var (x, z) = CurrentStage.GetStartPosition(i);
            player.Car = new BackendCar(player.Info, i, x, z) { CurrentCheckpoint = 0, CurrentLap = 0 };
        }
    }
}
