namespace NFMWorldLibrary.Multiplayer;

public enum SessionState : byte
{
    NotStarted,
    WaitingToLoad,
    Started,
    Finished
}