using System.Runtime.CompilerServices;
using NFMWorld.Util;
using NFMWorld.Util;
using Random = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

class Plane : IComparable<Plane>
{
    private int _av;
    internal int Bfase;
    internal readonly int[] C = new int[3];
    internal int Chip;
    internal int Colnum = 0;
    private readonly int[] _cox = new int[3];
    private readonly int[] _coy = new int[3];
    private readonly int[] _coz = new int[3];
    internal float Ctmag;
    private SinCosFloat _cxy;
    private SinCosFloat _cxz;
    private SinCosFloat _czy;
    private float _deltaf = 1.0F;
    private int _disline = 7;
    private int _dx;
    private int _dy;
    private int _dz;
    internal int Embos;
    internal int Flx;
    internal int Fs;
    internal int Glass;
    internal int Gr;
    internal readonly float[] HSB = new float[3];
    internal int Light;
    internal int Master = 0;
    internal int N;
    internal bool Nocol;
    internal readonly int[] Oc = new int[3];
    internal readonly int[] Ox;
    internal readonly int[] Oy;
    internal readonly int[] Oz;
    private int _pa;
    private int _pb;
    private float _projf = 1.0F;
    internal bool Road;
    internal bool Solo;
    private int _typ;
    private int _vx;
    private int _vy;
    private int _vz;
    internal int Wx;
    internal int Wy;
    internal int Wz;

    internal sbyte Project;

    internal Plane(ReadOnlySpan<int> x, ReadOnlySpan<int> z, ReadOnlySpan<int> y, int n, int[] is2, int i3, int i4, int i5, int i6, int i7,
        int i8, int i9, int i10, bool abool, int i11, bool bool12)
    {
        N = n;
        Ox = new int[N];
        Oz = new int[N];
        Oy = new int[N];
        for (var i13 = 0; i13 < N; i13++)
        {
            Ox[i13] = x[i13];
            Oy[i13] = y[i13];
            Oz[i13] = z[i13];
        }
        Array.Copy(is2, 0, Oc, 0, 3);
        if (i4 == -15)
        {
            if (is2[0] == 211)
            {
                var i15 = (int) (Random.Double() * 40.0 - 20.0);
                var i16 = (int) (Random.Double() * 40.0 - 20.0);
                for (var i17 = 0; i17 < N; i17++)
                {
                    Ox[i17] += i15;
                    Oz[i17] += i16;
                }
            }
            var i18 = (int) (185.0 + Random.Double() * 20.0);
            is2[0] = (217 + i18) / 2;
            if (is2[0] == 211)
            {
                is2[0] = 210;
            }
            is2[1] = (189 + i18) / 2;
            is2[2] = (132 + i18) / 2;
            for (var i19 = 0; i19 < N; i19++)
            {
                if (Random.Double() > Random.Double())
                {
                    Ox[i19] += (int) (8.0 * Random.Double() - 4.0);
                }
                if (Random.Double() > Random.Double())
                {
                    Oy[i19] += (int) (8.0 * Random.Double() - 4.0);
                }
                if (Random.Double() > Random.Double())
                {
                    Oz[i19] += (int) (8.0 * Random.Double() - 4.0);
                }
            }
        }
        if (is2[0] == is2[1] && is2[1] == is2[2])
        {
            Nocol = true;
        }
        if (i3 == 0)
        {
            for (var i20 = 0; i20 < 3; i20++)
            {
                C[i20] = (int) (is2[i20] + is2[i20] * (Medium.Snap[i20] / 100.0F));
                if (C[i20] > 255)
                {
                    C[i20] = 255;
                }
                if (C[i20] < 0)
                {
                    C[i20] = 0;
                }
            }
        }
        if (i3 == 1)
        {
            for (var i21 = 0; i21 < 3; i21++)
            {
                C[i21] = (Medium.Csky[i21] * Medium.Fade[0] * 2 + Medium.Cfade[i21] * 3000) /
                         (Medium.Fade[0] * 2 + 3000);
            }
        }
        if (i3 == 2)
        {
            for (var i22 = 0; i22 < 3; i22++)
            {
                C[i22] = (int) (Medium.Crgrnd[i22] * 0.925F);
            }
        }
        if (i3 == 3)
        {
            Array.Copy(is2, 0, C, 0, 3);
        }
        _disline = i9;
        Bfase = i10;
        Glass = i3;
        Colors.RGBtoHSB(C[0], C[1], C[2], out HSB[0], out HSB[1], out HSB[2]);
        if (i3 == 3 && Medium.Trk != 2)
        {
            HSB[1] += 0.05F;
            if (HSB[1] > 1.0F)
            {
                HSB[1] = 1.0F;
            }
        }
        if (!Nocol && Glass != 1)
        {
            if (Bfase > 20 && HSB[1] > 0.25)
            {
                HSB[1] = 0.25F;
            }
            if (Bfase > 25 && HSB[2] > 0.7)
            {
                HSB[2] = 0.7F;
            }
            if (Bfase > 30 && HSB[1] > 0.15)
            {
                HSB[1] = 0.15F;
            }
            if (Bfase > 35 && HSB[2] > 0.6)
            {
                HSB[2] = 0.6F;
            }
            if (Bfase > 40)
            {
                HSB[0] = 0.075F;
            }
            if (Bfase > 50 && HSB[2] > 0.5)
            {
                HSB[2] = 0.5F;
            }
            if (Bfase > 60)
            {
                HSB[0] = 0.05F;
            }
        }
        Road = abool;
        Light = i11;
        Solo = bool12;
        Gr = i4;
        if (Gr == -1337)
        {
            Project = -1;
            Gr = 0;
        }
        else if (Gr == 1337)
        {
            Project = 1;
            Gr = 0;
        }
        Fs = i5;
        Wx = i6;
        Wy = i7;
        Wz = i8;
        Deltafntyp();
    }

    internal void D(Plane last, Plane? next, int mx, int my, int mz, SinCosFloat xz, SinCosFloat xy, SinCosFloat yz, SinCosFloat wxz, SinCosFloat wzy, bool lowZ, int i36)
    {
        // cache fields for the hot path
        var trk = Medium.Trk;
        var gr = Gr;
        ReadOnlySpan<int> ox = Ox;
        ReadOnlySpan<int> oy = Oy;
        ReadOnlySpan<int> oz = Oz;

        // Dont render rims if far away and not car select
        if (Master == 1)
        {
            if (_av > 1500 && !Medium.Crs)
            {
                N = 12;
            }
            else
            {
                N = 20;
            }
        }

        var n = N;
        Span<int> x = stackalloc int[n];
        Span<int> z = stackalloc int[n];
        Span<int> y = stackalloc int[n];
        if (Embos == 0)
        {
            if (gr is -11 or -12 or -13 && Medium.Lastmaf == 1)
            {
                for (var i = 0; i < n; i++)
                {
                    x[i] = -ox[i] + mx;
                    y[i] = oy[i] + my;
                    z[i] = -oz[i] + mz;
                }
            }
            else
            {
                for (var i = 0; i < n; i++)
                {
                    x[i] = ox[i] + mx;
                    y[i] = oy[i] + my;
                    z[i] = oz[i] + mz;
                }
            }
        }
        else
        {
            RenderFlame(mx, my, mz, xz, xy, yz, x, y, z);
        }
        if (Wz != 0)
        {
            Rot(y, z, Wy + my, Wz + mz, wzy, n);
        }
        if (Wx != 0)
        {
            Rot(x, z, Wx + mx, Wz + mz, wxz, n);
        }
        if (Chip == 1 && (Medium.Random() > 0.6 || Bfase == 0))
        {
            Chip = 0;
            if (Bfase == 0 && Nocol)
            {
                Bfase = 1;
            }
        }
        if (Chip != 0)
        {
            RenderChip(mx, my, mz, xz, xy, yz);
        }
        Rot(x, y, mx, my, xy, n);
        Rot(y, z, my, mz, yz, n);
        Rot(x, z, mx, mz, xz, n);
        if ((xy != 0 || yz != 0 || xz != 0) && trk != 2)
        {
            _projf = 1.0F;
            for (var i = 0; i < 3; i++)
            {
                for (var j = 0; j < 3; j++)
                {
                    if (j != i)
                    {
                        _projf *= (float) (float.Sqrt((x[i] - x[j]) * (x[i] - x[j]) + (z[i] - z[j]) * (z[i] - z[j])) / 100.0);
                    }
                }
            }
            _projf /= 3.0F;
        }
        Rot(x, z, Medium.Cx, Medium.Cz, Medium.Xz, n);
        var bool72 = false;
        Span<int> is73 = stackalloc int[n];
        Span<int> is74 = stackalloc int[n];
        var i75 = 500;
        for (var i = 0; i < n; i++)
        {
            is73[i] = Xs(x[i], z[i]);
            is74[i] = Ys(y[i], z[i]);
        }
        var i77 = 0;
        var i78 = 1;
        for (var i79 = 0; i79 < n; i79++)
        {
            for (var i80 = i79; i80 < n; i80++)
            {
                if (i79 != i80 && Math.Abs(is73[i79] - is73[i80]) - Math.Abs(is74[i79] - is74[i80]) < i75)
                {
                    i78 = i79;
                    i77 = i80;
                    i75 = Math.Abs(is73[i79] - is73[i80]) - Math.Abs(is74[i79] - is74[i80]);
                }
            }
        }
        if (is74[i77] < is74[i78])
        {
            (i77, i78) = (i78, i77);
        }
        if (Spy(x[i77], z[i77]) > Spy(x[i78], z[i78]))
        {
            bool72 = true;
            var i82 = 0;
            for (var i = 0; i < n; i++)
            {
                if (z[i] < 50 && y[i] > Medium.Cy)
                {
                    bool72 = false;
                }
                else if (y[i] == y[0])
                {
                    i82++;
                }
            }

            if (i82 == n && y[0] > Medium.Cy)
            {
                bool72 = false;
            }
        }
        Rot(y, z, Medium.Cy, Medium.Cz, Medium.Zy, n);
        Span<int> xScreen = stackalloc int[n];
        Span<int> yScreen = stackalloc int[n];
        var offscreenPointsNegY = 0;
        var offscreenPointsPosY = 0;
        var offscreenPointsNegX = 0;
        var offscreenPointsPosX = 0;
        var lowZPoints = 0;
        for (var i = 0; i < n; i++)
        {
            xScreen[i] = Xs(x[i], z[i]);
            yScreen[i] = Ys(y[i], z[i]);
            if (yScreen[i] < Medium.Ih || z[i] < 10)
            {
                offscreenPointsNegY++;
            }
            if (yScreen[i] > Medium.H || z[i] < 10)
            {
                offscreenPointsPosY++;
            }
            if (xScreen[i] < Medium.Iw || z[i] < 10)
            {
                offscreenPointsNegX++;
            }
            if (xScreen[i] > Medium.W || z[i] < 10)
            {
                offscreenPointsPosX++;
            }
            if (z[i] < 10)
            {
                lowZPoints++;
            }
        }
        if (offscreenPointsNegX == n || offscreenPointsNegY == n || offscreenPointsPosY == n || offscreenPointsPosX == n)
        {
            // All points are outside the screen
            return;
        }
        if (trk is 1 or 4 && (offscreenPointsNegX != 0 || offscreenPointsNegY != 0 || offscreenPointsPosY != 0 || offscreenPointsPosX != 0))
        {
            // Some points are outside the screen
            return;
        }
        if (trk == 3 && lowZPoints != 0)
        {
            // Something is too close to the screen or behind the screen
            return;
        }
        if (lowZPoints != 0)
        {
            lowZ = true;
        }
        if (i36 != -1)
        {
            var i93 = 0;
            var i94 = 0;
            for (var i = 0; i < n; i++)
            {
                for (var j = i + 1; j < n; j++)
                {
                    var abs = Math.Abs(xScreen[i] - xScreen[j]);
                    if (abs > i93)
                    {
                        i93 = abs;
                    }

                    var abs2 = Math.Abs(yScreen[i] - yScreen[j]);
                    if (abs2 > i94)
                    {
                        i94 = abs2;
                    }
                }
            }
            if (i93 == 0 || i94 == 0)
            {
                return;
            }

            if (i93 < 3 && i94 < 3 && (i36 / i93 > 15 && i36 / i94 > 15 || lowZ) && (!Medium.Lightson || Light == 0))
            {
                return;
            }
        }
        if (!GetShouldRender(i36, yScreen, xScreen, y, x, z, bool72, ref lowZ))
        {
            return;
        }

        var f = (_projf / _deltaf + 0.3f).Cap();

        if (lowZ && !Solo)
        {
            var bool113 = false;
            if (f > 1.0F)
            {
                if (f >= 1.27)
                {
                    bool113 = true;
                }
                f = 1.0F;
            }
            if (bool113)
            {
                f *= 0.89f;
            }
            else
            {
                f *= 0.86f;
            }
            if (f < 0.37)
            {
                f = 0.37F;
            }
            switch (gr)
            {
                case -9:
                    f = 0.7F;
                    break;
                case -4:
                    f = 0.74F;
                    break;
            }
            if (gr != -7 && trk == 0 && bool72)
            {
                f = 0.32F;
            }
            switch (gr)
            {
                case -8:
                case -14:
                case -15:
                    f = 1.0F;
                    break;
                case -11:
                case -12:
                    f = 0.6F;
                    if (i36 == -1)
                    {
                        f = Medium.Cpflik || Medium.Nochekflk && !Medium.Lastcheck ? 1.0F : 0.76F;
                    }

                    break;
                case -13 when i36 == -1:
                    f = Medium.Cpflik ? 0.0F : 0.76F;
                    break;
                case -6:
                    f = 0.62F;
                    break;
                case -5:
                    f = 0.55F;
                    break;
            }
        }
        else
        {
            if (f > 1.0F)
            {
                f = 1.0F;
            }
            if (f < 0.6 || bool72)
            {
                f = 0.6F;
            }
        }
        CalcColor(last, next, i36, f, bool72, out var r, out var g, out var b);
        G.SetColor(new Color(r, g, b));
        G.SetAntialiasing(false);
        G.FillPolygon(xScreen, yScreen, n);
        G.SetAntialiasing(true);
        if (trk != 0 && gr == -10)
        {
            lowZ = false;
        }
        if (!lowZ)
        {
            DrawPart(xScreen, yScreen);
        }
        else if (Road && _av <= 3000 && trk == 0 && Medium.Fade[0] > 4000)
        {
            r -= 10;
            if (r < 0)
            {
                r = 0;
            }
            g -= 10;
            if (g < 0)
            {
                g = 0;
            }
            b -= 10;
            if (b < 0)
            {
                b = 0;
            }
            G.SetColor(new Color(r, g, b));
            G.DrawPolygon(xScreen, yScreen, n);
        }
        if (gr == -10)
        {
            if (trk == 0)
            {
                r = C[0];
                g = C[1];
                b = C[2];
                if (i36 == -1 && Medium.Cpflik)
                {
                    r = (int) (r * 1.6);
                    if (r > 255)
                    {
                        r = 255;
                    }
                    g = (int) (g * 1.6);
                    if (g > 255)
                    {
                        g = 255;
                    }
                    b = (int) (b * 1.6);
                    if (b > 255)
                    {
                        b = 255;
                    }
                }
                for (var fadefrom = 0; fadefrom < 16; fadefrom++)
                {
                    if (_av > Medium.Fade[fadefrom])
                    {
                        r = (r * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                        g = (g * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                        b = (b * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                    }
                }

                G.SetColor(new Color(r, g, b));
                G.DrawPolygon(xScreen, yScreen, n);
            }
            else if (Medium.Cpflik && Medium.Hit == 5000)
            {
                g = (int) (Random.Single() * 115.0);
                r = g * 2 - 54;
                if (r < 0)
                {
                    r = 0;
                }
                if (r > 255)
                {
                    r = 255;
                }
                b = 202 + g * 2;
                if (b < 0)
                {
                    b = 0;
                }
                if (b > 255)
                {
                    b = 255;
                }
                g += 101;
                if (g < 0)
                {
                    g = 0;
                }
                if (g > 255)
                {
                    g = 255;
                }
                G.SetColor(new Color(r, g, b));
                G.DrawPolygon(xScreen, yScreen, n);
            }
        }

        if (gr != -18 || trk != 0)
        {
            return;
        }

        r = C[0];
        g = C[1];
        b = C[2];
        if (Medium.Cpflik && Medium.Elecr >= 0.0F)
        {
            r = (int) (25.5F * Medium.Elecr);
            if (r > 255)
            {
                r = 255;
            }
            g = (int) (128.0F + 12.8F * Medium.Elecr);
            if (g > 255)
            {
                g = 255;
            }
            b = 255;
        }
        for (var fadefrom = 0; fadefrom < 16; fadefrom++)
        {
            if (_av <= Medium.Fade[fadefrom]) continue;
            r = (r * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
            g = (g * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
            b = (b * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
        }

        G.SetColor(new Color(r, g, b));
        G.DrawPolygon(xScreen, yScreen, n);
    }

    private bool GetShouldRender(int i36, Span<int> yScreen, Span<int> xScreen, Span<int> y, Span<int> x, Span<int> z, bool bool72, ref bool lowZ)
    {
        var fs = 1;
        var gr = Gr;
        if (gr is < 0 and >= -15)
        {
            gr = 0;
        }
        switch (Gr)
        {
            case -11:
                gr = -90;
                break;
            case -12:
                gr = -75;
                break;
            case -14:
            case -15:
                gr = -50;
                break;
        }
        if (Glass == 2)
        {
            gr = 200;
        }
        if (Fs != 0)
        {
            int i101;
            int i102;
            if (Math.Abs(yScreen[0] - yScreen[1]) > Math.Abs(yScreen[2] - yScreen[1]))
            {
                i101 = 0;
                i102 = 2;
            }
            else
            {
                i101 = 2;
                i102 = 0;
                fs *= -1;
            }
            if (yScreen[1] > yScreen[i101])
            {
                fs *= -1;
            }
            if (xScreen[1] > xScreen[i102])
            {
                fs *= -1;
            }
            if (Fs != 22)
            {
                fs *= Fs;
                if (fs == -1)
                {
                    gr += 40;
                    fs = -111;
                }
            }
        }
        if (Medium.Lightson && Light == 2)
        {
            gr -= 40;
        }
        var maxY = y[0];
        var minY = y[0];
        var maxX = x[0];
        var minX = x[0];
        var maxZ = z[0];
        var minZ = z[0];
        for (var i = 1; i < N; i++)
        {
            if (y[i] > maxY)
            {
                maxY = y[i];
            }
            if (y[i] < minY)
            {
                minY = y[i];
            }
            if (x[i] > maxX)
            {
                maxX = x[i];
            }
            if (x[i] < minX)
            {
                minX = x[i];
            }
            if (z[i] > maxZ)
            {
                maxZ = z[i];
            }
            if (z[i] < minZ)
            {
                minZ = z[i];
            }
        }
        var centerY = (maxY + minY) / 2;
        var centerX = (maxX + minX) / 2;
        var centerZ = (maxZ + minZ) / 2;
        _av = (int) float.Sqrt((Medium.Cy - centerY) * (Medium.Cy - centerY) + (Medium.Cx - centerX) * (Medium.Cx - centerX) + centerZ * centerZ + gr * gr * gr);
        if (Fs == 22 && _av < 11200)
        {
            Medium.Lastmaf = fs;
        }
        if (Medium.Trk == 0 && (_av > Medium.Fade[_disline] || _av == 0))
        {
            return false;
        }
        if (fs == -111 && _av > 4500 && !Road)
        {
            return false;
        }
        if (Gr == -13 && (!Medium.Lastcheck || i36 != -1))
        {
            return false;
        }
        if (Master == 2 && _av > 1500 && !Medium.Crs)
        {
            return false;
        }
        if (Gr is -14 or -15 or -12 && (_av > 11000 || bool72 || fs == -111 || Medium.Resdown == 2) && Medium.Trk != 2 && Medium.Trk != 3)
        {
            return false;
        }
        if (Gr == -11 && _av > 11000 && Medium.Trk != 2 && Medium.Trk != 3)
        {
            return false;
        }
        if (Glass == 2 && (Medium.Trk != 0 || _av > 6700))
        {
            return false;
        }
        if (Flx != 0 && Medium.Random() > 0.3 && Flx != 77)
        {
            return false;
        }
        if (fs == -111 && _av > 1500)
        {
            lowZ = true;
        }
        if (_av > 3000 && Medium.Adv <= 900)
        {
            lowZ = true;
        }

        return true;
    }

    private void CalcColor(Plane last, Plane? next, int i36, float brightness, bool bool72, out int r, out int g, out int b)
    {
        if (Project == -1)
        {
            brightness = (float) (last._projf / last._deltaf + 0.3);

            if (brightness > 1.0F)
            {
                brightness = 1.0F;
            }
            if (brightness < 0.6 || bool72)
            {
                //yeah its referencing OUR bool72, i dunno man...
                brightness = 0.6F;
            }
        }
        else if (Project == 1 && next != null)
        {
            brightness = (float) (next._projf / next._deltaf + 0.3);

            if (brightness > 1.0F)
            {
                brightness = 1.0F;
            }
            if (brightness < 0.6 || bool72)
            {
                //yeah its referencing OUR bool72, i dunno man...
                brightness = 0.6F;
            }
        }
        var color = Color.GetHSBColor(HSB[0], HSB[1], HSB[2] * brightness);
        switch (Medium.Trk)
        {
            case 1:
            {
                Color.RGBtoHSB(Oc[0], Oc[1], Oc[2], out var hu, out var sa, out var br);
                hu = 0.15F;
                sa = 0.3F;
                color = Color.GetHSBColor(hu, sa, br * brightness + 0.0F);
                break;
            }
            case 3:
            {
                Color.RGBtoHSB(Oc[0], Oc[1], Oc[2], out var hu, out var sa, out var br);
                hu = 0.6F;
                sa = 0.14F;
                color = Color.GetHSBColor(hu, sa, br * brightness + 0.0F);
                break;
            }
        }

        r = color.R;
        g = color.G;
        b = color.B;

        if (Medium.Lightson && (Light != 0 || (Gr == -11 || Gr == -12) && i36 == -1))
        {
            r = Oc[0];
            if (r > 255)
            {
                r = 255;
            }
            if (r < 0)
            {
                r = 0;
            }
            g = Oc[1];
            if (g > 255)
            {
                g = 255;
            }
            if (g < 0)
            {
                g = 0;
            }
            b = Oc[2];
            if (b > 255)
            {
                b = 255;
            }
            if (b < 0)
            {
                b = 0;
            }
        }
        if (Medium.Trk == 0)
        {
            for (var fadefrom = 15; fadefrom >= 0; fadefrom--)
            {
                if (_av > Medium.Fade[fadefrom])
                {
                    r = (r * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                    g = (g * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                    b = (b * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                    break;
                }
            }
        }
    }

    private void DrawPart(Span<int> is85, Span<int> is86)
    {
        int i114;
        int i115;
        int i116;
        if (Flx == 0)
        {
            if (!Solo)
            {
                i114 = 0;
                i115 = 0;
                i116 = 0;
/*
                        if (false) {
                            i114 = (int) (Random.Double() * 255);
                            i115 = (int) (Random.Double() * 255);
                            i116 = (int) (Random.Double() * 255);
                        }
*/
                if (Medium.Lightson && Light != 0)
                {
                    i114 = Oc[0] / 2;
                    if (i114 > 255)
                    {
                        i114 = 255;
                    }
                    if (i114 < 0)
                    {
                        i114 = 0;
                    }
                    i115 = Oc[1] / 2;
                    if (i115 > 255)
                    {
                        i115 = 255;
                    }
                    if (i115 < 0)
                    {
                        i115 = 0;
                    }
                    i116 = Oc[2] / 2;
                    if (i116 > 255)
                    {
                        i116 = 255;
                    }
                    if (i116 < 0)
                    {
                        i116 = 0;
                    }
                }
                G.SetColor(new Color(i114, i115, i116));
                G.DrawPolygon(is85, is86, N);
            }
        }
        else
        {
            if (Flx == 2)
            {
                G.SetColor(new Color(0, 0, 0));
                G.DrawPolygon(is85, is86, N);
            }
            if (Flx == 1)
            {
                i114 = 0;
                i115 = (int) (223.0F + 223.0F * (Medium.Snap[1] / 100.0F));
                if (i115 > 255)
                {
                    i115 = 255;
                }
                if (i115 < 0)
                {
                    i115 = 0;
                }
                i116 = (int) (255.0F + 255.0F * (Medium.Snap[2] / 100.0F));
                if (i116 > 255)
                {
                    i116 = 255;
                }
                if (i116 < 0)
                {
                    i116 = 0;
                }
                G.SetColor(new Color(i114, i115, i116));
                G.DrawPolygon(is85, is86, N);
                Flx = 2;
            }
            if (Flx == 3)
            {
                i114 = 0;
                i115 = (int) (255.0F + 255.0F * (Medium.Snap[1] / 100.0F));
                if (i115 > 255)
                {
                    i115 = 255;
                }
                if (i115 < 0)
                {
                    i115 = 0;
                }
                i116 = (int) (223.0F + 223.0F * (Medium.Snap[2] / 100.0F));
                if (i116 > 255)
                {
                    i116 = 255;
                }
                if (i116 < 0)
                {
                    i116 = 0;
                }
                G.SetColor(new Color(i114, i115, i116));
                G.DrawPolygon(is85, is86, N);
                Flx = 2;
            }
            if (Flx == 77)
            {
                G.SetColor(new Color(16, 198, 255));
                G.DrawPolygon(is85, is86, N);
                Flx = 0;
            }
        }
    }

    private void RenderChip(int mx, int my, int mz, float xz, float xy, float yz)
    {
        if (Chip == 1)
        {
            _cxz = xz;
            _cxy = xy;
            _czy = yz;
            var i60 = (int) (Medium.Random() * N);
            _cox[0] = Ox[i60];
            _coz[0] = Oz[i60];
            _coy[0] = Oy[i60];
            if (Ctmag > 3.0F)
            {
                Ctmag = 3.0F;
            }
            if (Ctmag < -3.0F)
            {
                Ctmag = -3.0F;
            }
            _cox[1] = (int) (_cox[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _cox[2] = (int) (_cox[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _coy[1] = (int) (_coy[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _coy[2] = (int) (_coy[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _coz[1] = (int) (_coz[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _coz[2] = (int) (_coz[0] + Ctmag * (10.0F - Medium.Random() * 20.0F));
            _dx = 0;
            _dy = 0;
            _dz = 0;
            if (Bfase != -7)
            {
                _vx = (int) (Ctmag * (30.0F - Medium.Random() * 60.0F));
                _vz = (int) (Ctmag * (30.0F - Medium.Random() * 60.0F));
                _vy = (int) (Ctmag * (30.0F - Medium.Random() * 60.0F));
            }
            else
            {
                _vx = (int) (Ctmag * (10.0F - Medium.Random() * 20.0F));
                _vz = (int) (Ctmag * (10.0F - Medium.Random() * 20.0F));
                _vy = (int) (Ctmag * (10.0F - Medium.Random() * 20.0F));
            }
            Chip = 2;
        }
        var is61 = new int[3];
        var is62 = new int[3];
        var is63 = new int[3];
        for (var i64 = 0; i64 < 3; i64++)
        {
            is61[i64] = _cox[i64] + mx;
            is63[i64] = _coy[i64] + my;
            is62[i64] = _coz[i64] + mz;
        }
        Rot(is61, is63, mx, my, _cxy, 3);
        Rot(is63, is62, my, mz, _czy, 3);
        Rot(is61, is62, mx, mz, _cxz, 3);
        for (var i65 = 0; i65 < 3; i65++)
        {
            is61[i65] += _dx;
            is63[i65] += _dy;
            is62[i65] += _dz;
        }
        _dx += _vx;
        _dz += _vz;
        _dy += _vy;
        _vy += 7;
        if (is63[0] > Medium.Ground)
        {
            Chip = 19;
        }
        Rot(is61, is62, Medium.Cx, Medium.Cz, Medium.Xz, 3);
        Rot(is63, is62, Medium.Cy, Medium.Cz, Medium.Zy, 3);
        var is66 = new int[3];
        var is67 = new int[3];
        for (var i68 = 0; i68 < 3; i68++)
        {
            is66[i68] = Xs(is61[i68], is62[i68]);
            is67[i68] = Ys(is63[i68], is62[i68]);
        }
        var i69 = (int) (Medium.Random() * 3.0F);
        if (Bfase != -7)
        {
            if (i69 == 0)
            {
                G.SetColor(new Color(C[0], C[1], C[2]).Darker());
            }
            if (i69 == 1)
            {
                G.SetColor(new Color(C[0], C[1], C[2]));
            }
            if (i69 == 2)
            {
                G.SetColor(new Color(C[0], C[1], C[2]).Brighter());
            }
        }
        else
        {
            G.SetColor(Color.GetHSBColor(HSB[0], HSB[1], HSB[2]));
        }
        G.FillPolygon(is66, is67, 3);
        Chip++;
        if (Chip == 20)
        {
            Chip = 0;
        }
    }

    private void RenderFlame(int mx, int my, int mz, float xz, float xy, float yz, Span<int> x, Span<int> y, Span<int> z)
    {
        if (Embos <= 11 && Medium.Random() > 0.5 && Glass != 1)
        {
            for (var i41 = 0; i41 < N; i41++)
            {
                x[i41] = (int) (Ox[i41] + mx + (15.0F - Medium.Random() * 30.0F));
                y[i41] = (int) (Oy[i41] + my + (15.0F - Medium.Random() * 30.0F));
                z[i41] = (int) (Oz[i41] + mz + (15.0F - Medium.Random() * 30.0F));
            }
            Rot(x, y, mx, my, xy, N);
            Rot(y, z, my, mz, yz, N);
            Rot(x, z, mx, mz, xz, N);
            Rot(x, z, Medium.Cx, Medium.Cz, Medium.Xz, N);
            Rot(y, z, Medium.Cy, Medium.Cz, Medium.Zy, N);
            var is42 = new int[N];
            var is43 = new int[N];
            for (var i44 = 0; i44 < N; i44++)
            {
                is42[i44] = Xs(x[i44], z[i44]);
                is43[i44] = Ys(y[i44], z[i44]);
            }
            G.SetColor(new Color(230, 230, 230));
            G.FillPolygon(is42, is43, N);
        }
        var f = 1.0F;
        if (Embos <= 4)
        {
            f = 1.0F + Medium.Random() / 5.0F;
        }
        if (Embos is > 4 and <= 7)
        {
            f = 1.0F + Medium.Random() / 4.0F;
        }
        if (Embos is > 7 and <= 9)
        {
            f = 1.0F + Medium.Random() / 3.0F;
            if (HSB[2] > 0.7)
            {
                HSB[2] = 0.7F;
            }
        }
        if (Embos is > 9 and <= 10)
        {
            f = 1.0F + Medium.Random() / 2.0F;
            if (HSB[2] > 0.6)
            {
                HSB[2] = 0.6F;
            }
        }
        if (Embos is > 10 and <= 12)
        {
            f = 1.0F + Medium.Random() / 1.0F;
            if (HSB[2] > 0.5)
            {
                HSB[2] = 0.5F;
            }
        }
        if (Embos == 12)
        {
            Chip = 1;
            Ctmag = 2.0F;
            Bfase = -7;
        }
        if (Embos == 13)
        {
            HSB[1] = 0.2F;
            HSB[2] = 0.4F;
        }
        if (Embos == 16)
        {
            _pa = (int) (Medium.Random() * N);
            for (_pb = (int) (Medium.Random() * N); _pa == _pb; _pb = (int) (Medium.Random() * N))
            {
            }
        }
        if (Embos >= 16)
        {
            var i45 = 1;
            var i46 = 1;
            int i47;
            for (i47 = (int)Math.Abs(yz); i47 > 270; i47 -= 360)
            {
            }
            i47 = Math.Abs(i47);
            if (i47 > 90)
            {
                i45 = -1;
            }
            int i48;
            for (i48 = (int)Math.Abs(xy); i48 > 270; i48 -= 360)
            {
            }
            i48 = Math.Abs(i48);
            if (i48 > 90)
            {
                i46 = -1;
            }
            var is49 = new int[3];
            var is50 = new int[3];
            x[0] = Ox[_pa] + mx;
            y[0] = Oy[_pa] + my;
            z[0] = Oz[_pa] + mz;
            x[1] = Ox[_pb] + mx;
            y[1] = Oy[_pb] + my;
            z[1] = Oz[_pb] + mz;
            while (Math.Abs(x[0] - x[1]) > 100)
            {
                if (x[1] > x[0])
                {
                    x[1] -= 30;
                }
                else
                {
                    x[1] += 30;
                }
            }

            while (Math.Abs(z[0] - z[1]) > 100)
            {
                if (z[1] > z[0])
                {
                    z[1] -= 30;
                }
                else
                {
                    z[1] += 30;
                }
            }

            var i51 = (int) (Math.Abs(x[0] - x[1]) / 3 * (0.5 - Medium.Random()));
            var i52 = (int) (Math.Abs(z[0] - z[1]) / 3 * (0.5 - Medium.Random()));
            x[2] = (x[0] + x[1]) / 2 + i51;
            z[2] = (z[0] + z[1]) / 2 + i52;
            var i53 = (int) ((Math.Abs(x[0] - x[1]) + Math.Abs(z[0] - z[1])) / 1.5 *
                             (Medium.Random() / 2.0F + 0.5));
            y[2] = (y[0] + y[1]) / 2 - i45 * i46 * i53;
            Rot(x, y, mx, my, xy, 3);
            Rot(y, z, my, mz, yz, 3);
            Rot(x, z, mx, mz, xz, 3);
            Rot(x, z, Medium.Cx, Medium.Cz, Medium.Xz, 3);
            Rot(y, z, Medium.Cy, Medium.Cz, Medium.Zy, 3);
            for (var i54 = 0; i54 < 3; i54++)
            {
                is49[i54] = Xs(x[i54], z[i54]);
                is50[i54] = Ys(y[i54], z[i54]);
            }
            var i55 = (int) (255.0F + 255.0F * (Medium.Snap[0] / 400.0F));
            if (i55 > 255)
            {
                i55 = 255;
            }
            if (i55 < 0)
            {
                i55 = 0;
            }
            var i56 = (int) (169.0F + 169.0F * (Medium.Snap[1] / 300.0F));
            if (i56 > 255)
            {
                i56 = 255;
            }
            if (i56 < 0)
            {
                i56 = 0;
            }
            var i57 = (int) (89.0F + 89.0F * (Medium.Snap[2] / 200.0F));
            if (i57 > 255)
            {
                i57 = 255;
            }
            if (i57 < 0)
            {
                i57 = 0;
            }
            G.SetColor(new Color(i55, i56, i57));
            G.FillPolygon(is49, is50, 3);
            x[0] = Ox[_pa] + mx;
            y[0] = Oy[_pa] + my;
            z[0] = Oz[_pa] + mz;
            x[1] = Ox[_pb] + mx;
            y[1] = Oy[_pb] + my;
            z[1] = Oz[_pb] + mz;
            while (Math.Abs(x[0] - x[1]) > 100)
            {
                if (x[1] > x[0])
                {
                    x[1] -= 30;
                }
                else
                {
                    x[1] += 30;
                }
            }

            while (Math.Abs(z[0] - z[1]) > 100)
            {
                if (z[1] > z[0])
                {
                    z[1] -= 30;
                }
                else
                {
                    z[1] += 30;
                }
            }

            x[2] = (x[0] + x[1]) / 2 + i51;
            z[2] = (z[0] + z[1]) / 2 + i52;
            i53 = (int) (i53 * 0.8);
            y[2] = (y[0] + y[1]) / 2 - i45 * i46 * i53;
            Rot(x, y, mx, my, xy, 3);
            Rot(y, z, my, mz, yz, 3);
            Rot(x, z, mx, mz, xz, 3);
            Rot(x, z, Medium.Cx, Medium.Cz, Medium.Xz, 3);
            Rot(y, z, Medium.Cy, Medium.Cz, Medium.Zy, 3);
            for (var i58 = 0; i58 < 3; i58++)
            {
                is49[i58] = Xs(x[i58], z[i58]);
                is50[i58] = Ys(y[i58], z[i58]);
            }
            i55 = (int) (255.0F + 255.0F * (Medium.Snap[0] / 400.0F));
            if (i55 > 255)
            {
                i55 = 255;
            }
            if (i55 < 0)
            {
                i55 = 0;
            }
            i56 = (int) (207.0F + 207.0F * (Medium.Snap[1] / 300.0F));
            if (i56 > 255)
            {
                i56 = 255;
            }
            if (i56 < 0)
            {
                i56 = 0;
            }
            i57 = (int) (136.0F + 136.0F * (Medium.Snap[2] / 200.0F));
            if (i57 > 255)
            {
                i57 = 255;
            }
            if (i57 < 0)
            {
                i57 = 0;
            }
            G.SetColor(new Color(i55, i56, i57));
            G.FillPolygon(is49, is50, 3);
        }
        for (var i59 = 0; i59 < N; i59++)
        {
            if (_typ == 1)
            {
                x[i59] = (int) (Ox[i59] * f + mx);
            }
            else
            {
                x[i59] = Ox[i59] + mx;
            }
            if (_typ == 2)
            {
                y[i59] = (int) (Oy[i59] * f + my);
            }
            else
            {
                y[i59] = Oy[i59] + my;
            }
            if (_typ == 3)
            {
                z[i59] = (int) (Oz[i59] * f + mz);
            }
            else
            {
                z[i59] = Oz[i59] + mz;
            }
        }
        if (Embos != 70)
        {
            Embos++;
        }
        else
        {
            Embos = 16;
        }
    }

    internal void Deltafntyp()
    {
        var i = UMath.SafeAbs(Ox[2] - Ox[1]);
        var i24 = UMath.SafeAbs(Oy[2] - Oy[1]);
        var i25 = UMath.SafeAbs(Oz[2] - Oz[1]);
        if (i24 <= i && i24 <= i25)
        {
            _typ = 2;
        }
        if (i <= i24 && i <= i25)
        {
            _typ = 1;
        }
        if (i25 <= i && i25 <= i24)
        {
            _typ = 3;
        }
        _deltaf = 1.0F;
        for (var i26 = 0; i26 < 3; i26++)
        {
            for (var i27 = 0; i27 < 3; i27++)
            {
                if (i27 != i26)
                {
                    _deltaf *= float.Sqrt((Ox[i27] - Ox[i26]) * (Ox[i27] - Ox[i26]) + (Oy[i27] - Oy[i26]) * (Oy[i27] - Oy[i26]) + (Oz[i27] - Oz[i26]) * (Oz[i27] - Oz[i26])) / 100.0f;
                }
            }
        }
        _deltaf = _deltaf / 3.0F;
    }

    internal void Loadprojf()
    {
        _projf = 1.0F;
        for (var i = 0; i < 3; i++)
        {
            for (var i28 = 0; i28 < 3; i28++)
            {
                if (i28 != i)
                {
                    _projf *= float.Sqrt((Ox[i] - Ox[i28]) * (Ox[i] - Ox[i28]) + (Oz[i] - Oz[i28]) * (Oz[i] - Oz[i28])) / 100.0f;
                }
            }
        }
        _projf = _projf / 3.0F;
    }

    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, int angle, int len)
    {
        if (angle != 0)
        {
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var (sin, cos) = Medium.SinCos(angle);
                a[i] = offA + (int) ((pa - offA) * cos - (pb - offB) * sin);
                b[i] = offB + (int) ((pa - offA) * sin + (pb - offB) * cos);
            }
        }
    }

    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, float angle, int len)
    {
        if (angle != 0)
        {
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var (sin, cos) = Medium.SinCos(angle);
                a[i] = offA + (int) ((pa - offA) * cos - (pb - offB) * sin);
                b[i] = offB + (int) ((pa - offA) * sin + (pb - offB) * cos);
            }
        }
    }

    internal static void Rot(Span<int> a, Span<int> b, int offA, int offB, SinCosFloat angle, int len)
    {
        if (angle != 0)
        {
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var cos = angle.Cos;
                var sin = angle.Sin;
                a[i] = offA + (int) ((pa - offA) * cos - (pb - offB) * sin);
                b[i] = offB + (int) ((pa - offA) * sin + (pb - offB) * cos);
            }
        }
    }
    
    internal static void Rot(Span<float> a, Span<float> b, float offA, float offB, float angle, int len)
    {
        if (angle != 0)
        {
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var (sin, cos) = Medium.SinCos(angle);
                a[i] = offA + ((pa - offA) * cos - (pb - offB) * sin);
                b[i] = offB + ((pa - offA) * sin + (pb - offB) * cos);
            }
        }
    }

    internal void S(int i, int i120, int i121, float i122, float i123, float i124, int i125)
    {
        Span<int> ais = stackalloc int[N];
        Span<int> is126 = stackalloc int[N];
        Span<int> is127 = stackalloc int[N];
        for (var i128 = 0; i128 < N; i128++)
        {
            ais[i128] = Ox[i128] + i;
            is127[i128] = Oy[i128] + i120;
            is126[i128] = Oz[i128] + i121;
        }
        Rot(ais, is127, i, i120, i123, N);
        Rot(is127, is126, i120, i121, i124, N);
        Rot(ais, is126, i, i121, i122, N);
        var i129 = (int) (Medium.Crgrnd[0] / 1.5);
        var i130 = (int) (Medium.Crgrnd[1] / 1.5);
        var i131 = (int) (Medium.Crgrnd[2] / 1.5);
        for (var i132 = 0; i132 < N; i132++)
        {
            is127[i132] = Medium.Ground;
        }
        if (i125 == 0)
        {
            var i133 = 0;
            var i134 = 0;
            var i135 = 0;
            var i136 = 0;
            for (var i137 = 0; i137 < N; i137++)
            {
                var i138 = 0;
                var i139 = 0;
                var i140 = 0;
                var i141 = 0;
                for (var i142 = 0; i142 < N; i142++)
                {
                    if (ais[i137] >= ais[i142])
                    {
                        i138++;
                    }
                    if (ais[i137] <= ais[i142])
                    {
                        i139++;
                    }
                    if (is126[i137] >= is126[i142])
                    {
                        i140++;
                    }
                    if (is126[i137] <= is126[i142])
                    {
                        i141++;
                    }
                }
                if (i138 == N)
                {
                    i133 = ais[i137];
                }
                if (i139 == N)
                {
                    i134 = ais[i137];
                }
                if (i140 == N)
                {
                    i135 = is126[i137];
                }
                if (i141 == N)
                {
                    i136 = is126[i137];
                }
            }
            var i143 = (i133 + i134) / 2;
            var i144 = (i135 + i136) / 2;
            var i145 = (i143 - Trackers.Sx + Medium.X) / 3000;
            if (i145 > Trackers.Ncx)
            {
                i145 = Trackers.Ncx;
            }
            if (i145 < 0)
            {
                i145 = 0;
            }
            var i146 = (i144 - Trackers.Sz + Medium.Z) / 3000;
            if (i146 > Trackers.Ncz)
            {
                i146 = Trackers.Ncz;
            }
            if (i146 < 0)
            {
                i146 = 0;
            }
            for (var i147 = Trackers.Sect[i145, i146].Length - 1; i147 >= 0; i147--)
            {
                var i148 = Trackers.Sect[i145, i146][i147];
                var i149 = 0;
                if (Math.Abs(Trackers.Zy[i148]) != 90 && Math.Abs(Trackers.Xy[i148]) != 90 &&
                    Trackers.Rady[i148] != 801 &&
                    Math.Abs(i143 - (Trackers.X[i148] - Medium.X)) < Trackers.Radx[i148] &&
                    Math.Abs(i144 - (Trackers.Z[i148] - Medium.Z)) < Trackers.Radz[i148] &&
                    (!Trackers.Decor[i148] || Medium.Resdown != 2))
                {
                    i149++;
                }
                if (i149 != 0)
                {
                    for (var i150 = 0; i150 < N; i150++)
                    {
                        is127[i150] = Trackers.Y[i148] - Medium.Y;
                        if (Trackers.Zy[i148] != 0)
                        {
                            is127[i150] +=
                                (int) ((is126[i150] - (Trackers.Z[i148] - Medium.Z - Trackers.Radz[i148])) *
                                       Medium.Sin(Trackers.Zy[i148]) / Medium.Sin(90 - Trackers.Zy[i148]) -
                                       Trackers.Radz[i148] * Medium.Sin(Trackers.Zy[i148]) /
                                       Medium.Sin(90 - Trackers.Zy[i148]));
                        }
                        if (Trackers.Xy[i148] != 0)
                        {
                            is127[i150] +=
                                (int) ((ais[i150] - (Trackers.X[i148] - Medium.X - Trackers.Radx[i148])) *
                                       Medium.Sin(Trackers.Xy[i148]) / Medium.Sin(90 - Trackers.Xy[i148]) -
                                       Trackers.Radx[i148] * Medium.Sin(Trackers.Xy[i148]) /
                                       Medium.Sin(90 - Trackers.Xy[i148]));
                        }
                    }
                    i129 = (int) (Trackers.C[i148][0] / 1.5);
                    i130 = (int) (Trackers.C[i148][1] / 1.5);
                    i131 = (int) (Trackers.C[i148][2] / 1.5);
                    break;
                }
            }
        }
        var abool = true;
        Span<int> is151 = stackalloc int[N];
        Span<int> is152 = stackalloc int[N];
        if (i125 == 2)
        {
            i129 = 87;
            i130 = 85;
            i131 = 57;
        }
        else
        {
            for (var i153 = 0; i153 < Medium.Nsp; i153++)
            {
                for (var i154 = 0; i154 < N; i154++)
                {
                    if (Math.Abs(ais[i154] - Medium.Spx[i153]) < Medium.Sprad[i153] &&
                        Math.Abs(is126[i154] - Medium.Spz[i153]) < Medium.Sprad[i153])
                    {
                        abool = false;
                    }
                }
            }
        }
        if (abool)
        {
            Rot(ais, is126, Medium.Cx, Medium.Cz, Medium.Xz, N);
            Rot(is127, is126, Medium.Cy, Medium.Cz, Medium.Zy, N);
            var i155 = 0;
            var i156 = 0;
            var i157 = 0;
            var i158 = 0;
            for (var i159 = 0; i159 < N; i159++)
            {
                is151[i159] = Xs(ais[i159], is126[i159]);
                is152[i159] = Ys(is127[i159], is126[i159]);
                if (is152[i159] < Medium.Ih || is126[i159] < 10)
                {
                    i155++;
                }
                if (is152[i159] > Medium.H || is126[i159] < 10)
                {
                    i156++;
                }
                if (is151[i159] < Medium.Iw || is126[i159] < 10)
                {
                    i157++;
                }
                if (is151[i159] > Medium.W || is126[i159] < 10)
                {
                    i158++;
                }
            }
            if (i157 == N || i155 == N || i156 == N || i158 == N)
            {
                abool = false;
            }
        }
        if (abool)
        {
            for (var i160 = 0; i160 < 16; i160++)
            {
                if (_av > Medium.Fade[i160])
                {
                    i129 = (i129 * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                    i130 = (i130 * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                    i131 = (i131 * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                }
            }

            G.SetColor(new Color(i129, i130, i131));
            G.FillPolygon(is151, is152, N);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Spy(int x, int z)
    {
        return (int) Math.Sqrt((x - Medium.Cx) * (x - Medium.Cx) + z * z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Xs(int x, int z)
    {
        if (z < Medium.Cz)
        {
            z = Medium.Cz;
        }
        return (z - Medium.FocusPoint) * (Medium.Cx - x) / z + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Ys(int x, int z)
    {
        if (z < Medium.Cz)
        {
            z = Medium.Cz;
        }
        return (z - Medium.FocusPoint) * (Medium.Cy - x) / z + x;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Plane? o)
    {
        if (o == null)
        {
            return -1;
        }

        if (_av == o._av)
        {
            return 0;
        }

        if (_av < o._av)
        {
            return 1;
        }

        return -1;
    }
}