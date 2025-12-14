using System.Diagnostics;
using System.Text;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;
using Path = System.IO.Path;
using Vector3 = Stride.Core.Mathematics.Vector3;
using NFMWorld.Mad.UI;

namespace NFMWorld.Mad;

public class GameSparker
{
    private static SpriteBatch _spriteBatch;
    public static GraphicsDevice _graphicsDevice;
    public static readonly float PHYSICS_MULTIPLIER = 21.4f/63f;

    public static PerspectiveCamera camera = new();
    public static OrthoCamera lightCamera = new()
    {
        Width = 8192,
        Height = 8192
    };

    public static readonly string version = GetVersionString();

    private static string GetVersionString()
    {
        var assembly = System.Reflection.Assembly.GetExecutingAssembly();
        var attributes = assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false);
        if (attributes.Length > 0 && attributes[0] is System.Reflection.AssemblyInformationalVersionAttribute infoVersion)
        {
            var version = infoVersion.InformationalVersion;
            // clip the commit hash
            var parts = version.Split('-');
            if (parts.Length >= 3)
            {
                var hash = parts[^1];
                if (hash.Length > 8)
                {
                    parts[^1] = hash.Substring(0, 8);
                    return string.Join("-", parts);
                }
            }
            return version;
        }
        return "NFM-World dev";
    }

    public enum GameState
    {
        Menu,
        InGame
    }

    public static GameState CurrentState = GameState.Menu;
    public static MainMenu? MainMenu = null;
    public static MessageWindow MessageWindow = new();
    public static SettingsMenu SettingsMenu = new();

    private static DirectionalLight light;
    
    private static MicroStopwatch timer;
    public static UnlimitedArray<Mesh> cars;
    public static UnlimitedArray<Mesh> stage_parts;
    
    public static Stage current_stage = null!;

    public static FollowCamera PlayerFollowCamera = new();
    
    public static DevConsole devConsole = new();

    public static readonly string[] CarRads = {
        "2000tornados", "formula7", "canyenaro", "lescrab", "nimi", "maxrevenge", "leadoxide", "koolkat", "drifter",
        "policecops", "mustang", "king", "audir8", "masheen", "radicalone", "drmonster"
    };
    public static readonly string[] StageRads = {
        "road", "froad", "twister2", "twister1", "turn", "offroad", "bumproad", "offturn", "nroad", "nturn",
        "roblend", "noblend", "rnblend", "roadend", "offroadend", "hpground", "ramp30", "cramp35", "dramp15",
        "dhilo15", "slide10", "takeoff", "sramp22", "offbump", "offramp", "sofframp", "halfpipe", "spikes", "rail",
        "thewall", "checkpoint", "fixpoint", "offcheckpoint", "sideoff", "bsideoff", "uprise", "riseroad", "sroad",
        "soffroad", "tside", "launchpad", "thenet", "speedramp", "offhill", "slider", "uphill", "roll1", "roll2",
        "roll3", "roll4", "roll5", "roll6", "opile1", "opile2", "aircheckpoint", "tree1", "tree2", "tree3", "tree4",
        "tree5", "tree6", "tree7", "tree8", "cac1", "cac2", "cac3", "8sroad", "8soffroad"
    };

    public static DevConsoleWriter Writer = null!;

    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
        Watch
    }
    private static ViewMode currentViewMode = ViewMode.Follow;
    /////////////////////////////////

    public static UnlimitedArray<Car> cars_in_race = [];
    public static int playerCarIndex = 0;
    public static int playerCarID = 14;
    public static int _stagePartCount = 0;

    public static Dictionary<Keys, bool> DebugKeyStates = new();

    public static void KeyPressed(Keys key)
    {
        DebugKeyStates[key] = true;
        
        // ideally it would be perfect if it was the tilde key, like in Source Engine games
        if (key == Keys.F1)
        {
            devConsole.Toggle();
            return;
        }

        if (CurrentState == GameState.Menu)
        {
            // Handle key capture for settings menu
            if (SettingsMenu.IsOpen && SettingsMenu.IsCapturingKey())
            {
                SettingsMenu.HandleKeyCapture(key);
            }
            return;
        }

        if (!devConsole.IsOpen()) {
            var bindings = SettingsMenu.Bindings;
            
            if (key == bindings.Accelerate)
            {
                cars_in_race[playerCarIndex].Control.Up = true;
            }
            if (key == bindings.Brake)
            {
                cars_in_race[playerCarIndex].Control.Down = true;
            }
            if (key == bindings.TurnRight)
            {
                cars_in_race[playerCarIndex].Control.Right = true;
            }
            if (key == bindings.TurnLeft)
            {
                cars_in_race[playerCarIndex].Control.Left = true;
            }
            if (key == bindings.Handbrake)
            {
                cars_in_race[playerCarIndex].Control.Handb = true;
            }
            if (key == bindings.Enter)
            {
                cars_in_race[playerCarIndex].Control.Enter = true;
            }
            if (key == bindings.LookBack)
            {
                cars_in_race[playerCarIndex].Control.Lookback = -1;
            }
            if (key == bindings.LookLeft)
            {
                cars_in_race[playerCarIndex].Control.Lookback = 3;
            }
            if (key == bindings.LookRight)
            {
                cars_in_race[playerCarIndex].Control.Lookback = 2;
            }
            if (key == bindings.ToggleMusic)
            {
                cars_in_race[playerCarIndex].Control.Mutem = !cars_in_race[playerCarIndex].Control.Mutem;
            }

            if (key == bindings.ToggleSFX)
            {
                cars_in_race[playerCarIndex].Control.Mutes = !cars_in_race[playerCarIndex].Control.Mutes;
            }

            if (key == bindings.ToggleArrace)
            {
                cars_in_race[playerCarIndex].Control.Arrace = !cars_in_race[playerCarIndex].Control.Arrace;
            }

            if (key == bindings.ToggleRadar)
            {
                cars_in_race[playerCarIndex].Control.Radar = !cars_in_race[playerCarIndex].Control.Radar;
            }
            if (key == bindings.CycleView)
            {
                currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
            }
        }
    }

    public static void KeyReleased(Keys key)
    {
        DebugKeyStates[key] = false;
        
        if (CurrentState == GameState.Menu)
        {
            return;
        }
        
        var bindings = SettingsMenu.Bindings;
        
        if (cars_in_race[playerCarIndex].Control.Multion < 2)
        {
            if (key == bindings.Accelerate)
            {
                cars_in_race[playerCarIndex].Control.Up = false;
            }
            if (key == bindings.Brake)
            {
                cars_in_race[playerCarIndex].Control.Down = false;
            }
            if (key == bindings.TurnRight)
            {
                cars_in_race[playerCarIndex].Control.Right = false;
            }
            if (key == bindings.TurnLeft)
            {
                cars_in_race[playerCarIndex].Control.Left = false;
            }
            if (key == bindings.Handbrake)
            {
                cars_in_race[playerCarIndex].Control.Handb = false;
            }
        }
        if (key == Keys.Escape)
        {
            cars_in_race[playerCarIndex].Control.Exit = false;
        }
        if (key == bindings.LookBack || key == bindings.LookLeft || key == bindings.LookRight)
        {
            cars_in_race[playerCarIndex].Control.Lookback = 0;
        }
    }

    public static List<string> GetAvailableStages()
    {
        var stages = new List<string>();
        var stagesPath = "data/stages";
        
        if (Directory.Exists(stagesPath))
        {
            // recursive search
            foreach (var file in Directory.GetFiles(stagesPath, "*.txt", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(stagesPath, file);
                var pathWithoutExtension = Path.ChangeExtension(relativePath, null);
                stages.Add(pathWithoutExtension.Replace('\\', '/'));
            }
        }
        
        // sort numbers properly
        stages.Sort((a, b) => {
            var aSegments = a.Split('/');
            var bSegments = b.Split('/');
            
            for (int seg = 0; seg < Math.Min(aSegments.Length, bSegments.Length); seg++)
            {
                var aParts = System.Text.RegularExpressions.Regex.Split(aSegments[seg], @"(\d+)")
                    .Where(s => !string.IsNullOrEmpty(s)).ToArray();
                var bParts = System.Text.RegularExpressions.Regex.Split(bSegments[seg], @"(\d+)")
                    .Where(s => !string.IsNullOrEmpty(s)).ToArray();
                
                for (int i = 0; i < Math.Min(aParts.Length, bParts.Length); i++)
                {
                    if (int.TryParse(aParts[i], out var aNum) && int.TryParse(bParts[i], out var bNum))
                    {
                        int numCompare = aNum.CompareTo(bNum);
                        if (numCompare != 0) return numCompare;
                    }
                    else
                    {
                        int strCompare = string.Compare(aParts[i], bParts[i], StringComparison.OrdinalIgnoreCase);
                        if (strCompare != 0) return strCompare;
                    }
                }
                
                if (aParts.Length != bParts.Length)
                    return aParts.Length.CompareTo(bParts.Length);
            }
            
            return aSegments.Length.CompareTo(bSegments.Length);
        });
        
        return stages;
    }

    public static int GetModel(string input, bool forCar = false)
    {
        // Combine all model arrays
        string[][] allModels = new string[][]
        {
            forCar ? CarRads : StageRads
        };

        int modelId = 0;

        for (int i = 0; i < allModels.Length; i++)
        {
            for (int j = 0; j < allModels[i].Length; j++)
            {
                if (string.Equals(input, allModels[i][j], StringComparison.OrdinalIgnoreCase))
                {
                    int offset = 0;

                    // Calculate offset based on previous arrays
                    for (int k = 0; k < i; k++)
                    {
                        offset += allModels[k].Length;
                    }

                    modelId = j + offset;
                    return modelId;
                }
            }
        }

        Debug.WriteLine("No results for GetModel");
        return -1;
    }

    public static void Load(Game game)
    {
        _graphicsDevice = game.GraphicsDevice;
        _spriteBatch = new SpriteBatch(game.GraphicsDevice);
        timer = new MicroStopwatch();
        timer.Start();
        
        cars = [];
        stage_parts = [];

        FileUtil.LoadFiles("./data/models/cars", CarRads, (ais, id) => {
            cars[id] = new Mesh(game.GraphicsDevice, Encoding.UTF8.GetString(ais));
        });

        FileUtil.LoadFiles("./data/models/stage", StageRads, (ais, id) => {
            stage_parts[id] = new Mesh(game.GraphicsDevice, Encoding.UTF8.GetString(ais));
        });

        // init menu
        CurrentState = GameState.Menu;
        MainMenu = new MainMenu();
        
        // Initialize SettingsMenu writer
        SettingsMenu.Writer = Writer;
        
        // load user config
        SettingsMenu.LoadConfig();

        for (var i = 0; i < StageRads.Length; i++) {
            if (stage_parts[i] == null) {
                throw new Exception("No valid ContO (Stage Part) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }
        for (var i = 0; i < CarRads.Length; i++) {
            if (cars[i] == null)
            {
                throw new Exception("No valid ContO (Vehicle) has been assigned to ID " + i + " (" + StageRads[i] + ")");
            }
        }
    }

    public static void StartGame()
    {
        // temp
        CurrentState = GameState.InGame;
        MainMenu = null;

        current_stage = new Stage("nfm2/15_dwm", _graphicsDevice);
        cars_in_race[playerCarIndex] = new Car(new Stat(14), 14, cars[14], 0, 0);
        
        for (var i = 0; i < cars.Count; i++)
        {
            Console.WriteLine($"car {new Stat(i).Names}: {new Stat(i).Score:0}");
        }
        
        Console.WriteLine("Game started!");
    }

    public static void GameTick()
    {
        FrameTrace.ClearMessages();
        // only tick game logic when actually in-game
        if (CurrentState != GameState.InGame)
        {
            return;
        }
        
        cars_in_race[playerCarIndex].Drive();
        switch (currentViewMode)
        {
            case ViewMode.Follow:
                PlayerFollowCamera.Follow(camera, cars_in_race[playerCarIndex].Conto, cars_in_race[playerCarIndex].Mad.Cxz, cars_in_race[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                // Medium.Around(cars_in_race[playerCarIndex].Conto, true);
                break;
        }
        // camera.Position = new Vector3(0, 10000, 0);
        // camera.LookAt = new Vector3(1, 250, 0);
        
        foreach (var element in current_stage.pieces)
        {
            element.GameTick();
        }
    }

    public static void Render()
    {
        // only render game when in-game state
        if (CurrentState != GameState.InGame)
        {
            return;
        }

        lightCamera.Position = camera.Position + new Vector3(0, -5000, 0);
        lightCamera.LookAt = camera.Position + new Vector3(1f, 0, 0); // 0,0,0 causes shadows to break

        camera.OnBeforeRender();
        lightCamera.OnBeforeRender();
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // CREATE SHADOW MAP
        
        // Set our render target to our floating point render target
        _graphicsDevice.SetRenderTarget(Program.shadowRenderTarget);

        // Clear the render target to white or all 1's
        // We set the clear to white since that represents the 
        // furthest the object could be away
        _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);
        
        RenderInternal(true);

        _graphicsDevice.SetRenderTarget(null);
        
        // DRAW WITH SHADOW MAP
        
        _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        _graphicsDevice.SamplerStates[1] = SamplerState.PointClamp;

        RenderInternal();
        
        // DISPLAY SHADOW MAP
        _spriteBatch.Begin(0, BlendState.Opaque, SamplerState.PointClamp);
        _spriteBatch.Draw(Program.shadowRenderTarget, new Microsoft.Xna.Framework.Rectangle(0, 0, 128, 128), Microsoft.Xna.Framework.Color.White);
        _spriteBatch.End();

        _graphicsDevice.Textures[0] = null;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearWrap;

        FrameTrace.RenderMessages();
    }

    private static void RenderInternal(bool isCreateShadowMap = false)
    {
        current_stage.Render(camera, lightCamera, isCreateShadowMap);
        
        foreach (var car in cars_in_race)
        {
            car.Render(camera, lightCamera, isCreateShadowMap);
        }
    }

    public static void RenderImgui()
    {
        devConsole.Render();
        MessageWindow.Render();
        SettingsMenu.Render();
    }
}
