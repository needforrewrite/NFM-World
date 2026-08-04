declare class LuaGamemode
    extends NFMWorldLibrary_Backend_Gamemodes_BaseGamemode
    implements NFMWorldLibrary_Backend_Gamemodes_IGamemode
{
    readonly isClient: boolean;
    readonly _path: string;
    getResults(): NFMWorldLibrary_Gamemodes_RaceResults | null;
    finishRace(playerStandings: byteArray): void;
    createBackendCar(name: string, idx: number, x: fixed64, y: fixed64): NFMWorldLibrary_Backend_BackendCar;
    reset(): void;
    add_onEnter(callback: () => void): void;
    remove_onEnter(callback: () => void): void;
    add_onExit(callback: () => void): void;
    remove_onExit(callback: () => void): void;
    add_onGameTick(callback: () => void): void;
    remove_onGameTick(callback: () => void): void;
    add_onReset(callback: () => void): void;
    remove_onReset(callback: () => void): void;
}
