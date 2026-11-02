using System.Text.Json;
using System.Text.Json.Serialization;

namespace NFMWorldLibrary.Hud;

public sealed record HudSkinInfo(string Id, string Name, string Author, string Description);

public sealed class HudSkinDefinition
{
    public int Version { get; init; } = 1;
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string Description { get; init; } = "";
    public float ReferenceWidth { get; init; } = 960;
    public float ReferenceHeight { get; init; } = 540;
    public bool ShowDefaultMessages { get; init; } = true;
    public HudElement[] Elements { get; init; } = [];

    [JsonIgnore] public HudSkinInfo Info => new(Id, Name, Author, Description);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };

    public static HudSkinDefinition Parse(string json)
    {
        var skin = JsonSerializer.Deserialize<HudSkinDefinition>(json, JsonOptions)
            ?? throw new FormatException("Empty skin manifest.");
        if (skin.Version != 1) throw new FormatException("Unsupported HUD skin version.");
        if (!HudSkinId.IsValid(skin.Id) || skin.Id != HudSkinId.Normalize(skin.Id))
            throw new FormatException("Skin id must contain lowercase letters, digits, hyphens or underscores.");
        if (string.IsNullOrWhiteSpace(skin.Name) || skin.Name.Length > 80 ||
            skin.Author is null || skin.Description is null)
            throw new FormatException("A skin needs a name, author and description strings.");
        if (!Positive(skin.ReferenceWidth) || !Positive(skin.ReferenceHeight))
            throw new FormatException("Invalid reference dimensions.");
        if (skin.Elements is null || skin.Elements.Length is < 1 or > 128)
            throw new FormatException("A skin must have 1 to 128 elements.");
        foreach (var element in skin.Elements)
        {
            if (element is null) throw new FormatException("An element cannot be null.");
            element.Validate();
        }
        return skin;
    }

    internal static bool Positive(float value) => float.IsFinite(value) && value > 0 && value <= 16384;
}

public sealed class HudElement
{
    public string Type { get; init; } = "text";
    public string Anchor { get; init; } = "topLeft";
    public string VisibleWhen { get; init; } = "always";
    public float X { get; init; }
    public float Y { get; init; }
    public float Width { get; init; } = 180;
    public float Height { get; init; } = 24;
    public string Text { get; init; } = "";
    public string Value { get; init; } = "power";
    public string Color { get; init; } = "#B9D3FF";
    public string Background { get; init; } = "#00000066";
    public string Outline { get; init; } = "#000000";
    public float OutlineWidth { get; init; } = 1;
    public string Font { get; init; } = "Adventure";
    public float FontSize { get; init; } = 16;
    public string Align { get; init; } = "left";
    public string Source { get; init; } = "";
    public bool CriticalDamage { get; init; }
    public bool Vertical { get; init; }

    internal void Validate()
    {
        if (Type is not ("text" or "bar" or "image" or "arrow" or "standings"))
            throw new FormatException($"Unknown element type '{Type}'.");
        if (Anchor is not ("topLeft" or "topCenter" or "topRight" or "center" or "bottomLeft" or "bottomCenter" or "bottomRight"))
            throw new FormatException($"Unknown anchor '{Anchor}'.");
        if (VisibleWhen is not ("always" or "timeTrial" or "freePlay" or "hasTarget" or "hasPosition"))
            throw new FormatException($"Unknown visibility '{VisibleWhen}'.");
        if (!float.IsFinite(X) || !float.IsFinite(Y) || MathF.Abs(X) > 16384 || MathF.Abs(Y) > 16384 ||
            !HudSkinDefinition.Positive(Width) || !HudSkinDefinition.Positive(Height) ||
            !HudSkinDefinition.Positive(FontSize) || FontSize > 256 ||
            !float.IsFinite(OutlineWidth) || OutlineWidth is < 0 or > 16)
            throw new FormatException("Invalid element dimensions.");
        if (Font is not ("Adventure" or "AdventureHollow" or "DroidSans" or "NotoSans" or "RobotoMono") ||
            Align is not ("left" or "center" or "right"))
            throw new FormatException("Unknown font or text alignment.");
        if (!IsColor(Color) || !IsColor(Background) || !IsColor(Outline))
            throw new FormatException("Colors must be #RRGGBB or #RRGGBBAA.");
        if (Text is null || Text.Length > 1024) throw new FormatException("Invalid text.");
        HudText.Validate(Text);
        if (Type == "bar" && Value is not ("power" or "damage"))
            throw new FormatException("Bars bind to power or damage.");
        if (Type == "image" && !IsAssetPath(Source))
            throw new FormatException("Images need a relative path inside their skin folder.");
    }

    public bool IsVisible(IHudData data) => VisibleWhen switch
    {
        "timeTrial" => data.TimeTrial,
        "freePlay" => !data.TimeTrial,
        "hasTarget" => data.ArrowAngle is not null,
        "hasPosition" => data.HasPosition,
        _ => true
    };

    private static bool IsColor(string? color) => color is { Length: 7 or 9 } && color[0] == '#'
        && color.AsSpan(1).IndexOfAnyExcept("0123456789abcdefABCDEF") < 0;

    public static bool IsAssetPath(string? path) => !string.IsNullOrWhiteSpace(path)
        && !path.Any(char.IsControl) && !path.Contains(':') && !path.Contains('\\')
        && path.Split('/').All(part => part.Length > 0 && part is not "." and not "..")
        && Path.GetExtension(path).ToLowerInvariant() is ".svg" or ".png" or ".jpg" or ".jpeg" or ".dds";
}

public static class HudSkinId
{
    public const string Default = "classic";
    public static bool IsValid(string? id) => id is { Length: > 0 and <= 64 }
        && id.All(c => c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '_');
    public static string Normalize(string? id) => IsValid(id) ? id!.ToLowerInvariant() : Default;
}
