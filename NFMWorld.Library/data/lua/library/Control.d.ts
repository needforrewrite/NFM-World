declare class Control
{
    arrace: boolean;
    chatup: number;
    down: boolean;
    enter: boolean;
    exit: boolean;
    handb: boolean;
    multion: number;
    mutem: boolean;
    mutes: boolean;
    radar: boolean;
    right: boolean;
    up: boolean;
    left: boolean;
    lookback: number;
    wall: number;
    zyinv: boolean;
    falseo(i: number): void;
    reset(): void;
    encode(): Nibble_byte;
    decode(enc: Nibble_byte): void;
    decode_tuple5(enc: ValueTuple): void;
}
