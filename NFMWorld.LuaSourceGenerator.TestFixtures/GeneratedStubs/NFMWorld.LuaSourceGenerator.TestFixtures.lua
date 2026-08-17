---@class ICalculator

ICalculator = {}


---@class CalculatorBase : ICalculator

CalculatorBase = {}


---@class DerivedCalculator : CalculatorBase, ICalculator

DerivedCalculator = {}


---@class AnotherCalculator : ICalculator

AnotherCalculator = {}


---@class GenericMethods

GenericMethods = {}


---@class ExternalTypeUser

ExternalTypeUser = {}


---@class IDog : NFMWorld.LuaSourceGenerator.TestFixtures.IBaseAnimal
---@field breed string
---@field name string
---@field age integer

IDog = {}


---@class IFixtureCar : NFMWorld.LuaSourceGenerator.TestFixtures.IFixtureVehicle, NFMWorld.LuaSourceGenerator.TestFixtures.IFixtureTransform
---@field model string
---@field isElectric boolean
---@field speed integer
---@field driverName string
---@field x number
---@field y number
---@field z number

IFixtureCar = {}


---@class IPerson : NFMWorld.LuaSourceGenerator.TestFixtures.IHasName, NFMWorld.LuaSourceGenerator.TestFixtures.IHasAge
---@field email string
---@field getName fun(self: IPerson): string
---@field setName fun(self: IPerson, name: string)
---@field getAge fun(self: IPerson): integer
---@field setAge fun(self: IPerson, age: integer)

IPerson = {}


---@class RecordStructType : System.IEquatable_RecordStructType
---@field x integer
---@field y integer
---@field sum fun(self: RecordStructType): integer

RecordStructType = {}


---Creates a new RecordStructType
---@return RecordStructType
function RecordStructType.new() end

---Creates a new RecordStructType
---@param x integer
---@param y integer
---@return RecordStructType
function RecordStructType.new(x, y) end

---@class SampleClass
---@field id integer
---@field name string
---@field isActive boolean
---@field value number
---@field preciseValue number
---@field nullableInt integer|nil
---@field nullableFloat number|nil
---@field nullableBool boolean|nil
---@field publicField integer
---@field publicStringField string
---@field nullableLongField integer|nil
---@field getDoubleId fun(self: SampleClass): integer
---@field getGreeting fun(self: SampleClass, prefix: string): string
---@field setValue fun(self: SampleClass, newValue: number)
---@field calculate fun(self: SampleClass, a: number, b: number, multiply: boolean): number
---@field clone fun(self: SampleClass): SampleClass
---@field setNullableValue fun(self: SampleClass, newValue: number|nil)
---@field multiplyByNullable fun(self: SampleClass, multiplier: integer|nil): integer|nil
---@field formatWithOptional fun(self: SampleClass, prefix: string, suffix: string): string
---@field customName fun(self: SampleClass): string

SampleClass = {}

---@type integer
SampleClass.staticCounter = nil
---@type string
SampleClass.staticName = nil
---@type number|nil
SampleClass.staticNullableDouble = nil

---Creates a new SampleClass
---@return SampleClass
function SampleClass.new() end

---Creates a new SampleClass
---@param id integer
---@param name string
---@return SampleClass
function SampleClass.new(id, name) end

---Creates a new SampleClass
---@param id integer
---@param name string
---@param isActive boolean
---@param value number
---@return SampleClass
function SampleClass.new(id, name, isActive, value) end

---Creates a new SampleClass
---@param nullableId integer|nil
---@param nullableName string
---@return SampleClass
function SampleClass.new(nullableId, nullableName) end

---@param a integer
---@param b integer
---@return integer
function SampleClass.add(a, b) end

---@param a string
---@param b string
---@return string
function SampleClass.concat(a, b) end

function SampleClass.incrementCounter() end

---@param a integer|nil
---@param b integer|nil
---@return integer
function SampleClass.addNullable(a, b) end

---@param hasValue boolean
---@param value integer
---@return integer|nil
function SampleClass.getNullableValue(hasValue, value) end

---@class Vec2

Vec2 = {}


---@class StaticClass

StaticClass = {}

---@type string
StaticClass.staticProperty = nil
---@type number
StaticClass.readOnlyProperty = nil
---@type integer
StaticClass.staticField = nil

---@return integer
function StaticClass.getMagicNumber() end

---@param a integer
---@param b integer
---@return integer
function StaticClass.add(a, b) end

---@param name string
---@return string
function StaticClass.greet(name) end

---@param x number
---@param y number
---@param operation string
---@return number
function StaticClass.calculate(x, y, operation) end

---@param message string
function StaticClass.raiseMessage(message) end

---@class TypeInLuaNamespace
---@field name string
---@field value integer
---@field getDescription fun(self: TypeInLuaNamespace): string

TypeInLuaNamespace = {}


---Creates a new TypeInLuaNamespace
---@return TypeInLuaNamespace
function TypeInLuaNamespace.new() end

---Creates a new TypeInLuaNamespace
---@param name string
---@param value integer
---@return TypeInLuaNamespace
function TypeInLuaNamespace.new(name, value) end

---@class TypeWithArrays

TypeWithArrays = {}


---@class TypeWithByRefParameters

TypeWithByRefParameters = {}


---@class TypeWithConstants

TypeWithConstants = {}

---@type integer
TypeWithConstants.factor = nil
---@type string
TypeWithConstants.defaultName = nil
---@type number
TypeWithConstants.pi = nil
---@type integer
TypeWithConstants.multiplier = nil

---@param value integer
---@return integer
function TypeWithConstants.applyFactor(value) end

---@class TestColor : System.Enum, System.IComparable, System.IConvertible, System.ISpanFormattable, System.IFormattable

TestColor = {}


---@class TypeWithEnum
---@field color TestColor
---@field readOnlyColor TestColor
---@field nullableColor TestColor|nil
---@field defaultColor TestColor
---@field getColor fun(self: TypeWithEnum): TestColor
---@field setColor fun(self: TypeWithEnum, color: TestColor)
---@field isPrimary fun(self: TypeWithEnum, color: TestColor): boolean
---@field getNullableColor fun(self: TypeWithEnum, returnValue: boolean): TestColor|nil

TypeWithEnum = {}


---@class TypeWithEvents

TypeWithEvents = {}


---@class CustomEventArgs : System.EventArgs

CustomEventArgs = {}


---@class TypeWithExceptions

TypeWithExceptions = {}


---@class TypeWithExtensionMembers

TypeWithExtensionMembers = {}


---@class TypeWithFixedMathNullables
---@field nullableFixed fixed64|nil
---@field nullableVec3 fixed64vector3|nil
---@field normalFixed fixed64
---@field normalVec3 fixed64vector3
---@field getOptionalValue fun(self: TypeWithFixedMathNullables, returnValue: boolean): fixed64|nil

TypeWithFixedMathNullables = {}


---Creates a new TypeWithFixedMathNullables
---@return TypeWithFixedMathNullables
function TypeWithFixedMathNullables.new() end

---@class InlineBuffer

InlineBuffer = {}


---@class TypeWithInlineArray

TypeWithInlineArray = {}


---@class TypeWithIntIndexer

TypeWithIntIndexer = {}


---@class TypeWithMethodDeduplication : NFMWorld.LuaSourceGenerator.Test.SampleTypes.CalculatorBase, NFMWorld.LuaSourceGenerator.Test.SampleTypes.ICalculator

TypeWithMethodDeduplication = {}


---@class AnotherCalculator2 : NFMWorld.LuaSourceGenerator.Test.SampleTypes.ICalculator

AnotherCalculator2 = {}


---@class TypeWithNewMember : NFMWorld.LuaSourceGenerator.Test.SampleTypes.CalculatorBase

TypeWithNewMember = {}


---@class TypeWithMultiDimArray

TypeWithMultiDimArray = {}


---@class TypeWithMultiParamIndexer

TypeWithMultiParamIndexer = {}


---@class TypeWithNestedGeneric

TypeWithNestedGeneric = {}


---@class TypeWithOverloads
---@field value integer
---@field text string
---@field processNumber fun(self: TypeWithOverloads, x: integer): string
---@field processNumber fun(self: TypeWithOverloads, x: number): string
---@field processNumber fun(self: TypeWithOverloads, x: integer): string
---@field processNumber fun(self: TypeWithOverloads, x: number): string
---@field processData fun(self: TypeWithOverloads, s: string): string
---@field processData fun(self: TypeWithOverloads, arr: { [integer]: integer}): string
---@field processData fun(self: TypeWithOverloads, arr: { [integer]: number}): string
---@field processData fun(self: TypeWithOverloads, flag: boolean): string
---@field combine fun(self: TypeWithOverloads, a: integer, b: integer): string
---@field combine fun(self: TypeWithOverloads, a: number, b: number): string
---@field combine fun(self: TypeWithOverloads, a: string, b: string): string
---@field combine fun(self: TypeWithOverloads, a: integer, b: string): string
---@field combine fun(self: TypeWithOverloads, a: string, b: integer): string

TypeWithOverloads = {}


---Creates a new TypeWithOverloads
---@param value integer
---@return TypeWithOverloads
function TypeWithOverloads.new(value) end

---Creates a new TypeWithOverloads
---@param value number
---@return TypeWithOverloads
function TypeWithOverloads.new(value) end

---Creates a new TypeWithOverloads
---@param text string
---@return TypeWithOverloads
function TypeWithOverloads.new(text) end

---@param x integer
---@return string
function TypeWithOverloads.staticProcess(x) end

---@param x number
---@return string
function TypeWithOverloads.staticProcess(x) end

---@param s string
---@return string
function TypeWithOverloads.staticProcess(s) end

---@class TypeWithReferences

TypeWithReferences = {}


---@class TypeWithSpanParameters
---@field name string
---@field getName fun(self: TypeWithSpanParameters): string

TypeWithSpanParameters = {}


---Creates a new TypeWithSpanParameters
---@return TypeWithSpanParameters
function TypeWithSpanParameters.new() end

---@class TypeWithStaticAbstractInterface : NFMWorld.LuaSourceGenerator.TestFixtures.IParsableValue_TypeWithStaticAbstractInterface

TypeWithStaticAbstractInterface = {}


---@class TypeWithStringIndexer

TypeWithStringIndexer = {}


---@class TypeWithTupleOverloads
---@field processTuple fun(self: TypeWithTupleOverloads, coords: System.ValueTuple_int_int): string
---@field processTuple fun(self: TypeWithTupleOverloads, point: System.ValueTuple_int_int_int): string
---@field processMixed fun(self: TypeWithTupleOverloads, id: integer, data: System.ValueTuple_string_bool): string
---@field combine fun(self: TypeWithTupleOverloads, a: System.ValueTuple_int_int, b: System.ValueTuple_int_int): string
---@field combine fun(self: TypeWithTupleOverloads, a: System.ValueTuple_int_int_int, b: System.ValueTuple_int_int_int): string
---@field combine fun(self: TypeWithTupleOverloads, a: System.ValueTuple_int_int, scalar: integer): string

TypeWithTupleOverloads = {}


---Creates a new TypeWithTupleOverloads
---@return TypeWithTupleOverloads
function TypeWithTupleOverloads.new() end

---@class Vec3

Vec3 = {}


---@class TypeWithMemberShimOverrides
---@field myProperty CustomProperty
---@field myField CustomField
---@field methodWithParamShim fun(self: TypeWithMemberShimOverrides, value: CustomParam): integer
---@field methodWithReturnShim fun(self: TypeWithMemberShimOverrides): CustomReturn

TypeWithMemberShimOverrides = {}


---@class System.Collections.Generic.List_int_Enumerator : System.Collections.Generic.IEnumerator_int, System.Collections.IEnumerator, System.IDisposable

System.Collections.Generic.List_int_Enumerator = {}


---@class System.Collections.Generic.List_string_Enumerator : System.Collections.Generic.IEnumerator_string, System.Collections.IEnumerator, System.IDisposable

System.Collections.Generic.List_string_Enumerator = {}


