using Lua;
using Lua.Runtime;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Minimal tests validating ILuaUserData pattern, StructUserData&lt;T&gt;,
/// and FixedMath native type integration work correctly.
/// </summary>
[TestClass]
public class LuaRuntimeTests
{
    // ---------------------------------------------------------------
    // FixedMath native type tests
    // ---------------------------------------------------------------

    [TestMethod]
    public async Task Fixed64_BasicCreation()
    {
        using var state = LuaState.Create();
        var lib = Lua.Standard.FixedMathLibrary.Instance;
        state.Environment["fixed64"] = new LuaValue(new LuaFunction("fixed64", lib.Fixed64Constructor));

        var results = await state.DoStringAsync("return fixed64(3.5)");
        Assert.AreEqual(LuaValueType.Fixed64, results[0].Type);
    }

    [TestMethod]
    public async Task Fixed64Vector3_BasicCreation()
    {
        using var state = LuaState.Create();
        var lib = Lua.Standard.FixedMathLibrary.Instance;
        state.Environment["fixed64vector3"] = new LuaValue(new LuaFunction("fixed64vector3", lib.Fixed64Vector3Constructor));
        foreach (var fn in lib.VectorFunctions)
            state.Environment[fn.Name] = new LuaValue(fn.Func);

        var results = await state.DoStringAsync("return fixed64vector3(1, 2, 3)");
        Assert.AreEqual(LuaValueType.Fixed64Vector3, results[0].Type);
    }

    [TestMethod]
    public async Task Fixed64Vector3_Magnitude()
    {
        using var state = LuaState.Create();
        var lib = Lua.Standard.FixedMathLibrary.Instance;
        state.Environment["fixed64vector3"] = new LuaValue(new LuaFunction("fixed64vector3", lib.Fixed64Vector3Constructor));

        // Register vector functions under fixed64vec3 table
        var vecTable = new LuaTable(0, lib.VectorFunctions.Length);
        foreach (var fn in lib.VectorFunctions)
            vecTable[fn.Name] = new LuaValue(fn.Func);
        state.Environment["fixed64vec3"] = new LuaValue(vecTable);

        var results = await state.DoStringAsync(@"
            local v = fixed64vector3(3, 4, 0)
            return fixed64vec3.magnitude(v)
        ");
        Assert.AreEqual(5, (int)results[0].Read<double>());
    }

    [TestMethod]
    public async Task Fixed64Angle_BasicCreation()
    {
        using var state = LuaState.Create();
        var lib = Lua.Standard.FixedMathLibrary.Instance;

        // Register angle functions under f64anglelib table
        var angleTable = new LuaTable(0, lib.AngleFunctions.Length);
        foreach (var fn in lib.AngleFunctions)
            angleTable[fn.Name] = new LuaValue(fn.Func);
        state.Environment["f64anglelib"] = new LuaValue(angleTable);

        var results = await state.DoStringAsync("return f64anglelib.from_degrees(90)");
        Assert.AreEqual(LuaValueType.Fixed64Angle, results[0].Type);
    }

    [TestMethod]
    public async Task Fixed64Euler_BasicCreation()
    {
        using var state = LuaState.Create();
        var lib = Lua.Standard.FixedMathLibrary.Instance;

        // Register angle + euler tables and constructors
        var angleTable = new LuaTable(0, lib.AngleFunctions.Length);
        foreach (var fn in lib.AngleFunctions)
            angleTable[fn.Name] = new LuaValue(fn.Func);
        state.Environment["f64anglelib"] = new LuaValue(angleTable);

        var eulerTable = new LuaTable(0, lib.EulerFunctions.Length);
        foreach (var fn in lib.EulerFunctions)
            eulerTable[fn.Name] = new LuaValue(fn.Func);
        state.Environment["f64eulerlib"] = new LuaValue(eulerTable);

        // Also need the constructors
        state.Environment["f64euler"] = new LuaValue(new LuaFunction("f64euler", lib.EulerConstructor));

        var results = await state.DoStringAsync(@"
            local e = f64euler(f64anglelib.from_degrees(45), f64anglelib.from_degrees(30), f64anglelib.from_degrees(15))
            return f64eulerlib.wrap(e)
        ");
        Assert.AreEqual(LuaValueType.Fixed64Euler, results[0].Type);
    }
}

/// <summary>
/// Simple test type for StructUserData tests.
/// </summary>
public class TestData
{
    public string Name { get; set; } = "";
    public int Count { get; set; }
}
