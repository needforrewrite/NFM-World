using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.Test.SampleTypes;

[LuaVisible(Name = "TypeWithExceptions")]
public class TypeWithExceptions
{
    public int Value { get; set; }

    public TypeWithExceptions(int value)
    {
        if (value < 0)
            throw new ArgumentException("Value cannot be negative");
        Value = value;
    }

    public void ThrowsException()
    {
        throw new InvalidOperationException("This method always throws");
    }

    public int ThrowsExceptionWithParam(int x)
    {
        if (x == 0)
            throw new DivideByZeroException("Cannot divide by zero");
        return 100 / x;
    }

    public int WritableProperty
    {
        get => Value;
        set
        {
            if (value < 0)
                throw new ArgumentException("Property cannot be negative");
            Value = value;
        }
    }

    public static int StaticThrows(int x)
    {
        if (x < 0)
            throw new ArgumentException("Static method: x cannot be negative");
        return x * 2;
    }
}
