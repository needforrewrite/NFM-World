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
/// <remarks>
/// Everything sits in a slim top-centre band so the recording stays visible: the viewer exists to
/// watch the car, and a bar across the bottom hides the road ahead of it.
/// </remarks>
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
            name: () => "Replay viewer",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                right: 0,
                top: 0,
                flexDirection: FlexDirection.Column,
                alignItems: Align.Center,
                paddingTop: 12
            ),
            children: [Band(state, actions)]
        );

    private static Node Band(ReplayViewState state, Actions actions)
        => Sx.View(
            name: () => "Replay viewer band",
            style: () => Sx.Styles(
                width: 760,
                maxWidth: 96.Percent(),
                flexDirection: FlexDirection.Column,
                gapRow: 6,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 12,
                paddingRight: 12,
                borderRadius: 10,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                backgroundColor: new Color(6, 14, 30, 210)
            ),
            children:
            [
                Sx.Show(
                    when: () => state.MissingContent.Value,
                    then: () => Sx.Text(
                        layout: () => Sx.Styles(maxWidth: 720),
                        style: () => Sx.WrappingText(fontSize: 12, foregroundColor: Theme.Warning),
                        text: () => state.Info.Value
                    )
                ),
                ControlRow(state, actions),
                Scrubber(state, actions),
                Hint(state)
            ]
        );

    private static Node ControlRow(ReplayViewState state, Actions actions)
        => Sx.View(
            name: () => "Replay viewer controls",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                flexShrink: 0,
                gapColumn: 6
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 16, fontStyle: FontStyle.Bold),
                    text: () => state.TimeText.Value
                ),
                Sx.View(style: () => Sx.Styles(width: 10, flexShrink: 0)),
                Button.Create(
                    label: () => state.Playing.Value ? "PAUSE" : "PLAY",
                    onPress: actions.TogglePlay,
                    size: ButtonSize.Sm,
                    name: () => "Replay play"
                ),
                Button.Create(label: () => "RESTART", onPress: actions.Restart, size: ButtonSize.Sm),
                Button.Create(label: () => "−1", onPress: () => actions.Step(-1), size: ButtonSize.Sm),
                Button.Create(label: () => "+1", onPress: () => actions.Step(1), size: ButtonSize.Sm),
                Button.Create(label: () => "CAM", onPress: actions.CycleCamera, size: ButtonSize.Sm),
                Button.Create(
                    label: () => $"{state.Speed.Value:0.##}x",
                    onPress: () => actions.SetSpeed(NextSpeed(state.Speed.Value)),
                    size: ButtonSize.Sm
                ),
                Button.Create(
                    label: () => "← BACK",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    size: ButtonSize.Sm,
                    name: () => "Replay back"
                )
            ]
        );

    private static Node Scrubber(ReplayViewState state, Actions actions)
        => Sx.View(
            name: () => "Replay viewer scrubber",
            style: () => Sx.Styles(
                width: 720,
                maxWidth: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center
            ),
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
        );

    private static Node Hint(ReplayViewState state)
        => Sx.Text(
            layout: () => Sx.Styles(maxWidth: 720),
            style: () => Sx.WrappingText(fontSize: 10, foregroundColor: Theme.TextFaint),
            text: () => $"Frame {state.Frame.Value + 1} / {state.TotalFrames.Value}   ·   " +
                        "Space play/pause · ←/→ scrub · ,/. frame · 1-4 speed · C camera"
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
