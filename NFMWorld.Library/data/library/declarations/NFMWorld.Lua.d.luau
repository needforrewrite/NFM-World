declare extern type DeterministicRandom with
    next: ((self: DeterministicRandom) -> integer)
    nextBetween: ((self: DeterministicRandom, min: integer, max: integer) -> integer)
    nextf64: ((self: DeterministicRandom) -> fixed64)
end

declare DeterministicRandom: {
    new: ((value: fixed64) -> DeterministicRandom),
}

declare extern type LuaRect with
    x: number
    y: number
    width: number
    height: number
    __eq: ((left: LuaRect, right: LuaRect) -> boolean)
end

declare LuaRect: {
}

declare extern type LuaVector2 with
    x: number
    y: number
    __sub: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __add: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __div: ((left: LuaVector2, right: LuaVector2) -> LuaVector2)
    __eq: ((left: LuaVector2, right: LuaVector2) -> boolean)
end

declare LuaVector2: {
}

declare extern type LuaVector3 with
    x: number
    y: number
    z: number
    __sub: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __add: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __div: ((left: LuaVector3, right: LuaVector3) -> LuaVector3)
    __eq: ((left: LuaVector3, right: LuaVector3) -> boolean)
end

declare LuaVector3: {
}

