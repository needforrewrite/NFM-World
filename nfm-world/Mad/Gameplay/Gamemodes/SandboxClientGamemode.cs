using NFMWorld.Util;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorld.Gameplay.Gamemodes;

public class SandboxClientGamemode(BaseGamemodeParameters gamemodeParameters, BaseRacePhase raceValues)
    : SandboxGamemode(gamemodeParameters, raceValues), IClientGamemode
{
    public override void Enter()
    {
        base.Enter();
        raceValues.GetClientCar(NumPlayers)!.Sfx!.Mute = true;
    }

    public void KeyPressed(Keys key)
    {
        // Handle key presses specific to Time Trial mode
        if (key == Keys.R)
        {
            Reset();
        }
    }

    public void KeyReleased(Keys key)
    {
        // Handle key releases specific to Time Trial mode
    }

    public void Render()
    {
    }
}