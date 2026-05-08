using System.Runtime.CompilerServices;
using MessagePack;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary;

[MessagePackObject]
public readonly record struct PiecePlacement(
    [property: Key(0)] PiecePlacementType Type,
    [property: Key(1)] Rad3d Object,
    [property: Key(2)] f64Vector3 Position,
    [property: Key(3)] f64Euler Rotation,
    [property: Key(4)] AiNodeKind? NodeKind = null,
    [property: Key(5)] bool IsSpecial = false
);

public enum PiecePlacementType : byte
{
    CollisionObject,
    CheckPoint,
    FixHoop
}

[MessagePackObject]
public class StageLoader
{
    [Key(0)] public readonly string Path;

    [Key(1)] public ushort nlaps = 3;

    // soundtrack(folder,fileName)
    [Key(2)] public string musicPath = "";

    // soundtrackremaster(folder,fileName)
    [Key(3)] public string remasteredMusicPath = "";

    // soundtrackfreqmul(mul)
    [Key(4)] public double musicFreqMul = 1.0d;
    [Key(5)] public double musicTempoMul = 0d;
    [Key(6)] public string Name = "hogan rewish";
    [Key(7)] public int indexOffset = 10;

    private bool swapYandRot = false;
    private bool reverseChkY = false;

    // left
    [Key(8)] public int Sx;

    // top
    [Key(9)] public int Sz;

    // width
    [Key(10)] public int Ncx;

    // height
    [Key(11)] public int Ncz;

    [Key(12)] public Color3? Snap;
    [Key(13)] public Color3? Sky;
    [Key(14)] public Color3? GroundColor;
    [Key(15)] public Color3? GroundPolysColor;
    [Key(16)] public bool DrawPolys = true;
    [Key(17)] public Color3? Fog;
    [Key(18)] public InlineArray4<int>? Texture;
    [Key(19)] public InlineArray5<int>? Clouds;
    [Key(20)] public bool DrawClouds = true;
    [Key(21)] public float? CloudCoverage;
    [Key(22)] public int? FogDensity;
    [Key(23)] public int? FadeFrom;
    [Key(24)] public bool LightsOn;
    [Key(25)] public bool DrawMountains = true;
    [Key(26)] public int? MountainSeed;
    [Key(27)] public float? MountainCoverage;
    [Key(28)] public Vector3? LightDirection;
    [Key(29)] public UnlimitedArray<PiecePlacement> pieces = new();
    [Key(30)] public UnlimitedArray<Rad3dBoxDef> walls = new();
    [Key(31)] public int maxr = 0;
    [Key(32)] public int maxl = 100;
    [Key(33)] public int maxt = 0;
    [Key(34)] public int maxb = 100;

    public StageLoader(string stageName)
    {
        Path = stageName;
        //var customStagePath = "stages/" + CheckPoints.Stage + ".txt";
        var customStagePath = System.IO.Path.IsPathRooted(stageName) ? stageName : "data/stages/" + stageName + ".txt";
        var line = "";
        int lineNumber = 0;
        try
        {
            foreach (var aline in System.IO.File.ReadAllLines(customStagePath))
            {
                line = aline.Trim();
                lineNumber++;
                
                
                if (line.StartsWith("snap"))
                {
                    Snap = new Color3(
                        (short)Utility.GetInt("snap", line, 0),
                        (short)Utility.GetInt("snap", line, 1),
                        (short)Utility.GetInt("snap", line, 2)
                    );
                }

                if (line.StartsWith("sky"))
                {
                    Sky = new Color3(
                        (short)Utility.GetInt("sky", line, 0),
                        (short)Utility.GetInt("sky", line, 1),
                        (short)Utility.GetInt("sky", line, 2)
                    );
                }

                if (line.StartsWith("ground"))
                {
                    GroundColor = new Color3(
                        (short)Utility.GetInt("ground", line, 0),
                        (short)Utility.GetInt("ground", line, 1),
                        (short)Utility.GetInt("ground", line, 2)
                    );
                }

                if (line.StartsWith("polys"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawPolys = false;
                    }
                    else
                    {
                        GroundPolysColor = new Color3(
                            (short)Utility.GetInt("polys", line, 0),
                            (short)Utility.GetInt("polys", line, 1),
                            (short)Utility.GetInt("polys", line, 2)
                        );
                    }
                }

                if (line.StartsWith("fog"))
                {
                    Fog = new Color3(
                        (short)Utility.GetInt("fog", line, 0),
                        (short)Utility.GetInt("fog", line, 1),
                        (short)Utility.GetInt("fog", line, 2)
                    );
                }

                if (line.StartsWith("texture"))
                {
                    var texture = new InlineArray4<int>();
                    texture[0] = Utility.GetInt("texture", line, 0);
                    texture[1] = Utility.GetInt("texture", line, 1);
                    texture[2] = Utility.GetInt("texture", line, 2);
                    texture[3] = Utility.GetInt("texture", line, 3);
                    Texture = texture;
                }

                if (line.StartsWith("clouds"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawClouds = false;
                    }
                    else
                    {
                        // Support both single seed value and full cloud parameters
                        var cloudParams = line.Split(',');
                        if (cloudParams.Length == 2) // clouds(seed) format
                        {
                            CloudCoverage = Utility.GetInt("clouds", line, 0);
                        }
                        else // clouds(param1,param2,...) format
                        {
                            var clouds = new InlineArray5<int>();
                            clouds[0] = Utility.GetInt("clouds", line, 0);
                            clouds[1] = Utility.GetInt("clouds", line, 1);
                            clouds[2] = Utility.GetInt("clouds", line, 2);
                            clouds[3] = Utility.GetInt("clouds", line, 3);
                            clouds[4] = Utility.GetInt("clouds", line, 4);
                            Clouds = clouds;
                        }
                    }
                }

                if (line.StartsWith("cloudcoverage"))
                {
                    CloudCoverage = Utility.GetFloat("cloudcoverage", line, 0);
                }

                if (line.StartsWith("density"))
                {
                    FogDensity = (Utility.GetInt("density", line, 0) + 1) * 2 - 1;
                    if (FogDensity < 1)
                    {
                        FogDensity = 1;
                    }
                    if (FogDensity > 30)
                    {
                        FogDensity = 30;
                    }
                }

                if (line.StartsWith("fadefrom"))
                {
                    FadeFrom = Utility.GetInt("fadefrom", line, 0);
                }

                if (line.StartsWith("distfog"))
                {
                    FadeFrom = Utility.GetInt("distfog", line, 0);
                }

                if (line.StartsWith("lightson"))
                {
                    LightsOn = true;
                }

                if (line.StartsWith("mountains"))
                {
                    // Check for mountains(false) first
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawMountains = false;
                    }
                    else
                    {
                        MountainSeed = Utility.GetInt("mountains", line, 0);
                    }
                }

                if (line.StartsWith("mountaincoverage"))
                {
                    MountainCoverage = Utility.GetFloat("mountaincoverage", line, 0);
                }

                if (line.StartsWith("lightdir"))
                {
                    LightDirection = new Vector3(
                        Utility.GetFloat("lightdir", line, 0),
                        Utility.GetFloat("lightdir", line, 1),
                        Utility.GetFloat("lightdir", line, 2)
                    );
                }

                if (line.StartsWith("modeloffset"))
                {
                    indexOffset = Utility.GetInt("modeloffset", line, 0);
                }

                if (line.StartsWith("swapRotY"))
                {
                    swapYandRot = true;
                }

                if (line.StartsWith("reverseChkY"))
                {
                    reverseChkY = true;
                }

                if (line.StartsWith("set"))
                {
                    if (!TryGetPieceToPlace(Utility.GetString("set", line, 0), out var set)) continue;

                    var setheight = World.Ground;

                    var ymult = -1;
                    
                    var hasCustomY = line.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        if(swapYandRot)
                        {
                            setheight = Utility.GetInt("set", line, 3);
                        }
                        else
                        {
                            setheight = Utility.GetInt("set", line, 4) * ymult + World.Ground;
                        }
                    }

                    var rotPlace = 3;

                    if (swapYandRot)
                    {
                        rotPlace = 4;
                    }

                    var obj = new PiecePlacement(
                        PiecePlacementType.CollisionObject,
                        set,
                        new f64Vector3(Utility.GetInt("set", line, 1), setheight, Utility.GetInt("set", line, 2)),
                        new f64Euler(f64AngleSingle.FromDegrees(Utility.GetInt("set", line, rotPlace)), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle));
                    if (line.Contains(")p"))     //AI tags
                    {
                        obj = obj with { NodeKind = AiNodeKind.Road };
                        if (line.Contains(")pt"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.Turn };
                        }
                        if (line.Contains(")pr"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.Ramp };
                        }
                        if (line.Contains(")po"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.FixRoadStart };
                        }
                        if (line.Contains(")ph"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.Halfpipe };
                        }
                    }
                    pieces.Add(obj);
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }
                }
                if (line.StartsWith("chk"))
                {
                    var ymult = -1;
                    var isAirCheckpoint = false;
                    
                    if (!TryGetPieceToPlace(Utility.GetString("chk", line, 0), out var mesh)) continue;

                    if (mesh.FileName == "nfmm/aircheckpoint")
                    {
                        ymult = 1; // default to inverted Y for stupid rollercoaster chks for compatibility reasons
                        isAirCheckpoint = true;
                    }

                    if (reverseChkY)
                    {
                        ymult = 1;
                    }

                    var chkheight = World.Ground;

                    var rotPlace = 3;
                    if (swapYandRot)
                    {
                        rotPlace = 4;
                    }

                    f64AngleSingle rotation = f64AngleSingle.FromDegrees(Utility.GetInt("chk", line, rotPlace));

                    // Check if optional Y coordinate is provided (5 parameters instead of 4)
                    var hasCustomY = line.Split(',').Length >= 5;

                    if (hasCustomY)
                    {

                        if(swapYandRot)
                        {
                            chkheight = Utility.GetInt("chk", line, 3) * ymult * -1;
                        }
                        else
                        {
                            chkheight = Utility.GetInt("chk", line, 4) * ymult + (isAirCheckpoint ? 0 : World.Ground);
                        }
                    }

                    var obj = new PiecePlacement(
                        PiecePlacementType.CheckPoint,
                        mesh,
                        new f64Vector3(Utility.GetInt("chk", line, 1), chkheight, Utility.GetInt("chk", line, 2)),
                        new f64Euler(rotation, f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                        AiNodeKind.CheckPoint
                    );
                    pieces.Add(obj);
                    
                    // CheckPoints.X[CheckPoints.N] = Utility.GetInt("chk", astring, 1);
                    // CheckPoints.Z[CheckPoints.N] = Utility.GetInt("chk", astring, 2);
                    // CheckPoints.Y[CheckPoints.N] = chkheight;
                    // if (Utility.GetInt("chk", astring, 3) == 0)
                    // {
                    //     CheckPoints.Typ[CheckPoints.N] = 1;
                    // }
                    // else
                    // {
                    //     CheckPoints.Typ[CheckPoints.N] = 2;
                    // }
                    // CheckPoints.Pcs = CheckPoints.N;
                    // CheckPoints.N++;
                    //stage_parts[stagePartCount].Checkpoint = CheckPoints.Nsp + 1;
                    //CheckPoints.Nsp++;
                }
                if (line.StartsWith("fix"))
                {
                    if (!TryGetPieceToPlace(Utility.GetString("set", line, 0), out var mesh)) continue;

                    var fix = new PiecePlacement(
                        PiecePlacementType.FixHoop,
                        mesh,
                        new f64Vector3(Utility.GetInt("fix", line, 1), Utility.GetInt("fix", line, 3), Utility.GetInt("fix", line, 2)),
                        new f64Euler(f64AngleSingle.FromDegrees(Utility.GetInt("fix", line, 4)), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                        AiNodeKind.FixHoop
                    );
                    
                    if (line.EndsWith(")s"))
                    {
                        fix = fix with { IsSpecial = true };
                    }
                    pieces.Add(fix);
                }
                // oteek: FUCK PILES IM NGL
                // if (!CheckPoints.Notb && astring.StartsWith("pile"))
                // {
                //     _stageContos[_nob] = new ContO(Utility.GetInt("pile", astring, 0), Utility.GetInt("pile", astring, 1),
                //         Utility.GetInt("pile", astring, 2), Utility.GetInt("pile", astring, 3), Utility.GetInt("pile", astring, 4),
                //         Medium.Ground);
                //     _nob++;
                // }
                if (line.StartsWith("nlaps"))
                {
                    nlaps = (ushort)Utility.GetInt("nlaps", line, 0);
                }
                if (line.StartsWith("name"))
                {
                    Name = Utility.GetString("name", line, 0);
                }
                if (line.StartsWith("stagemaker"))
                {
                    //CheckPoints.Maker = Getastring("stagemaker", astring, 0);
                }
                if (line.StartsWith("publish"))
                {
                    //CheckPoints.Pubt = Utility.GetInt("publish", astring, 0);
                }
                if (line.StartsWith("soundtrack("))
                {
                    string folder = Utility.GetString("soundtrack", line, 0);
                    string fileName = Utility.GetString("soundtrack", line, 1);

                    if(folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
                    {
                        throw new Exception("Invalid folder or file name in soundtrack() directive");
                    }

                    musicPath = $"{folder}/{fileName}";
                }
                if(line.StartsWith("soundtrackfreqmul"))
                {
                    float mul = Utility.GetFloat("soundtrackfreqmul", line, 0);
                    musicFreqMul = mul;
                }
                if(line.StartsWith("soundtracktempomul"))
                {
                    float mul = Utility.GetFloat("soundtracktempomul", line, 0);
                    musicTempoMul = mul;
                }
                if(line.StartsWith("soundtrackremaster"))
                {
                    string folder = Utility.GetString("soundtrackremaster", line, 0);
                    string fileName = Utility.GetString("soundtrackremaster", line, 1);

                    if(folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
                    {
                        throw new Exception("Invalid folder or file name in soundtrackremaster() directive");
                    }

                    remasteredMusicPath = $"{folder}/{fileName}";
                }

                // stage walls
                if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

                if (line.StartsWith("maxr"))
                {
                    var n = Utility.GetInt("maxr", line, 0);
                    var o = Utility.GetInt("maxr", line, 1);
                    maxr = o;
                    var p = Utility.GetInt("maxr", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(new PiecePlacement(
                            PiecePlacementType.CollisionObject,
                            wall,
                            new f64Vector3(o, World.Ground, q * 4800 + p),
                            f64Euler.Identity                        
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(o + 500, -5000, n * 4800 / 2 + p - 2400),
                        Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                        Xy: 90,
                        Zy: 0,
                        Skid: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                }
                if (line.StartsWith("maxl"))
                {
                    var n = Utility.GetInt("maxl", line, 0);
                    var o = Utility.GetInt("maxl", line, 1);
                    maxl = o;
                    var p = Utility.GetInt("maxl", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(new PiecePlacement(
                            PiecePlacementType.CollisionObject,
                            wall,
                            new f64Vector3(o, World.Ground, q * 4800 + p),
                            new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)       
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(o - 500, -5000, n * 4800 / 2 + p - 2400),
                        Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                        Xy: -90,
                        Zy: 0,
                        Skid: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                }
                if (line.StartsWith("maxt"))
                {
                    var n = Utility.GetInt("maxt", line, 0);
                    var o = Utility.GetInt("maxt", line, 1);
                    maxt = o;
                    var p = Utility.GetInt("maxt", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(new PiecePlacement(
                            PiecePlacementType.CollisionObject,
                            wall,
                            new f64Vector3(q * 4800 + p, World.Ground, o),
                            new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)       
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o + 500),
                        Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                        Xy: 0,
                        Zy: 90,
                        Skid: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                }
                if (line.StartsWith("maxb"))
                {
                    var n = Utility.GetInt("maxb", line, 0);
                    var o = Utility.GetInt("maxb", line, 1);
                    maxb = o;
                    var p = Utility.GetInt("maxb", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(new PiecePlacement(
                            PiecePlacementType.CollisionObject,
                            wall,
                            new f64Vector3(q * 4800 + p, World.Ground, o),
                            new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)       
                        ));
                    }
                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o - 500),
                        Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                        Xy: 180,
                        Zy: -90,
                        Skid: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                }
            }
        }
        catch (Exception ex)
        {
            throw new StageLoadException(line, lineNumber, ex);
        }
    }

    public StageLoader()
    {
        // Create an empty stage loader for editor purposes
        Path = "default_stage";
    }

    private bool TryGetPieceToPlace(string setstring, out Rad3d mesh)
    {
        if (int.TryParse(setstring, out var setindex))
        {
            setindex -= indexOffset;
            mesh = BackendGameSparker.stage_parts[setindex];
            if (mesh == null!)
            {
                SentrySdk.CaptureMessage($"Stage part '{setstring}' not found.");
                Logging.Error($"Stage part '{setstring}' not found.");
                mesh = BackendGameSparker.error_mesh;
            }
            return true;
        }
        else
        {
            var stagePart = BackendGameSparker.GetStagePart(setstring);
            if (stagePart.Rad == null)
            {
                SentrySdk.CaptureMessage($"Stage part '{setstring}' not found.");
                Logging.Error($"Stage part '{setstring}' not found.");
                mesh = BackendGameSparker.error_mesh;
                return true;
            }
            mesh = stagePart.Rad;
            return true;
        }
    }
}

public class StageLoadException(string line, int lineNumber, Exception exception)
    : Exception($"Error loading stage at line {lineNumber}: {line}", exception)
{
    public string Line { get; } = line;
    public int LineNumber { get; } = lineNumber;
}