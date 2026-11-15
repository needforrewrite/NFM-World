using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

public static partial class GarageView
{
    private sealed class GaragePaintList : ScrollView
    {
        private Component? _revealed;

        public GaragePaintList(Node content)
        {
            Name = "Garage color list";
            Styles = Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                marginTop: 10,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                paddingRight: 8
            );
            AddChild(content);
        }

        protected override void RenderContent(Vector2 position, Vector2 size)
        {
            base.RenderContent(position, size);
            var state = UiState.Garage;
            var index = state.ActiveColorSlot.Value;
            var name = index >= 0 && index < state.EditingSlots.Count
                ? "Garage color " + state.EditingSlots[index].Slot
                : index == -1 ? "Garage wheel " + (state.OriginalWheels.Value ? "Classic" : "Model") : "";
            var target = state.CustomizeTabsFocused.Value ? null : Find(this, name);
            if (ReferenceEquals(target, _revealed)) return;
            _revealed = target;
            target?.ScrollIntoView();
        }

        private static Component? Find(Node node, string name)
        {
            if (node is Component component && component.Name == name) return component;
            foreach (var child in node.VisualChildren)
                if (Find(child, name) is { } found) return found;
            return null;
        }
    }

    private static Node CustomizeTabs(Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                flexShrink: 0,
                marginTop: 12,
                backgroundColor: new Color(7, 14, 28, 180),
                borderBottom: 1,
                borderColor: Theme.MenuBorder
            ),
            children: new[] { CustomizeTab("Body", false, actions), CustomizeTab("Wheels", true, actions) }
        );

    private static Node CustomizeTab(string label, bool wheels, Actions actions)
    {
        var hovered = new Signal<bool>(false);
        var active = () => UiState.Garage.WheelsTab.Value == wheels;
        return Sx.View(
            name: () => "Garage customize tab " + label,
            style: () => Sx.Styles(
                position: Position.Relative,
                paddingLeft: 24,
                paddingRight: 24,
                height: 36,
                minWidth: 110,
                flexShrink: 0,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: active() ? new Color(22, 42, 80, 220)
                    : hovered.Value ? new Color(22, 42, 80, 130) : Theme.Transparent
            ),
            onmousedown: _ => actions.CustomizeTab?.Invoke(wheels),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.Text(style: () => Sx.TextStyles(
                    fontSize: 14,
                    fontFamily: FontFamily.NotoSans,
                        fontStyle: FontStyle.Bold,
                        horizontalAlignment: TextHorizontalAlignment.Center,
                        foregroundColor: active() ? RowBorderActive : Theme.MenuDim
                ), text: () => label
                ),
                Sx.View(style: () => Sx.Styles(
                    position: Position.Absolute,
                    left: 0,
                    right: 0,
                    bottom: 0,
                    height: 3,
                    pointerEvents: false,
                    backgroundColor: active() ? UiState.Garage.CustomizeTabsFocused.Value
                        ? RowBorderActive : Theme.MenuBlue : Theme.Transparent
                )
                )
            ]
        );
    }

    private static Node WheelChoices(Actions actions)
        => Sx.View(style: () => Sx.Styles(
            width: 100.Percent(),
            flexDirection: FlexDirection.Row,
                gapColumn: 10,
            flexShrink: 0
        ), children:
            [
                Sx.Show(
                    when: () => UiState.Garage.HasCustomWheels.Value,
                    then: () => WheelChoice("Model", false, actions)
                ),
                WheelChoice("Classic", true, actions)
            ]);

    private static Node WheelChoice(string label, bool original, Actions actions)
    {
        var hovered = new Signal<bool>(false);
        var selected = () => UiState.Garage.OriginalWheels.Value == original;
        var key = () => CarThumbnails.WheelKey(UiState.Garage.CurrentCar.Value?.FileName ?? "", original);
        return Sx.View(
            name: () => "Garage wheel " + label,
            style: () => Sx.Styles(
                width: 108,
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                paddingTop: 6,
                paddingBottom: 6,
                paddingLeft: 6,
                paddingRight: 6,
                gapRow: 4,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: selected() ? RowBorderActive : Theme.MenuBorder,
                backgroundColor: selected() || hovered.Value ? Theme.MenuHighlight : Theme.MenuFill
            ),
            onmousedown: _ => actions.WheelStyle?.Invoke(original),
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.For<string>(() => [key()], (id, _) => WheelThumbnail(id)),
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 11,
                    horizontalAlignment: TextHorizontalAlignment.Center,
                        foregroundColor: Theme.MenuTextBlue
                    ),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: () => label
                )
            ]
        );
    }

    private static Node WheelThumbnail(string id)
    {
        var ready = CarThumbnails.ReadySignal(id);
        return Sx.Show(
            () => ready.Value,
            () => new NFMWorld.DriverInterface.UI.Image
            {
                ImageData = CarThumbnails.Get(id)!,
                Styles = Sx.Styles(width: 100.Percent(), aspectRatio: 1, flexShrink: 0)
            },
            () => new LoadingSpinner
            {
                Styles = Sx.Styles(width: 100.Percent(), aspectRatio: 1, flexShrink: 0)
            }
        );
    }
}
