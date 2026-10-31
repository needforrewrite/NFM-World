using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.UI.Routes;

public static class MainMenuIntroView
{
    public static Node Build(IImage logo, Func<Node, Node> menu, Func<Node> garage)
    {
        var state = UiState.MainMenu;
        return Sx.View(
            style: () => Sx.Styles(width: "100%", height: "100%"),
            children:
            [
                Sx.Show(() => !state.MenuVisible.Value, () => Sx.View(
                    style: () => Sx.Styles(position: Position.Absolute, width: "100%", height: "100%",
                        flexDirection: FlexDirection.Column, justifyContent: Justify.Center,
                        alignItems: Align.Center, pointerEvents: false),
                    children:
                    [
                        Sx.View(style: () => Sx.Styles(width: "52%", maxWidth: 700,
                                opacity: state.LogoOpacity.Value),
                            children: [Logo(logo)]),
                        Sx.View(
                            style: () => Sx.Styles(marginTop: 28, opacity: state.PromptOpacity.Value,
                                paddingLeft: 26, paddingRight: 26, paddingTop: 10, paddingBottom: 10),
                            children: [Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 28, fontFamily: FontFamily.Adventure,
                                    fontStyle: FontStyle.Bold, foregroundColor: new Color(185, 211, 255),
                                    strokeColor: new Color(12, 12, 80),
                                    horizontalAlignment: TextHorizontalAlignment.Center),
                                text: () => "PRESS ANY BUTTON")])
                    ])),
                Sx.Show(() => state.MenuVisible.Value, () => Sx.View(
                    style: () => Sx.Styles(width: "100%", height: "100%", pointerEvents: state.InputEnabled.Value),
                    children:
                    [
                        new MenuShade
                        {
                            Styles = Sx.Styles(position: Position.Absolute, left: 0, top: 0,
                                width: "68%", height: "100%", pointerEvents: false)
                        },
                        Sx.Show(() => !state.GarageOpen.Value, () => menu(Logo(logo))),
                        Sx.Show(() => state.GarageOpen.Value, () => garage()),
                        MainMenuView.SettingsOverlay()
                    ])),
                Sx.View(style: () => Sx.Styles(position: Position.Absolute, top: 0, left: 0,
                    width: "100%", height: "100%", backgroundColor: new Color(0, 0, 0),
                    opacity: state.BlackOpacity.Value, pointerEvents: false, zIndex: 100))
            ]);
    }

    private static Node Logo(IImage logo) => new NFMWorld.DriverInterface.UI.Image
    {
        ImageData = logo,
        Styles = Sx.Styles(width: "100%", aspectRatio: (float)logo.Width / logo.Height, flexShrink: 0)
    };

    private sealed class MenuShade : Component
    {
        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            G.SetLinearGradient((int)position.X, (int)position.Y, (int)size.X, 0,
                [new Color(3, 8, 20, 205), new Color(3, 8, 20, 0)], null);
            G.FillRect((int)position.X, (int)position.Y, (int)size.X, (int)size.Y);
        }
    }
}
