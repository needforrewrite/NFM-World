using NFMWorld.Util;

namespace NFMWorld.Mad;

internal class Wheels
{
    private float _depth = 3.0F;
    internal int Ground;
    internal int Mast = 0;

    private readonly int[] _rc =
    {
        120, 120, 120
    };

    private float _size = 2.0F;
    internal int Sparkat;

    public Wheels()
    {
        Sparkat = 0;
        Ground = 0;
    }

    internal void Make(UnlimitedArray<Plane> planes, int npl, int cx, int cy, int cz, int rotates, int w, int h, int gwgr)
    {
        var x = new int[20];
        var y = new int[20];
        var z = new int[20];
        int[] c = [45, 45, 45];
        var i14 = 0;
        var wdiv = w / 10.0F;
        var hdiv = h / 10.0F;
        if (rotates == 11)
        {
            i14 = (int) (cx + 4.0F * wdiv);
        }
        Sparkat = (int) (hdiv * 24.0F);
        Ground = (int) (cy + 13.0F * hdiv);
        var xinv = -1;
        if (cx < 0)
        {
            xinv = 1;
        }
        for (var i17 = 0; i17 < 20; i17++)
        {
            x[i17] = (int) (cx - 4.0F * wdiv);
        }
        y[0] = (int) (cy - 9.1923F * hdiv);
        z[0] = (int) (cz + 9.1923F * hdiv);
        y[1] = (int) (cy - 12.557F * hdiv);
        z[1] = (int) (cz + 3.3646F * hdiv);
        y[2] = (int) (cy - 12.557F * hdiv);
        z[2] = (int) (cz - 3.3646F * hdiv);
        y[3] = (int) (cy - 9.1923F * hdiv);
        z[3] = (int) (cz - 9.1923F * hdiv);
        y[4] = (int) (cy - 3.3646F * hdiv);
        z[4] = (int) (cz - 12.557F * hdiv);
        y[5] = (int) (cy + 3.3646F * hdiv);
        z[5] = (int) (cz - 12.557F * hdiv);
        y[6] = (int) (cy + 9.1923F * hdiv);
        z[6] = (int) (cz - 9.1923F * hdiv);
        y[7] = (int) (cy + 12.557F * hdiv);
        z[7] = (int) (cz - 3.3646F * hdiv);
        y[8] = (int) (cy + 12.557F * hdiv);
        z[8] = (int) (cz + 3.3646F * hdiv);
        y[9] = (int) (cy + 9.1923F * hdiv);
        z[9] = (int) (cz + 9.1923F * hdiv);
        y[10] = (int) (cy + 3.3646F * hdiv);
        z[10] = (int) (cz + 12.557F * hdiv);
        y[11] = (int) (cy - 3.3646F * hdiv);
        z[11] = (int) (cz + 12.557F * hdiv);
        y[12] = cy;
        z[12] = (int) (cz + 10.0F * _size);
        y[13] = (int) (cy + 8.66 * _size);
        z[13] = (int) (cz + 5.0F * _size);
        y[14] = (int) (cy + 8.66 * _size);
        z[14] = (int) (cz - 5.0F * _size);
        y[15] = cy;
        z[15] = (int) (cz - 10.0F * _size);
        y[16] = (int) (cy - 8.66 * _size);
        z[16] = (int) (cz - 5.0F * _size);
        y[17] = (int) (cy - 8.66 * _size);
        z[17] = (int) (cz + 5.0F * _size);
        y[18] = cy;
        z[18] = (int) (cz + 10.0F * _size);
        y[19] = (int) (cy - 3.3646F * hdiv);
        z[19] = (int) (cz + 12.557F * hdiv);
        planes[npl] = new Plane(x, z, y, 20, c, 0, gwgr, 0, i14, cy, cz, 7, 0, false, 0, false)
        {
            Master = 1
        };
        npl++;
        x[2] = (int) (cx - _depth * wdiv);
        y[2] = cy;
        z[2] = cz;
        var i18 = (int) (gwgr - _depth / _size * 4.0F);
        if (i18 < -16)
        {
            i18 = -16;
        }
        y[0] = cy;
        z[0] = (int) (cz + 10.0F * _size);
        y[1] = (int) (cy + 8.66 * _size);
        z[1] = (int) (cz + 5.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        y[0] = (int) (cy + 8.66 * _size);
        z[0] = (int) (cz + 5.0F * _size);
        y[1] = (int) (cy + 8.66 * _size);
        z[1] = (int) (cz - 5.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        y[0] = (int) (cy + 8.66 * _size);
        z[0] = (int) (cz - 5.0F * _size);
        y[1] = cy;
        z[1] = (int) (cz - 10.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        y[0] = cy;
        z[0] = (int) (cz - 10.0F * _size);
        y[1] = (int) (cy - 8.66 * _size);
        z[1] = (int) (cz - 5.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        y[0] = (int) (cy - 8.66 * _size);
        z[0] = (int) (cz - 5.0F * _size);
        y[1] = (int) (cy - 8.66 * _size);
        z[1] = (int) (cz + 5.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        y[0] = (int) (cy - 8.66 * _size);
        z[0] = (int) (cz + 5.0F * _size);
        y[1] = cy;
        z[1] = (int) (cz + 10.0F * _size);
        planes[npl] = new Plane(x, z, y, 3, _rc, 0, i18, 0, i14, cy, cz, 7, 0, false, 0, false);
        if (_depth / _size < 7.0F)
        {
            planes[npl].Master = 2;
        }
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 12.557F * hdiv);
        z[0] = (int) (cz + 3.3646F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 12.557F * hdiv);
        z[1] = (int) (cz - 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 12.557F * hdiv);
        z[2] = (int) (cz - 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 12.557F * hdiv);
        z[3] = (int) (cz + 3.3646F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, -1 * xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 9.1923F * hdiv);
        z[0] = (int) (cz - 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 12.557F * hdiv);
        z[1] = (int) (cz - 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 12.557F * hdiv);
        z[2] = (int) (cz - 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 9.1923F * hdiv);
        z[3] = (int) (cz - 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 9.1923F * hdiv);
        z[0] = (int) (cz - 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 3.3646F * hdiv);
        z[1] = (int) (cz - 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 3.3646F * hdiv);
        z[2] = (int) (cz - 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 9.1923F * hdiv);
        z[3] = (int) (cz - 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 3.3646F * hdiv);
        z[0] = (int) (cz - 12.557F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 3.3646F * hdiv);
        z[1] = (int) (cz - 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 3.3646F * hdiv);
        z[2] = (int) (cz - 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 3.3646F * hdiv);
        z[3] = (int) (cz - 12.557F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, -1 * xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 9.1923F * hdiv);
        z[0] = (int) (cz - 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 3.3646F * hdiv);
        z[1] = (int) (cz - 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 3.3646F * hdiv);
        z[2] = (int) (cz - 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 9.1923F * hdiv);
        z[3] = (int) (cz - 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 9.1923F * hdiv);
        z[0] = (int) (cz - 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 12.557F * hdiv);
        z[1] = (int) (cz - 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 12.557F * hdiv);
        z[2] = (int) (cz - 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 9.1923F * hdiv);
        z[3] = (int) (cz - 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 12.557F * hdiv);
        z[0] = (int) (cz - 3.3646F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 12.557F * hdiv);
        z[1] = (int) (cz + 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 12.557F * hdiv);
        z[2] = (int) (cz + 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 12.557F * hdiv);
        z[3] = (int) (cz - 3.3646F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, -1 * xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 9.1923F * hdiv);
        z[0] = (int) (cz + 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 12.557F * hdiv);
        z[1] = (int) (cz + 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 12.557F * hdiv);
        z[2] = (int) (cz + 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 9.1923F * hdiv);
        z[3] = (int) (cz + 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 9.1923F * hdiv);
        z[0] = (int) (cz + 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy + 3.3646F * hdiv);
        z[1] = (int) (cz + 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy + 3.3646F * hdiv);
        z[2] = (int) (cz + 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 9.1923F * hdiv);
        z[3] = (int) (cz + 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy + 3.3646F * hdiv);
        z[0] = (int) (cz + 12.557F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 3.3646F * hdiv);
        z[1] = (int) (cz + 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 3.3646F * hdiv);
        z[2] = (int) (cz + 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy + 3.3646F * hdiv);
        z[3] = (int) (cz + 12.557F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, -1 * xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 9.1923F * hdiv);
        z[0] = (int) (cz + 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 3.3646F * hdiv);
        z[1] = (int) (cz + 12.557F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 3.3646F * hdiv);
        z[2] = (int) (cz + 12.557F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 9.1923F * hdiv);
        z[3] = (int) (cz + 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
        x[0] = (int) (cx - 4.0F * wdiv);
        y[0] = (int) (cy - 9.1923F * hdiv);
        z[0] = (int) (cz + 9.1923F * hdiv);
        x[1] = (int) (cx - 4.0F * wdiv);
        y[1] = (int) (cy - 12.557F * hdiv);
        z[1] = (int) (cz + 3.3646F * hdiv);
        x[2] = (int) (cx + 4.0F * wdiv);
        y[2] = (int) (cy - 12.557F * hdiv);
        z[2] = (int) (cz + 3.3646F * hdiv);
        x[3] = (int) (cx + 4.0F * wdiv);
        y[3] = (int) (cy - 9.1923F * hdiv);
        z[3] = (int) (cz + 9.1923F * hdiv);
        planes[npl] = new Plane(x, z, y, 4, c, 0, gwgr, xinv, i14, cy, cz, 7, 0, false, 0, true);
        npl++;
    }

    internal void Setrims(int i, int i0, int i1, int i2, int i3)
    {
        _rc[0] = i;
        _rc[1] = i0;
        _rc[2] = i1;
        _size = i2 / 10.0F;
        if (_size < 0.0F)
        {
            _size = 0.0F;
        }
        _depth = i3 / 10.0F;
        if (_depth / _size > 41.0F)
        {
            _depth = _size * 41.0F;
        }
        if (_depth / _size < -25.0F)
        {
            _depth = -(_size * 25.0F);
        }
    }
}