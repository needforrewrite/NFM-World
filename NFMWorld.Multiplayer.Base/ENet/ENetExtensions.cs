using ENet;

namespace NFMWorldLibrary.Multiplayer;

public static class ENetExtensions
{
    extension(Packet packet)
    {
        public Span<byte> AsSpan()
        {
            if (!packet.IsSet)
                throw new InvalidOperationException("Packet is not set.");

            return packet.DataSpan;
        }

        public ReadOnlyMemory<byte> AsMemory()
        {
            if (!packet.IsSet)
                throw new InvalidOperationException("Packet is not set.");

            return packet.Memory;
        }
    }
}
