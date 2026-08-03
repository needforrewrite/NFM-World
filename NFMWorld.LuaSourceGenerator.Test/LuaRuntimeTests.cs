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
    // StructUserData<T> — pure C# tests
    // ---------------------------------------------------------------

    [TestMethod]
    public void StructUserData_ImplicitConversion()
    {
        var wrapper = new StructUserData<int>(null) { Value = 42 };
        LuaValue luaVal = wrapper;

        Assert.AreEqual(LuaValueType.UserData, luaVal.Type);
        Assert.IsTrue(luaVal.TryRead<ILuaUserData>(out var userData));
        Assert.IsInstanceOfType<StructUserData<int>>(userData);
    }

    [TestMethod]
    public void StructUserData_MetatableAccess()
    {
        var wrapper = new StructUserData<string>(new LuaTable()) { Value = "hello" };
        Assert.IsNotNull(((ILuaUserData)wrapper).Metatable);
    }

    [TestMethod]
    public void StructUserData_ImmutableMetatable()
    {
        var wrapper = new StructUserData<int> { Value = 42 };
        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            ((ILuaUserData)wrapper).Metatable = new LuaTable();
        });
    }

    // ---------------------------------------------------------------
    // StructUserData<T> — Lua integration tests
    // ---------------------------------------------------------------

    [TestMethod]
    public async Task StructUserData_InLua_ReadProperty()
    {
        using var state = LuaState.Create();
        var mt = new LuaTable();
        mt[Metamethods.Index] = new LuaFunction("__index", (context, ct) =>
        {
            var w = context.GetArgument<StructUserData<TestData>>(0);
            var k = context.GetArgument(1);
            if (k.TryRead<string>(out var sk))
            {
                if (sk == "Name") return new(context.Return(w.Value.Name));
                if (sk == "Count") return new(context.Return((double)w.Value.Count));
            }
            return new(context.Return(LuaValue.Nil));
        });
        var wrapper = new StructUserData<TestData>(mt) { Value = new TestData { Name = "hello", Count = 42 } };
        state.Environment["obj"] = (LuaValue)wrapper;

        var results = await state.DoStringAsync("return obj.Name, obj.Count");
        Assert.AreEqual("hello", results[0].Read<string>());
        Assert.AreEqual(42, (int)results[1].Read<double>());
    }

    [TestMethod]
    public async Task StructUserData_InLua_WriteProperty()
    {
        using var state = LuaState.Create();
        var data = new TestData { Name = "old", Count = 0 };
        // Build a proper metatable with __newindex for TestData
        var mt = new LuaTable();
        mt[Metamethods.Index] = new LuaFunction("__index", (context, ct) =>
        {
            var w = context.GetArgument<StructUserData<TestData>>(0);
            var k = context.GetArgument(1);
            if (k.TryRead<string>(out var sk))
            {
                if (sk == "Name") return new(context.Return(w.Value.Name));
                if (sk == "Count") return new(context.Return((double)w.Value.Count));
            }
            return new(context.Return(LuaValue.Nil));
        });
        mt[Metamethods.NewIndex] = new LuaFunction("__newindex", (context, ct) =>
        {
            var w = context.GetArgument<StructUserData<TestData>>(0);
            var k = context.GetArgument(1);
            var v = context.GetArgument(2);
            if (k.TryRead<string>(out var sk))
            {
                if (sk == "Name") w.Value.Name = v.Read<string>();
                else if (sk == "Count") w.Value.Count = (int)v.Read<double>();
            }
            return new(context.Return());
        });
        var wrapper = new StructUserData<TestData>(mt) { Value = data };
        state.Environment["obj"] = (LuaValue)wrapper;

        await state.DoStringAsync("obj.Name = 'new'; obj.Count = 99");
        Assert.AreEqual("new", wrapper.Value.Name);
        Assert.AreEqual(99, wrapper.Value.Count);
    }

    [TestMethod]
    public async Task StructUserData_ArrayAccess()
    {
        using var state = LuaState.Create();
        var arr = new int[] { 10, 20, 30 };
        var wrapper = new StructUserData<int[]> { Value = arr };
        state.Environment["arr"] = (LuaValue)wrapper;

        var results = await state.DoStringAsync("return arr[1], arr[2], arr[3], #arr");
        Assert.AreEqual(10, (int)results[0].Read<double>());
        Assert.AreEqual(20, (int)results[1].Read<double>());
        Assert.AreEqual(30, (int)results[2].Read<double>());
        Assert.AreEqual(3, (int)results[3].Read<double>());
    }

    [TestMethod]
    public async Task StructUserData_StringArray()
    {
        using var state = LuaState.Create();
        var arr = new string[] { "a", "b", "c" };
        var wrapper = new StructUserData<string[]> { Value = arr };
        state.Environment["arr"] = (LuaValue)wrapper;

        var results = await state.DoStringAsync("return arr[1], arr[2], arr[3]");
        Assert.AreEqual("a", results[0].Read<string>());
        Assert.AreEqual("b", results[1].Read<string>());
        Assert.AreEqual("c", results[2].Read<string>());
    }

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
