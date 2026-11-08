using NFMWorld.DriverInterface.DriverInterface;
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
                new RaceOverlays(race, () => race.Skin.Value == "world",
                    () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value), () => race.StageSnap.Value),
                new RaceMessages(race),
                Sx.Show(() => HudSkinRegistry.ShowDefaultMessages(race.Skin.Value) && race.Message.Length > 0,
                    () => new HudScaleHost(GlassCardUi.CenterText(() => race.Message, 24,
                        colorAccessor: () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value)
                            ? new Color(0, 0, 0) : Theme.TextSecondary))),
                Sx.Show(
                    () => race.Finished.Value && HudSkinRegistry.UsesClassicSkin(race.Skin.Value)
                          && (race.ClientWon.Value || race.ClientLost.Value),
                    () => new HudScaleHost(GlassCardUi.CenterText(
                        () => race.ClientWon.Value ? "YOU WON!" : "YOU LOST!",
                        48, color: new Color(0, 0, 0), top: 100f, height: 120f,
                        fontFamily: FontFamily.Adventure, fontStyle: FontStyle.Bold | FontStyle.Italic))),
                Sx.Show(() => race.Paused.Value, () => PauseMenuView.Build(actions))
            ]);
    }
}
