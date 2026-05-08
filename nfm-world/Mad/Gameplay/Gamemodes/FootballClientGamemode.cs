using NFMWorld.Util;
using NFMWorldLibrary.Backend;
using NFMWorldLibrary.Backend.Gamemodes;

namespace NFMWorld.Gameplay.Gamemodes;

public class FootballClientGamemode(BaseGamemodeParameters gamemodeParameters, IRaceValues raceValues)
    : FootballGamemode(gamemodeParameters, raceValues), IClientGamemode
{
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