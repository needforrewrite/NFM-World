declare interface IGamemodeData
{
    readonly carsInRace: NFMWorldLibrary_Util_UnlimitedArray_NFMWorldLibrary_IInGameCar_;
    readonly currentStage: NFMWorldLibrary_Backend_BackendStage;
    readonly raceState: NFMWorldLibrary_RaceState;
    readonly clientCallbacks: NFMWorldLibrary_Backend_IClientCallbacks;
}
