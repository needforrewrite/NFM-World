using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

/// <summary>
/// A car's stats after the current <see cref="StagePhysics"/> have been applied.
/// Rebuilt whenever the stage properties or the car's health change - the physics loop reads
/// these every tick, so they are plain fields in the units the loop already uses.
/// </summary>
[PluginVisible]
public struct EffectiveStats
{
    public fix64 Moment;
    public fix64 Push;
    public fix64 Lift;
    public fix64 Revpush;
    public fix64 Revlift;

    /// <summary><see cref="CarStats.Grip"/> scaled by the stage's <c>grip()</c> override.</summary>
    public fix64 Grip;

    /// <summary>Damage taken as a fraction of max health (0..1).</summary>
    public fix64 DamageFraction;

    public static EffectiveStats From(CarStats stats, StagePhysics physics, int damagePoints)
    {
        // phy-addons' Mad.update(rage): a damaged car trades survivability for shove.
        // at full health the scaling is 1, so stock stages are unaffected.
        var damage = stats.Maxmag > 0
            ? fix64.Min(fix64.Max((fix64)damagePoints / (fix64)stats.Maxmag, fix64.Zero), fix64.One)
            : fix64.Zero;
        // phy-addons' Mad.update(rage) only applies the buff when rage > 1; at 0 in a default
        // StagePhysics we would divide by zero.
        var rage = (fix64)physics.Rage;
        var strength = fix64.One;
        var weakness = fix64.One;
        if (physics.Rage > 1f)
        {
            strength = fix64.One + damage * (rage - fix64.One);
            weakness = fix64.One + damage * (fix64.One / rage - fix64.One);
        }

        return new EffectiveStats
        {
            Moment = stats.Moment * strength,
            Push = stats.Push * strength,
            Lift = stats.Lift * strength,
            Revpush = stats.Revpush * weakness,
            Revlift = stats.Revlift * weakness,
            Grip = stats.Grip * (fix64)physics.Grip,
            DamageFraction = damage,
        };
    }
}
