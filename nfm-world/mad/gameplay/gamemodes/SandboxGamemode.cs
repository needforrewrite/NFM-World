using System.Diagnostics;
using Maxine.Extensions;
using NFMWorld;
using NFMWorld.Mad;
using NFMWorld.Mad.gamemodes;
using NFMWorld.Util;
using Stride.Core.Mathematics;

public class SandboxGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase baseRacePhase)
    : BaseGamemode(gamemodeParameters, baseRacePhase)
{
    private int _newTick = 0;
    
    public override void Enter()
    {
        foreach (var (idx, player) in players.WithIndex())
        {
            carsInRace[idx] = new InGameCar(idx, GameSparker.GetCar(player.CarName).Car!, 0, 0, idx == playerCarIndex);
        }
        carsInRace[NumPlayers] = new InGameCar(1, GameSparker.GetCar("nfmm/audir8").Car!, 100, 0, false);
        carsInRace[NumPlayers].Sfx.Mute = true;

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
        FrameTrace.AddMessage($"contox: {carsInRace[0].CarRef.Position.X:0.00}, contoz: {carsInRace[0].CarRef.Position.Z:0.00}, contoy: {carsInRace[0].CarRef.Position.Y:0.00}");

        // Inter-car collision is run at the original tickrate (21.4TPS) to emulate original physics behavior
        // We round this up to 3 ticks per 63TPS tick.
        if (++_newTick == GameSparker.OriginalTicksPerNewTick)
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
            car.Drive();
        }
    }

    public override void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            Reset();
        }
    }

    public override void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    public override void Render()
    {
    }
}