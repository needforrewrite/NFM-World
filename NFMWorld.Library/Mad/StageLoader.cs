using System.Text;
using TightPack;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorld.Sentry;

namespace NFMWorldLibrary;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[TightPackAllowSerialize]
public readonly partial record struct PiecePlacement(
    [property: TightPackOrder(0), PluginVisible]
    PiecePlacementType Type,
    [property: TightPackOrder(1), PluginVisible]
    Rad3d Object,
    [property: TightPackOrder(2), PluginVisible]
    f64Vector3 Position,
    [property: TightPackOrder(3), PluginVisible]
    f64Euler Rotation,
    [property: TightPackOrder(4), PluginVisible]
    AiNodeKind? NodeKind = null,
    [property: TightPackOrder(5), PluginVisible]
    bool IsSpecial = false,
    [property: TightPackOrder(6), PluginVisible]
    bool IsWall = false
);

[PluginVisible]
public enum PiecePlacementType : byte
{
    CollisionObject,
    CheckPoint,
    FixHoop
}

// count = n parameter
// position = o parameter
// offset = p parameter
[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public partial record StageWall(
    [property: TightPackOrder(0), PluginVisible]
    WallDirection Direction,
    [property: TightPackOrder(1), PluginVisible]
    int Count,
    [property: TightPackOrder(2), PluginVisible]
    int Position,
    [property: TightPackOrder(3), PluginVisible]
    int Offset
);

[PluginVisible]
public enum WallDirection : byte
{
    Right,
    Left,
    Top,
    Bottom
}

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
public readonly partial record struct HierarchyGroup(
    [property: TightPackOrder(0), PluginVisible]
    string Name,
    [property: TightPackOrder(1), PluginVisible]
    UnlimitedArray<PiecePlacement> Pieces,
    // Old group format: #editor_group(Name,x:z,...)
    [property: TightPackOrder(2), PluginVisible]
    UnlimitedArray<string> CoordinateKeys
);

// colors have to be processed in order, so we provide a list of instructions in order
[TightPackable]
[TightPackUnion(0, typeof(SnapInstruction))]
[TightPackUnion(1, typeof(SkyInstruction))]
[TightPackUnion(2, typeof(FogInstruction))]
[TightPackUnion(3, typeof(CloudsInstruction))]
[TightPackUnion(4, typeof(GroundInstruction))]
[TightPackUnion(5, typeof(TextureInstruction))]
[TightPackUnion(6, typeof(PolysInstruction))]
[PluginVisible]
public abstract partial record EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record SnapInstruction(
    [property: TightPackOrder(0), PluginVisible]
    Color3 Color
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record SkyInstruction(
    [property: TightPackOrder(0), PluginVisible]
    Color3 Color
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record FogInstruction(
    [property: TightPackOrder(0), PluginVisible]
    Color3 Color
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record CloudsInstruction(
    [property: TightPackOrder(0), PluginVisible]
    IReadOnlyList<int> Clouds
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record GroundInstruction(
    [property: TightPackOrder(0), PluginVisible]
    Color3 Color
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record TextureInstruction(
    [property: TightPackOrder(0), PluginVisible]
    IReadOnlyList<int> Texture
) : EnvironmentInstruction;

[TightPackable(GenerateType.VersionTolerant), PluginVisible]
[method: TightPackConstructor]
public partial record PolysInstruction(
    [property: TightPackOrder(0), PluginVisible]
    Color3 Color
) : EnvironmentInstruction;

[TightPackable(GenerateType.CircularReference), PluginVisible]
public partial class StageLoader
{
    [TightPackOrder(0), PluginVisible] public string Path;

    [TightPackOrder(1), PluginVisible] public ushort nlaps = 3;

    // soundtrack(folder,fileName)
    [TightPackOrder(2), PluginVisible] public string musicPath = "";

    // soundtrackremaster(folder,fileName)
    [TightPackOrder(3), PluginVisible] public string remasteredMusicPath = "";

    [TightPackIgnore] public IReadOnlyDictionary<string, byte[]>? EmbeddedMusic { get; set; }

    // soundtrackfreqmul(mul)
    [TightPackOrder(4), PluginVisible] public double musicFreqMul = 1.0d;
    [TightPackOrder(5), PluginVisible] public double musicTempoMul = 1.0d;
    [TightPackOrder(6), PluginVisible] public string Name = "hogan rewish";
    [TightPackOrder(7), PluginVisible] public int indexOffset = 10;

    private bool swapYandRot = false;
    private bool reverseChkY = false;

    // left
    [TightPackOrder(8), PluginVisible] public int Sx;

    // top
    [TightPackOrder(9), PluginVisible] public int Sz;

    // width
    [TightPackOrder(10), PluginVisible] public int Ncx;

    // height
    [TightPackOrder(11), PluginVisible] public int Ncz;

    [TightPackOrder(21), PluginVisible] public float? CloudCoverage;
    [TightPackOrder(22), PluginVisible] public int? FogDensity;
    [TightPackOrder(23), PluginVisible] public int? FadeFrom;
    [TightPackOrder(24), PluginVisible] public bool LightsOn;
    [TightPackOrder(25), PluginVisible] public bool DrawMountains = true;
    [TightPackOrder(26), PluginVisible] public int? MountainSeed;
    [TightPackOrder(27), PluginVisible] public float? MountainCoverage;
    [TightPackOrder(28), PluginVisible] public Vector3? LightDirection;
    [TightPackOrder(29), PluginVisible] public UnlimitedArray<PiecePlacement> pieces = new();
    [TightPackOrder(30), PluginVisible] public UnlimitedArray<Rad3dBoxDef> walls = new();
    [TightPackOrder(31), PluginVisible] public int maxr = 0;
    [TightPackOrder(32), PluginVisible] public int maxl = 100;
    [TightPackOrder(33), PluginVisible] public int maxt = 0;
    [TightPackOrder(34), PluginVisible] public int maxb = 100;

    [TightPackOrder(35), PluginVisible] public UnlimitedArray<EnvironmentInstruction> EnvironmentInstructions = new();
    [TightPackOrder(36), PluginVisible] public bool DrawPolys = true;
    [TightPackOrder(37), PluginVisible] public bool DrawClouds = true;

    // aerial()
    [TightPackOrder(38), PluginVisible] public bool Aerial;

    /// <summary>Per-stage physics overrides (phy-addons stage properties).</summary>
    [TightPackOrder(39), PluginVisible]
    public StagePhysics Physics { get; set; } = new StagePhysics();

    [TightPackOrder(40), PluginVisible] public UnlimitedArray<StageWall> wallDefs = [];

    [TightPackIgnore, PluginVisible] public UnlimitedArray<HierarchyGroup> groups = [];
    [TightPackIgnore, PluginVisible] public HierarchyGroup ungrouped = new("Ungrouped", [], []);
    [TightPackIgnore, PluginVisible] public HierarchyGroup currentGroup;
    [TightPackIgnore, PluginVisible] public int UngroupedOrderIndex = -1;

    [TightPackOrder(41), PluginVisible] public UnlimitedArray<string> unknownParameters = [];

    // amodel/amodel2 models, resolved while loading only; pieces hold the Rad3d directly.
    [TightPackIgnore] private readonly Dictionary<string, Rad3d> _embeddedModels = new(StringComparer.OrdinalIgnoreCase);

    public StageLoader(string stageName)
        : this(stageName, stageName, null)
    {
    }

    /// <summary>
    /// Parses explicit stage lines instead of reading them from the VFS. Used by the archive's
    /// validator, which must simulate against the stage it was told to, not one the replay carries.
    /// </summary>
    public static StageLoader FromLines(string stageName, IEnumerable<string> stageLines)
        => new(stageName, stageName, stageLines);

    private StageLoader(string stageName, string path, IEnumerable<string>? providedLines)
    {
        currentGroup = ungrouped;

        Path = path;

        // Read-only legacy fallback: a tracks .radq the user put in data/legacy/tracks, then the
        // original track host.
        var stageLines = providedLines
                         ?? (VFS.FileExists($"data/stages/{stageName}.txt")
                             ? VFS.ReadAllLines($"data/stages/{stageName}.txt")
                             : (LegacyAssets.LegacyAssetImporter.TryReadLocal("tracks", stageName)
                                ?? LegacyAssets.LegacyAssetImporter.TryFetch("tracks", stageName)) is { } legacy
                                 ? legacy.Split('\n').Select(l => l.TrimEnd('\r'))
                                 : []);

        var line = "";
        int lineNumber = 0;
        string? embeddedLabel = null;
        var embeddedLines = 0;
        var embeddedData = new StringBuilder();

        try
        {
            foreach (var aline in stageLines)
            {
                line = aline.Trim();

                // ds:/? prefixes mark lines a phy-addons stage editor added; load them as usual.
                if (line.StartsWith("ds:")) line = line[3..];
                if (line.StartsWith('?')) line = line[1..];

                lineNumber++;

                // amodel2 payload: the model data continues on `//- ` lines.
                if (embeddedLabel != null)
                {
                    if (line.StartsWith("//- "))
                    {
                        embeddedData.Append(line[4..]);
                        if (--embeddedLines > 0) continue;

                        RegisterEmbeddedModel(embeddedLabel, embeddedData.ToString());
                        embeddedLabel = null;
                        embeddedData.Clear();
                        continue;
                    }

                    embeddedLabel = null;
                }

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
                            group = group with { CoordinateKeys = [.. keys] };
                        }
                    }

                    groups.Add(group);
                    currentGroup = group;
                }

                else if (line.StartsWith("#editor_ungrouped_order"))
                {
                    UngroupedOrderIndex = Utility.GetInt("#editor_ungrouped_order", line, 0);
                }

                else if (line.StartsWith("#editor_ungrouped"))
                {
                    // Ends the current #editor_group block: the pieces after it are ungrouped.
                    currentGroup = ungrouped;
                }

                else if (line.StartsWith("snap"))
                {
                    EnvironmentInstructions.Add(
                        new SnapInstruction(
                            new Color3(
                                (short)Utility.GetInt("snap", line, 0),
                                (short)Utility.GetInt("snap", line, 1),
                                (short)Utility.GetInt("snap", line, 2)
                            )
                        )
                    );
                }

                else if (line.StartsWith("sky"))
                {
                    EnvironmentInstructions.Add(
                        new SkyInstruction(
                            new Color3(
                                (short)Utility.GetInt("sky", line, 0),
                                (short)Utility.GetInt("sky", line, 1),
                                (short)Utility.GetInt("sky", line, 2)
                            )
                        )
                    );
                }

                else if (line.StartsWith("ground"))
                {
                    EnvironmentInstructions.Add(
                        new GroundInstruction(
                            new Color3(
                                (short)Utility.GetInt("ground", line, 0),
                                (short)Utility.GetInt("ground", line, 1),
                                (short)Utility.GetInt("ground", line, 2)
                            )
                        )
                    );
                }

                else if (line.StartsWith("polys"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        DrawPolys = false;
                    }
                    else
                    {
                        EnvironmentInstructions.Add(
                            new PolysInstruction(
                                new Color3(
                                    (short)Utility.GetInt("polys", line, 0),
                                    (short)Utility.GetInt("polys", line, 1),
                                    (short)Utility.GetInt("polys", line, 2)
                                )
                            )
                        );
                    }
                }

                else if (line.StartsWith("fog"))
                {
                    EnvironmentInstructions.Add(
                        new FogInstruction(
                            new Color3(
                                (short)Utility.GetInt("fog", line, 0),
                                (short)Utility.GetInt("fog", line, 1),
                                (short)Utility.GetInt("fog", line, 2)
                            )
                        )
                    );
                }

                else if (line.StartsWith("texture"))
                {
                    var texture = new[]
                    {
                        Utility.GetInt("texture", line, 0),
                        Utility.GetInt("texture", line, 1),
                        Utility.GetInt("texture", line, 2),
                        Utility.GetInt("texture", line, 3)
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
                            var clouds = new[]
                            {
                                Utility.GetInt("clouds", line, 0),
                                Utility.GetInt("clouds", line, 1),
                                Utility.GetInt("clouds", line, 2),
                                Utility.GetInt("clouds", line, 3),
                                Utility.GetInt("clouds", line, 4)
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
                    LightDirection = new Vector3(
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
                        if (swapYandRot)
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
                        new f64Euler(
                            f64AngleSingle.FromDegrees(Utility.GetInt("set", line, rotPlace)),
                            f64AngleSingle.ZeroAngle,
                            f64AngleSingle.ZeroAngle
                        )
                    );
                    if (line.Contains(")p")) //AI tags
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
                        if (swapYandRot)
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
                        new f64Vector3(
                            Utility.GetInt("fix", line, 1),
                            Utility.GetInt("fix", line, 3),
                            Utility.GetInt("fix", line, 2)
                        ),
                        new f64Euler(
                            f64AngleSingle.FromDegrees(Utility.GetInt("fix", line, 4)),
                            f64AngleSingle.ZeroAngle,
                            f64AngleSingle.ZeroAngle
                        ),
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
                // phy-addons stage properties. `gravity`, `grip`, `dam` etc. are not stock
                // directives; they are read by phy-addons that scan for these prefixes. The
                // parenthesis is required: a bare prefix makes `dam` swallow `damagevents()` and
                // `lava` swallow `lavavents()`, which are other addons' scenery directives, and
                // the numeric parse then throws the whole stage away.
                else if (line.StartsWith("stats("))
                {
                    Physics = Physics with { RechargedStats = true };
                }
                else if (line.StartsWith("gravity("))
                {
                    Physics = Physics with { Gravity = Utility.GetFloat("gravity(", line, 0) };
                }
                else if (line.StartsWith("grip("))
                {
                    Physics = Physics with { Grip = Utility.GetFloat("grip(", line, 0) };
                }
                else if (line.StartsWith("powerloss("))
                {
                    Physics = Physics with { Powerloss = Utility.GetFloat("powerloss(", line, 0) };
                }
                else if (line.StartsWith("dam("))
                {
                    Physics = Physics with { Damage = Utility.GetFloat("dam(", line, 0) };
                }
                else if (line.StartsWith("lava("))
                {
                    Physics = Physics with { Lava = Utility.GetFloat("lava(", line, 0) };
                }
                else if (line.StartsWith("maxhit("))
                {
                    // The directive is a multiplier on the stock 300 impulse clamp.
                    Physics = Physics with { MaxHit = 300f * Utility.GetFloat("maxhit(", line, 0) };
                }
                else if (line.StartsWith("recoil("))
                {
                    Physics = Physics with { Recoil = Utility.GetFloat("recoil(", line, 0) };
                }
                else if (line.StartsWith("rage("))
                {
                    Physics = Physics with { Rage = Math.Max(Utility.GetFloat("rage(", line, 0), 1f) };
                }
                else if (line.StartsWith("noarrow(") || line.StartsWith("disableArrow("))
                {
                    Physics = Physics with { NoArrow = true };
                }
                else if (line.StartsWith("name("))
                {
                    Name = Utility.GetString("name(", line, 0);
                }
                else if (line.StartsWith("aerial("))
                {
                    Aerial = true;
                }
                else if (line.StartsWith("amodel("))
                {
                    // phy-addons embedded model. The third argument is a source URL phy-addons
                    // would fetch instead; NFMW never fetches from a stage file.
                    var label = Utility.GetString("amodel", line, 0);
                    var data = Utility.GetString("amodel", line, 1);
                    if (data.Length > 0) RegisterEmbeddedModel(label, data);
                }
                else if (line.StartsWith("amodel2("))
                {
                    // Same model, with the payload continued over the next `//- ` lines.
                    embeddedLabel = Utility.GetString("amodel2", line, 0);
                    embeddedLines = Math.Max(Utility.GetInt("amodel2", line, 1), 0);
                    embeddedData.Clear();
                    if (embeddedLines == 0) embeddedLabel = null;
                }
                else if (line.StartsWith("model("))
                {
                    // phy-addons free-placed model: model(label,x,y,z,xy,xz,zy)
                    if (!TryGetPieceToPlace(Utility.GetString("model", line, 0), out var model)) continue;

                    var obj = new PiecePlacement(
                        PiecePlacementType.CollisionObject,
                        model,
                        new f64Vector3(
                            Utility.GetInt("model", line, 1),
                            World.Ground - Utility.GetInt("model", line, 2),
                            Utility.GetInt("model", line, 3)
                        ),
                        new f64Euler(
                            f64AngleSingle.FromDegrees(Utility.GetInt("model", line, 5)),
                            f64AngleSingle.FromDegrees(Utility.GetInt("model", line, 6)),
                            f64AngleSingle.FromDegrees(Utility.GetInt("model", line, 4))
                        )
                    );
                    pieces.Add(obj);
                    currentGroup.Pieces.Add(obj);
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

                    if (folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
                    {
                        Logging.Error("Invalid folder or file name in soundtrack() directive");
                    }
                    else
                    {
                        musicPath = $"{folder}/{fileName}";
                    }
                }
                else if (line.StartsWith("soundtrackfreqmul"))
                {
                    float mul = Utility.GetFloat("soundtrackfreqmul", line, 0);
                    musicFreqMul = mul;
                }
                else if (line.StartsWith("soundtracktempomul"))
                {
                    float mul = Utility.GetFloat("soundtracktempomul", line, 0);
                    musicTempoMul = mul;
                }
                else if (line.StartsWith("soundtrackremaster"))
                {
                    string folder = Utility.GetString("soundtrackremaster", line, 0);
                    string fileName = Utility.GetString("soundtrackremaster", line, 1);

                    if (folder.Contains(".") || folder.Contains("/") || fileName.Contains("..") || fileName.Contains("/"))
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

                    var n = GetWallCount("maxr", line);
                    var o = Utility.GetInt("maxr", line, 1);
                    maxr = o;
                    var p = Utility.GetInt("maxr", line, 2);

                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(
                            new PiecePlacement(
                                PiecePlacementType.CollisionObject,
                                wall,
                                new f64Vector3(o, World.Ground, q * 4800 + p),
                                f64Euler.Identity,
                                IsWall: true
                            )
                        );
                    }

                    walls.Add(
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(o + 500, -5000, (long)n * 4800 / 2 + p - 2400),
                            Radius: new f64Vector3(600, 7100, (long)n * 4800 / 2),
                            Xy: 90,
                            Zy: 0,
                            SurfaceType: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    );

                    wallDefs.Add(new StageWall(WallDirection.Right, n, o, p));
                }
                else if (line.StartsWith("maxl"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

                    var n = GetWallCount("maxl", line);
                    var o = Utility.GetInt("maxl", line, 1);
                    maxl = o;
                    var p = Utility.GetInt("maxl", line, 2);

                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(
                            new PiecePlacement(
                                PiecePlacementType.CollisionObject,
                                wall,
                                new f64Vector3(o, World.Ground, q * 4800 + p),
                                new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                                IsWall: true
                            )
                        );
                    }

                    walls.Add(
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(o - 500, -5000, (long)n * 4800 / 2 + p - 2400),
                            Radius: new f64Vector3(600, 7100, (long)n * 4800 / 2),
                            Xy: -90,
                            Zy: 0,
                            SurfaceType: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    );

                    wallDefs.Add(new StageWall(WallDirection.Left, n, o, p));
                }
                else if (line.StartsWith("maxt"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

                    var n = GetWallCount("maxt", line);
                    var o = Utility.GetInt("maxt", line, 1);
                    maxt = o;
                    var p = Utility.GetInt("maxt", line, 2);

                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(
                            new PiecePlacement(
                                PiecePlacementType.CollisionObject,
                                wall,
                                new f64Vector3(q * 4800 + p, World.Ground, o),
                                new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                                IsWall: true
                            )
                        );
                    }

                    walls.Add(
                        new Rad3dBoxDef(
                            Translation: new f64Vector3((long)n * 4800 / 2 + p - 2400, -5000, o + 500),
                            Radius: new f64Vector3((long)n * 4800 / 2, 7100, 600),
                            Xy: 0,
                            Zy: 90,
                            SurfaceType: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    );

                    wallDefs.Add(new StageWall(WallDirection.Top, n, o, p));
                }
                else if (line.StartsWith("maxb"))
                {
                    if (!TryGetPieceToPlace("nfmm/thewall", out var wall)) continue;

                    var n = GetWallCount("maxb", line);
                    var o = Utility.GetInt("maxb", line, 1);
                    maxb = o;
                    var p = Utility.GetInt("maxb", line, 2);

                    for (var q = 0; q < n; q++)
                    {
                        pieces.Add(
                            new PiecePlacement(
                                PiecePlacementType.CollisionObject,
                                wall,
                                new f64Vector3(q * 4800 + p, World.Ground, o),
                                new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle),
                                IsWall: true
                            )
                        );
                    }

                    walls.Add(
                        new Rad3dBoxDef(
                            Translation: new f64Vector3((long)n * 4800 / 2 + p - 2400, -5000, o - 500),
                            Radius: new f64Vector3((long)n * 4800 / 2, 7100, 600),
                            Xy: 180,
                            Zy: -90,
                            SurfaceType: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    );

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

    [TightPackConstructor]
    public StageLoader()
    {
        // Create an empty stage loader for editor purposes
        currentGroup = ungrouped;
        Path = "default_stage";
    }

    /// <summary>
    /// The wall-piece count from a <c>maxr/l/t/b</c> line, clamped. The value comes from a
    /// user-editable file and drives a loop that allocates one piece per iteration.
    /// </summary>
    private const int MaxWallCount = 10_000;

    private static int GetWallCount(string command, string line)
        => Math.Clamp(Utility.GetInt(command, line, 0), 0, MaxWallCount);

    /// <summary>Registers an amodel/amodel2 payload so <c>set</c>/<c>chk</c>/<c>fix</c> can place it.</summary>
    private void RegisterEmbeddedModel(string label, string data)
    {
        if (string.IsNullOrWhiteSpace(label)) return;

        if (LegacyAssets.LegacyAssetImporter.DecodeEmbeddedRad(data) is not { } text)
        {
            Logging.Error($"Could not decode embedded stage model '{label}'.");
            return;
        }

        try
        {
            _embeddedModels[label] = RadParser.ParseRad(text, label);
        }
        catch (Exception ex)
        {
            // A stage's embedded model is untrusted input; it must not take the stage down.
            Logging.Error($"Could not parse embedded stage model '{label}': {ex.Message}");
        }
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
            // Numbered labels stay stock parts, as in phy-addons, which only reaches its model
            // cache for a label it cannot parse as an index.
            if (_embeddedModels.TryGetValue(setstring, out var embedded))
            {
                mesh = embedded;
                return true;
            }

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
