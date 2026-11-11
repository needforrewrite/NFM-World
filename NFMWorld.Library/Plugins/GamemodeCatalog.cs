using NFMWorldLibrary.Gamemodes.DotNet;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The gamemodes this build ships, and the config each one declares. The create dialog reads
/// this to render its options, so it never has to know what a gamemode's settings are.
/// </summary>
/// <remarks>
/// A gamemode is loaded to read its schema, then released: the plugin belongs to a race, not
/// to the menu. The cost is one assembly load per dialog open, which is what listing the
/// options unavoidably needs.
/// </remarks>
public static class GamemodeCatalog
{
    /// <summary>The gamemodes offered in the lobby, in display order.</summary>
    public static IReadOnlyList<string> Ids { get; } = [DefaultGamemodes.PvP, DefaultGamemodes.TimeTrial];

    public static string DisplayName(string id) => id switch
    {
        DefaultGamemodes.PvP => "PvP",
        DefaultGamemodes.TimeTrial => "Time Trial",
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
}
