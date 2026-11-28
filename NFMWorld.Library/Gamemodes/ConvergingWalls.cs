using System.Globalization;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// The PvP <c>walls</c> option: minutes until the stage boundary walls start closing in.
/// <c>off</c> (or zero) disables it. Read by the client gamemode, which drives
/// <see cref="Backend.ClosingWalls"/> from the race clock.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly record struct ConvergingWalls(double Minutes)
{
    [PluginVisible] public bool Enabled => Minutes > 0;

    [PluginVisible] public static ConvergingWalls Off { get; } = new(0);

    [PluginVisible]
    public static ConvergingWalls From(IReadOnlyDictionary<string, object>? config)
        => config is not null && config.TryGetValue("walls", out var value)
            ? From(value?.ToString())
            : Off;

    [PluginVisible]
    public static ConvergingWalls From(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))

            return Off;

        var text = value.Trim();

        if (text.Equals("off", StringComparison.OrdinalIgnoreCase))

            return Off;

        return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var minutes)
            ? new ConvergingWalls(Math.Clamp(minutes, 0, 60))
            : Off;
    }
}
