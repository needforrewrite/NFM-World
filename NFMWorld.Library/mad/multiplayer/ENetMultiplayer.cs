namespace NFMWorldLibrary.Mad.Multiplayer;

public class ENetMultiplayer
{
    private static GameOrchestrator _server;

    static ENetMultiplayer()
    {
        Init();
    }

    private static bool _init = false;
    public static void Init()
    {
        if (_init) return;
        _init = true;
        ENet.Library.Initialize();
        AppDomain.CurrentDomain.ProcessExit += (s, e) =>
        {
            ENet.Library.Deinitialize();
        };
    }

    public static void StartServer(ushort port)
    {
        _server = new GameOrchestrator(new ENetMultiplayerServerTransport(port));
        _server.Start();
    }
}