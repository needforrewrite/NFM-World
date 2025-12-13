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
    private static GraphicsDevice _graphicsDevice;
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
    public static UnlimitedArray<Mesh> placed_stage_elements;

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

    // stage loading
    private static int _indexOffset = 10;
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
        placed_stage_elements = [];

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

        Loadstage("nfm2/15_dwm");
        cars_in_race[playerCarIndex] = new Car(new Stat(14), 14, cars[14], 0, 0);
        
        for (var i = 0; i < cars.Count; i++)
        {
            Console.WriteLine($"car {new Stat(i).Names}: {new Stat(i).Score:0}");
        }
        
        Console.WriteLine("Game started!");
    }

    internal static int Getint(string astring, string string4, int i)
    {
        return Utility.GetInt(astring, string4, i);
    }


    public static void CreateObject(string objectName, int x, int y, int z, int r)
    {
        var model = GetModel(objectName);
        if (model == -1)
        {
            devConsole.Log($"Object '{objectName}' not found.", "warning");
            return;
        }

        placed_stage_elements[_stagePartCount] = new Mesh(
            stage_parts[model],
            new Vector3(x, 250 - y, z),
            new Euler(AngleSingle.FromDegrees(r), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
        );

        _stagePartCount++;

        devConsole.Log($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}", "info");
    }

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public static void Loadstage(string stage)
    {
        placed_stage_elements.Clear();
        _stagePartCount = 0;
        Trackers.Nt = 0;

        var i = 0;
        var k = 100;
        var l = 0;
        var m = 100;
        var astring = "";
        try
        {
            //var customStagePath = "stages/" + CheckPoints.Stage + ".txt";
            var customStagePath = "data/stages/" + stage + ".txt";
            foreach (var line in System.IO.File.ReadAllLines(customStagePath))
            {
                astring = "" + line.Trim();
                if (astring.StartsWith("snap"))
                {
                    World.Snap = new Color3(
                        (short) Getint("snap", astring, 0),
                        (short) Getint("snap", astring, 1),
                        (short) Getint("snap", astring, 2)
                    );
                }
                if (astring.StartsWith("sky"))
                {
                    // Medium.Setsky(Getint("sky", astring, 0), Getint("sky", astring, 1), Getint("sky", astring, 2));
                }
                if (astring.StartsWith("ground"))
                {
                    // Medium.Setgrnd(Getint("ground", astring, 0), Getint("ground", astring, 1),
                    //     Getint("ground", astring, 2));
                }
                if (astring.StartsWith("polys"))
                {
                    if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        // Medium.drawPolys = false;
                    }
                    else
                    {
                        // Medium.Setpolys(Getint("polys", astring, 0), Getint("polys", astring, 1),
                        // Getint("polys", astring, 2));
                    }
                }
                if (astring.StartsWith("fog"))
                {
                    World.Fog = new Color3(
                        (short) Getint("fog", astring, 0),
                        (short) Getint("fog", astring, 1),
                        (short) Getint("fog", astring, 2)
                    );
                    // Medium.Setfade(Getint("fog", astring, 0), Getint("fog", astring, 1), Getint("fog", astring, 2));
                }
                if (astring.StartsWith("texture"))
                {
                    //Medium.Setexture(Getint("texture", astring, 0), Getint("texture", astring, 1),
                    //    Getint("texture", astring, 2), Getint("texture", astring, 3));
                }
                if (astring.StartsWith("clouds"))
                {
                    if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        // Medium.drawClouds = false;
                    }
                    else
                    {
                        // Medium.Setclouds(Getint("clouds", astring, 0), Getint("clouds", astring, 1),
                        //     Getint("clouds", astring, 2), Getint("clouds", astring, 3), Getint("clouds", astring, 4));
                    }
                }
                if (astring.StartsWith("density"))
                {
                    var fogd = (Getint("density", astring, 0) + 1) * 2 - 1;
                    fogd = Math.Clamp(fogd, 1, 30);
                    World.Density = UMath.InverseLerp(1, 30, fogd);
                    //Medium.Fogd = (Getint("density", astring, 0) + 1) * 2 - 1;
                    //if (Medium.Fogd < 1)
                    //{
                    //    Medium.Fogd = 1;
                    //}
                    //if (Medium.Fogd > 30)
                    //{
                    //    Medium.Fogd = 30;
                    //}
                }
                if (astring.StartsWith("fadefrom"))
                {
                    World.FadeFrom = Getint("fadefrom", astring, 0);
                    // Medium.Fadfrom(Getint("fadefrom", astring, 0));
                }
                if (astring.StartsWith("lightson"))
                {
                    // Medium.Lightson = true;
                }
                if (astring.StartsWith("mountains"))
                {
                    // Check for mountains(false) first
                    // if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     Medium.drawMountains = false;
                    // }
                    // else
                    // {
                    //     Medium.Mgen = Getint("mountains", astring, 0);
                    // }
                }
                if (astring.StartsWith("set"))
                {
                    var setindex = Getint("set", astring, 0);
                    setindex -= _indexOffset;
                    var setheight = World.Ground - stage_parts[setindex].GroundAt;
                    
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        setheight = Getint("set", astring, 4);
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[setindex],
                            new Vector3(Getint("set", astring, 1), setheight, Getint("set", astring, 2)),
                            new Euler(AngleSingle.FromDegrees(Getint("set", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle));
                    }
                    else
                    {
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[setindex],
                            new Vector3(Getint("set", astring, 1), setheight, Getint("set", astring, 2)),
                            new Euler(AngleSingle.FromDegrees(Getint("set", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle));
                    }
                    if (astring.Contains(")p"))     //AI tags
                    {
                        // CheckPoints.X[CheckPoints.N] = Getint("set", astring, 1);
                        // CheckPoints.Z[CheckPoints.N] = Getint("set", astring, 2);
                        // CheckPoints.Y[CheckPoints.N] = 0;
                        // CheckPoints.Typ[CheckPoints.N] = 0;
                        // if (astring.Contains(")pt"))
                        // {
                        //     CheckPoints.Typ[CheckPoints.N] = -1;
                        // }
                        // if (astring.Contains(")pr"))
                        // {
                        //     CheckPoints.Typ[CheckPoints.N] = -2;
                        // }
                        // if (astring.Contains(")po"))
                        // {
                        //     CheckPoints.Typ[CheckPoints.N] = -3;
                        // }
                        // if (astring.Contains(")ph"))
                        // {
                        //     CheckPoints.Typ[CheckPoints.N] = -4;
                        // }
                        // if (astring.Contains("aout"))
                        // {
                        //     Console.WriteLine("aout: " + CheckPoints.N);
                        // }
                        // CheckPoints.N++;
                        // _notb = _nob + 1;
                    }
                    _stagePartCount++;
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }
                }
                if (astring.StartsWith("chk"))
                {
                    var chkindex = Getint("chk", astring, 0);
                    chkindex -= _indexOffset;
                    var chkheight = World.Ground - stage_parts[chkindex].GroundAt;


                    // Check if optional Y coordinate is provided (5 parameters instead of 4)
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        chkheight = Getint("chk", astring, 4);
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[chkindex],
                            new Vector3(Getint("chk", astring, 1), chkheight, Getint("chk", astring, 2)),
                            new Euler(AngleSingle.FromDegrees(Getint("chk", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
                        );
                    }
                    else
                    {
	                    placed_stage_elements[_stagePartCount] = new Mesh(
	                        stage_parts[chkindex],
	                        new Vector3(Getint("set", astring, 1), chkheight, Getint("set", astring, 2)),
	                        new Euler(AngleSingle.FromDegrees(Getint("set", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
	                    );
                    }
                    
                    // CheckPoints.X[CheckPoints.N] = Getint("chk", astring, 1);
                    // CheckPoints.Z[CheckPoints.N] = Getint("chk", astring, 2);
                    // CheckPoints.Y[CheckPoints.N] = chkheight;
                    // if (Getint("chk", astring, 3) == 0)
                    // {
                    //     CheckPoints.Typ[CheckPoints.N] = 1;
                    // }
                    // else
                    // {
                    //     CheckPoints.Typ[CheckPoints.N] = 2;
                    // }
                    // CheckPoints.Pcs = CheckPoints.N;
                    // CheckPoints.N++;
                    //stage_parts[_stagePartCount].Checkpoint = CheckPoints.Nsp + 1;
                    //CheckPoints.Nsp++;
                    _stagePartCount++;
                }
                if (astring.StartsWith("fix"))
                {
                    var fixindex = Getint("fix", astring, 0);
                    fixindex -= _indexOffset;
                    placed_stage_elements[_stagePartCount] = new FixHoop(
                        stage_parts[fixindex],
                        new Vector3(Getint("set", astring, 1), Getint("set", astring, 3), Getint("set", astring, 2)),
                        new Euler(AngleSingle.FromDegrees(Getint("set", astring, 4)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                    );
                    // CheckPoints.Fx[CheckPoints.Fn] = Getint("fix", astring, 1);
                    // CheckPoints.Fz[CheckPoints.Fn] = Getint("fix", astring, 2);
                    // CheckPoints.Fy[CheckPoints.Fn] = Getint("fix", astring, 3);
                    if (Getint("fix", astring, 4) != 0)
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = true;
                        ((FixHoop)placed_stage_elements[_stagePartCount]).Rotated = true;
                    }
                    else
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = false;
                    }
                    //CheckPoints.Special[CheckPoints.Fn] = astring.IndexOf(")s") != -1;
                    //CheckPoints.Fn++;
                    _stagePartCount++;
                }
                // oteek: FUCK PILES IM NGL
                // if (!CheckPoints.Notb && astring.StartsWith("pile"))
                // {
                //     _stageContos[_nob] = new ContO(Getint("pile", astring, 0), Getint("pile", astring, 1),
                //         Getint("pile", astring, 2), Getint("pile", astring, 3), Getint("pile", astring, 4),
                //         Medium.Ground);
                //     _nob++;
                // }
                if (astring.StartsWith("nlaps"))
                {
                    //CheckPoints.Nlaps = Getint("nlaps", astring, 0);
                }
                if (astring.StartsWith("name"))
                {
                    //CheckPoints.Name = Getastring("name", astring, 0).Replace('|', ',');
                }
                if (astring.StartsWith("stagemaker"))
                {
                    //CheckPoints.Maker = Getastring("stagemaker", astring, 0);
                }
                if (astring.StartsWith("publish"))
                {
                    //CheckPoints.Pubt = Getint("publish", astring, 0);
                }
                if (astring.StartsWith("soundtrack"))
                {
                    
                }

                // stage walls
                var wall = GetModel("thewall");
                if (astring.StartsWith("maxr"))
                {
                    var n = Getint("maxr", astring, 0);
                    var o = Getint("maxr", astring, 1);
                    i = o;
                    var p = Getint("maxr", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[wall],
                            new Vector3(o, World.Ground, q * 4800 + p),
                            Euler.Identity                        
                        );
                        _stagePartCount++;
                    }
                    Trackers.Y[Trackers.Nt] = -5000;
                    Trackers.Rady[Trackers.Nt] = 7100;
                    Trackers.X[Trackers.Nt] = o + 500;
                    Trackers.Radx[Trackers.Nt] = 600;
                    Trackers.Z[Trackers.Nt] = n * 4800 / 2 + p - 2400;
                    Trackers.Radz[Trackers.Nt] = n * 4800 / 2;
                    Trackers.Xy[Trackers.Nt] = 90;
                    Trackers.Zy[Trackers.Nt] = 0;
                    Trackers.Dam[Trackers.Nt] = 167;
                    Trackers.Decor[Trackers.Nt] = false;
                    Trackers.Skd[Trackers.Nt] = 0;
                    Trackers.Nt++;
                }
                if (astring.StartsWith("maxl"))
                {
                    var n = Getint("maxl", astring, 0);
                    var o = Getint("maxl", astring, 1);
                    k = o;
                    var p = Getint("maxl", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[wall],
                            new Vector3(o, World.Ground, q * 4800 + p),
                            new Euler(AngleSingle.FromDegrees(180), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
                        _stagePartCount++;
                    }
                    Trackers.Y[Trackers.Nt] = -5000;
                    Trackers.Rady[Trackers.Nt] = 7100;
                    Trackers.X[Trackers.Nt] = o - 500;
                    Trackers.Radx[Trackers.Nt] = 600;
                    Trackers.Z[Trackers.Nt] = n * 4800 / 2 + p - 2400;
                    Trackers.Radz[Trackers.Nt] = n * 4800 / 2;
                    Trackers.Xy[Trackers.Nt] = -90;
                    Trackers.Zy[Trackers.Nt] = 0;
                    Trackers.Dam[Trackers.Nt] = 167;
                    Trackers.Decor[Trackers.Nt] = false;
                    Trackers.Skd[Trackers.Nt] = 0;
                    Trackers.Nt++;
                }
                if (astring.StartsWith("maxt"))
                {
                    var n = Getint("maxt", astring, 0);
                    var o = Getint("maxt", astring, 1);
                    l = o;
                    var p = Getint("maxt", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[wall],
                            new Vector3(q * 4800 + p, World.Ground, o),
                            new Euler(AngleSingle.FromDegrees(90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
                        _stagePartCount++;
                    }
                    Trackers.Y[Trackers.Nt] = -5000;
                    Trackers.Rady[Trackers.Nt] = 7100;
                    Trackers.Z[Trackers.Nt] = o + 500;
                    Trackers.Radz[Trackers.Nt] = 600;
                    Trackers.X[Trackers.Nt] = n * 4800 / 2 + p - 2400;
                    Trackers.Radx[Trackers.Nt] = n * 4800 / 2;
                    Trackers.Zy[Trackers.Nt] = 90;
                    Trackers.Xy[Trackers.Nt] = 0;
                    Trackers.Dam[Trackers.Nt] = 167;
                    Trackers.Decor[Trackers.Nt] = false;
                    Trackers.Skd[Trackers.Nt] = 0;
                    Trackers.Nt++;
                }
                if (astring.StartsWith("maxb"))
                {
                    var n = Getint("maxb", astring, 0);
                    var o = Getint("maxb", astring, 1);
                    m = o;
                    var p = Getint("maxb", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        placed_stage_elements[_stagePartCount] = new Mesh(
                            stage_parts[wall],
                            new Vector3(q * 4800 + p, World.Ground, o),
                            new Euler(AngleSingle.FromDegrees(-90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
                        _stagePartCount++;
                    }
                    Trackers.Y[Trackers.Nt] = -5000;
                    Trackers.Rady[Trackers.Nt] = 7100;
                    Trackers.Z[Trackers.Nt] = o - 500;
                    Trackers.Radz[Trackers.Nt] = 600;
                    Trackers.X[Trackers.Nt] = n * 4800 / 2 + p - 2400;
                    Trackers.Radx[Trackers.Nt] = n * 4800 / 2;
                    Trackers.Zy[Trackers.Nt] = -90;
                    Trackers.Xy[Trackers.Nt] = 0;
                    Trackers.Dam[Trackers.Nt] = 167;
                    Trackers.Decor[Trackers.Nt] = false;
                    Trackers.Skd[Trackers.Nt] = 0;
                    Trackers.Nt++;
                }
            }
            // Medium.Newpolys(k, i - k, m, l - m, _stagePartCount);
            // Medium.Newmountains(k, i, m, l);
            // Medium.Newclouds(k, i, m, l);
            // Medium.Newstars();
            Trackers.LoadTrackers(placed_stage_elements, k, i - k, m, l - m);
        }
        catch (Exception exception)
        {
            Writer.WriteLine("Error in stage: " + stage, "error");
            Writer.WriteLine("At line: " + astring, "error");
            Writer.WriteLine(exception.ToString(), "error");

            // At least for debugging we want this to crash the game
            throw;
        }
        GC.Collect();
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
        
        foreach (var element in placed_stage_elements)
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
        foreach (var element in placed_stage_elements)
        {
            element.Render(camera, lightCamera, isCreateShadowMap);
        }

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
