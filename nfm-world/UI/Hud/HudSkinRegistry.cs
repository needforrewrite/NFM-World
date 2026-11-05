using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI.Hud;

public static class HudSkinRegistry
{
    private sealed record Registration(HudSkinInfo Info, Func<IHudData, Node> Build, bool ShowDefaultMessages);
    private static readonly Dictionary<string, Registration> Skins = new(StringComparer.Ordinal);
    private static readonly HashSet<string> Failed = new(StringComparer.Ordinal);
    private static bool _loaded;

    public static IReadOnlyList<HudSkinInfo> Options
    {
        get
        {
            if (!_loaded) Refresh();
            return Skins.Values.Select(s => s.Info).ToArray();
        }
    }

    public static void Refresh()
    {
        Skins.Clear();
        Failed.Clear();
        Register(new HudSkinInfo("classic", "Classic", "NeedForRewrite Team", "The classic Need for Madness look."), BuildDefault);
        Register(new HudSkinInfo("world", "World", "NeedForRewrite Team", "Blue glass HUD with radar, standings and a destination arrow."),
            data => new HudScaleHost(new WorldSkin.WorldHud(data).Build()));
        try
        {
            foreach (var package in HudSkinCatalog.Discover(VFS, (path, error) =>
                Logging.Error($"HUD skin '{path}': {error.Message}")))
                Register(package.Definition.Info, data => new CustomHud(package, data)
                {
                    Styles = Sx.Styles(width: "100%", height: "100%", pointerEvents: false)
                }, package.Definition.ShowDefaultMessages);
        }
        catch (Exception ex)
        {
            Logging.Error($"Could not discover HUD skins: {ex.Message}");
        }
        _loaded = true;
    }

    private static void Register(HudSkinInfo info, Func<IHudData, Node> build, bool showDefaultMessages = true)
        => Skins.Add(info.Id, new(info, build, showDefaultMessages));

    public static bool ShowDefaultMessages(string id)
        => Failed.Contains(id) || !Skins.TryGetValue(id, out var skin) || skin.ShowDefaultMessages;

    public static bool UsesClassicSkin(string id) => id == "classic" || Failed.Contains(id) || !Skins.ContainsKey(id);

    public static Node BuildDefault(IHudData data) => new ClassicHud(data)
    {
        Styles = Sx.Styles(width: "100%", height: "100%", pointerEvents: false)
    };

    public static IReadOnlyDictionary<string, Func<Node>> Branches(IHudData data)
        => new BranchMap(data);

    private static Node Build(string id, IHudData data)
    {
        try
        {
            return Skins[id].Build(data);
        }
        catch (Exception ex)
        {
            Failed.Add(id);
            Logging.Error($"Could not initialize HUD skin '{id}': {ex.Message}. Using Classic.");
            return BuildDefault(data);
        }
    }

    // Resolve on selection so skins discovered by a later settings visit can mount too.
    private sealed class BranchMap(IHudData data) : IReadOnlyDictionary<string, Func<Node>>
    {
        public bool TryGetValue(string key, out Func<Node> value)
        {
            value = () => Build(key, data);
            return Skins.ContainsKey(key);
        }
        public Func<Node> this[string key] => () => Build(key, data);
        public IEnumerable<string> Keys => Skins.Keys;
        public IEnumerable<Func<Node>> Values => Keys.Select(key => this[key]);
        public int Count => Skins.Count;
        public bool ContainsKey(string key) => Skins.ContainsKey(key);
        public IEnumerator<KeyValuePair<string, Func<Node>>> GetEnumerator()
            => Keys.Select(key => new KeyValuePair<string, Func<Node>>(key, this[key])).GetEnumerator();
        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator() => GetEnumerator();
    }
}
