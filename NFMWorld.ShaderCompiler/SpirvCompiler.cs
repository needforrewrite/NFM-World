using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using Silk.NET.Core.Native;
using Silk.NET.Shaderc;

namespace NFMWorld.ShaderCompiler;

/// <summary>
/// HLSL -> SPIR-V, via shaderc (glslang's HLSL front-end + SPIRV-Tools).
///
/// This replaces sokol-shdc's <c>spirv.cc</c> (glslang wiring) and <c>input.cc</c>
/// (annotated-GLSL parsing) in one step: the front-end now reads HLSL directly, so
/// there is no annotated-GLSL intermediate to parse.
/// </summary>
public sealed unsafe class SpirvCompiler : IDisposable
{
    private readonly Shaderc _shaderc = Shaderc.GetApi();

    /// <summary>
    /// Resolves an <c>#include</c> target (relative or absolute path as written) to its
    /// text, or null if it cannot be found.
    /// </summary>
    public Func<string, string?> IncludeResolver { get; set; } = _ => null;

    // shaderc hands the IncludeResult back as raw pointers, so the buffers have to be
    // unmanaged and stay alive until glslang is done with them; they are freed in
    // ReleaseInclude, which shaderc calls once per include.

    public (byte[]? Spirv, string? Error) Compile(
        string source,
        string fileName,
        ShaderStage stage,
        string entryPoint,
        IReadOnlyDictionary<string, string>? defines = null)
    {
        // glslang does not fail on an entry point that does not exist: it compiles an empty
        // module and returns success, so a typo'd name silently produces a shader with no work
        // in it. (This was caught by an entry point named `MainVS` against a shader that declares
        // `VSMain` - the bundle compiled, the reflection had zero inputs, and the generated HLSL
        // was a stub.) Checking the name against the source is the only place this is visible,
        // because by the time the SPIR-V exists there is nothing left to compare against.
        if (!DeclaresEntryPoint(source, entryPoint))
            return (null, $"{fileName}: no function named '{entryPoint}' is defined in this source.");


        var compiler = _shaderc.CompilerInitialize();
        var options = _shaderc.CompileOptionsInitialize();
        try
        {
            _shaderc.CompileOptionsSetSourceLanguage(options, SourceLanguage.Hlsl);
            // The version argument is an EnvVersion, not a numeric Vulkan minor version.
            // Vulkan 1.1 implies SPIR-V 1.3; target env and SPIR-V version must agree or
            // shaderc rejects its own output at the optimisation step.
            _shaderc.CompileOptionsSetTargetEnv(options, TargetEnv.Vulkan, (uint)EnvVersion.Vulkan11);
            _shaderc.CompileOptionsSetTargetSpirv(options, SpirvVersion.Shaderc13);
            _shaderc.CompileOptionsSetOptimizationLevel(options, OptimizationLevel.Performance);
            // Without debug info glslang strips every OpName, and reflection can only
            // report synthetic identifiers (_20, _m0) instead of the HLSL names the
            // generated bindings have to match against.
            _shaderc.CompileOptionsSetGenerateDebugInfo(options);

            // HLSL cbuffer packing: honour the declared offsets/register assignments
            // rather than letting glslang repack. Without HlslOffsets a cbuffer's
            // reflected layout will not match what fxc/D3D11 produces for the same source.
            _shaderc.CompileOptionsSetHlslOffsets(options, 1);
            _shaderc.CompileOptionsSetAutoBindUniforms(options, 0);
            _shaderc.CompileOptionsSetPreserveBindings(options, 1);

            if (defines is not null)
                foreach (var (name, value) in defines)
                    _shaderc.CompileOptionsAddMacroDefinition(options, name, (UIntPtr)name.Length, value, (UIntPtr)value.Length);

            // glslang does not open included files itself when driven through shaderc; it
            // asks the host. Without this, every #include fails outright even though the
            // same source compiles under fxc/D3DCompile.
            using var resolver = PfnIncludeResolveFn.From(ResolveInclude);
            using var releaser = PfnIncludeResultReleaseFn.From(ReleaseInclude);
            _shaderc.CompileOptionsSetIncludeCallbacks(options, resolver, releaser, (void*)0);

            // The length is a byte count, not a char count: shaderc reads the marshalled
            // buffer as UTF-8, so any non-ASCII character in a comment (the repo's shaders
            // use ± and → in comments) makes source.Length too small and silently
            // truncates the last few bytes of the compilation unit.
            var byteCount = System.Text.Encoding.UTF8.GetByteCount(source);

            var kind = stage == ShaderStage.Vertex ? ShaderKind.VertexShader : ShaderKind.FragmentShader;
            var result = _shaderc.CompileIntoSpv(
                compiler, source, (UIntPtr)byteCount, kind, fileName, entryPoint, options);

            try
            {
                var status = _shaderc.ResultGetCompilationStatus(result);
                if (status != CompilationStatus.Success)
                {
                    var msg = _shaderc.ResultGetErrorMessageS(result) ?? status.ToString();
                    return (null, $"{fileName} [{entryPoint}]: {status}\n{msg}");
                }

                var length = (int)_shaderc.ResultGetLength(result);
                var bytes = new byte[length];
                var src = _shaderc.ResultGetBytes(result);
                new ReadOnlySpan<byte>(src, length).CopyTo(bytes);
                return (bytes, null);
            }
            finally
            {
                _shaderc.ResultRelease(result);
            }
        }
        finally
        {
            _shaderc.CompileOptionsRelease(options);
            _shaderc.CompilerRelease(compiler);
        }
    }

    /// <summary>
    /// Whether <paramref name="entryPoint"/> is defined in <paramref name="source"/>. Includes
    /// are not expanded here, so a name defined in an <c>#include</c>d header would report
    /// missing - every entry point in this repository is in the <c>.fx</c> itself, and a false
    /// negative is a loud error rather than a silently empty shader.
    /// </summary>
    private static bool DeclaresEntryPoint(string source, string entryPoint)
    {
        foreach (Match match in EntryPointRegex.Matches(source))
        {
            if (match.Groups[1].Value == entryPoint)
                return true;
        }
        return false;
    }

    // A definition, not a call or a forward declaration: an identifier followed by a parameter
    // list whose closing paren is followed by a body (allowing for the semantic/return-type
    // annotations that sit between the two, as in `): SV_TARGET\n{`).
    private static readonly Regex EntryPointRegex = new(
        @"\b([A-Za-z_]\w*)\s*\([^;{)]*\)\s*(?::[^;{]*)?\{", RegexOptions.Compiled);

    /// <summary>
    /// Allocates a native <c>shaderc_include_result</c> describing the resolved include.
    /// The name and content buffers are unmanaged and are freed in <see cref="ReleaseInclude"/>.
    /// </summary>
    private unsafe IncludeResult* ResolveInclude(void* userData, byte* requestedSource, int includeType, byte* requestingSource, nuint includeDepth)
    {
        var requested = Marshal.PtrToStringUTF8((nint)requestedSource) ?? "";
        var text = IncludeResolver(requested);

        var result = (IncludeResult*)NativeMemory.Alloc((nuint)sizeof(IncludeResult));
        if (text is null)
        {
            // Null content signals "not found"; glslang turns it into a compile error.
            result->SourceName = null;
            result->SourceNameLength = 0;
            result->Content = null;
            result->ContentLength = 0;
            result->UserData = null;
            return result;
        }

        var nameBytes = Encoding.UTF8.GetBytes(requested);
        var contentBytes = Encoding.UTF8.GetBytes(text);
        var namePtr = (byte*)NativeMemory.Alloc((nuint)nameBytes.Length);
        var contentPtr = (byte*)NativeMemory.Alloc((nuint)contentBytes.Length);
        Marshal.Copy(nameBytes, 0, (nint)namePtr, nameBytes.Length);
        Marshal.Copy(contentBytes, 0, (nint)contentPtr, contentBytes.Length);

        result->SourceName = namePtr;
        result->SourceNameLength = (UIntPtr)nameBytes.Length;
        result->Content = contentPtr;
        result->ContentLength = (UIntPtr)contentBytes.Length;
        result->UserData = null;

        return result;
    }

    /// <summary>
    /// Frees the buffers handed out by <see cref="ResolveInclude"/>. shaderc calls this
    /// once per include, when it is done reading that include's text.
    /// </summary>
    private unsafe void ReleaseInclude(void* userData, IncludeResult* result)
    {
        if (result is null) return;
        if (result->SourceName is not null) NativeMemory.Free(result->SourceName);
        if (result->Content is not null) NativeMemory.Free(result->Content);
        NativeMemory.Free(result);
    }

    public void Dispose() { }
}
