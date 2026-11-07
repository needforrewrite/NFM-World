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
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

public class MainMenuPhase : BaseStageRenderingPhase
{
    private const float IntroFadeStart = 3;
    private const float LogoFadeStart = 5.2f;
    private const float LogoFadeDuration = 2.2f;
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

    public override bool EnableUiInput => _menuVisible
        && (_transitionTime < 0 || (_transitionToMenu && _transitionSwitched));

    public MainMenuPhase(string stageName) : base(MenuPreferences.ResolveStage(TheVFS.VFS, SettingsMenu.MenuStage, stageName))
    {
        _appliedMenuStage = SettingsMenu.MenuStage;
        _appliedMenuMusic = SettingsMenu.MenuMusic;
        _appliedRemasteredMusic = GameSparker.UseRemasteredMusic;
        _logo = G.LoadImage("data/images/logo.svg");
        CurrentStage!.CarSoundsEnabled = false;
        _cameraDirector.Reset(Camera, CurrentStage!.Backend);
        CurrentStage.FadeFromOverride = _cameraDirector.ViewDistance;
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
    public override Node BuildUi() => MainMenuIntroView.Build(_logo, logo => Sx.View(
            style: () => Sx.Styles(position: Position.Absolute, left: 0, top: 0, width: "100%", height: "100%"),
            children:
            [
                MainMenuView.Build(new MainMenuView.Actions(
                    Play: OnFreePlayClicked,
                    Garage: OnGarageClicked,
                    ModelEditor: GameSparker.StartModelViewer,
                    StageEditor: GameSparker.StartStageEditor,
                    TimeTrials: OnTTClicked,
                    Unavailable: _ => GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable."),
                    Account: AccountView.Show), logo),
                AccountView.Build(new AccountView.Actions(
                    SignIn: OnSignIn,
                    Register: OnRegister,
                    SignOut: OnSignOut,
                    Close: AccountView.Hide),
                    () => GameSparker.AccountManager.LoggedIn,
                    () => GameSparker.AccountManager.ActiveAccount?.Username ?? "")
            ]),
        () => _garage!.BuildUi());

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

    private void OnRegister(string username, string password)
    {
        AccountView.SetBusy();
        _ = RegisterAsync(username, password);
    }

    private async Task RegisterAsync(string username, string password)
    {
        var created = await GameSparker.AccountManager.CreateLocalAccount(username, password);
        if (!created.Success)
        {
            AccountView.SetStatus("!" + created.Message, isError: true);
            return;
        }

        // Creating an account and signing in are separate calls, so do both rather than make
        // the player type the password again.
        var signedIn = await GameSparker.AccountManager.LogInToLocalAccount(username, password);
        AccountView.SetStatus(signedIn.Success ? "Account created." : "!" + signedIn.Message, isError: !signedIn.Success);
        if (signedIn.Success)
            AccountView.Hide();
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
        MainMenuView.Reset();

        ApplyMenuPreferences(checkHistory: true);
        CurrentStage!.CarSoundsEnabled = false;
        if (_menuVisible)
        {
            SetupMenuCar();
            if (_menuCar != null)
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, true);
        }
        UiState.MainMenu.StageName.Value = CurrentStage!.Backend.Name;
        base.Enter();
    }

    private void SetupMenuCar()
    {
        if (MenuHistory.Car is not { } car) return;
        CarsInRace.Clear();
        _menuCar = new BackendCar(car, 0, 0, 0, true);
        CarsInRace.Add(_menuCar);
    }

    protected override (string Path, double Tempo, double Frequency)? GetMusicTrack()
        => MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) is { } path
            ? (path, 1d, 1d) : base.GetMusicTrack();

    private void ApplyMenuPreferences(bool checkHistory = false)
    {
        var stageChanged = _appliedMenuStage != SettingsMenu.MenuStage;
        var musicChanged = _appliedMenuMusic != SettingsMenu.MenuMusic
            || (_appliedRemasteredMusic != GameSparker.UseRemasteredMusic
                && MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) == null);
        _appliedRemasteredMusic = GameSparker.UseRemasteredMusic;
        if (!stageChanged && !musicChanged && !checkHistory) return;

        var stageName = MenuPreferences.ResolveStage(TheVFS.VFS, SettingsMenu.MenuStage, MenuHistory.Stage);
        if (StageName != stageName)
        {
            StopAttractDrive();
            _settingsCamera.Reset();
            LoadStage(stageName, loadMusic: false);
            CurrentStage!.CarSoundsEnabled = false;
            _cameraDirector.Reset(Camera, CurrentStage.Backend);
            _attractShot = -1;
            if (_menuVisible && _menuCar != null)
            {
                CurrentStage.GetCarVisual(_menuCar);
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, true);
            }
            UiState.MainMenu.StageName.Value = CurrentStage.Backend.Name;
            musicChanged |= MenuPreferences.ResolveMusic(TheVFS.VFS, SettingsMenu.MenuMusic) == null;
        }

        if (musicChanged)
        {
            LoadStageMusic(reloadIfLoaded: true);
            if (!checkHistory) GameSparker.CurrentMusic = StageMusic;
        }
        _appliedMenuStage = SettingsMenu.MenuStage;
        _appliedMenuMusic = SettingsMenu.MenuMusic;
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            StopAttractDrive();
            GameSparker.AccountManager.ActiveAccountChanged -= OnActiveAccountChanged;
            (_logo as IDisposable)?.Dispose();
        }
        base.Dispose(disposing);
    }

    private void OnActiveAccountChanged(Account? account)
    {
        UiState.MainMenu.Set(account?.Username, account != null);
    }

    private void OnFreePlayClicked()
    {
        var factory = GamemodeFactory.Create("nfmm/pvp", new Dictionary<string, object>()
        {
            ["constraint"] = "both"
        });
        ClientSidePlayerInfo[] players = [
            new()
            {
                CarName = MenuHistory.Car?.FileName ?? MenuHistory.DefaultCar,
                IsClientPlayer = true,
                PlayerName = "MadPlayer",
                Color = default,
                IsBot = false
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido1",
                Color = default,
                IsBot = true
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido2",
                Color = default,
                IsBot = true
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido3",
                Color = default,
                IsBot = true
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido4",
                Color = default,
                IsBot = true
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido5",
                Color = default,
                IsBot = true
            },
            new()
            {
                CarName = "nfmm/audir8",
                IsClientPlayer = false,
                PlayerName = "ElStupido6",
                Color = default,
                IsBot = true
            }
        ];
        var stageName = MenuHistory.Stage;
        var inRace = new RacePhase(stageName, factory, players,
            LocalRaceHost.Create(stageName, factory, new ClientGamemodeParameters { Players = players }));
        inRace.Exited += (sender, args) =>
        {
            GameSparker.PopGroup(PhaseManager.Groups.Event);
        };
        GameSparker.PushPhase(inRace, PhaseManager.Groups.Event);

        Logging.Info("Game started!");
    }

    private void OnGarageClicked()
    {
        if (_garage != null || _menuCar == null) return;
        _garageEntryCar = _menuCar;
        if (_garageBlend == 0)
            _garageCamera.Reset();
        _garage = new GarageController(CarsInRace, _menuCar);
        _garage.CarSelected += (_, _) => CloseGarage(true);
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
                GameSparker.MessageWindow.ShowMessage("Time trial unavailable",
                    "This stage has no checkpoints. Choose a stage with checkpoints to start a time trial.");
                return;
            }
            PhaseSharedState.SelectedStageName = stageName;

            GaragePhase gp = new(stageName);
            gp.CarSelected += (sender, car) =>
            {
                var factory = GamemodeFactory.Create("nfmm/timetrial");
                ClientSidePlayerInfo[] players = [
                    new()
                    {
                        CarName = car.FileName,
                        Color = default,
                        IsBot = false,
                        IsClientPlayer = true,
                        PlayerName = "MadPlayer"
                    }
                ];
                var inRace = new RacePhase(stageName, factory, players,
                    LocalRaceHost.Create(stageName, factory, new ClientGamemodeParameters { Players = players }));
                inRace.Exited += (sender, args) =>
                {
                    GameSparker.PopGroup(PhaseManager.Groups.Event);
                };
                GameSparker.PushPhase(inRace, PhaseManager.Groups.Event);
            };

            gp.CarSelectionCancelled += (sender, _) =>
            {
                GameSparker.PopPhase();
            };

            GameSparker.PushPhase(gp, PhaseManager.Groups.Event);
        };

        GameSparker.PushPhase(ssp, PhaseManager.Groups.Event);
    }

    public override void GameTick()
    {
        const float seconds = 1f / Physics.TargetTps;
        _settingsCamera.RestoreNormal(Camera);
        ApplyMenuPreferences();
        if (_menuVisible)
        {
            _menuTime += seconds;
            if (_garage != null)
                _garageCamera.Tick(seconds);
            _garageBlend = Math.Clamp(_garageBlend + (_garage != null ? seconds : -seconds) / 1.1f, 0, 1);
            _menuCar = _garage?.CurrentCar ?? _menuCar;
            if (_menuCar != null)
            {
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, garageBlend: Smooth(_garageBlend),
                    garageAngle: _garageCamera.Angle, garageHeight: _garageCamera.Height);
                _settingsCamera.Update(Camera, _menuCar, NFMWorld.UI.Components.SettingsView.CameraPreviewActive);
                if (NFMWorld.UI.Components.SettingsView.MenuHudPreviewData is { } preview)
                {
                    preview.Power.Value = (float)_menuCar.CarPhysics.Power / 100f;
                    preview.Damage.Value = _menuCar.Stats.Maxmag > 0
                        ? (float)_menuCar.CarPhysics.DamagePoints / _menuCar.Stats.Maxmag : 0;
                    preview.Racers.Clear();
                    preview.Racers.Add(new NFMWorldLibrary.Hud.HudRacer(1, _menuCar.Stats.Name,
                        preview.DamageFraction, true, _menuCar.Wasted));
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
            UiState.MainMenu.BlackOpacity.Value = Math.Max(1 - Smooth((_introTime - IntroFadeStart) / 2), _cameraDirector.Fade);
        }
        if (!_menuVisible)
        {
            _introVisibility = Math.Clamp(_introVisibility
                + (UiState.MainMenu.SceneryOnly.Value ? -seconds : seconds) / 0.6f, 0, 1);
            var visibility = Smooth(_introVisibility);
            UiState.MainMenu.LogoOpacity.Value = Smooth((_introTime - LogoFadeStart) / LogoFadeDuration) * visibility;
            UiState.MainMenu.PromptOpacity.Value = Smooth((_introTime - PromptFadeStart) / 0.8f) * visibility
                * (0.82f + 0.18f * MathF.Cos((_introTime - PromptFadeStart) * MathF.Tau / 1.5f));
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
        if (_transitionTime >= 0) return;
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
        if (EnableUiInput && _garage is null)
            MainMenuView.KeyPressed(key);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        if (_garage != null)
        {
            _garage.KeyPressed(key, imguiWantsKeyboard, keys);
            return;
        }
        if (!imguiWantsKeyboard && key == Key.Enter
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
        if (!imguiWantsKeyboard && !_menuVisible && _introTime >= IntroFadeStart && _transitionTime < 0)
        {
            if (key == Key.Escape)
            {
                UiState.MainMenu.SceneryOnly.Value = !UiState.MainMenu.SceneryOnly.Value;
                return;
            }
            BeginTransition(true);
        }
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button,
        MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        if (_garage != null && EnableUiInput)
            _garageCamera.Press(x, y, imguiWantsMouse, button);
        if (!imguiWantsMouse && !_menuVisible && _introTime >= IntroFadeStart && _transitionTime < 0)
            BeginTransition(true);
    }

    public override void MouseMoved(int x, int y, bool imguiWantsMouse, MouseButtons buttons,
        bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseMoved(x, y, imguiWantsMouse, buttons, ctrlKey, shiftKey, altKey);
        if (_garage != null && EnableUiInput)
            _garageCamera.Move(x, y, imguiWantsMouse, buttons);
    }

    public override void MouseReleased(int x, int y, bool imguiWantsMouse, MouseButton button,
        MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MouseReleased(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        _garageCamera.Release(button);
    }
}
