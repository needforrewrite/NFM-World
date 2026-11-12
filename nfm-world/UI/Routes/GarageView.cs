using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// Garage: browse collections on the left, inspect the selected car on the right, and keep
/// the middle of the screen clear for the 3D preview.
/// </summary>
public static class GarageView
{
    internal const int SidebarWidth = 380;
    internal const int DetailWidth = 380;

    /// <summary>What the garage asks the game to do. The phase fills these in.</summary>
    public sealed record Actions(
        Action<string, string> CarSelected,
        Action<string> CollectionSelected,
        Action Confirm,
        Action Back,
        Action<bool> SourceSelected,
        Action<string> OnlineSearch
    );

    private static readonly Signal<string> SearchQuery = new("");

    /// <summary>Empties the search box. Returns false when it was already empty.</summary>
    public static bool ClearSearch()
    {
        if (SearchQuery.Value.Length == 0)
            return false;

        SearchQuery.Value = "";
        return true;
    }

    // Stable references: a card whose selection did not change re-uses the same table,
    // which the renderer's reference check turns into zero property writes.
    private static readonly Styles CardSelected = Sx.Styles(
        marginBottom: 12,
        opacity: 1,
        flexDirection: FlexDirection.Column,
        flexShrink: 0
    );

    private static readonly Styles CardUnselected = Sx.Styles(
        marginBottom: 12,
        opacity: 0.7f,
        flexDirection: FlexDirection.Column,
        flexShrink: 0
    );

    public static Node Build(Actions actions)
    {
        return Sx.View(
            name: () => "Garage",
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Row
            ),
            children:
            [
                BrowsePanel(actions),
                PreviewGap(),
                DetailPanel(actions)
            ]
        );
    }

    private static View BrowsePanel(Actions actions)
        => Sx.View(
            name: () => "Garage browse",
            style: () => Sx.Styles(
                width: SidebarWidth,
                height: 100.Percent(),
                paddingTop: 28,
                paddingBottom: 28,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 12,
                backgroundColor: new Color(6, 14, 30, 205),
                borderRight: 1,
                borderColor: Theme.MenuBorder
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 30,
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: Theme.MenuBlue
                    ),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "GARAGE"
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0, marginBottom: 4),
                    text: () => "Pick a car, then confirm."
                ),
                SourcePicker(actions),
                SearchBox(actions),
                StatusLine(),
                CollectionList(actions),
                Button.Create(
                    label: () => "← Back to Menu",
                    onPress: actions.Back,
                    variant: ButtonVariant.Ghost,
                    fullWidth: true,
                    name: () => "Garage back"
                )
            ]
        );

    /// <summary>The middle stays empty so the car preview is visible.</summary>
    private static View PreviewGap()
        => Sx.View(
            name: () => "Garage preview gap",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                flexDirection: FlexDirection.Column,
                justifyContent: Justify.FlexEnd,
                alignItems: Align.Center,
                pointerEvents: false
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(marginBottom: 20),
                    text: () => "Drag to orbit  ·  ← → change car  ·  Enter select"
                )
            ]
        );

    private static View DetailPanel(Actions actions)
        => Sx.View(
            name: () => "Garage detail",
            style: () => Sx.Styles(
                width: DetailWidth,
                height: 100.Percent(),
                paddingTop: 28,
                paddingBottom: 28,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column,
                flexShrink: 0,
                gapRow: 12,
                backgroundColor: new Color(6, 14, 30, 205),
                borderLeft: 1,
                borderColor: Theme.MenuBorder
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "SELECTED CAR"
                ),
                Sx.Show(
                    when: () => UiState.Garage.CurrentCar.Value is not null,
                    then: () => SelectedCarHeading(),
                    otherwise: () => Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 22,
                            fontFamily: FontFamily.Adventure,
                            foregroundColor: Theme.MenuBlue
                        ),
                        layout: () => Sx.Styles(flexShrink: 0),
                        text: () => "Nothing selected"
                    )
                ),
                StatsPanel(),
                Button.Create(
                    label: () => "SELECT CAR",
                    onPress: actions.Confirm,
                    variant: ButtonVariant.Primary,
                    fullWidth: true,
                    name: () => "Garage confirm"
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => "Esc clears the search, then goes back."
                )
            ]
        );

    private static View SelectedCarHeading()
        => Sx.View(
            name: () => "Garage selected car",
            style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0, gapRow: 2),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(
                        fontSize: 26,
                        fontFamily: FontFamily.Adventure,
                        fontStyle: FontStyle.Bold,
                        foregroundColor: Theme.MenuBlue
                    ),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => UiState.Garage.CurrentCar.Value?.Name ?? ""
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 12, foregroundColor: Theme.MenuDim),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => Subtitle(UiState.Garage.CurrentCar.Value)
                )
            ]
        );

    private static Node SearchBox(Actions actions)
    {
        var input = Sx.TextInput(
            style: () => Sx.Styles(
                width: 100.Percent(),
                paddingTop: 10,
                paddingBottom: 10,
                paddingLeft: 14,
                paddingRight: 14,
                backgroundColor: new Color(255, 255, 255, 20),
                borderTop: 1,
                borderBottom: 1,
                borderLeft: 1,
                borderRight: 1,
                borderColor: Theme.MenuBorder,
                borderRadius: 8,
                flexShrink: 0
            ),
            text: () => SearchQuery.Value,
            onchange: value => SearchQuery.Value = value,
            placeholder: () => UiState.Garage.OnlineSource.Value ? "Search the archive, Enter to load..." : "Search cars...",
            // Enter picks the first match, as the Luau version did.
            onsubmit: _ =>
            {
                if (UiState.Garage.OnlineSource.Value)
                {
                    actions.OnlineSearch(SearchQuery.Value);
                    return;
                }

                if (FilterCollections().FirstOrDefault() is { Cars.Count: > 0 } first)
                {
                    actions.CarSelected(first.Id, first.Cars[0].FileName);
                    SearchQuery.Value = "";
                }
            }
        );
        input.Name = "Garage search";
        return input;
    }

    private static Node SourcePicker(Actions actions)
        => Sx.View(
            name: () => "Garage source",
            style: () => Sx.Styles(flexDirection: FlexDirection.Row, flexShrink: 0, gapColumn: 8, gapRow: 8),
            children:
            [
                Button.Create(
                    label: () => "LOCAL",
                    onPress: () => actions.SourceSelected(false),
                    active: () => !UiState.Garage.OnlineSource.Value,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(flexGrow: 1),
                    name: () => "Garage source local"
                ),
                Button.Create(
                    label: () => "ONLINE",
                    onPress: () => actions.SourceSelected(true),
                    active: () => UiState.Garage.OnlineSource.Value,
                    size: ButtonSize.Sm,
                    style: () => Sx.Styles(flexGrow: 1),
                    name: () => "Garage source online"
                )
            ]
        );

    private static Node StatusLine()
        => Sx.Show(
            when: () => UiState.Garage.OnlineStatus.Value.Length > 0,
            then: () => Sx.Text(
                style: () => Sx.WrappingText(fontSize: 11, foregroundColor: Theme.MenuDim),
                layout: () => Sx.Styles(width: 100.Percent(), flexShrink: 0),
                text: () => UiState.Garage.OnlineStatus.Value
            ),
            otherwise: () => Sx.View(style: () => Sx.Styles(height: 0, flexShrink: 0))
        );

    private static View StatsPanel() => Sx.View(
        name: () => "Garage stats",
        style: () => Sx.Styles(
            flexGrow: 1,
            flexShrink: 1,
            minHeight: 0,
            flexDirection: FlexDirection.Column,
            overflow: Overflow.Scroll
        ),
        children:
        [
            Sx.Show(
                when: () => UiState.Garage.CurrentCar.Value is not null,
                then: () => StatsCard(),
                otherwise: () => Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 14, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(marginTop: 16),
                    text: () => "Select a car to view its stats."
                )
            )
        ]
    );

    private static View StatsCard()
    {
        return Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                paddingTop: 16,
                paddingBottom: 8,
                flexDirection: FlexDirection.Column
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
    }

    private static View Stat(string label, Func<CarStatsViewModel, double> read, Color color)
        => GlassCardUi.StatBar(label, () => UiState.Garage.CurrentCar.Value is { } car ? read(car) : 0, color);

    private static View CollectionList(Actions actions)
    {
        var filtered = Sx.Computed(FilterCollections);

        return Sx.View(
            name: () => "Garage car list",
            style: () => Sx.Styles(
                flexGrow: 1,
                flexShrink: 1,
                minHeight: 0,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll
            ),
            children:
            [
                // A fixed-width column: the scroll container then only ever scrolls
                // vertically, however wide a car name happens to be.
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
                        Sx.For(
                            each: () => filtered.Value,
                            row: (collection, _) => Sx.View(
                                style: () => Sx.Styles(flexDirection: FlexDirection.Column, flexShrink: 0),
                                children:
                                [
                                    CollectionSection(collection.Id, collection.Cars.Count, actions),
                                    Sx.For(
                                        each: () => collection.Cars,
                                        row: (car, _) => CarCard(car, collection.Id, actions)
                                    )
                                ]
                            )
                        ),
                        Sx.Show(
                            when: () => filtered.Value.Count == 0,
                            then: () => Sx.Text(
                                style: () => Sx.WrappingText(
                                    fontSize: 14,
                                    foregroundColor: Theme.TextFaint
                                ),
                                layout: () => Sx.Styles(
                                    width: 100.Percent(),
                                    paddingTop: 24,
                                    paddingBottom: 24,
                                    paddingLeft: 24,
                                    paddingRight: 24
                                ),
                                text: () => UiState.Garage.OnlineSource.Value
                                    ? SearchQuery.Value.Trim().Length > 0
                                        ? $"No public cars match \"{SearchQuery.Value}\""
                                        : "Search the archive to list public cars."
                                    : SearchQuery.Value.Trim().Length > 0
                                        ? $"No cars match \"{SearchQuery.Value}\""
                                        : "No cars in this build."
                            )
                        )
                    ]
                )
            ]
        );
    }

    private static View CollectionSection(string id, int count, Actions actions)
    {
        var isActive = () => UiState.Garage.CurrentCollection.Value == id;

        return Sx.View(
            name: () => "Garage collection " + id,
            style: () => Sx.Styles(
                marginBottom: 6,
                paddingTop: 4,
                paddingBottom: 4,
                paddingLeft: 8,
                paddingRight: 8,
                borderRadius: 4,
                borderLeft: 2,
                borderColor: isActive() ? Theme.Accent : Theme.Transparent,
                backgroundColor: isActive() ? Theme.AccentSoft : Theme.Transparent,
                flexShrink: 0
            ),
            onmousedown: _ => actions.CollectionSelected(id),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(justifyContent: Justify.SpaceBetween, gapColumn: 8),
                    children:
                    [
                        Sx.Text(
                            style: () => Sx.TextStyles(
                                fontSize: 12,
                                fontStyle: FontStyle.Bold,
                                foregroundColor: isActive() ? Theme.Accent : Theme.MenuDim
                            ),
                            text: () => id
                        ),
                        Sx.Text(
                            style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                            text: () => count.ToString()
                        )
                    ]
                )
            ]
        );
    }

    private static View CarCard(CarStatsViewModel car, string collectionId, Actions actions)
    {
        var isSelected = () => UiState.Garage.CurrentCar.Value?.FileName == car.FileName;

        // One node, not a wrapper around the card: the card already carries the click,
        // the spacing and the padding.
        var card = GlassCardUi.Card(
            color: () => isSelected() ? Theme.Accent : new Color(255, 255, 255, 38),
            style: () => isSelected() ? CardSelected : CardUnselected,
            onmousedown: _ => actions.CarSelected(collectionId, car.FileName),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => car.Name
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    layout: () => Sx.Styles(flexShrink: 0),
                    text: () => car.Author is { Length: > 0 } author ? author : collectionId
                )
            ]
        );
        card.Name = "Garage car " + car.FileName;
        return card;
    }

    private static string Subtitle(CarStatsViewModel? car)
        => car is null
            ? ""
            : car.Author is { Length: > 0 } author
                ? $"Published by {author}"
                : car.Collection;

    private static IReadOnlyList<CarCollectionViewModel> FilterCollections()
    {
        var query = SearchQuery.Value.Trim();
        var collections = UiState.Garage.Collections.Value;

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
}
