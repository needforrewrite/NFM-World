declare class DeterministicRandom
{
    _random: DeterministicRandom;
    next(): number;
    nextf64(): Fixed64;
    static create(value: Fixed64): LuaDeterministicRandom;
}
