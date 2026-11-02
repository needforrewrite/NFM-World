namespace NFMWorldLibrary.Hud;

public interface IHudData
{
    int SpeedKmh { get; }
    float PowerFraction { get; }
    float DamageFraction { get; }
    int Lap { get; }
    int TotalLaps { get; }
    int LapTimeMs { get; }
    int Position { get; }
    int TotalRacers { get; }
    bool HasPosition => Position > 0 && Position <= TotalRacers;
    int Countdown { get; }
    string Message { get; }
    bool Paused { get; }
    bool TimeTrial { get; }
    bool IsPreview => false;
    int Wasted { get; }
    int Opponents { get; }
    float? ArrowAngle { get; }
    string ArrowTarget { get; }
    bool ArrowAtCars { get; }
    IReadOnlyList<HudRacer> Racers { get; }
}

public readonly record struct HudRacer(int Position, string Name, float Damage, bool IsClient, bool Wasted);
