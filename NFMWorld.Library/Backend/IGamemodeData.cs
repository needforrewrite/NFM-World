using nfm_world_library.Lua;
using NFMWorld.DriverInterface;
using NFMWorldLibrary.Backend.Gamemodes;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

/// <summary>
/// Data for the gamemode.
/// </summary>
[LuaVisible]
public partial interface IGamemodeData
{
    UnlimitedArray<IInGameCar> CarsInRace { get; }
    BackendStage CurrentStage { get; }
    RaceState RaceState { get; }

    [ClientOnly]
    IClientCallbacks ClientCallbacks { get; }
}