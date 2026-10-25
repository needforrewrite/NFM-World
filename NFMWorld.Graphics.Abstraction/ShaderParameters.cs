using System.Numerics;
using System.Runtime.InteropServices;
using Maxine.Extensions.Mathematics;
using NFMWorld.Graphics;
using Half = System.Half;
using Matrix = System.Numerics.Matrix4x4;

namespace NFMWorld;

file static class MatrixParameterGuard
{
    public static bool HasNaN(Matrix m) =>
        float.IsNaN(m.M11) || float.IsNaN(m.M12) || float.IsNaN(m.M13) || float.IsNaN(m.M14) ||
        float.IsNaN(m.M21) || float.IsNaN(m.M22) || float.IsNaN(m.M23) || float.IsNaN(m.M24) ||
        float.IsNaN(m.M31) || float.IsNaN(m.M32) || float.IsNaN(m.M33) || float.IsNaN(m.M34) ||
        float.IsNaN(m.M41) || float.IsNaN(m.M42) || float.IsNaN(m.M43) || float.IsNaN(m.M44);

    public static void ReportSkipped(string parameter, string method) =>
        GraphicsDiagnostics.Warning?.Invoke($"{parameter}.{method}: matrix contains NaN, skipping.");
}

/// <summary>
/// These wrapper structs used to capture an FNA <c>EffectParameter?</c> and call
/// <c>parameter.SetValue(...)</c> directly - a persistent reference into a live <c>Effect</c>
/// object. The new graphics abstraction has no such persistent-parameter concept: every uniform
/// write goes through <see cref="ICommandBuffer.SetUniform"/>, keyed by a small integer slot
/// (resolved once from a pipeline's <c>ShaderReflection</c> - see the generated
/// <c>*EffectParameters</c> types in <c>NFMWorld.Shaders.Generated</c>) and scoped to whichever
/// command buffer the caller is currently recording into. So each wrapper here now just captures
/// that resolved <paramref name="slot"/> and takes the <see cref="ICommandBuffer"/> explicitly on
/// every <c>SetValue</c> call, rather than capturing a parameter reference at construction time.
/// A negative slot (see <c>SlotOf</c> in the generated types) mirrors the old "parameter is null"
/// case - the HLSL compiler optimized the uniform out - and every SetValue below no-ops on it.
///
/// They live in this project - the one that declares <see cref="ICommandBuffer"/>, which is
/// everything they touch - rather than in the application, because the generated bundles name
/// them directly: a bundle that compiles is a bundle whose parameter wrappers have to resolve, and
/// the OpenGL smoke test compiles the real bundles without referencing the game. The namespace is
/// <c>NFMWorld</c> rather than <c>NFMWorld.Graphics</c> so that every existing call site, which
/// sits in the application's root namespace, keeps compiling untouched.
///
/// <see cref="GraphicsDiagnostics.Warning"/> carries the one diagnostic below (a NaN matrix); this
/// layer cannot reach the application's logging stack, so the host injects the sink.
/// </summary>
public readonly struct FloatEffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, float value)
    {
        if (slot < 0) return;
        Span<float> v = [value];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Float2EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, float x, float y) => SetValue(cb, new Vector2(x, y));
    public void SetValue(ICommandBuffer cb, Vector2 value)
    {
        if (slot < 0) return;
        Span<float> v = [value.X, value.Y];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        cb.SetUniform(slot, MemoryMarshal.AsBytes(value[..2]));
    }
}
public readonly struct Float3EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, float x, float y, float z) => SetValue(cb, new Vector3(x, y, z));
    public void SetValue(ICommandBuffer cb, Vector3 value)
    {
        if (slot < 0) return;
        Span<float> v = [value.X, value.Y, value.Z];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        cb.SetUniform(slot, MemoryMarshal.AsBytes(value[..3]));
    }
}
public readonly struct Float4EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, float x, float y, float z, float w) => SetValue(cb, new Vector4(x, y, z, w));
    public void SetValue(ICommandBuffer cb, Vector4 value)
    {
        if (slot < 0) return;
        Span<float> v = [value.X, value.Y, value.Z, value.W];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        cb.SetUniform(slot, MemoryMarshal.AsBytes(value[..4]));
    }
}
public readonly struct Float3x3EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Matrix matrix)
    {
        if (slot < 0) return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            MatrixParameterGuard.ReportSkipped(nameof(Float3x3EffectParameter), nameof(SetValue));
            return;
        }

        Span<float> v = [matrix.M11, matrix.M12, matrix.M13, matrix.M21, matrix.M22, matrix.M23, matrix.M31, matrix.M32, matrix.M33];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }

    public void SetValueTranspose(ICommandBuffer cb, Matrix matrix)
    {
        if (slot < 0) return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            MatrixParameterGuard.ReportSkipped(nameof(Float3x3EffectParameter), nameof(SetValueTranspose));
            return;
        }

        Span<float> v = [matrix.M11, matrix.M21, matrix.M31, matrix.M12, matrix.M22, matrix.M32, matrix.M13, matrix.M23, matrix.M33];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }

    public void SetValue(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        cb.SetUniform(slot, MemoryMarshal.AsBytes(value[..9]));
    }

    public void SetValueTranspose(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        // TODO might be wrong - carried over unchanged from the pre-conversion implementation.
        Span<float> v =
        [
            value[0], value[3], value[6],
            value[1], value[4], value[7],
            value[2], value[5], value[8],
        ];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Float4x4EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Matrix matrix)
    {
        if (slot < 0) return;

        // Guard against NaN matrices — can happen on first frame if the camera
        // hasn't been fully initialised yet (e.g. Width/Height both zero, or
        // Position == LookAt causing CreateLookAt to normalise a zero vector).
        if (MatrixParameterGuard.HasNaN(matrix))
        {
            MatrixParameterGuard.ReportSkipped(nameof(Float4x4EffectParameter), nameof(SetValue));
            return;
        }

        Span<float> v =
        [
            matrix.M11, matrix.M12, matrix.M13, matrix.M14,
            matrix.M21, matrix.M22, matrix.M23, matrix.M24,
            matrix.M31, matrix.M32, matrix.M33, matrix.M34,
            matrix.M41, matrix.M42, matrix.M43, matrix.M44,
        ];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }

    public void SetValueTranspose(ICommandBuffer cb, Matrix matrix)
    {
        if (slot < 0) return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            MatrixParameterGuard.ReportSkipped(nameof(Float4x4EffectParameter), nameof(SetValueTranspose));
            return;
        }

        Span<float> v =
        [
            matrix.M11, matrix.M21, matrix.M31, matrix.M41,
            matrix.M12, matrix.M22, matrix.M32, matrix.M42,
            matrix.M13, matrix.M23, matrix.M33, matrix.M43,
            matrix.M14, matrix.M24, matrix.M34, matrix.M44,
        ];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }

    public void SetValue(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        cb.SetUniform(slot, MemoryMarshal.AsBytes(value[..16]));
    }

    public void SetValueTranspose(ICommandBuffer cb, ReadOnlySpan<float> value)
    {
        if (slot < 0) return;
        // TODO might be wrong - carried over unchanged from the pre-conversion implementation.
        Span<float> v =
        [
            value[0], value[4], value[8], value[12],
            value[1], value[5], value[9], value[13],
            value[2], value[6], value[10], value[14],
            value[3], value[7], value[11], value[15],
        ];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct HalfEffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Half half) => SetValue(cb, (float)half);
    public void SetValue(ICommandBuffer cb, float value)
    {
        if (slot < 0) return;
        Span<float> v = [value];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Half2EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Half x, Half y) => SetValue(cb, (float)x, (float)y);
    public void SetValue(ICommandBuffer cb, Half2 value) => SetValue(cb, value.X, value.Y);
    public void SetValue(ICommandBuffer cb, float x, float y)
    {
        if (slot < 0) return;
        Span<float> v = [x, y];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Vector2 value) => SetValue(cb, value.X, value.Y);
}
public readonly struct Half3EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Half x, Half y, Half z) => SetValue(cb, (float)x, (float)y, (float)z);
    public void SetValue(ICommandBuffer cb, Half3 value) => SetValue(cb, value.X, value.Y, value.Z);
    public void SetValue(ICommandBuffer cb, float x, float y, float z)
    {
        if (slot < 0) return;
        Span<float> v = [x, y, z];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Vector3 value) => SetValue(cb, value.X, value.Y, value.Z);
}
public readonly struct Half4EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, Half x, Half y, Half z, Half w) => SetValue(cb, (float)x, (float)y, (float)z, (float)w);
    public void SetValue(ICommandBuffer cb, Half4 value) => SetValue(cb, value.X, value.Y, value.Z, value.W);
    public void SetValue(ICommandBuffer cb, float x, float y, float z, float w)
    {
        if (slot < 0) return;
        Span<float> v = [x, y, z, w];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Vector4 value) => SetValue(cb, value.X, value.Y, value.Z, value.W);
}
public readonly struct Half3x3EffectParameter(int slot)
{
    // TODO - was already an empty stub before this conversion; no .fx file currently uses a half3x3 uniform.
}
public readonly struct Half4x4EffectParameter(int slot)
{
    // TODO - was already an empty stub before this conversion; no .fx file currently uses a half4x4 uniform.
}
public readonly struct BoolEffectParameter(int slot)
{
    // HLSL bool uniforms are backed by a 4-byte int, not a float bit pattern (matches FNA's own
    // EffectParameter.SetValue(bool) and NFMWorld.Graphics.FNA3D.MojoShaderEffectReflection's
    // UniformType.Int mapping for non-float scalars).
    public void SetValue(ICommandBuffer cb, bool value)
    {
        if (slot < 0) return;
        Span<int> v = [value ? 1 : 0];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct IntEffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, int value)
    {
        if (slot < 0) return;
        Span<int> v = [value];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Int2EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, int x, int y)
    {
        if (slot < 0) return;
        Span<int> v = [x, y];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Int2 value) => SetValue(cb, value.X, value.Y);
}
public readonly struct Int3EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, int x, int y, int z)
    {
        if (slot < 0) return;
        Span<int> v = [x, y, z];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Int3 value) => SetValue(cb, value.X, value.Y, value.Z);
}
public readonly struct Int4EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, int x, int y, int z, int w)
    {
        if (slot < 0) return;
        Span<int> v = [x, y, z, w];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
    public void SetValue(ICommandBuffer cb, Int4 value) => SetValue(cb, value.X, value.Y, value.Z, value.W);
}
public readonly struct UintEffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, uint value)
    {
        if (slot < 0) return;
        Span<int> v = [(int)value];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Uint2EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, uint x, uint y)
    {
        if (slot < 0) return;
        Span<int> v = [(int)x, (int)y];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Uint3EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, uint x, uint y, uint z)
    {
        if (slot < 0) return;
        Span<int> v = [(int)x, (int)y, (int)z];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct Uint4EffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, uint x, uint y, uint z, uint w)
    {
        if (slot < 0) return;
        Span<int> v = [(int)x, (int)y, (int)z, (int)w];
        cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
    }
}
public readonly struct TextureEffectParameter(int slot)
{
    public void SetValue(ICommandBuffer cb, ITexture? texture, ISampler? sampler)
    {
        if (slot < 0 || texture is null || sampler is null) return;
        cb.SetShaderResource(slot, texture, sampler);
    }
}
