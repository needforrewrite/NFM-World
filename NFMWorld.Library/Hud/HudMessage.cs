namespace NFMWorldLibrary.Hud;

/// <summary>How a race message renders: neutral, a caution, or an alarm.</summary>
public enum HudMessageKind
{
    Info,
    Warning,
    Danger
}

/// <summary>One line of in-race status text, as written by <see cref="RaceAnnouncer"/>.</summary>
public readonly record struct HudMessage(string Text, HudMessageKind Kind);
