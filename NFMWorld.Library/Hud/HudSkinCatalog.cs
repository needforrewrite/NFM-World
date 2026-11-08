using NFMWorld.PhysicsFS;

namespace NFMWorldLibrary.Hud;

public sealed record HudSkinPackage(string Directory, HudSkinDefinition Definition);

public static class HudSkinCatalog
{
    public const string Directory = "data/hud/skins";

    public static IReadOnlyList<HudSkinPackage> Discover(VirtualFS fs, Action<string, Exception> report)
    {
        var packages = new List<HudSkinPackage>();
        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "classic", "world" };
        foreach (var path in fs.EnumerateFiles(Directory, "skin.json", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            try
            {
                var definition = HudSkinDefinition.Parse(fs.ReadAllText(path));
                var folder = path[..path.LastIndexOf('/')];
                if (folder != $"{Directory}/{definition.Id}")
                    throw new FormatException("Install skins in data/hud/skins/<id>/skin.json.");
                if (!ids.Add(definition.Id)) throw new FormatException($"Duplicate or reserved skin id '{definition.Id}'.");
                packages.Add(new HudSkinPackage(folder, definition));
            }
            catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException or IOException
                                           or ArgumentException or UnauthorizedAccessException
                                           or System.Security.SecurityException)
            {
                report(path, ex);
            }
        }

        return packages;
    }
}
