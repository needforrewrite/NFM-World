using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world_library.util;
using nfm_world.driverinterface;
using nfm_world.gameplay;
using nfm_world.mesh;
using nfm_world.sfx;
using nfm_world.ui;
using nfm_world.util;
using Path = System.IO.Path;
using nfm_world.mad.account;

namespace nfm_world;

public class GameSparker
{
    public static Program _game;
    public static GraphicsDevice _graphicsDevice;
    public static readonly string version = GetVersionString();
    public static AccountManager AccountManager = new AccountManager();

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
        private set;
    }

    public static void SetPhase(BasePhase phase)
    {
        CurrentPhase?.Exit();
        CurrentPhase = phase;
        CurrentPhase?.Enter();
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
    public static StageEditorPhase? StageEditor;

    private static DirectionalLight light;
    
    private static MicroStopwatch timer;

    public static Dictionary<Rad3d, Mesh> stage_part_meshes = new(Rad3d.VisualEqualityComparer.Instance);
    public static Mesh error_mesh;
    
    public static bool devRenderTrackers = false;
    
    public static DevConsole devConsole = new();

    public static SettingsMenu SettingsMenu;

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
    public static void Load(Program game)
    {
        _game = game;
        _graphicsDevice = game.GraphicsDevice;

        foreach (var stageParts in (Span<UnlimitedArray<Rad3d>>)[BackendGameSparker.stage_parts, BackendGameSparker.vendor_stage_parts, BackendGameSparker.user_stage_parts])
        foreach (var stagePart in stageParts)
        {
            try
            {
                var mesh = new Mesh(_graphicsDevice, stagePart);
                stage_part_meshes[stagePart] = mesh;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating mesh for stage part '{stagePart.FileName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        error_mesh = new Mesh(_graphicsDevice, BackendGameSparker.error_mesh);

        SfxLibrary.LoadSounds();

        timer = new MicroStopwatch();
        timer.Start();
        
        // init menu
        SettingsMenu = new SettingsMenu(game);
        MainMenu = new MainMenuPhase(_graphicsDevice);
        SettingsMenu.LoadConfig();

        InRace = new InRacePhase(_graphicsDevice);
        SetPhase(MainMenu);

        // Initialize ModelEditor after cars are loaded
        ModelEditor = new ModelEditorPhase(_graphicsDevice);
        
        StageEditor = new StageEditorPhase(_graphicsDevice);
    }
    
    public static Mesh GetStagePartMesh(Rad3d stagePart)
    {
        ref var mesh = ref CollectionsMarshal.GetValueRefOrAddDefault(stage_part_meshes, stagePart, out var exists);
        if (exists)
        {
            return mesh!;
        }

        return mesh = new Mesh(_graphicsDevice, stagePart);
    }

    public static void StartModelViewer()
    {
        SetPhase(ModelEditor);
    }
    
    public static void ExitEditor()
    {
        SetPhase(MainMenu);
        devRenderTrackers = false;
    }

    public static void StartStageEditor()
    {
        SetPhase(StageEditor);
    }
    
    public static void ReturnToMainMenu()
    {
        SetPhase(MainMenu);
    }

    public static void StartGame()
    {
        // temp
        SetPhase(InRace);

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
