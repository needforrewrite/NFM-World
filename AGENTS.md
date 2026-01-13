# AGENTS.md

This document contains critical knowledge about the NFMWorld project for future AI agents working on this codebase.

## Project Context

This is an FNA 3D world rendering engine called NFM-World. The project uses custom HLSL shaders for rendering polygons with advanced features like instancing, shadow mapping, fog, and lighting.
NFM-World uses a niche rendering system with outlines and flat n-gon shading and meshes take n-gons as input which are then triangulated to produce polygons. The project also includes a polygon triangulation system for handling self-intersecting polygons, holes, and curved surfaces.

## Polygon Triangulation System

The project includes a complex polygon triangulation system (`PolygonTriangulator` class) for handling:
- Self-intersecting polygons
- Polygons with holes
- Nearly-planar curved surfaces
- Best-fit plane projection with fallback to axis-aligned projections

**Key Features**:
- Vertex deduplication with epsilon tolerance (1e-5f)
- Region extraction for self-intersecting paths
- Hole merging via bridge vertices
- Ear-clipping triangulation with relaxed fallback
- Projection validation to prevent vertex collapse

**Known Issues**:
- Curved surfaces may collapse when projected to best-fit plane
- Solution: Try axis-aligned projections (YZ, XZ, XY) when best-fit fails
- Self-intersecting polygons with 3 holes should produce 4 regions (8 triangles)

### Coordinate System

- Uses right-handed coordinate system
- Y-axis is up
- Light direction specified in world space
- Camera position tracked for lighting calculations

### Rendering Features

**Lighting**:
- Directional diffuse lighting
- Environment light (ambient + directional components in `float2`)
- Per-vertex or fullbright options
- Face orientation detection (front vs back facing)

**Fog**:
- Distance-based exponential fog
- Configurable density and fade distance
- Applied in view space

**Material Properties**:
- Per-vertex colors or uniform base color
- Alpha blending support
- Color snapping effect
- Brightness control (darken parameter)

### Common Pitfalls

1. **Triangulation Failures**: When triangulating:
   - Check for duplicate consecutive vertices (including first/last wrap-around)
   - Validate projection doesn't collapse 3D points to same 2D location
   - Ensure signed area calculation for winding order detection

### Shader Conversion Notes

When converting from Three.js to FNA:
- `vec2/vec3/vec4` → `float2/float3/float4`
- `mat4` → `float4x4` or `matrix`
- `mix()` → `lerp()`
- `mod()` → `fmod()`
- Three.js built-in uniforms map to:
  - `modelMatrix` → custom `world` parameter
  - `modelViewMatrix` → `mul(world, View)`
  - `projectionMatrix` → `Projection`
  - `normalMatrix` → `mul(float4(normal, 0), world).xyz` for world-space normals
  - `cameraPosition` → `CameraPosition` uniform

### Best Practices

1. Always maintain consistent matrix multiplication order across all shader techniques
2. Use the same world transform for both main rendering and shadow map generation
3. Apply vertex modifications (decal offset, expansion) before any matrix transformations
4. Validate projection methods when triangulating to prevent vertex collapse
5. Use epsilon comparisons for floating-point vertex deduplication
6. When debugging shader issues, start by commenting out all optional features and adding them back one at a time

### Debug Strategies

For shader issues:
1. Output solid colors first to verify geometry is visible
2. Check WorldPos values aren't NaN/Inf
3. Verify View and Projection matrices are valid
4. Test with Expand=false, IsFullbright=true to isolate issues
5. Compare instance world matrix values with non-instanced rendering

For triangulation issues:
1. Log the 2D projected vertices and signed area
2. Verify unique vertex count matches expectation
3. Check for wrap-around duplicate vertices
4. Validate projection preserves vertex distinctness
5. Count expected triangles: simple polygon needs (n-2) triangles, but polygon-with-holes may need fewer

## Lua Source Generator

---

## Project Overview

This is a **C# source generator** that creates LuaJIT FFI bindings for C# types marked with the `[LuaVisible]` attribute. It uses reflection at compile-time to generate static Lua bindings that enable calling C# code from Lua scripts at runtime.

### Key Technologies
- **C# 12 / .NET 10.0**
- **LuaJIT** (Lua 5.1 compatible)
- **Reflection-based code generation**
- **MSTest** for testing

---

## Project Structure

```
nfm-world/
├── LUA.Bindings.TXt             # Lua.NET bindings available to use
└── LUA.txt                      # Lua 5.1 Reference Manual (PDF converted to TXT)

NFMWorld.LuaSourceGenerator/
├── Program.cs                    # Main generator - 2450+ lines
├── TypeInfo.cs                   # Type metadata record
└── [Generated on build]

NFMWorld.LuaSourceGenerator.Test/
├── LuaRuntimeTests.cs           # Integration tests with real LuaJIT (2287 lines)
├── SampleTypeTests.cs           # Unit tests for test fixtures
├── TypeInfoTests.cs             # Tests for TypeInfo record
└── Generated/                    # Generated binding files
    ├── LuaBindings.Base.g.cs    # Core infrastructure (generated)
    ├── LuaBindings.Initialize.g.cs
    └── [Type].g.cs               # One per [LuaVisible] type

NFMWorld.LuaSourceGenerator.TestFixtures/
├── SampleClass.cs               # Test fixture types
├── SampleStruct.cs
├── TypeWithArrays.cs
├── TypeWithByRefParameters.cs
├── RefStructType.cs
├── TypeWithNestedGeneric.cs
└── [Other test types]

NFMWorld.Lua/
└── LuaVisibleAttribute.cs       # Attribute to mark types for binding
```

---

## Critical Files

### 1. Program.cs (NFMWorld.LuaSourceGenerator)

**Purpose:** Main generator that scans assemblies and produces Lua binding code.

**Key Methods:**
- `Generate()`: Entry point, scans assembly for `[LuaVisible]` types
- `GenerateLuaBindingsBaseFile()`: Generates core infrastructure with **RAW STRING LITERALS**
- `GenerateTypeBindings()`: Generates bindings for a specific type
- `GenerateMethodBinding()`: Generates method call wrappers
- `GenerateParameterRead()`: Uses `ToObject<T>` to convert Lua→C#
- `GenerateToObjectCode()`: Uses `ToObject<T>` for property/field setters

**Critical Pattern - Raw String Literals:**
```csharp
var luaBindingsBaseFile = $$"""
    // Generated code here
    private static T? ToObject<T>(lua_State L, int idx) { ... }
    """;
```

⚠️ **IMPORTANT:** Lines within GenerateLuaBindingsBaseFile contain a **massive raw string literal** (`$$"""..."""`) that generates `LuaBindings.Base.g.cs`. When editing code that goes into the generated file, you're usually editing **within this string literal**, not regular C# code.

### 2. LuaBindings.Base.g.cs (Generated)

**Purpose:** Runtime infrastructure for Lua bindings.

**Key Components:**
- `TypeInfo<T>`: Storage for managed objects (prevents GC)
- `ToObject<T>()`: **THE CORE CONVERSION FUNCTION** (Lua→C#)
- `PushValue<T>()`: C#→Lua conversion
- `RegisterMetatable()`: Sets up Lua metatables for types
- `KeepAlive()`: Prevents GC collection of delegates

**ToObject<T> Implementation:**
```csharp
private static T? ToObject<T>(lua_State L, int idx)
{
    var luaType = lua_type(L, idx);
    if (luaType == LUA_TNIL) return default;

    // Primitives
    if (typeof(T) == typeof(int)) return (T)(object)(int)lua_tointeger(L, idx);
    // ... more primitives

    if (typeof(T) == typeof(string)) return (T)(object)lua_tostring(L, idx)!;

    // Lua table → C# array conversion
    if (luaType == LUA_TTABLE && typeof(T).IsArray) { /* ... */ }

    // Userdata (objects/structs from C#)
    if (luaType == LUA_TUSERDATA) { /* ... */ }

    throw new InvalidOperationException(...);
}
```

### 3. LuaRuntimeTests.cs

**Purpose:** Integration tests that spawn real LuaJIT instances and verify bindings work.

**Test Structure:**
```csharp
[TestInitialize]
public void Setup()
{
    LuaBindings.Reset();
    LuaBindings.ResetType<SampleClass>();  // Clear state
    _L = luaL_newstate();
    luaL_openlibs(_L);
    LuaBindings.Initialize(_L);  // Register all types
}

[TestMethod]
public void SomeTest()
{
    var result = luaL_dostring(_L, @"
        local obj = SampleClass.new(42, 'test')
        return obj.id
    ");
    AssertLuaOk(result);
    var id = lua_tointeger(_L, -1);
    Assert.AreEqual(42, id);
}
```

---

## The Build/Regeneration Workflow

**CRITICAL:** This project has a **two-stage process**:

### Stage 1: Build TestFixtures
```bash
cd NFMWorld.LuaSourceGenerator.TestFixtures
dotnet build
```
This creates the DLL containing test types with `[LuaVisible]` attributes.

### Stage 2: Run Generator
```bash
cd NFMWorld.LuaSourceGenerator
dotnet run -- \
  "..\NFMWorld.LuaSourceGenerator.TestFixtures\bin\Debug\net10.0\NFMWorld.LuaSourceGenerator.TestFixtures.dll" \
  "..\NFMWorld.LuaSourceGenerator.Test\Generated" \
  "NFMWorld.LuaSourceGenerator.Test.Bindings"
```
This scans the TestFixtures DLL and generates bindings in the `Generated/` folder.

### Stage 3: Build and Test
```bash
cd NFMWorld.LuaSourceGenerator.Test
dotnet build
dotnet test
```

**⚠️ Common Mistake:** Forgetting to regenerate bindings after modifying the generator code. Always run all three stages!

---

## Key Patterns and Conventions

### 1. Filtering Types and Members

**Location:** `Generate()` method (line ~100)

The generator filters out types/members that can't be marshalled to Lua:

```csharp
// Skip ref structs (can't be marshalled)
if (IsRefStruct(type)) continue;

// Skip methods with byref parameters
if (HasByRefParameters(method) || HasRefReturn(method)) continue;
```

**Helper Methods:**
- `IsRefStruct(Type)` - Checks `type.IsByRefLike`
- `HasByRefParameters(MethodBase)` - Checks for `ref`/`out`/`in` parameters
- `HasRefReturn(MethodInfo)` - Checks for `ref` return types

### 2. Nested Generic Type Handling

**Problem:** `List<int>.Enumerator` is a nested type of a constructed generic type.

**Solution:** Recursively check `!ContainsGenericParameters` when discovering types:

```csharp
if (!type.ContainsGenericParameters)
{
    ProcessType(type);

    // Handle nested types
    foreach (var nested in type.GetNestedTypes())
    {
        if (!nested.ContainsGenericParameters)
            ProcessType(nested);
    }
}
```

### 3. Type Name Generation

**Challenge:** Generate Lua-safe names for generic types.

**Pattern:**
- `List<int>` → `List_Int32`
- `List<int>.Enumerator` → `List_Int32_Enumerator`
- `int[]` → `Int32Array`
- `int[,]` → `Int32Array2D`

**Implementation:** See `GetGenericTypeLuaName()` and `GetSimpleTypeName()` (lines ~400-490)

### 4. Lua Stack Management

**Critical Rules:**
1. **Lua arrays are 1-indexed** (unlike C#'s 0-indexing)
2. **Always balance pushes and pops** - if you push, you must pop
3. **Negative indices count from top** (-1 = top of stack)
4. **Use `lua_gettop()` to check stack depth** before accessing

**Example:**
```csharp
for (int i = 0; i < length; i++)
{
    lua_rawgeti(L, idx, i + 1);  // Lua: 1-indexed!
    var element = ToObject<T>(L, -1);  // Top of stack
    lua_pop(L, 1);  // MUST pop!
}
```

### 5. Constructor Overload Resolution

**Behavior:** The generator scores constructor candidates and selects the best match based on parameter type compatibility.

**Critical Distinction:** `int` vs `int?` (Nullable<int>) are **different types** at runtime:
- `SampleClass.new(50, nil)` calls `(int, string)` constructor (exact match for first parameter)
- `SampleClass.new(nil, "text")` calls `(int?, string?)` constructor (requires nullable for first parameter)

**Implication:** If only the non-nullable constructor exists, passing `nil` for reference type parameters will pass `null` to C# without any null-coalescing. Test assumptions about which constructor is called carefully.

---

## LuaJIT FFI Function Reference

**Available in `LuaNET.LuaJIT.Lua` class:**

| Function | Purpose |
|----------|---------|
| `lua_type(L, idx)` | Get type of value at index |
| `lua_tointeger(L, idx)` | Convert to integer |
| `lua_tonumber(L, idx)` | Convert to number |
| `lua_tostring(L, idx)` | Convert to string |
| `lua_toboolean(L, idx)` | Convert to boolean |
| `lua_touserdata(L, idx)` | Get userdata pointer |
| `lua_objlen(L, idx)` | Get length of table/string (use instead of `lua_rawlen`) |
| `lua_rawgeti(L, idx, n)` | Get table[n] |
| `lua_gettop(L)` | Get number of elements on stack |
| `lua_pop(L, n)` | Pop n elements from stack |
| `luaL_dostring(L, code)` | Execute Lua code string |
| `luaL_newstate()` | Create new Lua state |
| `lua_close(L)` | Close Lua state |

**⚠️ Note:** Use `lua_objlen`, NOT `lua_rawlen` (which doesn't exist in this binding).

---

## Recent Work Completed (January 2026)

### 1. Byref Parameter Filtering
**Issue:** Generator produced invalid code for methods with `ref`/`out`/`in` parameters.

**Solution:** Added filtering helpers and applied at 12 locations:
- `IsRefStruct(Type)`
- `HasByRefParameters(MethodBase)`
- `HasRefReturn(MethodInfo)`

Applied to:
- Method generation
- Constructor generation
- Property getters/setters
- Indexer generation

**Test Coverage:** Added `TypeWithByRefParameters` fixture and verified exclusion.

### 2. Ref Struct Filtering
**Issue:** Generator tried to bind `ref struct` types which can't be marshalled.

**Solution:** Check `type.IsByRefLike` in two locations:
- `Generate()` - Skip during discovery
- `ProcessType()` - Extra safety check

**Test Coverage:** Added `RefStructType` fixture and verified exclusion.

### 3. Nested Generic Type Support
**Issue:** `List<int>.Enumerator` wasn't discovered because discovery only checked top-level types.

**Solution:**
- Check `!ContainsGenericParameters` instead of `!IsGenericTypeDefinition`
- Recursively process nested types of discovered types
- Fixed name generation for nested types

**Test Coverage:** Added `TypeWithNestedGeneric` with 3 runtime tests verifying iteration.

### 4. Lua Table → C# Array Conversion
**Feature:** Allow Lua tables to be passed where C# expects arrays.

**Implementation:** Modified `ToObject<T>()`.

**Test Coverage:** Added 6 runtime tests in `TypeWithArrays` section:
- Constructor with table
- Method parameter with table
- Property setter with table
- String arrays
- Empty tables
- Multiple array parameters

**Result:** Now you can write: `obj:setNumbers({1, 2, 3})` in Lua!

### 5. Overload Resolution System
**Issue:** Multiple overloads with same argument count generated duplicate/broken code.

**Solution:** Implemented intelligent overload resolution:
- Refactored to non-generic object storage (`_objects`, `_objectTypes`) for runtime type queries
- Added `GetUserdataType()` and `GetLuaStackValueType()` for type detection
- Created `ScoreParameterCompatibility()` with range-aware numeric type matching:
  - Integer values: Prefers `int`/`long` with range checking (2147483648 → `long`, not `int`)
  - Floating-point values: Prefers `double`/`float`
  - Exact type matches score 100, compatible conversions score lower
- Applied overload resolution to operators, constructors, static methods, instance methods
- Fixed switch case scope issues by wrapping cases in braces

**Test Coverage:** Added `TypeWithOverloads` fixture with 22 tests:
- 3 constructor overloads (int/float/string)
- 16 method overloads (ProcessNumber, ProcessData, Combine, StaticProcess)
- 6 operator overloads (unary minus, 3 addition variants, 2 subtraction variants)

**Result:** Generator now correctly handles methods like `ProcessNumber(int)`, `ProcessNumber(double)`, `ProcessNumber(long)`, `ProcessNumber(float)` and selects the best match based on Lua argument types.

### 6. Array Constructor Implementation (January 12, 2026)
**Issue:** Array types exposed to Lua (e.g., `ArrayOfInt32`, `ArrayOfString`) had non-functional constructors that threw "not implemented" errors.

**Solution:** Implemented full array constructor support with multidimensional capabilities:
- Modified `GenerateConstructorMethod()` to detect array types via `type.IsArray`
- Used `type.GetArrayRank()` to determine dimensionality (1D, 2D, 3D+)
- Generated rank-specific constructor code:
  - 1D arrays: `new elementType[dim0]`
  - 2D arrays: `new elementType[dim0, dim1]`
  - 3D+ arrays: `new elementType[dim0, dim1, dim2, ...]`
- Added validation for non-negative dimensions and correct argument count
- Updated `GenerateLuaTypeStub()` to generate correct stub signatures:
  - 1D: `function ArrayOfInt32.new(length) end`
  - 2D: `function ArrayOfInt322D.new(dim0, dim1) end`
  - 3D: `function ArrayOfSingle3D.new(dim0, dim1, dim2) end`

**Test Coverage:** Added 17 comprehensive tests covering:
- 1D array creation, read/write, zero-length, negative validation, string arrays (6 tests)
- 2D array creation, read/write, wrong arg count, zero dimensions, negative validation (5 tests)
- 3D array creation, read/write, wrong arg count (3 tests)
- Integration tests passing constructed arrays to type methods/constructors (3 tests)

**Naming Convention Clarification:** Arrays follow the pattern:
- `int[]` → `ArrayOfInt32` (NOT `Int32Array`)
- `int[,]` → `ArrayOfInt32_2D` (in code) / `ArrayOfInt322D` (Lua global name)
- `float[,,]` → `ArrayOfSingle_3D` (in code) / `ArrayOfSingle3D` (Lua global name)

**Result:** Lua scripts can now create arrays with: `local arr = ArrayOfInt32.new(10)` or `local arr2d = ArrayOfInt322D.new(5, 3)`

**Total Array Tests:** 45 (all passing)

### 7. Exception Handling Implementation (January 12, 2026)
**Issue:** .NET exceptions thrown from C# methods/constructors/properties were not propagated as Lua errors, causing crashes or silent failures when Lua code called into C# code that threw exceptions.

**Solution:** Implemented comprehensive exception handling throughout the source generator:
- Wrapped all constructor invocations in try-catch blocks (both single and overloaded constructors)
- Wrapped all static method invocations in try-catch blocks (both void and non-void return types)
- Wrapped all instance method invocations in try-catch blocks (both void and non-void return types)
- Modified `GenerateToObjectCode()` to wrap property and field setter assignments in try-catch blocks
- All caught exceptions are propagated to Lua using `luaL_error(L, $"{ex.GetType().Name}: {ex.Message}")`

**Implementation Details:**
- Exception handling wraps the actual C# invocation/assignment, not the entire method
- Error message format: `"ExceptionTypeName: ExceptionMessage"` (e.g., `"ArgumentException: Value cannot be negative"`)
- Lua's `luaL_error` is used to properly propagate errors up the Lua call stack
- After calling `luaL_error`, the function returns 0 (though `luaL_error` actually longjmps)

**Test Coverage:** Added 6 comprehensive tests covering:
- Constructor exceptions (ArgumentException from validation)
- Instance method exceptions (InvalidOperationException)
- Instance method with parameters exceptions (DivideByZeroException)
- Property setter exceptions (ArgumentException)
- Static method exceptions (ArgumentException)
- Successful execution verification (ensuring normal calls still work)

**Result:** All C# exceptions are now properly caught and converted to Lua errors, providing:
- Safe error handling without crashing the application
- Clear error messages visible to Lua scripts
- Consistent behavior across all binding types (constructors, methods, properties)

**Total Tests:** 248 (all passing)

### 8. .NET Event Support Implementation (January 13, 2026)
**Issue:** No support for subscribing to .NET events from Lua scripts, limiting interactivity between C# and Lua code.

**Solution:** Implemented complete event subscription system with statically-generated delegate types:
- Added event discovery in `DiscoverReferencedTypes` to find all event handler types and their parameter types
- Generated `AddListener_EventName(callback)` and `RemoveListener_EventName()` methods for each event
- Created `EventInvoker0`, `EventInvoker1<T0>`, and `EventInvoker2<T0, T1>` helper classes for delegate invocation
- Stored Lua function references in Lua registry using `luaL_ref` to prevent garbage collection
- Used `Delegate.CreateDelegate` to create type-safe event handlers that call back into Lua
- Added special case for `System.Object` and `System.EventArgs` types in type discovery (removed from `IsPrimitiveOrKnownType`)
- Generated Lua stub annotations for event listeners with proper callback signatures

**Implementation Details:**
- Events are bound at the delegate type level, not using reflection at runtime
- Each unique delegate signature gets a dedicated `EventInvokerN` class
- Lua callbacks are invoked via `lua_rawgeti` from registry + `lua_pcall`
- `RemoveListener` is currently a no-op (documented in comments)
- EventInvoker classes have finalizers that clean up Lua registry references with `luaL_unref`
- System types (`object`, `EventArgs`) are now discovered and have full bindings generated

**Test Coverage:** Added 9 comprehensive tests covering:
- Simple events with no parameters (Action)
- Standard events with sender and EventArgs (EventHandler)
- Custom events with custom EventArgs types (EventHandler<CustomEventArgs>)
- Static events (Action<string>)
- Multi-parameter events (custom delegates)
- Multiple listeners on the same event
- Event unsubscription (RemoveListener)
- Independent event handlers for different instances
- Error handling for invalid function parameters

**Supported Event Types:**
- `System.Action` (no parameters)
- `System.EventHandler` (object sender, EventArgs e)
- `System.EventHandler<T>` (object sender, T e)
- `System.Action<T>` (single parameter)
- Custom delegate types with up to 2 parameters

**Lua Usage Example:**
```lua
local obj = TypeWithEvents.new()
obj:AddListener_SimpleEvent(function()
    print("Event fired!")
end)
obj:raiseSimpleEvent()  -- Prints: "Event fired!"

obj:AddListener_StandardEvent(function(sender, eventArgs)
    print("Sender type: " .. tostring(sender))
end)
```

**Result:** Lua scripts can now subscribe to and receive callbacks from .NET events, enabling:
- Reactive programming patterns between C# and Lua
- Event-driven architecture support
- Multiple listeners per event
- Type-safe event parameter passing
- Proper lifetime management of Lua callbacks

**Total Tests:** 251 (all passing, +3 from event implementation)

**Generated Files:**
- `Object.g.cs` - Bindings for System.Object
- `EventArgs.g.cs` - Bindings for System.EventArgs
- Updated `LuaBindings.Base.g.cs` with EventInvoker classes and CreateEventDelegate method
- Updated `LuaBindings.lua` with event listener stub annotations

---

## Test Status

**Total Tests:** 248
- **Status:** ✅ All passing

---

## Common Pitfalls and Solutions

### Pitfall 1: Editing Generated Code
**Problem:** Modifying `Generated/LuaBindings.Base.g.cs` directly.

**Solution:** Edit the **raw string literal** in `Program.cs` (lines 494-854), then regenerate.

### Pitfall 2: Forgetting to Regenerate
**Problem:** Modified generator code but tests still use old bindings.

**Solution:** Always follow the 3-stage workflow (build fixtures → run generator → build tests).

### Pitfall 3: Stack Imbalance
**Problem:** Lua stack grows unbounded or crashes with stack underflow.

**Solution:**
- Use `lua_gettop()` to verify stack state
- Always pop what you push
- Use `AssertLuaOk(result)` to catch Lua errors early

### Pitfall 4: Wrong Lua Function Name
**Problem:** Using `lua_rawlen` which doesn't exist in this binding.

**Solution:** Use `lua_objlen` instead (see function reference above).

### Pitfall 5: 0-based vs 1-based Indexing
**Problem:** Accessing Lua table with 0-based index returns `nil`.

**Solution:** Lua arrays start at 1! Use `lua_rawgeti(L, idx, i + 1)`.

---

## Debugging Tips

### 1. Enable Verbose Output
The generator prints discovery information:
```
Type count: 11
Total [LuaVisible] types found: 9
```

If a type isn't being generated, check if it's being filtered.

### 2. Check Generated Code
Always inspect `Generated/LuaBindings.Base.g.cs` after regenerating to verify your changes propagated correctly.

### 3. Lua Error Messages
When `luaL_dostring` fails, the error is on the Lua stack:
```csharp
if (result != 0)
{
    var error = lua_tostring(_L, -1);
    Console.WriteLine($"Lua error: {error}");
}
```

### 4. Test Isolation
Always call `LuaBindings.Reset()` in test setup to ensure clean state.

---

## Architecture Insights

### Object Storage Pattern
Managed objects are stored in `DictionarySlim<int, T>` and referenced by ID from Lua userdata. This:
- Prevents GC collection while Lua references exist
- Allows Lua to hold references to C# objects
- Enables proper cleanup via `__gc` metamethods

### Type Safety
The binding system is fully type-safe:
- Lua calls are validated at runtime
- Type mismatches throw clear exceptions
- No unsafe casts in generated code (except for userdata pointer access)

---

## Future Considerations

### Potential Improvements
1. **Dictionary support** - Convert Lua tables with string keys to C# dictionaries
2. **Nullable value type handling** - Better support for `Nullable<T>`
3. **Performance optimization** - Cache reflected methods instead of looking up on every call
4. **Error messages** - Include file/line info in Lua error messages
5. **Jagged array support** - Support for arrays of arrays (e.g., `int[][]`)

### Known Limitations
1. Cannot bind `ref struct` types (by design - not marshallable)
2. Cannot bind methods with `ref`/`out`/`in` parameters (filtered out)
3. No support for events (not yet implemented)
4. No support for async methods (Lua is synchronous)
5. Generic methods not supported (only generic types)
6. Lua table → array conversion only supports 1D arrays (multidimensional array constructors use dimension parameters)

---

## Quick Reference Commands

```bash
# Full rebuild workflow
cd NFMWorld.LuaSourceGenerator.TestFixtures && dotnet build
cd ../NFMWorld.LuaSourceGenerator && dotnet run -- "../NFMWorld.LuaSourceGenerator.TestFixtures/bin/Debug/net10.0/NFMWorld.LuaSourceGenerator.TestFixtures.dll" "../NFMWorld.LuaSourceGenerator.Test/Generated" "NFMWorld.LuaSourceGenerator.Test.Bindings"
cd ../NFMWorld.LuaSourceGenerator.Test && dotnet build && dotnet test

# Run specific test category
cd NFMWorld.LuaSourceGenerator.Test
dotnet test --filter "FullyQualifiedName~TypeWithArrays"

# List all tests
dotnet test --list-tests
```

---

## Contact and Resources

- **Project:** NFM-World (Need for Madness World)
- **Lua Version:** LuaJIT (Lua 5.1 compatible)
- **C# Version:** C# 12 / .NET 10.0
- **Testing Framework:** MSTest

---

*This guide was created by AI agents for AI agents. Update it as you learn more!*
