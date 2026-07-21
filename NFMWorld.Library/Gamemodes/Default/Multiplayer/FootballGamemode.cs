using Maxine.Extensions;
using NFMWorld.DriverInterface;

namespace NFMWorldLibrary.Backend.Gamemodes;

/// <summary>
/// Client-side football (soccer) gamemode. No server-side logic —
/// purely physics-driven with a ball entity.
/// </summary>
public class FootballGamemode(GamemodeParameters gamemodeParameters, IGamemodeData gamemodeData)
    : BaseGamemode(gamemodeParameters, gamemodeData)
{
    private int _newTick = 0;

    public override void Begin()
    {
        foreach (var (idx, player) in Players.WithIndex())
        {
            CarsInRace[idx] = new BackendCar(player, idx, 500, 0);
        }
        CarsInRace[Players.Count] = new BackendCar(BackendGameSparker.GetCar("football/BALL").Rad!, 1, 0, 0, false);

        Reset();
    }

    public override void End()
    {
    }

    public override void Reset()
    {
        base.Reset();
    }

    public override void GameTick()
    {
        FrameTrace.AddMessage($"contox: {CarsInRace[0].Position.X:0.00}, contoz: {CarsInRace[0].Position.Z:0.00}, contoy: {CarsInRace[0].Position.Y:0.00}");

        if (++_newTick == Physics.OriginalTicksPerNewTick)
        {
            for (int i = 0; i < CarsInRace.Count; i++)
            for (int j = 0; j < CarsInRace.Count; j++)
            {
                if (i != j)
                {
                    CarsInRace[i].Collide(CarsInRace[j]);
                }
            }

            _newTick = 0;
        }

        foreach (var car in CarsInRace)
        {
            car.Drive(gamemodeData.CurrentStage);
        }
    }

    public override void KeyPressed(Key key, in Keys keys)
    {
        base.KeyPressed(key, keys);
        if (key == Key.R)
        {
            Reset();
        }
    }

    public override void KeyReleased(Key key, in Keys keys)
    {
        base.KeyReleased(key, keys);
    }

    public override void Render()
    {
        base.Render();
    }
}