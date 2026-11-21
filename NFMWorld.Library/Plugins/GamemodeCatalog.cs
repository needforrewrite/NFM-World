using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The gamemodes this build ships, and the config each one declares. The create dialog reads
/// this to render its options, so it never has to know what a gamemode's settings are.
/// </summary>
/// <remarks>
/// A gamemode is loaded to read its schema, then released: the plugin belongs to a race, not
/// to the menu. Reading it is the lobby phase's job (<see cref="ConfigFields"/>), never a
/// view's: a view slot re-runs for reasons the schema does not depend on, and each run would
/// load and unload the plugin again.
/// </remarks>
public static class GamemodeCatalog
{
    public static IReadOnlyList<string> Ids { get; } =
        [DefaultGamemodes.PvP, DefaultGamemodes.TimeTrial, DefaultGamemodes.Rocket];

    public static string DisplayName(string id) => id switch
    {
        DefaultGamemodes.PvP => "PvP",
        DefaultGamemodes.TimeTrial => "Time Trial",
        DefaultGamemodes.Rocket => "Rocket Madness",
        _ => id
    };

    /// <summary>
    /// Loads <paramref name="gamemodeId"/> to read its config schema. Returns an empty list if
    /// the plugin is missing or declares nothing; the caller is not told which.
    /// </summary>
    public static IReadOnlyList<GamemodeConfigProperty> ConfigProperties(string gamemodeId)
    {
        // The schema is the only thing wanted here, so the plugin is loaded and released at
        // once: a missing or broken plugin costs the dialog its options, not the menu.
        var plugin = GamemodePlugin.Load(
            gamemodeId,
            PluginPaths.For("gamemodes", gamemodeId, "gamemode.dll"),
            PluginPaths.PackageFor("gamemodes", gamemodeId, "gamemode.radpack")
        );
        if (plugin is null)
            return [];

        try
        {
            return [.. plugin.ConfigProperties];
        }
        finally
        {
            plugin.Unload();
        }
    }

    /// <summary>
    /// <see cref="ConfigProperties"/> projected to plain records, keeping only the fields that
    /// offer valid choices for the editor.
    /// </summary>
    public static IReadOnlyList<GamemodeConfigField> ConfigFields(string gamemodeId) =>
        [.. ConfigProperties(gamemodeId)
            .Where(property => property.Options.Count > 0)
            .Select(property => new GamemodeConfigField(
                property.Name,
                property.Label,
                [.. property.Options.Select(option => new GamemodeConfigChoice(option.Label, option.Value))],
                IntegerInput: property.Type == typeof(int),
                property.VisibleWhenProperty,
                property.VisibleWhenValue
            ))];
}

/// <summary>One value a gamemode's config field offers.</summary>
public sealed record GamemodeConfigChoice(string Label, string Value);

/// <summary>
/// A config field as the create dialog renders it. Copied out of the plugin, so holding one
/// cannot keep the plugin's assembly loaded.
/// </summary>
public sealed record GamemodeConfigField(
    string Name,
    string Label,
    IReadOnlyList<GamemodeConfigChoice> Choices,
    bool IntegerInput = false,
    string? VisibleWhenProperty = null,
    string? VisibleWhenValue = null
)
{
    public bool IsVisible(Func<string, string?> getValue)
        => VisibleWhenProperty is not { } property
           || getValue(property) is { } value && value == VisibleWhenValue;
}
