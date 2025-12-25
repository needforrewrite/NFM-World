using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;
using ImGuiNET;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;
using Path = System.IO.Path;
using NFMWorld.Mad.UI;
using NFMWorld.SkiaDriver;
using NFMWorld.DriverInterface;

namespace NFMWorld.Mad;

public class GameSparker
{
    public const float OriginalTps = 21.4f;
    public const float TargetTps = 63f;
    public const int OriginalTicksPerNewTick = 3;
    public static Program _game;
    public static GraphicsDevice _graphicsDevice;
    public const float PHYSICS_MULTIPLIER = OriginalTps/TargetTps;

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

    public static BasePhase CurrentPhase
    {
        get;
        set
        {
            // ReSharper disable once ConditionalAccessQualifierIsNonNullableAccordingToAPIContract
            field?.Exit();
            field = value;
            value.Enter();
        }
    }

    public static IRadicalMusic? CurrentMusic;
    /// <summary>
    /// Use remastered music (soundtrackremaster in stage files) where available.
    /// </summary>
    public static bool UseRemasteredMusic = false;

    public static MainMenuPhase? MainMenu;
    public static InRacePhase? InRace;
    public static MessageWindow MessageWindow = new();
    public static ModelEditorPhase? ModelEditor;
    //public static StageEditorPhase? StageEditor;

    private static DirectionalLight light;
    
    private static MicroStopwatch timer;
    public static UnlimitedArray<Car> cars;
    public static UnlimitedArray<Car> vendor_cars;
    public static UnlimitedArray<Car> user_cars;
    public static UnlimitedArray<Mesh> stage_parts;
    public static UnlimitedArray<Mesh> vendor_stage_parts;
    public static UnlimitedArray<Mesh> user_stage_parts;
    public static Mesh error_mesh;
    
    public static bool devRenderTrackers = false;
    
    public static DevConsole devConsole = new();

    public static SettingsMenu SettingsMenu;

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

    public static DevConsoleWriter Writer;

    static GameSparker()
    {
        var originalOut = Console.Out;
        Writer = new DevConsoleWriter(devConsole, originalOut);
        Console.SetOut(Writer);
    }

    /////////////////////////////////

    public static Dictionary<Keys, bool> DebugKeyStates = new();

    public static void KeyPressed(Keys key)
    {
        DebugKeyStates[key] = true;

        var bindings = SettingsMenu.Bindings;
        
        if (key == bindings.ToggleDevConsole)
        {
            devConsole.Toggle();
        }
    }

    public static void KeyReleased(Keys key)
    {
        DebugKeyStates[key] = false;
        
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

    public static (int Id, Car? Car) GetCar(string name)
    {
        IReadOnlyList<Car>[] arrays = [cars, vendor_cars, user_cars];

        var total = 0;
        foreach (var t in arrays)
        {
            foreach (var car in t)
            {
                if (string.Equals(car.FileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (total, car);
                }

                total++;
            }
        }

        Console.WriteLine("No results for GetCar");
        return (-1, null!);
    }

    public static (int Id, Mesh? Mesh) GetStagePart(string name)
    {
        IReadOnlyList<Mesh>[] arrays = [stage_parts, vendor_stage_parts, user_stage_parts];

        var total = 0;
        foreach (var t in arrays)
        {
            foreach (var part in t)
            {
                if (string.Equals(part.FileName, name, StringComparison.OrdinalIgnoreCase))
                {
                    return (total, part);
                }

                total++;
            }
        }

        Console.WriteLine("No results for GetStagePart");
        return (-1, null!);
    }

    public static string GetModelName(int index, bool forCar = false)
    {
        var models = forCar ? CarRads : StageRads;
        
        if (index >= 0 && index < models.Length)
        {
            return models[index];
        }
        
        return "";
    }

    public static void Load(Program game)
    {
        _game = game;
        _graphicsDevice = game.GraphicsDevice;

        SfxLibrary.LoadSounds();

        timer = new MicroStopwatch();
        timer.Start();
        
        cars = [];
        vendor_cars = [];
        user_cars = [];
        
        stage_parts = [];
        vendor_stage_parts = [];
        user_stage_parts = [];

        FileUtil.LoadFiles("./data/models/nfmm/cars", CarRads, (ais, id, fileName) => {
            cars[id] = new Car(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "nfmm/" + fileName);
        });

        FileUtil.LoadFiles("./data/models/nfmm/stage", StageRads, (ais, id, fileName) => {
            stage_parts[id] = new Mesh(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "nfmm/" + fileName);
        });
        
        FileUtil.LoadFiles("./data/models/nfmw/cars", (ais, fileName) => {
            vendor_cars.Add(new Car(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "nfmw/" + fileName));
        });

        FileUtil.LoadFiles("./data/models/elo/cars", (ais, fileName) => {
            vendor_cars.Add(new Car(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "nfmw/" + fileName));
        });
        
        FileUtil.LoadFiles("./data/models/nfmw/stage", (ais, fileName) => {
            vendor_stage_parts.Add(new Mesh(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "nfmw/" + fileName));
        });
        
        FileUtil.LoadFiles("./data/models/user/cars", (ais, fileName) => {
            try
            {
                user_cars.Add(new Car(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "user/" + fileName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user car '{fileName}': {ex.Message}\n{ex.StackTrace}");
            }
        });
        
        FileUtil.LoadFiles("./data/models/user/stage", (ais, fileName) => {
            try
            {
                user_stage_parts.Add(new Mesh(game.GraphicsDevice, RadParser.ParseRad(Encoding.UTF8.GetString(ais)), "user/" + fileName));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading user stage part '{fileName}': {ex.Message}\n{ex.StackTrace}");
            }
        });

        error_mesh = new Mesh(
            game.GraphicsDevice,
            RadParser.ParseRad(Encoding.UTF8.GetString(System.IO.File.ReadAllBytes("./data/models/error.rad"))),
            "error.rad"
        );

        // init menu
        SettingsMenu = new SettingsMenu(game);
        MainMenu = new MainMenuPhase(_graphicsDevice);
        SettingsMenu.LoadConfig();

        InRace = new InRacePhase(_graphicsDevice);
        CurrentPhase = MainMenu;

        // Initialize ModelEditor after cars are loaded
        ModelEditor = new ModelEditorPhase(_graphicsDevice);
        
        //StageEditor = new StageEditorPhase(_graphicsDevice);
        
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

    public static void StartModelViewer()
    {
        CurrentPhase = ModelEditor;
    }
    
    public static void ExitModelViewer()
    {
        CurrentPhase = MainMenu;
        devRenderTrackers = false;
    }

    public static void StartStageEditor()
    {
        //CurrentPhase = StageEditor;
    }
    
    public static void ReturnToMainMenu()
    {
        CurrentPhase = MainMenu;
    }

    public static void StartGame()
    {
        // temp
        CurrentPhase = InRace;

        Console.WriteLine("Game started!");
    }

    public static void GameTick()
    {
        World.GameTick();
        FrameTrace.ClearMessages();
    }

    public static void Render()
    {
    }

    public static void RenderImgui()
    {
        devConsole.Render();
        MessageWindow.Render();
        SettingsMenu.Render();
    }

    public static void WindowSizeChanged(int width, int height)
    {
    }
}
