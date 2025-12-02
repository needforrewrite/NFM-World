using System.Diagnostics;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameSparker
{
    private static MicroStopwatch timer;
    private static UnlimitedArray<ContO> cars;
    private static UnlimitedArray<ContO> stage_parts;
    private static UnlimitedArray<ContO> placed_stage_elements;
    private static CarState[] current_car_states;
    private static CarState[] prev_car_states;

    private static readonly string[] CarRads = {
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

    private static long accumulator = 0;
    internal static long lastTickTime = 0;
    internal static long lastRenderTime = 0;
    public static float PHYSICS_MULTIPLIER = 0.3377f;

    /* Frequency of physics ticks */
    private static int physics_dt_us = (int)(47000*PHYSICS_MULTIPLIER);

    private static MediumState currentMediumState;
    private static MediumState prevMediumState;

    // View modes
    public enum ViewMode
    {
        Follow,
        Around,
    }
    private static ViewMode currentViewMode = ViewMode.Follow;
    /////////////////////////////////

    private static UnlimitedArray<Car> cars_in_race = [];
    private static int playerCarIndex = 0;

    // stage loading
    private static int _indexOffset = 10;
    public static int _stagePartCount = 0;

public static void KeyPressed(Keys key)
    {
        //if (!_exwist)
        //{
            //115 114 99
            if (key == Keys.Up)
            {
                cars_in_race[playerCarIndex].Control.Up = true;
            }
            if (key == Keys.Down)
            {
                cars_in_race[playerCarIndex].Control.Down = true;
            }
            if (key == Keys.Right)
            {
                cars_in_race[playerCarIndex].Control.Right = true;
            }
            if (key == Keys.Left)
            {
                cars_in_race[playerCarIndex].Control.Left = true;
            }
            if (key == Keys.Space)
            {
                cars_in_race[playerCarIndex].Control.Handb = true;
            }
            if (key == Keys.Enter)
            {
                cars_in_race[playerCarIndex].Control.Enter = true;
            }
            if (key == Keys.Z)
            {
                cars_in_race[playerCarIndex].Control.Lookback = -1;
            }
            if (key == Keys.X)
            {
                cars_in_race[playerCarIndex].Control.Lookback = 1;
            }
            if (key == Keys.M)
            {
                cars_in_race[playerCarIndex].Control.Mutem = !cars_in_race[playerCarIndex].Control.Mutem;
            }

            if (key == Keys.N)
            {
                cars_in_race[playerCarIndex].Control.Mutes = !cars_in_race[playerCarIndex].Control.Mutes;
            }

            if (key == Keys.A)
            {
                cars_in_race[playerCarIndex].Control.Arrace = !cars_in_race[playerCarIndex].Control.Arrace;
            }

            if (key == Keys.S)
            {
                cars_in_race[playerCarIndex].Control.Radar = !cars_in_race[playerCarIndex].Control.Radar;
            }
            if (key == Keys.V)
            {
                currentViewMode = (ViewMode)(((int)currentViewMode + 1) % Enum.GetValues<ViewMode>().Length);
            }
        //}
    }

    public static void KeyReleased(Keys key)
    {
        //if (!_exwist)
        //{
            if (cars_in_race[playerCarIndex].Control.Multion < 2)
            {
                if (key == Keys.Up)
                {
                    cars_in_race[playerCarIndex].Control.Up = false;
                }
                if (key == Keys.Down)
                {
                    cars_in_race[playerCarIndex].Control.Down = false;
                }
                if (key == Keys.Right)
                {
                    cars_in_race[playerCarIndex].Control.Right = false;
                }
                if (key == Keys.Left)
                {
                    cars_in_race[playerCarIndex].Control.Left = false;
                }
                if (key == Keys.Space)
                {
                    cars_in_race[playerCarIndex].Control.Handb = false;
                }
            }
            if (key == Keys.Escape)
            {
                cars_in_race[playerCarIndex].Control.Exit = false;
//                if (Madness.fullscreen)
//                {
//                    Madness.exitfullscreen();
//                }
            }
            if (key == Keys.X || key == Keys.Z)
            {
                cars_in_race[playerCarIndex].Control.Lookback = 0;
            }
        //}
    }

    private static int GetModel(string input)
    {
        // Combine all model arrays
        string[][] allModels = new string[][]
        {
            StageRads
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

    public static void Load()
    {
        timer = new MicroStopwatch();
        timer.Start();
        new Medium();
        currentMediumState = new MediumState();
        prevMediumState = new MediumState();

        Medium.Groundpolys();
        Medium.D();
        
        cars = [];
        stage_parts = [];
        placed_stage_elements = [];

        current_car_states = new CarState[100];
        prev_car_states = new CarState[100];

        for(int i = 0; i < 100; i++) {
            current_car_states[i] = new CarState();
            prev_car_states[i] = new CarState();
        }

        FileUtil.LoadFiles("./data/models/cars", CarRads, (ais, id) => {
            cars[id] = new ContO(ais);
            if (!cars[id].Shadow)
            {
                throw new Exception("car does not have a shadow");
            }
        });

        FileUtil.LoadFiles("./data/models/stage", StageRads, (ais, id) => {
            stage_parts[id] = new ContO(ais);
        });

        Loadstage("1");

        cars_in_race[playerCarIndex] = new Car(new Stat(14), 0, cars[14], 0, 0);

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

        Medium.Fadfrom(5000);
    }

    internal static int Getint(string astring, string string4, int i)
    {
        // TODO
        return Utility.Getint(astring, string4, i);
    }



    /**
     * Loads stage currently set by checkpoints.stage onto stageContos
     */
    private static void Loadstage(string stage)
    {
        placed_stage_elements.Clear();
        _stagePartCount = 0;
        Trackers.Nt = 0;
        Medium.Resdown = 0;
        Medium.Rescnt = 5;
        Medium.Lightson = false;
        Medium.Noelec = 0;
        Medium.Ground = 250;
        Medium.Trk = 0;
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
                    Medium.Setpolys(Getint("polys", astring, 0), Getint("polys", astring, 1),
                        Getint("polys", astring, 2));
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
                    Medium.Setcloads(Getint("clouds", astring, 0), Getint("clouds", astring, 1),
                        Getint("clouds", astring, 2), Getint("clouds", astring, 3), Getint("clouds", astring, 4));
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
                    Medium.Mgen = Getint("mountains", astring, 0);
                }
                if (astring.StartsWith("set"))
                {
                    var setindex = Getint("set", astring, 0);

                    setindex -= _indexOffset;
                    Console.WriteLine("Setindex ais: " + setindex);
                    // ok why does it not load certain shit and doesnt assign correct pieces properly, very strange
                    placed_stage_elements[_stagePartCount] = new ContO(stage_parts[setindex], Getint("set", astring, 1),
                        Medium.Ground - stage_parts[setindex].Grat, Getint("set", astring, 2),
                        Getint("set", astring, 3));
                    if (astring.Contains(")p"))
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
                    var chkheight = Medium.Ground - stage_parts[chkindex].Grat;
                    placed_stage_elements[_stagePartCount] = new ContO(stage_parts[chkindex], Getint("chk", astring, 1), chkheight,
                        Getint("chk", astring, 2), Getint("chk", astring, 3));
                    
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
                    placed_stage_elements[_stagePartCount] = new ContO(stage_parts[fixindex], Getint("fix", astring, 1),
                        Getint("fix", astring, 3),
                        Getint("fix", astring, 2), Getint("fix", astring, 4));
                    // CheckPoints.Fx[CheckPoints.Fn] = Getint("fix", astring, 1);
                    // CheckPoints.Fz[CheckPoints.Fn] = Getint("fix", astring, 2);
                    // CheckPoints.Fy[CheckPoints.Fn] = Getint("fix", astring, 3);
                    placed_stage_elements[_stagePartCount].Elec = true;
                    if (Getint("fix", astring, 4) != 0)
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = true;
                        placed_stage_elements[_stagePartCount].Roted = true;
                    }
                    else
                    {
                        //CheckPoints.Roted[CheckPoints.Fn] = false;
                    }
                    //CheckPoints.Special[CheckPoints.Fn] = astring.IndexOf(")s") != -1;
                    //CheckPoints.Fn++;
                    _stagePartCount++;
                }
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
                        placed_stage_elements[_stagePartCount] = new ContO(stage_parts[wall], o,
                            Medium.Ground - stage_parts[29].Grat, //29 may need to be 85 or xtgraphics.nCars - 16
                            q * 4800 + p, 0);
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
                        placed_stage_elements[_stagePartCount] = new ContO(stage_parts[wall], o, Medium.Ground - stage_parts[wall].Grat,
                            q * 4800 + p,
                            180);
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
                        placed_stage_elements[_stagePartCount] = new ContO(stage_parts[wall], q * 4800 + p, Medium.Ground - stage_parts[wall].Grat,
                            o,
                            90);
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
                        placed_stage_elements[_stagePartCount] = new ContO(stage_parts[wall], q * 4800 + p, Medium.Ground - stage_parts[wall].Grat,
                            o,
                            -90);
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
            //Medium.Newpolys(k, i - k, m, l - m, _notb);
            Medium.Newclouds(k, i, m, l);
            Medium.Newmountains(k, i, m, l);
            Medium.Newstars();
            Trackers.Devidetrackers(k, i - k, m, l - m);
        }
        catch (Exception exception)
        {
            Console.WriteLine("Error in stage " + stage);
            Console.WriteLine("At line: " + astring);
            Console.WriteLine(exception);
        }
        GC.Collect();
    }

    public static void GameTick()
    {
        var tickStartTime = timer.ElapsedMicroseconds;

        cars_in_race[playerCarIndex].Drive();
        switch (currentViewMode)
        {
            case ViewMode.Follow:
                Medium.Follow(cars_in_race[playerCarIndex].Conto, cars_in_race[playerCarIndex].Mad.Cxz, cars_in_race[playerCarIndex].Control.Lookback);
                break;
            case ViewMode.Around:
                Medium.Around(cars_in_race[playerCarIndex].Conto, true);
                break;
        }

        lastTickTime = timer.ElapsedMicroseconds - tickStartTime;
    }

    public static void Render()
    {
        var tickStartTime = timer.ElapsedMicroseconds;

        Medium.D();

        var renderQueue = new UnlimitedArray<ContO>(placed_stage_elements.Count);

        var j = 0;
        // process player cars
        foreach (var car in cars_in_race)
        {
            if (car.Conto.Dist != 0)
            {
                renderQueue[j++] = car.Conto;
            }
            else if (car.Conto.Dist == 0)
            {
                car.Conto.D();
            }
        }
        
        // process stage elements
        foreach (var contO in placed_stage_elements)
        {
            if (contO.Dist != 0)
            {
                renderQueue[j++] = contO;
            }
            else if (contO.Dist == 0)
            {
                contO.D();
            }
        }

        // sort the render queue by distance in descending order
        renderQueue.Sort(static (a, b) => b.Dist.CompareTo(a.Dist));

        // render all objects in the sorted order
        foreach (var obj in renderQueue)
        {
            obj.D();
        }

        lastRenderTime = timer.ElapsedMicroseconds - tickStartTime;
    }
}