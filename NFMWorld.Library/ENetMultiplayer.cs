namespace NFMWorldLibrary.Multiplayer;

public class ENetMultiplayer
{
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
}