using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;

namespace NFMWorld.UI.Components;

internal static class LoadingOverlay
{
    public static Node Build() => Sx.Show(
        () => LoadingWork.Status.Value.Length > 0,
        () => Sx.View(
            style: () => Sx.Styles(
                position: Position.Absolute,
                right: 20,
                bottom: 20,
                maxWidth: 85.Percent(),
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 10,
                paddingLeft: 12,
                paddingRight: 16,
                paddingTop: 8,
                paddingBottom: 8,
                backgroundColor: Theme.MenuPanel,
                borderRadius: 8,
                pointerEvents: false,
                zIndex: 1000
            ),
            children:
            [
                new LoadingSpinner { Styles = Sx.Styles(width: 28, height: 28, flexShrink: 0) },
                Sx.Text(
                    style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.MenuBlue),
                    text: () => LoadingWork.Status.Value
                )
            ]
        )
    );
}
