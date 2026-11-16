using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI.Routes;

/// <summary>What a row on the garage's home menu does.</summary>
public enum GarageHomeAction
{
    ChangeCar,
    CustomizeCar,
    Inventory,
    Back
}

/// <summary>A row on the garage's home menu.</summary>
public sealed record GarageHomeEntry(
    string Label,
    string Description,
    GarageHomeAction Action,
    bool Enabled = true
);

/// <summary>
/// Garage menus over the live preview, with a full-screen car catalogue.
/// </summary>
public static partial class GarageView
{
    /// <summary>Id of the archive tab, alongside the installed collection ids.</summary>
    public const string OnlineCategory = "Online";
    public const string AllCategory = "All";

    /// <summary>Home and paint menu width.</summary>
    internal const int MenuWidth = 460;

    /// <summary>Stats card width, anchored to the opposite edge.</summary>
    internal const int DetailWidth = 292;

    /// <summary>Distance from the window edges for the floating panels.</summary>
    private const int PanelInset = 24;

    internal static readonly Signal<int> ColumnCount = new(3);
    public static int GridColumns => ColumnCount.Value;
    internal static int ColumnsForWidth(float width) => Math.Max(3, (int)(width / 480));
    internal static readonly Signal<bool> NavigatingCategories = new(false);
    private const int TileGap = 14;

    /// <summary>Height cap for the paint editor.</summary>

    /// <summary>The panels, so an orbit press can be ignored over them and only over them.</summary>
    internal static View? MenuPanelNode { get; private set; }
    internal static View? DetailPanelNode { get; private set; }

    /// <summary>The home menu. The keyboard walks the same list the view renders.</summary>
    public static readonly IReadOnlyList<GarageHomeEntry> HomeEntries =
    [
        new("CHANGE CAR", "Choose the vehicle you drive.", GarageHomeAction.ChangeCar),
        new("CUSTOMIZE CAR", "Customize the body paint and wheels.", GarageHomeAction.CustomizeCar),
        new(
            "INVENTORY",
            "View and inspect your inventory items.",
            GarageHomeAction.Inventory,
            Enabled: false
        ),
        new("BACK", "Return to the main menu.", GarageHomeAction.Back)
    ];

    /// <summary>What the garage asks the game to do. The controller fills these in.</summary>
    public sealed record Actions(
        Action<string, string> CarSelected,
        Action Confirm,
        Action Back,
        Action<string> CategorySelected,
        Action<string> OnlineSearch,
        Action<GaragePage> Navigate,
        Action<int> ActivateHome,
        Action<int, int, double> SetColorChannel,
        Action ResetColors,
        Action<bool>? CustomizeTab = null,
        Action<bool>? WheelStyle = null,
        Action<bool>? PaintInteraction = null
    );

    private static readonly Color MenuBlue = Theme.MenuBlue;
    private static readonly Color TextBlue = Theme.MenuTextBlue;
    private static readonly Color MenuDim = Theme.MenuDim;
    private static readonly Color RowBorder = new(128, 170, 255, 195);
    private static readonly Color RowBorderActive = new(205, 227, 255);

    private static readonly Styles CarCardSelected = CardStyle(Theme.MenuHighlight, RowBorderActive, 1);

    private static readonly Styles CarCardHover = CardStyle(new Color(40, 62, 105, 200), RowBorder, 0.95f);

    private static readonly Styles CarCardIdle = CardStyle(Theme.MenuFill, RowBorder, 0.82f);

    private static Styles CardStyle(Color background, Color border, float opacity)
        => Sx.Styles(
            width: 0,
            flexGrow: 1,
            minWidth: 0,
            flexShrink: 0,
            paddingTop: 4,
            paddingBottom: 4,
            paddingLeft: 4,
            paddingRight: 4,
            flexDirection: FlexDirection.Column,
            gapRow: 3,
            overflow: Overflow.Hidden,
            borderTop: 1,
            borderBottom: 1,
            borderLeft: 1,
            borderRight: 1,
            borderRadius: 2,
            backgroundColor: background,
            borderColor: border,
            opacity: opacity
        );

    /// <summary>Empties the search box. Returns false when it was already empty.</summary>
    public static bool ClearSearch()
    {
        if (UiState.Garage.SearchQuery.Value.Length == 0)
            return false;

        UiState.Garage.SearchQuery.Value = "";
        return true;
    }

    /// <summary>The car list after the search filter, shared by the view and the keyboard.</summary>
    public static IReadOnlyList<CarCollectionViewModel> Filter(
        IReadOnlyList<CarCollectionViewModel> collections,
        string query
    )
    {
        query = query.Trim();
        if (query.Length == 0)
            return collections;

        var result = new List<CarCollectionViewModel>();
        foreach (var collection in collections)
        {
            var matched = collection.Cars
                .Where(car => car.Name.Contains(query, StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (matched.Length > 0)
                result.Add(collection with { Cars = matched });
        }

        return result;
    }

    public static Node Build(Actions actions, bool thumbnailPreview = false)
    {
        _compactPreview = false;
        var menu = MenuPanel(actions);
        var detail = DetailPanel(actions);
        MenuPanelNode = menu;
        DetailPanelNode = detail;

        return Sx.View(
            name: () => "Garage",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                backgroundColor: UiState.Garage.Page.Value == GaragePage.Cars
                    ? new Color(10, 18, 36, 245) : Theme.Transparent
            ),
            children: [
                thumbnailPreview ? ThumbnailPreview(MenuWidth + PanelInset + 20) : null,
                menu, detail,
                thumbnailPreview ? null : PreviewHint()]
        );
    }

    /// <summary>True when a point is over a panel, so the orbit must not start there.</summary>
    internal static bool OverUi(int x, int y) => !_compactPreview && UiState.Garage.Page.Value == GaragePage.Cars
                                                 || Inside(MenuPanelNode, x, y) || Inside(DetailPanelNode, x, y);

    private static bool Inside(View? panel, int x, int y)
    {
        if (panel is not { IsDisplayed: true } || panel.LayoutBorderSize.X <= 0)
            return false;

        var position = panel.LayoutBorderPosition;
        var size = panel.LayoutBorderSize;
        return x >= position.X && x <= position.X + size.X
               && y >= position.Y && y <= position.Y + size.Y;
    }

    // ── Left menu ─────────────────────────────────────────────────────

    private static View MenuPanel(Actions actions)
        => Sx.View(
            name: () => "Garage menu",
            style: () =>
            {
                var browsing = UiState.Garage.Page.Value == GaragePage.Cars;
                var bounded = UiState.Garage.Page.Value != GaragePage.Home;
                return Sx.Styles(
                    position: Position.Absolute,
                    left: PanelInset,
                    top: PanelInset,
                    right: browsing ? DetailWidth + PanelInset + 20 : (MeasurementMarginPosition?)null,
                    width: browsing ? MeasurementWidthHeight.Auto() : MenuWidth,
                    bottom: bounded ? PanelInset : (MeasurementMarginPosition?)null,
                    height: MeasurementWidthHeight.Auto(),
                    paddingTop: browsing ? 14 : 24,
                    paddingBottom: browsing ? 12 : 20,
                    paddingLeft: browsing ? 14 : 22,
                    paddingRight: browsing ? 14 : 22,
                    flexDirection: FlexDirection.Column,
                    backgroundColor: browsing ? Theme.Transparent : Theme.MenuPanel,
                    borderTop: 1,
                    borderBottom: 1,
                    borderLeft: 1,
                    borderRight: 1,
                    borderColor: Theme.MenuBorder,
                    borderRadius: 10
                );
            },
            children:
            [
                Sx.Switch(
                    key: UiState.Garage.Page,
                    branches: new Dictionary<GaragePage, Func<Node>>
                    {
                        [GaragePage.Home] = () => HomePage(actions),
                        [GaragePage.Cars] = () => CarsPage(actions),
                        [GaragePage.Colors] = () => ColorsPage(actions)
                    }
                )
            ]
        );

    /// <summary>The one line under the car: the panels own the key hints.</summary>
    private static View PreviewHint()
        => Sx.View(
            name: () => "Garage preview",
            style: () => Sx.Styles(
                position: Position.Absolute,
                left: MenuWidth + PanelInset + 20,
                right: PanelInset,
                bottom: 18,
                display: UiState.Garage.Page.Value == GaragePage.Cars ? Display.None : Display.Flex,
                justifyContent: Justify.Center,
                alignItems: Align.Center,
                pointerEvents: false
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    text: () => "Drag to orbit"
                )
            ]
        );

    private static Node HomePage(Actions actions)
        => Sx.View(
            name: () => "Garage home",
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Column
            ),
            children:
            [
                PageHeader("GARAGE", "Personalize your ride."),
                Sx.View(
                    name: () => "Garage home rows",
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Column,
                        flexShrink: 0,
                        gapRow: 8,
                        marginTop: 18
                    ),
                    children:
                    [
                        Sx.For(
                            each: () => HomeEntries,
                            row: (entry, index) => HomeRow(entry, index, actions)
                        )
                    ]
                ),
                DescriptionBar(() => HomeEntries[
                    Math.Clamp(UiState.Garage.HomeCursor.Value, 0, HomeEntries.Count - 1)
                ].Description),
                Hint("↑ ↓ navigate · Enter select · Esc back")
            ]
        );

    private static Node HomeRow(GarageHomeEntry entry, int index, Actions actions)
    {
        var selected = () => UiState.Garage.HomeCursor.Value == index;

        var row = Sx.View(
            name: () => "Garage home " + entry.Label,
            style: () => Sx.Styles(
                height: 46,
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 10,
                paddingLeft: 14,
                paddingRight: 14,
                backgroundColor: selected()
                    ? entry.Enabled ? Theme.MenuHighlight : new Color(40, 62, 105, 150)
                    : Theme.MenuFill,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: selected() ? RowBorderActive : RowBorder,
                borderRadius: 6,
                opacity: entry.Enabled ? 1 : 0.62f
            ),
            onmouseenter: _ => UiState.Garage.HomeCursor.Value = index,
            onmousedown: _ => actions.ActivateHome(index),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 21,
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: selected() ? new Color(235, 244, 255) : MenuBlue
                    ),
                    layout: () => Sx.Styles(flexGrow: 1, flexShrink: 1),
                    text: () => entry.Label
                ),
                entry.Enabled
                    ? null
                    : Sx.View(
                        name: () => "Garage home badge " + entry.Label,
                        style: () => Sx.Styles(
                            paddingLeft: 8,
                            paddingRight: 8,
                            paddingTop: 2,
                            paddingBottom: 2,
                            borderRadius: 9,
                            backgroundColor: new Color(255, 179, 0, 60),
                            flexShrink: 0
                        ),
                        children:
                        [
                            Sx.Text(
                                style: () => Sx.TextStyles(
                                    fontSize: 10,
                                    fontStyle: FontStyle.Bold,
                                    foregroundColor: Theme.Warning
                                ),
                                text: () => "SOON"
                            )
                        ]
                    )
            ]
        );

        return row;
    }

    // ── Change car ────────────────────────────────────────────────────

    private static Node CarsPage(Actions actions)
        => Sx.View(
            name: () => "Garage cars",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Column
            ),
            children:
            [
                Sx.View(
                    name: () => "Garage catalogue toolbar",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        alignItems: Align.Center,
                        gapColumn: 12
                    ),
                    children:
                    [
                        BackLink(() => actions.Navigate(GaragePage.Home)),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 24,
                                fontFamily: FontFamily.Adventure,
                                foregroundColor: MenuBlue
                            ),
                            layout: () => Sx.Styles(flexGrow: 1, minWidth: 0),
                            text: () => "CHANGE CAR"
                        ),
                        SearchBox(actions)
                    ]
                ),
                CategoryTabs(actions),
                StatusLine(),
                CarList(actions),
                Hint("← → ↑ ↓ pick · ↑ from top row: tabs · Enter select · Esc back")
            ]
        );

    /// <summary>The picker's tabs: one per installed collection, then the archive.</summary>
    private static Node CategoryTabs(Actions actions)
        => Sx.View(
            name: () => "Garage categories",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                flexWrap: Wrap.Wrap,
                alignItems: Align.FlexStart,
                gapRow: 0,
                gapColumn: 0,
                marginTop: 14,
                backgroundColor: new Color(7, 14, 28, 180),
                borderBottom: 1,
                borderColor: RowBorder,
                flexShrink: 0
            ),
            children:
            [
                Sx.For(
                    each: () => UiState.Garage.Categories.Value,
                    row: (category, _) => CategoryTab(category, actions)
                )
            ]
        );

    private static View CategoryTab(string category, Actions actions)
    {
        var active = () => UiState.Garage.Category.Value == category;
        var hovered = new Signal<bool>(false);

        return Sx.View(
            name: () => "Garage category " + category,
            style: () => Sx.Styles(
                position: Position.Relative,
                paddingLeft: 24,
                paddingRight: 24,
                paddingTop: 14,
                paddingBottom: 14,
                minWidth: 110,
                alignItems: Align.Center,
                justifyContent: Justify.Center,
                backgroundColor: active()
                    ? new Color(22, 42, 80, 220)
                    : hovered.Value ? new Color(22, 42, 80, 130) : Theme.Transparent,
                flexShrink: 0
            ),
            onmousedown: _ =>
            {
                NavigatingCategories.Value = false;
                actions.CategorySelected(category);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 14,
                        fontFamily: FontFamily.NotoSans,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: active() ? new Color(205, 227, 255)
                            : hovered.Value ? new Color(185, 211, 255) : new Color(130, 155, 195)
                    ),
                    text: () => category
                ),
                Sx.View(
                    style: () => Sx.Styles(
                        position: Position.Absolute,
                        left: 0,
                        right: 0,
                        bottom: 0,
                        height: 3,
                        backgroundColor: active() && NavigatingCategories.Value ? new Color(230, 240, 255)
                            : active() ? MenuBlue : Theme.Transparent,
                        pointerEvents: false
                    )
                )
            ]
        );
    }

    private static Node SearchBox(Actions actions, bool releaseFocus = false, bool compact = false)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: compact ? MeasurementWidthHeight.Point(180) : 40.Percent(),
                paddingTop: compact ? 8 : 9,
                paddingBottom: compact ? 8 : 9,
                paddingLeft: 12,
                paddingRight: 12,
                backgroundColor: compact ? new Color(22, 42, 80, 145) : new Color(255, 255, 255, 18),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                borderRadius: 6,
                flexShrink: 0
            ),
            text: () => UiState.Garage.SearchQuery.Value,
            onchange: value => UiState.Garage.SearchQuery.Value = value,
            placeholder: () => UiState.Garage.OnlineSource.Value
                ? "Search the archive, Enter to load..."
                : "Search cars...",
            // Enter picks the first match; online it runs the archive search.
            onsubmit: _ =>
            {
                if (UiState.Garage.OnlineSource.Value)
                {
                    actions.OnlineSearch(UiState.Garage.SearchQuery.Value);
                    return;
                }

                if (Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
                    .FirstOrDefault() is { Cars.Count: > 0 } first)
                {
                    actions.CarSelected(first.Id, first.Cars[0].FileName);
                    UiState.Garage.SearchQuery.Value = "";
                    if (releaseFocus) FocusManager.ClearFocus();
                }
            }
        );
        input.Name = "Garage search";
        if (compact)
            input.TextStyles = Sx.TextStyles(fontSize: 13, foregroundColor: TextBlue).Resolved;
        return input;
    }

    private static Node StatusLine()
        => Sx.Show(
            when: () => UiState.Garage.OnlineStatus.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(fontSize: 11, foregroundColor: MenuDim),
                layout: () => Sx.Styles(width: 100.Percent(), marginTop: 8, flexShrink: 0),
                text: () => UiState.Garage.OnlineStatus.Value
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0, flexShrink: 0))
        );

    private static View CarList(Actions actions)
    {
        var collections = Sx.Computed(
            () => Filter(UiState.Garage.Collections.Value, UiState.Garage.SearchQuery.Value)
        );
        var cars = Sx.Computed(
            () => collections.Value
                .SelectMany(collection => collection.Cars)
                .ToArray()
        );

        return new GarageCarList
        {
            Name = "Garage car list",
            Styles = Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                marginTop: 10,
                marginBottom: 10,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                paddingRight: 8
            ),
            Children =
            {
                // A fixed-width column: the scroller then only ever scrolls vertically.
                Sx.View(
                    name: () => "Garage car list content",
                    style: () => Sx.Styles(
                        width: 100.Percent(),
                        flexShrink: 0,
                        flexDirection: FlexDirection.Column,
                        gapRow: 8
                    ),
                    children:
                    [
                        Sx.View(
                            name: () => "Garage grid",
                            style: () => Sx.Styles(
                                width: 100.Percent(),
                                flexShrink: 0,
                                flexDirection: FlexDirection.Column,
                                gapRow: 24
                            ),
                            children:
                            [
                                Sx.For(
                                    each: () => collections.Value,
                                    row: (collection, _) => CarSection(collection, actions)
                                )
                            ]
                        ),
                        Sx.Show(
                            when: () => cars.Value.Length == 0,
                            then: () => Sx.Text(
                                style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.TextFaint),
                                layout: () => Sx.Styles(
                                    width: 100.Percent(),
                                    paddingTop: 20,
                                    paddingBottom: 20,
                                    paddingLeft: 4
                                ),
                                text: () => UiState.Garage.OnlineSource.Value
                                    ? UiState.Garage.SearchQuery.Value.Trim().Length > 0
                                        ? $"No public cars match \"{UiState.Garage.SearchQuery.Value}\""
                                        : "Search the archive to list public cars."
                                    : UiState.Garage.SearchQuery.Value.Trim().Length > 0
                                        ? $"No cars match \"{UiState.Garage.SearchQuery.Value}\""
                                        : "No cars in this category."
                            )
                        )
                    ]
                )
            }
        };
    }

    private static View CarSection(CarCollectionViewModel collection, Actions actions)
        => Sx.View(
            name: () => "Garage collection " + collection.Id,
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Column,
                gapRow: TileGap
            ),
            children:
            [
                Sx.Show(
                    when: () => UiState.Garage.Category.Value == AllCategory,
                    then: () => Sx.View(
                        style: () => Sx.Styles(
                            width: 100.Percent(),
                            flexShrink: 0,
                            marginTop: 8,
                            paddingTop: 10,
                            paddingBottom: 10,
                            paddingLeft: 12,
                            paddingRight: 12,
                            borderBottom: 1,
                            borderColor: RowBorder
                        ),
                        children:
                        [
                            Sx.Text(
                                style: () => Sx.TextStyles(
                                    fontSize: 18,
                                    fontFamily: FontFamily.Adventure,
                                    foregroundColor: TextBlue
                                ),
                                text: () => collection.Id.ToUpperInvariant()
                            )
                        ]
                    )
                ),
                Sx.For(
                    each: () => collection.Cars.Chunk(GridColumns).ToArray(),
                    row: (row, _) => CarRow(row, actions)
                )
            ]
        );

    private static View CarRow(CarStatsViewModel[] cars, Actions actions)
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                flexDirection: FlexDirection.Row,
                gapColumn: TileGap
            ),
            children: Enumerable.Range(0, GridColumns).Select(index => Sx.View(
                style: () => Sx.Styles(
                    width: 0,
                    flexGrow: 1,
                    minWidth: 0,
                    flexShrink: 0,
                    flexDirection: FlexDirection.Column,
                    pointerEvents: index < cars.Length
                ),
                children: index < cars.Length
                    ? CarCard(cars[index], cars[index].Collection, actions, fillWidth: true)
                    : null
            )
            )
                .ToArray()
        );

    private static View CarCard(CarStatsViewModel car, string collectionId, Actions actions, bool fillWidth = false)
    {
        var isSelected = () => UiState.Garage.CurrentCar.Value?.FileName == car.FileName;
        var hovered = new Signal<bool>(false);

        return Sx.View(
            name: () => "Garage car " + car.FileName,
            style: () =>
            {
                var style = isSelected() ? CarCardSelected : hovered.Value ? CarCardHover : CarCardIdle;
                return fillWidth ? style.Over(Sx.Styles(width: 100.Percent(), flexGrow: 0)) : style;
            },
            onmousedown: _ =>
            {
                NavigatingCategories.Value = false;
                actions.CarSelected(collectionId, car.FileName);
            },
            onmouseenter: _ => hovered.Value = true,
            onmouseleave: _ => hovered.Value = false,
            children:
            [
                CarThumbnail(car.FileName),
                Sx.Text(
                    style: () => Sx.WrappingText(
                        fontSize: 14,
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        horizontalAlignment: TextHorizontalAlignment.Center,
                        foregroundColor: isSelected() ? new Color(235, 244, 255) : new Color(210, 226, 255)
                    ),
                    layout: () => Sx.Styles(width: 100.Percent(), height: 26),
                    text: () => car.Name
                )
            ]
        );
    }

    /// <summary>
    /// The card's car image. A model is baked to a texture a couple of times a frame, so the
    /// card holds a placeholder until its bake lands; the per-car signal keeps that swap local
    /// to the one card instead of rebuilding the grid.
    /// </summary>
    private static Node CarThumbnail(string id)
    {
        var ready = CarThumbnails.ReadySignal(id);
        return Sx.Show(
            when: () => ready.Value,
            then: () => new NFMWorld.DriverInterface.UI.Image
            {
                ImageData = CarThumbnails.Get(id)!,
                Styles = Sx.Styles(
                    width: 100.Percent(),
                    aspectRatio: 2,
                    flexShrink: 0,
                    borderRadius: 4
                )
            },
            otherwise: () =>
            {
                CarThumbnails.RequestKnown(id);
                return new LoadingSpinner
                {
                    Name = "Garage car placeholder " + id,
                    Styles = Sx.Styles(
                        width: 100.Percent(),
                        aspectRatio: 2,
                        flexShrink: 0,
                        borderRadius: 4,
                        backgroundColor: new Color(255, 255, 255, 10)
                    )
                };
            }
        );
    }

    // ── Paint ─────────────────────────────────────────────────────────

    private static Node ColorsPage(Actions actions)
        => Sx.View(
            name: () => "Garage colors",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Column
            ),
            children:
            [
                PageHeader("CUSTOMIZE CAR", "Choose your paint and wheels.", () => actions.Navigate(GaragePage.Home)),
                CustomizeTabs(actions),
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 18,
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: TextBlue
                    ),
                    layout: () => Sx.Styles(marginTop: 12, flexShrink: 0),
                    text: () => UiState.Garage.CurrentCar.Value?.Name ?? "No car selected"
                ),
                new GaragePaintList(
                        Sx.View(
                            style: () => Sx.Styles(
                                width: 100.Percent(),
                                flexShrink: 0,
                                flexDirection: FlexDirection.Column,
                                gapRow: 10
                            ),
                            children:
                            [
                                Sx.Show(
                                    when: () => UiState.Garage.WheelsTab.Value && UiState.Garage.HasCustomWheels.Value,
                                    then: () => WheelChoices(actions)
                                ),
                                Sx.For(
                                    each: () => UiState.Garage.EditingSlots,
                                    row: (slot, index) => ColorCard(slot, index, actions)
                                ),
                                Sx.Show(
                                    when: () => UiState.Garage.CurrentCar.Value is null,
                                    then: () => Sx.Text(
                                        style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.TextFaint),
                                        layout: () => Sx.Styles(width: 100.Percent(), paddingTop: 12),
                                        text: () => "Choose a car before painting it."
                                    )
                                ),
                                Sx.Show(
                                    when: () => UiState.Garage.CurrentCar.Value is not null
                                                && UiState.Garage.EditingSlots.Count == 0,
                                    then: () => Sx.Text(
                                        style: () => Sx.WrappingText(fontSize: 13, foregroundColor: Theme.TextFaint),
                                        layout: () => Sx.Styles(width: 100.Percent(), paddingTop: 12),
                                        text: () => UiState.Garage.WheelsTab.Value
                                            ? "This wheel mesh does not declare any editable colours."
                                            : "This car does not declare any editable colours."
                                    )
                                )
                            ]
                        )
                ),
                Sx.Show(
                    when: () => UiState.Garage.EditingSlots.Count > 0,
                    then: () => Button.Create(
                        label: () => "RESET COLORS",
                        onPress: actions.ResetColors,
                        fullWidth: true,
                        style: () => Sx.Styles(
                            marginTop: 8,
                            flexShrink: 0,
                            backgroundColor: UiState.Garage.ActiveColorSlot.Value == UiState.Garage.EditingSlots.Count
                                ? Theme.MenuHighlight : Theme.MenuFill,
                            borderColor: UiState.Garage.ActiveColorSlot.Value == UiState.Garage.EditingSlots.Count
                                ? RowBorderActive : RowBorder
                        ),
                        textStyle: () => Sx.TextStyles(
                            fontSize: 13,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: MenuBlue
                        ),
                        name: () => "Garage reset colors"
                    )
                ),
                Hint("Tab tabs · ↑ ↓ navigate · ← → control · Shift + ← → paint · Enter select")
            ]
        );

    private static Node ColorCard(UiState.GarageColorSlot slot, int index, Actions actions)
    {
        var active = () => !UiState.Garage.CustomizeTabsFocused.Value && UiState.Garage.ActiveColorSlot.Value == index;

        return Sx.View(
            name: () => "Garage color " + slot.Slot,
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 4,
                paddingTop: 8,
                paddingBottom: 8,
                paddingLeft: 10,
                paddingRight: 10,
                borderRadius: 6,
                borderLeft: 2,
                borderColor: active() ? Theme.MenuBlue : Theme.MenuBorder,
                backgroundColor: active() ? Theme.MenuFill : new Color(255, 255, 255, 12)
            ),
            onmousedown: _ =>
            {
                UiState.Garage.CustomizeTabsFocused.Value = false;
                UiState.Garage.ActiveColorSlot.Value = index;
            },
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 8,
                        flexShrink: 0
                    ),
                    children:
                    [
                        Sx.View(
                            name: () => "Garage color swatch " + slot.Slot,
                            style: () => Sx.Styles(
                                width: 26,
                                height: 14,
                                flexShrink: 0,
                                borderRadius: 3,
                                borderTop: 1,
                                borderBottom: 1,
                                borderLeft: 1,
                                borderRight: 1,
                                borderColor: new Color(255, 255, 255, 70),
                                backgroundColor: new Color(
                                    (byte)slot.R.Value,
                                    (byte)slot.G.Value,
                                    (byte)slot.B.Value
                                )
                            )
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 14,
                                fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: active() ? new Color(235, 244, 255) : MenuBlue
                            ),
                            layout: () => Sx.Styles(flexGrow: 1),
                            text: () => slot.Name
                        )
                    ]
                ),
                ColorChannelRow(slot, index, 0, "Hue", actions),
                ColorChannelRow(slot, index, 1, "Saturation", actions),
                ColorChannelRow(slot, index, 2, "Brightness", actions)
            ]
        );
    }

    private static Node ColorChannelRow(
        UiState.GarageColorSlot slot,
        int slotIndex,
        int channel,
        string label,
        Actions actions
    )
    {
        var active = () => !UiState.Garage.CustomizeTabsFocused.Value && UiState.Garage.ActiveColorSlot.Value == slotIndex
                           && UiState.Garage.ActiveColorChannel.Value == channel;

        return Sx.View(
            name: () => $"Garage color {slot.Slot} {label}",
            style: () => Sx.Styles(
                flexDirection: FlexDirection.Row,
                alignItems: Align.Center,
                gapColumn: 6,
                height: 22,
                paddingLeft: 4,
                paddingRight: 4,
                borderRadius: 4,
                flexShrink: 0,
                backgroundColor: active() ? new Color(128, 170, 255, 46) : Theme.Transparent
            ),
            onmousedown: _ =>
            {
                UiState.Garage.ActiveColorSlot.Value = slotIndex;
                UiState.Garage.ActiveColorChannel.Value = channel;
            },
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 10,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: active() ? new Color(235, 244, 255) : MenuDim
                    ),
                    layout: () => Sx.Styles(width: 66, flexShrink: 0),
                    text: () => label
                ),
                new ColorSpectrumSlider(
                    slot,
                    channel,
                    value => actions.SetColorChannel(slot.Slot, channel, value),
                    actions.PaintInteraction
                )
                {
                    Styles = Sx.Styles(flexGrow: 1, minWidth: 0, height: 22)
                },
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: new Color(255, 255, 255, 160)),
                    layout: () => Sx.Styles(width: 32, flexShrink: 0),
                    text: () => channel == 0
                        ? $"{Math.Round(slot.Hue.Value * 360)}°"
                        : $"{Math.Round(slot.Channel(channel) * 100)}%"
                )
            ]
        );
    }

    // ── Stats card ────────────────────────────────────────────────────

    private static View DetailPanel(Actions actions)
        => Sx.View(
            name: () => "Garage detail",
            style: () => Sx.Styles(
                position: Position.Absolute,
                right: PanelInset,
                top: PanelInset,
                bottom: PanelInset,
                width: DetailWidth,
                paddingTop: 18,
                paddingBottom: 16,
                paddingLeft: 18,
                paddingRight: 18,
                flexDirection: FlexDirection.Column,
                backgroundColor: Theme.MenuPanel,
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                borderRadius: 2,
                overflow: Overflow.Hidden,
                visibility: UiState.Garage.Page.Value == GaragePage.Cars
                    ? Visibility.Visible
                    : Visibility.Hidden
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 10, foregroundColor: MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "SELECTED CAR"
                ),
                Sx.Show(
                    when: () => UiState.Garage.CurrentCar.Value is not null,
                    then: () => Sx.View(
                        style: () => Sx.Styles(
                            width: 100.Percent(),
                            flexShrink: 0,
                            flexDirection: FlexDirection.Column,
                            gapRow: 8
                        ),
                        children:
                        [
                            Sx.View(
                                style: () => Sx.Styles(
                                    width: 100.Percent(),
                                    flexShrink: 0,
                                    flexDirection: FlexDirection.Column,
                                    backgroundColor: new Color(38, 55, 84)
                                ),
                                children: [(Func<Node>)(() => CarThumbnail(UiState.Garage.CurrentCar.Value!.FileName))]
                            ),
                            SelectedCarHeading()
                        ]
                    ),
                    otherwise: () => Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 20,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: MenuBlue
                        ),
                        layout: () => Sx.Styles(flexShrink: 0),
                        text: () => "Nothing selected"
                    )
                ),
                PaintSwatches(),
                StatsPanel(),
                Sx.Show(
                    when: () => UiState.Garage.CurrentCar.Value is not null,
                    then: () => Button.Create(
                        label: () => "SELECT CAR",
                        onPress: actions.Confirm,
                        fullWidth: true,
                        style: () => Sx.Styles(
                            paddingTop: 10,
                            paddingBottom: 10,
                            backgroundColor: Theme.MenuHighlight,
                            borderColor: RowBorderActive
                        ),
                        textStyle: () => Sx.TextStyles(
                            fontSize: 15,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: new Color(235, 244, 255)
                        ),
                        name: () => "Garage confirm"
                    )
                )
            ]
        );

    private static View SelectedCarHeading(float contentWidth = DetailWidth - 36)
        => Sx.View(
            name: () => "Garage selected car",
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 2),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: FittedCarNameSize(contentWidth),
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        verticalAlignment: TextVerticalAlignment.Center,
                        foregroundColor: MenuBlue
                    ),
                    layout: () => Sx.Styles(width: 100.Percent(), height: 30, flexShrink: 0, overflow: Overflow.Hidden),
                    text: () => UiState.Garage.CurrentCar.Value?.Name ?? ""
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: MenuDim),
                    layout: () => Sx.Styles(height: 18, flexShrink: 0, overflow: Overflow.Hidden),
                    text: () => Subtitle(UiState.Garage.CurrentCar.Value)
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 14, foregroundColor: TextBlue),
                    layout: () => Sx.Styles(height: 22, flexShrink: 0, marginTop: 4),
                    text: () => string.IsNullOrWhiteSpace(UiState.Garage.CurrentCar.Value?.Class)
                        ? "" : "Class " + UiState.Garage.CurrentCar.Value!.Class
                )
            ]
        );

    private static float FittedCarNameSize(float contentWidth)
    {
        var name = UiState.Garage.CurrentCar.Value?.Name ?? "";
        var width = TheGraphics.G.GetFontMetrics(new Font(FontFamily.Adventure, FontStyle.Bold, 22))
            .MeasureText(name).X;
        return width > contentWidth - 2 ? Math.Max(1, 22 * (contentWidth - 2) / width) : 22;
    }

    /// <summary>A row of paint chips, the one-glance answer to "what can I change".</summary>
    private static Node PaintSwatches()
        => Sx.Show(
            when: () => UiState.Garage.CurrentCar.Value is not null
                        && UiState.Garage.ColorSlots.Value.Count > 0,
            then: () => Sx.View(
                name: () => "Garage paint swatches",
                style: () => Sx.Styles(
                    flexDirection: FlexDirection.Row,
                    alignItems: Align.Center,
                    gapColumn: 6,
                    marginTop: 10,
                    flexShrink: 0
                ),
                children:
                [
                    Sx.Text(
                        style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.TextFaint),
                        layout: () => Sx.Styles(marginRight: 2),
                        text: () => "PAINT"
                    ),
                    Sx.For(
                        each: () => UiState.Garage.ColorSlots.Value,
                        row: (slot, _) => Sx.View(
                            name: () => "Garage paint chip " + slot.Slot,
                            style: () => Sx.Styles(
                                width: 18,
                                height: 10,
                                borderRadius: 2,
                                borderTop: 1,
                                borderBottom: 1,
                                borderLeft: 1,
                                borderRight: 1,
                                borderColor: new Color(255, 255, 255, 60),
                                backgroundColor: new Color(
                                    (byte)slot.R.Value,
                                    (byte)slot.G.Value,
                                    (byte)slot.B.Value
                                )
                            )
                        )
                    )
                ]
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0, flexShrink: 0))
        );

    private static View StatsPanel()
        => Sx.View(
            name: () => "Garage stats",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                marginTop: 10,
                marginBottom: 10,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                paddingRight: 12
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(width: 100.Percent(), flexShrink: 0, flexDirection: FlexDirection.Column),
                    children:
                    [
                        Sx.Show(
                            when: () => UiState.Garage.CurrentCar.Value is not null,
                            then: () => StatsCard(),
                            otherwise: () => Sx.Text(
                                style: () => Sx.TextStyles(fontSize: 13, foregroundColor: Theme.TextFaint),
                                layout: () => Sx.Styles(marginTop: 12),
                                text: () => "Select a car to view its stats."
                            )
                        )
                    ]
                )
            ]
        );

    private static View StatsCard()
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexDirection: FlexDirection.Column,
                gapRow: 6
            ),
            children:
            [
                Stat("Top Speed", c => c.TopSpeed, new Color(255, 110, 64)),
                Stat("Acceleration", c => c.Acceleration, new Color(255, 215, 64)),
                Stat("Handling", c => c.Handling, new Color(105, 240, 174)),
                Stat("Power Save", c => c.PowerSave, new Color(64, 196, 255)),
                Stat("Strength", c => c.Strength, new Color(255, 64, 129)),
                Stat("Max Health", c => c.MaxHealth, new Color(224, 64, 251)),
                Stat("Stunting", c => c.Stunting, new Color(255, 110, 64)),
                Stat("Hypergliding", c => c.Hypergliding, new Color(124, 77, 255)),
                Stat("AB'ing", c => c.Abing, new Color(68, 138, 255))
            ]
        );

    private static View Stat(string label, Func<CarStatsViewModel, double> read, Color color)
        => GlassCardUi.StatBar(label, () => UiState.Garage.CurrentCar.Value is { } car ? read(car) : 0, color, spacing: 0);

    // ── Shell helpers ─────────────────────────────────────────────────

    private static Node PageHeader(string title, string subtitle, Action? back = null)
        => Sx.View(
            name: () => "Garage header " + title,
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 2),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        flexDirection: FlexDirection.Row,
                        alignItems: Align.Center,
                        gapColumn: 8,
                        flexShrink: 0
                    ),
                    children:
                    [
                        back is null
                            ? null
                            : BackLink(back),
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: back is null ? 34 : 26,
                                fontFamily: FontFamily.Adventure,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: MenuBlue
                            ),
                            layout: () => Sx.Styles(flexShrink: 0),
                            text: () => title
                        )
                    ]
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => subtitle
                )
            ]
        );

    private static Node BackLink(Action back)
    {
        var link = Styled.StyleView(
            baseStyle: Sx.Styles(
                paddingLeft: 8,
                paddingRight: 8,
                paddingTop: 2,
                paddingBottom: 2,
                borderRadius: 4,
                backgroundColor: Theme.MenuFill
            ),
            hoverStyle: Sx.Styles(backgroundColor: Theme.MenuHighlight)
        );
        return link(
            null,
            "Garage back link",
            _ => back(),
            Sx.Text(
                style: () => Sx.TextStyles(
                    fontSize: 16,
                    fontFamily: FontFamily.Adventure,
                    fontStyle: FontStyle.Bold,
                    foregroundColor: TextBlue
                ),
                text: () => "‹"
            )
        );
    }

    private static Node DescriptionBar(Func<string?> text)
        => Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                flexShrink: 0,
                marginTop: 14,
                paddingLeft: 12,
                paddingRight: 12,
                paddingTop: 7,
                paddingBottom: 7,
                backgroundColor: new Color(3, 8, 20, 135),
                borderRadius: 10
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.WrappingText(fontSize: 11, foregroundColor: new Color(185, 211, 255)),
                    layout: () => Sx.Styles(width: 100.Percent()),
                    text: text
                )
            ]
        );

    private static Node Hint(string text)
        => Sx.Text(
            style: () => Sx.TextStyles(fontSize: 10, foregroundColor: Theme.TextFaint),
            layout: () => Sx.Styles(marginTop: 8, flexShrink: 0),
            text: () => text
        );

    private static string Subtitle(CarStatsViewModel? car)
        => car is null
            ? ""
            : car.Author is { Length: > 0 } author
                ? $"Published by {author}"
                : car.Collection;
}
