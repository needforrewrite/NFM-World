using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class WheelMeshBuilder
{
    private readonly Rad3dWheelDef _wheelDef;
    private float _depth = 3.0F;
    internal int Mast = 0;

    private readonly int[] _rc = [120, 120, 120];

    private float _size = 2.0F;
    
    private List<Rad3dPoly> _planes = new(19);

    public WheelMeshBuilder(Rad3dWheelDef wheelDef, Rad3dRimsDef? rimsN)
    {
        _wheelDef = wheelDef;
        if (rimsN is { } rims)
        {
            _rc[0] = rims.Color.R;
            _rc[1] = rims.Color.G;
            _rc[2] = rims.Color.B;
            _size = rims.Size / 10.0F;
            if (_size < 0.0F)
            {
                _size = 0.0F;
            }

            _depth = rims.Depth / 10.0F;
            if (_depth / _size > 41.0F)
            {
                _depth = _size * 41.0F;
            }

            if (_depth / _size < -25.0F)
            {
                _depth = -(_size * 25.0F);
            }
        }

        Make(_planes, 0, 0, 0, wheelDef.Rotates, wheelDef.Width, wheelDef.Height);
    }

    public Mesh BuildMesh(GraphicsDevice graphicsDevice, Transform parent)
    {
        return new Mesh(graphicsDevice, new Rad3d(_planes.ToArray(), true))
        {
            Position = _wheelDef.Position,
            Parent = parent
        };
    }

    private void Make(List<Rad3dPoly> planes, float cx, float cy, float cz, int rotates, float width, float height)
    {
        Span<float> x = stackalloc float[20];
        Span<float> y = stackalloc float[20];
        Span<float> z = stackalloc float[20];
        Span<int> color = [45, 45, 45];
        float offcx = 0;
        var w10 = width / 10.0F;
        var h10 = height / 10.0F;
        if (rotates == 11)
        {
            offcx = (cx + 4.0F * w10);
        }
        // Sparkat = (int) (h10 * 24.0F);
        // Ground = (int) (cy + 13.0F * h10);
        for (var i = 0; i < 20; i++)
        {
            x[i] = (cx - 4.0F * w10);
        }
        y[0] = (cy - 9.1923F * h10);
        z[0] = (cz + 9.1923F * h10);
        y[1] = (cy - 12.557F * h10);
        z[1] = (cz + 3.3646F * h10);
        y[2] = (cy - 12.557F * h10);
        z[2] = (cz - 3.3646F * h10);
        y[3] = (cy - 9.1923F * h10);
        z[3] = (cz - 9.1923F * h10);
        y[4] = (cy - 3.3646F * h10);
        z[4] = (cz - 12.557F * h10);
        y[5] = (cy + 3.3646F * h10);
        z[5] = (cz - 12.557F * h10);
        y[6] = (cy + 9.1923F * h10);
        z[6] = (cz - 9.1923F * h10);
        y[7] = (cy + 12.557F * h10);
        z[7] = (cz - 3.3646F * h10);
        y[8] = (cy + 12.557F * h10);
        z[8] = (cz + 3.3646F * h10);
        y[9] = (cy + 9.1923F * h10);
        z[9] = (cz + 9.1923F * h10);
        y[10] = (cy + 3.3646F * h10);
        z[10] = (cz + 12.557F * h10);
        y[11] = (cy - 3.3646F * h10);
        z[11] = (cz + 12.557F * h10);
        y[12] = cy;
        z[12] = (cz + 10.0F * _size);
        y[13] = (cy + 8.66f * _size);
        z[13] = (cz + 5.0F * _size);
        y[14] = (cy + 8.66f * _size);
        z[14] = (cz - 5.0F * _size);
        y[15] = cy;
        z[15] = (cz - 10.0F * _size);
        y[16] = (cy - 8.66f * _size);
        z[16] = (cz - 5.0F * _size);
        y[17] = (cy - 8.66f * _size);
        z[17] = (cz + 5.0F * _size);
        y[18] = cy;
        z[18] = (cz + 10.0F * _size);
        y[19] = (cy - 3.3646F * h10);
        z[19] = (cz + 12.557F * h10);

        AddPoly(x, z, y, 20, color, true);
        x[2] = (cx - _depth * w10);
        y[2] = cy;
        z[2] = cz;
        y[0] = cy;
        z[0] = (cz + 10.0F * _size);
        y[1] = (cy + 8.66f * _size);
        z[1] = (cz + 5.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        y[0] = (cy + 8.66f * _size);
        z[0] = (cz + 5.0F * _size);
        y[1] = (cy + 8.66f * _size);
        z[1] = (cz - 5.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        y[0] = (cy + 8.66f * _size);
        z[0] = (cz - 5.0F * _size);
        y[1] = cy;
        z[1] = (cz - 10.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        y[0] = cy;
        z[0] = (cz - 10.0F * _size);
        y[1] = (cy - 8.66f * _size);
        z[1] = (cz - 5.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        y[0] = (cy - 8.66f * _size);
        z[0] = (cz - 5.0F * _size);
        y[1] = (cy - 8.66f * _size);
        z[1] = (cz + 5.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        y[0] = (cy - 8.66f * _size);
        z[0] = (cz + 5.0F * _size);
        y[1] = cy;
        z[1] = (cz + 10.0F * _size);
        AddPoly(x, z, y, 3, _rc);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 12.557F * h10);
        z[0] = (cz + 3.3646F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 12.557F * h10);
        z[1] = (cz - 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 12.557F * h10);
        z[2] = (cz - 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 12.557F * h10);
        z[3] = (cz + 3.3646F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 9.1923F * h10);
        z[0] = (cz - 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 12.557F * h10);
        z[1] = (cz - 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 12.557F * h10);
        z[2] = (cz - 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 9.1923F * h10);
        z[3] = (cz - 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 9.1923F * h10);
        z[0] = (cz - 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 3.3646F * h10);
        z[1] = (cz - 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 3.3646F * h10);
        z[2] = (cz - 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 9.1923F * h10);
        z[3] = (cz - 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 3.3646F * h10);
        z[0] = (cz - 12.557F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 3.3646F * h10);
        z[1] = (cz - 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 3.3646F * h10);
        z[2] = (cz - 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 3.3646F * h10);
        z[3] = (cz - 12.557F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 9.1923F * h10);
        z[0] = (cz - 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 3.3646F * h10);
        z[1] = (cz - 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 3.3646F * h10);
        z[2] = (cz - 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 9.1923F * h10);
        z[3] = (cz - 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 9.1923F * h10);
        z[0] = (cz - 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 12.557F * h10);
        z[1] = (cz - 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 12.557F * h10);
        z[2] = (cz - 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 9.1923F * h10);
        z[3] = (cz - 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 12.557F * h10);
        z[0] = (cz - 3.3646F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 12.557F * h10);
        z[1] = (cz + 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 12.557F * h10);
        z[2] = (cz + 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 12.557F * h10);
        z[3] = (cz - 3.3646F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 9.1923F * h10);
        z[0] = (cz + 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 12.557F * h10);
        z[1] = (cz + 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 12.557F * h10);
        z[2] = (cz + 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 9.1923F * h10);
        z[3] = (cz + 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 9.1923F * h10);
        z[0] = (cz + 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy + 3.3646F * h10);
        z[1] = (cz + 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy + 3.3646F * h10);
        z[2] = (cz + 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 9.1923F * h10);
        z[3] = (cz + 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy + 3.3646F * h10);
        z[0] = (cz + 12.557F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 3.3646F * h10);
        z[1] = (cz + 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 3.3646F * h10);
        z[2] = (cz + 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy + 3.3646F * h10);
        z[3] = (cz + 12.557F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 9.1923F * h10);
        z[0] = (cz + 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 3.3646F * h10);
        z[1] = (cz + 12.557F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 3.3646F * h10);
        z[2] = (cz + 12.557F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 9.1923F * h10);
        z[3] = (cz + 9.1923F * h10);
        AddPoly(x, z, y, 4, color);
        x[0] = (cx - 4.0F * w10);
        y[0] = (cy - 9.1923F * h10);
        z[0] = (cz + 9.1923F * h10);
        x[1] = (cx - 4.0F * w10);
        y[1] = (cy - 12.557F * h10);
        z[1] = (cz + 3.3646F * h10);
        x[2] = (cx + 4.0F * w10);
        y[2] = (cy - 12.557F * h10);
        z[2] = (cz + 3.3646F * h10);
        x[3] = (cx + 4.0F * w10);
        y[3] = (cy - 9.1923F * h10);
        z[3] = (cz + 9.1923F * h10);
        AddPoly(x, z, y, 4, color);

        return;

        void AddPoly(Span<float> x, Span<float> z, Span<float> y, int n, Span<int> c, bool noOutline = false)
        {
            var verts = new Vector3[n];
            for (var vi = 0; vi < n; vi++)
            {
                verts[vi] = new Vector3(x[vi], y[vi], z[vi]);
            }
            planes.Add(new Rad3dPoly(
                new Color3((short)c[0], (short)c[1], (short)c[2]),
                null,
                PolyType.Flat,
                noOutline ? null : LineType.Flat,
                verts
            ));
        }
    }
}