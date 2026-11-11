using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>Playback state the replay viewer renders.</summary>
public sealed class ReplayViewState
{
    public Signal<bool> Playing { get; } = new(false);
    public Signal<int> Frame { get; } = new(0);
    public Signal<int> TotalFrames { get; } = new(0);
    public Signal<double> Speed { get; } = new(1);
    public Signal<string> TimeText { get; } = new("0:00.000");
    public Signal<string> Info { get; } = new("");
    public Signal<bool> MissingContent { get; } = new(false);
}

/// <summary>
/// The replay viewer's chrome: playback controls, scrubber and content warning. Input is the
/// viewer's (keyboard shortcuts live in the phase); this only calls back.
/// </summary>
public static class ReplayView
{
    public sealed record Actions(
        Action TogglePlay,
        Action Restart,
        Action<int> Step,
        Action<double> Seek,
        Action<double> SetSpeed,
        Action CycleCamera,
        Action Back
    );

    public static Node Build(ReplayViewState state, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                bottom: 0,
                paddingTop: 16,
                paddingBottom: 16,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column,
                gapRow: 8,
                backgroundColor: new Color(0, 0, 0, 170)
            ),
            children:
            [
                Sx.Show(
                    when: () => state.MissingContent.Value,
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 13, foregroundColor: new Color(255, 160, 80)),
                        text: () => state.Info.Value
                    )
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 20, fontStyle: FontStyle.Bold),
                    text: () => state.TimeText.Value
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, gapColumn: 8, alignItems: Align.Center),
                    children:
                    [
                        Button.Create(
                            label: () => state.Playing.Value ? "PAUSE" : "PLAY",
                            onPress: actions.TogglePlay,
                            size: ButtonSize.Sm
                        ),
                        Button.Create(label: () => "RESTART", onPress: actions.Restart, size: ButtonSize.Sm),
                        Button.Create(label: () => "− FRAME", onPress: () => actions.Step(-1), size: ButtonSize.Sm),
                        Button.Create(label: () => "+ FRAME", onPress: () => actions.Step(1), size: ButtonSize.Sm),
                        Button.Create(label: () => "CAMERA", onPress: actions.CycleCamera, size: ButtonSize.Sm),
                        Button.Create(
                            label: () => $"{state.Speed.Value:0.##}x",
                            onPress: () => actions.SetSpeed(NextSpeed(state.Speed.Value)),
                            size: ButtonSize.Sm
                        ),
                        Button.Create(label: () => "← BACK", onPress: actions.Back, size: ButtonSize.Sm)
                    ]
                ),
                Sx.View(
                    style: () => Sx.Styles(flexDirection: FlexDirection.Row, alignItems: Align.Center, gapColumn: 8),
                    children:
                    [
                        Controls.Slider(
                            () => (double)state.Frame.Value,
                            value => actions.Seek(value),
                            0,
                            Math.Max(1, state.TotalFrames.Value - 1),
                            1
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    text: () => $"Frame {state.Frame.Value + 1} / {state.TotalFrames.Value}   ·   " +
                                "Space play/pause · ←/→ scrub · ,/. frame · 1-4 speed · C camera"
                )
            ]
        );

    private static double NextSpeed(double current)
        => current switch
        {
            < 0.5 => 0.5,
            < 1 => 1,
            < 2 => 2,
            < 4 => 4,
            _ => 0.25
        };
}
