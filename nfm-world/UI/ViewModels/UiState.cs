using NFMWorld.DriverInterface;
using NFMWorldLibrary;
using SignalsDotnet;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Multiplayer;

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
    public static LobbyState Lobby { get; } = new();

    /// <summary>Per-frame HUD values, written by the race phase.</summary>
    public sealed class RaceState : IHudData
    {
        public Signal<string> Skin { get; } = new(HudSkinId.Default);
        /// <summary>Bumped once the plain-list presentation data (<see cref="Racers"/> etc.) has been rewritten for the frame.</summary>
        public Signal<int> PresentationRevision { get; } = new(0);
        public Signal<Color3> StageSnap { get; } = new(default(Color3));
        public bool TimeTrial { get; set; }
        public bool IsPreview { get; set; }
        public int Wasted { get; set; }
        public int Opponents { get; set; }
        public float? ArrowAngle { get; set; }
        public string ArrowTarget { get; set; } = "";
        public bool ArrowAtCars { get; set; }
        public bool ArrowLocked { get; set; }
        public List<HudRacer> Racers { get; } = [];
        public List<HudRacerMarker> Markers { get; } = [];
        public (float X, float Y, float Z)? ClientPosition { get; set; }
        public float ClientHeading { get; set; }
        public (float X, float Z) TrackCenter { get; set; }
        public (float X, float Z)? TargetCheckpoint { get; set; }
        public List<(float X, float Z)> Checkpoints { get; } = [];
        /// <summary>Live camera view-projection, for projecting name tags onto the screen.</summary>
        public Matrix ViewProjection { get; set; } = Matrix.Identity;
        /// <summary>The baked top-down stage image, once <see cref="NFMWorld.StageMinimap"/> has one.</summary>
        public NFMWorld.DriverInterface.DriverInterface.IImage? MapImage { get; set; }
        public (float MinX, float MaxZ, float UnitsPerPixel) MapFrame { get; set; }
        public Signal<float> Speed { get; } = new(0);
        public Signal<float> Power { get; } = new(1);
        public Signal<float> Damage { get; } = new(0);
        public Signal<int> Lap { get; } = new(1);
        public Signal<int> TotalLaps { get; } = new(3);
        public Signal<int> LapTimeMs { get; } = new(0);
        public Signal<int> Position { get; } = new(1);
        public Signal<int> TotalRacers { get; } = new(1);
        public Signal<int> CountdownTimer { get; } = new(0);
        public HudMessage? StatusMessage { get; set; }
        public HudMessage? StuntMessage { get; set; }
        public Signal<string?> StateText { get; } = new(null);
        public Signal<DateTime?> StateTextEndsAt { get; } = new(null);
        public Signal<bool> Paused { get; } = new(false);

        public int SpeedKmh => float.IsFinite(Speed.Value)
            ? Math.Max(0, (int)Math.Floor(Speed.Value * 1.4 * 21 * 3600 / 100000 + 0.5)) : 0;
        public float PowerFraction => Fraction(Power.Value / 0.98f);
        public float DamageFraction => Fraction(Damage.Value);
        int IHudData.Lap => Lap.Value;
        int IHudData.TotalLaps => TotalLaps.Value;
        int IHudData.LapTimeMs => LapTimeMs.Value;
        int IHudData.Position => Position.Value;
        int IHudData.TotalRacers => TotalRacers.Value;
        public int Countdown => CountdownTimer.Value;
        bool IHudData.Paused => Paused.Value;
        bool IHudData.ArrowLocked => ArrowLocked;
        IReadOnlyList<HudRacer> IHudData.Racers => Racers;
        (float X, float Y, float Z)? IHudData.ClientPosition => ClientPosition;
        float IHudData.ClientHeading => ClientHeading;
        (float X, float Z) IHudData.TrackCenter => TrackCenter;
        (float X, float Z)? IHudData.TargetCheckpoint => TargetCheckpoint;
        IReadOnlyList<(float X, float Z)> IHudData.Checkpoints => Checkpoints;
        IReadOnlyList<HudRacerMarker> IHudData.Markers => Markers;
        Matrix IHudData.ViewProjection => ViewProjection;
        object? IHudData.MapImage => MapImage;
        (float MinX, float MaxZ, float UnitsPerPixel) IHudData.MapFrame => MapFrame;
        public string Message
        {
            get
            {
                _ = LapTimeMs.Value;
                if (CountdownTimer.Value > 0) return $"Starting in {CountdownTimer.Value}";
                return StateTextEndsAt.Value is { } endsAt && DateTime.UtcNow >= endsAt
                    ? "" : StateText.Value ?? "";
            }
        }

        private static float Fraction(float value) => float.IsFinite(value) ? Math.Clamp(value, 0, 1) : 0;

        /// <summary>Writes the whole frame's HUD state as one flush.</summary>
        public void Update(HudStateData hud, bool paused)
        {
            Signal.Batch(() =>
            {
                StageSnap.Value = World.Snap;
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
                StatusMessage = hud.StatusMessage;
                StuntMessage = hud.StuntMessage;
                Paused.Value = paused;
            });
        }
    }

    /// <summary>Account state shown in the main menu.</summary>
    public sealed class MainMenuState
    {
        public Signal<bool> MenuVisible { get; } = new(false);
        public Signal<bool> GarageOpen { get; } = new(false);
        public Signal<bool> InputEnabled { get; } = new(false);
        public Signal<float> LogoOpacity { get; } = new(0);
        public Signal<bool> SceneryOnly { get; } = new(false);
        public Signal<float> PromptOpacity { get; } = new(0);
        public Signal<float> BlackOpacity { get; } = new(1);
        public Signal<string> StageName { get; } = new("");
        public Signal<string> PlayerName { get; } = new("");
        public Signal<bool> IsLoggedIn { get; } = new(false);

        public void Set(string? name, bool loggedIn)
        {
            PlayerName.Value = name ?? "";
            IsLoggedIn.Value = loggedIn;
        }
    }

    /// <summary>
    /// Lobby state. The phase writes these from <c>GameTick</c>; the view only reads them, so a
    /// lobby packet updates just the slots that changed.
    /// </summary>
    public sealed class LobbyState
    {
        public Signal<bool> Connected { get; } = new(false);
        /// <summary>Reconnection has given up; only leaving the lobby will help.</summary>
        public Signal<bool> Disconnected { get; } = new(false);
        public Signal<string> Status { get; } = new("");
        public Signal<IReadOnlyList<LobbyPlayer>> Players { get; } = new([]);
        public Signal<IReadOnlyList<LobbySession>> Sessions { get; } = new([]);
        public Signal<IReadOnlyList<LobbyChatLine>> Chat { get; } = new([]);

        /// <summary>The session the local player is in, or null.</summary>
        public Signal<LobbySession?> CurrentSession { get; } = new(null);

        /// <summary>The row the player has highlighted, which JOIN acts on.</summary>
        public Signal<uint?> SelectedSessionId { get; } = new(null);

        public Signal<bool> ShowCreateDialog { get; } = new(false);
        public Signal<string> SelectedStage { get; } = new("");
        public Signal<int> MaxPlayers { get; } = new(4);

        public void Reset()
        {
            Connected.Value = false;
            Disconnected.Value = false;
            Status.Value = "";
            Players.Value = [];
            Sessions.Value = [];
            Chat.Value = [];
            CurrentSession.Value = null;
            SelectedSessionId.Value = null;
            ShowCreateDialog.Value = false;
        }
    }

    public sealed record LobbyPlayer(Guid Id, string Name, string Car, Color Color, bool IsSelf);

    public sealed record LobbySession(
        uint Id, string CreatorName, string StageName, int PlayerCount, int MaxPlayers,
        SessionState State, bool IsMember, bool IsCreator);

    /// <summary>Id keys the row: two identical lines from one sender are still two lines.</summary>
    public sealed record LobbyChatLine(long Id, string Sender, string Message, Color Color, bool IsSystem);

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
