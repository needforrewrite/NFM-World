using System.Collections.Immutable;
using NFMWorldLibrary.Rad;
using NFMWorld.PluginSystem;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary;

/// <summary>
/// Builds the procedural tree/pile cluster a phy-addons <c>pile(seed,w,h,x,z)</c> line places.
/// Ported from <c>ContO(int, int, int, Medium, Trackers, int, int, int)</c>: a seeded blob of four
/// side planes and one canopy, with the same five collision boxes phy registers for it.
/// </summary>
[PluginVisible]
public static class StagePileBuilder
{
    /// <summary>Name every generated pile mesh carries, so the editor can tell a pile from a part.</summary>
    public const string ModelName = "pile";

    [PluginVisible]
    public static Rad3d Build(int seed, int width, int height, Color3 groundColor, Color3 polysColor)
    {
        var random = new URandom(seed);

        float f = Math.Clamp(width, 2, 6);
        float g = Math.Clamp(height, 2, 6);
        f /= 1.5f;
        g /= 1.5f;
        g *= 1f + (f - 2f) * 0.1786f;

        // Outer ring: eight points around a circle of a random radius each.
        var outerX = new int[8];
        var outerZ = new int[8];

        for (var i = 0; i < 8; i++)
        {
            var r = (float)(50.0 + 100.0 * random.NextDouble());

            switch (i)
            {
                case 0:
                    outerX[i] = -(int)(r * f * 0.7071f);
                    outerZ[i] = (int)(r * f * 0.7071f);

                    break;
                case 1:
                    outerX[i] = 0;
                    outerZ[i] = (int)(r * f);

                    break;
                case 2:
                    outerX[i] = (int)(r * f * 0.7071f);
                    outerZ[i] = (int)(r * f * 0.7071f);

                    break;
                case 3:
                    outerX[i] = (int)(r * f);
                    outerZ[i] = 0;

                    break;
                case 4:
                    outerX[i] = (int)(r * f * 0.7071f);
                    outerZ[i] = -(int)(r * f * 0.7071f);

                    break;
                case 5:
                    outerX[i] = 0;
                    outerZ[i] = -(int)(r * f);

                    break;
                case 6:
                    outerX[i] = -(int)(r * f * 0.7071f);
                    outerZ[i] = -(int)(r * f * 0.7071f);

                    break;
                default:
                    outerX[i] = -(int)(r * f);
                    outerZ[i] = 0;

                    break;
            }
        }

        // Inner ring and canopy heights.
        var innerX = new int[8];
        var innerZ = new int[8];
        var innerY = new int[8];

        for (var i = 0; i < 8; i++)
        {
            innerX[i] = (int)(outerX[i] * (0.2 + 0.4 * random.NextDouble()));
            innerZ[i] = (int)(outerZ[i] * (0.2 + 0.4 * random.NextDouble()));
            innerY[i] = -(int)((10.0 + 15.0 * random.NextDouble()) * g);
        }

        // Round the blob off: each ring point is pulled towards its neighbours' midpoint.

        for (var i = 0; i < 8; i++)
        {
            var prev = i == 0 ? 7 : i - 1;
            var next = i == 7 ? 0 : i + 1;
            outerX[i] = ((outerX[prev] + outerX[next]) / 2 + outerX[i]) / 2;
            outerZ[i] = ((outerZ[prev] + outerZ[next]) / 2 + outerZ[i]) / 2;
            innerX[i] = ((innerX[prev] + innerX[next]) / 2 + innerX[i]) / 2;
            innerZ[i] = ((innerZ[prev] + innerZ[next]) / 2 + innerZ[i]) / 2;
            innerY[i] = ((innerY[prev] + innerY[next]) / 2 + innerY[i]) / 2;
        }

        var shade = (f / g - 0.33f) / 33.4f;
        shade = shade < 0.005f ? 0f : shade > 0.057f ? 0.057f : shade;

        var polys = new Rad3dPoly[5];
        var lastShade = -1f;

        for (var side = 0; side < 4; side++)
        {
            var a = side * 2;
            var b = a + 2 == 8 ? 0 : a + 2;

            var points = new Vector3[6];
            points[0] = new Vector3(outerX[a], 0, outerZ[a]);
            points[1] = new Vector3(outerX[a + 1], 0, outerZ[a + 1]);
            points[2] = new Vector3(outerX[b], 0, outerZ[b]);
            points[3] = new Vector3(innerX[b], innerY[b], innerZ[b]);
            points[4] = new Vector3(innerX[a + 1], innerY[a + 1], innerZ[a + 1]);
            points[5] = new Vector3(innerX[a], innerY[a], innerZ[a]);

            var amount = (float)((0.17 - shade) * random.NextDouble());
            while (MathF.Abs(lastShade - amount) < 0.03f - shade * 0.176f)
                amount = (float)((0.17 - shade) * random.NextDouble());
            lastShade = amount;

            polys[side] = MakePoly(points, Shade(groundColor, polysColor, 2.2f + amount - shade));
        }

        var canopy = new Vector3[8];
        for (var i = 0; i < 8; i++)
            canopy[i] = new Vector3(innerX[i], innerY[i], innerZ[i]);

        polys[4] = MakePoly(
            canopy,
            Shade(groundColor, polysColor, 2.15f + (float)(0.02 * random.NextDouble()))
        );

        return new Rad3d(
            [],
            new CarStats(),
            [],
            null,
            BuildBoxes(outerX, outerZ, innerX, innerZ, innerY),
            polys,
            castsShadow: false,
            atp: [],
            fileName: ModelName
        );
    }

    /// <summary>phy tints each pile plane by the stage's ground and ground-polygon colours.</summary>
    private static Color3 Shade(Color3 ground, Color3 polys, float divisor)
        => new(
            ClampByte((ground.R + polys.R) / divisor),
            ClampByte((ground.G + polys.G) / divisor),
            ClampByte((ground.B + polys.B) / divisor)
        );

    private static short ClampByte(float value) => (short)Math.Clamp((int)value, 0, 255);

    private static Rad3dPoly MakePoly(Vector3[] points, Color3 color)
        => new(
            color,
            null,
            PolyType.Flat,
            null,
            0,
            points,
            Triangles: null
        );

    /// <summary>
    /// The five boxes phy's pile constructor registers: four slanted side walls and one canopy
    /// slab. Coordinates are local to the piece, as <see cref="Rad3dBoxDef"/> expects.
    /// </summary>
    private static Rad3dBoxDef[] BuildBoxes(
        int[] outerX,
        int[] outerZ,
        int[] innerX,
        int[] innerZ,
        int[] innerY
    )
    {
        var boxes = new Rad3dBoxDef[5];
        var lowX = 0;
        var highX = 0;
        var lowZ = 0;
        var highZ = 0;

        for (var side = 0; side < 4; side++)
        {
            var a = side * 2 + 1;
            var y = innerY[a] / 2;
            var rady = Math.Abs(innerY[a] / 2);
            int x, radx, z, radz;

            if (side != 0 && side != 2)
            {
                x = (outerX[a] + innerX[a]) / 2;
                radx = Math.Abs(x - outerX[a]);
                var b = side * 2 + 2 == 8 ? 0 : side * 2 + 2;
                z = (outerZ[side * 2] + outerZ[b]) / 2;
                radz = Math.Abs(z - outerZ[side * 2]);
            }
            else
            {
                z = (outerZ[a] + innerZ[a]) / 2;
                radz = Math.Abs(z - outerZ[a]);
                var b = side * 2 + 2 == 8 ? 0 : side * 2 + 2;
                x = (outerX[side * 2] + outerX[b]) / 2;
                radx = Math.Abs(x - outerX[side * 2]);
            }

            var slope = Slope(rady, side is 0 or 2 ? radz : radx);
            var xy = side is 1 or 3 ? slope : 0;
            var zy = side is 0 or 2 ? slope : 0;

            switch (side)
            {
                case 0:
                    lowZ = z - radz;

                    break;
                case 1:
                    lowX = x - radx;

                    break;
                case 2:
                    highZ = z + radz;
                    zy = -zy;

                    break;
                default:
                    highX = x + radx;
                    xy = -xy;

                    break;
            }

            boxes[side] = new Rad3dBoxDef(
                Xy: xy,
                Zy: zy,
                Radius: new f64Vector3(radx, rady + 10, radz),
                Translation: new f64Vector3(x, y, z),
                SurfaceType: SurfaceType.OffRoad,
                Damage: 1,
                NotWall: false,
                Color: new Color3()
            );
        }

        var topY = 0;
        foreach (var t in innerY)
            topY += t;
        topY /= 8;

        boxes[4] = new Rad3dBoxDef(
            Xy: 0,
            Zy: 0,
            Radius: new f64Vector3(Math.Abs(highX - lowX) / 2, 200, Math.Abs(highZ - lowZ) / 2),
            Translation: new f64Vector3((lowX + highX) / 2, topY, (lowZ + highZ) / 2),
            SurfaceType: SurfaceType.BumpySides,
            Damage: 1,
            NotWall: false,
            Color: new Color3()
        );

        return boxes;
    }

    /// <summary>phy clamps a pile box's slope to 40°.</summary>
    private static int Slope(int rise, int run)
    {
        if (run == 0)

            return 0;
        var degrees = (int)(MathF.Atan((float)rise / run) * (180f / MathF.PI));

        return Math.Clamp(degrees, -40, 40);
    }
}
