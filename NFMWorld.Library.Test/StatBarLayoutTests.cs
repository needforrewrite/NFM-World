using Lua;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorldLibrary.Util;

namespace NFMWorld.Library.Test;

/// <summary>
/// Guards <c>StatBar</c>'s track width against the flex parent it is mounted in.
///
/// StatBar's track is <c>width = "100%"</c> and the fill inside it is a percentage of the
/// track. A percentage resolves against the parent's definite size, so when StatBar's root
/// had no width of its own it depended entirely on how the caller laid it out: inside a
/// column the parent's width is definite and it worked, but racehud puts the bar in a row
/// next to its label, where nothing supplies a basis. Yoga then sized the whole bar from
/// the fill's own percentage, so lowering the value shrank the bar AND the fill shrank
/// again within it (50%% read as a quarter-width bar).
///
/// Giving the StatBar root <c>width = "100%"</c> makes the bar a percentage of its parent
/// in either direction, so only the fill moves. These tests mount the real
/// <c>glasscard.luau</c> through the real LuaUiLibrary host and lay it out with Yoga.
/// </summary>
[TestClass]
public class StatBarLayoutTests
{
    private static View? _root;
    private static LuaState? _state;

    private static string FindRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            if (Directory.Exists(Path.Combine(dir.FullName, "NFMWorld.Library", "data", "library")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new DirectoryNotFoundException("repo root not found");
    }

    private static IEnumerable<Component> Descendants(Component node)
    {
        foreach (var child in node.VisualChildren)
        {
            if (child is not Component c)
            {
                continue;
            }

            yield return c;
            foreach (var d in Descendants(c))
            {
                yield return d;
            }
        }
    }

    /// <summary>
    /// Mounts a StatBar through the real glasscard.luau. <paramref name="racehudLayout"/>
    /// reproduces the two real callers: racehud puts the bar in a row beside its label,
    /// while garage stacks bars directly inside a fixed-width column card.
    /// </summary>
    private static void MountBar(bool racehudLayout)
    {
        _root = null;

        IBackend.Backend = new DummyBackend();
        GameThreadContext.Install();
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(FindRepoRoot());

        var state = LuaHelpers.OpenState();
        LuaUiLibrary.Register(state, view => _root = view, (_, _) => { }, (_, _) => () => { });

        var body = racehudLayout
            ? """
              x(Sx.View)({ style = { width = 200, flexDirection = "column", gap = 8 },
                x(GlassCard)({
                  x(Sx.View)({ style = { flexDirection = "row" },
                    x(Sx.View)({ style = { marginBottom = 4 }, x(Sx.Text)({ "Power" }) }),
                    bar(),
                  }),
                }),
              })
              """
            : """
              x(Sx.View)({ style = { width = 340, flexDirection = "row" },
                x(Sx.View)({ style = { width = 280, padding = 24, flexDirection = "column" },
                  bar(),
                  bar(),
                }),
              })
              """;

        state.DoString(
            $$"""
            local Sx = require("../../library/sx/index")
            local GlassCard = require("./glasscard").GlassCard
            local StatBar = require("./glasscard").StatBar
            local x = Sx.x

            local value, setValue = Sx.createSignal(1)
            _G.setBarValue = setValue

            local function bar()
              return x(StatBar)({ label = "", value = function() return value() end, color = "#ff5252", height = 10 })
            end

            Sx.render(
              {{body}}
            )
            """,
            "NFMWorld.Library/data/uis/components/probe.luau"
        );

        GameThreadContext.Current.ExecutePendingTasks();
        _state = state;
    }

    /// <summary>
    /// Snapshots the StatBar's track and fill widths. The fill is the leaf View painted in
    /// the bar's own colour; the track is its parent (the pale, clipped background). The
    /// widths are read out as floats because the Components are live: a later layout would
    /// otherwise change what an earlier capture reports.
    /// </summary>
    private static (float Track, float Fill) MeasureTrackAndFill()
    {
        var fill = Descendants(_root!)
            .Last(c => c.Styles.BackgroundColor is not null && !Descendants(c).Any());
        var track = (Component)fill.VisualParent!;
        return (track.LayoutWidth, fill.LayoutWidth);
    }

    private static void SetValueAndLayout(double value)
    {
        _state!.DoString(
            $"setBarValue({value.ToString(System.Globalization.CultureInfo.InvariantCulture)})",
            "probe-set"
        );
        GameThreadContext.Current.ExecutePendingTasks();
        _root!.LayoutAndRender(new LuaVector2(800, 600));
    }

    [TestMethod]
    public void StatBar_KeepsTrackWidth_WhileFillShrinks()
    {
        foreach (var (label, racehudLayout) in new[] { ("racehud row", true), ("garage column", false) })
        {
            MountBar(racehudLayout);
            _root!.LayoutAndRender(new LuaVector2(800, 600));

            var (fullTrackWidth, _) = MeasureTrackAndFill();
            Assert.IsTrue(fullTrackWidth > 0, $"the bar must have a width in the {label} layout");

            SetValueAndLayout(1.0);
            var (trackFull, fillFull) = MeasureTrackAndFill();

            SetValueAndLayout(0.5);
            var (trackHalf, fillHalf) = MeasureTrackAndFill();

            SetValueAndLayout(0.0);
            var (trackEmpty, fillEmpty) = MeasureTrackAndFill();

            Console.WriteLine($"[{label}] 100%: track={trackFull:0.##} fill={fillFull:0.##}");
            Console.WriteLine($"[{label}]  50%: track={trackHalf:0.##} fill={fillHalf:0.##}");
            Console.WriteLine($"[{label}]   0%: track={trackEmpty:0.##} fill={fillEmpty:0.##}");

            // The track is the bar's visible extent; it must not move with the value.
            Assert.AreEqual(fullTrackWidth, trackFull, 0.5f, $"track stays put at 100% ({label})");
            Assert.AreEqual(fullTrackWidth, trackHalf, 0.5f, $"track stays put at 50% ({label})");
            Assert.AreEqual(fullTrackWidth, trackEmpty, 0.5f, $"track stays put at 0% ({label})");

            // Only the fill responds, and it is a percentage OF the track.
            Assert.AreEqual(fullTrackWidth, fillFull, 0.5f, $"fill is full at 100% ({label})");
            Assert.AreEqual(fullTrackWidth / 2f, fillHalf, 0.5f, $"fill is half at 50% ({label})");
            Assert.AreEqual(0f, fillEmpty, 0.5f, $"fill is empty at 0% ({label})");
        }
    }
}
