using System.Diagnostics;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameSparker
{
    private static MicroStopwatch timer;
    private static ContO[] cars;
    private static ContO[] stage_parts;
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
    private static long lastFrameTime = 0;
    /* Frequency of physics ticks */
    private static int physics_dt_us = (int)(47000*0.333333f);

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

    private static Control playerControl;
    private static Control AIControl;
    private static Mad playerMad;
    private static Mad AIMad;

public static void KeyPressed(Keys key)
    {
        //if (!_exwist)
        //{
            //115 114 99
            if (key == Keys.Up)
            {
                playerControl.Up = true;
            }
            if (key == Keys.Down)
            {
                playerControl.Down = true;
            }
            if (key == Keys.Right)
            {
                playerControl.Right = true;
            }
            if (key == Keys.Left)
            {
                playerControl.Left = true;
            }
            if (key == Keys.Space)
            {
                playerControl.Handb = true;
            }
            if (key == Keys.Enter)
            {
                playerControl.Enter = true;
            }
            if (key == Keys.Z)
            {
                playerControl.Lookback = -1;
            }
            if (key == Keys.X)
            {
                playerControl.Lookback = 1;
            }
            if (key == Keys.M)
            {
                playerControl.Mutem = !playerControl.Mutem;
            }

            if (key == Keys.N)
            {
                playerControl.Mutes = !playerControl.Mutes;
            }

            if (key == Keys.A)
            {
                playerControl.Arrace = !playerControl.Arrace;
            }

            if (key == Keys.S)
            {
                playerControl.Radar = !playerControl.Radar;
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
            if (playerControl.Multion < 2)
            {
                if (key == Keys.Up)
                {
                    playerControl.Up = false;
                }
                if (key == Keys.Down)
                {
                    playerControl.Down = false;
                }
                if (key == Keys.Right)
                {
                    playerControl.Right = false;
                }
                if (key == Keys.Left)
                {
                    playerControl.Left = false;
                }
                if (key == Keys.Space)
                {
                    playerControl.Handb = false;
                }
            }
            if (key == Keys.Escape)
            {
                playerControl.Exit = false;
//                if (Madness.fullscreen)
//                {
//                    Madness.exitfullscreen();
//                }
            }
            if (key == Keys.X || key == Keys.Z)
            {
                playerControl.Lookback = 0;
            }
        //}
    }

    public static void Load()
    {
        Trackers.Devidetrackers(10000, 10000, 10000, 10000);

        playerMad = new Mad(new Stat(14), 0);
        playerControl = new Control();
        timer = new MicroStopwatch();
        timer.Start();
        new Medium();
        currentMediumState = new MediumState();
        prevMediumState = new MediumState();

        Medium.Groundpolys();
        Medium.D();
        
        cars = new ContO[100];
        stage_parts = new ContO[100];

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


        // part gallery stage, kill asap when proper stage loading is done
        cars[0] = new ContO(cars[14], 0, 0, 0, 0);

        stage_parts[0] = new ContO(stage_parts[37], 0, 250, 0, 0);
        stage_parts[1] = new ContO(stage_parts[0], 0, 250, 5600, 0);
        stage_parts[2] = new ContO(stage_parts[16], 0, 250, 3400, 0);
        stage_parts[3] = new ContO(stage_parts[13], 0, 250, 8400, 180);
        stage_parts[4] = new ContO(stage_parts[13], 0, 250, -2800, 0);
        //its 4:08 am, idk why the fuck it keeps repeating the first 5 pieces wtf
        stage_parts[5] = new ContO(stage_parts[0], 0, 250, 18000, 0);
        stage_parts[6] = new ContO(stage_parts[1], 3000, 250, 18000, 0);
        stage_parts[7] = new ContO(stage_parts[2], 6000, 250, 18000, 0);
        stage_parts[8] = new ContO(stage_parts[3], 9000, 250, 18000, 0);
        stage_parts[9] = new ContO(stage_parts[4], 12000, 250, 18000, 0);
        stage_parts[10] = new ContO(stage_parts[5], 15000, 250, 18000, 0);
        stage_parts[11] = new ContO(stage_parts[6], 18000, 250, 18000, 0);
        stage_parts[12] = new ContO(stage_parts[7], 21000, 250, 18000, 0);
        stage_parts[13] = new ContO(stage_parts[8], 24000, 250, 18000, 0);
        stage_parts[14] = new ContO(stage_parts[9], 27000, 250, 18000, 0);
        stage_parts[15] = new ContO(stage_parts[10], 30000, 250, 18000, 0);
        stage_parts[16] = new ContO(stage_parts[11], 33000, 250, 18000, 0);
        stage_parts[17] = new ContO(stage_parts[12], 36000, 250, 18000, 0);
        stage_parts[18] = new ContO(stage_parts[13], 39000, 250, 18000, 0);
        stage_parts[19] = new ContO(stage_parts[14], 42000, 250, 18000, 0);
        stage_parts[20] = new ContO(stage_parts[15], 45000, 250, 18000, 0);
        stage_parts[21] = new ContO(stage_parts[16], 48000, 250, 18000, 0);
        stage_parts[22] = new ContO(stage_parts[17], 51000, 250, 18000, 0);
        stage_parts[23] = new ContO(stage_parts[18], 54000, 250, 18000, 0);
        stage_parts[24] = new ContO(stage_parts[19], 57000, 250, 18000, 0);
        stage_parts[25] = new ContO(stage_parts[20], 60000, 250, 18000, 0);
        stage_parts[26] = new ContO(stage_parts[21], 63000, 250, 18000, 0);
        stage_parts[27] = new ContO(stage_parts[22], 66000, 250, 18000, 0);
        stage_parts[28] = new ContO(stage_parts[23], 69000, 250, 18000, 0);
        stage_parts[29] = new ContO(stage_parts[24], 72000, 250, 18000, 0);
        stage_parts[30] = new ContO(stage_parts[25], 75000, 250, 18000, 0);
        stage_parts[31] = new ContO(stage_parts[26], 78000, 250, 18000, 0);
        stage_parts[32] = new ContO(stage_parts[27], 81000, 250, 18000, 0);
        stage_parts[33] = new ContO(stage_parts[28], 84000, 250, 18000, 0);
        stage_parts[34] = new ContO(stage_parts[29], 87000, 250, 18000, 0);
        stage_parts[35] = new ContO(stage_parts[30], 90000, 250, 18000, 0);
        stage_parts[36] = new ContO(stage_parts[31], 93000, 250, 18000, 0);
        stage_parts[37] = new ContO(stage_parts[32], 96000, 250, 18000, 0);
        stage_parts[38] = new ContO(stage_parts[33], 99000, 250, 18000, 0);
        stage_parts[39] = new ContO(stage_parts[34], 102000, 250, 18000, 0);
        stage_parts[40] = new ContO(stage_parts[35], 105000, 250, 18000, 0);
        stage_parts[41] = new ContO(stage_parts[36], 108000, 250, 18000, 0);
        stage_parts[42] = new ContO(stage_parts[37], 111000, 250, 18000, 0);
        stage_parts[43] = new ContO(stage_parts[38], 114000, 250, 18000, 0);


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

    public static void GameTick()
    {
        if(lastFrameTime == 0) 
            lastFrameTime = timer.ElapsedMicroseconds;

        accumulator += timer.ElapsedMicroseconds - lastFrameTime;

        while(accumulator >= physics_dt_us)
        {
            accumulator -= physics_dt_us;

            //Medium.Follow(cars[0], playerMad.Cxz, playerControl.Lookback);
            //Medium.Around(cars[0], true);

            switch (currentViewMode)
            {
                case ViewMode.Follow:
                    Medium.Follow(cars[0], playerMad.Cxz, playerControl.Lookback);
                    break;
                case ViewMode.Around:
                    Medium.Around(cars[0], true);
                    break;
            }

            playerMad.Drive(playerControl, cars[0]);

            prevMediumState = currentMediumState;
            currentMediumState = new MediumState();

            prev_car_states[0] = current_car_states[0];
            current_car_states[0] = new CarState(cars[0]);
        }

        float interp_ratio = accumulator / (float)physics_dt_us;

        MediumState medium_interp_state = currentMediumState.InterpWith(prevMediumState, interp_ratio);
        medium_interp_state.Apply();

        CarState car_interp_state = current_car_states[0].InterpWith(prev_car_states[0], interp_ratio);
        car_interp_state.Apply(cars[0]);

        Render();

        current_car_states[0].Apply(cars[0]);
        currentMediumState.Apply();
        lastFrameTime = timer.ElapsedMicroseconds;
    }

    private static void Render()
    {
        Medium.D();

        var renderQueue = new List<ContO>();

        // process instantiated stage parts
        for (int i = 0; i < stage_parts.Length; i++)
        {
            if (stage_parts[i] != null && stage_parts[i].Dist != 0 && stage_parts[i].IsInstantiated)
            {
                renderQueue.Add(stage_parts[i]);
            }
            else if (stage_parts[i] != null && stage_parts[i].Dist == 0 && stage_parts[i].IsInstantiated)
            {
                stage_parts[i].D();
            }
        }

        // process instantiated cars
        for (int i = 0; i < cars.Length; i++)
        {
            if (cars[i] != null && cars[i].Dist != 0 && cars[i].IsInstantiated)
            {
                renderQueue.Add(cars[i]);
            }
            else if (cars[i] != null && cars[i].Dist == 0 && cars[i].IsInstantiated)
            {
                cars[i].D();
            }
        }

        // sort the render queue by distance in descending order
        renderQueue.Sort((a, b) => b.Dist.CompareTo(a.Dist));

        // render all objects in the sorted order
        foreach (var obj in renderQueue)
        {
            obj.D();
        }
    }
}