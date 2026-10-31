using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

public class MainMenuPhase : BaseStageRenderingPhase
{
    private const float LogoFadeStart = 5.2f;
    private const float LogoFadeDuration = 2.2f;
    private const float PromptFadeStart = LogoFadeStart + LogoFadeDuration + 0.2f;
    private readonly MenuCameraDirector _cameraDirector = new();
    private readonly IImage _logo;
    private float _introTime;
    private float _transitionTime = -1;
    private float _menuTime;
    private bool _menuVisible;
    private bool _entered;
    private BackendCar? _menuCar;
    private GarageController? _garage;
    private BackendCar? _garageEntryCar;
    private float _garageBlend;
    private MenuAttractDrive? _attractDrive;
    private int _attractShot = -1;
    private bool _transitionToMenu = true;
    private bool _transitionSwitched;

    public override bool EnableUiInput => _menuVisible && _transitionTime < 0;

    public MainMenuPhase(string stageName) : base(stageName)
    {
        _logo = G.LoadImage("data/images/logo.svg");
        CurrentStage!.CarSoundsEnabled = false;
        _cameraDirector.Reset(Camera, CurrentStage!.Backend);
        CurrentStage.FadeFromOverride = _cameraDirector.ViewDistance;
        UiState.MainMenu.MenuVisible.Value = false;
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
    public override Node BuildUi() => MainMenuIntroView.Build(_logo, logo => MainMenuView.Build(new MainMenuView.Actions(
        Play: OnFreePlayClicked,
        Garage: OnGarageClicked,
        ModelEditor: GameSparker.StartModelViewer,
        StageEditor: GameSparker.StartStageEditor,
        TimeTrials: OnTTClicked,
        Unavailable: _ => GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable.")), logo),
        () => _garage!.BuildUi());

    public override void Enter()
    {
        if (_entered && StageName != MenuHistory.Stage)
            LoadStage(MenuHistory.Stage, reloadIfLoaded: true);
        CurrentStage!.CarSoundsEnabled = false;
        if (_menuVisible)
        {
            SetupMenuCar();
            if (_menuCar != null)
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, true);
        }
        UiState.MainMenu.StageName.Value = CurrentStage!.Backend.Name;
        _entered = true;
        base.Enter();
    }

    private void SetupMenuCar()
    {
        if (MenuHistory.Car is not { } car) return;
        CarsInRace.Clear();
        _menuCar = new BackendCar(car, 0, 0, 0, true);
        CarsInRace.Add(_menuCar);
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
                PlayerName = "ElStupido",
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
        _garage = new GarageController(CarsInRace, _menuCar);
        _garage.CarSelected += (_, _) => CloseGarage(true);
        _garage.CarSelectionCancelled += (_, _) => CloseGarage(false);
        _garage.Enter();
        UiState.MainMenu.GarageOpen.Value = true;
    }

    private void CloseGarage(bool commit)
    {
        _menuCar = commit ? _garage?.CurrentCar : _garageEntryCar;
        if (_menuCar != null && CarsInRace[0] != _menuCar)
            CarsInRace[0] = _menuCar;
        _garage = null;
        _garageEntryCar = null;
        UiState.MainMenu.GarageOpen.Value = false;
    }

    private void OnTTClicked()
    {
        StageSelectPhase ssp = new();
        ssp.StageSelected += (sender, stageName) =>
        {
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
        if (_menuVisible)
        {
            _menuTime += seconds;
            _garageBlend = Math.Clamp(_garageBlend + (_garage != null ? seconds : -seconds) / 1.1f, 0, 1);
            _menuCar = _garage?.CurrentCar ?? _menuCar;
            if (_menuCar != null)
                _cameraDirector.ShowCar(Camera, _menuCar, _menuTime, garageBlend: Smooth(_garageBlend));
        }
        else
        {
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
            if (_transitionTime >= 2)
                _transitionTime = -1;
        }
        else if (!_menuVisible)
        {
            UiState.MainMenu.BlackOpacity.Value = Math.Max(1 - Smooth((_introTime - 3) / 2), _cameraDirector.Fade);
        }
        if (!_menuVisible)
        {
            UiState.MainMenu.LogoOpacity.Value = Smooth((_introTime - LogoFadeStart) / LogoFadeDuration);
            UiState.MainMenu.PromptOpacity.Value = Smooth((_introTime - PromptFadeStart) / 0.8f)
                * (0.82f + 0.18f * MathF.Cos((_introTime - PromptFadeStart) * MathF.Tau / 4));
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
        _transitionToMenu = toMenu;
        _transitionSwitched = false;
        _transitionTime = 0;
    }

    private static float Smooth(float value)
    {
        value = Math.Clamp(value, 0, 1);
        return value * value * (3 - 2 * value);
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
        if (_garage != null)
        {
            _garage.KeyPressed(key, imguiWantsKeyboard, keys);
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
        if (!imguiWantsKeyboard && !_menuVisible && _introTime >= PromptFadeStart && _transitionTime < 0)
            BeginTransition(true);
    }

    public override void MousePressed(int x, int y, bool imguiWantsMouse, MouseButton button,
        MouseButtons buttons, bool ctrlKey, bool shiftKey, bool altKey)
    {
        base.MousePressed(x, y, imguiWantsMouse, button, buttons, ctrlKey, shiftKey, altKey);
        if (!imguiWantsMouse && !_menuVisible && _introTime >= PromptFadeStart && _transitionTime < 0)
            BeginTransition(true);
    }
}
