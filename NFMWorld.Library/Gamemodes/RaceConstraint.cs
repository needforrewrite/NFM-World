using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// What a race is decided by, from the gamemode's <c>constraint</c> config option.
/// </summary>
[PluginVisible(Visible.IncludingAllMembersOfType)]
public readonly record struct RaceConstraint(bool Racing, bool Wasting)
{
    public static RaceConstraint Both { get; } = new(true, true);

    /// <summary>Reads the <c>constraint</c> option, defaulting to both.</summary>
    public static RaceConstraint From(IReadOnlyDictionary<string, object>? config)
        => config is not null && config.TryGetValue("constraint", out var value)
            ? From(value?.ToString())
            : Both;

    public static RaceConstraint From(string? value) => value?.Trim().ToLowerInvariant() switch
    {
        "racing" => new RaceConstraint(Racing: true, Wasting: false),
        "wasting" => new RaceConstraint(Racing: false, Wasting: true),
        _ => Both
    };
}
