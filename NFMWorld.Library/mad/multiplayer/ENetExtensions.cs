using ENet;
using Maxine.Extensions;

namespace NFMWorldLibrary.Multiplayer;

public static class ENetExtensions
{
    extension(Packet packet)
    {
        public Span<byte> AsSpan()
        {
            if (!packet.IsSet)
                throw new InvalidOperationException("Packet is not set.");

            unsafe
            {
                byte* dataPtr = (byte*)packet.Data.ToPointer();
                return new Span<byte>(dataPtr, packet.Length);
            }
        }

        public UnmanagedMemoryManager<byte> AsMemory()
        {
            if (!packet.IsSet)
                throw new InvalidOperationException("Packet is not set.");

            unsafe
            {
                byte* dataPtr = (byte*)packet.Data.ToPointer();
                return new UnmanagedMemoryManager<byte>(dataPtr, packet.Length);
            }
        }
    }

    extension(ref Packet packet)
    {
        public void Create(ReadOnlySpan<byte> data, PacketFlags flags = PacketFlags.Reliable)
        {
            unsafe
            {
                fixed (byte* dataPtr = data)
                {
                    packet.Create((IntPtr)dataPtr, data.Length, flags);
                }
            }
        }
    }
}