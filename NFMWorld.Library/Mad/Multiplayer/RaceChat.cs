namespace NFMWorldLibrary.Multiplayer;

public static class RaceChat
{
    public const int MaxMessageLength = 512;
    public const int HistoryLimit = 100;

    public static string Normalize(string? message)
    {
        if (string.IsNullOrWhiteSpace(message)) return "";
        var text = new string(message.Trim().Take(MaxMessageLength)
            .Select(c => char.IsControl(c) ? ' ' : c).ToArray()
        ).Trim();
        return text.Length > 0 && char.IsHighSurrogate(text[^1]) ? text[..^1] : text;
    }
}
