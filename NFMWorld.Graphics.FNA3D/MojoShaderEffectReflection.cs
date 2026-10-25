using System.Runtime.InteropServices;
using NFMWorld.MojoShader;
using NFMWorld.Shaders;
using static NFMWorld.MojoShader.MOJOSHADER_symbolClass;
using static NFMWorld.MojoShader.MOJOSHADER_symbolType;

namespace NFMWorld.Graphics.FNA3D;

/// <summary>
/// Walks a parsed MOJOSHADER_effect (the effectData FNA3D_CreateEffect hands back alongside the
/// FNA3D_Effect* itself) to build a <see cref="ShaderReflection"/> and locate techniques. This is
/// the empirical answer to the shader-abstraction design's open question of how FNA3D's
/// Effect-centric API maps onto <see cref="IShaderModule"/>: reflection can only be produced once
/// the Effect has actually been parsed against a live device, so it lives here (built by
/// <see cref="FNA3DGraphicsDevice.CreatePipeline"/>) rather than on the pre-device
/// <see cref="FNA3DShaderModule"/>. Ported from FNA's own Effect.INTERNAL_parseEffectStruct
/// (FNA/src/Graphics/Effect/Effect.cs), which reads the same native layout to build its
/// EffectParameter/EffectTechnique wrappers.
/// </summary>
internal static class MojoShaderEffectReflection
{
    /// <summary>
    /// One entry per uniform in <see cref="ShaderReflection.Uniforms"/> (by array index): the raw
    /// pointer to that parameter's value storage inside the Effect, which
    /// <see cref="FNA3DCommandBuffer.SetUniform"/> memcpy's <c>ReadOnlySpan&lt;byte&gt;</c> data into.
    /// </summary>
    public readonly record struct BuiltReflection(ShaderReflection Reflection, IntPtr[] ValuePointers);

    public static unsafe BuiltReflection Build(IntPtr effectData)
    {
        var effect = Marshal.PtrToStructure<MOJOSHADER_effect>(effectData);
        var uniforms = new List<UniformParam>();
        var valuePointers = new List<IntPtr>();
        var textures = new List<ResourceBinding>();
        var samplers = new List<ResourceBinding>();

        var paramSize = Marshal.SizeOf<MOJOSHADER_effectParam>();
        for (var i = 0; i < effect.param_count; i++)
        {
            var param = Marshal.PtrToStructure<MOJOSHADER_effectParam>(effect.parameters + i * paramSize);
            var value = param.value;

            // Samplers/textures/shader objects aren't set through SetUniform - they go through
            // ICommandBuffer.SetShaderResource instead. Their "slot" here is this parameter's
            // enumeration order among sampler/texture params only (mirrors uniforms' "index into
            // this reflection's list" convention) - NOT the shader's real compiled D3D9 sampler
            // register (s0, s1, ...), which MojoShader only exposes via the current pass's parsed
            // shader symbol table, not on MOJOSHADER_effectValue itself (see FNA's own
            // Effect.INTERNAL_ApplyEffect, which walks per-shader `samplers[].sampler_register` to
            // get the real register). Replicating that fully is unnecessary for a shader with a
            // single sampler (register 0 is what fxc.exe assigns the only sampler in the effect) -
            // revisit if/when a multi-sampler shader (e.g. Poly.fx's cascade shadow maps) needs
            // real per-sampler register resolution.
            if (value.type.parameter_type is MOJOSHADER_SYMTYPE_SAMPLER or MOJOSHADER_SYMTYPE_SAMPLER1D
                or MOJOSHADER_SYMTYPE_SAMPLER2D or MOJOSHADER_SYMTYPE_SAMPLER3D or MOJOSHADER_SYMTYPE_SAMPLERCUBE)
            {
                samplers.Add(new ResourceBinding(Marshal.PtrToStringAnsi(value.name) ?? string.Empty, samplers.Count));
                continue;
            }
            if (value.type.parameter_type is MOJOSHADER_SYMTYPE_TEXTURE or MOJOSHADER_SYMTYPE_TEXTURE1D
                or MOJOSHADER_SYMTYPE_TEXTURE2D or MOJOSHADER_SYMTYPE_TEXTURE3D or MOJOSHADER_SYMTYPE_TEXTURECUBE)
            {
                textures.Add(new ResourceBinding(Marshal.PtrToStringAnsi(value.name) ?? string.Empty, textures.Count));
                continue;
            }

            var name = Marshal.PtrToStringAnsi(value.name) ?? string.Empty;
            var type = MapType(value.type);
            // Matches FNA's own Effect.cs: value_count is already the total float component count
            // (rows * columns * array-element count), not a separate multiplier.
            var sizeInBytes = (int)(value.value_count * sizeof(float));

            // Offset here means "index into this reflection's Uniforms list" for this backend, not
            // a byte offset into a shared buffer - see ICommandBuffer.SetUniform's doc comment.
            uniforms.Add(new UniformParam(name, valuePointers.Count, sizeInBytes, type));
            valuePointers.Add(value.values);
        }

        var reflection = new ShaderReflection
        {
            Uniforms = uniforms,
            Textures = textures,
            Samplers = samplers,
        };
        return new BuiltReflection(reflection, valuePointers.ToArray());
    }

    /// <summary>Returns the MOJOSHADER_effectTechnique* for the named technique, or the first technique if <paramref name="name"/> is null.</summary>
    public static IntPtr FindTechnique(IntPtr effectData, string? name, out int passCount)
    {
        var effect = Marshal.PtrToStructure<MOJOSHADER_effect>(effectData);
        var techniqueSize = Marshal.SizeOf<MOJOSHADER_effectTechnique>();
        for (var i = 0; i < effect.technique_count; i++)
        {
            var techniquePtr = effect.techniques + i * techniqueSize;
            var technique = Marshal.PtrToStructure<MOJOSHADER_effectTechnique>(techniquePtr);
            var techniqueName = Marshal.PtrToStringAnsi(technique.name);
            if (name is null || techniqueName == name)
            {
                passCount = (int)technique.pass_count;
                return techniquePtr;
            }
        }
        throw new InvalidOperationException(name is null
            ? "Effect has no techniques."
            : $"Effect has no technique named '{name}'.");
    }

    private static UniformType MapType(in MOJOSHADER_symbolTypeInfo type) => type.parameter_class switch
    {
        MOJOSHADER_SYMCLASS_SCALAR when type.parameter_type == MOJOSHADER_SYMTYPE_FLOAT => UniformType.Float,
        MOJOSHADER_SYMCLASS_SCALAR => UniformType.Int,
        MOJOSHADER_SYMCLASS_VECTOR => type.columns switch
        {
            2 => UniformType.Vector2,
            3 => UniformType.Vector3,
            _ => UniformType.Vector4,
        },
        MOJOSHADER_SYMCLASS_MATRIX_ROWS or MOJOSHADER_SYMCLASS_MATRIX_COLUMNS => UniformType.Matrix4x4,
        _ => UniformType.Float,
    };
}
