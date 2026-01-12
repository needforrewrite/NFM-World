# Agent Guide: NFMWorld Lua Source Generator

**Last Updated:** January 12, 2026

This document contains critical knowledge about the NFMWorld Lua Source Generator project for future AI agents working on this codebase.

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

---

## Test Status

**Total Tests:** 190
- **Original:** 184
- **Nested Generic Tests:** 3
- **Array Conversion Tests:** 6
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
1. **Multi-dimensional array support** - Currently only 1D arrays convert from tables
2. **Dictionary support** - Convert Lua tables with string keys to C# dictionaries
3. **Nullable value type handling** - Better support for `Nullable<T>`
4. **Performance optimization** - Cache reflected methods instead of looking up on every call
5. **Error messages** - Include file/line info in Lua error messages

### Known Limitations
1. Cannot bind `ref struct` types (by design - not marshallable)
2. Cannot bind methods with `ref`/`out`/`in` parameters (filtered out)
3. No support for events (not yet implemented)
4. No support for async methods (Lua is synchronous)
5. Generic methods not supported (only generic types)

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
