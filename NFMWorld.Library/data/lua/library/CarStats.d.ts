declare class CarStats
    implements CarStats
{
    swits: Int3;
    acelf: Vector3d;
    handb: number;
    airs: Fixed64;
    airc: number;
    _deprecated_Turn: number;
    grip: Fixed64;
    bounce: Fixed64;
    simag: Fixed64;
    moment: Fixed64;
    comprad: Fixed64;
    push: Fixed64;
    revpush: Fixed64;
    lift: number;
    revlift: number;
    powerloss: number;
    flipy: number;
    msquash: number;
    clrad: number;
    dammult: Fixed64;
    maxmag: number;
    dishandle: Fixed64;
    outdam: Fixed64;
    name: string;
    enginsignature: number;
    turnradius: number;
    roadgrip: Fixed64 | null;
    offroadgrip: Fixed64 | null;
    offtrackgrip: Fixed64 | null;
    turn: Fixed64;
    validate(fileName: string): string;
    validateFailName(fileName: string): string;
    validateFail(property: string): string;
    static default: CarStats;
    static validateStats(stats: CarStats, fileName: string): CarStats;
}
