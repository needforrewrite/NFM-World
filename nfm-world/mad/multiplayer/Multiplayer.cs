using Steamworks;

namespace NFMWorld.Mad;

public class Multiplayer
{
    private static GameOrchestrator _server;
    
    // private static readonly Dictionary<sbyte, PlayerState> _otherPlayersStates = [];

    public static void Initialize()
    {
        try
        {
            SteamClient.Init(480);
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Steam initialization failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static void StartServer(int virtualport = 0)
    {
        _server = new GameOrchestrator(new SteamMultiplayerServerTransport(virtualport));
    }

    // public static void OnReceived(byte opcode, Span<byte> data)
    // {
    //     switch (opcode)
    //     {
    //         case S2C_PlayerState.Opcode:
    //             var s2cPlayerState = MemoryMarshal.Read<S2C_PlayerState>(data);
    //             _otherPlayersStates[s2cPlayerState.PlayerIndex] = s2cPlayerState.State;
    //             break;
    //     }
    // }

}