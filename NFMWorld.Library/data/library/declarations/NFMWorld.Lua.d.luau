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

declare extern type Vector2 with
    x: number
    y: number
    __sub: ((left: Vector2, right: Vector2) -> Vector2)
    __add: ((left: Vector2, right: Vector2) -> Vector2)
    __div: ((left: Vector2, right: Vector2) -> Vector2)
    __eq: ((left: Vector2, right: Vector2) -> boolean)
end

declare Vector2: {
}

declare extern type Vector3 with
    x: number
    y: number
    z: number
    __sub: ((left: Vector3, right: Vector3) -> Vector3)
    __add: ((left: Vector3, right: Vector3) -> Vector3)
    __div: ((left: Vector3, right: Vector3) -> Vector3)
    __eq: ((left: Vector3, right: Vector3) -> boolean)
end

declare Vector3: {
}

