using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;

namespace NFMWorldLibrary.Backend;

public class BackendGamemodeContext : IGamemodeContext
{
    public required BackendStage CurrentStage { get; init; }
    public required RaceState RaceState { get; init; }

    /// <summary>
    /// Headless client callbacks. Rendering/visual hooks are no-ops in the
    /// backend context, so gamemodes can run without a live client phase.
    /// </summary>
    public IClientCallbacks ClientCallbacks { get; init; } = NoOpClientCallbacks.Instance;

    /// <summary>
    /// Sink for <see cref="SendServerEvent"/>. Wired to the in-process local
    /// host during singleplayer (single-path rework).
    /// </summary>
    public Action<ReadOnlyMemory<byte>>? ServerEventSink { get; init; }

    /// <summary>
    /// Sink for <see cref="UpdatePlayers"/>. Wired when the server drives the
    /// player roster (Players as the single source of truth).
    /// </summary>
    public Action<IReadOnlyList<ClientSidePlayer>>? PlayerUpdateSink { get; init; }

    public void SendServerEvent(ReadOnlySpan<byte> payload)
        => ServerEventSink?.Invoke(payload.ToArray());

    public void UpdatePlayers(IReadOnlyList<ClientSidePlayer> players)
        => PlayerUpdateSink?.Invoke(players);

    public static BackendGamemodeContext Create(string stage)
    {
        return new BackendGamemodeContext
        {
            CurrentStage = new BackendStage(stage),
            RaceState = RaceState.InProgress
        };
    }

    public static BackendGamemodeContext Create(string stage, StageLoader stageData)
    {
        return new BackendGamemodeContext
        {
            CurrentStage = new BackendStage(stage, stageData),
            RaceState = RaceState.InProgress
        };
    }

    private sealed class NoOpClientCallbacks : IClientCallbacks
    {
        public static readonly NoOpClientCallbacks Instance = new();

        public void ResetCheckpointGlow() { }
        public void UpdateCheckpointGlow(ushort currentCheckpoint, bool isFinish) { }
        public IClientCarCallbacks GetClientCarCallbacks(BackendCar car) => NoOpClientCarCallbacks.Instance;
    }

    private sealed class NoOpClientCarCallbacks : IClientCarCallbacks
    {
        public static readonly NoOpClientCarCallbacks Instance = new();

        public bool CastsShadow { get; set; }
        public bool? GetsShadowed { get; set; }
        public float? AlphaOverride { get; set; }
        public bool? Glow { get; set; }
        public bool? Finish { get; set; }
    }
}
