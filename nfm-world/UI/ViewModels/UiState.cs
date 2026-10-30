using NFMWorld.DriverInterface;
using NFMWorldLibrary;
using SignalsDotnet;

namespace NFMWorld.UI.ViewModels;

/// <summary>
/// Every signal the UI reads, one holder per route.
/// </summary>
/// <remarks>
/// Replaces the per-phase Lua bridges: a phase writes a signal directly instead of
/// serialising a payload into a push, and the view's per-slot effects do the rest.
/// One static instance because the renderer is a single tree - the routes are
/// mutually exclusive and never alive together.
/// </remarks>
public static class UiState
{
    public static MainMenuState MainMenu { get; } = new();
    public static GarageState Garage { get; } = new();
    public static RaceState Race { get; } = new();
    public static SettingsState Settings { get; } = new();

    /// <summary>Per-frame HUD values, written by the race phase.</summary>
    public sealed class RaceState
    {
        public Signal<float> Speed { get; } = new(0);
        public Signal<float> Power { get; } = new(1);
        public Signal<float> Damage { get; } = new(0);
        public Signal<int> Lap { get; } = new(1);
        public Signal<int> TotalLaps { get; } = new(3);
        public Signal<int> LapTimeMs { get; } = new(0);
        public Signal<int> Position { get; } = new(1);
        public Signal<int> TotalRacers { get; } = new(1);
        public Signal<int> CountdownTimer { get; } = new(0);
        public Signal<string?> StateText { get; } = new(null);
        public Signal<DateTime?> StateTextEndsAt { get; } = new(null);
        public Signal<bool> Paused { get; } = new(false);

        /// <summary>Writes the whole frame's HUD state as one flush.</summary>
        public void Update(HudStateData hud, bool paused)
        {
            Signal.Batch(() =>
            {
                Speed.Value = hud.Speed;
                Power.Value = hud.Power;
                Damage.Value = hud.Damage;
                Lap.Value = hud.Lap;
                TotalLaps.Value = hud.TotalLaps;
                LapTimeMs.Value = hud.LapTime;
                Position.Value = hud.Position;
                TotalRacers.Value = hud.TotalRacers;
                CountdownTimer.Value = hud.CountdownTimer;
                StateText.Value = hud.StateText;
                StateTextEndsAt.Value = hud.StateTextEndsAt;
                Paused.Value = paused;
            });
        }
    }

    /// <summary>Account state shown in the main menu.</summary>
    public sealed class MainMenuState
    {
        public Signal<string> PlayerName { get; } = new("");
        public Signal<bool> IsLoggedIn { get; } = new(false);

        public void Set(string? name, bool loggedIn)
        {
            PlayerName.Value = name ?? "";
            IsLoggedIn.Value = loggedIn;
        }
    }

    /// <summary>Garage car list and the selected car's stats.</summary>
    public sealed class GarageState
    {
        public Signal<IReadOnlyList<CarCollectionViewModel>> Collections { get; } = new([]);
        public Signal<string> CurrentCollection { get; } = new(nameof(NFMWorldLibrary.Collection.User));
        public Signal<CarStatsViewModel?> CurrentCar { get; } = new(null);
    }
}

/// <summary>Car stats shown in the garage.</summary>
public sealed record CarStatsViewModel(
    string FileName,
    string Name,
    string Collection,
    double TopSpeed,
    double Acceleration,
    double Handling,
    double PowerSave,
    double Strength,
    double MaxHealth,
    double Stunting,
    double Hypergliding,
    double Abing);

/// <summary>A named group of cars in the garage.</summary>
public sealed record CarCollectionViewModel(string Id, IReadOnlyList<CarStatsViewModel> Cars);

/// <summary>
/// Settings state, and the actions the settings view can take.
/// </summary>
/// <remarks>
/// The options come from <see cref="SettingsMenu"/> - the existing key-binding and
/// config registry - so this holds only what the view needs to render and what it
/// calls back into.
/// </remarks>
public sealed class SettingsState
{
    public Signal<bool> Open { get; } = new(false);
    public Signal<bool> RequireRestart { get; } = new(false);

    /// <summary>The action whose key is being rebound, or null.</summary>
    public Signal<string?> CapturingAction { get; } = new(null);

    public Action? CloseRequested { get; set; }
    public Action? RestartConfirmed { get; set; }
    public Action? SaveConfig { get; set; }
    public Action<string, object?>? ApplySetting { get; set; }
    public Action? ResetDefaults { get; set; }
}
