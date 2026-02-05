using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

// Interface for testing interface implementation deduplication
public interface ICalculator
{
    int Add(int a, int b);
    int Multiply(int x, int y);
    string GetDescription();
}

// Base class for testing virtual override deduplication
public class CalculatorBase
{
    public virtual int Subtract(int a, int b)
    {
        return a - b;
    }

    public virtual int Divide(int a, int b)
    {
        if (b == 0) throw new DivideByZeroException();
        return a / b;
    }

    public virtual string GetName()
    {
        return "BaseCalculator";
    }
}

[LuaVisible]
public class TypeWithMethodDeduplication : CalculatorBase, ICalculator
{
    // Interface implementations (should use ICalculator's implementation)
    public int Add(int a, int b)
    {
        return a + b;
    }

    public int Multiply(int x, int y)
    {
        return x * y;
    }

    public string GetDescription()
    {
        return "Calculator implementation";
    }

    // Virtual overrides (should use CalculatorBase's implementation)
    public override int Subtract(int a, int b)
    {
        return base.Subtract(a, b);
    }

    public override int Divide(int a, int b)
    {
        return base.Divide(a, b);
    }

    public override string GetName()
    {
        return "DerivedCalculator";
    }

    // New method that shadows base (should generate its own implementation)
    public new string ToString()
    {
        return "TypeWithMethodDeduplication instance";
    }

    // Unique method (should generate its own implementation)
    public int Square(int x)
    {
        return x * x;
    }
}

// Another type implementing the same interface (to verify interface implementation reuse)
[LuaVisible]
public class AnotherCalculator : ICalculator
{
    public int Add(int a, int b)
    {
        return a + b + 1; // Different implementation
    }

    public int Multiply(int x, int y)
    {
        return x * y * 2; // Different implementation
    }

    public string GetDescription()
    {
        return "Another calculator";
    }
}

// Type with 'new' member (should generate its own implementation, not reuse base)
[LuaVisible]
public class TypeWithNewMember : CalculatorBase
{
    // This uses 'new' to shadow the base method, not override it
    // Should generate its own implementation
    public new int Subtract(int a, int b)
    {
        return (a - b) * 2; // Different behavior
    }

    // This is a normal override (should use base implementation)
    public override int Divide(int a, int b)
    {
        return base.Divide(a, b);
    }
}
