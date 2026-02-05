using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using nfm_world_library.Lua;

namespace NFMWorld.LuaSourceGenerator.TestFixtures;

/// <summary>
/// Interface with static abstract members (C# 11 feature).
/// </summary>
public interface IParsableValue<TSelf> where TSelf : IParsableValue<TSelf>
{
    static abstract TSelf Parse(string input);
    static abstract bool TryParse(string input, [NotNullWhen(true)] out TSelf? result);
    static abstract TSelf Zero { get; }
}

/// <summary>
/// Test fixture that implements an interface with static abstract members.
/// This tests that static abstract interface implementations are bound as static methods, not instance methods.
/// </summary>
[LuaVisible]
public struct TypeWithStaticAbstractInterface : IParsableValue<TypeWithStaticAbstractInterface>
{
    public int Value { get; set; }

    public TypeWithStaticAbstractInterface(int value)
    {
        Value = value;
    }

    // Static abstract interface implementation - should be bound as static method
    public static TypeWithStaticAbstractInterface Parse(string input)
    {
        if (string.IsNullOrWhiteSpace(input))
            throw new ArgumentException("Input cannot be null or whitespace.", nameof(input));

        if (!int.TryParse(input, out var value))
            throw new FormatException($"Unable to parse '{input}' as integer.");

        return new TypeWithStaticAbstractInterface(value);
    }

    // Static abstract interface implementation - should be bound as static method
    public static bool TryParse(string input, [NotNullWhen(true)] out TypeWithStaticAbstractInterface result)
    {
        result = default;

        if (string.IsNullOrWhiteSpace(input))
            return false;

        if (!int.TryParse(input, out var value))
            return false;

        result = new TypeWithStaticAbstractInterface(value);
        return true;
    }

    // Static abstract property implementation - should be accessible as static property
    public static TypeWithStaticAbstractInterface Zero => new TypeWithStaticAbstractInterface(0);

    // Regular static method for comparison
    public static TypeWithStaticAbstractInterface FromDouble(double value)
    {
        return new TypeWithStaticAbstractInterface((int)value);
    }

    // Instance method for comparison
    public int Add(int other)
    {
        return Value + other;
    }

    public override string ToString() => Value.ToString();
}
