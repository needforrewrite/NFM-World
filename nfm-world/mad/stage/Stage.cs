using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using nfm_world.mad.collision;
using NFMWorld;
using NFMWorld.Mad;
using NFMWorld.Util;
using SoftFloat;
using Stride.Core.Extensions;

/**
    Represents a stage. Holds all information relating to track pices, scenery, etc.
    But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class Stage : GameObject
{
    public UnlimitedArray<GameObject> pieces = [];
    public UnlimitedArray<IAiNode> nodes = [];
    public UnlimitedArray<CheckPoint> checkpoints = [];
    public UnlimitedArray<FixHoop> fixHoops = [];

    private int _fadeFrom = 0;

    public int nlaps = 3;

    public int stagePartCount => pieces.Count;

    // soundtrack(folder,fileName)
    public string musicPath = "";
    // soundtrackremaster(folder,fileName)
    public string remasteredMusicPath = "";
    // soundtrackfreqmul(mul)
    public double musicFreqMul = 1.0d;
    public double musicTempoMul = 0d;

    public string Name = "hogan rewish";

    public Sky sky;
    public Ground ground;
    public GroundPolys? polys;
    public GroundPolys? clouds;
    public Mountains? mountains;

    public static int indexOffset = 10;
    private bool swapYandRot = false;
    private bool reverseChkY = false;

    public readonly string Path;
    
    // left
    internal int Sx;
    // top
    internal int Sz;
    // width
    internal int Ncx;
    // height
    internal int Ncz;

    public void ReapplyFadeFrom()
    {
        World.FadeFrom = _fadeFrom;
    }

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public Stage(string stageName, GraphicsDevice graphicsDevice)
    {
        Children = pieces;

        Path = stageName;
        World.ResetValues();
        // Medium.Noelec = 0;
        // Medium.Ground = 250;
        // Medium.Trk = 0;
        var maxr = 0;
        var maxl = 100;
        var maxt = 0;
        var maxb = 100;
        var line = "";
        swapYandRot = false;
        indexOffset = 10;
        reverseChkY = false;
        try
        {
            //var customStagePath = "stages/" + CheckPoints.Stage + ".txt";
            var customStagePath = "data/stages/" + stageName + ".txt";
            foreach (var aline in System.IO.File.ReadAllLines(customStagePath))
            {
                line = aline.Trim();
                if (line.StartsWith("snap"))
                {
                    World.Snap = new Color3(
                        (short)Utility.GetInt("snap", line, 0),
                        (short)Utility.GetInt("snap", line, 1),
                        (short)Utility.GetInt("snap", line, 2)
                    );
                }

                if (line.StartsWith("sky"))
                {
                    World.Sky = new Color3(
                        (short)Utility.GetInt("sky", line, 0),
                        (short)Utility.GetInt("sky", line, 1),
                        (short)Utility.GetInt("sky", line, 2)
                    );
                }

                if (line.StartsWith("ground"))
                {
                    World.GroundColor = new Color3(
                        (short)Utility.GetInt("ground", line, 0),
                        (short)Utility.GetInt("ground", line, 1),
                        (short)Utility.GetInt("ground", line, 2)
                    );
                }

                if (line.StartsWith("polys"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        World.DrawPolys = false;
                        World.HasPolys = false;
                    }
                    else
                    {
                        World.HasPolys = true;
                        World.DrawPolys = true;
                        World.GroundPolysColor = new Color3(
                            (short)Utility.GetInt("polys", line, 0),
                            (short)Utility.GetInt("polys", line, 1),
                            (short)Utility.GetInt("polys", line, 2)
                        );
                    }
                }

                if (line.StartsWith("fog"))
                {
                    World.Fog = new Color3(
                        (short)Utility.GetInt("fog", line, 0),
                        (short)Utility.GetInt("fog", line, 1),
                        (short)Utility.GetInt("fog", line, 2)
                    );
                }

                if (line.StartsWith("texture"))
                {
                    World.HasTexture = true;
                    World.Texture =
                    [
                        Utility.GetInt("texture", line, 0),
                        Utility.GetInt("texture", line, 1),
                        Utility.GetInt("texture", line, 2),
                        Utility.GetInt("texture", line, 3)
                    ];
                }

                if (line.StartsWith("clouds"))
                {
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        World.DrawClouds = false;
                        World.HasClouds = false;
                    }
                    else
                    {
                        World.HasClouds = true;
                        World.DrawClouds = true;
                        // Support both single seed value and full cloud parameters
                        var cloudParams = line.Split(',');
                        if (cloudParams.Length == 2) // clouds(seed) format
                        {
                            World.CloudCoverage = Utility.GetInt("clouds", line, 0);
                        }
                        else // clouds(param1,param2,...) format
                        {
                            World.Clouds =
                            [
                                Utility.GetInt("clouds", line, 0),
                                Utility.GetInt("clouds", line, 1),
                                Utility.GetInt("clouds", line, 2),
                                Utility.GetInt("clouds", line, 3),
                                Utility.GetInt("clouds", line, 4)
                            ];
                        }
                    }
                }

                if (line.StartsWith("cloudcoverage"))
                {
                    World.CloudCoverage = Utility.GetFloat("cloudcoverage", line, 0);
                }

                if (line.StartsWith("density"))
                {
                    World.FogDensity = (Utility.GetInt("density", line, 0) + 1) * 2 - 1;
                    if (World.FogDensity < 1)
                    {
                        World.FogDensity = 1;
                    }
                    if (World.FogDensity > 30)
                    {
                        World.FogDensity = 30;
                    }
                }

                if (line.StartsWith("fadefrom"))
                {
                    World.FadeFrom = Utility.GetInt("fadefrom", line, 0);
                    _fadeFrom = World.FadeFrom;
                }

                if (line.StartsWith("lightson"))
                {
                    World.LightsOn = true;
                }

                if (line.StartsWith("mountains"))
                {
                    // Check for mountains(false) first
                    if (line.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        World.DrawMountains = false;
                    }
                    else
                    {
                        World.DrawMountains = true;
                        World.MountainSeed = Utility.GetInt("mountains", line, 0);
                    }
                }

                if (line.StartsWith("mountaincoverage"))
                {
                    World.MountainCoverage = Utility.GetFloat("mountaincoverage", line, 0);
                }

                if (line.StartsWith("lightdir"))
                {
                    World.LightDirection = new Vector3(
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
                    if (!TryGetPieceToPlace(Utility.GetString("set", line, 0), out var mesh)) continue;

                    var setheight = World.Ground;
                    
                    var hasCustomY = line.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        if(swapYandRot)
                        {
                            setheight = Utility.GetInt("set", line, 3);
                        }
                        else
                        {
                            setheight = Utility.GetInt("set", line, 4) * -1;
                        }
                    }

                    var rotPlace = 3;

                    if (swapYandRot)
                    {
                        rotPlace = 4;
                    }

                    var obj = new CollisionObject(
                        mesh,
                        new f64Vector3(Utility.GetInt("set", line, 1), setheight, Utility.GetInt("set", line, 2)),
                        new f64Euler(f64AngleSingle.FromDegrees(Utility.GetInt("set", line, rotPlace)), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle));
                    pieces[stagePartCount] = obj;
                    if (line.Contains(")p"))     //AI tags
                    {
                        nodes[nodes.Count] = obj;
                        obj.Kind = AiNodeKind.Road;
                        if (line.Contains(")pt"))
                        {
                            obj.Kind = AiNodeKind.Turn;
                        }
                        if (line.Contains(")pr"))
                        {
                            obj.Kind = AiNodeKind.Ramp;
                        }
                        if (line.Contains(")po"))
                        {
                            obj.Kind = AiNodeKind.FixRoadStart;
                        }
                        if (line.Contains(")ph"))
                        {
                            obj.Kind = AiNodeKind.Halfpipe;
                        }
                    }
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }
                }
                if (line.StartsWith("chk"))
                {
                    var ymult = -1;
                    
                    if (!TryGetPieceToPlace(Utility.GetString("chk", line, 0), out var mesh)) continue;

                    if (mesh.FileName == "nfmm/aircheckpoint" || reverseChkY)
                    {
                        ymult = 1; // default to inverted Y for stupid rollercoaster chks for compatibility reasons
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
                            chkheight = Utility.GetInt("chk", line, 4) * ymult;
                        }
                    }

                    var obj = new CheckPoint(
                        mesh,
                        new f64Vector3(Utility.GetInt("chk", line, 1), chkheight, Utility.GetInt("chk", line, 2)),
                        new f64Euler(rotation, f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)
                    );
                    pieces[stagePartCount] = obj;
                    nodes[nodes.Count] = obj;
                    checkpoints[checkpoints.Count] = obj;
                    
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

                    var fix = new FixHoop(
                        mesh,
                        new f64Vector3(Utility.GetInt("fix", line, 1), Utility.GetInt("fix", line, 3), Utility.GetInt("fix", line, 2)),
                        new f64Euler(f64AngleSingle.FromDegrees(Utility.GetInt("fix", line, 4)), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)                        
                    );
                    pieces[stagePartCount] = fix;
                    if (Utility.GetInt("fix", line, 4) != 0)
                    {
                        fix.Rotated = true;
                    }

                    fixHoops[fixHoops.Count] = fix;
                    nodes[nodes.Count] = fix;
                    if (line.EndsWith(")s"))
                    {
                        fix.IsSpecial = true;
                    }
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
                    nlaps = Utility.GetInt("nlaps", line, 0);
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
                var wall = GameSparker.GetStagePart("nfmm/thewall");
                if (wall.Mesh == null)
                {
                    throw new InvalidOperationException("Stage wall part 'thewall' not found.");
                }
                if (line.StartsWith("maxr"))
                {
                    var n = Utility.GetInt("maxr", line, 0);
                    var o = Utility.GetInt("maxr", line, 1);
                    maxr = o;
                    var p = Utility.GetInt("maxr", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new CollisionObject(
                            wall.Mesh,
                            new f64Vector3(o, World.Ground, q * 4800 + p),
                            f64Euler.Identity                        
                        );
                    }

                    pieces[stagePartCount] = new WallCollision([
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(o + 500, -5000, n * 4800 / 2 + p - 2400),
                            Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                            Xy: 90,
                            Zy: 0,
                            Skid: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    ]);
                }
                if (line.StartsWith("maxl"))
                {
                    var n = Utility.GetInt("maxl", line, 0);
                    var o = Utility.GetInt("maxl", line, 1);
                    maxl = o;
                    var p = Utility.GetInt("maxl", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new CollisionObject(
                            wall.Mesh,
                            new f64Vector3(o, World.Ground, q * 4800 + p),
                            new f64Euler(f64AngleSingle.FromDegrees(180), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)                        
                        );
                    }
                    pieces[stagePartCount] = new WallCollision([
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(o - 500, -5000, n * 4800 / 2 + p - 2400),
                            Radius: new f64Vector3(600, 7100, n * 4800 / 2),
                            Xy: -90,
                            Zy: 0,
                            Skid: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    ]);
                }
                if (line.StartsWith("maxt"))
                {
                    var n = Utility.GetInt("maxt", line, 0);
                    var o = Utility.GetInt("maxt", line, 1);
                    maxt = o;
                    var p = Utility.GetInt("maxt", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new CollisionObject(
                            wall.Mesh,
                            new f64Vector3(q * 4800 + p, World.Ground, o),
                            new f64Euler(f64AngleSingle.FromDegrees(90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)                        
                        );
                    }

                    pieces[stagePartCount] = new WallCollision([
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o + 500),
                            Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                            Xy: 0,
                            Zy: 90,
                            Skid: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    ]);
                }
                if (line.StartsWith("maxb"))
                {
                    var n = Utility.GetInt("maxb", line, 0);
                    var o = Utility.GetInt("maxb", line, 1);
                    maxb = o;
                    var p = Utility.GetInt("maxb", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new CollisionObject(
                            wall.Mesh,
                            new f64Vector3(q * 4800 + p, World.Ground, o),
                            new f64Euler(f64AngleSingle.FromDegrees(-90), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)                        
                        );
                    }
                    pieces[stagePartCount] = new WallCollision([
                        new Rad3dBoxDef(
                            Translation: new f64Vector3(n * 4800 / 2 + p - 2400, -5000, o - 500),
                            Radius: new f64Vector3(n * 4800 / 2, 7100, 600),
                            Xy: 180,
                            Zy: -90,
                            Skid: 0,
                            NotWall: false,
                            Color: new Color3(),
                            Damage: 1
                        )
                    ]);
                }
            }

            if(musicPath.IsNullOrEmpty())
            {
                GameSparker.Writer.WriteLine("No music is defined for this stage!", "error");
            }

            // Medium.Newpolys(maxl, maxr - maxl, maxb, maxt - maxb, stagePartCount);
            // Medium.Newmountains(maxl, maxr, maxb, maxt);
            // Medium.Newclouds(maxl, maxr, maxb, maxt);
            // Medium.Newstars();
            SetBounds(maxl, maxr - maxl, maxb, maxt - maxb);

            if (World.DrawPolys)
            {
                polys = NFMWorld.Mad.Environment.MakePolys(this, maxl, maxr - maxl, maxb, maxt - maxb, stagePartCount, graphicsDevice);
            }

            if (World.DrawClouds)
            {
                clouds = NFMWorld.Mad.Environment.MakeClouds(maxl, maxr, maxb, maxt, graphicsDevice);
            }

            if (World.DrawMountains)
            {
                mountains = NFMWorld.Mad.Environment.MakeMountains(maxl, maxr, maxb, maxt, graphicsDevice);
            }
        }
        catch (Exception exception)
        {
            GameSparker.Writer.WriteLine("Error in stage: " + stageName, "error");
            GameSparker.Writer.WriteLine("At line: " + line, "error");
            GameSparker.Writer.WriteLine(exception.ToString(), "error");
        }
        sky = new Sky(graphicsDevice);
        ground = new Ground(graphicsDevice);
    }

    private void SetBounds(int sx, int ncx, int sz, int ncz)
    {
        Sx = sx;
        Sz = sz;
        Ncx = ncx;
        if (Ncx <= 0)
        {
            Ncx = 1;
        }
        Ncz = ncz;
        if (Ncz <= 0)
        {
            Ncz = 1;
        }
        
        CollisionQuadTree = new QuadTree<CollisionBoxRef>(sx, sz, ncx, ncz);
        foreach (var piece in pieces)
        {
            if (piece is ICollidable collidable)
            {
                AddToQuadTree(collidable);
            }
        }
        CollisionQuadTree.TrimExcess();
    }

    private static bool TryGetPieceToPlace(string setstring, out PlaceableObjectInfo mesh)
    {
        if (int.TryParse(setstring, out var setindex))
        {
            setindex -= indexOffset;
            mesh = GameSparker.stage_parts[setindex];
            if (mesh == null!)
            {
                GameSparker.Writer.WriteLine($"Stage part '{setstring}' not found.", "error");
                mesh = GameSparker.error_mesh;
                return true;
            }
        }
        else
        {
            var stagePart = GameSparker.GetStagePart(setstring);
            if (stagePart.Mesh == null)
            {
                GameSparker.Writer.WriteLine($"Stage part '{setstring}' not found.", "error");
                mesh = GameSparker.error_mesh;
                return true;
            }
            mesh = stagePart.Mesh;
        }

        return true;
    }

    public GameObject CreateObject(string objectName, int x, int y, int z, int r)
    {
        var part = GameSparker.GetStagePart(objectName);
        if (part.Mesh == null)
        {
            GameSparker.devConsole.Log($"Object '{objectName}' not found.", "warning");
            part = (-1, GameSparker.error_mesh);
        }

        var mesh = pieces[stagePartCount] = new CollisionObject(
            part.Mesh,
            new f64Vector3(x,
            250 - y,
            z), 
            new f64Euler(f64AngleSingle.FromDegrees(r), f64AngleSingle.ZeroAngle, f64AngleSingle.ZeroAngle)
        );

        GameSparker.devConsole.Log($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}", "info");

        AddToQuadTree((mesh as ICollidable)!);

        return mesh;
    }

    // A struct for this would be ideal, but it's a very large object so it would cause enormous stack allocations
    public class CollisionBoxRef : IQuadObject
    {
        public readonly int Index;
        private readonly f64Bounds _bounds;
        
        // Box and GameObject position and rotation in world space
        public readonly f64Vector3 GameObjectPosition;
        public readonly fix64 GameObjectXz;
        public readonly Rad3dBoxDef Box;
        
        // Precomputed BoxRoad/BoxWall/BoxRamp for faster collision checks
        public readonly BoxRoad? BoxRoad;
        public readonly BoxWall? BoxWall;
        public readonly BoxRamp? BoxRamp;

        public CollisionBoxRef(
            fix64 gameObjectX,
            fix64 gameObjectY,
            fix64 gameObjectZ,
            fix64 gameObjectRotXz,
            Rad3dBoxDef box,
            fix64 radius,
            int index)
        {
            Index = index;
            GameObjectPosition = new f64Vector3(gameObjectX, gameObjectY, gameObjectZ);
            GameObjectXz = gameObjectRotXz;

            Box = box;
            
            var rad = box.Radius;
            var radFlipped = new f64Vector3(rad.Z, rad.Y, rad.X);
            var trackersPosition = box.Translation;

            if (box is { Xy: 0, Zy: 0 })
            {
                BoxRoad = new BoxRoad(rad, trackersPosition, gameObjectRotXz, GameObjectPosition);
            }
            else if (box.Zy == 90 || box.Zy == -90 || box.Xy == 90 || box.Xy == -90)
            {
                if (box.Zy == -90)
                {
                    BoxWall = new BoxWall(rad, 0, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
                else if (box.Xy == 90)
                {
                    BoxWall = new BoxWall(radFlipped, 90, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
                else if (box.Zy == 90)
                {
                    BoxWall = new BoxWall(rad, 180, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
                else
                {
                    BoxWall = new BoxWall(radFlipped, -90, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
            }
            else if ((box.Zy != 0 && box.Zy != 90 && box.Zy != -90) || (box.Xy != 0 && box.Xy != 90 && box.Xy != -90))
            {
                if (box.Zy != 0)
                {
                    BoxRamp = new BoxRamp(rad, box.Zy, 0, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
                else
                {
                    BoxRamp = new BoxRamp(radFlipped, box.Xy, -90, trackersPosition, gameObjectRotXz, GameObjectPosition);
                }
            }

            _bounds = new f64Bounds(
                gameObjectX - radius,
                gameObjectZ - radius,
                radius * 2,
                radius * 2
            );
        }

        public f64Bounds Bounds => _bounds;
    }
    
    private QuadTree<CollisionBoxRef> CollisionQuadTree = new(0,0,0,0);
    private int _quadTreeInsertionIndex = 0;

    private void AddToQuadTree(ICollidable mesh)
    {
        fix64 x = 0;
        fix64 y = 0;
        fix64 z = 0;
        fix64 xz = 0;
        if (mesh is GameObject gameObject)
        {
            x = (fix64)gameObject.Position.X;
            y = (fix64)gameObject.Position.Y;
            z = (fix64)gameObject.Position.Z;
            xz = gameObject.Rotation.Xz.Degrees;
        }
        
        foreach (var box in mesh.Boxes)
        {
            var maxR = fix64.Max(
                mesh.MaxRadius,
                fix64.Max(
                    fix64.Max(
                        fix64.Abs(box.Translation.X) + fix64.Abs(box.Radius.X),
                        fix64.Abs(box.Translation.Z) + fix64.Abs(box.Radius.Z)
                    ),
                    fix64.Abs(box.Translation.Y) + fix64.Abs(box.Radius.Y)
                )
            );
            CollisionQuadTree.Insert(new CollisionBoxRef(
                gameObjectX: x,
                gameObjectY: y,
                gameObjectZ: z,
                gameObjectRotXz: xz,
                box: box,
                maxR,
                index: _quadTreeInsertionIndex++
            ));
        }
    }
    
    private List<CollisionBoxRef> _tempTrackers = new();
    public ReadOnlySpan<CollisionBoxRef> RetrievePointCollidables(fix64 x, fix64 z)
    {
        _tempTrackers.Clear();
        CollisionQuadTree.RetrievePoint(_tempTrackers, x, z);
        var span = CollectionsMarshal.AsSpan(_tempTrackers);
        span.Sort(static (a, b) => a.Index.CompareTo(b.Index));
        return span;
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        sky?.Render(camera, lighting);
        ground?.Render(camera, lighting);
        polys?.Render(camera, lighting);
        clouds?.Render(camera, lighting);
        mountains?.Render(camera, lighting);
        base.Render(camera, lighting);
    }
}