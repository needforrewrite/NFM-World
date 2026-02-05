using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

[LuaVisible]
public static class StaticClass
{
    // Static field
    public static int StaticField = 42;

    // Static property with getter and setter
    public static string StaticProperty { get; set; } = "Initial";

    // Read-only static property
    public static double ReadOnlyProperty => 3.14159;

    // Static method with no parameters
    public static int GetMagicNumber()
    {
        return 123;
    }

    // Static method with parameters
    public static int Add(int a, int b)
    {
        return a + b;
    }

    // Static method returning string
    public static string Greet(string name)
    {
        return $"Hello, {name}!";
    }

    // Static method with multiple parameters and return value
    public static double Calculate(double x, double y, string operation)
    {
        return operation switch
        {
            "add" => x + y,
            "sub" => x - y,
            "mul" => x * y,
            "div" => y != 0 ? x / y : double.NaN,
            _ => 0
        };
    }

    // Static event
    public static event Action<string>? OnMessage;

    // Static method to raise event
    public static void RaiseMessage(string message)
    {
        OnMessage?.Invoke(message);
    }
}
