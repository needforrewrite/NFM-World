using SignalsDotnet;

namespace NFMWorld.UI.ViewModels;

public sealed class RaceFinishState
{
    public const long ContinueDelayMs = 1000;
    private long? _finishedAt;
    public Signal<bool> Active { get; } = new(false);
    public Signal<bool> CanContinue { get; } = new(false);
    public Signal<bool> ShowSummary { get; } = new(false);
    public Signal<bool> ContinueSpectating { get; } = new(false);
    public Signal<bool> Won { get; } = new(false);
    public Signal<bool> Lost { get; } = new(false);
    public Signal<string> StageName { get; } = new("");
    public Signal<string> Description { get; } = new("");

    public void Observe(bool finished, long now, bool won, string stageName, string description, bool lost = true)
    {
        if (!finished)
        {
            Reset();
            return;
        }

        _finishedAt ??= now;
        Active.Value = true;
        CanContinue.Value = now - _finishedAt.Value >= ContinueDelayMs;
        Won.Value = won;
        Lost.Value = !won && lost;
        StageName.Value = stageName;
        Description.Value = description;
    }

    public bool TryShowSummary()
    {
        if (!Active.Value || !CanContinue.Value) return false;
        ShowSummary.Value = true;
        return true;
    }

    public void Reset()
    {
        _finishedAt = null;
        Active.Value = false;
        CanContinue.Value = false;
        ShowSummary.Value = false;
        ContinueSpectating.Value = false;
        Won.Value = false;
        Lost.Value = false;
        StageName.Value = "";
        Description.Value = "";
    }
}
