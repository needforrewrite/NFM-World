declare class Int3
    implements System_IEquatable_NFMWorldLibrary_Int3_
{
    x: number;
    y: number;
    z: number;
    getHashCode(): number;
    equals(other: Int3): boolean;
    equals_obj(value: any): boolean;
    static readonly zero: Int3;
    static readonly unitX: Int3;
    static readonly unitY: Int3;
    static readonly unitZ: Int3;
    static readonly one: Int3;
    static throwArgumentOutOfRangeException(): number;
}
