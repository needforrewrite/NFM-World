namespace ENet;

/// <summary>
/// Global ENet entry points. The managed port has no native state to initialise, so these exist
/// for API compatibility with the native wrapper.
/// </summary>
public static class Library
{
    public const uint maxChannelCount = 0xFF;
    public const uint maxPeers = 0xFFF;
    public const uint maxPacketSize = 32 * 1024 * 1024;
    public const uint throttleThreshold = 40;
    public const uint throttleScale = 32;
    public const uint throttleAcceleration = 2;
    public const uint throttleDeceleration = 2;
    public const uint throttleInterval = 5000;
    public const uint timeoutLimit = 32;
    public const uint timeoutMinimum = 5000;
    public const uint timeoutMaximum = 30000;
    public const uint version = (2 << 16) | (5 << 8) | 3;

    /// <summary>Monotonic millisecond clock, the managed equivalent of <c>enet_time_get</c>.</summary>
    public static uint Time => (uint)Environment.TickCount64;

    public static bool Initialize() => true;

    public static void Deinitialize()
    {
    }
}
