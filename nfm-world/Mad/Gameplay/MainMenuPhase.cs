using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.Reactor.Sx;
using NFMWorld.UI;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Radpack;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public class MainMenuPhase : BaseStageRenderingPhase
{
    // The intro is a beat, not a wait: the scene is already up when the clock starts (GameTick
    // returns until the stage is adopted), so this is black-to-prompt time and nothing else.
    private const float IntroFadeStart = 1;
    private const float LogoFadeStart = 2f;
    private const float LogoFadeDuration = 1.4f;
    private const float TransitionDuration = 2f;
    private const float PromptFadeStart = LogoFadeStart + LogoFadeDuration + 0.2f;
    private readonly MenuCameraDirector _cameraDirector = new();
    private readonly SettingsCameraPreview _settingsCamera = new();
    private readonly IImage _logo;
    private float _introTime;
    private float _introVisibility = 1;
    private float _transitionTime = -1;
    private float _menuTime;
    private bool _menuVisible;
    private BackendCar? _menuCar;
    private GarageController? _garage;
    private ReplayBrowserController? _replays;
    private readonly SignalsDotnet.Signal<bool> _replaysOpen = new(false);
    private MyContentController? _myContent;
    private readonly SignalsDotnet.Signal<bool> _myContentOpen = new(false);
    private BackendCar? _garageEntryCar;
    private float _garageBlend;
    private readonly GarageCameraControls _garageCamera = new();
    private MenuAttractDrive? _attractDrive;
    private int _attractShot = -1;
    private bool _transitionToMenu = true;
    private bool _transitionSwitched;
    private string _appliedMenuStage;
    private string _appliedMenuMusic;
    private bool _appliedRemasteredMusic;
    private (string Host, ushort Port, bool Insecure)? _lobbyAfterSignIn;
    private bool _warnedDeletion;

    public override bool EnableUiInput => CurrentStage is not null
                                          && _menuVisible
                                          && (_transitionTime < 0 || (_transitionToMenu && _transitionSwitched));

    private static string ResolveMenuStage(string lastPlayed)
        => MenuPreferences.ResolveStage(
            TheVFS.VFS,
            SettingsMenu.MenuStage,
            RadpackReader.IsArchiveId(lastPlayed) && !TheVFS.VFS.FileExists(lastPlayed)
                ? MenuHistory.DefaultStage : lastPlayed
        );

    public MainMenuPhase(string stageName) : base(ResolveMenuStage(stageName))
    {
        _appliedMenuStage = SettingsMenu.MenuStage;
        _appliedMenuMusic = SettingsMenu.MenuMusic;
        _appliedRemasteredMusic = GameSparker.UseRemasteredMusic;
        _logo = G.LoadImage("data/images/logo.svg");
        UiState.MainMenu.MenuVisible.Value = false;
        UiState.MainMenu.SceneryOnly.Value = false;
        UiState.MainMenu.GarageOpen.Value = false;
        UiState.MainMenu.InputEnabled.Value = false;
        UiState.MainMenu.BlackOpacity.Value = 1;
        UiState.MainMenu.LogoOpacity.Value = 0;
        UiState.MainMenu.PromptOpacity.Value = 0;
        // Write initial account state if available
        var account = GameSparker.AccountManager.LoggedIn
            ? GameSparker.AccountManager.ActiveAccount
            : null;
        UiState.MainMenu.Set(account?.Username, account != null);

        // Subscribe to account changes via the CLR event directly
        GameSparker.AccountManager.ActiveAccountChanged += OnActiveAccountChanged;
    }

    /// <summary>The menu's items, wired straight to this phase's handlers.</summary>
    public override Node BuildUi() => MainMenuIntroView.Build(
        _logo,
        logo => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, width: 100.Percent(), height: 100.Percent()),
            children:
            [
                MainMenuView.Build(
                    new MainMenuView.Actions(
                        Play: OnFreePlayClicked,
                        Garage: OnGarageClicked,
                        ModelEditor: GameSparker.StartModelViewer,
                        StageEditor: GameSparker.StartStageEditor,
                        TimeTrials: OnTTClicked,
                        Rocket: OnRocketClicked,
                        Unavailable: _ => GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable."),
                        Replays: OpenReplays,
                        Account: AccountView.Show,
                        Multiplayer: OnMultiplayerClicked,
                        MyContent: OpenMyContent,
                        CheckForUpdates: GameSparker.CheckForDesktopUpdates
                    ),
                    logo,
                    GameSparker.ExeVersion
                ),
                AccountView.Build(
                    new AccountView.Actions(
                        SignIn: OnSignIn,
                        Register: OnRegister,
                        SignOut: OnSignOut,
                        Close: CloseAccount,
                        ManageAccount: OnManageAccount
                    ),
                    () => GameSparker.AccountManager.LoggedIn,
                    () => GameSparker.AccountManager.ActiveAccount?.Username ?? "",
                    () => GameSparker.AccountManager.ActiveAccount?.DeletionPendingUntil is { } d
                        ? d.UtcDateTime.ToString("yyyy-MM-dd")
                        : ""
                )
            ]
        ),
        () => _garage!.BuildUi(),
        () => _replaysOpen.Value,
        () => _replays!.BuildUi(),
        () => _myContentOpen.Value,
        () => _myContent!.BuildUi()
    );

    private void OnSignIn(string username, string password)
    {
        AccountView.SetBusy();
        _ = SignInAsync(username, password);
    }

    private async Task SignInAsync(string username, string password)
    {
        var result = await GameSparker.AccountManager.LogInToLocalAccount(username, password);
        AccountView.SetStatus(result.Success ? "Signed in." : "!" + result.Message, isError: !result.Success);
        if (result.Success)
            AccountView.Hide();
    }

    private void OnRegister()
    {
        var result = GameSparker.AccountManager.OpenRegistration();
        AccountView.SetStatus((result.Success ? "" : "!") + result.Message, isError: !result.Success);
    }

    private void OnManageAccount()
    {
        var result = GameSparker.AccountManager.OpenAccountSecurity();
        AccountView.SetStatus((result.Success ? "" : "!") + result.Message, isError: !result.Success);
    }

    private void CloseAccount()
    {
        _lobbyAfterSignIn = null;
        AccountView.Hide();
    }

    public void RequireLobbySignIn(string host, ushort port, bool insecure)
    {
        _lobbyAfterSignIn = (host, port, insecure);
        AccountView.Show();
        AccountView.SetStatus("Sign in to play multiplayer. New accounts can be registered in your browser.", isError: false);
    }

    private void OnSignOut()
    {
        GameSparker.AccountManager.LogOut();
        AccountView.Hide();
    }

    public override void Enter()
    {
        // The page stack and settings overlay live in static signals, so they survive
        // Exit(). Without this, quitting a race from a submenu lands back on that submenu
        // rather than the root.
        if (_replays is null)
            MainMenuView.Reset();

        ApplyMenuPreferences(checkHistory: true);
        if (CurrentStage is not null)
            CurrentStage.CarSoundsEnabled = false;

        if (_menuVisible)
        {
            SetupMenuCar();
            if (_menuCar != null)
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, true);
        }

        if (CurrentStage is not null)
            UiState.MainMenu.StageName.Value = CurrentStage.Backend.Name;

        _ = GameSparker.AccountManager.RefreshAsync();
        WarnIfDeletionPending();
        base.Enter();
    }

    internal bool TryTakeLastPlayedRace(RacePhase race, bool enteringMenu)
    {
        if (SettingsMenu.MenuStage != MenuPreferences.LastPlayedStage
            || race.CurrentStage is not { } stage
            || stage.Backend.Path != MenuHistory.Stage
            || stage.Backend.Path != race.StageName)

            return false;

        var takeMusic = MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) is null;
        race.ExitForMenu(keepMusicPlaying: enteringMenu && takeMusic);

        OnStageReplacing();
        UnloadStage();

        if (takeMusic)
        {
            UnloadStageMusic();
            _appliedMenuMusic = SettingsMenu.MenuMusic;
        }

        StageName = race.StageName;
        TakeStageFrom(race, takeMusic);
        OnStageLoaded();
        ApplyMenuPreferences();
        LoadStageMusic();

        return true;
    }

    private void SetupMenuCar()
    {
        var car = MenuHistory.Car;

        if (car is not null && !BackendCar.IsDrivable(car))
        {
            // A remembered user model can be an unfinished editor export; the physics
            // cannot build it, so fall back instead of taking the menu down.
            Logging.Warning($"Remembered car '{car.FileName}' is not drivable; falling back.");
            car = BackendGameSparker.GetCar(MenuHistory.DefaultCar).Rad;
            if (car is not null && !BackendCar.IsDrivable(car))
                car = BackendGameSparker.cars.Values.SelectMany(cars => cars).FirstOrDefault(BackendCar.IsDrivable);
        }

        if (car is not { } chosen)

            return;
        CarsInRace.Clear();
        _menuCar = new BackendCar(chosen, 0, 0, 0, true);
        CarsInRace.Add(_menuCar);
    }

    protected override (string Path, double Tempo, double Frequency)? GetMusicTrack()
        => MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) is { } path
            ? (path, 1d, 1d)
            : base.GetMusicTrack();

    private void ApplyMenuPreferences(bool checkHistory = false)
    {
        if (CurrentStage is null && IsStageLoading)

            return;
        var selection = SettingsMenu.MenuStage == MenuPreferences.LastPlayedStage
            ? MenuHistory.Stage : SettingsMenu.MenuStage;
        var stageChanged = _appliedMenuStage != SettingsMenu.MenuStage;
        var musicChanged = _appliedMenuMusic != SettingsMenu.MenuMusic
                           || (_appliedRemasteredMusic != GameSparker.UseRemasteredMusic
                               && MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) == null);
        _appliedRemasteredMusic = GameSparker.UseRemasteredMusic;
        var stageName = RadpackReader.IsArchiveId(selection) ? selection : ResolveMenuStage(MenuHistory.Stage);

        if (!stageChanged && !musicChanged && !checkHistory && StageName == stageName)

            return;

        if (StageName != stageName)
        {
            LoadStage(stageName, reloadIfLoaded: true);
        }
        else if (musicChanged)
        {
            LoadStageMusic(reloadIfLoaded: true);
        }

        _appliedMenuStage = SettingsMenu.MenuStage;
        _appliedMenuMusic = SettingsMenu.MenuMusic;
    }

    protected override void OnStageLoaded()
    {
        _settingsCamera.Reset();
        CurrentStage!.CarSoundsEnabled = false;
        _cameraDirector.Reset(Camera, CurrentStage.Backend);
        CurrentStage.FadeFromOverride = _cameraDirector.ViewDistance;
        _attractShot = -1;

        if (_menuVisible && _menuCar != null)
        {
            CurrentStage.GetCarVisual(_menuCar);
            _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, true);
        }

        UiState.MainMenu.StageName.Value = CurrentStage.Backend.Name;
    }

    protected override void OnStageReplacing() => StopAttractDrive();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            CloseReplays();
            CloseMyContent();
            StopAttractDrive();
            GameSparker.AccountManager.ActiveAccountChanged -= OnActiveAccountChanged;
            (_logo as IDisposable)?.Dispose();
        }

        base.Dispose(disposing);
    }

    private void OnActiveAccountChanged(Account? account)
    {
        UiState.MainMenu.Set(account?.Username, account != null);
        WarnIfDeletionPending();
    }

    private void WarnIfDeletionPending()
    {
        if (_warnedDeletion)

            return;

        var account = GameSparker.AccountManager.ActiveAccount;

        if (account?.DeletionPendingUntil is not { } until)

            return;

        _warnedDeletion = true;
        GameSparker.MessageWindow.ShowMessage(
            "Account deletion scheduled",
            $"Your account is scheduled for deletion on {until.UtcDateTime:yyyy-MM-dd}. "
            + "Manage it from the account security page in your browser."
        );
    }

    private void OnMultiplayerClicked()
    {
        if (!LobbyConnection.TryOpen(out var error))
        {
            GameSparker.MessageWindow.ShowMessage("Multiplayer unavailable", error);
        }
    }

    private void OnFreePlayClicked()
    {
        StageSelectPhase ssp;

        try
        {
            ssp = new StageSelectPhase();
        }
        catch (DirectoryNotFoundException ex)
        {
            Logging.Error(ex.Message);
            GameSparker.MessageWindow.ShowMessage("Stages unavailable", ex.Message);

            return;
        }

        ssp.StageSelected += (sender, stageName) =>
        {
            PhaseSharedState.SelectedStageName = stageName;

            OpenQuickCarSelect(ssp, stageName, timeTrial: false);
        };

        GameSparker.PushPhase(ssp, PhaseManager.Groups.Event);
    }

    private void OpenReplays()
    {
        if (_replays is not null)

            return;
        _replays = new ReplayBrowserController(CloseReplays);
        _replaysOpen.Value = true;
    }

    private void CloseReplays()
    {
        _replaysOpen.Value = false;
        _replays?.Dispose();
        _replays = null;
    }

    private void OpenMyContent()
    {
        if (_myContent is not null)

            return;
        _myContent = new MyContentController(CloseMyContent);
        _myContentOpen.Value = true;
    }

    private void CloseMyContent()
    {
        _myContentOpen.Value = false;
        _myContent?.Dispose();
        _myContent = null;
    }

    private void OnGarageClicked()
    {
        if (_garage != null)

            return;
        if (_menuCar == null)
            SetupMenuCar();

        if (_menuCar == null)
        {
            GameSparker.MessageWindow.ShowMessage("Cars unavailable", "No playable cars are installed.");

            return;
        }

        _garageEntryCar = _menuCar;
        if (_garageBlend == 0)
            _garageCamera.Reset();
        _garage = new GarageController(
            CarsInRace,
            _menuCar,
            car => CurrentStage?.GetCarVisual(car)
        );
        _garage.CarSelected += (_, _) => _garageEntryCar = _garage!.CurrentCar;
        _garage.CarSelectionCancelled += (_, _) => CloseGarage(false);
        _garage.Enter();
        UiState.MainMenu.GarageOpen.Value = true;
    }

    private void CloseGarage(bool commit)
    {
        _garageCamera.Release(MouseButton.Primary);
        _menuCar = commit ? _garage?.CurrentCar : _garageEntryCar;
        if (_menuCar != null && CarsInRace[0] != _menuCar)
            CarsInRace[0] = _menuCar;
        _garage?.End();
        _garage = null;
        _garageEntryCar = null;
        UiState.MainMenu.GarageOpen.Value = false;
    }

    private void OnTTClicked()
    {
        StageSelectPhase ssp;

        try
        {
            ssp = new StageSelectPhase();
        }
        catch (DirectoryNotFoundException ex)
        {
            Logging.Error(ex.Message);
            GameSparker.MessageWindow.ShowMessage("Stages unavailable", ex.Message);

            return;
        }

        ssp.StageSelected += (sender, stageName) =>
        {
            if (ssp.CurrentStage?.Backend.Checkpoints.Count is not > 0)
            {
                GameSparker.MessageWindow.ShowMessage(
                    "Time trial unavailable",
                    "This stage has no checkpoints. Choose a stage with checkpoints to start a time trial."
                );

                return;
            }

            PhaseSharedState.SelectedStageName = stageName;

            OpenQuickCarSelect(ssp, stageName, timeTrial: true);
        };
        GameSparker.PushPhase(ssp, PhaseManager.Groups.Event);
    }

    private void OnRocketClicked()
    {
        StageSelectPhase ssp;

        try
        {
            ssp = new StageSelectPhase();
        }
        catch (DirectoryNotFoundException ex)
        {
            Logging.Error(ex.Message);
            GameSparker.MessageWindow.ShowMessage("Stages unavailable", ex.Message);

            return;
        }

        ssp.StageSelected += (sender, stageName) =>
        {
            PhaseSharedState.SelectedStageName = stageName;
            OpenQuickCarSelect(ssp, stageName, timeTrial: false, rocket: true);
        };

        GameSparker.PushPhase(ssp, PhaseManager.Groups.Event);
    }

    private static void OpenQuickCarSelect(StageSelectPhase source, string stageName, bool timeTrial, bool rocket = false)
    {
        if (MenuHistory.Car is not { } car || !BackendCar.IsDrivable(car))
        {
            GameSparker.MessageWindow.ShowMessage("Cars unavailable", "No playable cars are installed.");

            return;
        }

        var picker = new GaragePhase(source, quickSelect: true);
        picker.CarSelectionCancelled += (_, _) =>
        {
            source.TakeStageFrom(picker, takeMusic: false);
            source.CurrentStage!.FadeFromOverride = 9999999;
            GameSparker.PopPhase();
        };
        picker.CarSelected += (_, _) =>
        {
            var identity = MenuHistory.LastPlayed.Car;
            var factory = timeTrial
                ? GamemodeFactory.Create("nfmm/timetrial")
                : rocket
                    ? GamemodeFactory.Create("nfmm/rocket")
                    : GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object> { ["constraint"] = "both" });
            ClientSidePlayerInfo[] players = timeTrial
                ? [new ClientSidePlayerInfo("MadPlayer", identity, default, false, true)
                {
                    CarColors = CarPaint.SavedColors(identity), Wheels = CarWheels.Get(identity)
                }]
                : FreePlayRoster.Create(identity, rocket: rocket);
            LocalRaceHost host;

            try
            {
                host = LocalRaceHost.CreateDeferred(stageName, factory, new ClientGamemodeParameters { Players = players });
            }
            catch
            {
                factory.End();

                throw;
            }

            var race = new RacePhase(stageName, factory, players, host, stageSource: picker);
            race.Exited += (_, _) => GameSparker.PopGroup(PhaseManager.Groups.Event);
            GameSparker.Phases.ReplaceGroup(race, PhaseManager.Groups.Event);
        };
        GameSparker.PushPhase(picker, PhaseManager.Groups.Event);
    }

    public override void GameTick()
    {
        if (_lobbyAfterSignIn is { } lobby && GameSparker.AccountManager.LoggedIn)
        {
            _lobbyAfterSignIn = null;
            if (!LobbyConnection.TryOpen(lobby.Host, lobby.Port, lobby.Insecure, out var error))
                GameSparker.MessageWindow.ShowMessage("Multiplayer unavailable", error);

            return;
        }

        const float seconds = 1f / Physics.TargetTps;
        _settingsCamera.RestoreNormal(Camera);
        ApplyMenuPreferences();

        if (CurrentStage is null)

            return;
        if (ReferenceEquals(GameSparker.CurrentPhase, this)
            && EnableUiInput
            && _transitionTime < 0
            && _garage is null
            &&
            !_replaysOpen.Value
            && !_myContentOpen.Value
            && !AccountView.IsOpen
            && !MainMenuView.HasBlockingOverlay)
            GameSparker.PumpDesktopUpdates?.Invoke();

        if (_menuVisible)
        {
            _menuTime += seconds;
            if (_garage != null)
                _garageCamera.Tick(seconds);
            _garageBlend = Math.Clamp(_garageBlend + (_garage != null ? seconds : -seconds) / 1.1f, 0, 1);
            _menuCar = _garage?.CurrentCar ?? _menuCar;

            if (_menuCar != null)
            {
                _cameraDirector.ShowCar(
                    Camera,
                    _menuCar,
                    _menuTime,
                    garageBlend: Smooth(_garageBlend),
                    garageAngle: _garageCamera.Angle,
                    garageHeight: _garageCamera.Height,
                    garageFraming: MenuCameraDirector.GarageFraming
                );
                _settingsCamera.Update(Camera, _menuCar, NFMWorld.UI.Components.SettingsView.CameraPreviewActive);

                if (NFMWorld.UI.Components.SettingsView.MenuHudPreviewData is { } preview)
                {
                    preview.Power.Value = (float)_menuCar.CarPhysics.Power / 100f;
                    preview.Damage.Value = _menuCar.Stats.Maxmag > 0
                        ? (float)_menuCar.CarPhysics.DamagePoints / _menuCar.Stats.Maxmag
                        : 0;
                    preview.Racers.Clear();
                    preview.Racers.Add(
                        new NFMWorldLibrary.Hud.HudRacer(
                            1,
                            _menuCar.Stats.Name,
                            preview.DamageFraction,
                            true,
                            _menuCar.Wasted
                        )
                    );
                }
            }
        }
        else
        {
            _settingsCamera.Reset();
            _introTime += seconds;
            _cameraDirector.Tick(Camera, CurrentStage!.Backend, seconds);

            if (_attractShot != _cameraDirector.ShotId)
            {
                _attractShot = _cameraDirector.ShotId;
                StopAttractDrive();
                if (_cameraDirector.CarActionRoad is { } road)
                    _attractDrive = new MenuAttractDrive(CurrentStage, CarsInRace, road);
            }

            _attractDrive?.Tick();
        }

        if (_transitionTime >= 0)
        {
            _transitionTime += seconds;

            if (_transitionTime >= 0.8f && !_transitionSwitched)
            {
                _transitionSwitched = true;
                StopAttractDrive();
                _menuVisible = _transitionToMenu;

                if (_menuVisible)
                {
                    _menuTime = 0;
                    SetupMenuCar();
                    if (_menuCar != null)
                        _cameraDirector.ShowCar(Camera, _menuCar, 0, true);
                }
                else
                {
                    CarsInRace.Clear();
                    _menuCar = null;
                    _menuTime = 0;
                    _garageBlend = 0;
                    _introTime = LogoFadeStart;
                    _cameraDirector.Reset(Camera, CurrentStage!.Backend);
                }

                UiState.MainMenu.MenuVisible.Value = _menuVisible;
                MainMenuView.Reset();
            }

            UiState.MainMenu.BlackOpacity.Value = _transitionTime < 0.8f
                ? Smooth(_transitionTime / 0.8f)
                : 1 - Smooth((_transitionTime - 0.8f) / 1.2f);
            if (_transitionTime >= TransitionDuration)
                _transitionTime = -1;
        }
        else if (!_menuVisible)
        {
            // The curtain lifts across the whole pre-logo span, so backing out of the menu (which
            // resets the clock to LogoFadeStart) returns with no black left over.
            var blackFade = LogoFadeStart - IntroFadeStart;
            UiState.MainMenu.BlackOpacity.Value = Math.Max(
                1 - Smooth((_introTime - IntroFadeStart) / blackFade),
                _cameraDirector.Fade
            );
        }

        if (!_menuVisible)
        {
            _introVisibility = Math.Clamp(
                _introVisibility
                + (UiState.MainMenu.SceneryOnly.Value ? -seconds : seconds) / 0.6f,
                0,
                1
            );
            var visibility = Smooth(_introVisibility);
            UiState.MainMenu.LogoOpacity.Value = Smooth((_introTime - LogoFadeStart) / LogoFadeDuration) * visibility;
            UiState.MainMenu.PromptOpacity.Value = Smooth((_introTime - PromptFadeStart) / 0.8f)
                                                   * visibility
                                                   * (0.82f
                                                      + 0.18f * MathF.Cos((_introTime - PromptFadeStart) * MathF.Tau / 1.5f));
        }

        CurrentStage!.FadeFromOverride = _menuVisible ? null : _cameraDirector.ViewDistance;
        UiState.MainMenu.InputEnabled.Value = EnableUiInput;
        base.GameTick();
    }

    private void StopAttractDrive()
    {
        _attractDrive?.Dispose();
        _attractDrive = null;
    }

    private void BeginTransition(bool toMenu)
    {
        if (_transitionTime >= 0)

            return;
        UiState.MainMenu.SceneryOnly.Value = false;
        _transitionToMenu = toMenu;
        _transitionSwitched = false;
        _transitionTime = 0;
    }

    private static float Smooth(float value)
    {
        value = Math.Clamp(value, 0, 1);

        return value * value * (3 - 2 * value);
    }

    public void NavigationKeyRepeated(Key key)
    {
        if (EnableUiInput && _garage is null && _replays is null && _myContent is null && !AccountView.IsOpen)
            MainMenuView.KeyPressed(key);
    }

    public override void KeyRepeated(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyRepeated(key, imguiWantsKeyboard, keys);
        if (_replays is not null && key is Key.Up or Key.Down or Key.Left or Key.Right)
            _replays.KeyPressed(key);
        if (_myContent is not null && key is Key.Up or Key.Down)
            _myContent.KeyPressed(key);
        if (_garage != null)
            _garage.KeyRepeated(key, imguiWantsKeyboard, keys);
    }

    public override void Render(NFMWorld.Graphics.ICommandBuffer cb, float alpha)
    {
        base.Render(cb, alpha);

        // The garage is inline here, so its card images are baked from the menu's frame.
        if (_garage != null)
            _garage.RenderThumbnails(cb);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        if (_replays is not null)
        {
            _replays.KeyPressed(key);

            return;
        }

        if (_myContent is not null)
        {
            _myContent.KeyPressed(key);

            return;
        }

        if (AccountView.IsOpen)
        {
            if (!imguiWantsKeyboard && key == Key.Escape)
                CloseAccount();

            return;
        }

        if (_garage != null)
        {
            _garage.KeyPressed(key, imguiWantsKeyboard, keys);

            return;
        }

        if (!imguiWantsKeyboard
            && key == Key.Enter
            && ((!_menuVisible && _transitionTime < 0) || (_transitionTime >= 0 && _transitionToMenu)))
        {
            BeginTransition(true);
            _transitionTime = TransitionDuration;

            return;
        }

        if (!imguiWantsKeyboard && EnableUiInput && key == Key.Escape)
        {
            if (!MainMenuView.NavigateBack())
                BeginTransition(false);

            return;
        }

        if (!imguiWantsKeyboard && EnableUiInput)
            MainMenuView.KeyPressed(key);

        if (imguiWantsKeyboard || _menuVisible || _transitionTime >= 0 || _introTime < IntroFadeStart)

            return;

        // Escape stays the scenery-only toggle for the whole intro; every other key skips straight
        // in, which is what "PRESS ANY BUTTON" promises.

        if (key == Key.Escape)
        {
            UiState.MainMenu.SceneryOnly.Value = !UiState.MainMenu.SceneryOnly.Value;

            return;
        }

        BeginTransition(true);
    }

    public override void MousePressed(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        if (_garage != null && EnableUiInput)
            _garageCamera.Press(x, y, imguiWantsMouse, button);

        if (imguiWantsMouse || _menuVisible || _transitionTime >= 0 || _introTime < IntroFadeStart)

            return;

        BeginTransition(true);
    }

    public override void MouseMoved(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        if (_garage != null && EnableUiInput)
            _garageCamera.Move(x, y, imguiWantsMouse, buttons);
    }

    public override void MouseReleased(
        int x,
        int y,
        bool imguiWantsMouse,
        MouseButton button,
        MouseButtons buttons,
        bool ctrlKey,
        bool shiftKey,
        bool altKey
    )
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Release(button);
        if (button == MouseButton.Primary)
            _garage?.FinishPaintInteraction();
    }
}
