using System.Runtime.InteropServices;
using MemoryPack;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Files.Demo;

namespace NFMWorldLibrary;

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerState
{
    public required CarFrame CarFrame;
    public required uint Ticks;

    private ulong _currentTimeInMs;

    [MemoryPackIgnore]
    public required DateTimeOffset CurrentTime
    {
        readonly get => DateTimeOffset.FromUnixTimeMilliseconds((long)_currentTimeInMs);
        set => _currentTimeInMs = (ulong)value.ToUnixTimeMilliseconds();
    }
    
    public static void ApplyTo(PlayerState state, BackendCar c)
    {
        state.CarFrame.ApplyToCar(c);
    }
    
    public static PlayerState CreateFrom(uint ticks, BackendCar car)
    {
        return new PlayerState
        {
            CarFrame = CarFrame.Create(car),
            CurrentTime = DateTimeOffset.UtcNow,
            Ticks = ticks
        };
    }
}
