using System.Reflection;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Gameplay.Gamemodes;
using NFMWorld.UI;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Files;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Gamemodes;
using NFMWorldLibrary.Multiplayer;
using Steamworks;
using Logging = NFMWorldLibrary.Logging;

namespace NFMWorld;

public static class DevConsoleCommands
{
    public static void RegisterAll(DevConsole console)
    {
        // TODO: map, car, go_tt, etc will break campaign flow (because they do not hook existing events) and so should
        //       be debug-only or implemented in a way that does not require setting the phase

        // general
        console.RegisterCommand("help", (c, args) => PrintHelp(c));
        console.RegisterCommand("clear", (c, args) => ClearLog(c));
        console.RegisterCommand("speed", SetSpeed);
        console.RegisterCommand("map", LoadStage);
        console.RegisterCommand("setpos", SetPos);
        console.RegisterCommand("create", CreateObject);
        console.RegisterCommand("reset", (c, args) => ResetCar(c));
        console.RegisterCommand("exit", (c, args) => ExitApplication(c));
        console.RegisterCommand("quit", (c, args) => ExitApplication(c));
        console.RegisterCommand("fov", SetFov);
        console.RegisterCommand("followy", SetFollowY);
        console.RegisterCommand("followz", SetFollowZ);
        console.RegisterCommand("car", SwitchCar);
        console.RegisterCommand("breakx", BreakX);
        console.RegisterCommand("breaky", BreakY);
        console.RegisterCommand("breakz", BreakZ);
        console.RegisterCommand("waste", WastePlayer);
        console.RegisterCommand("connect", Connect);
        
        console.RegisterCommand("replay_trial", (c, args) =>
        {
            var inRace = new InRacePhase(GameSparker.GraphicsDevice, args[1], new TimeTrialPreviewGamemodeFactory(SavedTimeTrial.Load(args[0], args[1])!), [
                new PlayerParameters
                {
                    CarName = args[0],
                    Color = new Color3(255, 0, 0),
                    PlayerName = "Player",
                    IsBot = false,
                    IsClientPlayer = true
                }
            ]);
            GameSparker.PopToRoot();
            GameSparker.PushPhase(inRace, PhaseManager.Groups.Event);
        });
            
        // rendering
        console.RegisterCommand("r_frametrace", SetFrameTrace);
        console.RegisterCommand("r_blackpoint", SetBlackPoint);
        console.RegisterCommand("r_whitepoint", SetWhitePoint);
        console.RegisterCommand("r_displaytrackers", (c, args) => GameSparker.devRenderTrackers = !GameSparker.devRenderTrackers);
        console.RegisterCommand("r_debugdisplay", (c, _) => {
            BaseStageRenderingPhase.DebugDisplay = !BaseStageRenderingPhase.DebugDisplay;
            Logging.Info(BaseStageRenderingPhase.DebugDisplay.ToString());
        });
            
        // gamemode
        console.RegisterCommand("go_tt", (c, args) =>
        {
            if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
            {
                var inRace = new InRacePhase(GameSparker.GraphicsDevice, inRacePhase.StageName, new TimeTrialGamemodeFactory(), inRacePhase.Players);
                inRace.Exited += (sender, args) =>
                {
                    GameSparker.PopGroup(PhaseManager.Groups.Event);
                };
                GameSparker.SetPhase(inRace);
            }
        });
        console.RegisterCommand("go_race", (c, args) =>
        {
            if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
            {
                var inRace = new InRacePhase(GameSparker.GraphicsDevice, inRacePhase.StageName, new PvpGamemodeFactory(PvpConstraint.Racing), inRacePhase.Players);
                inRace.Exited += (sender, args) =>
                {
                    GameSparker.PopGroup(PhaseManager.Groups.Event);
                };
                GameSparker.SetPhase(inRace);
            }
        });
        console.RegisterCommand("go_sbox", (c, args) =>
        {
            if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
            {
                var inRace = new InRacePhase(GameSparker.GraphicsDevice, inRacePhase.StageName, new SandboxGamemodeFactory(), inRacePhase.Players);
                inRace.Exited += (sender, args) =>
                {
                    GameSparker.PopGroup(PhaseManager.Groups.Event);
                };
                GameSparker.SetPhase(inRace);
            }
        });
        console.RegisterCommand("go_football", (c, args) =>
        {
            if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
            {
                var inRace = new InRacePhase(GameSparker.GraphicsDevice, inRacePhase.StageName, new FootballGamemodeFactory(), inRacePhase.Players);
                inRace.Exited += (sender, args) =>
                {
                    GameSparker.PopGroup(PhaseManager.Groups.Event);
                };
                GameSparker.SetPhase(inRace);
            }
        });

        console.RegisterCommand("disconnect", (c, args) => Disconnect(c));

        //ui
        console.RegisterCommand("ui_open_devcam", (c, args) => ToggleCameraSettings(c));
        console.RegisterCommand("ui_open_devmsg", ShowMessageTest);

        console.RegisterCommand("demo_playback", DemoPlayback);
        console.RegisterCommand("music_remastered", RemasteredMusic);

#if DEBUG
        console.RegisterCommand("fix", (c, args) =>
        {
            if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
            {
                var car = inRacePhase.CarsInRace[0];
                car.Fix();
            }
        });
        console.RegisterCommand("html_test", (console, args) => GameSparker.PushPhase(new XamlTestPhase()));
        console.RegisterCommand("cef_reload", (console, args) =>
        {
            GameSparker.CefRenderer?.Reload();
            Logging.Info("CEF page reloaded.");
        });
#endif
        console.RegisterCommand("cef_devtools", (console, args) =>
        {
            GameSparker.CefRenderer?.ShowDevTools();
            Logging.Info("CEF DevTools opened.");
        });

        //cheats
        //console.RegisterCommand("sv_cheats", SVCheats);
        //console.RegisterCommand("god", Godmode);

        //im sobbing
        console.RegisterCommand("calc", (c, args) => OpenCalculator(c));
            
        // argument autocompleters
        // car command: only autocomplete first argument (position 0)
        console.RegisterArgumentAutocompleter("car", (args, position) =>
            position == 0
                ? BackendGameSparker.cars.Values.SelectMany(i => i).Select(a => a.FileName).ToArray()
                : []);
            
        // create command: only autocomplete first argument (position 0) - the stage/road name
        console.RegisterArgumentAutocompleter("create", (args, position) => 
            position == 0 
                ? BackendGameSparker.stage_parts.Select(part => part.FileName)
                    .Concat(BackendGameSparker.vendor_stage_parts.Select(part => part.FileName))
                    .Concat(BackendGameSparker.user_stage_parts.Select(part => part.FileName))
                    .ToArray()
                : []);
            
        // map command: only autocomplete first argument (position 0)
        console.RegisterArgumentAutocompleter("map", (args, position) => 
            position == 0 ? GameSparker.GetAvailableStages() : []);
        
        console.RegisterArgumentAutocompleter("replay_trial", (args, position) =>
        {
            _tts ??= SavedTimeTrial.GetTimeTrials().ToArray();

            if (position == 0)
            {
                return _tts.Select(tt => tt.carName).Distinct().ToArray();
            }

            if (position == 1)
            {
                var carName = args[0];
                return _tts.Where(tt => tt.carName == carName).Select(tt => tt.stageName).Distinct().ToArray();
            }

            return [];
        });
    }

    private static (string stageName, string carName, string fileName)[]? _tts;

    private static void RemasteredMusic(DevConsole console, string[] args)
    {
        GameSparker.UseRemasteredMusic = !GameSparker.UseRemasteredMusic;
        Logging.Info($"Remastered music is now {(GameSparker.UseRemasteredMusic ? "enabled" : "disabled")}.");
        Logging.Info("Change stage for the change to teka effect.");
    }

    private static void DemoPlayback(DevConsole console, string[] args)
    {
        TimeTrialClientGamemode.PlaybackOnReset = !TimeTrialClientGamemode.PlaybackOnReset;
        Logging.Info($"Playback set to {TimeTrialClientGamemode.PlaybackOnReset}, for maps with a saved demo file.");
        Logging.Info("Restart the time trial for changes to take effect.");
    }

    private static void WastePlayer(DevConsole console, string[] args)
    {
        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            inRacePhase.GetCarVisual(0).VisuallyWasted = true;
        }
    }

    private static void Connect(DevConsole console, string[] args)
    {
        ENetMultiplayer.Init();
            
        if (args.Length < 1)
        {
            Logging.Info("Usage: connect <host> <port>");
            return;
        }
            
        if (args.Length < 2 || !ushort.TryParse(args[1], out ushort port))
            port = 7000;

        GameSparker.PushPhase(new LobbyPhase(GameSparker.GraphicsDevice,
            new WebSocketMultiplayerClientTransport(args[0], port)));
    }

    private static void BreakX(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float amount))
        {
            amount = 150;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var car = inRacePhase.CarsInRace[0];
            var visual = inRacePhase.GetCarVisual(0);
            var stats = car.Stats;
            MeshDamage.DamageX(stats, car, visual, 0, amount);
            MeshDamage.DamageX(stats, car, visual, 1, amount);
            MeshDamage.DamageX(stats, car, visual, 2, amount);
            MeshDamage.DamageX(stats, car, visual, 3, amount);
        }
    }

    private static void BreakY(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float amount))
        {
            amount = 150;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var car = inRacePhase.CarsInRace[0];
            var visual = inRacePhase.GetCarVisual(0);
            var stats = car.Stats;
            var nbsq = 0;
            var squash = inRacePhase.CarsInRace[0].CarPhysics.RoofDamage;
            var mtouch = inRacePhase.CarsInRace[0].CarPhysics.Mtouch;
            MeshDamage.DamageY(stats, car, visual, 0, amount, mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(stats, car, visual, 1, amount, mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(stats, car, visual, 2, amount, mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(stats, car, visual, 3, amount, mtouch, ref nbsq, ref squash);
        }
    }

    private static void BreakZ(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out float amount))
        {
            amount = 150;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var car = inRacePhase.CarsInRace[0];
            var visual = inRacePhase.GetCarVisual(0);
            var stats = car.Stats;
            MeshDamage.DamageZ(stats, car, visual, 0, amount);
            MeshDamage.DamageZ(stats, car, visual, 1, amount);
            MeshDamage.DamageZ(stats, car, visual, 2, amount);
            MeshDamage.DamageZ(stats, car, visual, 3, amount);
        }
    }

    private static void SetBlackPoint(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out var blackPoint))
        {
            Logging.Info("Usage: r_blackpoint <value>");
            return;
        }

        World.BlackPoint = blackPoint;
        Logging.Info($"Set black point to {blackPoint}");
    }
        
    private static void SetWhitePoint(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out var whitePoint))
        {
            Logging.Info("Usage: r_whitepoint <value>");
            return;
        }

        World.WhitePoint = whitePoint;
        Logging.Info($"Set white point to {whitePoint}");
    }

    private static void SetFrameTrace(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var isDeveloper))
        {
            isDeveloper = !FrameTrace.IsEnabled ? 1 : 0;
        }

        FrameTrace.IsEnabled = isDeveloper != 0;
        Logging.Info($"Frame trace {(FrameTrace.IsEnabled ? "enabled" : "disabled")}");
    }

    private static void OpenCalculator(DevConsole console)
    {
        Logging.Info("F@cked by SkyBULLET!");
        System.Diagnostics.Process.Start("calc.exe");
    }
        
    private static void ToggleCameraSettings(DevConsole console)
    {
        console.ToggleCameraSettings();
        Logging.Info("Camera settings window toggled");
    }

    private static void PrintHelp(DevConsole console)
    {
        Logging.Info("Available commands:");
        foreach (var command in console.GetCommandNames())
        {
            Logging.Info($"- {command}");
        }
    }

    private static void ClearLog(DevConsole console)
    {
        console.ClearLog();
    }

    private static void SetSpeed(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out var speed))
        {
            Logging.Info("Usage: speed <value>");
            return;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            inRacePhase.CarsInRace[0].CarPhysics.Speed = (fix64)speed;
        }
        Logging.Info($"Set player car speed to {speed}");
    }

    private static void ResetCar(DevConsole console)
    {
        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var originalCar = inRacePhase.CarsInRace[0];
            inRacePhase.CarsInRace[0] = new BackendCar(originalCar.Rad, 0, 0, 0, true);
        }

        Logging.Info("Position reset");
    }

    private static void ExitApplication(DevConsole console)
    {
        Logging.Info("Exiting application...");
        System.Environment.Exit(0); // Terminates the application
    }

    private static void SetPos(DevConsole console, string[] args)
    {
        if (args.Length < 3 || !int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y) || !int.TryParse(args[2], out var z))
        {
            Logging.Info("Usage: setpos <x> <y> <z>");
            return;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var mesh = inRacePhase.CarsInRace[0];
            mesh.Position = new f64Vector3(x, y, z);
            Logging.Info($"Teleported player to ({x}, {y}, {z})");
        }
    }

    private static void CreateObject(DevConsole console, string[] args)
    {
        if (args.Length < 5 || !int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y) || !int.TryParse(args[3], out var z) || !int.TryParse(args[4], out var r))
        {
            Logging.Info("Usage: create <object_name> <x> <y> <z> <r>");
            return;
        }

        var objectName = args[0];

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            inRacePhase.CurrentStage.Backend.CreateObject(objectName, x, y, z, r);
        }
        else
        {
            Logging.Info("Not in game.");
        }
    }

    private static void LoadStage(DevConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Logging.Info("Usage: map <stage_file>");
            return;
        }

        var stageName = args[0];

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            Logging.Info($"Switched to stage '{stageName}'");
            var inRace = new InRacePhase(GameSparker.GraphicsDevice, stageName, inRacePhase.Gamemode, inRacePhase.Players);
            inRace.Exited += (sender, args) =>
            {
                GameSparker.PopGroup(PhaseManager.Groups.Event);
            };
            GameSparker.SetPhase(inRace);
        }
    }

    private static void SwitchCar(DevConsole console, string[] args)
    {
        if (args.Length < 1)
        {
            Logging.Info("Usage: car <car_id>");
            return;
        }

        var carId = string.Join(" ", args);
        var (id, car) = BackendGameSparker.GetCar(carId);

        if (car == null)
        {
            Logging.Warning($"Car '{carId}' not found.");
            return;
        }

        if (GameSparker.CurrentPhase is InRacePhase inRacePhase)
        {
            var inRace = new InRacePhase(
                GameSparker.GraphicsDevice,
                inRacePhase.StageName,
                inRacePhase.Gamemode,
                inRacePhase.Players.Select(p => p.IsClientPlayer
                    ? new PlayerParameters
                    {
                        CarName = car.FileName,
                        Color = p.Color,
                        PlayerName = p.PlayerName,
                        IsBot = p.IsBot,
                        IsClientPlayer = true
                    }
                    : p).ToArray()
            );
            inRace.Exited += (sender, args) =>
            {
                GameSparker.PopGroup(PhaseManager.Groups.Event);
            };
            GameSparker.SetPhase(inRace);
        }
        
        IBackend.Backend.StopAllSounds();

        Logging.Info($"Switched to car '{carId}'");
    }
        

    private static void SetFov(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !float.TryParse(args[0], out var fov))
        {
            Logging.Info("Usage: fov <fov in degrees>");
            return;
        }

        CameraSettings.Fov = fov;
    }
        
    private static void SetFollowY(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var yoff))
        {
            Logging.Info("Usage: followy <yoff>");
            return;
        }

        FollowCamera.FollowYOffset = yoff;
    }

    private static void SetFollowZ(DevConsole console, string[] args)
    {
        if (args.Length < 1 || !int.TryParse(args[0], out var zoff))
        {
            Logging.Info("Usage: followz <zoff>");
            return;
        }

        FollowCamera.FollowZOffset = zoff;
    }

    private static void ShowMessageTest(DevConsole console, string[] args)
    {
        if (args.Length == 0)
        {
            Logging.Info("Usage: msg <ok|yesno|okcancel|custom>");
            return;
        }

        switch (args[0].ToLower())
        {
            case "ok":
                GameSparker.MessageWindow.ShowMessage(
                    "Information",
                    "This is a simple message with an OK button.",
                    result => Logging.Info($"User clicked: {result}")
                );
                break;

            case "yesno":
                GameSparker.MessageWindow.ShowYesNo(
                    "Confirmation",
                    "Do you want to continue?",
                    result => 
                    {
                        Logging.Info($"User clicked: {result}");
                        if (result == MessageWindow.MessageResult.Yes)
                        {
                            Logging.Info("User confirmed!");
                        }
                        else
                        {
                            Logging.Info("User declined.");
                        }
                    }
                );
                break;

            case "okcancel":
                GameSparker.MessageWindow.ShowOKCancel(
                    "Warning",
                    "Are you sure you want to proceed? This action cannot be undone.",
                    result => Logging.Info($"User clicked: {result}")
                );
                break;

            case "custom":
                GameSparker.MessageWindow.ShowCustom(
                    "Choose Option",
                    "Please select one of the following options:",
                    new[] { "Option A", "Option B", "Option C" },
                    result => Logging.Info($"User selected: {result}")
                );
                break;

            default:
                Logging.Info("Invalid argument. Use: ok, yesno, okcancel, or custom");
                break;
        }
    }

    private static void Disconnect(DevConsole console)
    {
        if (GameSparker.CurrentPhase is not InRacePhase or InMultiplayerRacePhase)
        {
            Logging.Info("Not in game.");
            return;
        }

        GameSparker.PopToRoot();
        IBackend.Backend.StopAllSounds();
            
        Logging.Info("Returned to main menu.");
    }
}