using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Hud;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Hud;

namespace NFMWorld.UI.Routes;

public static class RaceHudView
{
    public sealed record Actions(Action Resume, Action Restart, Action Quit);

    public static Node Build(Actions actions)
    {
        HudSkinRegistry.Refresh();
        var race = UiState.Race;
        race.Skin.Value = SettingsMenu.HudSkin;
        return Sx.View(
            style: () => Sx.Styles(width: "100%", height: "100%", position: Position.Relative),
            children:
            [
                Sx.Switch(race.Skin, HudSkinRegistry.Branches(race), () => HudSkinRegistry.BuildDefault(race)),
                Sx.Show(() => HudSkinRegistry.ShowDefaultMessages(race.Skin.Value) && race.Message.Length > 0,
                    () => new HudScaleHost(GlassCardUi.CenterText(() => race.Message, 24,
                        colorAccessor: () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value)
                            ? HudColorSnap.Apply(Theme.TextSecondary, race.StageSnap.Value) : Theme.TextSecondary))),
                Sx.Show(() => race.Paused.Value, () => PauseMenuView.Build(actions))
            ]);
    }
}
