using System.Diagnostics;
using NFMWorld.Mad.Interp;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameSparker
{
    private static Stopwatch timer;
    private static ContO[] cars;

    private static readonly string[] CarRads = { "2000tornados" };

    private static long accumulator = 0;
    private static long lastFrameTime = 0;
    /* Frequency of physics ticks */
    private static int physics_dt = 47;

    private static MediumState currentMediumState;
    private static MediumState prevMediumState;

    public static void Load()
    {
        timer.Start();
        new Medium();
        currentMediumState = new MediumState();
        prevMediumState = new MediumState();

        Medium.D();
        
        cars = new ContO[10];

        FileUtil.LoadFiles("../data/cars", CarRads, (ais, id) =>
        {
            cars[id] = new ContO(ais);
            if (!cars[id].Shadow)
            {
                throw new Exception("car does not have a shadow");
            }
        });
    }

    public static void GameTick()
    {
        if(lastFrameTime == 0) 
            lastFrameTime = timer.ElapsedMilliseconds;

        accumulator += timer.ElapsedMilliseconds - lastFrameTime;


        while(accumulator >= physics_dt)
        {
            accumulator -= physics_dt;
            Medium.Around(cars[0], true);

            prevMediumState = currentMediumState;
            currentMediumState = new MediumState();
        }

        float interp_ratio = accumulator / (float)physics_dt;
        MediumState interp_state = currentMediumState.InterpWith(prevMediumState, interp_ratio);
        interp_state.Apply();

        Console.WriteLine(currentMediumState.X + ", " + prevMediumState.X + ", " + interp_state.X + ", " + interp_ratio);

        Render();

        currentMediumState.Apply();
        lastFrameTime = timer.ElapsedMilliseconds;
    }

    private static void Render()
    {
        Medium.D();
        cars[0].D();
    }
}