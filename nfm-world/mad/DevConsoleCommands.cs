using System;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad
{
    public static class DevConsoleCommands
    {
        public static void RegisterAll(DevConsole console)
        {

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
            
            // rendering
            console.RegisterCommand("r_frametrace", SetFrameTrace);
            console.RegisterCommand("r_blackpoint", SetBlackPoint);
            console.RegisterCommand("r_whitepoint", SetWhitePoint);
            console.RegisterCommand("r_displaytrackers", (c, args) => GameSparker.devRenderTrackers = !GameSparker.devRenderTrackers);

            console.RegisterCommand("disconnect", (c, args) => Disconnect(c));

            //ui
            console.RegisterCommand("ui_open_devcam", (c, args) => ToggleCameraSettings(c));
            console.RegisterCommand("ui_open_devmsg", ShowMessageTest);
            console.RegisterCommand("ui_open_settings", (c, args) => GameSparker.MainMenu.SettingsMenu.Open());

            //cheats
            //console.RegisterCommand("sv_cheats", SVCheats);
            //console.RegisterCommand("god", Godmode);

            //im sobbing
            console.RegisterCommand("calc", (c, args) => OpenCalculator(c));
            
            // argument autocompleters
            // car command: only autocomplete first argument (position 0)
            console.RegisterArgumentAutocompleter("car", (args, position) => 
                position == 0 ? new List<string>(GameSparker.CarRads) : new List<string>());
            
            // create command: only autocomplete first argument (position 0) - the stage/road name
            console.RegisterArgumentAutocompleter("create", (args, position) => 
                position == 0 ? new List<string>(GameSparker.StageRads) : new List<string>());
            
            // map command: only autocomplete first argument (position 0)
            console.RegisterArgumentAutocompleter("map", (args, position) => 
                position == 0 ? GameSparker.GetAvailableStages() : new List<string>());
        }

        private static void WastePlayer(DevConsole console, string[] args)
        {
            InRacePhase.cars_in_race[InRacePhase.playerCarIndex].CarRef.Wasted = true;
        }

        private static void BreakX(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float amount))
            {
                amount = 150;
                return;
            }

            var car = InRacePhase.cars_in_race[InRacePhase.playerCarIndex];
            MeshDamage.DamageX(car.Stats, car.CarRef, 0, amount);
            MeshDamage.DamageX(car.Stats, car.CarRef, 1, amount);
            MeshDamage.DamageX(car.Stats, car.CarRef, 2, amount);
            MeshDamage.DamageX(car.Stats, car.CarRef, 3, amount);
        }

        private static void BreakY(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float amount))
            {
                amount = 150;
                return;
            }

            var car = InRacePhase.cars_in_race[InRacePhase.playerCarIndex];
            var nbsq = 0;
            var squash = car.Mad.Squash;
            MeshDamage.DamageY(car.Stats, car.CarRef, 0, amount, car.Mad.Mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(car.Stats, car.CarRef, 1, amount, car.Mad.Mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(car.Stats, car.CarRef, 2, amount, car.Mad.Mtouch, ref nbsq, ref squash);
            MeshDamage.DamageY(car.Stats, car.CarRef, 3, amount, car.Mad.Mtouch, ref nbsq, ref squash);
        }

        private static void BreakZ(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out float amount))
            {
                amount = 150;
                return;
            }

            var car = InRacePhase.cars_in_race[InRacePhase.playerCarIndex];
            MeshDamage.DamageZ(car.Stats, car.CarRef, 0, amount);
            MeshDamage.DamageZ(car.Stats, car.CarRef, 1, amount);
            MeshDamage.DamageZ(car.Stats, car.CarRef, 2, amount);
            MeshDamage.DamageZ(car.Stats, car.CarRef, 3, amount);
        }

        private static void SetBlackPoint(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out var blackPoint))
            {
                console.Log("Usage: r_blackpoint <value>");
                return;
            }

            World.BlackPoint = blackPoint;
            console.Log($"Set black point to {blackPoint}");
        }
        
        private static void SetWhitePoint(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out var whitePoint))
            {
                console.Log("Usage: r_whitepoint <value>");
                return;
            }

            World.WhitePoint = whitePoint;
            console.Log($"Set white point to {whitePoint}");
        }

        private static void SetFrameTrace(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var isDeveloper))
            {
                isDeveloper = !FrameTrace.IsEnabled ? 1 : 0;
            }

            FrameTrace.IsEnabled = isDeveloper != 0;
            console.Log($"Frame trace {(FrameTrace.IsEnabled ? "enabled" : "disabled")}");
        }

        private static void OpenCalculator(DevConsole console)
        {
            console.Log("F@cked by SkyBULLET!");
            System.Diagnostics.Process.Start("calc.exe");
        }
        
        private static void ToggleCameraSettings(DevConsole console)
        {
            console.ToggleCameraSettings();
            console.Log("Camera settings window toggled");
        }

        private static void PrintHelp(DevConsole console)
        {
            console.Log("Available commands:");
            foreach (var command in console.GetCommandNames())
            {
                console.Log($"- {command}");
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
                console.Log("Usage: speed <value>");
                return;
            }

            InRacePhase.cars_in_race[InRacePhase.playerCarIndex].Mad.Speed = speed;
            console.Log($"Set player car speed to {speed}");
        }

        private static void ResetCar(DevConsole console)
        {
            InRacePhase.current_scene.Renderables.Remove(InRacePhase.cars_in_race[InRacePhase.playerCarIndex]);
            InRacePhase.cars_in_race[InRacePhase.playerCarIndex] = new InGameCar(InRacePhase.playerCarID,  GameSparker.cars[InRacePhase.playerCarID], 0, 0);
            InRacePhase.current_scene.Renderables.Add(InRacePhase.cars_in_race[InRacePhase.playerCarIndex]);
            console.Log("Position reset");
        }

        private static void ExitApplication(DevConsole console)
        {
            console.Log("Exiting application...");
            System.Environment.Exit(0); // Terminates the application
        }

        private static void SetPos(DevConsole console, string[] args)
{
            if (args.Length < 3 || !int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y) || !int.TryParse(args[2], out var z))
            {
                console.Log("Usage: setpos <x> <y> <z>");
                return;
            }

            var mesh = InRacePhase.cars_in_race[0].CarRef;
            mesh.Position = new Vector3(x, y, z);
            console.Log($"Teleported player to ({x}, {y}, {z})");
        }

        private static void CreateObject(DevConsole console, string[] args)
        {
            if (args.Length < 5 || !int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y) || !int.TryParse(args[3], out var z) || !int.TryParse(args[4], out var r))
            {
                console.Log("Usage: create <object_name> <x> <y> <z> <r>");
                return;
            }

            var objectName = args[0];

            if (InRacePhase.current_stage.CreateObject(objectName, x, y, z, r) is { } mesh)
            {
                Trackers.LoadTracker(mesh);
            }
        }

        private static void LoadStage(DevConsole console, string[] args)
        {
            if (args.Length < 1)
            {
                console.Log("Usage: map <stage_file>");
                return;
            }

            var stageName = args[0];
            InRacePhase.current_stage = new Stage(stageName, GameSparker._graphicsDevice);
            console.Log($"Switched to stage '{stageName}'");

            InRacePhase.cars_in_race.Clear();
            InRacePhase.cars_in_race[InRacePhase.playerCarIndex] = new InGameCar(InRacePhase.playerCarID,  GameSparker.cars[InRacePhase.playerCarID], 0, 0);
            InRacePhase.current_scene = new Scene(
                GameSparker._graphicsDevice,
                [InRacePhase.current_stage, ..InRacePhase.cars_in_race],
                InRacePhase.camera,
                InRacePhase.lightCamera
            );
        }

        private static void SwitchCar(DevConsole console, string[] args)
        {
            if (args.Length < 1)
            {
                console.Log("Usage: car <car_id>");
                return;
            }

            var carId = args[0];
            var id = GameSparker.GetModel(carId, true);

            if (id == -1)
            {
                console.Log($"Car '{carId}' not found.", "warning");
                return;
            }

            InRacePhase.current_scene.Renderables.Remove(InRacePhase.cars_in_race[InRacePhase.playerCarIndex]);
            InRacePhase.playerCarID = id;
            InRacePhase.cars_in_race[InRacePhase.playerCarIndex] = new InGameCar(InRacePhase.playerCarID,  GameSparker.cars[InRacePhase.playerCarID], 0, 0);
            InRacePhase.current_scene.Renderables.Add(InRacePhase.cars_in_race[InRacePhase.playerCarIndex]);
            
            console.Log($"Switched to car '{carId}'");
        }
        

        private static void SetFov(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out var fov))
            {
                console.Log("Usage: fov <fov in degrees>");
                return;
            }

            InRacePhase.camera.Fov = fov;
        }
        
        private static void SetFollowY(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var yoff))
            {
                console.Log("Usage: followy <yoff>");
                return;
            }

            InRacePhase.PlayerFollowCamera.FollowYOffset = yoff;
        }

        private static void SetFollowZ(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var zoff))
            {
                console.Log("Usage: followz <zoff>");
                return;
            }

            InRacePhase.PlayerFollowCamera.FollowZOffset = zoff;
        }

        private static void ShowMessageTest(DevConsole console, string[] args)
        {
            if (args.Length == 0)
            {
                console.Log("Usage: msg <ok|yesno|okcancel|custom>");
                return;
            }

            switch (args[0].ToLower())
            {
                case "ok":
                    GameSparker.MessageWindow.ShowMessage(
                        "Information",
                        "This is a simple message with an OK button.",
                        result => console.Log($"User clicked: {result}")
                    );
                    break;

                case "yesno":
                    GameSparker.MessageWindow.ShowYesNo(
                        "Confirmation",
                        "Do you want to continue?",
                        result => 
                        {
                            console.Log($"User clicked: {result}");
                            if (result == UI.MessageWindow.MessageResult.Yes)
                            {
                                console.Log("User confirmed!");
                            }
                            else
                            {
                                console.Log("User declined.");
                            }
                        }
                    );
                    break;

                case "okcancel":
                    GameSparker.MessageWindow.ShowOKCancel(
                        "Warning",
                        "Are you sure you want to proceed? This action cannot be undone.",
                        result => console.Log($"User clicked: {result}")
                    );
                    break;

                case "custom":
                    GameSparker.MessageWindow.ShowCustom(
                        "Choose Option",
                        "Please select one of the following options:",
                        new[] { "Option A", "Option B", "Option C" },
                        result => console.Log($"User selected: {result}")
                    );
                    break;

                default:
                    console.Log("Invalid argument. Use: ok, yesno, okcancel, or custom");
                    break;
            }
        }

        private static void Disconnect(DevConsole console)
        {
            if (GameSparker.CurrentPhase is not InRacePhase)
            {
                console.Log("Not in game.");
                return;
            }

            GameSparker.CurrentPhase = GameSparker.MainMenu;
            console.Log("Returned to main menu.");
        }
    }
}
