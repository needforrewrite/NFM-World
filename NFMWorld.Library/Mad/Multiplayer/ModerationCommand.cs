namespace NFMWorldLibrary.Multiplayer;

/// <summary>A parsed chat command; <see cref="Action"/> is the enum, so no string action escapes.</summary>
public sealed record ModerationCommandRequest(
    ModerationActionKind Action,
    string Target,
    string? Reason,
    TimeSpan? Duration,
    string? Argument
);

/// <summary>
/// Parses a moderator chat command. The verbs are the only user-typed names in the system and map
/// to <see cref="ModerationActionKind"/> members; nothing else accepts a string action.
/// </summary>
public static class ModerationCommand
{
    private static readonly Dictionary<string, ModerationActionKind> NoDuration = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kick"] = ModerationActionKind.Kick,
        ["ban"] = ModerationActionKind.Ban,
        ["mute"] = ModerationActionKind.Mute,
        ["unban"] = ModerationActionKind.Unban,
        ["unmute"] = ModerationActionKind.Unmute,
        ["unlist"] = ModerationActionKind.Unlist,
        ["relist"] = ModerationActionKind.Relist,
        ["deleterequest"] = ModerationActionKind.DeleteRequest,
        ["deletecancel"] = ModerationActionKind.DeleteCancel,
        ["rename"] = ModerationActionKind.Rename
    };

    private static readonly Dictionary<string, ModerationActionKind> WithDuration = new(StringComparer.OrdinalIgnoreCase)
    {
        ["kick"] = ModerationActionKind.TempKick,
        ["ban"] = ModerationActionKind.TempBan,
        ["mute"] = ModerationActionKind.TempMute
    };

    /// <summary>False for an unknown verb or a missing target; the caller answers "Unknown command."</summary>
    public static bool TryParse(string text, out ModerationCommandRequest request)
    {
        request = null!;

        if (!text.StartsWith('/'))

            return false;

        var tokens = text[1..].Split(' ', StringSplitOptions.RemoveEmptyEntries);

        if (tokens.Length == 0)

            return false;

        var verb = tokens[0];

        if (tokens.Length < 2)

            return false;

        var target = tokens[1];
        var rest = tokens[2..];

        if (WithDuration.TryGetValue(verb, out var tempAction))
        {
            // "<verb> <target> [duration] [reason...]" — an unparseable duration token is treated
            // as the start of the reason, not an error.

            if (rest.Length > 0 && ModerationDurations.TryParse(rest[0], out var duration))
            {
                request = new ModerationCommandRequest(tempAction, target, Reason(rest[1..]), duration, null);

                return true;
            }

            // No duration parsed: this is the permanent form.
            request = new ModerationCommandRequest(NoDuration[verb], target, Reason(rest), null, null);

            return true;
        }

        if (NoDuration.TryGetValue(verb, out var noDurationAction))
        {
            if (verb.Equals("rename", StringComparison.OrdinalIgnoreCase))
            {
                var arg = rest.Length == 0 ? null : string.Join(" ", rest).Trim();
                request = new ModerationCommandRequest(ModerationActionKind.Rename, target, null, null, arg);

                return true;
            }

            request = new ModerationCommandRequest(noDurationAction, target, Reason(rest), null, null);

            return true;
        }

        return false;
    }

    private static string? Reason(string[] tokens)
    {
        var joined = string.Join(" ", tokens).Trim();

        return joined.Length == 0 ? null : joined;
    }
}

/// <summary>Accepts <c>&lt;n&gt;&lt;s|m|h|d|w&gt;</c>, e.g. 30m, 7d, 12h.</summary>
public static class ModerationDurations
{
    public static bool TryParse(string token, out TimeSpan value)
    {
        value = default;

        if (string.IsNullOrWhiteSpace(token) || token.Length < 2)

            return false;

        if (!long.TryParse(token[..^1], out var number) || number <= 0)

            return false;

        value = token[^1] switch
        {
            's' or 'S' => TimeSpan.FromSeconds(number),
            'm' or 'M' => TimeSpan.FromMinutes(number),
            'h' or 'H' => TimeSpan.FromHours(number),
            'd' or 'D' => TimeSpan.FromDays(number),
            'w' or 'W' => TimeSpan.FromDays(number * 7),
            _ => default
        };

        return value != default;
    }
}
