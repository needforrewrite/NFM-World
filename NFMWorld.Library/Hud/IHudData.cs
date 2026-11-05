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
    /// <summary>Whether the arrow is locked onto a chosen opponent rather than the automatic pick.</summary>
    bool ArrowLocked => false;
    IReadOnlyList<HudRacer> Racers { get; }

    // ── World-space data for overlays that draw outside the HUD skin ──────
    /// <summary>The client car's world position, when there is one.</summary>
    (float X, float Y, float Z)? ClientPosition { get; }
    float ClientHeading { get; }
    /// <summary>Track centre in world XZ, used to un-rotate the minimap.</summary>
    (float X, float Z) TrackCenter { get; }
    /// <summary>The checkpoint the client is currently heading for.</summary>
    (float X, float Z)? TargetCheckpoint { get; }
    /// <summary>All checkpoints in lap order, for drawing the minimap track.</summary>
    IReadOnlyList<(float X, float Z)> Checkpoints { get; }
    /// <summary>Racers with world positions, for the minimap and name tags.</summary>
    IReadOnlyList<HudRacerMarker> Markers { get; }
    /// <summary>Live camera view-projection, for projecting name tags onto the screen.</summary>
    System.Numerics.Matrix4x4 ViewProjection { get; }

    // ── Optional real map image (see StageMinimap) ──────────────────────
    /// <summary>
    /// A baked top-down image of the stage, when one is ready. The HUD draws it as-is, so it is
    /// typed as the driver's <c>IImage</c>; null until <c>StageMinimap</c> has rendered one.
    /// </summary>
    object? MapImage => null;
    /// <summary>The image's world frame: top-left corner in world XZ, and world units per pixel.</summary>
    (float MinX, float MaxZ, float UnitsPerPixel) MapFrame => default;
}

/// <param name="LockSlot">
/// The digit that locks this racer, 1 through 10, or 0 for the client and for opponents past the
/// last key. Opponents are numbered in sidebar order.
/// </param>
public readonly record struct HudRacer(int Position, string Name, float Damage, bool IsClient, bool Wasted,
    int LockSlot = 0);

public readonly record struct HudRacerMarker(
    string Name,
    float X,
    float Y,
    float Z,
    int Position,
    bool IsClient,
    bool Wasted);
