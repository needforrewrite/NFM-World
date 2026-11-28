using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Hud;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary.Hud;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

public static class RaceHudView
{
    /// <summary><paramref name="Restart"/> is null when the host owns the race (online).</summary>
    public sealed record Actions(
        Action Resume,
        Action? Restart,
        Action Quit,
        RaceChatState? Chat = null,
        Action<string>? SendChat = null,
        Action? CloseChat = null,
        RaceFinishState? Finish = null,
        Action? ContinueFinish = null,
        Signal<bool>? HudVisible = null);

    public static Node Build(Actions actions)
        => Sx.View(
            style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent()),
            children: [
            Sx.Show(() => actions.Finish?.ShowSummary.Value != true, () => BuildHud(actions)),
            Sx.Show(
                () => actions.Finish?.ShowSummary.Value == true,
                () => RaceFinishView.Summary(actions.Finish!, actions.ContinueFinish!)
            )
        ]
        );

    private static Node BuildHud(Actions actions)
    {
        HudSkinRegistry.Refresh();
        var race = UiState.Race;
        race.Skin.Value = SettingsMenu.HudSkin;

        return Sx.View(
            style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), position: Position.Relative),
            children:
            [
                Sx.Show(
                    () => actions.HudVisible?.Value != false,
                    () => Sx.View(
                        style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent(), position: Position.Absolute),
                        children: [
                HudSkinRegistry.BuildWithGamemodeWidgets(
                    race,
                    () => Sx.Switch(race.Skin, HudSkinRegistry.Branches(race), () => HudSkinRegistry.BuildDefault(race))
                ),
                new RaceOverlays(
                    race,
                    () => race.Skin.Value == "world" || HudSkinRegistry.DrawsRadar(race.Skin.Value),
                    () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value),
                    () => race.StageSnap.Value
                ),
                new RaceMessages(race),
                Sx.Show(
                    () => HudSkinRegistry.ShowDefaultMessages(race.Skin.Value)
                          && race.Message.Length > 0
                          && !(race.Finished.Value
                               && (race.ResultTitle.Value is not null
                                   || race.ClientWon.Value
                                   || race
                                   .ClientLost.Value)),
                    () => new HudScaleHost(
                        GlassCardUi.CenterText(
                            () => race.Message,
                            24,
                            colorAccessor: () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value)
                                ? new Color(0, 0, 0)
                                : Theme.TextSecondary
                        )
                    )
                ),
                Sx.Show(
                    () => race.Finished.Value
                          && (race.ResultTitle.Value is not null
                              || race.ClientWon.Value
                              || race.ClientLost
                              .Value),
                    () => new HudScaleHost(RaceFinishView.Banner(race, actions.Finish))
                ),
                    ]
                    )
                ),
                Sx.Show(
                    () => actions.Chat is not null,
                    () => RaceChatView.Build(
                        actions.Chat!,
                        () => HudSkinRegistry.UsesClassicSkin(race.Skin.Value),
                        actions.SendChat!,
                        actions.CloseChat!
                    )
                ),
                Sx.Show(() => race.Paused.Value, () => PauseMenuView.Build(actions))
            ]
        );
    }
}
