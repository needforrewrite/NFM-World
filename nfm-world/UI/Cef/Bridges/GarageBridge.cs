using NFMWorldLibrary;
using NFMWorldLibrary.Util;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Bridge for GaragePhase — car stat display, car selection, collection switching, and search.
/// </summary>
public sealed class GarageBridge() : PhaseBridge("garage")
{
    public override bool EnableInput => true;

    protected override void OnMessage(string type, LuaValue args)
    {
        switch (type)
        {
            case "selectCar":
                if (args.TryRead<LuaTable>(out var a)
                    && a.TryGetValue("collection", out var col)
                    && a.TryGetValue("fileName", out var car))
                {
                    CarSelected?.Invoke(col.ReadOrDefault<string>() ?? "", car.ReadOrDefault<string>() ?? "");
                }
                break;
            case "selectCollection":
                if (args.TryRead<LuaTable>(out var b) && b.TryGetValue("collection", out var selCol))
                {
                    CollectionSelected?.Invoke(selCol.ReadOrDefault<string>() ?? "");
                }
                break;
            case "cycleCar":
                if (args.TryRead<LuaTable>(out var c) && c.TryGetValue("direction", out var dir))
                {
                    var direction = dir.ReadOrDefault<string>() ?? "";
                    CycleCarRequested?.Invoke(direction == "right" ? 1 : -1);
                }
                break;
            case "confirm":
                ConfirmSelection?.Invoke();
                break;
            case "cancel":
                CancelSelection?.Invoke();
                break;
            case "back":
                BackRequested?.Invoke();
                break;
            case "requestGarageData":
                GarageDataRequested?.Invoke();
                break;
        }
    }

    /// <summary>
    /// Push the currently selected car's stats to JS.
    /// </summary>
    public void PushCurrentCar(CarStatsData car)
    {
        Push("currentCar", car);
    }

    /// <summary>
    /// Push available car collections to JS.
    /// </summary>
    public void PushCollections(CarCollectionData[] collections)
    {
        Push("collections", new CarCollectionsData { Collections = collections });
    }

    /// <summary>
    /// Push the currently active collection to JS.
    /// </summary>
    public void PushCurrentCollection(Collection collection)
    {
        Push("currentCollection", new CurrentCollectionData { Id = collection.ToString() });
    }

    public event Action<string, string>? CarSelected;
    public event Action<string>? CollectionSelected;
    public event Action<int>? CycleCarRequested;
    public event Action? ConfirmSelection;
    public event Action? CancelSelection;
    public event Action? BackRequested;
    public event Action? GarageDataRequested;
}

/// <summary>
/// Car stats sent to the garage JS page.
/// </summary>
[PluginVisible]
public sealed partial class CarStatsData
{
    [PluginVisible] public string FileName { get; set; } = "";
    [PluginVisible] public string Name { get; set; } = "";
    [PluginVisible] public string Collection { get; set; } = nameof(NFMWorldLibrary.Collection.User);
    [PluginVisible] public double TopSpeed { get; set; }
    [PluginVisible] public double Acceleration { get; set; }
    [PluginVisible] public double Handling { get; set; }
    [PluginVisible] public double PowerSave { get; set; }
    [PluginVisible] public double Strength { get; set; }
    [PluginVisible] public double MaxHealth { get; set; }
    [PluginVisible] public double Stunting { get; set; }
    [PluginVisible] public double Hypergliding { get; set; }
    [PluginVisible] public double Abing { get; set; }
}

/// <summary>
/// Collection of cars sent to the garage JS page.
/// </summary>
[PluginVisible]
public sealed partial class CarCollectionsData
{
    [PluginVisible] public LuaArray<CarCollectionData> Collections { get; set; } = [];
}

/// <summary>
/// Collection of cars sent to the garage JS page.
/// </summary>
[PluginVisible]
public sealed partial class CarCollectionData
{
    [PluginVisible] public string Id { get; set; } = nameof(Collection.User);
    [PluginVisible] public LuaArray<CarStatsData> Cars { get; set; } = [];
}

[PluginVisible]
public sealed partial class CurrentCollectionData
{
    [PluginVisible] public required string Id { get; set; }
}
