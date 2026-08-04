declare class CarStats
    implements System_IEquatable_NFMWorldLibrary_CarStats_
{
    readonly swits: Int3;
    readonly acelf: fixed64vector3;
    readonly handb: number;
    readonly airs: fixed64;
    readonly airc: number;
    readonly _deprecated_Turn: number;
    readonly grip: fixed64;
    readonly bounce: fixed64;
    readonly simag: fixed64;
    readonly moment: fixed64;
    readonly comprad: fixed64;
    readonly push: fixed64;
    readonly revpush: fixed64;
    readonly lift: number;
    readonly revlift: number;
    readonly powerloss: number;
    readonly flipy: number;
    readonly msquash: number;
    readonly clrad: number;
    readonly dammult: fixed64;
    readonly maxmag: number;
    readonly dishandle: fixed64;
    readonly outdam: fixed64;
    readonly name: string;
    readonly enginsignature: number;
    turnradius: number;
    roadgrip: fixed64 | null;
    offroadgrip: fixed64 | null;
    offtrackgrip: fixed64 | null;
    readonly turn: fixed64;
    validate(fileName: string): string;
    validateFailName(fileName: string): string;
    validateFail(property: string): string;
    static default: CarStats;
    static validateStats(stats: CarStats, fileName: string): CarStats;
}
