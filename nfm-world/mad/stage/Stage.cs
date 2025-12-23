using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using NFMWorld.Util;
using Stride.Core.Extensions;

/**
    Represents a stage. Holds all information relating to track pices, scenery, etc.
    But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class Stage : IRenderable
{
    public UnlimitedArray<Mesh> pieces = [];
    public UnlimitedArray<CheckPoint> checkpoints = [];

    public int nlaps = 3;

    public int stagePartCount => pieces.Count;

    // soundtrack(folder,fileName)
    public string musicPath = "";
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

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public Stage(string stageName, GraphicsDevice graphicsDevice)
    {
        Path = stageName;
        World.ResetValues();
        Trackers.Nt = 0;
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
                    }
                    else
                    {
                        World.HasPolys = true;
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
                    }
                    else
                    {
                        World.HasClouds = true;
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

                    pieces[stagePartCount] = new Mesh(
                        mesh,
                        new Vector3(Utility.GetInt("set", line, 1), setheight, Utility.GetInt("set", line, 2)),
                        new Euler(AngleSingle.FromDegrees(Utility.GetInt("set", line, rotPlace)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle));
                    if (line.Contains(")p"))     //AI tags
                    {
                        // CheckPoints.X[CheckPoints.N] = Utility.GetInt("set", astring, 1);
                        // CheckPoints.Z[CheckPoints.N] = Utility.GetInt("set", astring, 2);
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
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }
                }
                if (line.StartsWith("chk"))
                {
                    var ymult = -1;
                    
                    if (!TryGetPieceToPlace(Utility.GetString("chk", line, 0), out var mesh)) continue;

                    if (mesh.FileName == "aircheckpoint" || reverseChkY)
                    {
                        ymult = 1; // default to inverted Y for stupid rollercoaster chks for compatibility reasons
                    }

                    var chkheight = World.Ground;

                    var rotPlace = 3;
                    if (swapYandRot)
                    {
                        rotPlace = 4;
                    }

                    AngleSingle rotation = AngleSingle.FromDegrees(Utility.GetInt("chk", line, rotPlace));

                    // Check if optional Y coordinate is provided (5 parameters instead of 4)
                    var hasCustomY = line.Split(',').Length >= 5;


                    if (hasCustomY)
                    {

                        if(swapYandRot)
                        {
                            chkheight = Utility.GetInt("chk", line, 3) * ymult;
                        }
                        else
                        {
                            chkheight = Utility.GetInt("chk", line, 4) * ymult;
                        }

                        pieces[stagePartCount] = new CheckPoint(
                            mesh,
                            new Vector3(Utility.GetInt("chk", line, 1), chkheight, Utility.GetInt("chk", line, 2)),
                            new Euler(rotation, AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
                        );
                        checkpoints[checkpoints.Count] = (CheckPoint)pieces[stagePartCount - 1];
                    }
                    else
                    {
                        pieces[stagePartCount] = new CheckPoint(
                            mesh,
                            new Vector3(Utility.GetInt("chk", line, 1), chkheight, Utility.GetInt("chk", line, 2)),
                            new Euler(rotation, AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
                        checkpoints[checkpoints.Count] = (CheckPoint)pieces[stagePartCount - 1];
                    }
                    
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

                    pieces[stagePartCount] = new FixHoop(
                        mesh,
                        new Vector3(Utility.GetInt("fix", line, 1), Utility.GetInt("fix", line, 3), Utility.GetInt("fix", line, 2)),
                        new Euler(AngleSingle.FromDegrees(Utility.GetInt("fix", line, 4)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                    );
                    // CheckPoints.Fx[CheckPoints.Fn] = Utility.GetInt("fix", astring, 1);
                    // CheckPoints.Fz[CheckPoints.Fn] = Utility.GetInt("fix", astring, 2);
                    // CheckPoints.Fy[CheckPoints.Fn] = Utility.GetInt("fix", astring, 3);
                    if (Utility.GetInt("fix", line, 4) != 0)
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = true;
                        ((FixHoop)pieces[stagePartCount - 1]).Rotated = true;
                    }
                    else
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = false;
                    }
                    //CheckPoints.Special[CheckPoints.Fn] = astring.IndexOf(")s") != -1;
                    //CheckPoints.Fn++;
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

                // stage walls
                var wall = GameSparker.GetStagePart("thewall");
                if (line.StartsWith("maxr"))
                {
                    var n = Utility.GetInt("maxr", line, 0);
                    var o = Utility.GetInt("maxr", line, 1);
                    maxr = o;
                    var p = Utility.GetInt("maxr", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            wall.Mesh,
                            new Vector3(o, World.Ground, q * 4800 + p),
                            Euler.Identity                        
                        );
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
                if (line.StartsWith("maxl"))
                {
                    var n = Utility.GetInt("maxl", line, 0);
                    var o = Utility.GetInt("maxl", line, 1);
                    maxl = o;
                    var p = Utility.GetInt("maxl", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            wall.Mesh,
                            new Vector3(o, World.Ground, q * 4800 + p),
                            new Euler(AngleSingle.FromDegrees(180), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
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
                if (line.StartsWith("maxt"))
                {
                    var n = Utility.GetInt("maxt", line, 0);
                    var o = Utility.GetInt("maxt", line, 1);
                    maxt = o;
                    var p = Utility.GetInt("maxt", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            wall.Mesh,
                            new Vector3(q * 4800 + p, World.Ground, o),
                            new Euler(AngleSingle.FromDegrees(90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
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
                if (line.StartsWith("maxb"))
                {
                    var n = Utility.GetInt("maxb", line, 0);
                    var o = Utility.GetInt("maxb", line, 1);
                    maxb = o;
                    var p = Utility.GetInt("maxb", line, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            wall.Mesh,
                            new Vector3(q * 4800 + p, World.Ground, o),
                            new Euler(AngleSingle.FromDegrees(-90), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
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

            if(musicPath.IsNullOrEmpty())
            {
                GameSparker.Writer.WriteLine("No music is defined for this stage!", "error");
            }

            // Medium.Newpolys(maxl, maxr - maxl, maxb, maxt - maxb, stagePartCount);
            // Medium.Newmountains(maxl, maxr, maxb, maxt);
            // Medium.Newclouds(maxl, maxr, maxb, maxt);
            // Medium.Newstars();
            Trackers.LoadTrackers(pieces, maxl, maxr - maxl, maxb, maxt - maxb);

            if (World.DrawPolys)
            {
                polys = NFMWorld.Mad.Environment.MakePolys(maxl, maxr - maxl, maxb, maxt - maxb, stagePartCount, graphicsDevice);
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

    private static bool TryGetPieceToPlace(string setstring, out Mesh mesh)
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
            mesh = stagePart.Mesh;
            if (stagePart.Id == -1)
            {
                GameSparker.Writer.WriteLine($"Stage part '{setstring}' not found.", "error");
                mesh = GameSparker.error_mesh;
                return true;
            }
        }

        return true;
    }

    public Mesh? CreateObject(string objectName, int x, int y, int z, int r)
    {
        var part = GameSparker.GetStagePart(objectName);
        if (part.Id == -1)
        {
            GameSparker.devConsole.Log($"Object '{objectName}' not found.", "warning");
            part = (-1, GameSparker.error_mesh);
        }

        var mesh = pieces[stagePartCount] = new Mesh(
            part.Mesh,
            new Vector3(x,
            250 - y,
            z), 
            new Euler(AngleSingle.FromDegrees(r), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
        );


        GameSparker.devConsole.Log($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}", "info");

        return mesh;
    }

    public void Render(Camera camera, Lighting? lighting = null)
    {
        sky.Render(camera, lighting);
        ground.Render(camera, lighting);
        polys?.Render(camera, lighting);
        clouds?.Render(camera, lighting);
        mountains?.Render(camera, lighting);

        foreach (var piece in pieces)
        {
            piece.Render(camera, lighting);
        }
    }
}