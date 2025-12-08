using System;
using NFMWorld.Util;

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

            //im sobbing
            console.RegisterCommand("calc", (c, args) => OpenCalculator(c));
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
            GameSparker.cars_in_race[0].Conto.X = 0;
            GameSparker.cars_in_race[0].Conto.Y = 250;
            GameSparker.cars_in_race[0].Conto.Z = 0;
            GameSparker.cars_in_race[0].Conto.Xy = 0;
            GameSparker.cars_in_race[0].Conto.Xz = 0;
            GameSparker.cars_in_race[0].Conto.Zy = 0;
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

            GameSparker.cars_in_race[0].Conto.X = x;
            GameSparker.cars_in_race[0].Conto.Y = y;
            GameSparker.cars_in_race[0].Conto.Z = z;
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
        }
        

        private static void SetFov(DevConsole console, string[] args)
        {
            if (args.Length < 1 || !float.TryParse(args[0], out var fov))
            {
                console.Log("Usage: fov <fov in degrees>");
                return;
            }

            Medium.FocusPoint = GetFocusPoint(fov);
        }
        
        private static int GetFocusPoint(float fov) {
            return (int) MathF.Round(Medium.Cx * MathF.Tan(MathF.Abs(180 - fov) * 0.5f * (MathF.PI / 180)));
        }
    }
}
