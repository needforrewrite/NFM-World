using Steamworks;

namespace NFMWorldLibrary.Multiplayer;

public class SteamMultiplayer
{
    private static GameOrchestrator _server;
    
    // private static readonly Dictionary<sbyte, PlayerState> _otherPlayersStates = [];

    static SteamMultiplayer()
    {
        Init();
    }

    private static bool _init = false;
    public static void Init()
    {
        if (_init) return;
        _init = true;
        
        try
        {
            SteamClient.Init(480);
            SteamNetworkingUtils.InitRelayNetworkAccess();
        }
        catch (Exception ex)
        {
            Logging.Info($"Steam initialization failed: {ex.Message}\n{ex.StackTrace}");
        }
    }

    public static void StartServer(int virtualport = 0)
    {
        _server = new GameOrchestrator(new SteamMultiplayerServerTransport(virtualport));
        _server.Start();
    }

}