using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Mad;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using Color3 = NFMWorld.Mad.Color3;

/**
    Represents a stage. Holds all information relating to track pices, scenery, etc.
    But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class Stage
{
    public UnlimitedArray<Mesh> pieces = [];

    public int stagePartCount => pieces.Count;

    public Sky? sky;

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public Stage(string stageName, GraphicsDevice graphicsDevice)
    {
        int indexOffset = 10;

        Trackers.Nt = 0;
        // Medium.Resdown = 0;
        // Medium.Rescnt = 5;
        // Medium.Lightson = false;
        // Medium.Noelec = 0;
        // Medium.Ground = 250;
        // Medium.Trk = 0;
        // Medium.drawClouds = true;
        // Medium.drawMountains = true;
        // Medium.drawStars = true;
        // Medium.drawPolys = true;
        var maxr = 0;
        var maxl = 100;
        var maxt = 0;
        var maxb = 100;
        var astring = "";
        try
        {
            //var customStagePath = "stages/" + CheckPoints.Stage + ".txt";
            var customStagePath = "data/stages/" + stageName + ".txt";
            foreach (var line in System.IO.File.ReadAllLines(customStagePath))
            {
                astring = "" + line.Trim();
                if (astring.StartsWith("snap"))
                {
                    World.Snap = new Color3(
                        (short) Utility.GetInt("snap", astring, 0),
                        (short) Utility.GetInt("snap", astring, 1),
                        (short) Utility.GetInt("snap", astring, 2)
                    );
                }
                if (astring.StartsWith("sky"))
                {
                    World.Sky = new Color3(
                        (short) Utility.GetInt("sky", astring, 0),
                        (short) Utility.GetInt("sky", astring, 1),
                        (short) Utility.GetInt("sky", astring, 2)
                    );
                }
                if (astring.StartsWith("ground"))
                {
                    // Medium.Setgrnd(Utility.GetInt("ground", astring, 0), Utility.GetInt("ground", astring, 1),
                    //     Utility.GetInt("ground", astring, 2));
                }
                if (astring.StartsWith("polys"))
                {
                    // if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     Medium.drawPolys = false;
                    // }
                    // else
                    // {
                    //     Medium.Setpolys(Utility.GetInt("polys", astring, 0), Utility.GetInt("polys", astring, 1),
                    //     Utility.GetInt("polys", astring, 2));
                    // }
                }
                if (astring.StartsWith("fog"))
                {
                    World.Fog = new Color3(
                        (short)Utility.GetInt("fog", astring, 0),
                        (short)Utility.GetInt("fog", astring, 1),
                        (short)Utility.GetInt("fog", astring, 2)
                    );
                }
                if (astring.StartsWith("texture"))
                {
                    // Medium.Setexture(Utility.GetInt("texture", astring, 0), Utility.GetInt("texture", astring, 1),
                    //     Utility.GetInt("texture", astring, 2), Utility.GetInt("texture", astring, 3));
                }
                if (astring.StartsWith("clouds"))
                {
                    // if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     Medium.drawClouds = false;
                    // }
                    // else
                    // {
                    //     Medium.Setclouds(Utility.GetInt("clouds", astring, 0), Utility.GetInt("clouds", astring, 1),
                    //         Utility.GetInt("clouds", astring, 2), Utility.GetInt("clouds", astring, 3), Utility.GetInt("clouds", astring, 4));
                    // }
                }
                if (astring.StartsWith("density"))
                {
                    // Medium.Fogd = (Utility.GetInt("density", astring, 0) + 1) * 2 - 1;
                    // if (Medium.Fogd < 1)
                    // {
                    //     Medium.Fogd = 1;
                    // }
                    // if (Medium.Fogd > 30)
                    // {
                    //     Medium.Fogd = 30;
                    // }
                }
                if (astring.StartsWith("fadefrom"))
                {
                    World.FadeFrom = Utility.GetInt("fadefrom", astring, 0);
                }
                if (astring.StartsWith("lightson"))
                {
                    // Medium.Lightson = true;
                }
                if (astring.StartsWith("mountains"))
                {
                    // // Check for mountains(false) first
                    // if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    // {
                    //     Medium.drawMountains = false;
                    // }
                    // else
                    // {
                    //     Medium.Mgen = Utility.GetInt("mountains", astring, 0);
                    // }
                }
                if (astring.StartsWith("set"))
                {
                    var setindex = Utility.GetInt("set", astring, 0);
                    setindex -= indexOffset;
                    var setheight = World.Ground - GameSparker.stage_parts[setindex].GroundAt;
                    
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        setheight = Utility.GetInt("set", astring, 4);
                    }

                    pieces[stagePartCount] = new Mesh(
                        GameSparker.stage_parts[setindex],
                        new Vector3(Utility.GetInt("set", astring, 1), setheight, Utility.GetInt("set", astring, 2)),
                        new Euler(AngleSingle.FromDegrees(Utility.GetInt("set", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle));
                    if (astring.Contains(")p"))     //AI tags
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
                if (astring.StartsWith("chk"))
                {
                    var chkindex = Utility.GetInt("chk", astring, 0);
                    chkindex -= indexOffset;
                    var chkheight = World.Ground - GameSparker.stage_parts[chkindex].GroundAt;


                    // Check if optional Y coordinate is provided (5 parameters instead of 4)
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        chkheight = Utility.GetInt("chk", astring, 4);
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[chkindex],
                            new Vector3(Utility.GetInt("chk", astring, 1), chkheight, Utility.GetInt("chk", astring, 2)),
                            new Euler(AngleSingle.FromDegrees(Utility.GetInt("chk", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
                        );
                    }
                    else
                    {
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[chkindex],
                            new Vector3(Utility.GetInt("set", astring, 1), chkheight, Utility.GetInt("set", astring, 2)),
                            new Euler(AngleSingle.FromDegrees(Utility.GetInt("set", astring, 3)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                        );
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
                if (astring.StartsWith("fix"))
                {
                    var fixindex = Utility.GetInt("fix", astring, 0);
                    fixindex -= indexOffset;
                    pieces[stagePartCount] = new FixHoop(
                        GameSparker.stage_parts[fixindex],
                        new Vector3(Utility.GetInt("set", astring, 1), Utility.GetInt("set", astring, 3), Utility.GetInt("set", astring, 2)),
                        new Euler(AngleSingle.FromDegrees(Utility.GetInt("set", astring, 4)), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)                        
                    );
                    // CheckPoints.Fx[CheckPoints.Fn] = Utility.GetInt("fix", astring, 1);
                    // CheckPoints.Fz[CheckPoints.Fn] = Utility.GetInt("fix", astring, 2);
                    // CheckPoints.Fy[CheckPoints.Fn] = Utility.GetInt("fix", astring, 3);
                    if (Utility.GetInt("fix", astring, 4) != 0)
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = true;
                        ((FixHoop)pieces[stagePartCount]).Rotated = true;
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
                if (astring.StartsWith("nlaps"))
                {
                    //CheckPoints.Nlaps = Utility.GetInt("nlaps", astring, 0);
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
                    //CheckPoints.Pubt = Utility.GetInt("publish", astring, 0);
                }
                if (astring.StartsWith("soundtrack"))
                {
                    
                }

                // stage walls
                var wall = GameSparker.GetModel("thewall");
                if (astring.StartsWith("maxr"))
                {
                    var n = Utility.GetInt("maxr", astring, 0);
                    var o = Utility.GetInt("maxr", astring, 1);
                    maxr = o;
                    var p = Utility.GetInt("maxr", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[wall],
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
                if (astring.StartsWith("maxl"))
                {
                    var n = Utility.GetInt("maxl", astring, 0);
                    var o = Utility.GetInt("maxl", astring, 1);
                    maxl = o;
                    var p = Utility.GetInt("maxl", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[wall],
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
                if (astring.StartsWith("maxt"))
                {
                    var n = Utility.GetInt("maxt", astring, 0);
                    var o = Utility.GetInt("maxt", astring, 1);
                    maxt = o;
                    var p = Utility.GetInt("maxt", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[wall],
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
                if (astring.StartsWith("maxb"))
                {
                    var n = Utility.GetInt("maxb", astring, 0);
                    var o = Utility.GetInt("maxb", astring, 1);
                    maxb = o;
                    var p = Utility.GetInt("maxb", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new Mesh(
                            GameSparker.stage_parts[wall],
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
            // Medium.Newpolys(k, i - k, m, l - m, stagePartCount);
            // Medium.Newmountains(k, i, m, l);
            // Medium.Newclouds(k, i, m, l);
            // Medium.Newstars();
            Trackers.LoadTrackers(pieces, maxl, maxr - maxl, maxb, maxt - maxb);
        }
        catch (Exception exception)
        {
            GameSparker.Writer.WriteLine("Error in stage: " + stageName, "error");
            GameSparker.Writer.WriteLine("At line: " + astring, "error");
            GameSparker.Writer.WriteLine(exception.ToString(), "error");
        }
        sky = new Sky(graphicsDevice);
    }

    public void CreateObject(string objectName, int x, int y, int z, int r)
    {
        var model = GameSparker.GetModel(objectName);
        if (model == -1)
        {
            GameSparker.devConsole.Log($"Object '{objectName}' not found.", "warning");
            return;
        }

        pieces[stagePartCount] = new Mesh(
            GameSparker.stage_parts[model],
            new Vector3(x,
            250 - GameSparker.stage_parts[model].GroundAt - y,
            z), 
            new Euler(AngleSingle.FromDegrees(r), AngleSingle.ZeroAngle, AngleSingle.ZeroAngle)
        );


        GameSparker.devConsole.Log($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}", "info");
    }

    public void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        if (!isCreateShadowMap)
            sky?.Render(camera);
        
        foreach (var element in pieces)
        {
            element.Render(camera, lightCamera, isCreateShadowMap);
        }
    }
}