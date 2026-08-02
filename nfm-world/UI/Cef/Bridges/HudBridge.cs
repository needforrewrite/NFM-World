using System.Text.Json;
using MemoryPack;
using NFMWorld.DriverInterface;

namespace NFMWorld.UI.Cef;

/// <summary>
/// Bridge for in-race HUD. Pushes HudState records each frame (60 fps).
/// Also handles pause-menu interactions (resume, restart, quit) and hosts
/// a <see cref="SettingsHandler"/> sub-handler for in-race settings access.
///
/// Input is disabled during normal racing (clicks pass through); it is
/// enabled on the CefRenderer directly by BaseRacePhase during pause.
/// </summary>
public sealed class HudBridge : PhaseBridge
{
    private readonly SettingsHandler _settings = new();

    public HudBridge() : base("race")
    {
        AddSubHandler(_settings);
        _settings.CloseRequested += () => SettingsCloseRequested?.Invoke();
    }

    public override bool EnableInput => false;

    /// <summary>
    /// The settings sub-handler. Exposed so the hosting phase can forward
    /// key events during key-rebinding capture.
    /// </summary>
    public SettingsHandler Settings => _settings;

    /// <summary>
    /// Whether the settings sub-view is currently open in the pause menu.
    /// When true, Escape should dismiss settings rather than resume the race.
    /// Set by JS via <c>callNfmw("settingsOpened"/"settingsClosed")</c>.
    /// </summary>
    public bool IsSettingsOpen { get; private set; }

    protected override void OnMessage(string type, JsonElement? args)
    {
        switch (type)
        {
            case "resume":
                ResumeRequested?.Invoke();
                break;
            case "restart":
                RestartRequested?.Invoke();
                break;
            case "quit":
                QuitRequested?.Invoke();
                break;
            case "settingsOpened":
                IsSettingsOpen = true;
                break;
            case "settingsClosed":
                IsSettingsOpen = false;
                break;
        }
    }

    /// <summary>
    /// Push the full HUD state to JS. Call every frame from GameTick().
    /// </summary>
    public void PushHudState(HudStateData state)
    {
        PushMemoryPack("hudState", state);
    }

    /// <summary>
    /// Push pause context (lap, position, stage name) to JS so the
    /// pause menu can display race summary information.
    /// Also signals the RaceHud to show the pause overlay.
    /// </summary>
    public void PushPauseState(int lap, int totalLaps, int position, int totalRacers, string stageName)
    {
        Push("pauseState", new
        {
            lap,
            totalLaps,
            position,
            totalRacers,
            stageName
        });
    }

    /// <summary>
    /// Push the paused/unpaused state to JS so the RaceHud can show/hide
    /// the pause menu overlay without a hash navigation.
    /// </summary>
    public void PushPausedEvent(bool paused)
    {
        Push("paused", paused);
    }

    // ── Events ────────────────────────────────────────────────────

    /// <summary>Fired when the user clicks Resume in the pause menu.</summary>
    public event Action? ResumeRequested;

    /// <summary>Fired when the user clicks Restart in the pause menu.</summary>
    public event Action? RestartRequested;

    /// <summary>Fired when the user clicks Quit in the pause menu.</summary>
    public event Action? QuitRequested;

    /// <summary>Fired when the user closes settings from the pause menu.</summary>
    public event Action? SettingsCloseRequested;
}
