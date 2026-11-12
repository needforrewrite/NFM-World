using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

/// <summary>
/// Per-stage physics overrides, read from a stage file's <c>gravity()</c>/<c>lava()</c>/… directives
/// (phy-addons stage properties). Defaults are the stock game's behaviour.
/// </summary>
[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public partial record struct StagePhysics
{
    [TightPackOrder(0), PluginVisible] public float Gravity { get; init; } = 1f;
    [TightPackOrder(1), PluginVisible] public float Grip { get; init; } = 1f;
    [TightPackOrder(2), PluginVisible] public float Powerloss { get; init; } = 1f;
    [TightPackOrder(3), PluginVisible] public float Damage { get; init; } = 1f;

    /// <summary>Percent of max health a grounded car loses per original-rate tick.</summary>
    [TightPackOrder(4), PluginVisible]
    public float Lava { get; init; }

    /// <summary>Clamp for per-collision impulse; stock is 300.</summary>
    [TightPackOrder(5), PluginVisible]
    public float MaxHit { get; init; } = 300f;

    [TightPackOrder(6), PluginVisible] public float Recoil { get; init; } = 1f;

    /// <summary>≥ 1. Damaged cars get stronger (moment/push/lift scale up, revpush/revlift down).</summary>
    [TightPackOrder(7), PluginVisible]
    public float Rage { get; init; } = 1f;

    /// <summary>The <c>stats()</c> directive: use each car's recharged stat set.</summary>
    [TightPackOrder(8), PluginVisible]
    public bool RechargedStats { get; init; }

    [TightPackOrder(9), PluginVisible] public bool NoArrow { get; init; }

    // Note: default(StagePhysics) skips the initializers above and yields all zeros (no gravity).
    // Construct with new StagePhysics() anywhere a stage's physics are supplied.
    public StagePhysics()
    {
    }
}
