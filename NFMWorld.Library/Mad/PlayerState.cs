using System.Runtime.InteropServices;
using TightPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Hud;

namespace NFMWorldLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
[TightPackable]
public partial struct PlayerState
{
    public required CarFrame CarFrame;
    public required uint Ticks;

    /// <summary>Credited car's roster index plus one; zero means no attacker.</summary>
    public int DamageSource;
    public CompletedStunt CompletedStunt;

    private ulong _currentTimeInMs;

    public required DateTimeOffset CurrentTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }

    public static void ApplyTo(PlayerState state, BackendCar c, int checkpointsPerLap)
    {
        if (c.HasLeftRace) return;
        state.CarFrame.ApplyToCar(c);
        c.CarPhysics.ResetDamageCredit();
        c.CarPhysics.LastDamageFrom = state.DamageSource - 1;
        c.CarPhysics.LatestStunt = state.CompletedStunt;
        c.CarPhysics.UsesRemoteStuntFeedback = true;
        c.CurrentCheckpoint = state.CarFrame.RacePosition.CheckpointInlap;
        c.CurrentLap = state.CarFrame.RacePosition.Lap;
        c.TotalCheckpoint = c.CurrentCheckpoint + c.CurrentLap * checkpointsPerLap;
    }

    public static PlayerState CreateFrom(uint ticks, BackendCar car)
    {
        return new PlayerState
        {
            CarFrame = CarFrame.Create(car),
            DamageSource = car.CarPhysics.LastDamageFrom + 1,
            CompletedStunt = car.CarPhysics.LatestStunt,
            CurrentTime = DateTimeOffset.UtcNow,
            Ticks = ticks
        };
    }
}
