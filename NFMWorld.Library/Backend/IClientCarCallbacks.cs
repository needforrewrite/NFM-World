using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// Visual-only properties that a gamemode can set on a client car.
/// Physics properties (Stats, GroundAt, WheelAngle, etc.) are accessed directly
/// via <see cref="BackendCar"/> — this interface is purely for rendering knobs.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public interface IClientCarCallbacks
{
    bool CastsShadow { get; set; }
    bool? GetsShadowed { get; set; }
    float? AlphaOverride { get; set; }
    bool? Glow { get; set; }
    bool? Finish { get; set; }
}