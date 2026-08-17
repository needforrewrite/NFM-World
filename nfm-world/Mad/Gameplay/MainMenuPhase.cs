﻿﻿using Microsoft.Xna.Framework.Graphics;
 using NFMWorld.Accounts;
 using NFMWorld.DriverInterface;
 using NFMWorld.UI;
using NFMWorld.UI.Cef;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Gamemodes.Lua;
using NFMWorldLibrary.Gamemodes.RaceHost;
using NFMWorldLibrary.Multiplayer;
using NFMWorldLibrary.Util;

namespace NFMWorld.Gameplay;

// TODO: implement the same menu as in nfm-lit

public class MainMenuPhase : BaseStageRenderingPhase
{
    private readonly MainMenuBridge _bridge = new();

    public MainMenuPhase(GraphicsDevice graphicsDevice, string stageName) : base(graphicsDevice, stageName)
    {
        CefBridge = _bridge;

        _bridge.NavigateRequested += OnNavigateRequested;
        _bridge.LogoutRequested += OnLogoutClicked;
        _bridge.SettingsRestartConfirmed += () => System.Environment.Exit(0);

        // Push initial account state if available
        var account = GameSparker.AccountManager.LoggedIn
            ? GameSparker.AccountManager.ActiveAccount
            : null;
        _bridge.PushAccount(account?.Username, account != null);

        // Subscribe to account changes via the CLR event directly
        GameSparker.AccountManager.ActiveAccountChanged += OnActiveAccountChanged;
    }

    private void OnActiveAccountChanged(Account? account)
    {
        _bridge.PushAccount(account?.Username, account != null);
    }

    private void OnNavigateRequested(string page)
    {
        switch (page)
        {
            case "play":
            case "singleplayer":
                OnFreePlayClicked();
                break;
            case "multiplayer":
                OnClickUnavailable();
                break;
            case "training":
                OnClickUnavailable();
                break;
            case "garage":
                OnGarageClicked();
                break;
            case "settings":
                // Settings is now an embedded component in the main menu UI —
                // the frontend handles view switching directly without a phase push.
                break;
            case "credits":
                OnClickUnavailable();
                break;
            case "quit":
                OnQuitClicked();
                break;
            case "modelEditor":
                OnModelEditorClicked();
                break;
            case "stageEditor":
                OnStageEditorClicked();
                break;
            case "timeTrials":
                OnTTClicked();
                break;
        }
    }

    private void OnFreePlayClicked()
    {
        var factory = new LuaGamemodeFactory("nfmm/pvp", new Dictionary<string, object>()
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
        var inRace = new RacePhase(GraphicsDevice, "nfm2/9_majestic", factory, players,
            LocalRaceHost.Create("nfm2/9_majestic", factory, new ClientGamemodeParameters { Players = players }));
        inRace.Exited += (sender, args) =>
        {
            GameSparker.PopGroup(PhaseManager.Groups.Event);
        };
        GameSparker.PushPhase(inRace, PhaseManager.Groups.Event);

        Logging.Info("Game started!");
    }

    private void OnLogoutClicked()
    {
        if (GameSparker.AccountManager.LoggedIn)
        {
            GameSparker.AccountManager.LogOut();
        }
    }

    private void OnTTClicked()
    {
        StageSelectPhase ssp = new(GraphicsDevice);
        ssp.StageSelected += (sender, stageName) =>
        {
            PhaseSharedState.SelectedStageName = stageName;

            GaragePhase gp = new(GraphicsDevice, stageName);
            gp.CarSelected += (sender, car) =>
            {
                var factory = new LuaGamemodeFactory("nfmm/timetrial");
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
                var inRace = new RacePhase(GraphicsDevice, stageName, factory, players,
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

    private void OnGarageClicked()
    {
        GaragePhase gp = new GaragePhase(GraphicsDevice);

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


    private void OnModelEditorClicked()
    {
        GameSparker.StartModelViewer();
    }

    private void OnStageEditorClicked()
    {
        GameSparker.StartStageEditor();
    }

    private void OnSettingsClicked()
    {
        // Settings is now embedded in the main menu UI via SettingsHandler sub-handler.
        // The frontend MainMenu.tsx shows/hides the Settings component directly.
    }

    private void OnClickUnavailable()
    {
        GameSparker.MessageWindow.ShowMessage("Info", "This feature is currently unavailable.");
    }

    private void OnQuitClicked()
    {
        GameSparker.MessageWindow.ShowYesNo("Quit", "Are you sure you want to quit?",
        result =>
        {
            if (result == MessageWindow.MessageResult.Yes)
            {
                System.Environment.Exit(0);
            }
        });
    }

    public override void GameTick()
    {
        base.GameTick();
    }

    public override void KeyPressed(Key key, bool imguiWantsKeyboard, in Keys keys)
    {
        base.KeyPressed(key, imguiWantsKeyboard, keys);

        // Forward to sub-handlers (e.g., SettingsHandler key capture during rebinding)
        if (_bridge.TryHandleKeyPress(key))
            return;
    }

    public override void RenderImgui()
    {
        base.RenderImgui();

        // if (accountManagerMenu is not null)
        // {
        //     var res = accountManagerMenu.Process();
        //     if (res == AccountManagerModal.AccountManagerFloatingMenuState.LoggedIn)
        //     {
        //         accountManagerMenu.Close();
        //         accountManagerMenu = null;
        //     }
        //     else if (res == AccountManagerModal.AccountManagerFloatingMenuState.Canceled)
        //     {
        //         accountManagerMenu.Close();
        //         accountManagerMenu = null;
        //     }
        // }
    }
}