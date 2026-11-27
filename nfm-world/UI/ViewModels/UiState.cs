using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.UI.Routes;
using NFMWorldLibrary;
using NFMWorldLibrary.Util;
using SignalsDotnet;
using NFMWorldLibrary.Hud;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Plugins;

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
    public static MyContentState MyContent { get; } = new();

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
        public bool Spectating { get; set; }
        public bool ArrowLocked { get; set; }
        public float? SpeedFraction { get; set; }
        public float? TopSpeed { get; set; }
        public List<int> LapSplits { get; } = [];

        // The interface member is IReadOnlyList<int>; a List<int> property does not implement it
        // implicitly, so without this the default (empty) implementation was used and the
        // chronometer never had any rows.
        IReadOnlyList<int> IHudData.LapSplits => LapSplits;
        public float? ArrowDistanceMeters { get; set; }
        public string? MusicTrack { get; set; }
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

        /// <summary>Gamemode-declared HUD widgets, mounted by the skin at their named anchor.</summary>
        public IReadOnlyList<NFMWorldLibrary.Backend.GamemodeHudWidget> GamemodeWidgets { get; set; } = [];
        public Signal<int> Lap { get; } = new(1);
        public Signal<int> TotalLaps { get; } = new(3);
        public Signal<int> LapTimeMs { get; } = new(0);
        public Signal<int> Position { get; } = new(1);
        public Signal<int> TotalRacers { get; } = new(1);
        public Signal<int> CountdownTimer { get; } = new(0);
        public HudMessage? StatusMessage { get; set; }
        public HudMessage? StuntMessage { get; set; }
        public Signal<string?> StateText { get; } = new(null);
        public Signal<string?> ResultTitle { get; } = new(null);
        public Signal<DateTime?> StateTextEndsAt { get; } = new(null);
        public Signal<bool> Finished { get; } = new(false);
        public Signal<bool> ClientWon { get; } = new(false);
        public Signal<bool> ClientLost { get; } = new(false);
        public Signal<bool> Paused { get; } = new(false);

        public int SpeedKmh => float.IsFinite(Speed.Value)
            ? Math.Max(0, (int)Math.Floor(Speed.Value * 1.4 * 21 * 3600 / 100000 + 0.5))
            : 0;

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
        bool IHudData.Spectating => Spectating;
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
                    ? ""
                    : StateText.Value ?? "";
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
                    ResultTitle.Value = hud.ResultTitle;
                    StateTextEndsAt.Value = hud.StateTextEndsAt;
                    Finished.Value = hud.Finished;
                    ClientWon.Value = hud.ClientWon;
                    ClientLost.Value = hud.ClientLost;
                    StatusMessage = hud.StatusMessage;
                    StuntMessage = hud.StuntMessage;
                    Paused.Value = paused;
                }
            );
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

    /// <summary>The lobby's three full-area pages. The room players rail is always beside them.</summary>
    public enum LobbyPage
    {
        Room,
        Create,
        Match
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

        /// <summary>The row the player has highlighted.</summary>
        public Signal<uint?> SelectedSessionId { get; } = new(null);

        /// <summary>Which full-area page is showing: the room, the create flow or a game.</summary>
        public Signal<LobbyPage> Page { get; } = new(LobbyPage.Room);

        /// <summary>The game the match view is showing, or null.</summary>
        public Signal<uint?> ViewedSessionId { get; } = new(null);

        /// <summary>
        /// Unix ms updated a few times a second while a finished race is lingering, so a
        /// countdown can tick without the server broadcasting the whole lobby every second.
        /// </summary>
        public Signal<long> NowUnixMs { get; } = new(0);

        public Signal<string> SelectedStage { get; } = new("");

        /// <summary>
        /// The create dialog's stage list. The phase fills it when the dialog opens and owns the
        /// archive search; the view only renders it, so LOCAL and ONLINE are the same list.
        /// </summary>
        public StageSelectState CreateStages { get; } = new();
        public Signal<string> SelectedGamemode { get; } = new(NFMWorldLibrary.Multiplayer.DefaultGamemodes.PvP);
        public Signal<int> MaxPlayers { get; } = new(4);

        /// <summary>
        /// The selected gamemode's config schema, read by the phase. The view renders these
        /// records rather than reading the plugin itself, which loads an assembly.
        /// </summary>
        public Signal<IReadOnlyList<NFMWorldLibrary.Plugins.GamemodeConfigField>> GamemodeFields { get; } =
            new([]);

        /// <summary>Bots reserving slots within the lobby's total capacity.</summary>
        public Signal<int> BotCount { get; } = new(0);

        /// <summary>Gamemode config values chosen in the create dialog, keyed by property name.</summary>
        public Signal<IReadOnlyDictionary<string, string>> GamemodeConfig { get; } = new(new Dictionary<string, string>());

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
            Page.Value = LobbyPage.Room;
            ViewedSessionId.Value = null;
            NowUnixMs.Value = 0;
            CreateStages.Reset();
            BotCount.Value = 0;
            SelectedGamemode.Value = NFMWorldLibrary.Multiplayer.DefaultGamemodes.PvP;
            GamemodeFields.Value = [];
            GamemodeConfig.Value = new Dictionary<string, string>();
        }
    }

    public sealed record LobbyPlayer(
        Guid Id,
        string Name,
        string Car,
        Color Color,
        bool IsSelf,
        IReadOnlyList<Color>? CarColors = null,
        NFMWorldLibrary.Multiplayer.UserRole Role = NFMWorldLibrary.Multiplayer.UserRole.User);

    public sealed record LobbySession(
        uint Id,
        string CreatorName,
        string StageName,
        int PlayerCount,
        int MaxPlayers,
        SessionState State,
        bool IsMember,
        bool IsCreator,
        string Gamemode,
        string GamemodeLabel,
        IReadOnlyList<LobbySessionSetting> Settings,
        IReadOnlyList<LobbySessionSlot> Slots,
        IReadOnlyList<LobbyStanding> LiveStandings,
        IReadOnlyList<LobbyStanding> FinalStandings,
        long ClosesAtUnixMs,
        bool RequiresInvite = false
    )
    {
        public int EmptySlots => Math.Max(0, MaxPlayers - PlayerCount);
        public bool CanManage => IsCreator && IsMember && State == SessionState.NotStarted;
    }

    /// <summary>One row of the match lobby's live or final order.</summary>
    public sealed record LobbyStanding(
        Guid PlayerId,
        string Name,
        int Position,
        int Lap,
        bool Wasted,
        bool Dnf,
        int TotalLaps,
        byte? PowerPercent,
        byte? DamagePercent
    );

    /// <summary>One gamemode option as the match view shows it, already labelled.</summary>
    public sealed record LobbySessionSetting(
        string Label,
        string Value,
        GamemodeConfigField? Field = null,
        string? RawValue = null
    );

    /// <summary>
    /// One slot in a game. Empty slots are kept so the match view draws the same grid the
    /// lobby reserves, like the original game's waiting room.
    /// </summary>
    public sealed record LobbySessionSlot(
        byte Slot,
        string Name,
        string Car,
        Color Color,
        IReadOnlyList<Color>? CarColors,
        bool IsBot,
        bool IsEmpty,
        bool IsSelf,
        Guid PlayerId = default
    );

    /// <summary>
    /// Id keys the row: two identical lines from one sender are still two lines.
    /// <see cref="SessionId"/> is zero for room chat and the game's id for match chat.
    /// </summary>
    public sealed record LobbyChatLine(
        long Id,
        uint SessionId,
        Guid SenderId,
        string Sender,
        string Message,
        Color Color,
        bool IsSystem
    );

    /// <summary>Garage car list and the selected car's stats.</summary>
    public sealed class GarageState
    {
        public Signal<IReadOnlyList<CarCollectionViewModel>> Collections { get; } = new([]);
        public Signal<CarStatsViewModel?> CurrentCar { get; } = new(null);

        /// <summary>The picker tabs: each installed collection, then the archive.</summary>
        public Signal<IReadOnlyList<string>> Categories { get; } = new([]);

        /// <summary>The active picker tab.</summary>
        public Signal<string> Category { get; } = new(nameof(NFMWorldLibrary.Collection.NFMM));

        /// <summary>True while the active tab is the archive rather than an installed collection.</summary>
        public Signal<bool> OnlineSource { get; } = new(false);

        /// <summary>Progress, failure or eligibility note shown under the search box.</summary>
        public Signal<string> OnlineStatus { get; } = new("");

        /// <summary>Which garage submenu is showing.</summary>
        public Signal<GaragePage> Page { get; } = new(GaragePage.Home);

        /// <summary>The car filter. Kept here so the list and the keyboard walk the same rows.</summary>
        public Signal<string> SearchQuery { get; } = new("");

        /// <summary>Highlighted row on <see cref="GaragePage.Home"/>.</summary>
        public Signal<int> HomeCursor { get; } = new(0);

        /// <summary>The colour being edited on <see cref="GaragePage.Colors"/>.</summary>
        public Signal<int> ActiveColorSlot { get; } = new(0);

        /// <summary>0 = hue, 1 = saturation, 2 = brightness.</summary>
        public Signal<int> ActiveColorChannel { get; } = new(0);

        /// <summary>Editable colour slots of the selected car. Replaced only when the car changes.</summary>
        public Signal<IReadOnlyList<GarageColorSlot>> ColorSlots { get; } = new([]);
        public Signal<IReadOnlyList<GarageColorSlot>> WheelColorSlots { get; } = new([]);
        public Signal<bool> WheelsTab { get; } = new(false);
        public Signal<bool> HasCustomWheels { get; } = new(false);
        public Signal<bool> OriginalWheels { get; } = new(false);
        public Signal<bool> CustomizeTabsFocused { get; } = new(false);
        public bool HasBodyColors => ColorSlots.Value.Count > 0;
        public bool EditingWheels => !HasBodyColors || WheelsTab.Value;
        public IReadOnlyList<GarageColorSlot> EditingSlots => EditingWheels ? WheelColorSlots.Value : ColorSlots.Value;
    }

    /// <summary>
    /// MY CONTENT: the signed-in account's own uploads, which the archive lists separately from
    /// public content because a hidden or taken-down row is still the owner's to edit.
    /// </summary>
    public sealed class MyContentState
    {
        public Signal<IReadOnlyList<OwnedAsset>> Items { get; } = new([]);

        /// <summary>Highlighted row; the keyboard and the edit/delete actions act on it.</summary>
        public Signal<int> Cursor { get; } = new(0);

        /// <summary>Progress or failure note above the list.</summary>
        public Signal<string> Status { get; } = new("");

        /// <summary>True while a list, save, replace or delete is in flight.</summary>
        public Signal<bool> Busy { get; } = new(false);

        /// <summary>The row being renamed, or null when the editor is closed.</summary>
        public Signal<OwnedAsset?> Editing { get; } = new(null);

        public Signal<string> EditName { get; } = new("");
        public Signal<string> EditDescription { get; } = new("");

        /// <summary>Whether the edited row is listed publicly.</summary>
        public Signal<bool> EditPublic { get; } = new(true);

        public OwnedAsset? Selected => Items.Value.Count == 0
            ? null
            : Items.Value[Math.Clamp(Cursor.Value, 0, Items.Value.Count - 1)];

        public void Reset()
        {
            Items.Value = [];
            Cursor.Value = 0;
            Status.Value = "";
            Busy.Value = false;
            Editing.Value = null;
            EditName.Value = "";
            EditDescription.Value = "";
            EditPublic.Value = true;
        }
    }

    /// <summary>
    /// One editable colour slot. The values are signals rather than fields so a slider
    /// updates without remounting the slot's row.
    /// </summary>
    public sealed class GarageColorSlot : HsbColorState
    {
        public int Slot { get; }
        public string Name { get; }

        public GarageColorSlot(int slot, Color3 color, string? name = null) : base(color, CarPaint.MinimumChannel)
        {
            Slot = slot;
            Name = name ?? CarPaint.SlotName(slot);
        }
    }
}

/// <summary>The garage's pages: the menu, the car browser and the paint editor.</summary>
public enum GaragePage
{
    Home,
    Cars,
    Colors
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
    double Abing
)
{
    /// <summary>The archive account that published a public car; null for a local one.</summary>
    public string? Author { get; init; }
    public IReadOnlyList<string>? Tags { get; init; }
    public string? Class { get; init; }
}

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
