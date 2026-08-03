using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

// ---------------------------------------------------------------
// Interface + base class for deduplication testing
// ---------------------------------------------------------------

[LuaVisible]
public partial interface ICalculator
{
    int Add(int a, int b);
    int Multiply(int a, int b);
    string GetDescription();
}

[LuaVisible]
public partial class CalculatorBase : ICalculator
{
    public virtual int Add(int a, int b) => a + b;
    public virtual int Multiply(int a, int b) => a * b;
    public virtual string GetDescription() => "Base calculator";

    // Non-interface method — each derived type gets its own
    public virtual int Subtract(int a, int b) => a - b;
    public virtual int Divide(int a, int b) => a / b;
}

[LuaVisible]
public partial class DerivedCalculator : CalculatorBase
{
    public override string GetDescription() => "Derived calculator";
    public virtual int Square(int x) => x * x;
}

[LuaVisible]
public partial class AnotherCalculator : ICalculator
{
    public int Add(int a, int b) => a + b + 1;
    public int Multiply(int a, int b) => a * b * 2;
    public string GetDescription() => "Another calculator";
    public virtual int Power(int a, int b) => (int)Math.Pow(a, b);
}

// ---------------------------------------------------------------
// Generic type for StructUserData testing
// ---------------------------------------------------------------

[LuaVisible]
public partial class GenericWrapper<T>
{
    public T Value { get; set; } = default!;
    public T GetValue() => Value;
    public void SetValue(T val) { Value = val; }
}

[LuaVisible]
public partial class GenericMethods
{
    // Methods that use constructed generic types — should use StructUserData
    public System.Collections.Generic.List<int> CreateIntList() => new() { 1, 2, 3 };
    public System.Collections.Generic.Dictionary<string, int> CreateDict() => new() { ["a"] = 1, ["b"] = 2 };
    public int SumList(System.Collections.Generic.List<int> list) => list.Sum();
}

// ---------------------------------------------------------------
// External type — sealed BCL type that needs StructUserData
// ---------------------------------------------------------------

[LuaVisible]
public partial class ExternalTypeUser
{
    public System.Version GetVersion() => new(1, 2, 3);
    public System.Uri? CreateUri(string url) => new(url);

    public System.Version GetSameVersion(System.Version version) => version;
}
