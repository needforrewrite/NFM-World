using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

public class BackendGamemodeData : IGamemodeData
{
    public required BackendStage CurrentStage { get; init; }
    public required RaceState RaceState { get; init; }
    public IClientCallbacks ClientCallbacks => ClientServer.AccidentallyCalledClientMethodOnServer<IClientCallbacks>();

    public void SendServerEvent(ReadOnlySpan<byte> payload)
    {
        // Wired up when the singleplayer local host exists (single-path rework).
    }

    public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
    {
        // Wired up when Players becomes the single source of truth.
    }

    public static BackendGamemodeData Create(string stage)
    {
        var backendStage = new BackendStage(stage);

        return new BackendGamemodeData
        {
            CurrentStage = backendStage,
            RaceState = RaceState.InProgress
        };
    }

    public static IGamemodeData Create(string stage, StageLoader stageData)
    {
        var backendStage = new BackendStage(stage, stageData);

        return new BackendGamemodeData
        {
            CurrentStage = backendStage,
            RaceState = RaceState.InProgress
        };
    }
}
