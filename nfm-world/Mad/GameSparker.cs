using System.Reflection;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Accounts;
using NFMWorld.DriverInterface;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Gameplay;
using NFMWorld.Sfx;
using NFMWorld.UI;
using NFMWorld.UI.Cef;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using Path = System.IO.Path;
using NFMWorld.Sentry;

namespace NFMWorld;

public static partial class GameSparker
{
    public static WorldGame Game = null!;
    public static GraphicsDevice GraphicsDevice = null!;
    public static readonly string version = GetVersionString();
    public static AccountManager AccountManager = new AccountManager();

    /// <summary>
    /// The shared CEF renderer. Set by WorldGame.Initialize(). Phases access
    /// this to register/unregister their <see cref="PhaseBridge"/> instances.
    /// </summary>
    public static CefRenderer? CefRenderer { get; set; }

    private static string GetVersionString()
    {
        var assembly = Assembly.GetExecutingAssembly();
        var attributes = assembly.GetCustomAttributes(typeof(AssemblyInformationalVersionAttribute), false);
        if (attributes.Length > 0 && attributes[0] is AssemblyInformationalVersionAttribute infoVersion)
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
        get => Phases.Current;
    }

    /// <summary>
    /// The phase stack manager. Handles navigation (Push, Pop, Replace)
    /// and deferred disposal of popped phases.
    /// </summary>
    public static PhaseManager Phases { get; } = new();

    /// <summary>
    /// Replaces the current phase with a new one. The old phase is disposed
    /// at end-of-frame via <see cref="PhaseManager.FlushDisposals"/>.
    /// For navigation with back-support, use <see cref="PushPhase"/> instead.
    /// </summary>
    public static void SetPhase(BasePhase phase, bool keepGroup = true, PhaseManager.Group? group = null)
    {
        Phases.Replace(phase, keepGroup, group);
    }

    /// <summary>
    /// Pushes a new phase onto the stack, keeping the current phase alive.
    /// Use <see cref="PopPhase"/> to return to the previous phase.
    /// </summary>
    public static void PushPhase(BasePhase phase, PhaseManager.Group? group = null)
    {
        Phases.Push(phase, group);
    }

    /// <summary>
    /// Pops the current phase and returns to the previous phase on the stack.
    /// The popped phase is disposed at end-of-frame.
    /// </summary>
    public static void PopPhase()
    {
        Phases.Pop();
    }

    /// <summary>
    /// Pops a phase group and returns to the phase preceding the group on the stack.
    /// If the phase group is not on the top of the phase stack, nothing is popped.
    /// The popped phases are disposed at end-of-frame.
    /// </summary>
    public static void PopGroup(PhaseManager.Group group)
    {
        Phases.PopGroup(group);
    }

    /// <summary>
    /// Pops all phases above the root, returning to the root phase (typically MainMenu).
    /// All intermediate phases are disposed at end-of-frame.
    /// </summary>
    public static void PopToRoot()
    {
        Phases.PopToRoot();
    }

    public static IRadicalMusic? CurrentMusic
    {
        get;
        set
        {
            field?.SetPaused(true);

            field = value;

            if (field != null)
            {
                field.SetVolume(IRadicalMusic.CurrentVolume);
                field.Play();
            }
        }
    }
    /// <summary>
    /// Use remastered music (soundtrackremaster in stage files) where available.
    /// </summary>
    public static bool UseRemasteredMusic = false;

    public static MessageWindow MessageWindow = new();

    public static Dictionary<Rad3d, Mesh> stage_part_meshes = new(Rad3d.VisualEqualityComparer.Instance);
    public static Mesh error_mesh = null!;
    
    public static bool devRenderTrackers = false;
    
    public static DevConsole devConsole = new();

    public static SettingsMenu SettingsMenu;

    /////////////////////////////////

    public static Dictionary<Key, bool> DebugKeyStates = new();
    public static MainMenuPhase MainMenuPhase;

    public static void KeyPressed(Key key)
    {
        DebugKeyStates[key] = true;

        var bindings = SettingsMenu.Bindings;
        
        if (key == bindings.ToggleDevConsole)
        {
            devConsole.Toggle();
        }
    }

    public static void KeyReleased(Key key)
    {
        DebugKeyStates[key] = false;
        
    }

    public static List<string> GetAvailableStages()
    {
        var stages = new List<string>();
        var stagesPath = "data/stages";
        
        if (VFS.DirectoryExists(stagesPath))
        {
            // recursive search
            foreach (var file in VFS.EnumerateFiles(stagesPath, "*.txt", SearchOption.AllDirectories))
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
                var aParts = DigitSplit.Split(aSegments[seg])
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();
                var bParts = DigitSplit.Split(bSegments[seg])
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToArray();

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
    public static void Load(WorldGame game)
    {
        Game = game;
        GraphicsDevice = game.GraphicsDevice;

        foreach (var stageParts in (Span<UnlimitedArray<Rad3d>>)[BackendGameSparker.stage_parts, BackendGameSparker.vendor_stage_parts, BackendGameSparker.user_stage_parts])
        foreach (var stagePart in stageParts)
        {
            try
            {
                var mesh = new Mesh(GraphicsDevice, stagePart);
                stage_part_meshes[stagePart] = mesh;
            }
            catch (Exception ex)
            {
                SentrySdk.CaptureException(ex);
                Logging.Debug($"Error creating mesh for stage part '{stagePart.FileName}': {ex.Message}\n{ex.StackTrace}");
            }
        }
        
        error_mesh = new Mesh(GraphicsDevice, BackendGameSparker.error_mesh);

        SfxLibrary.LoadSounds();

        // init menu
        SettingsMenu = new SettingsMenu(game);
        PhaseSharedState.SelectedStageName = "nfm2/16_4dv";
        MainMenuPhase = new MainMenuPhase(GraphicsDevice, PhaseSharedState.SelectedStageName);

        Phases.SetRoot(MainMenuPhase);
    }
    
    public static Mesh GetStagePartMesh(Rad3d stagePart)
    {
        ref var mesh = ref CollectionsMarshal.GetValueRefOrAddDefault(stage_part_meshes, stagePart, out var exists);
        if (exists)
        {
            return mesh!;
        }

        return mesh = new Mesh(GraphicsDevice, stagePart);
    }

    public static void StartModelViewer()
    {
        PushPhase(new ModelEditorPhase(GraphicsDevice));
    }
    
    public static void ExitEditor()
    {
        PopPhase();
        devRenderTrackers = false;
    }

    public static void StartStageEditor()
    {
        PushPhase(new StageEditorPhase(GraphicsDevice));
    }

    public static void ReturnToMainMenu()
    {
        PopPhase();
    }

    public static void GameTick()
    {
        World.GameTick();
        FrameTrace.ClearMessages();
    }

    public static void Render()
    {
    }

    public static void Render3DOverlays()
    {
        CurrentPhase.Render3DOverlays();
    }

    public static void RenderImgui()
    {
        devConsole.Render();
        MessageWindow.Render();
        SettingsMenu.Render();
        CurrentPhase.RenderImgui();
    }

    public static void WindowSizeChanged(int width, int height)
    {
        SettingsMenu.RegisterResolution(width, height);
    }

    [System.Text.RegularExpressions.GeneratedRegex(@"(\d+)")]
    private static partial System.Text.RegularExpressions.Regex DigitSplit { get; }
}
