using Maxine.Extensions.Mathematics;
using Microsoft.Xna.Framework.Graphics;
using NFMWorldLibrary;
using Silk.NET.Maths;
using Half = System.Half;

namespace NFMWorld;

file static class MatrixParameterGuard
{
    public static bool HasNaN(Matrix m) =>
        float.IsNaN(m.M11) || float.IsNaN(m.M12) || float.IsNaN(m.M13) || float.IsNaN(m.M14) ||
        float.IsNaN(m.M21) || float.IsNaN(m.M22) || float.IsNaN(m.M23) || float.IsNaN(m.M24) ||
        float.IsNaN(m.M31) || float.IsNaN(m.M32) || float.IsNaN(m.M33) || float.IsNaN(m.M34) ||
        float.IsNaN(m.M41) || float.IsNaN(m.M42) || float.IsNaN(m.M43) || float.IsNaN(m.M44);
}

public readonly struct FloatEffectParameter(EffectParameter? parameter)
{
    public void SetValue(float value) => parameter?.SetValue(value);
}
public readonly struct Float2EffectParameter(EffectParameter? parameter)
{
    public void SetValue(float x, float y) => parameter?.SetValue(new Vector2(x, y));
    public void SetValue(Vector2 value) => parameter?.SetValue(value);
    public void SetValue(ReadOnlySpan<float> value) => parameter?.SetValueEXT(value[..2]);
}
public readonly struct Float3EffectParameter(EffectParameter? parameter)
{
    public void SetValue(float x, float y, float z) => parameter?.SetValue(new Vector3(x, y, z));
    public void SetValue(Vector3 value) => parameter?.SetValue(value);
    public void SetValue(ReadOnlySpan<float> value) => parameter?.SetValueEXT(value[..3]);
}
public readonly struct Float4EffectParameter(EffectParameter? parameter)
{
    public void SetValue(float x, float y, float z, float w) => parameter?.SetValue(new Vector4(x, y, z, w));
    public void SetValue(Vector4 value) => parameter?.SetValue(value);
    public void SetValue(ReadOnlySpan<float> value) => parameter?.SetValueEXT(value[..4]);
}
public readonly struct Float3x3EffectParameter(EffectParameter? parameter)
{
    public void SetValue(Matrix matrix)
    {
        if (parameter is null)
            return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            Logging.Warning("Float3x3EffectParameter.SetValue: matrix contains NaN, skipping.");
            return;
        }

        parameter.SetValue(matrix);
    }

    public void SetValueTranspose(Matrix matrix)
    {
        if (parameter is null)
            return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            Logging.Warning("Float3x3EffectParameter.SetValueTranspose: matrix contains NaN, skipping.");
            return;
        }

        parameter.SetValueTranspose(matrix);
    }
    public void SetValue(ReadOnlySpan<float> value) => parameter?.SetValueEXT(value[..9]);
    public void SetValueTranspose(ReadOnlySpan<float> value) => parameter?.SetValueTranspose(new Matrix(
        value[0], value[3], value[6], 0,
        value[1], value[4], value[7], 0,
        value[2], value[5], value[8], 0,
        0, 0, 0, 0)); // TODO might be wrong
}
public readonly struct Float4x4EffectParameter(EffectParameter? parameter)
{
    public void SetValue(Matrix matrix)
    {
        if (parameter is null)
            return;

        // Guard against NaN matrices — can happen on first frame if the camera
        // hasn't been fully initialised yet (e.g. Width/Height both zero, or
        // Position == LookAt causing CreateLookAt to normalise a zero vector).
        if (MatrixParameterGuard.HasNaN(matrix))
        {
            Logging.Warning("Float4x4EffectParameter.SetValue: matrix contains NaN, skipping.");
            return;
        }

        parameter.SetValue(matrix);
    }

    public void SetValueTranspose(Matrix matrix)
    {
        if (parameter is null)
            return;

        if (MatrixParameterGuard.HasNaN(matrix))
        {
            Logging.Warning("Float4x4EffectParameter.SetValueTranspose: matrix contains NaN, skipping.");
            return;
        }

        parameter.SetValueTranspose(matrix);
    }

    public void SetValue(ReadOnlySpan<float> value) => parameter?.SetValueEXT(value[..16]);
    public void SetValueTranspose(ReadOnlySpan<float> value) => parameter?.SetValueTranspose(new Matrix(
        value[0], value[4], value[8], value[12],
        value[1], value[5], value[9], value[13],
        value[2], value[6], value[10], value[14],
        value[3], value[7], value[11], value[15])); // TODO might be wrong
}
public readonly struct HalfEffectParameter(EffectParameter? parameter)
{
    public void SetValue(Half half) => parameter?.SetValue((float)half);
    public void SetValue(float value) => parameter?.SetValue(value);
}
public readonly struct Half2EffectParameter(EffectParameter? parameter)
{
    public void SetValue(Half x, Half y) => parameter?.SetValue(new Vector2((float)x, (float)y));
    public void SetValue(Half2 value) => parameter?.SetValue(new Vector2(value.X, value.Y));
    public void SetValue(float x, float y) => parameter?.SetValue(new Vector2(x, y));
    public void SetValue(Vector2 value) => parameter?.SetValue(value);
}
public readonly struct Half3EffectParameter(EffectParameter? parameter)
{
    public void SetValue(Half x, Half y, Half z) => parameter?.SetValue(new Vector3((float)x, (float)y, (float)z));
    public void SetValue(Half3 value) => parameter?.SetValue(new Vector3(value.X, value.Y, value.Z));
    public void SetValue(float x, float y, float z) => parameter?.SetValue(new Vector3(x, y, z));
    public void SetValue(Vector3 value) => parameter?.SetValue(value);
}
public readonly struct Half4EffectParameter(EffectParameter? parameter)
{
    public void SetValue(Half x, Half y, Half z, Half w) => parameter?.SetValue(new Vector4((float)x, (float)y, (float)z, (float)w));
    public void SetValue(Half4 value) => parameter?.SetValue(new Vector4(value.X, value.Y, value.Z, value.W));
    public void SetValue(float x, float y, float z, float w) => parameter?.SetValue(new Vector4(x, y, z, w));
    public void SetValue(Vector4 value) => parameter?.SetValue(value);
}
public readonly struct Half3x3EffectParameter(EffectParameter? parameter)
{
    // TODO
}
public readonly struct Half4x4EffectParameter(EffectParameter? parameter)
{
    // TODO
}
public readonly struct BoolEffectParameter(EffectParameter? parameter)
{
    public void SetValue(bool value) => parameter?.SetValue(value);
}
public readonly struct IntEffectParameter(EffectParameter? parameter)
{
    public void SetValue(int value) => parameter?.SetValue(value);
}
public readonly struct Int2EffectParameter(EffectParameter? parameter)
{
    public void SetValue(int x, int y) => parameter?.SetValueEXT([x, y]);
    public void SetValue(Int2 value) => parameter?.SetValueEXT([value.X, value.Y]);
}
public readonly struct Int3EffectParameter(EffectParameter? parameter)
{
    public void SetValue(int x, int y, int z) => parameter?.SetValueEXT([x, y, z]);
    public void SetValue(Int3 value) => parameter?.SetValueEXT([value.X, value.Y, value.Z]);
}
public readonly struct Int4EffectParameter(EffectParameter? parameter)
{
    public void SetValue(int x, int y, int z, int w) => parameter?.SetValueEXT([x, y, z, w]);
    public void SetValue(Int4 value) => parameter?.SetValueEXT([value.X, value.Y, value.Z, value.W]);
}
public readonly struct UintEffectParameter(EffectParameter? parameter)
{
    public void SetValue(uint value) => parameter?.SetValue((int)value);
}
public readonly struct Uint2EffectParameter(EffectParameter? parameter)
{
    public void SetValue(uint x, uint y) => parameter?.SetValueEXT([(int)x, (int)y]);
}
public readonly struct Uint3EffectParameter(EffectParameter? parameter)
{
    public void SetValue(uint x, uint y, uint z) => parameter?.SetValueEXT([(int)x, (int)y, (int)z]);
}
public readonly struct Uint4EffectParameter(EffectParameter? parameter)
{
    public void SetValue(uint x, uint y, uint z, uint w) => parameter?.SetValueEXT([(int)x, (int)y, (int)z, (int)w]);
}
public readonly struct TextureEffectParameter(EffectParameter? parameter)
{
    public void SetValue(Texture? texture) => parameter?.SetValue(texture);
}
