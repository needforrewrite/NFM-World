using NFMWorld.LuaSourceGenerator.TestFixtures;

namespace NFMWorld.LuaSourceGenerator.Test;

/// <summary>
/// Extension methods defined in a different assembly (Test project)
/// </summary>
public static class CrossAssemblyExtensions
{
    /// <summary>
    /// Extension method that triples the value
    /// </summary>
    public static int Triple(this TypeWithExtensionMembers obj)
    {
        return obj.Value * 3;
    }

    /// <summary>
    /// Extension method with parameter
    /// </summary>
    public static int Subtract(this TypeWithExtensionMembers obj, int amount)
    {
        return obj.Value - amount;
    }

    /// <summary>
    /// Extension method that returns boolean
    /// </summary>
    public static bool IsPositive(this TypeWithExtensionMembers obj)
    {
        return obj.Value > 0;
    }

    /// <summary>
    /// Extension method that modifies the object
    /// </summary>
    public static void IncrementValue(this TypeWithExtensionMembers obj)
    {
        obj.Value++;
    }

    /// <summary>
    /// Generic extension method - should NOT be bound
    /// </summary>
    public static T Convert<T>(this TypeWithExtensionMembers obj, Func<int, T> converter)
    {
        return converter(obj.Value);
    }

    /// <summary>
    /// Another generic extension method - should NOT be bound
    /// </summary>
    public static TypeWithExtensionMembers Transform<T>(this TypeWithExtensionMembers obj, T input) where T : struct
    {
        return new TypeWithExtensionMembers(obj.Value + input.GetHashCode());
    }
}
