using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Hud;

/// <summary>
/// Mounts the widgets a gamemode declares through <see cref="GamemodeHudWidget"/>. The
/// gamemode supplies only data (an anchor, a label, a 0..1 accessor); the node is built here,
/// which keeps plugin UI inside the host's Sx and sandbox rules.
/// </summary>
public static class GamemodeHudWidgets
{
    /// <summary>
    /// One self-hiding node per anchor: it mounts the widgets for <paramref name="anchor"/> the
    /// first time the gamemode declares any, and stays mounted after that. The default HUD skins
    /// place it at the matching spot in their layout.
    /// </summary>
    public static Node Build(IHudData race, string anchor) => Sx.Show(
        when: () => Declared(race, anchor) is { Count: > 0 },
        then: () =>
        {
            var widgets = Declared(race, anchor) ?? [];
            return Sx.View(
                style: () => Sx.Styles(flexDirection: FlexDirection.Column, gapRow: 8),
                children: [.. widgets.Select(w => BuildWidget(w, race))]
            );
        }
    );

    private static List<GamemodeHudWidget>? Declared(IHudData race, string anchor)
    {
        // Plain-list data: reading the revision re-runs this accessor once per frame, which is
        // what lets Sx.Show mount widgets the gamemode declared after the HUD was built.
        _ = (race as UiState.RaceState)?.PresentationRevision.Value;
        var widgets = (race as UiState.RaceState)?.GamemodeWidgets;
        if (widgets is null || widgets.Count == 0)
            return null;

        List<GamemodeHudWidget> declared = [];
        foreach (var widget in widgets)
            if (string.Equals(widget.Anchor, anchor, StringComparison.Ordinal))
                declared.Add(widget);
        return declared;
    }

    private static View BuildWidget(GamemodeHudWidget widget, IHudData race)
    {
        // The gamemode's accessor reads plain fields, not signals, so the bar's style effect
        // would never re-run on its own. Reading the per-frame revision subscribes it to the
        // one signal the phase bumps every tick.
        var fill = GlassCardUi.StatBar(
            widget.Label,
            () =>
            {
                _ = (race as UiState.RaceState)?.PresentationRevision.Value;
                return Math.Clamp((double)widget.Value(), 0, 1);
            },
            Theme.Accent,
            8
        );
        return Sx.View(
            style: () => Sx.Styles(width: 190, flexShrink: 0),
            children: [fill]
        );
    }
}
