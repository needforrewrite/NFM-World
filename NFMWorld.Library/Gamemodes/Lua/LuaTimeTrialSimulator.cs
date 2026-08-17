using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes.Lua;

/// <summary>
/// Headless time-trial replay validation. Runs the Lua <c>nfmm/timetrial</c>
/// gamemode through the single-player path (client gamemode + in-process
/// <see cref="LocalRaceHost"/>) while feeding the recorded inputs from a
/// <see cref="SavedTimeTrial"/> into the player car each tick.
/// </summary>
public static class LuaTimeTrialSimulator
{
    public static int? Run(string stageName, SavedTimeTrial replay, string carName, int tickLimit)
    {
        var parameters = new GamemodeParameters
        {
            Players =
            [
                new ClientSidePlayerParameters
                {
                    PlayerName = "Player",
                    CarName = carName,
                    Color = new Color3(255, 0, 0),
                    IsBot = false,
                    IsClientPlayer = true
                }
            ]
        };

        var factory = new LuaGamemodeFactory("nfmm/timetrial", new Dictionary<string, object>
        {
            ["simulation"] = true
        });

        var host = LocalRaceHost.Create(stageName, factory, parameters);

        var data = new BackendGamemodeData
        {
            CurrentStage = replay.StageData is { } stageData
                ? new BackendStage(stageName, stageData)
                : new BackendStage(stageName),
            RaceState = RaceState.InProgress,
            ServerEventSink = host.SendServerEvent
        };

        var client = factory.CreateGameMode(parameters, data);

        var finished = false;
        host.GameFinished += _ => finished = true;

        client.Begin();
        client.Reset();
        host.Start();

        var tick = 0;
        while (tick <= tickLimit && !finished)
        {
            host.Update();
            if (finished)
                break;

            if (client.Players[0].Car is { } car)
                car.Control.Decode(replay.GetTick(tick) ?? default);

            client.GameTick();
            tick++;
        }

        return finished ? tick : null;
    }
}
