using TightPack;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary;

/// <summary>Which component of a car's spawn a start directive sets.</summary>
[PluginVisible]
public enum StartAxis : byte
{
    X,
    Z,
    Y,
    Yaw,
    Pitch,
    Roll
}

/// <summary>
/// One <c>xstart</c>/<c>ystart</c>/<c>xzstart</c>/<c>startheight</c> line: the player count it was
/// written for and its per-player values. phy-addons applies a line only when the count matches the
/// race's player count, so the raw lines are kept and resolved at spawn time.
/// </summary>
[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public partial record StageStart(
    [property: TightPackOrder(0), PluginVisible] StartAxis Axis,
    [property: TightPackOrder(1), PluginVisible] int PlayerCount,
    [property: TightPackOrder(2), PluginVisible] float[] Values
);

/// <summary>
/// <c>pulsegrnd(a,b,c)</c>: ground and ground-polygon colours scale by
/// <c>a + b * sin(tick * c)</c>.
/// </summary>
[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct GroundPulse(
    [property: TightPackOrder(0), PluginVisible] float A,
    [property: TightPackOrder(1), PluginVisible] float B,
    [property: TightPackOrder(2), PluginVisible] float C
);

/// <summary>
/// <c>flameColor1/2/3(r,g,b)</c> and <c>flameSnapDiv(x,y,z)</c>: the two wasted-car fire colours
/// (outer, inner), the pre-fire ember colour (<c>flameColor3</c>, a phy-addons extension — phy
/// hardcodes it) and the per-channel snap divisors.
/// </summary>
[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct FlamePalette(
    [property: TightPackOrder(0), PluginVisible] Color3 Outer,
    [property: TightPackOrder(1), PluginVisible] Color3 Inner,
    [property: TightPackOrder(2), PluginVisible] Color3 Ember,
    [property: TightPackOrder(3), PluginVisible] float SnapDivX,
    [property: TightPackOrder(4), PluginVisible] float SnapDivY,
    [property: TightPackOrder(5), PluginVisible] float SnapDivZ
)
{
    /// <summary>phy's stock palette: orange/light-orange fire, near-white embers.</summary>
    public static FlamePalette Default { get; } = new(
        new Color3(255, 169, 89),
        new Color3(255, 207, 136),
        new Color3(230, 230, 230),
        4f,
        3f,
        2f
    );
}
