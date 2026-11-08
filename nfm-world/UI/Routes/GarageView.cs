using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI.Components;
using NFMWorld.UI.ViewModels;
using SignalsDotnet;

namespace NFMWorld.UI.Routes;

/// <summary>
/// Garage: collections, car list and the selected car's stats.
/// Port of <c>data/uis/routes/garage.luau</c>.
/// </summary>
public static class GarageView
{
    internal const int SidebarWidth = 340;

    /// <summary>What the garage asks the game to do. The phase fills these in.</summary>
    public sealed record Actions(
        Action<string, string> CarSelected,
        Action<string> CollectionSelected,
        Action Back
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
    // which the renderer's reference check turns into zero property writes. Only layout
    // is set here - the card's padding comes from GlassCard's own style, and `Over`
    // leaves it alone because these never mention it.
    private static readonly Styles CardSelected = Sx.Styles(
        marginBottom: 16,
        opacity: 1,
        flexDirection: FlexDirection.Column,
        flexShrink: 0
    );

    private static readonly Styles CardUnselected = Sx.Styles(
        marginBottom: 16,
        opacity: 0.7f,
        flexDirection: FlexDirection.Column,
        flexShrink: 0
    );

    public static Node Build(Actions actions)
    {
        return Sx.View(
            style: () => Sx.Styles(
                width: 100.Percent(),
                height: 100.Percent(),
                flexDirection: FlexDirection.Row
            ),
            children:
            [
                Sx.View(
                    style: () => Sx.Styles(
                        width: SidebarWidth,
                        height: 100.Percent(),
                        paddingTop: 32,
                        paddingBottom: 32,
                        paddingLeft: 24,
                        paddingRight: 24,
                        flexDirection: FlexDirection.Column,
                        gapColumn: 16,
                        gapRow: 16,
                        backgroundColor: new Color(0, 0, 0, 77),
                        borderRight: 1,
                        borderColor: new Color(255, 255, 255, 15)
                    ),
                    children:
                    [
                        Sx.Text(style: () => Sx.TextStyles(fontSize: 28, fontStyle: FontStyle.Bold), text: () => "Garage"),
                        StatsPanel(),
                        SearchBox(actions),
                        CollectionList(actions),

                        Button.Create(
                            label: () => "← Back to Menu",
                            onPress: actions.Back
                        )
                    ]
                )
            ]
        );
    }

    private static Node SearchBox(Actions actions) => Sx.TextInput(
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
            borderColor: new Color(255, 255, 255, 31),
            borderRadius: 8,
            flexShrink: 0
        ),
        text: () => SearchQuery.Value,
        onchange: value => SearchQuery.Value = value,
        placeholder: () => "Search cars...",
        // Enter picks the first match, as the Luau version did.
        onsubmit: _ =>
        {
            if (FilterCollections().FirstOrDefault() is { Cars.Count: > 0 } first)
            {
                actions.CarSelected(first.Id, first.Cars[0].FileName);
                SearchQuery.Value = "";
            }
        }
    );

    private static View StatsPanel() => Sx.View(
        style: () => Sx.Styles(flexShrink: 0, alignItems: Align.Center, justifyContent: Justify.Center),
        children:
        [
            Sx.Show(
                when: () => UiState.Garage.CurrentCar.Value is not null,
                then: () => StatsCard(),
                otherwise: () => Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 18, foregroundColor: Theme.TextFaint),
                    text: () => "Select a car to view stats"
                )
            )
        ]
    );

    private static View StatsCard()
    {
        return Sx.View(
            style: () => Sx.Styles(
                width: 280,
                paddingTop: 24,
                paddingBottom: 24,
                paddingLeft: 24,
                paddingRight: 24,
                flexDirection: FlexDirection.Column
            ),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 18, fontStyle: FontStyle.Bold, foregroundColor: Theme.Accent),
                    layout: () => Sx.Styles(marginBottom: 16),
                    text: () => UiState.Garage.CurrentCar.Value?.Name ?? ""
                ),
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
            style: () => Sx.Styles(
                flexGrow: 1,
                flexDirection: FlexDirection.Column,
                overflow: Overflow.Scroll,
                gapColumn: 8,
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
                            CollectionSection(collection.Id, actions),
                            Sx.For(
                                each: () => collection.Cars,
                                row: (car, _) => CarCard(car, collection.Id, actions)
                            )
                        ]
                    )
                ),
                Sx.Show(
                    when: () => SearchQuery.Value.Trim().Length > 0 && filtered.Value.Count == 0,
                    then: () => Sx.Text(
                        style: () => Sx.TextStyles(
                            fontSize: 14,
                            foregroundColor: Theme.TextFaint,
                            horizontalAlignment: TextHorizontalAlignment.Center
                        ),
                        layout: () => Sx.Styles(paddingTop: 24, paddingBottom: 24, paddingLeft: 24, paddingRight: 24),
                        text: () => $"No cars match \"{SearchQuery.Value}\""
                    )
                )
            ]
        );
    }

    private static View CollectionSection(string id, Actions actions)
    {
        var isActive = () => UiState.Garage.CurrentCollection.Value == id;

        return Sx.View(
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
                flexGrow: 1,
                flexShrink: 0
            ),
            onmousedown: _ => actions.CollectionSelected(id),
            children: Sx.Text(
                style: () => Sx.TextStyles(fontSize: 12, foregroundColor: isActive() ? Theme.Accent : Theme.TextFaint),
                text: () => id
            )
        );
    }

    private static View CarCard(CarStatsViewModel car, string collectionId, Actions actions)
    {
        var isSelected = () => UiState.Garage.CurrentCar.Value?.FileName == car.FileName;

        // One node, not a wrapper around the card: the card already carries the click,
        // the spacing and the padding, and a wrapper only doubled the margin that the
        // unset-padding bug made visible in the first place.
        return GlassCardUi.Card(
            color: () => isSelected() ? Theme.Accent : new Color(255, 255, 255, 38),
            style: () => isSelected() ? CardSelected : CardUnselected,
            onmousedown: _ => actions.CarSelected(collectionId, car.FileName),
            children:
            [
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 14, fontStyle: FontStyle.Bold),
                    layout: () => Sx.Styles(marginBottom: 4),
                    text: () => car.Name
                ),
                Sx.Text(
                    style: () => Sx.TextStyles(fontSize: 11, foregroundColor: Theme.TextFaint),
                    text: () => collectionId
                )
            ]
        );
    }

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
