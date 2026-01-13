using nfm_world_library.backend.gamemodes;

namespace nfm_world.gameplay.gamemodes;

public class LuaClientGamemode(string path, BaseGamemodeParameters gamemodeParameters, BaseRacePhase raceValues)
    : LuaGamemode(path, gamemodeParameters, raceValues), IClientGamemode
{
    
}