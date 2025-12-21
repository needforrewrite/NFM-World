using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json.Serialization;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using Color = NFMWorld.Util.Color;

namespace NFMWorld.Mad;

public class RadParser
{
    private int _npoints = 0;
    private bool _stonecold;
    private bool _noOutline;
    private float idiv = 1, iwid = 1, scaleX = 1, scaleY = 1, scaleZ = 1;
    
    private Dictionary<Color3, int> _colors = new();
    private CarStats _stats = new();
    private List<Rad3dWheelDef> _wheels = new();
    private Rad3dRimsDef? _rims;
    private List<Rad3dBoxDef> _boxes = new();
    private List<Rad3dPoly> _polys = new();
    private List<Vector3> _points = new();
    private bool _road;
    private bool _castsShadow;
    
    private RadParser()
    {
    }

    public static Rad3d ParseRad(string radFile)
    {
        var parser = new RadParser();
        var lines = radFile.AsSpan().Split("\n");
        int lineNumber = 0;
        foreach (var lineRange in lines)
        {
            lineNumber++;
            var line = radFile.AsSpan(lineRange).Trim();
            if (line.IsEmpty) continue;
            try
            {
                parser.ParseLine(line);
            }
            catch (Exception ex)
            {
                throw new Exception($"Error parsing line {lineNumber}: '{line.ToString()}'\n{ex.Message}", ex);
            }
        }

        return RepositionCar(new Rad3d(
            Colors: parser._colors.Keys.ToArray(),
            Stats: parser._stats,
            Wheels: parser._wheels.ToArray(),
            Rims: parser._rims,
            Boxes: parser._boxes.ToArray(),
            Polys: parser._polys.ToArray(),
            CastsShadow: parser._castsShadow
        ));
    }

    private static Rad3d RepositionCar(Rad3d rad3d)
    {
        if (rad3d.Wheels is { Length: < 4 }) return rad3d;

        // reposition car so that ground is at y=0 and the wheel x and z are equidistant from the origin
        // this fixes masheen bouncing on the big ramp
        float groundTranslation = float.MaxValue;
        float wheelXTranslation = 0;
        float wheelZTranslation = 0;
        for (var i = 0; i < 4; i++)
        {
            var wheel = rad3d.Wheels[i];
            var groundY = wheel.Ground;
            if (groundY < groundTranslation)
            {
                groundTranslation = groundY;
            }

            wheelXTranslation += wheel.Position.X;
            wheelZTranslation += wheel.Position.Z;
        }

        wheelXTranslation /= 4f;
        wheelZTranslation /= 4f;
        
        // maxine: this code is incredibly crucial!
        // in theory we should be moving the car to the wheel center, because otherwise the car drifts off of its center
        // on every tick when rotated around, however doing this breaks hypergliding. as we want to retain vanilla
        // behavior at high tickrate, we instead move it by x/y/z * phyiscs_multiplier, which restores
        // behavior at vanilla tickrate speeds.
        
        for (var i = 0; i < rad3d.Wheels.Length; i++)
        {
            var wheel = rad3d.Wheels[i];
            rad3d.Wheels[i] = wheel with
            {
                Position = new Vector3(
                    wheel.Position.X - (wheelXTranslation),
                    wheel.Position.Y,// - (groundTranslation),
                    wheel.Position.Z - (wheelZTranslation)
                )
            };
        }

        for (var i = 0; i < rad3d.Polys.Length; i++)
        {
            var poly = rad3d.Polys[i];
            for (var j = 0; j < poly.Points.Length; j++)
            {
                var point = poly.Points[j];
                poly.Points[j] = new Vector3(
                    point.X - (wheelXTranslation),
                    point.Y,// - (groundTranslation),
                    point.Z - (wheelZTranslation)
                );
            }
        }

        return rad3d;
    }

    private void ParseLine(ReadOnlySpan<char> line)
    {
        if (line.StartsWith("stonecold") || line.StartsWith("newstone")) _stonecold = true;
        else if (line.StartsWith("road")) _road = true;
        else if (line.StartsWith("notroad")) _road = false;
        else if (line.StartsWith("shadow")) _castsShadow = true;
        else if (line.StartsWith("gshadow")) _castsShadow = true; // used by decorative trees

        else if (line.StartsWith("1stColor("))
        {
            var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
            _colors[color] = 0;
        }

        else if (line.StartsWith("2ndColor("))
        {
            var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
            _colors[color] = 1;
        }

        else if (line.StartsWith("3rdColor("))
        {
            var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
            _colors[color] = 2;
        }

        else if (line.StartsWith("4thColor("))
        {
            var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
            _colors[color] = 3;
        }

        else if (line.StartsWith("swits(")) _stats = _stats with { Swits = Int3.FromSpan(BracketParser.GetNumbers(line, stackalloc int[3])) };
        else if (line.StartsWith("acelf(")) _stats = _stats with { Acelf = Vector3.FromSpan(BracketParser.GetNumbers(line, stackalloc float[3])) };
        else if (line.StartsWith("handb(")) _stats = _stats with { Handb = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("airs(")) _stats = _stats with { Airs = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("airc(")) _stats = _stats with { Airc = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("turn(")) _stats = _stats with { Turn = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("grip(")) _stats = _stats with { Grip = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("bounce(")) _stats = _stats with { Bounce = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("simag(")) _stats = _stats with { Simag = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("moment(")) _stats = _stats with { Moment = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("comprad(")) _stats = _stats with { Comprad = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("push(")) _stats = _stats with { Push = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("revpush(")) _stats = _stats with { Revpush = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("lift(")) _stats = _stats with { Lift = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("revlift(")) _stats = _stats with { Revlift = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("powerloss(")) _stats = _stats with { Powerloss = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("flipy(")) _stats = _stats with { Flipy = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("msquash(")) _stats = _stats with { Msquash = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("clrad(")) _stats = _stats with { Clrad = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("dammult(")) _stats = _stats with { Dammult = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("maxmag(")) _stats = _stats with { Maxmag = BracketParser.GetNumber<int>(line) };
        else if (line.StartsWith("dishandle(")) _stats = _stats with { Dishandle = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("outdam(")) _stats = _stats with { Outdam = BracketParser.GetNumber<float>(line) };
        else if (line.StartsWith("name(")) _stats = _stats with { Name = BracketParser.GetString(line) };
        else if (line.StartsWith("enginsignature(")) _stats = _stats with { Enginsignature = BracketParser.GetNumber<sbyte>(line) };

        else if (line.StartsWith("w("))
        {
            var (cx, (cy, (cz, (rotates, (width, (height, _)))))) = BracketParser.GetNumbers(line, stackalloc int[6]);
            _wheels.Add(new Rad3dWheelDef(
                Position: new Vector3(
                    cx * idiv * iwid * scaleX,
                    cy * idiv * scaleY,
                    cz * idiv * scaleZ
                ),
                Rotates: rotates,
                Width: width * idiv * iwid,
                Height: height * idiv
            ));
        }

        else if (line.StartsWith("rims("))
        {
            _rims = new Rad3dRimsDef(
                Color: new Color3(
                    (byte)BracketParser.GetNumbers(line, stackalloc int[3])[0],
                    (byte)BracketParser.GetNumbers(line, stackalloc int[3])[1],
                    (byte)BracketParser.GetNumbers(line, stackalloc int[3])[2]
                ),
                Size: BracketParser.GetNumbers(line, stackalloc int[5])[3],
                Depth: BracketParser.GetNumbers(line, stackalloc int[5])[4]
            );
        }

        else if (line.StartsWith("div(")) idiv = BracketParser.GetNumber<int>(line) / 10f;
        else if (line.StartsWith("idiv(")) idiv = BracketParser.GetNumber<int>(line) / 100f;
        else if (line.StartsWith("iwid(")) iwid = BracketParser.GetNumber<int>(line) / 100f;
        else if (line.StartsWith("ScaleX(")) scaleX = BracketParser.GetNumber<int>(line) / 100f;
        else if (line.StartsWith("ScaleY(")) scaleY = BracketParser.GetNumber<int>(line) / 100f;
        else if (line.StartsWith("ScaleZ(")) scaleZ = BracketParser.GetNumber<int>(line) / 100f;

        else if (line.StartsWith("<track>"))
        {
            _boxes.Add(new Rad3dBoxDef(
                Xy: 0,
                Zy: 0,
                Radius: new Vector3(),
                Translation: new Vector3(),
                Skid: 0,
                Damage: 0,
                NotWall: false,
                Color: new Color3()
            ));
        }

        if (_boxes.Count > 0)
        {
            ref var currentBox = ref _boxes.GetValueRef(^1);
            if (line.StartsWith("c("))
            {
                var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
                currentBox = currentBox with { Color = color };
            }
            else if (line.StartsWith("xy("))
                currentBox = currentBox with { Xy = BracketParser.GetNumber<int>(line) };
            else if (line.StartsWith("zy("))
                currentBox = currentBox with { Zy = BracketParser.GetNumber<int>(line) };
            else if (line.StartsWith("radx("))
                currentBox = currentBox with
                {
                    Radius = currentBox.Radius with
                    {
                        X = BracketParser.GetNumber<int>(line) * idiv * iwid * scaleX
                    }
                };
            else if (line.StartsWith("rady("))
                currentBox = currentBox with
                {
                    Radius = currentBox.Radius with
                    {
                        Y = BracketParser.GetNumber<int>(line) * idiv * scaleY
                    }
                };
            else if (line.StartsWith("radz("))
                currentBox = currentBox with
                {
                    Radius = currentBox.Radius with
                    {
                        Z = BracketParser.GetNumber<int>(line) * idiv * scaleZ
                    }
                };
            else if (line.StartsWith("tx("))
                currentBox = currentBox with
                {
                    Translation = currentBox.Translation with
                    {
                        X = BracketParser.GetNumber<int>(line) * idiv * iwid * scaleX
                    }
                };
            else if (line.StartsWith("ty("))
                currentBox = currentBox with
                {
                    Translation = currentBox.Translation with
                    {
                        Y = BracketParser.GetNumber<int>(line) * idiv * scaleY
                    }
                };
            else if (line.StartsWith("tz("))
                currentBox = currentBox with
                {
                    Translation = currentBox.Translation with
                    {
                        Z = BracketParser.GetNumber<int>(line) * idiv * scaleZ
                    }
                };
            else if (line.StartsWith("skid("))
                currentBox = currentBox with { Skid = BracketParser.GetNumber<int>(line) };
            else if (line.StartsWith("dam"))
                currentBox = currentBox with { Damage = 3 };
            else if (line.StartsWith("notwall("))
                currentBox = currentBox with { NotWall = true };
        }

        if (line.StartsWith("<p>"))
        {
            _polys.Add(new Rad3dPoly(new Color3(), null, PolyType.Flat, LineType.Flat, 0.0f, []));
            _noOutline = false;
        }

        if (_polys.Count > 0)
        {
            ref var poly = ref _polys.GetValueRef(^1);
            if (line.StartsWith("c("))
            {
                var color = Color3.FromSpan(BracketParser.GetNumbers(line, stackalloc short[3]));
                poly = poly with { Color = color };
                if (_colors.TryGetValue(color, out var colNum))
                {
                    poly = poly with { ColNum = colNum };
                }
            }

            else if (line.StartsWith("glass")) poly = poly with { PolyType = PolyType.Glass };
            else if (line.StartsWith("lightB")) poly = poly with { PolyType = PolyType.BrakeLight };
            else if (line.StartsWith("lightR")) poly = poly with { PolyType = PolyType.ReverseLight };
            else if (line.StartsWith("light")) poly = poly with { PolyType = PolyType.Light };
            else if (line.StartsWith("gr(-10)")) poly = poly with { LineType = LineType.BrightColored };
            else if (line.StartsWith("gr(-18)")) poly = poly with { LineType = LineType.Charged };
            else if (line.StartsWith("gr(-13)")) poly = poly with { PolyType = PolyType.Finish };
            else if (line.StartsWith("decal"))
            {
                // Parse decal with optional value: decal or decal(value)
                float decalValue = -1.0f; // default (no offset)
                if (line.Length > 5 && line[5] == '(')
                {
                    decalValue = BracketParser.GetNumber<float>(line);
                }
                poly = poly with { DecalOffset = decalValue };
            }

            else if (line.StartsWith("p("))
            {
                var position = Int3.FromSpan(BracketParser.GetNumbers(line, stackalloc int[3]));
                var transformedPoint = new Vector3(
                    position.X * idiv * iwid * scaleX,
                    position.Y * idiv * scaleY,
                    position.Z * idiv * scaleZ
                );
                _points.Add(transformedPoint);
            }
            
            else if (line.StartsWith("noOutline")) _noOutline = true;

            else if (line.StartsWith("</p>"))
            {
                poly = poly with { Points = _points.ToArray() };
                _points.Clear();
                if (_stonecold || _noOutline)
                {
                    if (poly.LineType == LineType.Flat)
                    {
                        if (_road)
                        {
                            poly = poly with { LineType = LineType.Colored };
                        }
                        else
                        {
                            poly = poly with { LineType = null };
                        }
                    }
                }
            }
        }
    }
}

[JsonSourceGenerationOptions(WriteIndented = false, PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(Rad3dWheelDef))]
[JsonSerializable(typeof(Rad3dRimsDef))]
[JsonSerializable(typeof(Rad3dBoxDef))]
[JsonSerializable(typeof(Rad3dPoly))]
[JsonSerializable(typeof(Rad3dWheelDef[]))]
[JsonSerializable(typeof(Rad3dRimsDef[]))]
[JsonSerializable(typeof(Rad3dBoxDef[]))]
[JsonSerializable(typeof(Rad3dPoly[]))]
[JsonSerializable(typeof(Rad3d))]
[JsonSerializable(typeof(CarStats))]
[JsonSerializable(typeof(Color3))]
[JsonSerializable(typeof(Int3))]
[JsonSerializable(typeof(Vector3))]
[JsonSerializable(typeof(Color3[]))]
[JsonSerializable(typeof(Int3[]))]
[JsonSerializable(typeof(Vector3[]))]
public partial class SourceGenerationContext : JsonSerializerContext;

public readonly record struct Rad3dWheelDef(
    [property: JsonPropertyName("pos")] Vector3 Position,
    [property: JsonPropertyName("rotates")] int Rotates,
    [property: JsonPropertyName("w")] float Width,
    [property: JsonPropertyName("h")] float Height
)
{
    public int Sparkat => (int) ((Height / 10f) * 24.0F);
    public int Ground => (int) (Position.Y + 13.0F * (Height / 10f));
}

public readonly record struct Rad3dRimsDef(
    [property: JsonPropertyName("color")] Color3 Color,
    [property: JsonPropertyName("size")] float Size,
    [property: JsonPropertyName("depth")] float Depth
);

public readonly record struct Rad3dBoxDef(
    [property: JsonPropertyName("xy")] float Xy,
    [property: JsonPropertyName("zy")] float Zy,
    [property: JsonPropertyName("rad")] Vector3 Radius,
    [property: JsonPropertyName("t")] Vector3 Translation,
    [property: JsonPropertyName("skid")] int Skid,
    [property: JsonPropertyName("damage")] int Damage,
    [property: JsonPropertyName("notwall")] bool NotWall,
    [property: JsonPropertyName("c")] Color3 Color
);

public record Rad3d(
    [property: JsonPropertyName("colors")] Color3[] Colors,
    [property: JsonPropertyName("stats")] CarStats Stats,
    [property: JsonPropertyName("wheels")] Rad3dWheelDef[] Wheels,
    [property: JsonPropertyName("rims")] Rad3dRimsDef? Rims,
    [property: JsonPropertyName("boxes")] Rad3dBoxDef[] Boxes,
    [property: JsonPropertyName("polys")] Rad3dPoly[] Polys,
    [property: JsonPropertyName("shadow")] bool CastsShadow
)
{
    public int MaxRadius { get; } = CalculateMaxRadius(Polys);

    private static int CalculateMaxRadius(Rad3dPoly[] polys)
    {
        var maxR = 0;
        foreach (var poly in polys)
        foreach (var point in poly.Points)
        {
            var rad = (int) float.Sqrt(point.X * point.X + point.Y * point.Y + point.Z * point.Z);
            if (rad > maxR)
            {
                maxR = rad;
            }
        }

        return maxR;
    }

    public Rad3d(Rad3dPoly[] polys, bool castsShadow) : this([], new CarStats(), [], null, [], polys, castsShadow)
    {
    }
}

public readonly record struct Rad3dPoly(
    [property: JsonPropertyName("c")] Color3 Color,
    [property: JsonPropertyName("colnum")] int? ColNum,
    [property: JsonPropertyName("polyType")] PolyType PolyType,
    [property: JsonPropertyName("lineType")] LineType? LineType,
    [property: JsonPropertyName("decalOffset")] float DecalOffset,
    [property: JsonPropertyName("p")] Vector3[] Points
);

public readonly record struct Color3(
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
    public int this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            return index switch
            {
                0 => R,
                1 => G,
                2 => B,
                _ => throw new IndexOutOfRangeException()
            };
        }
    }
    
    public static implicit operator Color(Color3 color) => new(color.R, color.G, color.B);
    public static implicit operator ColorBGRA(Color3 color) => new(color.R, color.G, color.B, 255);
    public static explicit operator Color3(Color color) => new(color.R, color.G, color.B);
    public static explicit operator Color3(ColorBGRA color) => new(color.R, color.G, color.B);
    public static implicit operator Microsoft.Xna.Framework.Color(Color3 color) => new(
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
}
