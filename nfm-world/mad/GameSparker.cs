using System.Diagnostics;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameSparker
{
    private static MicroStopwatch timer;
    private static ContO[] cars;

    private static readonly string[] CarRads = { "2000tornados" };

    private static long accumulator = 0;
    private static long lastFrameTime = 0;
    /* Frequency of physics ticks */
    private static int physics_dt_us = 47000;

    private static MediumState currentMediumState;
    private static MediumState prevMediumState;

    private static Control playerControl;
    private static Mad playerMad;

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

            /*if (key == Keys.V)
            {
                _view++;
                if (_view == 3)
                {
                    _view = 0;
                }
            }*/
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
        Trackers.Devidetrackers(100, 100, 100, 100);

        playerMad = new Mad(new Stat(0), 0);
        playerControl = new Control();
        timer = new MicroStopwatch();
        timer.Start();
        new Medium();
        currentMediumState = new MediumState();
        prevMediumState = new MediumState();

        Medium.D();
        
        cars = new ContO[10];

        FileUtil.LoadFiles("../data/cars", CarRads, (ais, id) =>
        {
            cars[id] = new ContO(ais);
            cars[id] = new ContO(cars[id], 0, 0, 0, 0);
            if (!cars[id].Shadow)
            {
                throw new Exception("car does not have a shadow");
            }
        });
    }

    public static void GameTick()
    {
        if(lastFrameTime == 0) 
            lastFrameTime = timer.ElapsedMicroseconds;

        accumulator += timer.ElapsedMicroseconds - lastFrameTime;

        while(accumulator >= physics_dt_us)
        {
            accumulator -= physics_dt_us;

            Medium.Follow(cars[0], playerMad.Cxz, playerControl.Lookback);
            playerMad.Drive(playerControl, cars[0]);

            prevMediumState = currentMediumState;
            currentMediumState = new MediumState();
        }

        float interp_ratio = accumulator / (float)physics_dt_us;
        MediumState interp_state = currentMediumState.InterpWith(prevMediumState, interp_ratio);
        interp_state.Apply();

        Render();

        currentMediumState.Apply();
        lastFrameTime = timer.ElapsedMicroseconds;
    }

    private static void Render()
    {
        Medium.D();
        cars[0].D();
    }
}