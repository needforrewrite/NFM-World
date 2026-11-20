using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
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
        Register(
            new HudSkinInfo("classic", "Classic", "NeedForRewrite Team", "The classic Need for Madness look."),
            BuildDefault
        );
        Register(
            new HudSkinInfo(
                "world",
                "World",
                "NeedForRewrite Team",
                "Blue glass HUD with radar, standings and a destination arrow."
            ),
            data => new HudScaleHost(new WorldSkin.WorldHud(data).Build())
        );

        Register(
            new HudSkinInfo(
                "dsmod2",
                "DSmod2",
                "DragShot (phy addons)",
                "phy's DSmod2 gadget set: outlined boxes, a big position ordinal and a dial speedometer."
            ),
            data => new GadgetSkin.Dsmod2Hud(data)
            {
                Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
            }
        );
        Register(
            new HudSkinInfo(
                "blackfusion",
                "Black Fusion",
                "DragShot (phy addons)",
                "phy's Black Fusion gadget set: dark panels, a split-arc speedometer and the ring guidance arrow."
            ),
            data => new GadgetSkin.BlackFusionHud(data)
            {
                Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
            }
        );
        Register(
            new HudSkinInfo(
                "digital7",
                "Digital 7",
                "DragShot (phy addons)",
                "phy's Digital 7 gadget set: seven-segment digits, an LED bar speedometer and swept-arc guidance."
            ),
            data => new GadgetSkin.Digital7Hud(data)
            {
                Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
            }
        );
        Register(
            new HudSkinInfo(
                "s7",
                "S7",
                "DragShot (phy addons)",
                "phy's S7 gadget set: skewed panels, a rainbow-rimmed speedometer and the wide chevron arrow."
            ),
            data => new GadgetSkin.S7Hud(data)
            {
                Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
            }
        );
        try
        {
            foreach (var package in HudSkinCatalog.Discover(
                         VFS,
                         (path, error) =>
                             Logging.Error($"HUD skin '{path}': {error.Message}")
                     ))
                Register(
                    package.Definition.Info,
                    data => new CustomHud(package, data)
                    {
                        Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
                    },
                    package.Definition.ShowDefaultMessages
                );
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

    /// <summary>Whether the skin draws its own radar, so the shared one must stay out of the way.</summary>
    public static bool DrawsRadar(string id) => id is "world" or "dsmod2" or "blackfusion" or "digital7" or "s7";

    public static Node BuildDefault(IHudData data) => new ClassicHud(data)
    {
        Styles = Sx.Styles(width: 100.Percent(), height: 100.Percent(), pointerEvents: false)
    };

    public static IReadOnlyDictionary<string, Func<Node>> Branches(IHudData data)
        => new BranchMap(data);

    /// <summary>
    /// The chosen skin's tree plus the gamemode's declared widgets. Every skin goes through
    /// here, so a gamemode's custom UI reaches the classic, world and JSON skins alike; the
    /// widgets sit above the skin because the immediate-mode classic skin draws from RenderContent.
    /// </summary>
    public static Node BuildWithGamemodeWidgets(IHudData data, Func<Node> skin)
        => Sx.View(
            style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), position: Position.Relative),
            children:
            [
                skin(),
                AnchoredWidget(GamemodeHudWidgets.Build(data, GamemodeHudAnchor.TopLeft), Sx.Styles(top: 232, left: 16)),
                AnchoredWidget(
                    GamemodeHudWidgets.Build(data, GamemodeHudAnchor.TopCenter),
                    Sx.Styles(top: 120, left: 0, right: 0, alignItems: Align.Center)
                ),
                AnchoredWidget(
                    GamemodeHudWidgets.Build(data, GamemodeHudAnchor.TopRight),
                    Sx.Styles(top: 300, right: 16, alignItems: Align.End)
                ),
                AnchoredWidget(
                    GamemodeHudWidgets.Build(data, GamemodeHudAnchor.BottomCenter),
                    Sx.Styles(bottom: 120, left: 0, right: 0, alignItems: Align.Center)
                )
            ]
        );

    private static Node AnchoredWidget(Node widget, Styles placement) => Sx.View(
        style: () => placement.Over(Sx.Styles(position: Position.Absolute, flexShrink: 0)),
        children: [widget]
    );

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
