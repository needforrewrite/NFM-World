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
        Action Back);

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
    private static readonly Styles CardSelected = new() { MarginBottom = 16, Opacity = 1, FlexDirection = FlexDirection.Column, FlexShrink = 0 };
    private static readonly Styles CardUnselected = new() { MarginBottom = 16, Opacity = 0.7f, FlexDirection = FlexDirection.Column, FlexShrink = 0 };

    public static Node Build(Actions actions)
    {
        return Sx.View(
            style: () => new Styles
            {
                Width = MeasurementWidthHeight.Percent(100),
                Height = MeasurementWidthHeight.Percent(100),
                FlexDirection = FlexDirection.Row,
            },
            children:
            [
                Sx.View(
                    style: () => new Styles
                    {
                        Width = SidebarWidth,
                        Height = MeasurementWidthHeight.Percent(100),
                        PaddingTop = 32,
                        PaddingBottom = 32,
                        PaddingLeft = 24,
                        PaddingRight = 24,
                        FlexDirection = FlexDirection.Column,
                        GapColumn = 16,
                        GapRow = 16,
                        BackgroundColor = new Color(0, 0, 0, 77),
                        BorderRight = 1,
                        BorderColor = new Color(255, 255, 255, 15),
                    },
                    children:
                    [
                        Sx.Text(style: () => new TextStyles { FontSize = 28, FontStyle = FontStyle.Bold }, text: () => "Garage"),
                        StatsPanel(),
                        SearchBox(actions),
                        CollectionList(actions),

                        Button.Create(
                            label: () => "← Back to Menu",
                            onPress: actions.Back)
                    ])
            ]);
    }

    private static Node SearchBox(Actions actions) => Sx.TextInput(
        style: () => new Styles
        {
            Width = MeasurementWidthHeight.Percent(100),
            PaddingTop = 10,
            PaddingBottom = 10,
            PaddingLeft = 14,
            PaddingRight = 14,
            BackgroundColor = new Color(255, 255, 255, 20),
            BorderTop = 1,
            BorderBottom = 1,
            BorderLeft = 1,
            BorderRight = 1,
            BorderColor = new Color(255, 255, 255, 31),
            BorderRadius = 8,
            FlexShrink = 0,
        },
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
        });

    private static View StatsPanel() => Sx.View(
        style: () => new Styles { FlexShrink = 0, AlignItems = Align.Center, JustifyContent = Justify.Center },
        children:
        [
            Sx.Show(
                when: () => UiState.Garage.CurrentCar.Value is not null,
                then: () => StatsCard(),
                otherwise: () => Sx.Text(
                    style: () => new TextStyles { FontSize = 18, ForegroundColor = Theme.TextFaint },
                    text: () => "Select a car to view stats"))
        ]);

    private static View StatsCard()
    {
        return Sx.View(
            style: () => new Styles
            {
                Width = 280,
                PaddingTop = 24,
                PaddingBottom = 24,
                PaddingLeft = 24,
                PaddingRight = 24,
                FlexDirection = FlexDirection.Column,
            },
            children:
            [
                Sx.Text(
                    style: () => new TextStyles { FontSize = 18, FontStyle = FontStyle.Bold, ForegroundColor = Theme.Accent },
                    layout: () => new Styles { MarginBottom = 16 },
                    text: () => UiState.Garage.CurrentCar.Value?.Name ?? ""),
                Stat("Top Speed", c => c.TopSpeed, new Color(255, 110, 64)),
                Stat("Acceleration", c => c.Acceleration, new Color(255, 215, 64)),
                Stat("Handling", c => c.Handling, new Color(105, 240, 174)),
                Stat("Power Save", c => c.PowerSave, new Color(64, 196, 255)),
                Stat("Strength", c => c.Strength, new Color(255, 64, 129)),
                Stat("Max Health", c => c.MaxHealth, new Color(224, 64, 251)),
                Stat("Stunting", c => c.Stunting, new Color(255, 110, 64)),
                Stat("Hypergliding", c => c.Hypergliding, new Color(124, 77, 255)),
                Stat("AB'ing", c => c.Abing, new Color(68, 138, 255))
            ]);
    }

    private static View Stat(string label, Func<CarStatsViewModel, double> read, Color color)
        => GlassCardUi.StatBar(label, () => UiState.Garage.CurrentCar.Value is { } car ? read(car) : 0, color);

    private static View CollectionList(Actions actions)
    {
        var filtered = Sx.Computed(FilterCollections);

        return Sx.View(
            style: () => new Styles
            {
                FlexGrow = 1,
                FlexDirection = FlexDirection.Column,
                Overflow = Overflow.Scroll,
                GapColumn = 8,
                GapRow = 8,
            },
            children:
            [
                Sx.For(
                    each: () => filtered.Value,
                    row: (collection, _) => Sx.View(
                        style: () => new Styles { FlexDirection = FlexDirection.Column, FlexShrink = 0 },
                        children:
                        [
                            CollectionSection(collection.Id, actions),
                            Sx.For(
                                each: () => collection.Cars,
                                row: (car, _) => CarCard(car, collection.Id, actions))
                        ])),
                Sx.Show(
                    when: () => SearchQuery.Value.Trim().Length > 0 && filtered.Value.Count == 0,
                    then: () => Sx.Text(
                        style: () => new TextStyles
                        {
                            FontSize = 14,
                            ForegroundColor = Theme.TextFaint,
                            HorizontalAlignment = TextHorizontalAlignment.Center,
                        },
                        layout: () => new Styles { PaddingTop = 24, PaddingBottom = 24, PaddingLeft = 24, PaddingRight = 24 },
                        text: () => $"No cars match \"{SearchQuery.Value}\""))
            ]);
    }

    private static View CollectionSection(string id, Actions actions)
    {
        var isActive = () => UiState.Garage.CurrentCollection.Value == id;

        return Sx.View(
            style: () => new Styles
            {
                MarginBottom = 6,
                PaddingTop = 4,
                PaddingBottom = 4,
                PaddingLeft = 8,
                PaddingRight = 8,
                BorderRadius = 4,
                BorderLeft = 2,
                BorderColor = isActive() ? Theme.Accent : Theme.Transparent,
                BackgroundColor = isActive() ? Theme.AccentSoft : Theme.Transparent,
                FlexGrow = 1,
                FlexShrink = 0,
            },
            onmousedown: _ => actions.CollectionSelected(id),
            children: Sx.Text(
                style: () => new TextStyles { FontSize = 12, ForegroundColor = isActive() ? Theme.Accent : Theme.TextFaint },
                text: () => id));
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
                    style: () => new TextStyles { FontSize = 14, FontStyle = FontStyle.Bold },
                    layout: () => new Styles { MarginBottom = 4 },
                    text: () => car.Name),
                Sx.Text(
                    style: () => new TextStyles { FontSize = 11, ForegroundColor = Theme.TextFaint },
                    text: () => collectionId)
            ]);
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
