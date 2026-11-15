using System.Runtime.CompilerServices;
using System.Text.Json;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;
using TightPack;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary.Util;

[TightPackable, PluginVisible]
public sealed partial class WheelAppearance
{
    [TightPackOrder(0), PluginVisible] public bool OriginalWheels { get; set; }
    [TightPackOrder(1), PluginVisible] public Color3? RimColor { get; set; }
    [TightPackOrder(2), PluginVisible] public Color3?[]? Colors { get; set; }
}

public static class CarWheels
{
    public const string SavePath = "data/cfg/car-wheels.json";

    private sealed record Baseline(Rad3dWheelDef[] Wheels, Rad3dRimsDef? Rims);

    private static readonly ConditionalWeakTable<Rad3d, Baseline> Baselines = new();
    private static readonly Dictionary<string, WheelAppearance> Saved = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object Gate = new();
    private static bool _loaded;
    private static bool _dirty;
    private static long _lastSave;

    public static WheelAppearance Normalize(WheelAppearance? appearance) => new()
    {
        OriginalWheels = appearance?.OriginalWheels ?? false,
        RimColor = appearance?.RimColor is { } color ? CarPaint.ClampColor(color) : null,
        Colors = CarPaint.NormalizeColors(appearance?.Colors)
    };

    public static WheelAppearance Get(string id)
    {
        lock (Gate)
        {
            if (!_loaded) Load();
            return Normalize(Saved.GetValueOrDefault(RadpackReader.Identity(id)));
        }
    }

    public static void Set(string id, WheelAppearance appearance, VirtualFS? fileSystem = null)
    {
        lock (Gate)
        {
            if (!_loaded) Load();
            Saved[RadpackReader.Identity(id)] = Normalize(appearance);
            _dirty = true;
            if (Environment.TickCount64 - _lastSave >= 250) Flush(fileSystem);
        }
    }

    public static void Load(VirtualFS? fileSystem = null)
    {
        lock (Gate)
        {
            _loaded = true;
            _dirty = false;
            Saved.Clear();
            try
            {
                var fs = fileSystem ?? TheVFS.VFS;
                if (!fs.FileExists(SavePath)) return;
                var data = JsonSerializer.Deserialize<Dictionary<string, WheelAppearance>>(fs.ReadAllText(SavePath));
                if (data is not null)
                    foreach (var (id, appearance) in data) Saved[id] = Normalize(appearance);
            }
            catch (Exception ex)
            {
                Logging.Warning($"Could not load wheel cosmetics: {ex.Message}");
            }
        }
    }

    public static void Flush(VirtualFS? fileSystem = null)
    {
        lock (Gate)
        {
            if (!_dirty) return;
            try
            {
                using var stream = (fileSystem ?? TheVFS.VFS).OpenWrite(SavePath);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonSerializer.Serialize(Saved));
                _dirty = false;
                _lastSave = Environment.TickCount64;
            }
            catch (Exception ex)
            {
                Logging.Warning($"Could not save wheel cosmetics: {ex.Message}");
            }
        }
    }

    private static Baseline Stock(Rad3d rad)
        => Baselines.GetValue(rad, model => new(model.Wheels.ToArray(), model.Rims));

    public static bool HasCustomMeshes(Rad3d rad) => Stock(rad).Wheels.Any(wheel => wheel.Polys is not null);

    internal static void CopyTo(Rad3d source, Rad3d instance, WheelAppearance? appearance)
    {
        Baselines.Add(instance, Stock(source));
        Apply(instance, appearance);
    }

    public static void Apply(Rad3d rad, WheelAppearance? appearance)
    {
        var stock = Stock(rad);
        var style = Normalize(appearance);
        rad.Rims = stock.Rims;
        if (style.RimColor is { } rim)
            rad.Rims = (stock.Rims ?? new Rad3dRimsDef(new Color3(120, 120, 120), 20, 30)) with { Color = rim };
        rad.Wheels = stock.Wheels.Select(wheel => wheel with
        {
            Polys = style.OriginalWheels ? null : wheel.Polys?.Select(poly =>
                poly.ColNum is { } slot && slot >= 0 && slot < (style.Colors?.Length ?? 0)
                && style.Colors![slot] is { } color ? poly with { Color = color } : poly
            ).ToArray()
        }).ToArray();
    }

    public static IReadOnlyList<CarColorSlot> Slots(Rad3d rad)
        => rad.Wheels.SelectMany(wheel => wheel.Polys ?? [])
            .Where(poly => poly.ColNum is >= 0 and < CarPaint.SlotCount)
            .GroupBy(poly => poly.ColNum!.Value).OrderBy(group => group.Key)
            .Select(group => new CarColorSlot(group.Key, group.First().Color)).ToArray();
}
