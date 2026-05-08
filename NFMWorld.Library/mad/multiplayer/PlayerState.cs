using System.Runtime.InteropServices;
using MessagePack;
using NFMWorldLibrary.Files.Demo;

namespace NFMWorldLibrary.Multiplayer;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerState
{
    public required DemoEntry DemoEntry;
    public required uint Ticks;

    private ulong _currentTimeInMs;

    [IgnoreMember]
    public required DateTimeOffset CurrentTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }
    
    public static void ApplyTo(PlayerState state, IInGameCar c)
    {
        state.DemoEntry.ApplyToCar(c);
    }
    
    public static PlayerState CreateFrom(uint ticks, IInGameCar car)
    {
        return new PlayerState
        {
            DemoEntry = DemoEntry.Create(car),
            CurrentTime = DateTimeOffset.UtcNow,
            Ticks = ticks
        };
    }
}
