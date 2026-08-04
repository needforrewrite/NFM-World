declare class DeterministicRandom
{
    _random: DeterministicRandom;
    next(): number;
    nextf64(): fixed64;
    static create(value: fixed64): LuaDeterministicRandom;
}
