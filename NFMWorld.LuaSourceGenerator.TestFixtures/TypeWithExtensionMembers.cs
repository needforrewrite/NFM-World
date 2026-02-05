using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Test type for extension methods and properties
/// </summary>
[LuaVisible]
public class TypeWithExtensionMembers
{
    public int Value { get; set; }

    public TypeWithExtensionMembers(int value)
    {
        Value = value;
    }
}

/// <summary>
/// Extension methods defined in the same assembly as the type
/// </summary>
public static class TypeWithExtensionMembersExtensions
{
    /// <summary>
    /// Extension method that doubles the value
    /// </summary>
    public static int Double(this TypeWithExtensionMembers obj)
    {
        return obj.Value * 2;
    }

    /// <summary>
    /// Extension method with parameter
    /// </summary>
    public static int Add(this TypeWithExtensionMembers obj, int amount)
    {
        return obj.Value + amount;
    }

    /// <summary>
    /// Extension method with multiple parameters
    /// </summary>
    public static int Multiply(this TypeWithExtensionMembers obj, int multiplier, int addend)
    {
        return obj.Value * multiplier + addend;
    }

    /// <summary>
    /// Extension method that returns string
    /// </summary>
    public static string FormatValue(this TypeWithExtensionMembers obj, string prefix)
    {
        return $"{prefix}: {obj.Value}";
    }

    /// <summary>
    /// Void extension method
    /// </summary>
    public static void PrintValue(this TypeWithExtensionMembers obj)
    {
        Console.WriteLine($"Value: {obj.Value}");
    }

    /// <summary>
    /// Generic extension method - should be filtered out
    /// </summary>
    public static T Convert<T>(this TypeWithExtensionMembers obj, Func<int, T> converter)
    {
        return converter(obj.Value);
    }

    /// <summary>
    /// Generic extension method with constraint - should be filtered out
    /// </summary>
    public static T Transform<T>(this TypeWithExtensionMembers obj, T input) where T : struct
    {
        return input;
    }

    // Note: C# 14 extension properties would be defined like this:
    // public static string FormattedValue(this TypeWithExtensionMembers obj) => $"[{obj.Value}]";
    // However, these are compiled into regular static methods with ExtensionAttribute,
    // which our generator already handles correctly via IsExtensionMethod().
}
