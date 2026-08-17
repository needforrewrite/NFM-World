using System.Runtime.CompilerServices;
using Maxine.Extensions.Collections;
using MemoryPack;
using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorldLibrary;

[MemoryPackable(GenerateType.VersionTolerant), LuaVisible]
public readonly partial record struct PiecePlacement(
    [property: MemoryPackOrder(0), LuaName] PiecePlacementType Type,
    [property: MemoryPackOrder(1), LuaName] Rad3d Object,
    [property: MemoryPackOrder(2), LuaName] f64Vector3 Position,
    [property: MemoryPackOrder(3), LuaName] f64Euler Rotation,
    [property: MemoryPackOrder(4), LuaName] AiNodeKind? NodeKind = null,
    [property: MemoryPackOrder(5), LuaName] bool IsSpecial = false,
    [property: MemoryPackOrder(6), LuaName] bool IsWall = false
);

[LuaVisible]
public enum PiecePlacementType : byte
{
    CollisionObject,
    CheckPoint,
    FixHoop
}

// count = n parameter
// position = o parameter
// offset = p parameter
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible]
public partial record StageWall(
    [property: MemoryPackOrder(0), LuaName] WallDirection Direction,
    [property: MemoryPackOrder(1), LuaName] int Count,
    [property: MemoryPackOrder(2), LuaName] int Position,
    [property: MemoryPackOrder(3), LuaName] int Offset
);

[LuaVisible]
public enum WallDirection : byte
{
    Right,
    Left,
    Top,
    Bottom
}

[MemoryPackable(GenerateType.VersionTolerant), LuaVisible]
public readonly partial record struct HierarchyGroup(
    [property: MemoryPackOrder(0), LuaName] string Name,
    [property: MemoryPackOrder(1), LuaName] UnlimitedArray<PiecePlacement> Pieces,
    // Old group format: #editor_group(Name,x:z,...)
    [property: MemoryPackOrder(2), LuaName] UnlimitedArray<string> CoordinateKeys
);

// colors have to be processed in order, so we provide a list of instructions in order
[MemoryPackable]
[MemoryPackUnion(0, typeof(SnapInstruction))]
[MemoryPackUnion(1, typeof(SkyInstruction))]
[MemoryPackUnion(2, typeof(FogInstruction))]
[MemoryPackUnion(3, typeof(CloudsInstruction))]
[MemoryPackUnion(4, typeof(GroundInstruction))]
[MemoryPackUnion(5, typeof(TextureInstruction))]
[MemoryPackUnion(6, typeof(PolysInstruction))]
[LuaVisible]
public abstract partial record EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record SnapInstruction([property: MemoryPackOrder(0), LuaName] Color3 Color) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record SkyInstruction([property: MemoryPackOrder(0), LuaName] Color3 Color) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record FogInstruction([property: MemoryPackOrder(0), LuaName] Color3 Color) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record CloudsInstruction([property: MemoryPackOrder(0), LuaName] LuaArray<int> Clouds) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record GroundInstruction([property: MemoryPackOrder(0), LuaName] Color3 Color) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record TextureInstruction([property: MemoryPackOrder(0), LuaName] LuaArray<int> Texture) : EnvironmentInstruction;
[MemoryPackable(GenerateType.VersionTolerant), LuaVisible] [method: MemoryPackConstructor] public partial record PolysInstruction([property: MemoryPackOrder(0), LuaName] Color3 Color) : EnvironmentInstruction;

[LuaVisible]
public readonly partial record struct LuaVector3(
    [property: LuaName] float X,
    [property: LuaName] float Y,
    [property: LuaName] float Z)
{
    public static implicit operator Vector3(LuaVector3 vec3) => new(vec3.X, vec3.Y, vec3.Z);
    public static implicit operator LuaVector3(Vector3 vec3) => new(vec3.X, vec3.Y, vec3.Z);
}

[MemoryPackable(GenerateType.CircularReference), LuaVisible]
public partial class StageLoader
{
    [MemoryPackOrder(0), LuaName] public string Path;

    [MemoryPackOrder(1), LuaName] public ushort nlaps = 3;

    // soundtrack(folder,fileName)
    [MemoryPackOrder(2), LuaName] public string musicPath = "";

    // soundtrackremaster(folder,fileName)
    [MemoryPackOrder(3), LuaName] public string remasteredMusicPath = "";

    // soundtrackfreqmul(mul)
    [MemoryPackOrder(4), LuaName] public double musicFreqMul = 1.0d;
    [MemoryPackOrder(5), LuaName] public double musicTempoMul = 1.0d;
    [MemoryPackOrder(6), LuaName] public string Name = "hogan rewish";
    [MemoryPackOrder(7), LuaName] public int indexOffset = 10;

    private bool swapYandRot = false;
    private bool reverseChkY = false;

    // left
    [MemoryPackOrder(8), LuaName] public int Sx;

    // top
    [MemoryPackOrder(9), LuaName] public int Sz;

    // width
    [MemoryPackOrder(10), LuaName] public int Ncx;

    // height
    [MemoryPackOrder(11), LuaName] public int Ncz;

    [MemoryPackOrder(21), LuaName] public float? CloudCoverage;
    [MemoryPackOrder(22), LuaName] public int? FogDensity;
    [MemoryPackOrder(23), LuaName] public int? FadeFrom;
    [MemoryPackOrder(24), LuaName] public bool LightsOn;
    [MemoryPackOrder(25), LuaName] public bool DrawMountains = true;
    [MemoryPackOrder(26), LuaName] public int? MountainSeed;
    [MemoryPackOrder(27), LuaName] public float? MountainCoverage;
    [MemoryPackOrder(28), LuaName] public LuaVector3? LightDirection;
    [MemoryPackOrder(29), LuaName] public LuaUnlimitedArray<PiecePlacement> pieces = new();
    [MemoryPackOrder(30), LuaName] public LuaUnlimitedArray<Rad3dBoxDef> walls = new();
    [MemoryPackOrder(31), LuaName] public int maxr = 0;
    [MemoryPackOrder(32), LuaName] public int maxl = 100;
    [MemoryPackOrder(33), LuaName] public int maxt = 0;
    [MemoryPackOrder(34), LuaName] public int maxb = 100;

    [MemoryPackOrder(35), LuaName] public LuaUnlimitedArray<EnvironmentInstruction> EnvironmentInstructions = new();
    [MemoryPackOrder(36), LuaName] public bool DrawPolys = true;
    [MemoryPackOrder(37), LuaName] public bool DrawClouds = true;

    [MemoryPackIgnore] public UnlimitedArray<StageWall> wallDefs = [];

    [MemoryPackIgnore] public UnlimitedArray<HierarchyGroup> groups = [];
    [MemoryPackIgnore] public HierarchyGroup ungrouped = new("Ungrouped", [], []);
    [MemoryPackIgnore] public HierarchyGroup currentGroup;
    [MemoryPackIgnore] public int UngroupedOrderIndex = -1;

    [MemoryPackIgnore] public UnlimitedArray<string> unknownParameters = [];

    public StageLoader(string stageName)
    {
        currentGroup = ungrouped;

        Path = stageName;
        var customStagePath = $"data/stages/{stageName}.txt";
        var line = "";
        int lineNumber = 0;

        try
        {
            foreach (var aline in VFS.ReadAllLines(customStagePath))
            {
                line = aline.Trim();
                lineNumber++;

                if (line.StartsWith("#editor_group"))
                {
                    HierarchyGroup group;
                    if (!line.Contains(','))
                    {
                        group = new HierarchyGroup(Utility.GetString("#editor_group", line, 0), [], []);
                    }
                    else
                    {
                        // Old format
                        var gparts = line["#editor_group(".Length..^1].Split(',');
                        group = new HierarchyGroup(gparts[0], [], []);
                        if (gparts.Length >= 1)
                        {
                            var keys = gparts.Skip(1).Select(s => s.Trim());
                            group = group with { CoordinateKeys = [..keys] };
                        }
                    }
                    
                    groups.Add(group);
                    currentGroup = group;
                }

                else if (line.StartsWith("#editor_ungrouped_order"))
                {
                    UngroupedOrderIndex = Utility.GetInt("#editor_ungrouped_order", line, 0);
                }
                
                else if (line.StartsWith("snap"))
                {
                    EnvironmentInstructions.Add(new SnapInstruction(new Color3(
                        (short)Utility.GetInt("snap", line, 0),
                        (short)Utility.GetInt("snap", line, 1),
                        (short)Utility.GetInt("snap", line, 2)
                    )));
                }

                else if (line.StartsWith("sky"))
                {
                    EnvironmentInstructions.Add(new SkyInstruction(new Color3(
                        (short)Utility.GetInt("sky", line, 0),
                        (short)Utility.GetInt("sky", line, 1),
                        (short)Utility.GetInt("sky", line, 2)
                    )));
                }

                else if (line.StartsWith("ground"))
                {
                    EnvironmentInstructions.Add(new GroundInstruction(new Color3(
                        (short)Utility.GetInt("ground", line, 0),
                        (short)Utility.GetInt("ground", line, 1),
                        (short)Utility.GetInt("ground", line, 2)
                    )));
                }

                else if (line.StartsWith("polys"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawPolys = false;
                    }
                    else
                    {
                        EnvironmentInstructions.Add(new PolysInstruction(new Color3(
                            (short)Utility.GetInt("polys", line, 0),
                            (short)Utility.GetInt("polys", line, 1),
                            (short)Utility.GetInt("polys", line, 2)
                        )));
                    }
                }

                else if (line.StartsWith("fog"))
                {
                    EnvironmentInstructions.Add(new FogInstruction(new Color3(
                        (short)Utility.GetInt("fog", line, 0),
                        (short)Utility.GetInt("fog", line, 1),
                        (short)Utility.GetInt("fog", line, 2)
                    )));
                }

                else if (line.StartsWith("texture"))
                {
                    var texture = new LuaArray<int>(4)
                    {
                        [0] = Utility.GetInt("texture", line, 0),
                        [1] = Utility.GetInt("texture", line, 1),
                        [2] = Utility.GetInt("texture", line, 2),
                        [3] = Utility.GetInt("texture", line, 3)
                    };
                    EnvironmentInstructions.Add(new TextureInstruction(texture));
                }

                else if (line.StartsWith("clouds"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawClouds = false;
                    }
                    else
                    {
                        // Support both single seed value and full cloud parameters
                        var cloudParams = line.Split(',');
                        if (cloudParams.Length == 1) // clouds(seed) format
                        {
                            CloudCoverage = Utility.GetInt("clouds", line, 0);
                        }
                        else // clouds(param1,param2,...) format
                        {
                            var clouds = new LuaArray<int>(5)
                            {
                                [0] = Utility.GetInt("clouds", line, 0),
                                [1] = Utility.GetInt("clouds", line, 1),
                                [2] = Utility.GetInt("clouds", line, 2),
                                [3] = Utility.GetInt("clouds", line, 3),
                                [4] = Utility.GetInt("clouds", line, 4)
                            };
                            EnvironmentInstructions.Add(new CloudsInstruction(clouds));
                        }
                    }
                }

                else if (line.StartsWith("cloudcoverage"))
                {
                    CloudCoverage = Utility.GetFloat("cloudcoverage", line, 0);
                }

                else if (line.StartsWith("density"))
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

                else if (line.StartsWith("fadefrom"))
                {
                    FadeFrom = Utility.GetInt("fadefrom", line, 0);
                }

                else if (line.StartsWith("distfog"))
                {
                    FadeFrom = Utility.GetInt("distfog", line, 0);
                }

                else if (line.StartsWith("lightson"))
                {
                    LightsOn = true;
                }

                else if (line.StartsWith("mountains"))
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

                else if (line.StartsWith("mountaincoverage"))
                {
                    MountainCoverage = Utility.GetFloat("mountaincoverage", line, 0);
                }

                else if (line.StartsWith("lightdir"))
                {
                    LightDirection = new LuaVector3(
                        Utility.GetFloat("lightdir", line, 0),
                        Utility.GetFloat("lightdir", line, 1),
                        Utility.GetFloat("lightdir", line, 2)
                    );
                }

                else if (line.StartsWith("modeloffset"))
                {
                    indexOffset = Utility.GetInt("modeloffset", line, 0);
                }

                else if (line.StartsWith("swapRotY"))
                {
                    swapYandRot = true;
                }

                else if (line.StartsWith("reverseChkY"))
                {
                    reverseChkY = true;
                }

                else if (line.StartsWith("set"))
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
                        else if (line.Contains(")pr"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.Ramp };
                        }
                        else if (line.Contains(")po"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.FixRoadStart };
                        }
                        else if (line.Contains(")ph"))
                        {
                            obj = obj with { NodeKind = AiNodeKind.Halfpipe };
                        }
                    }
                    else if (line.Contains(")nfmw_CheckPoint")) obj = obj with { NodeKind = AiNodeKind.CheckPoint };
                    else if (line.Contains(")nfmw_Road")) obj = obj with { NodeKind = AiNodeKind.Road };
                    else if (line.Contains(")nfmw_Turn")) obj = obj with { NodeKind = AiNodeKind.Turn };
                    else if (line.Contains(")nfmw_Auto")) obj = obj with { NodeKind = AiNodeKind.Auto };
                    else if (line.Contains(")nfmw_Ramp")) obj = obj with { NodeKind = AiNodeKind.Ramp };
                    else if (line.Contains(")nfmw_Halfpipe")) obj = obj with { NodeKind = AiNodeKind.Halfpipe };
                    else if (line.Contains(")nfmw_SequenceStart")) obj = obj with { NodeKind = AiNodeKind.SequenceStart };
                    else if (line.Contains(")nfmw_SequenceEnd")) obj = obj with { NodeKind = AiNodeKind.SequenceEnd };
                    else if (line.Contains(")nfmw_FixRoadStart")) obj = obj with { NodeKind = AiNodeKind.FixRoadStart };
                    else if (line.Contains(")nfmw_FixRamp")) obj = obj with { NodeKind = AiNodeKind.FixRamp };
                    else if (line.Contains(")nfmw_FixHoop")) obj = obj with { NodeKind = AiNodeKind.FixHoop };
                    else if (line.Contains(")nfmw_FixRoadEnd")) obj = obj with { NodeKind = AiNodeKind.FixRoadEnd };
                    else if (line.Contains(")nfmw_Avoid")) obj = obj with { NodeKind = AiNodeKind.Avoid };
                    else if (line.Contains(")nfmw_Reset")) obj = obj with { NodeKind = AiNodeKind.Reset };
                    pieces.Add(obj);
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }

                    currentGroup.Pieces.Add(obj);
                }
                else if (line.StartsWith("chk"))
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
                    
                    currentGroup.Pieces.Add(obj);
                }
                else if (line.StartsWith("fix"))
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
                    
                    currentGroup.Pieces.Add(fix);
                }
                // oteek: FUCK PILES IM NGL
                // if (!CheckPoints.Notb && astring.StartsWith("pile"))
                // {
                //     _stageContos[_nob] = new ContO(Utility.GetInt("pile", astring, 0), Utility.GetInt("pile", astring, 1),
                //         Utility.GetInt("pile", astring, 2), Utility.GetInt("pile", astring, 3), Utility.GetInt("pile", astring, 4),
                //         Medium.Ground);
                //     _nob++;
                // }
                else if (line.StartsWith("nlaps"))
                {
                    nlaps = (ushort)Utility.GetInt("nlaps", line, 0);
                }
                else if (line.StartsWith("name"))
                {
                    Name = Utility.GetString("name", line, 0);
                }
                else if (line.StartsWith("stagemaker"))
                {
                    //CheckPoints.Maker = Getastring("stagemaker", astring, 0);
                }
                else if (line.StartsWith("publish"))
                {
                    //CheckPoints.Pubt = Utility.GetInt("publish", astring, 0);
                }
                else if (line.StartsWith("soundtrack("))
                {
                    string folder = Utility.GetString("soundtrack", line, 0);
                    string fileName = Utility.GetString("soundtrack", line, 1);

                    if(folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
                    {
                        Logging.Error("Invalid folder or file name in soundtrack() directive");
                    }
                    else
                    {
                        musicPath = $"{folder}/{fileName}";
                    }
                }
                else if(line.StartsWith("soundtrackfreqmul"))
                {
                    float mul = Utility.GetFloat("soundtrackfreqmul", line, 0);
                    musicFreqMul = mul;
                }
                else if(line.StartsWith("soundtracktempomul"))
                {
                    float mul = Utility.GetFloat("soundtracktempomul", line, 0);
                    musicTempoMul = mul;
                }
                else if(line.StartsWith("soundtrackremaster"))
                {
                    string folder = Utility.GetString("soundtrackremaster", line, 0);
                    string fileName = Utility.GetString("soundtrackremaster", line, 1);

                    if(folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
                    {
                        Logging.Error("Invalid folder or file name in soundtrackremaster() directive");
                    }
                    else
                    {
                        remasteredMusicPath = $"{folder}/{fileName}";
                    }
                }

                // stage walls
                else if (line.StartsWith("maxr"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

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
                            f64Euler.Identity,
                            IsWall: true
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(o + 500, -5000, n * 4800 / 2 + p - 2400),
                        Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                        Xy: 90,
                        Zy: 0,
                        SurfaceType: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                    
                    wallDefs.Add(new StageWall(WallDirection.Right, n, o, p));
                }
                else if (line.StartsWith("maxl"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

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
                            new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                            IsWall: true
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(o - 500, -5000, n * 4800 / 2 + p - 2400),
                        Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                        Xy: -90,
                        Zy: 0,
                        SurfaceType: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                    
                    wallDefs.Add(new StageWall(WallDirection.Left, n, o, p));
                }
                else if (line.StartsWith("maxt"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

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
                            new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                            IsWall: true
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o + 500),
                        Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                        Xy: 0,
                        Zy: 90,
                        SurfaceType: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                    
                    wallDefs.Add(new StageWall(WallDirection.Top, n, o, p));
                }
                else if (line.StartsWith("maxb"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

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
                            new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                            IsWall: true
                        ));
                    }

                    walls.Add(new Rad3dBoxDef(
                        Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o - 500),
                        Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                        Xy: 180,
                        Zy: -90,
                        SurfaceType: 0,
                        NotWall: false,
                        Color: new Color3(),
                        Damage: 1
                    ));
                    
                    wallDefs.Add(new StageWall(WallDirection.Bottom, n, o, p));
                }
                else
                {
                    unknownParameters.Add(line);
                }
            }
        }
        catch (Exception ex)
        {
            throw new StageLoadException(line, lineNumber, ex);
        }
    }

    [MemoryPackConstructor]
    public StageLoader()
    {
        // Create an empty stage loader for editor purposes
        currentGroup = ungrouped;
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