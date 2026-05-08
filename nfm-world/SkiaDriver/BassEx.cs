using System.Runtime.InteropServices;
using ManagedBass;
using ManagedBass.Opus;

namespace NFMWorld.SkiaDriver;

public unsafe class BassEx
{
    public static int CreateStream(ReadOnlySpan<byte> Memory, BassFlags Flags)
    {
        var ptr = Marshal.AllocHGlobal(Memory.Length);
        // Copy data to unmanaged memory
        Memory.CopyTo(new Span<byte>((void*)ptr, Memory.Length));
        int handle = Bass.CreateStream(ptr, 0, Memory.Length, Flags);
        if (handle == 0)
            Marshal.FreeHGlobal(ptr);
        else
            Bass.ChannelSetSync(handle, SyncFlags.Free, 0L, static (a, b, c, ptr) => Marshal.FreeHGlobal(ptr), ptr);
        return handle;
    }
    
    public static int MusicLoad(ReadOnlySpan<byte> Memory, BassFlags Flags)
    {
        var ptr = Marshal.AllocHGlobal(Memory.Length);
        // Copy data to unmanaged memory
        Memory.CopyTo(new Span<byte>((void*)ptr, Memory.Length));
        int handle = Bass.MusicLoad(ptr, 0, Memory.Length, Flags);
        if (handle == 0)
            Marshal.FreeHGlobal(ptr);
        else
            Bass.ChannelSetSync(handle, SyncFlags.Free, 0L, static (a, b, c, ptr) => Marshal.FreeHGlobal(ptr), ptr);
        return handle;
    }
    
    public static int OpusCreateStream(ReadOnlySpan<byte> Memory, BassFlags Flags)
    {
        var ptr = Marshal.AllocHGlobal(Memory.Length);
        // Copy data to unmanaged memory
        Memory.CopyTo(new Span<byte>((void*)ptr, Memory.Length));
        int handle = BassOpus.CreateStream(ptr, 0, Memory.Length, Flags);
        if (handle == 0)
            Marshal.FreeHGlobal(ptr);
        else
            Bass.ChannelSetSync(handle, SyncFlags.Free, 0L, static (a, b, c, ptr) => Marshal.FreeHGlobal(ptr), ptr);
        return handle;
    }
}