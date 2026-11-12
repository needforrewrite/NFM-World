using System.Diagnostics.CodeAnalysis;

namespace TightPack;

public class TightPackSerializationException : Exception
{
    public TightPackSerializationException(string message) : base(message)
    {
    }

    public TightPackSerializationException(string message, Exception innerException) : base(message, innerException)
    {
    }

    [DoesNotReturn]
    public static void ThrowMessage(string message) => throw new TightPackSerializationException(message);

    [DoesNotReturn]
    public static void ThrowNotRegistered(Type type) => throw NotRegistered(type);

    public static TightPackSerializationException NotRegistered(Type type) =>
        new($"{type.FullName} has no registered formatter. Mark it [TightPackable], register one with " +
            "TightPackFormatterProvider.Register, or apply [TightPackCustomFormatter].");

    [DoesNotReturn]
    public static void ThrowOutOfRange(int needed, int remaining) =>
        throw new TightPackSerializationException($"Required {needed} byte(s) but only {remaining} remain in the buffer.");

    [DoesNotReturn]
    public static void ThrowInvalidAdvance(int count, int remaining) =>
        throw new TightPackSerializationException($"Cannot advance {count} byte(s), only {remaining} remain.");

    [DoesNotReturn]
    public static void ThrowOverrun(int declaredEnd, int consumed, int available) =>
        throw new TightPackSerializationException(
            $"A version tolerant member consumed {consumed} byte(s) past its declared {declaredEnd} limit (buffer {available})."
        );

    [DoesNotReturn]
    public static void ThrowInvalidMemberCount(Type type, int expected, int actual) =>
        throw new TightPackSerializationException(
            $"{type.FullName} expects {expected} member(s) but the payload declares {actual}. " +
            "Add members at the end, or use GenerateType.VersionTolerant for schema evolution."
        );

    [DoesNotReturn]
    public static void ThrowDepthLimit(Type type) =>
        throw new TightPackSerializationException(
            $"Serializing {type.FullName} reached the depth limit of {TightPackCode.DepthLimit}; the graph is cyclic."
        );

    [DoesNotReturn]
    public static void ThrowInvalidUnionTag(ushort tag, Type baseType) =>
        throw new TightPackSerializationException(
            $"Union tag {tag} is not declared on {baseType.FullName} with [TightPackUnion]."
        );

    [DoesNotReturn]
    public static void ThrowUnionValueNotMapped(Type actual, Type baseType) =>
        throw new TightPackSerializationException(
            $"{actual.FullName} is not declared on {baseType.FullName} with [TightPackUnion]."
        );

    [DoesNotReturn]
    public static void ThrowInvalidString(int byteLength, int remaining) =>
        throw new TightPackSerializationException(
            $"String payload declares {byteLength} byte(s) but only {remaining} remain in the buffer."
        );

    [DoesNotReturn]
    public static void ThrowInvalidCollection(int length, int remaining) =>
        throw new TightPackSerializationException(
            $"Collection declares {length} element(s) but only {remaining} byte(s) remain, so the payload is corrupt."
        );

    [DoesNotReturn]
    public static void ThrowInvalidLayout(Type type, int expected, int actual) =>
        throw new TightPackSerializationException(
            $"TightPack's layout proof for {type.FullName} expected {expected} byte(s) but the runtime reports {actual}. " +
            "This is a bug in the layout analyzer, not in your code."
        );

    [DoesNotReturn]
    public static void ThrowBufferTooSmall(int requested, int available) =>
        throw new TightPackSerializationException(
            $"The buffer writer returned {available} byte(s) for a {requested} byte request."
        );
}
