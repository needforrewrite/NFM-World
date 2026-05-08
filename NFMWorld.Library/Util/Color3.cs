using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using Maxine.Extensions.Mathematics;

namespace NFMWorldLibrary.Util;

public record struct Color3(
    [property: JsonPropertyName("r")] short R,
    [property: JsonPropertyName("g")] short G,
    [property: JsonPropertyName("b")] short B
)
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Vector3(Color3 c) => new(c.R / 255f, c.G / 255f, c.B / 255f);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Int3(Color3 c) => new(c.R, c.G, c.B);

    [JsonIgnore]
    public short this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        readonly get
        {
            return index switch
            {
                0 => R,
                1 => G,
                2 => B,
                _ => ThrowIndexOutOfRangeException(index)
            };

            static short ThrowIndexOutOfRangeException(int index)
            {
                throw new IndexOutOfRangeException($"Index was out of range. Must be between 0 and 2, inclusive. Received: {index}");
            }
        }
        set 
        {
            switch (index)
            {
                case 0:
                    R = value;
                    break;
                case 1:
                    G = value;
                    break;
                case 2:
                    B = value;
                    break;
                default:
                    ThrowIndexOutOfRangeException(index);
                    break;
            }

            static void ThrowIndexOutOfRangeException(int index)
            {
                throw new IndexOutOfRangeException($"Index was out of range. Must be between 0 and 2, inclusive. Received: {index}");
            }
        }
    }

    public static Color3 FromSpan(ReadOnlySpan<short> span)
        => new(span[0], span[1], span[2]);

    public static implicit operator ColorBGRA(Color3 color) => new(color.R, color.G, color.B, 255);
    public static explicit operator Color3(Color color) => new(color.R, color.G, color.B);
    public static explicit operator Color3(ColorBGRA color) => new(color.R, color.G, color.B);
    public static implicit operator Color(Color3 color) => new(
        (byte)Math.Clamp(color.R, (short)0, (short)255),
        (byte)Math.Clamp(color.G, (short)0, (short)255),
        (byte)Math.Clamp(color.B, (short)0, (short)255)
    );
    
    public static Color3 operator +(Color3 a, Color3 b)
        => new(
            (short)(a.R + b.R),
            (short)(a.G + b.G),
            (short)(a.B + b.B)
        );
    public static Color3 operator *(Color3 a, float b)
        => new(
            (short)(a.R * b),
            (short)(a.G * b),
            (short)(a.B * b)
        );
    public static Color3 operator /(Color3 a, float b)
        => new(
            (short)(a.R / b),
            (short)(a.G / b),
            (short)(a.B / b)
        );
    public static Color3 operator -(Color3 a, Color3 b)
        => new(
            (short)(a.R - b.R),
            (short)(a.G - b.G),
            (short)(a.B - b.B)
        );

    public void ToHSB(out float hue, out float saturation, out float brightness)
    {
        Colors.RGBtoHSB(R, G, B, out hue, out saturation, out brightness);
    }

    public static Color3 FromHSB(float hue, float saturation, float brightness)
    {
        var (r, g, b) = Colors.HSBtoRGB(hue, saturation, brightness);
        return new Color3(r, g, b);
    }

    private const double Factor = 0.7;
    public Color3 Darker()
    {
        return new Color3(
            (short) Math.Max((int) (R * Factor), 0),
            (short) Math.Max((int) (G * Factor), 0),
            (short) Math.Max((int) (B * Factor), 0)
        );
    }
    public Color3 Brighter()
    {
        var r = R;
        var g = G;
        var b = B;

        /* From 2D group:
         * 1. black.brighter() should return grey
         * 2. applying brighter to blue will always return blue, brighter
         * 3. non pure color (non zero rgb) will eventually return white
         */
        const int i = (int) (1.0 / (1.0 - Factor));
        if (r == 0 && g == 0 && b == 0)
        {
            return new Color3(i, i, i);
        }
        if (r is > 0 and < i)
        {
            r = i;
        }

        if (g is > 0 and < i)
        {
            g = i;
        }

        if (b is > 0 and < i)
        {
            b = i;
        }

        return new Color3(
            (short) Math.Min((int) (r / Factor), 255),
            (short) Math.Min((int) (g / Factor), 255),
            (short) Math.Min((int) (b / Factor), 255)
        );
    }
    
    public static implicit operator Vector4(Color3 color3)
        => new(color3.R / 255.0f, color3.G / 255.0f, color3.B / 255.0f, 1.0f);
    public static implicit operator System.Numerics.Vector4(Color3 color3)
        => new(color3.R / 255.0f, color3.G / 255.0f, color3.B / 255.0f, 1.0f);

    public void Deconstruct(out short R, out short G, out short B)
    {
        R = this.R;
        G = this.G;
        B = this.B;
    }
}