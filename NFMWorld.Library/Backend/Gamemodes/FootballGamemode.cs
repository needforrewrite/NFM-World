namespace NFMWorldLibrary.Backend.Gamemodes;

public class FootballGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues)
    : BaseGamemode(gamemodeParameters, raceValues)
{
    public override event EventHandler<byte[]>? RaceFinished;

    private int _newTick = 0;

    public override void Enter()
    {
        carsInRace[playerCarIndex] = new BackendCar(BackendGameSparker.GetCar(player.CarName).Rad!, playerCarIndex, 500, 0, true);
        carsInRace[1] = new BackendCar(BackendGameSparker.GetCar("football/BALL").Rad!, 1, 0, 0, false);

        Reset();
    }

    public override void Exit()
    {
        
    }

    public override void Reset()
    {
        base.Reset();
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {carsInRace[0].Position.X:0.00}, contoz: {carsInRace[0].Position.Z:0.00}, contoy: {carsInRace[0].Position.Y:0.00}");

        // Inter-car collision is run at the original tickrate (21.4TPS) to emulate original physics behavior
        // We round this up to 3 ticks per 63TPS tick.


        //All footballers have no powerloss.

        if (++_newTick == Physics.OriginalTicksPerNewTick)
        {
            for (int i = 0; i < carsInRace.Count; i++)
            for (int j = 0; j < carsInRace.Count; j++)
            {
                if (i != j)
                {
                    carsInRace[i].Collide(carsInRace[j]);
                }
            }

            _newTick = 0;
        }

        foreach (var car in carsInRace)
        {
            car.Drive(raceValues.CurrentStage);
        }
    }
}