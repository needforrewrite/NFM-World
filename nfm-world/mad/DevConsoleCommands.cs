using System;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad
{
    public static class DevConsoleCommands
    {
        public static void RegisterAll(DevConsole console)
        {
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
            console.RegisterCommand("car", SwitchCar);

            console.RegisterCommand("r_frametrace", SetFrameTrace);

            //im sobbing
            console.RegisterCommand("calc", (c, args) => OpenCalculator(c));
            
            // argument autocompleters
            console.RegisterArgumentAutocompleter("car", (args) => new List<string>(GameSparker.CarRads));
            console.RegisterArgumentAutocompleter("create", (args) => new List<string>(GameSparker.StageRads));
            console.RegisterArgumentAutocompleter("map", (args) => GameSparker.GetAvailableStages());
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

            GameSparker.cars_in_race[0].Mad.Speed = speed;
            console.Log($"Set player car speed to {speed}");
        }

        private static void ResetCar(DevConsole console)
        {
            // doesnt reset gravity i cba rn
            GameSparker.cars_in_race[0].Conto.Position = new Vector3(0, World.Ground, 0);
            GameSparker.cars_in_race[0].Conto.Rotation = Euler.Identity;
            GameSparker.cars_in_race[0].Mad.Speed = 0;

            //idk how to get rid of flames yet
            GameSparker.cars_in_race[0].Mad.Newcar = true;
            GameSparker.cars_in_race[0].Mad.Wasted = false;
            GameSparker.cars_in_race[0].Mad.Hitmag = 0;
            console.Log("Position reset");
        }

        private static void ExitApplication(DevConsole console)
        {
            console.Log("Exiting application...");
            Environment.Exit(0); // Terminates the application
        }

        private static void SetPos(DevConsole console, string[] args)
{
            if (args.Length < 3 || !int.TryParse(args[0], out var x) || !int.TryParse(args[1], out var y) || !int.TryParse(args[2], out var z))
            {
                console.Log("Usage: setpos <x> <y> <z>");
                return;
            }

            var mesh = GameSparker.cars_in_race[0].Conto;
            mesh.Position = new Vector3(x, y, z);
            console.Log($"Teleported player to ({x}, {y}, {z})");
        }

        private static void CreateObject(DevConsole console, string[] args)
        {
            if (args.Length < 4 || !int.TryParse(args[1], out var x) || !int.TryParse(args[2], out var y) || !int.TryParse(args[3], out var z) || !int.TryParse(args[4], out var r))
            {
                console.Log("Usage: create <object_name> <x> <y> <z> <r>");
                return;
            }

            var objectName = args[0];

            GameSparker.CreateObject(objectName, x, y, z, r);
        }

        private static void LoadStage(DevConsole console, string[] args)
        {
            if (args.Length < 1)
            {
                console.Log("Usage: map <stage_file>");
                return;
            }

            var stageName = args[0];
            GameSparker.Loadstage(stageName);
            console.Log($"Switched to stage '{stageName}'");

            GameSparker.cars_in_race.Clear();
            GameSparker.cars_in_race[GameSparker.playerCarIndex] = new Car(new Stat(GameSparker.playerCarID), GameSparker.playerCarID,  GameSparker.cars[GameSparker.playerCarID], 0, 0);
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

            GameSparker.cars_in_race.Clear();
            GameSparker.playerCarID = id;
            GameSparker.cars_in_race[GameSparker.playerCarIndex] = new Car(new Stat(id), id,  GameSparker.cars[id], 0, 0);
            
            console.Log($"Switched to car '{carId}'");
        }
        

        private static void SetFov(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out var fov))
            {
                console.Log("Usage: fov <fov in degrees>");
                return;
            }

            GameSparker.camera.Fov = fov;
        }
        
        private static void SetFollowY(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !int.TryParse(args[0], out var yoff))
            {
                console.Log("Usage: followy <yoff>");
                return;
            }

            GameSparker.PlayerFollowCamera.FollowYOffset = yoff;
        }
    }
}
