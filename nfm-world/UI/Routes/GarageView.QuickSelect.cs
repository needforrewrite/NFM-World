using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;

namespace NFMWorld.UI.Routes;

public static partial class GarageView
{
    private const int QuickSidebarWidth = 220;
    private static bool _compactPreview;

    public static Node BuildQuickSelect(Actions actions, string confirmLabel = "Race", bool thumbnailPreview = false)
        => Sx.Show(
            when: () => UiState.Garage.Page.Value == GaragePage.Colors,
            then: () => Build(actions, thumbnailPreview),
            otherwise: () => QuickSelectPanels(actions, confirmLabel, thumbnailPreview)
        );

    private static Node QuickSelectPanels(Actions actions, string confirmLabel, bool thumbnailPreview)
    {
        _compactPreview = true;
        var sidebar = Sx.View(
            name: () => "Quick car details",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: 0,
                top: 0,
                bottom: 0,
                width: QuickSidebarWidth,
                paddingTop: 16,
                paddingBottom: 16,
                paddingLeft: 14,
                paddingRight: 14,
                flexDirection: FlexDirection.Column,
                gapRow: 8,
                backgroundColor: Theme.MenuPanel,
                borderRight: 1,
                borderColor: Theme.MenuBorder
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 18,
                        fontFamily: FontFamily.Adventure,
                        foregroundColor: MenuBlue
                    ),
                    text: () => "CHOOSE YOUR CAR"
                ),
                SelectedCarHeading(QuickSidebarWidth - 28),
                Sx.View(
                    style: () => Sx.Styles(
                        height: 28,
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column
                    ),
                    children: PaintSwatches()
                ),
                StatsPanel(),
                Button.Create(
                    () => confirmLabel,
                    actions.Confirm,
                    size: ButtonSize.Sm,
                    fullWidth: true,
                    name: () => "Quick car confirm"
                ),
                Button.Create(
                    () => "Customize",
                    () => actions.Navigate(GaragePage.Colors),
                    size: ButtonSize.Sm,
                    fullWidth: true
                ),
                Button.Create(() => "Back", actions.Back, size: ButtonSize.Sm, fullWidth: true)
            ]
        );
        var strip = new ScrollView
        {
            Name = "Quick car strip",
            Styles = Sx.Styles(
                flexGrow: 1,
                minHeight: 0,
                overflow: Overflow.Scroll,
                flexDirection: FlexDirection.Row,
                paddingBottom: 10
            )
        };
        strip.AddChild(Sx.View(
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0, gapColumn: 10),
            children: Sx.For(
                each: () => Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
                    .SelectMany(collection => collection.Cars).ToArray(),
                row: (car, _) =>
                {
                    var tile = new QuickCarTile(car.FileName)
                    {
                        Styles = Sx.Styles(width: 154, flexShrink: 0, flexDirection: FlexDirection.Column)
                    };
                    tile.AddChild(CarCard(car, car.Collection, actions, fillWidth: true));

                    return tile;
                }
            )
        )
        );
        var categoryPicker = (View)Controls.Dropdown(
            options: () => UiState.Garage.Categories.Value,
            value: () => UiState.Garage.Categories.Value.ToList().IndexOf(UiState.Garage.Category.Value),
            onSelect: index =>
            {
                actions.CategorySelected(UiState.Garage.Categories.Value[index]);
                FocusManager.ClearFocus();
            },
            width: 120
        );
        categoryPicker.Name = "Quick car category";
        var dock = Sx.View(
            name: () => "Quick car picker",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: QuickSidebarWidth,
                right: 0,
                bottom: 0,
                height: 210,
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 12,
                paddingRight: 12,
                gapRow: 8,
                flexDirection: FlexDirection.Column,
                backgroundColor: Theme.MenuPanel,
                borderTop: 1,
                borderColor: Theme.MenuBorder
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        justifyContent: Justify.FlexEnd,
                        gapColumn: 10,
                        flexShrink: 0
                    ),
                    children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 10, foregroundColor: TextBlue),
                        layout: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                        text: () => "← → car  ·  ↑ ↓ category  ·  Enter race  ·  C customize  ·  Esc back"
                    ),
                    categoryPicker,
                    SearchBox(actions, releaseFocus: true, compact: true)
                ]
                ),
                strip,
                StatusLine()
            ]
        );
        MenuPanelNode = sidebar;
        DetailPanelNode = dock;

        return Sx.View(
            name: () => "Quick car select",
            style: () => Sx.Styles(width: 100.Percent(), height: 100.Percent()),
            children: [
                Sx.Show(() => thumbnailPreview, () => ThumbnailPreview(QuickSidebarWidth, 210)),
                sidebar, dock]
        );
    }

    private static Node ThumbnailPreview(float left, float bottom = 0)
        => Sx.View(
            name: () => "Garage thumbnail preview",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: left,
                right: 0,
                top: 0,
                bottom: bottom,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                pointerEvents: false
            ),
            children: [Sx.View(
                style: () => Sx.Styles(width: 70.Percent()),
                children: [
                Sx.For(
                    () => UiState.Garage.CurrentCar.Value is { } car ? new[] { car.FileName } : [],
                    (identity, _) => CarThumbnail(identity)
                )
            ]
            )]
        );

    private sealed class QuickCarTile(string identity) : View
    {
        private bool _selected;

        protected override bool OnPostLayout()
        {
            var selected = UiState.Garage.CurrentCar.Value?.FileName == identity;
            if (selected && !_selected)
                ScrollIntoView();
            _selected = selected;

            return base.OnPostLayout();
        }
    }
}
