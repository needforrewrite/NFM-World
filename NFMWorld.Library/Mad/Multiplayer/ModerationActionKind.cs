namespace NFMWorldLibrary.Multiplayer;

/// <summary>
/// One moderation action. Persisted in <c>ModerationAction.Action</c> and sent as its numeric
/// value over HTTP, so append only — never renumber, never rename.
/// </summary>
public enum ModerationActionKind : byte
{
    Kick = 0,
    TempKick = 1,
    Ban = 2,
    TempBan = 3,
    Unban = 4,
    Mute = 5,
    TempMute = 6,
    Unmute = 7,
    Unlist = 8,
    Relist = 9,
    Rename = 10,
    SetPassword = 11,
    SetRole = 12,
    DeleteRequest = 13,
    DeleteCancel = 14,
    DeleteErase = 15
}
