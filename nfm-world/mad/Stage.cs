using NFMWorld.Mad;
using NFMWorld.Util;

/**
    Represents a stage. Holds all information relating to track pices, scenery, etc.
    But does NOT hold any information relating to the actual game being played, unless such game affects the layout or scenery of the stage.
*/
public class Stage
{
    public UnlimitedArray<ContO> pieces = null!;

    public int stagePartCount
    {
        get { return pieces.Count; }
    }

    internal static int Getint(string astring, string string4, int i)
    {
        // TODO
        return Utility.Getint(astring, string4, i);
    }

    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    public Stage(string stageName)
    {
        int indexOffset = 10;

        pieces = [];

        Trackers.Nt = 0;
        Medium.Resdown = 0;
        Medium.Rescnt = 5;
        Medium.Lightson = false;
        Medium.Noelec = 0;
        Medium.Ground = 250;
        Medium.Trk = 0;
        Medium.drawClouds = true;
        Medium.drawMountains = true;
        Medium.drawStars = true;
        Medium.drawPolys = true;
        var i = 0;
        var k = 100;
        var l = 0;
        var m = 100;
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
                    Medium.Setsnap(Getint("snap", astring, 0), Getint("snap", astring, 1),
                        Getint("snap", astring, 2));
                }
                if (astring.StartsWith("sky"))
                {
                    Medium.Setsky(Getint("sky", astring, 0), Getint("sky", astring, 1), Getint("sky", astring, 2));
                }
                if (astring.StartsWith("ground"))
                {
                    Medium.Setgrnd(Getint("ground", astring, 0), Getint("ground", astring, 1),
                        Getint("ground", astring, 2));
                }
                if (astring.StartsWith("polys"))
                {
                    if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        Medium.drawPolys = false;
                    }
                    else
                    {
                        Medium.Setpolys(Getint("polys", astring, 0), Getint("polys", astring, 1),
                        Getint("polys", astring, 2));
                    }
                }
                if (astring.StartsWith("fog"))
                {
                    Medium.Setfade(Getint("fog", astring, 0), Getint("fog", astring, 1), Getint("fog", astring, 2));
                }
                if (astring.StartsWith("texture"))
                {
                    Medium.Setexture(Getint("texture", astring, 0), Getint("texture", astring, 1),
                        Getint("texture", astring, 2), Getint("texture", astring, 3));
                }
                if (astring.StartsWith("clouds"))
                {
                    if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        Medium.drawClouds = false;
                    }
                    else
                    {
                        Medium.Setclouds(Getint("clouds", astring, 0), Getint("clouds", astring, 1),
                            Getint("clouds", astring, 2), Getint("clouds", astring, 3), Getint("clouds", astring, 4));
                    }
                }
                if (astring.StartsWith("density"))
                {
                    Medium.Fogd = (Getint("density", astring, 0) + 1) * 2 - 1;
                    if (Medium.Fogd < 1)
                    {
                        Medium.Fogd = 1;
                    }
                    if (Medium.Fogd > 30)
                    {
                        Medium.Fogd = 30;
                    }
                }
                if (astring.StartsWith("fadefrom"))
                {
                    Medium.Fadfrom(Getint("fadefrom", astring, 0));
                }
                if (astring.StartsWith("lightson"))
                {
                    Medium.Lightson = true;
                }
                if (astring.StartsWith("mountains"))
                {
                    // Check for mountains(false) first
                    if (astring.Contains("false", StringComparison.OrdinalIgnoreCase))
                    {
                        Medium.drawMountains = false;
                    }
                    else
                    {
                        Medium.Mgen = Getint("mountains", astring, 0);
                    }
                }
                if (astring.StartsWith("set"))
                {
                    var setindex = Getint("set", astring, 0);
                    setindex -= indexOffset;
                    var setheight = Medium.Ground - GameSparker.stage_parts[setindex].Grat;
                    
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        setheight = Getint("set", astring, 4);
                        pieces[stagePartCount] = new ContO(pieces[setindex], Getint("set", astring, 1),
                            setheight, Getint("set", astring, 2),
                            Getint("set", astring, 3));
                    }
                    else
                    {
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[setindex], Getint("set", astring, 1),
                            setheight, Getint("set", astring, 2),
                            Getint("set", astring, 3));
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
                    // if (Medium.Loadnew)
                    // {
                    //     Medium.Loadnew = false;
                    // }
                }
                if (astring.StartsWith("chk"))
                {
                    var chkindex = Getint("chk", astring, 0);
                    chkindex -= indexOffset;

                    var chkheight = Medium.Ground - GameSparker.stage_parts[chkindex].Grat;

                    // Check if optional Y coordinate is provided (5 parameters instead of 4)
                    var hasCustomY = astring.Split(',').Length >= 5;
                    if (hasCustomY)
                    {
                        chkheight = Getint("chk", astring, 4);
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[chkindex], Getint("chk", astring, 1), chkheight,
                            Getint("chk", astring, 2), Getint("chk", astring, 3));
                    }
                    else
                    {
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[chkindex], Getint("chk", astring, 1), chkheight,
                            Getint("chk", astring, 2), Getint("chk", astring, 3));
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
                    //stage_parts[stagePartCount].Checkpoint = CheckPoints.Nsp + 1;
                    //CheckPoints.Nsp++;
                }
                if (astring.StartsWith("fix"))
                {
                    var fixindex = Getint("fix", astring, 0);
                    fixindex -= indexOffset;
                    pieces[stagePartCount] = new ContO(GameSparker.stage_parts[fixindex], Getint("fix", astring, 1),
                        Getint("fix", astring, 3),
                        Getint("fix", astring, 2), Getint("fix", astring, 4));
                    // CheckPoints.Fx[CheckPoints.Fn] = Getint("fix", astring, 1);
                    // CheckPoints.Fz[CheckPoints.Fn] = Getint("fix", astring, 2);
                    // CheckPoints.Fy[CheckPoints.Fn] = Getint("fix", astring, 3);
                    pieces[stagePartCount - 1].Elec = true;
                    if (Getint("fix", astring, 4) != 0)
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = true;
                        pieces[stagePartCount - 1].Roted = true;
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
                var wall = GameSparker.GetModel("thewall");
                if (astring.StartsWith("maxr"))
                {
                    var n = Getint("maxr", astring, 0);
                    var o = Getint("maxr", astring, 1);
                    i = o;
                    var p = Getint("maxr", astring, 2);
                    for (var q = 0; q < n; q++)
                    {
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[wall], o,
                            Medium.Ground - GameSparker.stage_parts[wall].Grat,
                            q * 4800 + p, 0);
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
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[wall], o, Medium.Ground - GameSparker.stage_parts[wall].Grat,
                            q * 4800 + p,
                            180);
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
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[wall], q * 4800 + p, Medium.Ground - GameSparker.stage_parts[wall].Grat,
                            o,
                            90);
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
                        pieces[stagePartCount] = new ContO(GameSparker.stage_parts[wall], q * 4800 + p, Medium.Ground - GameSparker.stage_parts[wall].Grat,
                            o,
                            -90);
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
            Medium.Newpolys(k, i - k, m, l - m, stagePartCount);
            Medium.Newmountains(k, i, m, l);
            Medium.Newclouds(k, i, m, l);
            Medium.Newstars();
            Trackers.Devidetrackers(k, i - k, m, l - m);
        }
        catch (Exception exception)
        {
            GameSparker.Writer.WriteLine("Error in stage: " + stageName, "error");
            GameSparker.Writer.WriteLine("At line: " + astring, "error");
            GameSparker.Writer.WriteLine(exception.ToString(), "error");
        }
        GC.Collect();
    }

    public void CreateObject(string objectName, int x, int y, int z, int r)
    {
        var model = GameSparker.GetModel(objectName);
        if (model == -1)
        {
            GameSparker.devConsole.Log($"Object '{objectName}' not found.", "warning");
            return;
        }

        pieces[stagePartCount] = new ContO(
            GameSparker.stage_parts[model], x, 250 - GameSparker.stage_parts[model].Grat - y, z, r);


        GameSparker.devConsole.Log($"Created {objectName} at ({x}, {y}, {z}), rotation: {r}", "info");
    }
}