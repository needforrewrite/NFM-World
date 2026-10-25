using System;
using System.CodeDom.Compiler;
using System.Diagnostics;
using System.Runtime.InteropServices;

[assembly: GeneratedCode("ClangSharp", "21.1.8.4")]

namespace SharpSokol.Native
{
    public static unsafe partial class Log
    {
        [DllImport("sokol", CallingConvention = CallingConvention.Cdecl, EntryPoint = "slog_func", ExactSpelling = true)]
        public static extern void func([NativeTypeName("const char *")] sbyte* tag, [NativeTypeName("uint32_t")] uint log_level, [NativeTypeName("uint32_t")] uint log_item, [NativeTypeName("const char *")] sbyte* message, [NativeTypeName("uint32_t")] uint line_nr, [NativeTypeName("const char *")] sbyte* filename, void* user_data);
    }

    /// <summary>Defines the type of a member as it was used in the native signature.</summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = false, Inherited = true)]
    [Conditional("DEBUG")]
    internal sealed partial class NativeTypeNameAttribute : Attribute
    {
        private readonly string _name;

        /// <summary>Initializes a new instance of the <see cref="NativeTypeNameAttribute" /> class.</summary>
        /// <param name="name">The name of the type that was used in the native signature.</param>
        public NativeTypeNameAttribute(string name)
        {
            _name = name;
        }

        /// <summary>Gets the name of the type that was used in the native signature.</summary>
        public string Name => _name;
    }

    /// <summary>Defines the annotation found in a native declaration.</summary>
    [AttributeUsage(AttributeTargets.Struct | AttributeTargets.Enum | AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter | AttributeTargets.ReturnValue, AllowMultiple = true, Inherited = false)]
    [Conditional("DEBUG")]
    internal sealed partial class NativeAnnotationAttribute : Attribute
    {
        private readonly string _annotation;

        /// <summary>Initializes a new instance of the <see cref="NativeAnnotationAttribute" /> class.</summary>
        /// <param name="annotation">The annotation that was used in the native declaration.</param>
        public NativeAnnotationAttribute(string annotation)
        {
            _annotation = annotation;
        }

        /// <summary>Gets the annotation that was used in the native declaration.</summary>
        public string Annotation => _annotation;
    }
}
