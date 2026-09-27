declare extern type ICalculator with
end

declare ICalculator: {
}

declare extern type CalculatorBase with
end

declare CalculatorBase: {
}

declare extern type DerivedCalculator extends CalculatorBase with
end

declare DerivedCalculator: {
}

declare extern type AnotherCalculator with
end

declare AnotherCalculator: {
}

declare extern type GenericMethods with
end

declare GenericMethods: {
}

declare extern type ExternalTypeUser with
end

declare ExternalTypeUser: {
}

declare extern type IDog with
    breed: string
    name: string
    age: integer
end

declare IDog: {
}

declare extern type IFixtureCar with
    model: string
    isElectric: boolean
    speed: integer
    driverName: string
    x: number
    y: number
    z: number
end

declare IFixtureCar: {
}

declare extern type IPerson with
    email: string
    getName: ((self: IPerson) -> string)
    setName: ((self: IPerson, name: string) -> nil)
    getAge: ((self: IPerson) -> integer)
    setAge: ((self: IPerson, age: integer) -> nil)
end

declare IPerson: {
}

declare extern type RecordStructType with
    x: integer
    y: integer
    sum: ((self: RecordStructType) -> integer)
    __eq: ((left: RecordStructType, right: RecordStructType) -> boolean)
end

declare RecordStructType: {
    new: (() -> RecordStructType) & ((x: integer, y: integer) -> RecordStructType),
}

declare extern type SampleClass with
    id: integer
    name: string
    isActive: boolean
    value: number
    preciseValue: number
    nullableInt: integer|nil
    nullableFloat: number|nil
    nullableBool: boolean|nil
    publicField: integer
    publicStringField: string
    nullableLongField: integer|nil
    getDoubleId: ((self: SampleClass) -> integer)
    getGreeting: ((self: SampleClass, prefix: string) -> string)
    setValue: ((self: SampleClass, newValue: number) -> nil)
    calculate: ((self: SampleClass, a: number, b: number, multiply: boolean) -> number)
    clone: ((self: SampleClass) -> SampleClass)
    setNullableValue: ((self: SampleClass, newValue: number|nil) -> nil)
    multiplyByNullable: ((self: SampleClass, multiplier: integer|nil) -> integer|nil)
    formatWithOptional: ((self: SampleClass, prefix: string, suffix: string) -> string)
    customName: ((self: SampleClass) -> string)
end

declare SampleClass: {
    staticCounter: integer,
    staticName: string,
    staticNullableDouble: number|nil,
    new: (() -> SampleClass) & ((id: integer, name: string) -> SampleClass) & ((id: integer, name: string, isActive: boolean, value: number) -> SampleClass) & ((nullableId: integer|nil, nullableName: string) -> SampleClass),
    add: ((a: integer, b: integer) -> integer),
    concat: ((a: string, b: string) -> string),
    incrementCounter: (() -> nil),
    addNullable: ((a: integer|nil, b: integer|nil) -> integer),
    getNullableValue: ((hasValue: boolean, value: integer) -> integer|nil),
}

declare extern type Vec2 with
    __add: ((a: Vec2, b: Vec2) -> Vec2)
    __sub: ((a: Vec2, b: Vec2) -> Vec2)
    __div: ((v: Vec2, scalar: number) -> Vec2)
    __unm: ((v: Vec2) -> Vec2)
    __eq: ((a: Vec2, b: Vec2) -> boolean)
end

declare Vec2: {
}

declare extern type StaticClass with
end

declare StaticClass: {
    staticProperty: string,
    readOnlyProperty: number,
    staticField: integer,
    getMagicNumber: (() -> integer),
    add: ((a: integer, b: integer) -> integer),
    greet: ((name: string) -> string),
    calculate: ((x: number, y: number, operation: string) -> number),
    raiseMessage: ((message: string) -> nil),
}

declare extern type TypeInLuaNamespace with
    name: string
    value: integer
    getDescription: ((self: TypeInLuaNamespace) -> string)
end

declare TypeInLuaNamespace: {
    new: (() -> TypeInLuaNamespace) & ((name: string, value: integer) -> TypeInLuaNamespace),
}

declare extern type TypeWithArrays with
end

declare TypeWithArrays: {
}

declare extern type TypeWithByRefParameters with
end

declare TypeWithByRefParameters: {
}

declare extern type TypeWithConstants with
end

declare TypeWithConstants: {
    factor: integer,
    defaultName: string,
    pi: number,
    multiplier: integer,
    applyFactor: ((value: integer) -> integer),
}

declare extern type TestColor with
end

declare TestColor: {
    red: TestColor,
    green: TestColor,
    blue: TestColor,
    yellow: TestColor,
}

declare extern type TypeWithEnum with
    color: TestColor
    readOnlyColor: TestColor
    nullableColor: TestColor|nil
    defaultColor: TestColor
    getColor: ((self: TypeWithEnum) -> TestColor)
    setColor: ((self: TypeWithEnum, color: TestColor) -> nil)
    isPrimary: ((self: TypeWithEnum, color: TestColor) -> boolean)
    getNullableColor: ((self: TypeWithEnum, returnValue: boolean) -> TestColor|nil)
end

declare TypeWithEnum: {
}

declare extern type TypeWithEvents with
end

declare TypeWithEvents: {
}

declare extern type CustomEventArgs with
end

declare CustomEventArgs: {
}

declare extern type TypeWithExceptions with
end

declare TypeWithExceptions: {
}

declare extern type TypeWithExtensionMembers with
end

declare TypeWithExtensionMembers: {
}

declare extern type TypeWithFixedMathNullables with
    nullableFixed: fixed64|nil
    nullableVec3: fixed64vector3|nil
    normalFixed: fixed64
    normalVec3: fixed64vector3
    getOptionalValue: ((self: TypeWithFixedMathNullables, returnValue: boolean) -> fixed64|nil)
end

declare TypeWithFixedMathNullables: {
    new: (() -> TypeWithFixedMathNullables),
}

declare extern type InlineBuffer with
end

declare InlineBuffer: {
}

declare extern type TypeWithInlineArray with
end

declare TypeWithInlineArray: {
}

declare extern type TypeWithIntIndexer with
end

declare TypeWithIntIndexer: {
}

declare extern type TypeWithMemberShimOverrides with
    myProperty: CustomProperty
    myField: CustomField
    methodWithParamShim: ((self: TypeWithMemberShimOverrides, value: CustomParam) -> integer)
    methodWithReturnShim: ((self: TypeWithMemberShimOverrides) -> CustomReturn)
end

declare TypeWithMemberShimOverrides: {
}

declare extern type TypeWithMethodDeduplication with
end

declare TypeWithMethodDeduplication: {
}

declare extern type AnotherCalculator2 with
end

declare AnotherCalculator2: {
}

declare extern type TypeWithNewMember with
end

declare TypeWithNewMember: {
}

declare extern type TypeWithMultiDimArray with
end

declare TypeWithMultiDimArray: {
}

declare extern type TypeWithMultiParamIndexer with
end

declare TypeWithMultiParamIndexer: {
}

declare extern type TypeWithNestedGeneric with
end

declare TypeWithNestedGeneric: {
}

declare extern type TypeWithOverloads with
    value: integer
    text: string
    processNumber: ((self: TypeWithOverloads, x: integer) -> string) & ((self: TypeWithOverloads, x: number) -> string) & ((self: TypeWithOverloads, x: integer) -> string) & ((self: TypeWithOverloads, x: number) -> string)
    processData: ((self: TypeWithOverloads, s: string) -> string) & ((self: TypeWithOverloads, arr: {integer}) -> string) & ((self: TypeWithOverloads, arr: {number}) -> string) & ((self: TypeWithOverloads, flag: boolean) -> string)
    combine: ((self: TypeWithOverloads, a: integer, b: integer) -> string) & ((self: TypeWithOverloads, a: number, b: number) -> string) & ((self: TypeWithOverloads, a: string, b: string) -> string) & ((self: TypeWithOverloads, a: integer, b: string) -> string) & ((self: TypeWithOverloads, a: string, b: integer) -> string)
    __unm: ((a: TypeWithOverloads) -> TypeWithOverloads)
    __add: ((a: TypeWithOverloads, b: TypeWithOverloads) -> TypeWithOverloads) & ((a: TypeWithOverloads, b: integer) -> TypeWithOverloads) & ((a: integer, b: TypeWithOverloads) -> TypeWithOverloads)
    __sub: ((a: TypeWithOverloads, b: TypeWithOverloads) -> TypeWithOverloads) & ((a: TypeWithOverloads, b: integer) -> TypeWithOverloads)
end

declare TypeWithOverloads: {
    new: ((value: integer) -> TypeWithOverloads) & ((value: number) -> TypeWithOverloads) & ((text: string) -> TypeWithOverloads),
    staticProcess: ((x: integer) -> string) & ((x: number) -> string) & ((s: string) -> string),
}

declare extern type TypeWithSpanParameters with
    name: string
    getName: ((self: TypeWithSpanParameters) -> string)
end

declare TypeWithSpanParameters: {
    new: (() -> TypeWithSpanParameters),
}

declare extern type TypeWithStaticAbstractInterface with
end

declare TypeWithStaticAbstractInterface: {
}

declare extern type TypeWithStringIndexer with
end

declare TypeWithStringIndexer: {
}

declare extern type TypeWithTupleOverloads with
    processTuple: ((self: TypeWithTupleOverloads, coords: System.ValueTuple_int_int) -> string) & ((self: TypeWithTupleOverloads, point: System.ValueTuple_int_int_int) -> string)
    processMixed: ((self: TypeWithTupleOverloads, id: integer, data: System.ValueTuple_string_bool) -> string)
    combine: ((self: TypeWithTupleOverloads, a: System.ValueTuple_int_int, b: System.ValueTuple_int_int) -> string) & ((self: TypeWithTupleOverloads, a: System.ValueTuple_int_int_int, b: System.ValueTuple_int_int_int) -> string) & ((self: TypeWithTupleOverloads, a: System.ValueTuple_int_int, scalar: integer) -> string)
end

declare TypeWithTupleOverloads: {
    new: (() -> TypeWithTupleOverloads),
}

declare extern type Vec3 with
    __add: ((a: Vec3, b: Vec3) -> Vec3)
    __sub: ((a: Vec3, b: Vec3) -> Vec3)
    __unm: ((v: Vec3) -> Vec3)
end

declare Vec3: {
}

declare extern type TypeWithReferences with
end

declare TypeWithReferences: {
}

