using NFMWorld.PluginSystem;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend.Gamemodes;

[PluginVisible]
[method: PluginVisible]
public partial class PhysicsController(IReadOnlyList<ClientSidePlayer> players, BackendStage stage)
{
    private int _newTick = 0;

    [PluginVisible]
    public void GameTick()
    {
        for (var i = 0; i < players.Count; i++)
        {
            var player = players[i];
            if (player.Bot is { } bot)
            {
                bot.RunAi();
            }
        }

        // Inter-car collision at original tickrate (21.4 TPS)
        if (++_newTick == Physics.OriginalTicksPerNewTick)
        {
            for (int i = 0; i < players.Count; i++)
            for (int j = 0; j < players.Count; j++)
            {
                if (i != j && players[i].Car is { } leftCar && players[j].Car is { } rightCar)
                {
                    leftCar.Collide(rightCar);
                }
            }

            _newTick = 0;
        }

        foreach (var player in players)
        {
            player.Car?.Drive(stage);
        }
    }
}