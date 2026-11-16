namespace NFMWorldLibrary.Multiplayer;

public enum PlayerDepartureReason : byte
{
    Disconnected,
    TimedOut
}

public static class PlayerDeparture
{
    public static string Message(string name, PlayerDepartureReason reason)
        => $"{name} has left the game ({(reason == PlayerDepartureReason.TimedOut ? "timed out" : "disconnected")})";
}
