using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.Hud;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.UI.Routes;

internal static class RaceFinishView
{
    public static View Banner(UiState.RaceState race, RaceFinishState? finish)
        => Sx.View(
            name: () => "Race finish announcement",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                width: 100.Percent(),
                height: 100.Percent(),
                pointerEvents: false
            ),
            children: [
                Sx.View(style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 5.Percent(),
                    right: 5.Percent(),
                    top: 24.Percent(),
                    flexDirection: FlexDirection.Column,
                    gapRow: 8
                ), children: [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold | FontStyle.Italic,
                        fontSize: 48,
                    foregroundColor: HudSkinRegistry.UsesClassicSkin(race.Skin.Value) ? new Color(0, 0, 0) : Theme.MenuBlue,
                    strokeColor: new Color(255, 255, 255),
                    strokeWidth: 1.5f,
                    horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                    text: () => race.ResultTitle.Value ?? (race.ClientWon.Value ? "YOU WON!" : "YOU LOST!")
                ),
                Sx.Show(() => !string.IsNullOrWhiteSpace(race.StateText.Value), () => Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 20,
                        fontStyle: FontStyle.Bold,
                    foregroundColor: new Color(255, 255, 255),
                        strokeColor: new Color(0, 0, 0),
                    breakType: BreakType.Word,
                        overflowBehavior: OverflowBehavior.ContinueVertically,
                    horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                    text: () => race.StateText.Value ?? ""
                ))
                ]),
                Sx.Show(() => finish?.CanContinue.Value == true, () => Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 14,
                        foregroundColor: new Color(255, 255, 255),
                        strokeColor: new Color(0, 0, 0),
                        horizontalAlignment: TextHorizontalAlignment.Center
                    ),
                    layout: () => Sx.Styles(position: Position.Absolute, left: 5.Percent(), right: 5.Percent(), top: 74.Percent()),
                    text: () => finish?.ContinueSpectating.Value == true
                        ? "Press [ Enter ] to continue spectating" : "Press [ Enter ] to continue"
                )
                )
            ]
        );

    public static Node Summary(RaceFinishState finish, Action continueAction)
        => Sx.View(
            name: () => "Race results",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                right: 0,
                bottom: 0,
                backgroundColor: new Color(62, 42, 18, 195),
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                zIndex: 600
            ),
            children: [
                Sx.View(
                    style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: 8.Percent(),
                        right: 8.Percent(),
                    top: 18.Percent(),
                        flexDirection: FlexDirection.Column,
                        alignItems: Align.Center,
                        gapRow: 12
                    ),
                    children: [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontFamily: FontFamily.Adventure,
                                fontSize: 42,
                            fontStyle: FontStyle.Bold | FontStyle.Italic,
                                foregroundColor: new Color(255, 222, 164),
                            strokeColor: new Color(152, 72, 12),
                                horizontalAlignment: TextHorizontalAlignment.Center
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => finish.Won.Value ? "CONGRATULATIONS!" : finish.Lost.Value ? "GAME OVER!" : "RACE FINISHED"
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(
                                fontSize: 18,
                                fontStyle: FontStyle.Bold,
                            foregroundColor: new Color(255, 213, 147),
                                horizontalAlignment: TextHorizontalAlignment.Center
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => finish.Won.Value ? "You won!" : finish.Lost.Value ? "You lost this race." : "Race over."
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(
                                fontSize: 20,
                                fontStyle: FontStyle.Bold,
                            foregroundColor: new Color(255, 151, 40),
                                horizontalAlignment: TextHorizontalAlignment.Center
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => finish.StageName.Value
                        ),
                        Sx.Text(
                            style: () => Sx.WrappingText(
                                fontSize: 14,
                                foregroundColor: new Color(255, 235, 195),
                            horizontalAlignment: TextHorizontalAlignment.Center
                            ),
                            layout: () => Sx.Styles(width: 100.Percent()),
                            text: () => finish.Description.Value
                        )
                    ]
                ),
                Sx.View(style: () => Sx.Styles(position: Position.Absolute, bottom: 16.Percent()), children: [
                    Button.Create(
                        () => "CONTINUE",
                        continueAction,
                        style: () => Sx.Styles(
                            backgroundColor: new Color(65, 70, 0, 235),
                            borderColor: new Color(190, 205, 22),
                            borderRadius: 12,
                            paddingLeft: 20,
                            paddingRight: 20
                        ),
                        textStyle: () => Sx.TextStyles(
                            fontFamily: FontFamily.Adventure,
                            fontSize: 24,
                            foregroundColor: new Color(255, 224, 35)
                        )
                    )
                ])
            ]
        );
}
