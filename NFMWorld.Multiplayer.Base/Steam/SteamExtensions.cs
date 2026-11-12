using Maxine.Extensions;
using Steamworks;
using Steamworks.Data;

namespace NFMWorldLibrary.Multiplayer;

public static class SteamExtensions
{
    extension(Connection connection)
    {
        public unsafe Result SendMessage<T>(Span<T> data, SendType sendType = SendType.Reliable)
            where T : unmanaged
        {
            fixed (T* ptr = data)
                return connection.SendMessage((IntPtr)ptr, data.AsBytes().Length, sendType);
        }

        public unsafe Result SendMessage<T>(ReadOnlySpan<T> data, SendType sendType = SendType.Reliable)
            where T : unmanaged
        {
            fixed (T* ptr = data)
                return connection.SendMessage((IntPtr)ptr, data.AsBytes().Length, sendType);
        }
    }
}
