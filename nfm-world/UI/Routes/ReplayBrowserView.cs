using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>One recording in the local replay browser.</summary>
public sealed record ReplayBrowserEntry(
    string File,
    string Stage,
    string Car,
    string Time,
    bool Compatible,
    string Status,
    bool Retryable
);

/// <summary>Browse the recordings under <c>data/tts</c> and open or retry one.</summary>
public static class ReplayBrowserView
{
    public sealed record Actions(Action<string> Open, Action<string> Retry, Action Back);

    public static Node Build(
        Signal<IReadOnlyList<ReplayBrowserEntry>> entries,
        Signal<string> message,
        Actions actions
    )
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                paddingTop: 40,
                paddingBottom: 32,
                paddingLeft: 48,
                paddingRight: 48,
                flexDirection: FlexDirection.Column,
                gapRow: 12,
                backgroundColor: new Color(0, 0, 0, 120)
            ),
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(fontSize: 32, fontStyle: FontStyle.Bold), text: () => "Replays"),
                Sx.Show(
                    when: () => message.Value.Length > 0,
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 12, foregroundColor: new Color(255, 160, 80)),
                        text: () => message.Value
                    )
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        flexGrow: 1,
                        minHeight: 0,
                        flexDirection: FlexDirection.Column,
                        overflow: Overflow.Scroll,
                        gapRow: 6
                    ),
                    children:
                    [
                        Sx.For(each: () => entries.Value, row: (entry, _) => Row(entry, actions)),
                        Sx.Show(
                            when: () => entries.Value.Count == 0,
                            then: () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 14, foregroundColor: Theme.TextFaint),
                                text: () => "No replays yet. Finish a time trial to record one."
                            )
                        )
                    ]
                ),
                Button.Create(label: () => "← Back", onPress: actions.Back)
            ]
        );

    private static Node Row(ReplayBrowserEntry entry, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 16,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 14,
                paddingRight: 14,
                borderRadius: 6,
                backgroundColor: new Color(22, 42, 80, 140)
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(flexGrow: 1, flexDirection: FlexDirection.Column),
                    onmousedown: _ => actions.Open(entry.File),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold),
                            text: () => $"{entry.Stage}  ·  {entry.Car}"
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 11,
                                foregroundColor: entry.Compatible ? Theme.TextFaint : new Color(255, 160, 80)
                            ),
                            text: () => entry.Compatible
                                ? $"{entry.Time}  ·  {entry.Status}"
                                : $"Incompatible recording  ·  {entry.Status}"
                        )
                    ]
                ),
                Sx.Show(
                    when: () => entry.Retryable,
                    then: () => Button.Create(
                        label: () => "RETRY UPLOAD",
                        onPress: () => actions.Retry(entry.File),
                        size: ButtonSize.Sm
                    )
                )
            ]
        );
}
