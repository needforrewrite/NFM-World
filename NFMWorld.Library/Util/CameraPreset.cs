using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFMWorldLibrary.Util;

public sealed record CameraPreset(string Name, float Fov, int FollowY, int FollowZ, bool SmoothFov)
{
    public const float MinFov = 58.7f;
    public const float MaxFov = 120f;
    public const int MinFollowY = -160;
    public const int MaxFollowY = 500;
    public const int MinFollowZ = -250;
    public const int MaxFollowZ = 50;

    public static CameraPreset NFMW { get; } = new("World", 80, -98, -250, true);
    public static CameraPreset Classic { get; } = new("Classic", 58.715516388168026651329f, 0, 0, false);

    public static float ClampFov(float value) => float.IsFinite(value) ? Math.Clamp(value, MinFov, MaxFov) : NFMW.Fov;

    public CameraPreset Clamp() => this with
    {
        Fov = ClampFov(Fov),
        FollowY = Math.Clamp(FollowY, MinFollowY, MaxFollowY),
        FollowZ = Math.Clamp(FollowZ, MinFollowZ, MaxFollowZ)
    };

    public bool Matches(CameraPreset other) => Math.Abs(Fov - other.Fov) < 0.051f
                                               && FollowY == other.FollowY
                                               && FollowZ == other.FollowZ
                                               && SmoothFov == other.SmoothFov;

    public static string ValidateCustomName(string name)
    {
        name = (name ?? "").Trim();
        if (name.Length is 0 or > 40 || name.Any(char.IsControl))
            throw new ArgumentException("Use a preset name between 1 and 40 characters.");
        if (name.Equals(NFMW.Name, StringComparison.OrdinalIgnoreCase)
            || name.Equals(Classic.Name, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("World and Classic are built-in presets. Choose another name.");
        return name;
    }
}

public static class CameraPresetConfig
{
    public static CameraPreset ReadCamera(string content)
    {
        var settings = Lines(content).ToArray();
        // Older configs inherited Classic for camera keys they did not explicitly save.
        var camera = settings.Length == 0 ? CameraPreset.NFMW : CameraPreset.Classic;
        foreach (var (key, value) in settings)
        {
            switch (key)
            {
                case "camera_fov" when float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fov)
                                       && float.IsFinite(fov):
                    camera = camera with { Fov = fov }; break;
                case "camera_follow_y" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var y):
                    camera = camera with { FollowY = y }; break;
                case "camera_follow_z" when int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var z):
                    camera = camera with { FollowZ = z }; break;
                case "camera_smooth_fov" when int.TryParse(value, out var smooth):
                    camera = camera with { SmoothFov = smooth != 0 }; break;
            }
        }

        return camera.Clamp();
    }

    public static List<CameraPreset> ReadPresets(string json)
    {
        var presets = JsonSerializer.Deserialize(json, CameraPresetJsonContext.Default.ListCameraPreset) ?? [];
        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<CameraPreset>();
        foreach (var preset in presets)
        {
            if (preset is null || !float.IsFinite(preset.Fov) || preset.Fov <= 0 || preset.Fov >= 180) continue;
            try
            {
                var name = CameraPreset.ValidateCustomName(preset.Name);
                if (names.Add(name)) result.Add((preset with { Name = name }).Clamp());
            }
            catch (ArgumentException)
            {
            }
        }

        return result;
    }

    public static string WritePresets(IEnumerable<CameraPreset> presets) =>
        JsonSerializer.Serialize(presets.ToList(), CameraPresetJsonContext.Default.ListCameraPreset);

    private static IEnumerable<(string Key, string Value)> Lines(string content)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith("//")) continue;
            var parts = trimmed.Split(' ', 2, StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length == 2) yield return (parts[0], parts[1]);
        }
    }
}

[JsonSerializable(typeof(List<CameraPreset>))]
internal partial class CameraPresetJsonContext : JsonSerializerContext;
