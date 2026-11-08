using System.Globalization;
using System.Text;

namespace NFMWorldLibrary.Hud;

public static class HudText
{
    private static readonly HashSet<string> Tokens = new(StringComparer.Ordinal)
    {
        "speed", "power", "damage", "lap", "totalLaps", "time", "position", "totalRacers",
        "wasted", "opponents", "countdown", "message", "target", "arrowMode"
    };

    public static void Validate(string template)
    {
        foreach (var part in Parse(template))
            if (part.Token && !Tokens.Contains(part.Text))
                throw new FormatException($"Unknown HUD token '{{{part.Text}}}'.");
    }

    public static Func<IHudData, string> Compile(string template)
    {
        Validate(template);
        var parts = Parse(template).ToArray();
        if (parts.All(part => !part.Token)) return _ => template;
        return data =>
        {
            var text = new StringBuilder();
            foreach (var part in parts) text.Append(part.Token ? Value(part.Text, data) : part.Text);
            return text.ToString();
        };
    }

    private static IEnumerable<(bool Token, string Text)> Parse(string template)
    {
        for (var offset = 0; offset < template.Length;)
        {
            var start = template.IndexOf('{', offset);
            if (start < 0) start = template.Length;
            var literal = template[offset..start];
            if (literal.Contains('}')) throw new FormatException("Unmatched '}' in HUD text.");
            if (literal.Length > 0) yield return (false, literal);
            if (start == template.Length) yield break;
            var end = template.IndexOf('}', start + 1);
            if (end < 0) throw new FormatException("Unmatched '{' in HUD text.");
            yield return (true, template[(start + 1)..end]);
            offset = end + 1;
        }
    }

    private static string Value(string token, IHudData data) => token switch
    {
        "speed" => Number(data.SpeedKmh),
        "power" => Number((int)MathF.Round(data.PowerFraction * 100)),
        "damage" => Number((int)MathF.Round(data.DamageFraction * 100)),
        "lap" => Number(data.Lap),
        "totalLaps" => Number(data.TotalLaps),
        "time" => FormatTime(data.LapTimeMs),
        "position" => Number(data.Position),
        "totalRacers" => Number(data.TotalRacers),
        "wasted" => Number(data.Wasted),
        "opponents" => Number(data.Opponents),
        "countdown" => Number(data.Countdown),
        "message" => data.Message,
        "target" => data.ArrowTarget,
        "arrowMode" => data.ArrowAtCars ? "Cars" : "Track",
        _ => throw new ArgumentException($"Unknown HUD token '{token}'.")
    };

    private static string Number(int value) => value.ToString(CultureInfo.InvariantCulture);

    public static string FormatTime(int ms)
    {
        ms = Math.Max(0, ms);
        return FormattableString.Invariant($"{ms / 60000:00}:{ms / 1000 % 60:00}.{ms % 1000:000}");
    }
}
