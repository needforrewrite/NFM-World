using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.Reactor;
using NFMWorld.UI;
using NFMWorld.UI.Routes;
using NFMWorld.UI.ViewModels;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;

namespace NFMWorld.Gameplay;

public class MainMenuPhase : BaseStageRenderingPhase
{
    public MainMenuPhase(string stageName) : base(stageName)
    {
        // Write initial account state if available
        var account = GameSparker.AccountManager.LoggedIn
            ? GameSparker.AccountManager.ActiveAccount
            : null;
        UiState.MainMenu.Set(account?.Username, account != null);

        // Subscribe to account changes via the CLR event directly
        GameSparker.AccountManager.ActiveAccountChanged += OnActiveAccountChanged;
    }

    /// <summary>The menu's items, wired straight to this phase's handlers.</summary>
    public override Node BuildUi() => MainMenuView.Build(new MainMenuView.Actions(
        Play: OnFreePlayClicked,
        Garage: OnGarageClicked,
        ModelEditor: GameSparker.StartModelViewer,
        StageEditor: GameSparker.StartStageEditor,
        TimeTrials: OnTTClicked,
        Unavailable: _ => GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable.")));

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
                CarName = "nfmm/radicalone",
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
        var inRace = new RacePhase("nfm2/9_majestic", factory, players,
            LocalRaceHost.Create("nfm2/9_majestic", factory, new ClientGamemodeParameters { Players = players }));
        inRace.Exited += (sender, args) =>
        {
            GameSparker.PopGroup(PhaseManager.Groups.Event);
        };
        GameSparker.PushPhase(inRace, PhaseManager.Groups.Event);

        Logging.Info("Game started!");
    }

    private void OnGarageClicked()
    {
        GaragePhase gp = new GaragePhase();

        gp.CarSelected += (sender, c) =>
        {
            GameSparker.PopPhase();
        };

        gp.CarSelectionCancelled += (sender, _) =>
        {
            GameSparker.PopPhase();
        };

        GameSparker.PushPhase(gp);
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
        base.GameTick();
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);
    }
}
