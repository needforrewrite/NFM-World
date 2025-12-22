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

    // public static void ReceiveMultiplayData()
    // {
    //     foreach (var (playerCarIndex, state) in _otherPlayersStates)
    //     {
    //         var c = GameSparker.cars_in_race[playerCarIndex];
    //         if (c == null)
    //         {
    //             GameSparker.cars_in_race[playerCarIndex] = c = new Car(new Stat(14), playerCarIndex, GameSparker.cars[14], 0, 0);
    //             GameSparker.current_scene.Renderables.Add(GameSparker.cars_in_race[playerCarIndex]);
    //         }
    //         c.Control.Left = state.Left;
    //         c.Control.Right = state.Right;
    //         c.Control.Up = state.Up;
    //         c.Control.Down = state.Down;
    //         c.Control.Handb = state.Handb;
    //         c.Mad.Newcar = state.Newcar;
    //         c.Mad.Mtouch = state.Mtouch;
    //         c.Mad.Wtouch = state.Wtouch;
    //         c.Mad.Pushed = state.Pushed;
    //         c.Mad.Gtouch = state.Gtouch;
    //         c.Mad.Pl = state.pl;
    //         c.Mad.Pr = state.pr;
    //         c.Mad.Pd = state.pd;
    //         c.Mad.Pu = state.pu;
    //         c.Conto.Position = new Vector3(state.x, state.y, state.z);
    //         c.Conto.Rotation = new Euler(AngleSingle.FromDegrees(state.xz), AngleSingle.FromDegrees(state.zy), AngleSingle.FromDegrees(state.xy));
    //         c.Mad.Speed = state.speed;
    //         c.Mad.Power = state.power;
    //         c.Mad.Mxz = state.mxz;
    //         c.Mad.Pzy = state.pzy;
    //         c.Mad.Pxy = state.pxy;
    //         c.Mad.Txz = state.txz;
    //         c.Mad.Loop = state.loop;
    //         c.Conto.TurningWheelAngle = c.Conto.TurningWheelAngle with { Xz = AngleSingle.FromDegrees(state.wxz) };
    //         c.Mad.Pcleared = state.pcleared;
    //         c.Mad.Clear = state.clear;
    //         c.Mad.Nlaps = state.nlaps;
    //     }
    // }
    //
    // public static unsafe void SendPlayerData(int playerCarIndex)
    // {
    //     var playerState = new C2S_PlayerState()
    //     {
    //         MyPlayerIndex = (sbyte)playerCarIndex,
    //         State = new PlayerState
    //         {
    //             Left = GameSparker.cars_in_race[playerCarIndex].Control.Left,
    //             Right = GameSparker.cars_in_race[playerCarIndex].Control.Right,
    //             Up = GameSparker.cars_in_race[playerCarIndex].Control.Up,
    //             Down = GameSparker.cars_in_race[playerCarIndex].Control.Down,
    //             Handb = GameSparker.cars_in_race[playerCarIndex].Control.Handb,
    //             Newcar = GameSparker.cars_in_race[playerCarIndex].Mad.Newcar,
    //             Mtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Mtouch,
    //             Wtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Wtouch,
    //             Pushed = GameSparker.cars_in_race[playerCarIndex].Mad.Pushed,
    //             Gtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Gtouch,
    //             pl = GameSparker.cars_in_race[playerCarIndex].Mad.Pl,
    //             pr = GameSparker.cars_in_race[playerCarIndex].Mad.Pr,
    //             pd = GameSparker.cars_in_race[playerCarIndex].Mad.Pd,
    //             pu = GameSparker.cars_in_race[playerCarIndex].Mad.Pu,
    //             x = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.X,
    //             y = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.Y,
    //             z = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.Z,
    //             xz = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Xz.Degrees,
    //             xy = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Xy.Degrees,
    //             zy = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Zy.Degrees,
    //             speed = GameSparker.cars_in_race[playerCarIndex].Mad.Speed,
    //             power = GameSparker.cars_in_race[playerCarIndex].Mad.Power,
    //             mxz = GameSparker.cars_in_race[playerCarIndex].Mad.Mxz,
    //             pzy = GameSparker.cars_in_race[playerCarIndex].Mad.Pzy,
    //             pxy = GameSparker.cars_in_race[playerCarIndex].Mad.Pxy,
    //             txz = GameSparker.cars_in_race[playerCarIndex].Mad.Txz,
    //             loop = GameSparker.cars_in_race[playerCarIndex].Mad.Loop,
    //             wxz = (int)GameSparker.cars_in_race[playerCarIndex].Conto.TurningWheelAngle.Xz.Degrees,
    //             pcleared = GameSparker.cars_in_race[playerCarIndex].Mad.Pcleared,
    //             clear = GameSparker.cars_in_race[playerCarIndex].Mad.Clear,
    //             nlaps = GameSparker.cars_in_race[playerCarIndex].Mad.Nlaps,
    //             dest = false
    //         }
    //     };
    //
    //     Span<byte> arr = stackalloc byte[sizeof(C2S_PlayerState) + 1];
    //     arr[0] = C2S_PlayerState.Opcode;
    //     MemoryMarshal.Write(arr[1..], in playerState);
    //
    //     _client?.Connection.SendMessage(arr, SendType.Unreliable);
    //     if (_server != null)
    //     {
    //         foreach (var clients in _server.ConnectedClients.Values)
    //         {
    //             clients.SendMessage(arr, SendType.Unreliable);
    //         }
    //     }
    // }
}