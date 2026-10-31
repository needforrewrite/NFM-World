using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Gamemodes;

/// <summary>
/// Headless time-trial replay validation. Runs the <c>nfmm/timetrial</c> gamemode
/// through the single-player path (client gamemode + in-process
/// <see cref="LocalRaceHost"/>) while feeding the recorded inputs from a
/// <see cref="SavedTimeTrial"/> into the player car each tick.
/// </summary>
public static class TimeTrialSimulator
{
    public static int? Run(string stageName, SavedTimeTrial replay, string carName, int tickLimit)
    {
        var parameters = new ClientGamemodeParameters
        {
            Players =
            [
                new ClientSidePlayerInfo
                {
                    PlayerName = "Player",
                    CarName = carName,
                    Color = new Color3(255, 0, 0),
                    IsBot = false,
                    IsClientPlayer = true
                }
            ]
        };

        var factory = GamemodeFactory.Create("nfmm/timetrial", new Dictionary<string, object>
        {
            ["simulation"] = true
        });

        // Give the host the recorded stage too, not just the client: it builds its own
        // stage from the current files otherwise, so a stage edited since the recording
        // would make valid replays fail (or invalid ones pass).
        var host = LocalRaceHost.Create(stageName, factory, parameters, replay.StageData);

        var context = new BackendGamemodeContext
        {
            CurrentStage = replay.StageData is { } stageData
                ? new BackendStage(stageName, stageData)
                : new BackendStage(stageName),
            RaceState = RaceState.InProgress,
            ServerEventSink = host.SendServerEvent
        };

        var client = factory.CreateGameMode(parameters, context);
        var players = (client as BaseClientGamemode)?.Players;

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

            if (players is [.., { Car: { } car }])
                car.Control.Decode(replay.GetTick(tick) ?? default);

            client.GameTick();
            tick++;
        }

        // The factory owns what it loaded; this one is per run. The host releases its own
        // server gamemode and must go before the factory unloads the plugin behind it.
        client.End();
        host.Dispose();
        factory.End();

        return finished ? tick : null;
    }
}
