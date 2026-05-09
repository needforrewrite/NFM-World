using System;
using System.Linq;
using System.Text;
using XamlX.TypeSystem;

namespace XamlX.CSharp;

/// <summary>
/// Utilities for formatting C# type names and string literals.
/// </summary>
internal static class CSharpFormatting
{
    /// <summary>
    /// Formats an IXamlType as a fully-qualified C# type name using global:: prefix.
    /// </summary>
    public static string FormatType(IXamlType type)
    {
        if (type.FullName == "System.Void") return "void";
        if (type.FullName == "System.Object") return "object";
        if (type.FullName == "System.String") return "string";
        if (type.FullName == "System.Boolean") return "bool";
        if (type.FullName == "System.Int32") return "int";
        if (type.FullName == "System.Int64") return "long";
        if (type.FullName == "System.Single") return "float";
        if (type.FullName == "System.Double") return "double";
        if (type.FullName == "System.Byte") return "byte";
        if (type.FullName == "System.SByte") return "sbyte";
        if (type.FullName == "System.Int16") return "short";
        if (type.FullName == "System.UInt16") return "ushort";
        if (type.FullName == "System.UInt32") return "uint";
        if (type.FullName == "System.UInt64") return "ulong";
        if (type.FullName == "System.Char") return "char";
        if (type.FullName == "System.Decimal") return "decimal";
        if (type.FullName == "System.IntPtr") return "nint";
        if (type.FullName == "System.UIntPtr") return "nuint";

        if (type.IsArray && type.ArrayElementType != null)
            return FormatType(type.ArrayElementType) + "[]";

        if (type is CSharpTypeBuilder csBuilder)
        {
            if (csBuilder.GenericParameters.Count > 0)
                return csBuilder.FullName + "<" + string.Join(", ", csBuilder.GenericParameters.Select(p => p.Name)) + ">";
            return csBuilder.FullName;
        }

        // Constructed generic CSharpTypeBuilder (e.g. Context<SomeNode>)
        if (type is ConstructedCSharpType constructed)
        {
            var defName = ((IXamlType)constructed.GenericTypeDefinition!).FullName;
            var genericArgs = string.Join(", ", constructed.GenericArguments.Select(FormatType));
            return $"{defName}<{genericArgs}>";
        }

        // Generic parameter types (like TTarget) - no global:: prefix
        if (type is CSharpGenericParameterType)
            return type.Name;

        var name = type.FullName;

        // Handle generic types
        if (type.GenericArguments.Count > 0)
        {
            var baseName = name;
            var backtick = baseName.IndexOf('`');
            if (backtick >= 0)
                baseName = baseName.Substring(0, backtick);

            var genericArgs = string.Join(", ", type.GenericArguments.Select(FormatType));
            return $"global::{baseName.Replace('+', '.')}<{genericArgs}>";
        }

        // Handle nested types
        name = name.Replace('+', '.');

        return $"global::{name}";
    }

    /// <summary>
    /// Formats a string as a C# string literal with proper escaping.
    /// </summary>
    public static string FormatStringLiteral(string value)
    {
        var sb = new StringBuilder("\"");
        foreach (var ch in value)
        {
            sb.Append(ch switch
            {
                '\\' => "\\\\",
                '"' => "\\\"",
                '\n' => "\\n",
                '\r' => "\\r",
                '\t' => "\\t",
                '\0' => "\\0",
                '\a' => "\\a",
                '\b' => "\\b",
                '\f' => "\\f",
                '\v' => "\\v",
                _ when char.IsControl(ch) => $"\\u{(int)ch:X4}",
                _ => ch.ToString()
            });
        }
        sb.Append('"');
        return sb.ToString();
    }
}