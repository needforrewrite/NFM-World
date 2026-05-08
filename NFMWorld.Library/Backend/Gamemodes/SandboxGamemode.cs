using Maxine.Extensions;

namespace NFMWorldLibrary.Backend.Gamemodes;

public class SandboxGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues)
    : BaseGamemode(gamemodeParameters, raceValues)
{
    public override event EventHandler<byte[]>? RaceFinished;

    private int _newTick = 0;

    public override void Enter()
    {
        foreach (var (idx, player) in players.WithIndex())
        {
            carsInRace[idx] = new BackendCar(BackendGameSparker.GetCar(player.CarName).Rad!, idx, 0, 0, idx == playerCarIndex);
        }
        carsInRace[NumPlayers] = new BackendCar(BackendGameSparker.GetCar("nfmm/audir8").Rad!, 1, 100, 0, false);

        Reset();
    }

    public override void Exit()
    {
        // Cleanup for Time Trial mode
    }

    public override void Reset()
    {
        base.Reset();
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {carsInRace[0].Position.X:0.00}, contoz: {carsInRace[0].Position.Z:0.00}, contoy: {carsInRace[0].Position.Y:0.00}");

        if (raceValues.raceState == RaceState.InProgress)
        {
            // Inter-car collision is run at the original tickrate (21.4TPS) to emulate original physics behavior
            // We round this up to 3 ticks per 63TPS tick.
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
}