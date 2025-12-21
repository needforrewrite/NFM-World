using System.Runtime.InteropServices;
using Steamworks;
using Steamworks.Data;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Multiplayer
{
    public static bool Connected;

    private static MultiplayerClient? _client;
    private static MultiplayerServer? _server;
    public static bool InMultiplayerGame;
    public static bool IsServerAPlayer = true;
    
    private static readonly Dictionary<sbyte, PlayerState> _otherPlayersStates = [];

    public static void Initialize()
    {
        SteamClient.Init(480);
    }

    public static void StartServer(int virtualport = 0)
    {
        _server = SteamNetworkingSockets.CreateRelaySocket<MultiplayerServer>(virtualport);
        Console.WriteLine($"SteamID: {SteamClient.SteamId}");
    }

    public static void Connect(SteamId serverId, int virtualport = 1)
    {
        _client = SteamNetworkingSockets.ConnectRelay<MultiplayerClient>(serverId, virtualport);
    }

    public static void OnReceived(byte opcode, Span<byte> data)
    {
        switch (opcode)
        {
            case S2C_PlayerState.Opcode:
                var s2cPlayerState = MemoryMarshal.Read<S2C_PlayerState>(data);
                _otherPlayersStates[s2cPlayerState.PlayerIndex] = s2cPlayerState.State;
                break;
        }
    }

    public static void ReceiveMultiplayData()
    {
        foreach (var (playerCarIndex, state) in _otherPlayersStates)
        {
            var c = GameSparker.cars_in_race[playerCarIndex];
            if (c == null)
            {
                GameSparker.cars_in_race[playerCarIndex] = c = new Car(new Stat(14), playerCarIndex, GameSparker.cars[14], 0, 0);
                GameSparker.current_scene.Renderables.Add(GameSparker.cars_in_race[playerCarIndex]);
            }
            c.Control.Left = state.Left;
            c.Control.Right = state.Right;
            c.Control.Up = state.Up;
            c.Control.Down = state.Down;
            c.Control.Handb = state.Handb;
            c.Mad.Newcar = state.Newcar;
            c.Mad.Mtouch = state.Mtouch;
            c.Mad.Wtouch = state.Wtouch;
            c.Mad.Pushed = state.Pushed;
            c.Mad.Gtouch = state.Gtouch;
            c.Mad.Pl = state.pl;
            c.Mad.Pr = state.pr;
            c.Mad.Pd = state.pd;
            c.Mad.Pu = state.pu;
            c.Conto.Position = new Vector3(state.x, state.y, state.z);
            c.Conto.Rotation = new Euler(AngleSingle.FromDegrees(state.xz), AngleSingle.FromDegrees(state.zy), AngleSingle.FromDegrees(state.xy));
            c.Mad.Speed = state.speed;
            c.Mad.Power = state.power;
            c.Mad.Mxz = state.mxz;
            c.Mad.Pzy = state.pzy;
            c.Mad.Pxy = state.pxy;
            c.Mad.Txz = state.txz;
            c.Mad.Loop = state.loop;
            c.Conto.TurningWheelAngle = c.Conto.TurningWheelAngle with { Xz = AngleSingle.FromDegrees(state.wxz) };
            c.Mad.Pcleared = state.pcleared;
            c.Mad.Clear = state.clear;
            c.Mad.Nlaps = state.nlaps;
        }
    }

    public static unsafe void SendPlayerData(int playerCarIndex)
    {
        var playerState = new C2S_PlayerState()
        {
            MyPlayerIndex = (sbyte)playerCarIndex,
            State = new PlayerState
            {
                Left = GameSparker.cars_in_race[playerCarIndex].Control.Left,
                Right = GameSparker.cars_in_race[playerCarIndex].Control.Right,
                Up = GameSparker.cars_in_race[playerCarIndex].Control.Up,
                Down = GameSparker.cars_in_race[playerCarIndex].Control.Down,
                Handb = GameSparker.cars_in_race[playerCarIndex].Control.Handb,
                Newcar = GameSparker.cars_in_race[playerCarIndex].Mad.Newcar,
                Mtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Mtouch,
                Wtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Wtouch,
                Pushed = GameSparker.cars_in_race[playerCarIndex].Mad.Pushed,
                Gtouch = GameSparker.cars_in_race[playerCarIndex].Mad.Gtouch,
                pl = GameSparker.cars_in_race[playerCarIndex].Mad.Pl,
                pr = GameSparker.cars_in_race[playerCarIndex].Mad.Pr,
                pd = GameSparker.cars_in_race[playerCarIndex].Mad.Pd,
                pu = GameSparker.cars_in_race[playerCarIndex].Mad.Pu,
                x = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.X,
                y = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.Y,
                z = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Position.Z,
                xz = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Xz.Degrees,
                xy = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Xy.Degrees,
                zy = (int)GameSparker.cars_in_race[playerCarIndex].Conto.Rotation.Zy.Degrees,
                speed = GameSparker.cars_in_race[playerCarIndex].Mad.Speed,
                power = GameSparker.cars_in_race[playerCarIndex].Mad.Power,
                mxz = GameSparker.cars_in_race[playerCarIndex].Mad.Mxz,
                pzy = GameSparker.cars_in_race[playerCarIndex].Mad.Pzy,
                pxy = GameSparker.cars_in_race[playerCarIndex].Mad.Pxy,
                txz = GameSparker.cars_in_race[playerCarIndex].Mad.Txz,
                loop = GameSparker.cars_in_race[playerCarIndex].Mad.Loop,
                wxz = (int)GameSparker.cars_in_race[playerCarIndex].Conto.TurningWheelAngle.Xz.Degrees,
                pcleared = GameSparker.cars_in_race[playerCarIndex].Mad.Pcleared,
                clear = GameSparker.cars_in_race[playerCarIndex].Mad.Clear,
                nlaps = GameSparker.cars_in_race[playerCarIndex].Mad.Nlaps,
                dest = false
            }
        };

        Span<byte> arr = stackalloc byte[sizeof(C2S_PlayerState) + 1];
        arr[0] = C2S_PlayerState.Opcode;
        MemoryMarshal.Write(arr[1..], in playerState);

        _client?.Connection.SendMessage(arr, SendType.Unreliable);
        if (_server != null)
        {
            foreach (var clients in _server.ConnectedClients.Values)
            {
                clients.SendMessage(arr, SendType.Unreliable);
            }
        }
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct S2C_PlayerState
{
    public const byte Opcode = 127;
    public required sbyte PlayerIndex;
    public required PlayerState State;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct C2S_PlayerState
{
    public const byte Opcode = 0;
    public required sbyte MyPlayerIndex;
    public required PlayerState State;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PlayerState
{
    public required bool Left;
    public required bool Right;
    public required bool Up;
    public required bool Down;
    public required bool Handb;
    public required bool Newcar;
    public required bool Mtouch;
    public required bool Wtouch;
    public required bool Pushed;
    public required bool Gtouch;
    public required bool pl;
    public required bool pr;
    public required bool pd;
    public required bool pu;
    public required bool dest;
    public required int x, y, z, xz, xy, zy;
    public required float speed, power;
    public required float mxz, pzy, pxy, txz;
    public required int loop;
    public required int wxz;
    public required int pcleared, clear, nlaps;
}

public class MultiplayerClient : ConnectionManager
{
    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        Console.WriteLine("Connected to server");
        Multiplayer.Connected = true;
    }

    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Console.WriteLine("Connecting to server");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        Console.WriteLine("Disconnected from server");
        Multiplayer.Connected = false;
    }

    public override unsafe void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        base.OnMessage(data, size, messageNum, recvTime, channel);
        var messageData = new Span<byte>((byte*)data, size);
        Multiplayer.OnReceived(messageData[0], messageData[1..]);
    }
}

internal class MultiplayerServer : SocketManager
{
    public Dictionary<uint, Connection> ConnectedClients { get; } = [];
    public Dictionary<uint, sbyte> PlayerCarIndices { get; } = [];
    
    public override void OnConnecting(Connection connection, ConnectionInfo info)
    {
        base.OnConnecting(connection, info);
        connection.Accept();
        Console.WriteLine($"{info.Identity} is connecting");
    }

    public override void OnConnected(Connection connection, ConnectionInfo info)
    {
        base.OnConnected(connection, info);
        Console.WriteLine($"{info.Identity} has joined the game");
        ConnectedClients.TryAdd(connection, connection);
    }

    public override void OnDisconnected(Connection connection, ConnectionInfo info)
    {
        base.OnDisconnected(connection, info);
        ConnectedClients.Remove(connection);
        Console.WriteLine($"{info.Identity} has left the game");
    }

    public override unsafe void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        base.OnMessage(connection, identity, data, size, messageNum, recvTime, channel);
        var messageData = new Span<byte>((byte*)data, size);
        
        var opcode = messageData[0];
        var message = messageData[1..];
        
        if (Multiplayer.IsServerAPlayer)
        {
            Multiplayer.OnReceived(opcode, message);
        }

        switch (opcode)
        {
            case C2S_PlayerState.Opcode:
            {
                var playerState = MemoryMarshal.Read<C2S_PlayerState>(message);
                if (!PlayerCarIndices.TryGetValue(connection, out var playerIndex))
                {
                    // TODO the server should be the one to tell the client their player index
                    PlayerCarIndices[connection] = playerState.MyPlayerIndex;
                }

                var s2cPlayerState = new S2C_PlayerState()
                {
                    PlayerIndex = playerIndex,
                    State = playerState.State
                };
                Span<byte> arr = stackalloc byte[sizeof(S2C_PlayerState) + 1];
                arr[0] = S2C_PlayerState.Opcode;
                MemoryMarshal.Write(arr[1..], in s2cPlayerState);

                // Broadcast to other clients
                BroadcastMessage(arr);
                break;
            }
        }

        return;

        void BroadcastMessage(Span<byte> messageData)
        {
            foreach (var client in ConnectedClients.Values)
            {
                if (client.Id != connection.Id)
                {
                    client.SendMessage(messageData, SendType.Unreliable);
                }
            }
        }
    }
}