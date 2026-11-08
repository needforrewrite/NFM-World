namespace NFMWorld.Gameplay;

/// <summary>
/// Shared state passed between phases. Prefer passing data directly via events;
/// this exists only for cases where event wiring would be excessively indirect.
/// </summary>
public class PhaseSharedState
{
    /// <summary>
    /// The stage name selected in StageSelectPhase, for use by subsequent phases.
    /// </summary>
    public static string? SelectedStageName;
}
