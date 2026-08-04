declare class CarStats
    implements CarStats
{
    readonly swits: Int3;
    readonly acelf: Vector3d;
    readonly handb: number;
    readonly airs: Fixed64;
    readonly airc: number;
    readonly _deprecated_Turn: number;
    readonly grip: Fixed64;
    readonly bounce: Fixed64;
    readonly simag: Fixed64;
    readonly moment: Fixed64;
    readonly comprad: Fixed64;
    readonly push: Fixed64;
    readonly revpush: Fixed64;
    readonly lift: number;
    readonly revlift: number;
    readonly powerloss: number;
    readonly flipy: number;
    readonly msquash: number;
    readonly clrad: number;
    readonly dammult: Fixed64;
    readonly maxmag: number;
    readonly dishandle: Fixed64;
    readonly outdam: Fixed64;
    readonly name: string;
    readonly enginsignature: number;
    turnradius: number;
    roadgrip: Fixed64 | null;
    offroadgrip: Fixed64 | null;
    offtrackgrip: Fixed64 | null;
    readonly turn: Fixed64;
    validate(fileName: string): string;
    validateFailName(fileName: string): string;
    validateFail(property: string): string;
    static default: CarStats;
    static validateStats(stats: CarStats, fileName: string): CarStats;
}
