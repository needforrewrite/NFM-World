using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using NFMWorld.PhysicsFS;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Radpack;

namespace NFMWorldLibrary.Util;

/// <summary>One editable body colour of an <see cref="Rad3d"/>.</summary>
public readonly record struct CarColorSlot(int Slot, Color3 Color);

/// <summary>The on-disk shape of the paint overrides.</summary>
internal sealed class CarPaintFile
{
    [JsonPropertyName("version")] public int Version { get; set; } = 1;

    /// <summary>Car id → slot ("0"…"3") → 0xRRGGBB.</summary>
    [JsonPropertyName("cars")]
    public Dictionary<string, Dictionary<string, int>> Cars { get; set; } = new();
}

/// <summary>
/// Per-car body colours picked in the garage.
/// </summary>
/// <remarks>
/// The RAD text is the definition, but a car can be loaded from a radpack with no text in
/// reach, so colours are applied by rewriting the polys tagged with a slot
/// (<see cref="Rad3dPoly.ColNum"/>) on the <see cref="Rad3d"/> instance itself.
/// Cached models stay unpainted. Previews and players own their painted copies; original
/// colours are captured before rewriting a preview so overrides can be reset.
/// </remarks>
public static class CarPaint
{
    public const string SavePath = "data/cfg/car-colors.json";
    public const int SlotCount = 4;
    public const int MinimumChannel = 15;

    public static Color3 ClampColor(Color3 color)
        => new(
            (short)Math.Clamp((int)color.R, MinimumChannel, 255),
            (short)Math.Clamp((int)color.G, MinimumChannel, 255),
            (short)Math.Clamp((int)color.B, MinimumChannel, 255)
        );

    private const int SaveIntervalMs = 250;

    private sealed class PaintState
    {
        public bool Captured;
        public readonly Color3?[] Base = new Color3?[SlotCount];
        public int Version = -1;
    }

    private static readonly Dictionary<string, Dictionary<string, Color3>> Overrides =
        new(StringComparer.OrdinalIgnoreCase);

    private static readonly ConditionalWeakTable<Rad3d, PaintState> States = new();

    // GetCar can be reached from a content-preparation thread, so the loaded overrides and
    // the per-instance paint state are guarded.
    private static readonly object Gate = new();
    private static bool _loaded;
    private static int _version;
    private static bool _dirty;
    private static long _lastSave;

    public static string SlotName(int slot) => slot switch
    {
        0 => "First Color",
        1 => "Second Color",
        2 => "Third Color",
        _ => "Fourth Color"
    };

    /// <summary>Reads the saved overrides. Always starts from an empty set.</summary>
    public static void Load(VirtualFS? fileSystem = null)
    {
        lock (Gate)
            LoadCore(fileSystem);
    }

    private static void LoadCore(VirtualFS? fileSystem)
    {
        Overrides.Clear();
        _loaded = true;
        _version++;
        _dirty = false;

        try
        {
            var fs = fileSystem ?? TheVFS.VFS;

            if (!fs.FileExists(SavePath))

                return;

            var model = JsonSerializer.Deserialize<CarPaintFile>(fs.ReadAllText(SavePath));

            if (model?.Cars is null)

                return;

            foreach (var (carId, slots) in model.Cars)
            {
                if (string.IsNullOrWhiteSpace(carId) || slots is null)

                    continue;

                var bySlot = new Dictionary<string, Color3>();

                foreach (var (slotKey, rgb) in slots)
                {
                    if (int.TryParse(slotKey, out var slot) && slot >= 0 && slot < SlotCount)
                        bySlot[slotKey] = ClampColor(Unpack(rgb));
                }

                if (bySlot.Count > 0)
                    Overrides[RadpackReader.Identity(carId)] = bySlot;
            }
        }
        catch (Exception ex)
        {
            Logging.Warning($"Could not load car colours: {ex.Message}");
        }
    }

    /// <summary>The colours a car declares, in slot order, as they currently render.</summary>
    public static IReadOnlyList<CarColorSlot> Slots(Rad3d rad)
    {
        var result = new List<CarColorSlot>(SlotCount);

        for (var slot = 0; slot < SlotCount; slot++)
        {
            if (LiveColor(rad, slot) is { } color)
                result.Add(new CarColorSlot(slot, color));
        }

        return result;
    }

    /// <summary>Stores one slot's colour and persists it (coalesced while a slider is dragged).</summary>
    public static void Set(string carId, int slot, Color3 color, VirtualFS? fileSystem = null)
    {
        if (slot < 0 || slot >= SlotCount)

            return;

        lock (Gate)
        {
            EnsureLoaded();
            var key = Key(carId);
            if (!Overrides.TryGetValue(key, out var slots))
                Overrides[key] = slots = new Dictionary<string, Color3>();

            slots[slot.ToString()] = ClampColor(color);
            _version++;
            _dirty = true;
            if (Environment.TickCount64 - _lastSave >= SaveIntervalMs)
                Save(fileSystem);
        }
    }

    /// <summary>Drops every override for a car.</summary>
    public static void Reset(string carId, VirtualFS? fileSystem = null)
    {
        lock (Gate)
        {
            EnsureLoaded();

            if (!Overrides.Remove(Key(carId)))

                return;

            _version++;
            _dirty = true;
            Save(fileSystem);
        }
    }

    /// <summary>Writes pending overrides now.</summary>
    public static void Save(VirtualFS? fileSystem = null)
    {
        lock (Gate)
        {
            if (!_dirty)

                return;

            _lastSave = Environment.TickCount64;
            var model = new CarPaintFile();

            foreach (var (carId, slots) in Overrides)
            {
                var values = new Dictionary<string, int>();
                foreach (var (slotKey, color) in slots)
                    values[slotKey] = Pack(color);
                if (values.Count > 0)
                    model.Cars[carId] = values;
            }

            try
            {
                var fs = fileSystem ?? TheVFS.VFS;
                using var stream = fs.OpenWrite(SavePath);
                using var writer = new StreamWriter(stream);
                writer.Write(JsonSerializer.Serialize(model, SerializerOptions));
                _dirty = false;
            }
            catch (Exception ex)
            {
                Logging.Warning($"Could not save car colours: {ex.Message}");
            }
        }
    }

    /// <summary>Writes pending overrides if a write was deferred.</summary>
    public static void Flush()
    {
        Save();
        CarWheels.Flush();
    }

    public static Color3?[] SavedColors(string carId)
    {
        lock (Gate)
        {
            EnsureLoaded();
            var colors = new Color3?[SlotCount];
            if (Overrides.TryGetValue(Key(carId), out var slots))
                for (var slot = 0; slot < SlotCount; slot++)
                    if (slots.TryGetValue(slot.ToString(), out var color))
                        colors[slot] = color;

            return colors;
        }
    }

    public static Color3?[] NormalizeColors(IReadOnlyList<Color3?>? colors)
        => colors is null ? [] : colors.Take(SlotCount)
            .Select(color => color is { } value ? (Color3?)ClampColor(value) : null).ToArray();

    public static Rad3d CreateInstance(Rad3d source, IReadOnlyList<Color3?>? colors = null, WheelAppearance? wheels = null)
    {
        lock (Gate)
        {
            States.TryGetValue(source, out var state);
            var polys = source.Polys.ToArray();

            for (var i = 0; i < polys.Length; i++)
            {
                if (polys[i].ColNum is not { } slot || slot < 0 || slot >= SlotCount)

                    continue;
                var color = colors is not null && slot < colors.Count && colors[slot] is { } chosen
                    ? ClampColor(chosen) : state?.Base[slot] ?? polys[i].Color;
                polys[i] = polys[i] with { Color = color };
            }

            var instance = new Rad3d(
                source.Colors.ToArray(),
                source.Stats,
                source.Wheels,
                source.Rims,
                source.Boxes,
                polys,
                source.CastsShadow,
                source.Atp,
                source.FileName,
                source.CollisionMesh,
                source.CollisionHull,
                source.AtLines?.ToArray(),
                source.RechargedStats
            );
            CarWheels.CopyTo(source, instance, wheels);

            return instance;
        }
    }

    /// <summary>Rewrites the painted slots of <paramref name="rad"/>. Cheap to call repeatedly.</summary>
    public static void Apply(Rad3d rad, string carId)
    {
        lock (Gate)
        {
            EnsureLoaded();
            var state = States.GetOrCreateValue(rad);
            CarWheels.Apply(rad, CarWheels.Get(carId));

            if (state.Version == _version)

                return;

            if (!state.Captured)
            {
                for (var slot = 0; slot < SlotCount; slot++)
                    state.Base[slot] = LiveColor(rad, slot);
                state.Captured = true;
            }

            Overrides.TryGetValue(Key(carId), out var overrides);
            var polys = rad.Polys;

            for (var slot = 0; slot < SlotCount; slot++)
            {
                Color3? target = overrides is not null && overrides.TryGetValue(slot.ToString(), out var chosen)
                    ? chosen
                    : state.Base[slot];

                if (target is null)

                    continue;

                for (var i = 0; i < polys.Length; i++)
                {
                    var poly = polys[i];
                    if (poly.ColNum == slot && !poly.Color.Equals(target.Value))
                        polys[i] = poly with { Color = target.Value };
                }
            }

            state.Version = _version;
        }
    }

    private static readonly JsonSerializerOptions SerializerOptions = new() { WriteIndented = false };

    private static string Key(string carId) => RadpackReader.Identity(carId.Replace('\\', '/'));

    private static void EnsureLoaded()
    {
        if (!_loaded)
            Load();
    }

    private static Color3? LiveColor(Rad3d rad, int slot)
    {
        foreach (var poly in rad.Polys)

            if (poly.ColNum == slot)

                return poly.Color;

        return null;
    }

    private static int Pack(Color3 color)
        => (Math.Clamp(color.R, (short)0, (short)255) << 16)
           | (Math.Clamp(color.G, (short)0, (short)255) << 8)
           | Math.Clamp(color.B, (short)0, (short)255);

    private static Color3 Unpack(int rgb)
        => new((short)((rgb >> 16) & 0xFF), (short)((rgb >> 8) & 0xFF), (short)(rgb & 0xFF));
}
