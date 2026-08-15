using nfm_world_library.Lua;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

[LuaVisible]
public partial class PhysicsController
{
    private readonly IReadOnlyList<ClientSidePlayer> _players;
    private readonly BackendStage _stage;
    private int _newTick = 0;

    [LuaHidden]
    public PhysicsController(IReadOnlyList<ClientSidePlayer> players, BackendStage stage)
    {
        _players = players;
        _stage = stage;
    }

    public void GameTick()
    {
        for (var i = 0; i < _players.Count; i++)
        {
            var player = _players[i];
            if (player.Bot is { } bot && player.Car is { } car)
            {
                bot.RunAi(car, i);
            }
        }

        // Inter-car collision at original tickrate (21.4 TPS)
        if (++_newTick == Physics.OriginalTicksPerNewTick)
        {
            for (int i = 0; i < _players.Count; i++)
            for (int j = 0; j < _players.Count; j++)
            {
                if (i != j && _players[i].Car is { } leftCar && _players[j].Car is { } rightCar)
                {
                    leftCar.Collide(rightCar);
                }
            }

            _newTick = 0;
        }

        foreach (var player in _players)
        {
            player.Car?.Drive(_stage);
        }
    }
}