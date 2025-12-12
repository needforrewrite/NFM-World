using System.Runtime.CompilerServices;
using NFMWorld.Util;
using URandom = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

public class Medium
{
    //private Medium() {}
    internal static int Adv = 500;

    private static long _atrx;
    private static long _atrz;
    private static int _bcxz;
    private static bool[] _bst;
    internal static bool Bt = false;

    internal static readonly int[] Cfade =
    {
        255, 220, 220
    };

    private static int[] _cgpx;
    private static int[] _cgpz;

    internal static readonly int[] Cgrnd =
    {
        205, 200, 200
    };

    internal static int Checkpoint = -1;
    private static int[,,] _clax;
    private static int[,,] _clay;
    private static int[,,] _claz;
    private static int[,,,] _clc;

    private static readonly int[] Cldd =
    {
        210, 210, 210, 1, -1000
    };

    private static readonly int[] Clds =
    {
        210, 210, 210
    };

    private static int[] _clx;
    private static int[] _clz;
    private static int[] _cmx;
    private static int _cntrn;
    internal static bool Cpflik;

    internal static readonly int[] Cpol =
    {
        215, 210, 210
    };

    internal static readonly int[] Crgrnd =
    {
        205, 200, 200
    };

    internal static bool Crs = false;

    internal static readonly int[] Csky =
    {
        170, 220, 255
    };

    public static int Cx = 1280/2;
    internal static int Cy = 300;
    internal static int Cz = 720/2;
    internal static bool Darksky;

    private static readonly bool[] Diup =
    {
        false, false, false
    };

    internal static float Elecr;

    internal static readonly int[] Fade =
    {
        3000, 4500, 6000, 7500, 9000, 10500, 12000, 13500, 15000, 16500, 18000, 19500, 21000, 22500, 24000, 25500
    };

    internal static int Fallen;
    private static float _fo = 1.0F;
    internal static int FocusPoint = 640;
    internal static int Fogd = 7;
    private static int _fvect = 200;
    private static float _gofo = (float) (0.33000001311302185 + URandom.Double() * 1.34);
    internal static int Ground = 250;
    internal static int H = 720;
    internal static int Hit = 45000;
    internal static int Ih = 0;
    internal static int Iw = 0;
    internal static bool Lastcheck = false;
    internal static int Lastmaf = 0;
    internal static int Lightn = -1;
    internal static bool Lightson = false;
    private static int _lilo = 217;
    internal static bool Loadnew = false;
    internal static bool Lton = false;
    internal static int Mgen = (int) (URandom.Double() * 100000.0);
    private static int[] _mrd;
    private static int[][][] _mtc;
    private static int[][] _mtx;
    private static int[][] _mty;
    private static int[][] _mtz;
    internal static int Ncl;
    private static int _nmt;
    private static int[] _nmv;
    private static int _noc;
    internal static bool Nochekflk = false;
    internal static int Noelec;
    internal static int Nrnd;
    internal static int Nrw;
    internal static int Nsp;
    private static int _nst;
    private static int[,] _ogpx;
    private static int[,] _ogpz;

    private static readonly int[] Ogrnd =
    {
        205, 200, 200
    };

    private static readonly int[] Osky =
    {
        170, 220, 255
    };

    private static float[] _pcv;
    private static int[] _pmx;
    internal static int Ptcnt = -10;
    internal static int Ptr;
    private static float[,] _pvr;

    private static readonly int[] Rand =
    {
        0, 0, 0
    };

    internal static int Rescnt = 5;
    internal static int Resdown;
    private static int _sgpx;
    private static int _sgpz;
    private static readonly int Skyline = -300;

    internal static readonly int[] Snap =
    {
        0, 0, 0
    };

    internal static readonly int[] Sprad = new int[7];
    internal static readonly int[] Spx = new int[7];
    internal static readonly int[] Spz = new int[7];
    private static int[,,] _stc;
    private static int[] _stx;
    private static int[] _stz;
    private static readonly float[] Tcos = new float[360];
    private static bool _td;

    private static readonly int[] Texture =
    {
        0, 0, 0, 50
    };

    internal static int Trk = 0;
    private static int _trn;
    internal static long Trx;
    internal static long Trz;
    private static readonly float[] Tsin = new float[360];
    private static int[] _twn;
    internal static bool Vert;
    internal static float Vxz = 180f;
    internal static int W = 1280;
    internal static int X;
    internal static int Y;
    internal static int Z;
    internal static SinCosFloat Xz;
    internal static SinCosFloat Zy;

    public static bool drawMountains = true;
    public static bool drawClouds = true;
    public static bool drawStars = true;

    public static int FollowYOffset = 0;

    static Medium()
    {
        for (var i = 0; i < 360; i++)
        {
            Tcos[i] = (float) Math.Cos(i * 0.017453292519943295);
        }
        for (var i = 0; i < 360; i++)
        {
            Tsin[i] = (float) Math.Sin(i * 0.017453292519943295);
        }
    }

    internal static void Addsp(int i, int i245, int i246)
    {
        if (Nsp != 7)
        {
            Spx[Nsp] = i;
            Spz[Nsp] = i245;
            Sprad[Nsp] = i246;
            Nsp++;
        }
    }

    internal static void Adjstfade(float f, float f271, int i, GameSparker gamesparker)
    {
        if (Resdown != 2)
        {
            if (f == 5.0F)
            {
                if (Resdown == 0 && Rescnt == 0)
                {
                    Fade[0] = 3000;
                    Fadfrom(Fade[0]);
                    Resdown = 1;
                    Rescnt = 10;
                }
                if (Resdown == 1 && Rescnt == 0)
                {
                    Resdown = 2;
                }
                if ((i == 0 || Resdown == 0) && f271 <= -20.0F)
                {
                    Rescnt--;
                }
            }
            else if (Resdown == 0)
            {
                Rescnt = 5;
            }
            else
            {
                Rescnt = 10;
            }
        }
    }

    internal static void Around(ContO conto, bool abool)
    {
        if (!abool)
        {
            if (!Vert)
            {
                Adv += 2;
            }
            else
            {
                Adv -= 2;
            }
            if (Adv > 900)
            {
                Vert = true;
            }
            if (Adv < -500)
            {
                Vert = false;
            }
        }
        else
        {
            Adv -= 14;
            if (Adv < 617)
            {
                Adv = 617;
            }
        }
        var i = 500 + Adv;
        if (abool && i < 1300)
        {
            i = 1300;
        }
        if (i < 1000)
        {
            i = 1000;
        }
        Y = conto.Y - Adv;
        if (Y > 10)
        {
            Vert = false;
        }
        X = conto.X + (int) ((conto.X - i - conto.X) * Cos(Vxz));
        Z = conto.Z + (int) ((conto.X - i - conto.X) * Sin(Vxz));
        if (!abool)
        {
            Vxz += 2;
        }
        else
        {
            Vxz += 4;
        }
        var i4 = 0;
        var i5 = Y;
        if (i5 > 0)
        {
            i5 = 0;
        }
        if (conto.Y - i5 - Cy < 0)
        {
            i4 = -180;
        }
        var i6 = (int) Math.Sqrt((conto.Z - Z + Cz) * (conto.Z - Z + Cz) + (conto.X - X - Cx) * (conto.X - X - Cx));
        var i7 = (int) (90 + i4 - Math.Atan(i6 / (double) (conto.Y - i5 - Cy)) / 0.017453292519943295);
        Xz = -Vxz + 90;
        if (abool)
        {
            i7 -= 15;
        }
        Zy += (i7 - Zy) / 10;
    }

/*
    internal static void Aroundtrack()
    {
        Y = -Hit;
        X = Cx + (int) Trx + (int) (17000.0F * Cos(Vxz));
        Z = (int) Trz + (int) (17000.0F * Sin(Vxz));
        if (Hit > 5000)
        {
            if (Hit == 45000)
            {
                _fo = 1.0F;
                Zy = 67;
                _atrx = (CheckPoints.X[0] - Trx) / 116L;
                _atrz = (CheckPoints.Z[0] - Trz) / 116L;
                FocusPoint = 400;
            }
            if (Hit == 20000)
            {
                Fallen = 500;
                _fo = 1.0F;
                Zy = 67;
                _atrx = (CheckPoints.X[0] - Trx) / 116L;
                _atrz = (CheckPoints.Z[0] - Trz) / 116L;
                FocusPoint = 400;
            }
            Hit -= Fallen;
            Fallen += 7;
            Trx += _atrx;
            Trz += _atrz;
            if (Hit < 17600)
            {
                Zy -= 2;
            }
            if (Fallen > 500)
            {
                Fallen = 500;
            }
            if (Hit <= 5000)
            {
                Hit = 5000;
                Fallen = 0;
            }
            Vxz += 3;
        }
        else
        {
            FocusPoint = (int) (400.0F * _fo);
            if (Math.Abs(_fo - _gofo) > 0.005)
            {
                if (_fo < _gofo)
                {
                    _fo += 0.005F;
                }
                else
                {
                    _fo -= 0.005F;
                }
            }
            else
            {
                _gofo = (float) (0.3499999940395355 + HansenRandom.Double() * 1.3);
            }
            Vxz++;
            Trx -= (Trx - CheckPoints.X[Ptr]) / 10L;
            Trz -= (Trz - CheckPoints.Z[Ptr]) / 10L;
            if (Ptcnt == 7)
            {
                Ptr++;
                if (Ptr == CheckPoints.N)
                {
                    Ptr = 0;
                    Nrnd++;
                }
                Ptcnt = 0;
            }
            else
            {
                Ptcnt++;
            }
        }
        if (Vxz > 360)
        {
            Vxz -= 360;
        }
        Xz = -Vxz - 90;
        if (-Y - Cy < 0)
        {
        }
        Math.Sqrt((Trz - Z + Cz) * (Trz - Z + Cz) + (Trx - X - Cx) * (Trx - X - Cx));
        Cpflik = !Cpflik;
    }
*/

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Mod(int x, int m) {
        var r = x % m;
        return r<0 ? r + m : r;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(int deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(int deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(int deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(float deg)
    {
        return float.Sin(deg * ((float)Math.PI / 180));
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(float deg)
    {
        return float.Cos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(float deg)
    {
        return float.SinCos(deg * ((float)Math.PI / 180));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Sin(SinCosFloat deg)
    {
        return deg.Sin;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Cos(SinCosFloat deg)
    {
        return deg.Cos;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static (float Sin, float Cos) SinCos(SinCosFloat deg)
    {
        return (deg.Sin, deg.Cos);
    }

    internal static void D()
    {
        Nsp = 0;
        if (Zy > 90)
        {
            Zy = 90;
        }
        if (Zy < -90)
        {
            Zy = -90;
        }
        if (Xz > 360)
        {
            Xz -= 360;
        }
        if (Xz < 0)
        {
            Xz += 360;
        }
        if (Y > 0)
        {
            Y = 0;
        }
        Ground = 250 - Y;
        var ais = new int[4];
        var is223 = new int[4];
        var i = Cgrnd[0];
        var i224 = Cgrnd[1];
        var i225 = Cgrnd[2];
        var i226 = Crgrnd[0];
        var i227 = Crgrnd[1];
        var i228 = Crgrnd[2];
        var i229 = H;
        for (var i230 = 0; i230 < 16; i230++)
        {
            var i231 = Fade[i230];
            var i232 = Ground;
            if (Zy != 0)
            {
                i232 = Cy + (int) ((Ground - Cy) * Cos(Zy) - (Fade[i230] - Cz) * Sin(Zy));
                i231 = Cz + (int) ((Ground - Cy) * Sin(Zy) + (Fade[i230] - Cz) * Cos(Zy));
            }
            ais[0] = Iw;
            is223[0] = Ys(i232, i231);
            if (is223[0] < Ih)
            {
                is223[0] = Ih;
            }
            if (is223[0] > H)
            {
                is223[0] = H;
            }
            ais[1] = Iw;
            is223[1] = i229;
            ais[2] = W;
            is223[2] = i229;
            ais[3] = W;
            is223[3] = is223[0];
            i229 = is223[0];
            if (i230 > 0)
            {
                i226 = (i226 * 7 + Cfade[0]) / 8;
                i227 = (i227 * 7 + Cfade[1]) / 8;
                i228 = (i228 * 7 + Cfade[2]) / 8;
                if (i230 < 3)
                {
                    i = (i * 7 + Cfade[0]) / 8;
                    i224 = (i224 * 7 + Cfade[1]) / 8;
                    i225 = (i225 * 7 + Cfade[2]) / 8;
                }
                else
                {
                    i = i226;
                    i224 = i227;
                    i225 = i228;
                }
            }
            if (is223[0] < H && is223[1] > Ih)
            {
                G.SetColor(new Color(i, i224, i225));
                G.FillPolygon(ais, is223, 4);
            }
        }
        if (Lightn != -1 && Lton)
        {
            if (Lightn < 16)
            {
                if (_lilo > Lightn + 217)
                {
                    _lilo -= 3;
                }
                else
                {
                    Lightn = (int) (16.0F + 16.0F * Random());
                }
            }
            else if (_lilo < Lightn + 217)
            {
                _lilo += 7;
            }
            else
            {
                Lightn = (int) (16.0F * Random());
            }
            Csky[0] = (int) (_lilo + _lilo * (Snap[0] / 100.0F));
            if (Csky[0] > 255)
            {
                Csky[0] = 255;
            }
            if (Csky[0] < 0)
            {
                Csky[0] = 0;
            }
            Csky[1] = (int) (_lilo + _lilo * (Snap[1] / 100.0F));
            if (Csky[1] > 255)
            {
                Csky[1] = 255;
            }
            if (Csky[1] < 0)
            {
                Csky[1] = 0;
            }
            Csky[2] = (int) (_lilo + _lilo * (Snap[2] / 100.0F));
            if (Csky[2] > 255)
            {
                Csky[2] = 255;
            }
            if (Csky[2] < 0)
            {
                Csky[2] = 0;
            }
        }
        i = Csky[0];
        i224 = Csky[1];
        i225 = Csky[2];
        var i233 = i;
        var i234 = i224;
        var i235 = i225;
        var i236 = Cy + (int) ((Skyline - 700 - Cy) * Cos(Zy) - (7000 - Cz) * Sin(Zy));
        var i237 = Cz + (int) ((Skyline - 700 - Cy) * Sin(Zy) + (7000 - Cz) * Cos(Zy));
        i236 = Ys(i236, i237);
        var i238 = Ih;
        for (var i239 = 0; i239 < 16; i239++)
        {
            var i240 = Fade[i239];
            var i241 = Skyline;
            if (Zy != 0)
            {
                i241 = Cy + (int) ((Skyline - Cy) * Cos(Zy) - (Fade[i239] - Cz) * Sin(Zy));
                i240 = Cz + (int) ((Skyline - Cy) * Sin(Zy) + (Fade[i239] - Cz) * Cos(Zy));
            }
            ais[0] = Iw;
            is223[0] = Ys(i241, i240);
            if (is223[0] > H)
            {
                is223[0] = H;
            }
            if (is223[0] < Ih)
            {
                is223[0] = Ih;
            }
            ais[1] = Iw;
            is223[1] = i238;
            ais[2] = W;
            is223[2] = i238;
            ais[3] = W;
            is223[3] = is223[0];
            i238 = is223[0];
            if (i239 > 0)
            {
                i = (i * 7 + Cfade[0]) / 8;
                i224 = (i224 * 7 + Cfade[1]) / 8;
                i225 = (i225 * 7 + Cfade[2]) / 8;
            }
            if (is223[1] < i236)
            {
                i233 = i;
                i234 = i224;
                i235 = i225;
            }
            if (is223[0] > Ih && is223[1] < H)
            {
                G.SetColor(new Color(i, i224, i225));
                G.FillPolygon(ais, is223, 4);
            }
        }
        ais[0] = Iw;
        is223[0] = i238;
        ais[1] = Iw;
        is223[1] = i229;
        ais[2] = W;
        is223[2] = i229;
        ais[3] = W;
        is223[3] = i238;
        if (is223[0] < H && is223[1] > Ih)
        {
            var f = (Math.Abs(Y) - 250.0F) / (Fade[0] * 2);
            if (f < 0.0F)
            {
                f = 0.0F;
            }
            if (f > 1.0F)
            {
                f = 1.0F;
            }
            i = (int) ((i * (1.0F - f) + i226 * (1.0F + f)) / 2.0F);
            i224 = (int) ((i224 * (1.0F - f) + i227 * (1.0F + f)) / 2.0F);
            i225 = (int) ((i225 * (1.0F - f) + i228 * (1.0F + f)) / 2.0F);
            G.SetColor(new Color(i, i224, i225));
            G.FillPolygon(ais, is223, 4);
        }
        if (Resdown != 2)
        {
            for (var i242 = 1; i242 < 20; i242++)
            {
                var i243 = 7000;
                var i244 = Skyline - 700 - i242 * 70;
                if (Zy != 0 && i242 != 19)
                {
                    i244 = Cy + (int) ((Skyline - 700 - i242 * 70 - Cy) * Cos(Zy) - (7000 - Cz) * Sin(Zy));
                    i243 = Cz + (int) ((Skyline - 700 - i242 * 70 - Cy) * Sin(Zy) + (7000 - Cz) * Cos(Zy));
                }
                ais[0] = Iw;
                if (i242 != 19)
                {
                    is223[0] = Ys(i244, i243);
                    if (is223[0] > H)
                    {
                        is223[0] = H;
                    }
                    if (is223[0] < Ih)
                    {
                        is223[0] = Ih;
                    }
                }
                else
                {
                    is223[0] = Ih;
                }
                ais[1] = Iw;
                is223[1] = i236;
                ais[2] = W;
                is223[2] = i236;
                ais[3] = W;
                is223[3] = is223[0];
                i236 = is223[0];
                i233 = (int) (i233 * 0.991);
                i234 = (int) (i234 * 0.991);
                i235 = (int) (i235 * 0.998);
                if (is223[1] > Ih && is223[0] < H)
                {
                    G.SetColor(new Color(i233, i234, i235));
                    G.FillPolygon(ais, is223, 4);
                }
            }
            if (Lightson)
            {
                Drawstars();
            }

            Drawmountains();
            Drawclouds();
        }
        Groundpolys();
        if (Noelec != 0)
        {
            Noelec--;
        }
        if (Cpflik)
        {
            Cpflik = false;
        }
        else
        {
            Cpflik = true;
            Elecr = Random() * 15.0F - 6.0F;
        }
    }

    private static void Drawclouds()
    {
        for (var i = 0; i < _noc; i++)
        {
            var i104 = Cx + (int) ((_clx[i] - X / 20 - Cx) * Cos(Xz) - (_clz[i] - Z / 20 - Cz) * Sin(Xz));
            var i105 = Cz + (int) ((_clx[i] - X / 20 - Cx) * Sin(Xz) + (_clz[i] - Z / 20 - Cz) * Cos(Xz));
            var i106 = Cz + (int) ((Cldd[4] - Y / 20 - Cy) * Sin(Zy) + (i105 - Cz) * Cos(Zy));
            var i107 = Xs(i104 + _cmx[i], i106);
            var i108 = Xs(i104 - _cmx[i], i106);
            if (i107 > 0 && i108 < W && i106 > -_cmx[i] && i107 - i108 > 20)
            {
                var ais = ArrayExt.New<int>(3, 12);
                var is109 = ArrayExt.New<int>(3, 12);
                var is110 = ArrayExt.New<int>(3, 12);
                var is111 = new int[12];
                var is112 = new int[12];
                bool bool116;
                for (var i120 = 0; i120 < 3; i120++)
                {
                    for (var i121 = 0; i121 < 12; i121++)
                    {
                        ais[i120][i121] = _clax[i, i120, i121] + _clx[i] - X / 20;
                        is110[i120][i121] = _claz[i, i120, i121] + _clz[i] - Z / 20;
                        is109[i120][i121] = _clay[i, i120, i121] + Cldd[4] - Y / 20;
                    }
                    Plane.Rot(ais[i120], is110[i120], Cx, Cz, Xz, 12);
                    Plane.Rot(is109[i120], is110[i120], Cy, Cz, Zy, 12);
                }
                for (var i122 = 0; i122 < 12; i122 += 2)
                {
                    var i123 = 0;
                    var i124 = 0;
                    var i125 = 0;
                    var i126 = 0;
                    bool116 = true;
                    var i127 = 0;
                    var i128 = 0;
                    var i129 = 0;
                    for (var i130 = 0; i130 < 6; i130++)
                    {
                        var i131 = 0;
                        var i132 = 1;
                        if (i130 == 0)
                        {
                            i131 = i122;
                        }
                        if (i130 == 1)
                        {
                            i131 = i122 + 1;
                            if (i131 >= 12)
                            {
                                i131 -= 12;
                            }
                        }
                        if (i130 == 2)
                        {
                            i131 = i122 + 2;
                            if (i131 >= 12)
                            {
                                i131 -= 12;
                            }
                        }
                        if (i130 == 3)
                        {
                            i131 = i122 + 2;
                            if (i131 >= 12)
                            {
                                i131 -= 12;
                            }
                            i132 = 2;
                        }
                        if (i130 == 4)
                        {
                            i131 = i122 + 1;
                            if (i131 >= 12)
                            {
                                i131 -= 12;
                            }
                            i132 = 2;
                        }
                        if (i130 == 5)
                        {
                            i131 = i122;
                            i132 = 2;
                        }
                        is111[i130] = Xs(ais[i132][i131], is110[i132][i131]);
                        is112[i130] = Ys(is109[i132][i131], is110[i132][i131]);
                        i128 += ais[i132][i131];
                        i127 += is109[i132][i131];
                        i129 += is110[i132][i131];
                        if (is112[i130] < 0 || is110[0][i130] < 10)
                        {
                            i123++;
                        }
                        if (is112[i130] > H || is110[0][i130] < 10)
                        {
                            i124++;
                        }
                        if (is111[i130] < 0 || is110[0][i130] < 10)
                        {
                            i125++;
                        }
                        if (is111[i130] > W || is110[0][i130] < 10)
                        {
                            i126++;
                        }
                    }
                    if (i125 == 6 || i123 == 6 || i124 == 6 || i126 == 6)
                    {
                        bool116 = false;
                    }
                    if (bool116)
                    {
                        i128 /= 6;
                        i127 /= 6;
                        i129 /= 6;
                        var i133 = (int) Math.Sqrt((Cy - i127) * (Cy - i127) + (Cx - i128) * (Cx - i128) +
                                                   i129 * i129);
                        if (i133 < Fade[7])
                        {
                            var i134 = _clc[i, 1, i122 / 2, 0];
                            var i135 = _clc[i, 1, i122 / 2, 1];
                            var i136 = _clc[i, 1, i122 / 2, 2];
                            for (var i137 = 0; i137 < 16; i137++)
                            {
                                if (i133 > Fade[i137])
                                {
                                    i134 = (i134 * Fogd + Cfade[0]) / (Fogd + 1);
                                    i135 = (i135 * Fogd + Cfade[1]) / (Fogd + 1);
                                    i136 = (i136 * Fogd + Cfade[2]) / (Fogd + 1);
                                }
                            }

                            G.SetColor(new Color(i134, i135, i136));
                            G.FillPolygon(is111, is112, 6);
                        }
                    }
                }
                for (var i138 = 0; i138 < 12; i138 += 2)
                {
                    var i139 = 0;
                    var i140 = 0;
                    var i141 = 0;
                    var i142 = 0;
                    bool116 = true;
                    var i143 = 0;
                    var i144 = 0;
                    var i145 = 0;
                    for (var i146 = 0; i146 < 6; i146++)
                    {
                        var i147 = 0;
                        var i148 = 0;
                        if (i146 == 0)
                        {
                            i147 = i138;
                        }
                        if (i146 == 1)
                        {
                            i147 = i138 + 1;
                            if (i147 >= 12)
                            {
                                i147 -= 12;
                            }
                        }
                        if (i146 == 2)
                        {
                            i147 = i138 + 2;
                            if (i147 >= 12)
                            {
                                i147 -= 12;
                            }
                        }
                        if (i146 == 3)
                        {
                            i147 = i138 + 2;
                            if (i147 >= 12)
                            {
                                i147 -= 12;
                            }
                            i148 = 1;
                        }
                        if (i146 == 4)
                        {
                            i147 = i138 + 1;
                            if (i147 >= 12)
                            {
                                i147 -= 12;
                            }
                            i148 = 1;
                        }
                        if (i146 == 5)
                        {
                            i147 = i138;
                            i148 = 1;
                        }
                        is111[i146] = Xs(ais[i148][i147], is110[i148][i147]);
                        is112[i146] = Ys(is109[i148][i147], is110[i148][i147]);
                        i144 += ais[i148][i147];
                        i143 += is109[i148][i147];
                        i145 += is110[i148][i147];
                        if (is112[i146] < 0 || is110[0][i146] < 10)
                        {
                            i139++;
                        }
                        if (is112[i146] > H || is110[0][i146] < 10)
                        {
                            i140++;
                        }
                        if (is111[i146] < 0 || is110[0][i146] < 10)
                        {
                            i141++;
                        }
                        if (is111[i146] > W || is110[0][i146] < 10)
                        {
                            i142++;
                        }
                    }
                    if (i141 == 6 || i139 == 6 || i140 == 6 || i142 == 6)
                    {
                        bool116 = false;
                    }
                    if (bool116)
                    {
                        i144 /= 6;
                        i143 /= 6;
                        i145 /= 6;
                        var i149 = (int) Math.Sqrt((Cy - i143) * (Cy - i143) + (Cx - i144) * (Cx - i144) +
                                                   i145 * i145);
                        if (i149 < Fade[7])
                        {
                            var i150 = _clc[i, 0, i138 / 2, 0];
                            var i151 = _clc[i, 0, i138 / 2, 1];
                            var i152 = _clc[i, 0, i138 / 2, 2];
                            for (var i153 = 0; i153 < 16; i153++)
                            {
                                if (i149 > Fade[i153])
                                {
                                    i150 = (i150 * Fogd + Cfade[0]) / (Fogd + 1);
                                    i151 = (i151 * Fogd + Cfade[1]) / (Fogd + 1);
                                    i152 = (i152 * Fogd + Cfade[2]) / (Fogd + 1);
                                }
                            }

                            G.SetColor(new Color(i150, i151, i152));
                            G.FillPolygon(is111, is112, 6);
                        }
                    }
                }
                var i154 = 0;
                var i155 = 0;
                var i156 = 0;
                var i157 = 0;
                bool116 = true;
                var i158 = 0;
                var i159 = 0;
                var i160 = 0;
                for (var i161 = 0; i161 < 12; i161++)
                {
                    is111[i161] = Xs(ais[0][i161], is110[0][i161]);
                    is112[i161] = Ys(is109[0][i161], is110[0][i161]);
                    i159 += ais[0][i161];
                    i158 += is109[0][i161];
                    i160 += is110[0][i161];
                    if (is112[i161] < 0 || is110[0][i161] < 10)
                    {
                        i154++;
                    }
                    if (is112[i161] > H || is110[0][i161] < 10)
                    {
                        i155++;
                    }
                    if (is111[i161] < 0 || is110[0][i161] < 10)
                    {
                        i156++;
                    }
                    if (is111[i161] > W || is110[0][i161] < 10)
                    {
                        i157++;
                    }
                }
                if (i156 == 12 || i154 == 12 || i155 == 12 || i157 == 12)
                {
                    bool116 = false;
                }
                if (bool116)
                {
                    i159 /= 12;
                    i158 /= 12;
                    i160 /= 12;
                    var i162 = (int) Math.Sqrt((Cy - i158) * (Cy - i158) + (Cx - i159) * (Cx - i159) + i160 * i160);
                    if (i162 < Fade[7])
                    {
                        var i163 = Clds[0];
                        var i164 = Clds[1];
                        var i165 = Clds[2];
                        for (var i166 = 0; i166 < 16; i166++)
                        {
                            if (i162 > Fade[i166])
                            {
                                i163 = (i163 * Fogd + Cfade[0]) / (Fogd + 1);
                                i164 = (i164 * Fogd + Cfade[1]) / (Fogd + 1);
                                i165 = (i165 * Fogd + Cfade[2]) / (Fogd + 1);
                            }
                        }

                        G.SetColor(new Color(i163, i164, i165));
                        G.FillPolygon(is111, is112, 12);
                    }
                }
            }
        }
    }

    private static void Drawmountains()
    {
        for (var i = 0; i < _nmt; i++)
        {
            var i185 = _mrd[i];
            var i186 =
                Cx + (int) ((_mtx[i185][0] - X / 30 - Cx) * Cos(Xz) - (_mtz[i185][0] - Z / 30 - Cz) * Sin(Xz));
            var i187 =
                Cz + (int) ((_mtx[i185][0] - X / 30 - Cx) * Sin(Xz) + (_mtz[i185][0] - Z / 30 - Cz) * Cos(Xz));
            var i188 = Cz + (int) ((_mty[i185][0] - Y / 30 - Cy) * Sin(Zy) + (i187 - Cz) * Cos(Zy));
            var i189 = Cx + (int) ((_mtx[i185][_nmv[i185] - 1] - X / 30 - Cx) * Cos(Xz) -
                                   (_mtz[i185][_nmv[i185] - 1] - Z / 30 - Cz) * Sin(Xz));
            var i190 = Cz + (int) ((_mtx[i185][_nmv[i185] - 1] - X / 30 - Cx) * Sin(Xz) +
                                   (_mtz[i185][_nmv[i185] - 1] - Z / 30 - Cz) * Cos(Xz));
            var i191 = Cz + (int) ((_mty[i185][_nmv[i185] - 1] - Y / 30 - Cy) * Sin(Zy) + (i190 - Cz) * Cos(Zy));
            if (Xs(i189, i191) > 0 && Xs(i186, i188) < W)
            {
                var ais = new int[_nmv[i185] * 2];
                var is192 = new int[_nmv[i185] * 2];
                var is193 = new int[_nmv[i185] * 2];
                for (var i194 = 0; i194 < _nmv[i185] * 2; i194++)
                {
                    ais[i194] = _mtx[i185][i194] - X / 30;
                    is192[i194] = _mty[i185][i194] - Y / 30;
                    is193[i194] = _mtz[i185][i194] - Z / 30;
                }
                var i195 = (int) Math.Sqrt(ais[_nmv[i185] / 4] * ais[_nmv[i185] / 4] +
                                           is193[_nmv[i185] / 4] * is193[_nmv[i185] / 4]);
                Plane.Rot(ais, is193, Cx, Cz, Xz, _nmv[i185] * 2);
                Plane.Rot(is192, is193, Cy, Cz, Zy, _nmv[i185] * 2);
                var is196 = new int[4];
                var is197 = new int[4];
                bool bool201;
                for (var i202 = 0; i202 < _nmv[i185] - 1; i202++)
                {
                    var i203 = 0;
                    var i204 = 0;
                    var i205 = 0;
                    var i206 = 0;
                    bool201 = true;
                    for (var i207 = 0; i207 < 4; i207++)
                    {
                        var i208 = i207 + i202;
                        if (i207 == 2)
                        {
                            i208 = i202 + _nmv[i185] + 1;
                        }
                        if (i207 == 3)
                        {
                            i208 = i202 + _nmv[i185];
                        }
                        is196[i207] = Xs(ais[i208], is193[i208]);
                        is197[i207] = Ys(is192[i208], is193[i208]);
                        if (is197[i207] < 0 || is193[i208] < 10)
                        {
                            i203++;
                        }
                        if (is197[i207] > H || is193[i208] < 10)
                        {
                            i204++;
                        }
                        if (is196[i207] < 0 || is193[i208] < 10)
                        {
                            i205++;
                        }
                        if (is196[i207] > W || is193[i208] < 10)
                        {
                            i206++;
                        }
                    }
                    if (i205 == 4 || i203 == 4 || i204 == 4 || i206 == 4)
                    {
                        bool201 = false;
                    }
                    if (bool201)
                    {
                        var f = i195 / 2500.0F + (8000.0F - Fade[0]) / 1000.0F - 2.0F -
                                (Math.Abs(Y) - 250.0F) / 5000.0F;
                        if (f > 0.0F && f < 10.0F)
                        {
                            if (f < 3.5)
                            {
                                f = 3.5F;
                            }
                            var i209 = (int) ((_mtc[i185][i202][0] + Cgrnd[0] + Csky[0] * f + Cfade[0] * f) /
                                              (2.0F + f * 2.0F));
                            var i210 = (int) ((_mtc[i185][i202][1] + Cgrnd[1] + Csky[1] * f + Cfade[1] * f) /
                                              (2.0F + f * 2.0F));
                            var i211 = (int) ((_mtc[i185][i202][2] + Cgrnd[2] + Csky[2] * f + Cfade[2] * f) /
                                              (2.0F + f * 2.0F));
                            G.SetColor(new Color(i209, i210, i211));
                            G.FillPolygon(is196, is197, 4);
                        }
                    }
                }
            }
        }
    }

    internal static void Drawstars()
    {
        for (var i = 0; i < _nst; i++)
        {
            var i215 = Cx + (int) (_stx[i] * Cos(Xz) - _stz[i] * Sin(Xz));
            var i216 = Cz + (int) (_stx[i] * Sin(Xz) + _stz[i] * Cos(Xz));
            var i217 = Cy + (int) (-200.0F * Cos(Zy) - i216 * Sin(Zy));
            var i218 = Cz + (int) (-200.0F * Sin(Zy) + i216 * Cos(Zy));
            i215 = Xs(i215, i218);
            i217 = Ys(i217, i218);
            if (i215 - 1 > Iw && i215 + 3 < W && i217 - 1 > Ih && i217 + 3 < H)
            {
                if (_twn[i] == 0)
                {
                    var i219 = (int) (3.0 * URandom.Double());
                    if (i219 >= 3)
                    {
                        i219 = 0;
                    }
                    if (i219 <= -1)
                    {
                        i219 = 2;
                    }
                    var i220 = i219 + 1;
                    if (URandom.Double() > URandom.Double())
                    {
                        i220 = i219 - 1;
                    }
                    if (i220 == 3)
                    {
                        i220 = 0;
                    }
                    if (i220 == -1)
                    {
                        i220 = 2;
                    }
                    for (var i221 = 0; i221 < 3; i221++)
                    {
                        _stc[i, 0, i221] = 200;
                        if (i219 == i221)
                        {
                            _stc[i, 0, i221] += (int) (55.0 * URandom.Double());
                        }
                        if (i220 == i221)
                        {
                            _stc[i, 0, i221] += 55;
                        }
                        _stc[i, 0, i221] = (_stc[i, 0, i221] * 2 + Csky[i221]) / 3;
                        _stc[i, 1, i221] = (_stc[i, 0, i221] + Csky[i221]) / 2;
                    }
                    _twn[i] = 3;
                }
                else
                {
                    _twn[i]--;
                }
                var i222 = 0;
                if (_bst[i])
                {
                    i222 = 1;
                }
                G.SetColor(new Color(_stc[i, 1, 0], _stc[i, 1, 1], _stc[i, 1, 2]));
                G.FillRect(i215 - 1, i217, 3 + i222, 1 + i222);
                G.FillRect(i215, i217 - 1, 1 + i222, 3 + i222);
                G.SetColor(new Color(_stc[i, 0, 0], _stc[i, 0, 1], _stc[i, 0, 2]));
                G.FillRect(i215, i217, 1 + i222, 1 + i222);
            }
        }
    }

    internal static void Fadfrom(int i)
    {
        if (i > 8000)
        {
            i = 8000;
        }
        for (var i270 = 1; i270 < 17; i270++)
        {
            Fade[i270 - 1] = i / 2 * (i270 + 1);
        }
    }

    internal static void Follow(ContO conto, float i, int i27)
    {
        Zy = 10;
        var i28 = 2 + Math.Abs(_bcxz) / 4;
        if (i28 > 20)
        {
            i28 = 20;
        }
        if (i27 != 0)
        {
            if (i27 == 1)
            {
                if (_bcxz < 180)
                {
                    _bcxz += i28;
                }
                if (_bcxz > 180)
                {
                    _bcxz = 180;
                }
            }
            if (i27 == -1)
            {
                if (_bcxz > -180)
                {
                    _bcxz -= i28;
                }
                if (_bcxz < -180)
                {
                    _bcxz = -180;
                }
            }
        }
        else if (Math.Abs(_bcxz) > i28)
        {
            if (_bcxz > 0)
            {
                _bcxz -= i28;
            }
            else
            {
                _bcxz += i28;
            }
        }
        else if (_bcxz != 0)
        {
            _bcxz = 0;
        }
        i += _bcxz;
        Xz = -i;
        X = conto.X - Cx + (int) (-(conto.Z - 800 - conto.Z) * Sin(i));
        Z = conto.Z - Cz + (int) ((conto.Z - 800 - conto.Z) * Cos(i));
        Y = conto.Y - 250 - Cy - FollowYOffset;
    }

    internal static void Getaround(ContO conto)
    {
        if (!Vert)
        {
            Adv += 2;
        }
        else
        {
            Adv -= 2;
        }
        if (Adv > 1700)
        {
            Vert = true;
        }
        if (Adv < -500)
        {
            Vert = false;
        }
        if (conto.Y - Adv > 10)
        {
            Vert = false;
        }
        var i = 500 + Adv;
        if (i < 1000)
        {
            i = 1000;
        }
        var i8 = conto.Y - Adv;
        var i9 = conto.X + (int) ((conto.X - i - conto.X) * Cos(Vxz));
        var i10 = conto.Z + (int) ((conto.X - i - conto.X) * Sin(Vxz));
        var i11 = 0;
        if (Math.Abs(i8 - Y) > _fvect)
        {
            if (Y < i8)
            {
                Y += _fvect;
            }
            else
            {
                Y -= _fvect;
            }
        }
        else
        {
            Y = i8;
            i11++;
        }
        if (Math.Abs(i9 - X) > _fvect)
        {
            if (X < i9)
            {
                X += _fvect;
            }
            else
            {
                X -= _fvect;
            }
        }
        else
        {
            X = i9;
            i11++;
        }
        if (Math.Abs(i10 - Z) > _fvect)
        {
            if (Z < i10)
            {
                Z += _fvect;
            }
            else
            {
                Z -= _fvect;
            }
        }
        else
        {
            Z = i10;
            i11++;
        }
        if (i11 == 3)
        {
            _fvect = 200;
        }
        else
        {
            _fvect += 2;
        }
        for (Vxz += 2; Vxz > 360; Vxz -= 360)
        {
        }
        var i12 = -Vxz + 90;
        var i13 = 0;
        if (conto.X - X - Cx > 0)
        {
            i13 = 180;
        }
        var i14 = -(int) (90 + i13 + Math.Atan((conto.Z - Z) / (double) (conto.X - X - Cx)) / 0.017453292519943295);
        var i15 = Y;
        i13 = 0;
        if (i15 > 0)
        {
            i15 = 0;
        }
        if (conto.Y - i15 - Cy < 0)
        {
            i13 = -180;
        }
        var i16 = (int) Math.Sqrt((conto.Z - Z + Cz) * (conto.Z - Z + Cz) +
                                  (conto.X - X - Cx) * (conto.X - X - Cx));
        var i17 = 25;
        if (i16 != 0)
        {
            i17 = (int) (90 + i13 - Math.Atan(i16 / (double) (conto.Y - i15 - Cy)) / 0.017453292519943295);
        }
        for ( /**/; i12 < 0; i12 += 360)
        {
        }
        for ( /**/; i12 > 360; i12 -= 360)
        {
        }
        for ( /**/; i14 < 0; i14 += 360)
        {
        }
        for ( /**/; i14 > 360; i14 -= 360)
        {
        }
        if ((Math.Abs(i12 - i14) < 30 || Math.Abs(i12 - i14) > 330) && i11 == 3)
        {
            if (Math.Abs(i12 - Xz) > 7 && Math.Abs(i12 - Xz) < 353)
            {
                if (Math.Abs(i12 - Xz) > 180)
                {
                    if (Xz > i12)
                    {
                        Xz += 7;
                    }
                    else
                    {
                        Xz -= 7;
                    }
                }
                else if (Xz < i12)
                {
                    Xz += 7;
                }
                else
                {
                    Xz -= 7;
                }
            }
            else
            {
                Xz = i12;
            }
        }
        else if (Math.Abs(i14 - Xz) > 6 && Math.Abs(i14 - Xz) < 354)
        {
            if (Math.Abs(i14 - Xz) > 180)
            {
                if (Xz > i14)
                {
                    Xz += 3;
                }
                else
                {
                    Xz -= 3;
                }
            }
            else if (Xz < i14)
            {
                Xz += 3;
            }
            else
            {
                Xz -= 3;
            }
        }
        else
        {
            Xz = i14;
        }
        Zy += (i17 - Zy) / 10;
    }

    internal static void Getfollow(ContO conto, int i, int i29)
    {
        Zy = 10;
        var i30 = 2 + Math.Abs(_bcxz) / 4;
        if (i30 > 20)
        {
            i30 = 20;
        }
        if (i29 != 0)
        {
            if (i29 == 1)
            {
                if (_bcxz < 180)
                {
                    _bcxz += i30;
                }
                if (_bcxz > 180)
                {
                    _bcxz = 180;
                }
            }
            if (i29 == -1)
            {
                if (_bcxz > -180)
                {
                    _bcxz -= i30;
                }
                if (_bcxz < -180)
                {
                    _bcxz = -180;
                }
            }
        }
        else if (Math.Abs(_bcxz) > i30)
        {
            if (_bcxz > 0)
            {
                _bcxz -= i30;
            }
            else
            {
                _bcxz += i30;
            }
        }
        else if (_bcxz != 0)
        {
            _bcxz = 0;
        }
        i += _bcxz;
        Xz = -i;
        var i31 = conto.X - Cx + (int) (-(conto.Z - 800 - conto.Z) * Sin(i));
        var i32 = conto.Z - Cz + (int) ((conto.Z - 800 - conto.Z) * Cos(i));
        var i33 = conto.Y - 250 - Cy;
        var i34 = 0;
        if (Math.Abs(i33 - Y) > _fvect)
        {
            if (Y < i33)
            {
                Y += _fvect;
            }
            else
            {
                Y -= _fvect;
            }
        }
        else
        {
            Y = i33;
            i34++;
        }
        if (Math.Abs(i31 - X) > _fvect)
        {
            if (X < i31)
            {
                X += _fvect;
            }
            else
            {
                X -= _fvect;
            }
        }
        else
        {
            X = i31;
            i34++;
        }
        if (Math.Abs(i32 - Z) > _fvect)
        {
            if (Z < i32)
            {
                Z += _fvect;
            }
            else
            {
                Z -= _fvect;
            }
        }
        else
        {
            Z = i32;
            i34++;
        }
        if (i34 == 3)
        {
            _fvect = 200;
        }
        else
        {
            _fvect += 2;
        }
    }

    public static void Groundpolys()
    {
        var i = (X - _sgpx) / 1200 - 12;
        if (i < 0)
        {
            i = 0;
        }
        var i48 = i + 25;
        if (i48 > Nrw)
        {
            i48 = Nrw;
        }
        if (i48 < i)
        {
            i48 = i;
        }
        var i49 = (Z - _sgpz) / 1200 - 12;
        if (i49 < 0)
        {
            i49 = 0;
        }
        var i50 = i49 + 25;
        if (i50 > Ncl)
        {
            i50 = Ncl;
        }
        if (i50 < i49)
        {
            i50 = i49;
        }
        var ais = new int[i48 - i, i50 - i49];
        for (var i51 = i; i51 < i48; i51++)
        {
            for (var i52 = i49; i52 < i50; i52++)
            {
                ais[i51 - i, i52 - i49] = 0;
                var i53 = i51 + i52 * Nrw;
                if (Resdown < 2 || i53 % 2 == 0)
                {
                    var i54 = Cx + (int) ((_cgpx[i53] - X - Cx) * Cos(Xz) - (_cgpz[i53] - Z - Cz) * Sin(Xz));
                    var i55 = Cz + (int) ((_cgpx[i53] - X - Cx) * Sin(Xz) + (_cgpz[i53] - Z - Cz) * Cos(Xz));
                    var i56 = Cz + (int) ((250 - Y - Cy) * Sin(Zy) + (i55 - Cz) * Cos(Zy));
                    if (Xs(i54 + _pmx[i53], i56) > 0 && Xs(i54 - _pmx[i53], i56) < W && i56 > -_pmx[i53] &&
                        i56 < Fade[2])
                    {
                        ais[i51 - i, i52 - i49] = i56;
                        var is57 = new int[8];
                        var is58 = new int[8];
                        var is59 = new int[8];
                        for (var i60 = 0; i60 < 8; i60++)
                        {
                            is57[i60] = (int) (_ogpx[i53, i60] * _pvr[i53, i60] + _cgpx[i53] - X);
                            is58[i60] = (int) (_ogpz[i53, i60] * _pvr[i53, i60] + _cgpz[i53] - Z);
                            is59[i60] = Ground;
                        }
                        Plane.Rot(is57, is58, Cx, Cz, Xz, 8);
                        Plane.Rot(is59, is58, Cy, Cz, Zy, 8);
                        var is61 = new int[8];
                        var is62 = new int[8];
                        var i63 = 0;
                        var i64 = 0;
                        var i65 = 0;
                        var i66 = 0;
                        var abool = true;
                        for (var i67 = 0; i67 < 8; i67++)
                        {
                            is61[i67] = Xs(is57[i67], is58[i67]);
                            is62[i67] = Ys(is59[i67], is58[i67]);
                            if (is62[i67] < 0 || is58[i67] < 10)
                            {
                                i63++;
                            }
                            if (is62[i67] > H || is58[i67] < 10)
                            {
                                i64++;
                            }
                            if (is61[i67] < 0 || is58[i67] < 10)
                            {
                                i65++;
                            }
                            if (is61[i67] > W || is58[i67] < 10)
                            {
                                i66++;
                            }
                        }
                        if (i65 == 8 || i63 == 8 || i64 == 8 || i66 == 8)
                        {
                            abool = false;
                        }
                        if (abool)
                        {
                            var i68 = (int) ((Cpol[0] * _pcv[i53] + Cgrnd[0]) / 2.0F);
                            var i69 = (int) ((Cpol[1] * _pcv[i53] + Cgrnd[1]) / 2.0F);
                            var i70 = (int) ((Cpol[2] * _pcv[i53] + Cgrnd[2]) / 2.0F);
                            if (i56 - _pmx[i53] > Fade[0])
                            {
                                i68 = (i68 * 7 + Cfade[0]) / 8;
                                i69 = (i69 * 7 + Cfade[1]) / 8;
                                i70 = (i70 * 7 + Cfade[2]) / 8;
                            }
                            if (i56 - _pmx[i53] > Fade[1])
                            {
                                i68 = (i68 * 7 + Cfade[0]) / 8;
                                i69 = (i69 * 7 + Cfade[1]) / 8;
                                i70 = (i70 * 7 + Cfade[2]) / 8;
                            }
                            G.SetColor(new Color(i68, i69, i70));
                            G.FillPolygon(is61, is62, 8);
                        }
                    }
                }
            }
        }
        for (var i71 = i; i71 < i48; i71++)
        {
            for (var i72 = i49; i72 < i50; i72++)
            {
                if (ais[i71 - i, i72 - i49] != 0)
                {
                    var i73 = i71 + i72 * Nrw;
                    var is74 = new int[8];
                    var is75 = new int[8];
                    var is76 = new int[8];
                    for (var i77 = 0; i77 < 8; i77++)
                    {
                        is74[i77] = _ogpx[i73, i77] + _cgpx[i73] - X;
                        is75[i77] = _ogpz[i73, i77] + _cgpz[i73] - Z;
                        is76[i77] = Ground;
                    }
                    Plane.Rot(is74, is75, Cx, Cz, Xz, 8);
                    Plane.Rot(is76, is75, Cy, Cz, Zy, 8);
                    var is78 = new int[8];
                    var is79 = new int[8];
                    var i80 = 0;
                    var i81 = 0;
                    var i82 = 0;
                    var i83 = 0;
                    var abool = true;
                    for (var i84 = 0; i84 < 8; i84++)
                    {
                        is78[i84] = Xs(is74[i84], is75[i84]);
                        is79[i84] = Ys(is76[i84], is75[i84]);
                        if (is79[i84] < 0 || is75[i84] < 10)
                        {
                            i80++;
                        }
                        if (is79[i84] > H || is75[i84] < 10)
                        {
                            i81++;
                        }
                        if (is78[i84] < 0 || is75[i84] < 10)
                        {
                            i82++;
                        }
                        if (is78[i84] > W || is75[i84] < 10)
                        {
                            i83++;
                        }
                    }
                    if (i82 == 8 || i80 == 8 || i81 == 8 || i83 == 8)
                    {
                        abool = false;
                    }
                    if (abool)
                    {
                        var i85 = (int) (Cpol[0] * _pcv[i73]);
                        var i86 = (int) (Cpol[1] * _pcv[i73]);
                        var i87 = (int) (Cpol[2] * _pcv[i73]);
                        if (ais[i71 - i, i72 - i49] - _pmx[i73] > Fade[0])
                        {
                            i85 = (i85 * 7 + Cfade[0]) / 8;
                            i86 = (i86 * 7 + Cfade[1]) / 8;
                            i87 = (i87 * 7 + Cfade[2]) / 8;
                        }
                        if (ais[i71 - i, i72 - i49] - _pmx[i73] > Fade[1])
                        {
                            i85 = (i85 * 7 + Cfade[0]) / 8;
                            i86 = (i86 * 7 + Cfade[1]) / 8;
                            i87 = (i87 * 7 + Cfade[2]) / 8;
                        }
                        G.SetColor(new Color(i85, i86, i87));
                        G.FillPolygon(is78, is79, 8);
                    }
                }
            }
        }
    }

    internal static void Newclouds(int i, int i88, int i89, int i90)
    {
        i = i / 20 - 10000;
        i88 = i88 / 20 + 10000;
        i89 = i89 / 20 - 10000;
        i90 = i90 / 20 + 10000;
        _noc = (i88 - i) * (i90 - i89) / 16666667;
        _clx = new int[_noc];
        _clz = new int[_noc];
        _cmx = new int[_noc];
        _clax = new int[_noc, 3, 12];
        _clay = new int[_noc, 3, 12];
        _claz = new int[_noc, 3, 12];
        _clc = new int[_noc, 2, 6, 3];
        for (var i91 = 0; i91 < _noc; i91++)
        {
            _clx[i91] = (int) (i + (i88 - i) * URandom.Double());
            _clz[i91] = (int) (i89 + (i90 - i89) * URandom.Double());
            var f = (float) (0.25 + URandom.Double() * 1.25);
            var f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 0] = (int) (f92 * 0.3826);
            _claz[i91, 0, 0] = (int) (f92 * 0.9238);
            _clay[i91, 0, 0] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 1] = (int) (f92 * 0.7071);
            _claz[i91, 0, 1] = (int) (f92 * 0.7071);
            _clay[i91, 0, 1] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 2] = (int) (f92 * 0.9238);
            _claz[i91, 0, 2] = (int) (f92 * 0.3826);
            _clay[i91, 0, 2] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 3] = (int) (f92 * 0.9238);
            _claz[i91, 0, 3] = -(int) (f92 * 0.3826);
            _clay[i91, 0, 3] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 4] = (int) (f92 * 0.7071);
            _claz[i91, 0, 4] = -(int) (f92 * 0.7071);
            _clay[i91, 0, 4] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 5] = (int) (f92 * 0.3826);
            _claz[i91, 0, 5] = -(int) (f92 * 0.9238);
            _clay[i91, 0, 5] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 6] = -(int) (f92 * 0.3826);
            _claz[i91, 0, 6] = -(int) (f92 * 0.9238);
            _clay[i91, 0, 6] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 7] = -(int) (f92 * 0.7071);
            _claz[i91, 0, 7] = -(int) (f92 * 0.7071);
            _clay[i91, 0, 7] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 8] = -(int) (f92 * 0.9238);
            _claz[i91, 0, 8] = -(int) (f92 * 0.3826);
            _clay[i91, 0, 8] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 9] = -(int) (f92 * 0.9238);
            _claz[i91, 0, 9] = (int) (f92 * 0.3826);
            _clay[i91, 0, 9] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 10] = -(int) (f92 * 0.7071);
            _claz[i91, 0, 10] = (int) (f92 * 0.7071);
            _clay[i91, 0, 10] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            f92 = (float) ((200.0 + URandom.Double() * 700.0) * f);
            _clax[i91, 0, 11] = -(int) (f92 * 0.3826);
            _claz[i91, 0, 11] = (int) (f92 * 0.9238);
            _clay[i91, 0, 11] = (int) ((25.0 - URandom.Double() * 50.0) * f);
            for (var i93 = 0; i93 < 12; i93++)
            {
                var i94 = i93 - 1;
                if (i94 == -1)
                {
                    i94 = 11;
                }
                var i95 = i93 + 1;
                if (i95 == 12)
                {
                    i95 = 0;
                }
                _clax[i91, 0, i93] = ((_clax[i91, 0, i94] + _clax[i91, 0, i95]) / 2 + _clax[i91, 0, i93]) / 2;
                _clay[i91, 0, i93] = ((_clay[i91, 0, i94] + _clay[i91, 0, i95]) / 2 + _clay[i91, 0, i93]) / 2;
                _claz[i91, 0, i93] = ((_claz[i91, 0, i94] + _claz[i91, 0, i95]) / 2 + _claz[i91, 0, i93]) / 2;
            }
            for (var i96 = 0; i96 < 12; i96++)
            {
                f92 = (float) (1.2 + 0.6 * URandom.Double());
                _clax[i91, 1, i96] = (int) (_clax[i91, 0, i96] * f92);
                _claz[i91, 1, i96] = (int) (_claz[i91, 0, i96] * f92);
                _clay[i91, 1, i96] = (int) (_clay[i91, 0, i96] - 100.0 * URandom.Double());
                f92 = (float) (1.1 + 0.3 * URandom.Double());
                _clax[i91, 2, i96] = (int) (_clax[i91, 1, i96] * f92);
                _claz[i91, 2, i96] = (int) (_claz[i91, 1, i96] * f92);
                _clay[i91, 2, i96] = (int) (_clay[i91, 1, i96] - 240.0 * URandom.Double());
            }
            _cmx[i91] = 0;
            for (var i97 = 0; i97 < 12; i97++)
            {
                var i98 = i97 - 1;
                if (i98 == -1)
                {
                    i98 = 11;
                }
                var i99 = i97 + 1;
                if (i99 == 12)
                {
                    i99 = 0;
                }
                _clay[i91, 1, i97] = ((_clay[i91, 1, i98] + _clay[i91, 1, i99]) / 2 + _clay[i91, 1, i97]) / 2;
                _clay[i91, 2, i97] = ((_clay[i91, 2, i98] + _clay[i91, 2, i99]) / 2 + _clay[i91, 2, i97]) / 2;
                var i100 = (int) Math.Sqrt(_clax[i91, 2, i97] * _clax[i91, 2, i97] +
                                           _claz[i91, 2, i97] * _claz[i91, 2, i97]);
                if (i100 > _cmx[i91])
                {
                    _cmx[i91] = i100;
                }
            }
            for (var i101 = 0; i101 < 6; i101++)
            {
                var d = URandom.Double();
                var d102 = URandom.Double();
                for (var i103 = 0; i103 < 3; i103++)
                {
                    f92 = Clds[i103] * 1.05F - Clds[i103];
                    _clc[i91, 0, i101, i103] = (int) (Clds[i103] + f92 * d);
                    if (_clc[i91, 0, i101, i103] > 255)
                    {
                        _clc[i91, 0, i101, i103] = 255;
                    }
                    if (_clc[i91, 0, i101, i103] < 0)
                    {
                        _clc[i91, 0, i101, i103] = 0;
                    }
                    _clc[i91, 1, i101, i103] = (int) (Clds[i103] * 1.05F + f92 * d102);
                    if (_clc[i91, 1, i101, i103] > 255)
                    {
                        _clc[i91, 1, i101, i103] = 255;
                    }
                    if (_clc[i91, 1, i101, i103] < 0)
                    {
                        _clc[i91, 1, i101, i103] = 0;
                    }
                }
            }
        }
    }

    internal static void Newmountains(int i, int i167, int i168, int i169)
    {
        var random = new URandom(Mgen);
        _nmt = (int) (20.0 + 10.0 * random.NextDouble());
        var i170 = (i + i167) / 60;
        var i171 = (i168 + i169) / 60;
        var i172 = Math.Max(i167 - i, i169 - i168) / 60;
        _mrd = new int[_nmt];
        _nmv = new int[_nmt];
        _mtx = new int[_nmt][];
        _mty = new int[_nmt][];
        _mtz = new int[_nmt][];
        _mtc = new int[_nmt][][];
        var ais = new int[_nmt];
        var is173 = new int[_nmt];
        for (var i174 = 0; i174 < _nmt; i174++)
        {
            int i175;
            float f;
            float f176;
            ais[i174] = (int) (10000.0 + random.NextDouble() * 10000.0);
            var i177 = (int) (random.NextDouble() * 360.0);
            if (random.NextDouble() > random.NextDouble())
            {
                f = (float) (0.2 + random.NextDouble() * 0.35);
                f176 = (float) (0.2 + random.NextDouble() * 0.35);
                _nmv[i174] = (int) (f * (24.0 + 16.0 * random.NextDouble()));
                i175 = (int) (85.0 + 10.0 * random.NextDouble());
            }
            else
            {
                f = (float) (0.3 + random.NextDouble() * 1.1);
                f176 = (float) (0.2 + random.NextDouble() * 0.35);
                _nmv[i174] = (int) (f * (12.0 + 8.0 * random.NextDouble()));
                i175 = (int) (104.0 - 10.0 * random.NextDouble());
            }
            _mtx[i174] = new int[_nmv[i174] * 2];
            _mty[i174] = new int[_nmv[i174] * 2];
            _mtz[i174] = new int[_nmv[i174] * 2];
            _mtc[i174] = ArrayExt.New<int>(_nmv[i174], 3);
            for (var i178 = 0; i178 < _nmv[i174]; i178++)
            {
                _mtx[i174][i178] =
                    (int) ((i178 * 500 + (random.NextDouble() * 800.0 - 400.0) - 250 * (_nmv[i174] - 1)) * f);
                _mtx[i174][i178 + _nmv[i174]] =
                    (int) ((i178 * 500 + (random.NextDouble() * 800.0 - 400.0) - 250 * (_nmv[i174] - 1)) * f);
                _mtx[i174][_nmv[i174]] = (int) (_mtx[i174][0] - (100.0 + random.NextDouble() * 600.0) * f);
                _mtx[i174][_nmv[i174] * 2 - 1] =
                    (int) (_mtx[i174][_nmv[i174] - 1] + (100.0 + random.NextDouble() * 600.0) * f);
                if (i178 == 0 || i178 == _nmv[i174] - 1)
                {
                    _mty[i174][i178] = (int) ((-400.0 - 1200.0 * random.NextDouble()) * f176 + Ground);
                }
                if (i178 == 1 || i178 == _nmv[i174] - 2)
                {
                    _mty[i174][i178] = (int) ((-1000.0 - 1450.0 * random.NextDouble()) * f176 + Ground);
                }
                if (i178 > 1 && i178 < _nmv[i174] - 2)
                {
                    _mty[i174][i178] = (int) ((-1600.0 - 1700.0 * random.NextDouble()) * f176 + Ground);
                }
                _mty[i174][i178 + _nmv[i174]] = Ground - 70;
                _mtz[i174][i178] = i171 + i172 + ais[i174];
                _mtz[i174][i178 + _nmv[i174]] = i171 + i172 + ais[i174];
                var f179 = (float) (0.5 + random.NextDouble() * 0.5);
                _mtc[i174][i178][0] = (int) (170.0F * f179 + 170.0F * f179 * (Snap[0] / 100.0F));
                if (_mtc[i174][i178][0] > 255)
                {
                    _mtc[i174][i178][0] = 255;
                }
                if (_mtc[i174][i178][0] < 0)
                {
                    _mtc[i174][i178][0] = 0;
                }
                _mtc[i174][i178][1] = (int) (i175 * f179 + 85.0F * f179 * (Snap[1] / 100.0F));
                if (_mtc[i174][i178][1] > 255)
                {
                    _mtc[i174][i178][1] = 255;
                }
                if (_mtc[i174][i178][1] < 1)
                {
                    _mtc[i174][i178][1] = 0;
                }
                _mtc[i174][i178][2] = 0;
            }
            for (var i180 = 1; i180 < _nmv[i174] - 1; i180++)
            {
                var i181 = i180 - 1;
                var i182 = i180 + 1;
                _mty[i174][i180] = ((_mty[i174][i181] + _mty[i174][i182]) / 2 + _mty[i174][i180]) / 2;
            }
            Plane.Rot(_mtx[i174], _mtz[i174], i170, i171, i177, _nmv[i174] * 2);
            is173[i174] = 0;
        }
        for (var i183 = 0; i183 < _nmt; i183++)
        {
            for (var i184 = i183 + 1; i184 < _nmt; i184++)
            {
                if (ais[i183] < ais[i184])
                {
                    is173[i183]++;
                }
                else
                {
                    is173[i184]++;
                }
            }

            _mrd[is173[i183]] = i183;
        }
    }

    internal static void Newpolys(int i, int i35, int i36, int i37, int i38)
    {
        var random = new URandom((i38 + Cgrnd[0] + Cgrnd[1] + Cgrnd[2]) * 1671);
        Nrw = i35 / 1200 + 9;
        Ncl = i37 / 1200 + 9;
        _sgpx = i - 4800;
        _sgpz = i36 - 4800;
        _ogpx = null;
        _ogpz = null;
        _pvr = null;
        _cgpx = null;
        _cgpz = null;
        _pmx = null;
        _pcv = null;
        _ogpx = new int[Nrw * Ncl, 8];
        _ogpz = new int[Nrw * Ncl, 8];
        _pvr = new float[Nrw * Ncl, 8];
        _cgpx = new int[Nrw * Ncl];
        _cgpz = new int[Nrw * Ncl];
        _pmx = new int[Nrw * Ncl];
        _pcv = new float[Nrw * Ncl];
        var i39 = 0;
        var i40 = 0;
        for (var i41 = 0; i41 < Nrw * Ncl; i41++)
        {
            _cgpx[i41] = _sgpx + i39 * 1200 + (int) (random.NextDouble() * 1000.0 - 500.0);
            _cgpz[i41] = _sgpz + i40 * 1200 + (int) (random.NextDouble() * 1000.0 - 500.0);
            for (var i42 = 0; i42 < Trackers.Nt; i42++)
            {
                if (Trackers.Zy[i42] == 0 && Trackers.Xy[i42] == 0)
                {
                    if (Trackers.Radx[i42] < Trackers.Radz[i42] &&
                        Math.Abs(_cgpz[i41] - Trackers.Z[i42]) < Trackers.Radz[i42])
                    {
                        for (;
                             Math.Abs(_cgpx[i41] - Trackers.X[i42]) < Trackers.Radx[i42];
                             _cgpx[i41] +=
                                 (int) (random.NextDouble() * Trackers.Radx[i42] * 2.0 - Trackers.Radx[i42]))
                        {
                        }
                    }
                    if (Trackers.Radz[i42] < Trackers.Radx[i42] &&
                        Math.Abs(_cgpx[i41] - Trackers.X[i42]) < Trackers.Radx[i42])
                    {
                        for (;
                             Math.Abs(_cgpz[i41] - Trackers.Z[i42]) < Trackers.Radz[i42];
                             _cgpz[i41] +=
                                 (int) (random.NextDouble() * Trackers.Radz[i42] * 2.0 - Trackers.Radz[i42]))
                        {
                        }
                    }
                }
            }

            if (++i39 == Nrw)
            {
                i39 = 0;
                i40++;
            }
        }
        for (var i43 = 0; i43 < Nrw * Ncl; i43++)
        {
            var f = (float) (0.3 + 1.6 * random.NextDouble());
            _ogpx[i43, 0] = 0;
            _ogpz[i43, 0] = (int) ((100.0 + random.NextDouble() * 760.0) * f);
            _ogpx[i43, 1] = (int) ((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
            _ogpz[i43, 1] = _ogpx[i43, 1];
            _ogpx[i43, 2] = (int) ((100.0 + random.NextDouble() * 760.0) * f);
            _ogpz[i43, 2] = 0;
            _ogpx[i43, 3] = (int) ((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
            _ogpz[i43, 3] = -_ogpx[i43, 3];
            _ogpx[i43, 4] = 0;
            _ogpz[i43, 4] = -(int) ((100.0 + random.NextDouble() * 760.0) * f);
            _ogpx[i43, 5] = -(int) ((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
            _ogpz[i43, 5] = _ogpx[i43, 5];
            _ogpx[i43, 6] = -(int) ((100.0 + random.NextDouble() * 760.0) * f);
            _ogpz[i43, 6] = 0;
            _ogpx[i43, 7] = -(int) ((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
            _ogpz[i43, 7] = -_ogpx[i43, 7];
            for (var i44 = 0; i44 < 8; i44++)
            {
                var i45 = i44 - 1;
                if (i45 == -1)
                {
                    i45 = 7;
                }
                var i46 = i44 + 1;
                if (i46 == 8)
                {
                    i46 = 0;
                }
                _ogpx[i43, i44] = ((_ogpx[i43, i45] + _ogpx[i43, i46]) / 2 + _ogpx[i43, i44]) / 2;
                _ogpz[i43, i44] = ((_ogpz[i43, i45] + _ogpz[i43, i46]) / 2 + _ogpz[i43, i44]) / 2;
                _pvr[i43, i44] = (float) (1.1 + random.NextDouble() * 0.8);
                var i47 = (int) Math.Sqrt(
                    (int) (_ogpx[i43, i44] * _ogpx[i43, i44] * _pvr[i43, i44] * _pvr[i43, i44] +
                           _ogpz[i43, i44] * _ogpz[i43, i44] * _pvr[i43, i44] * _pvr[i43, i44]));
                if (i47 > _pmx[i43])
                {
                    _pmx[i43] = i47;
                }
            }
            _pcv[i43] = (float) (0.97 + random.NextDouble() * 0.03);
            if (_pcv[i43] > 1.0F)
            {
                _pcv[i43] = 1.0F;
            }
            if (random.NextDouble() > random.NextDouble())
            {
                _pcv[i43] = 1.0F;
            }
        }
    }

    internal static void Newstars()
    {
        _stx = [];
        _stz = [];
        _stc = new int[0, 0, 0];
        _bst = [];
        _twn = [];
        _nst = 0;
        if (Lightson)
        {
            var random = new URandom((long) (URandom.Double() * 100000.0));
            _nst = 40;
            _stx = new int[_nst];
            _stz = new int[_nst];
            _stc = new int[_nst, 2, 3];
            _bst = new bool[_nst];
            _twn = new int[_nst];
            for (var i = 0; i < _nst; i++)
            {
                _stx[i] = (int) (2000.0 * random.NextDouble() - 1000.0);
                _stz[i] = (int) (2000.0 * random.NextDouble() - 1000.0);
                var i212 = (int) (3.0 * random.NextDouble());
                if (i212 >= 3)
                {
                    i212 = 0;
                }
                if (i212 <= -1)
                {
                    i212 = 2;
                }
                var i213 = i212 + 1;
                if (random.NextDouble() > random.NextDouble())
                {
                    i213 = i212 - 1;
                }
                if (i213 == 3)
                {
                    i213 = 0;
                }
                if (i213 == -1)
                {
                    i213 = 2;
                }
                for (var i214 = 0; i214 < 3; i214++)
                {
                    _stc[i, 0, i214] = 200;
                    if (i212 == i214)
                    {
                        _stc[i, 0, i214] += (int) (55.0 * random.NextDouble());
                    }
                    if (i213 == i214)
                    {
                        _stc[i, 0, i214] += 55;
                    }
                    _stc[i, 0, i214] = (_stc[i, 0, i214] * 2 + Csky[i214]) / 3;
                    _stc[i, 1, i214] = (_stc[i, 0, i214] + Csky[i214]) / 2;
                }
                _twn[i] = (int) (4.0 * random.NextDouble());
                _bst[i] = random.NextDouble() > 0.8;
            }
        }
    }

    internal static bool RandomBoolean()
    {
        return URandom.Boolean();
    }

    internal static float Random()
    {
        if (_cntrn == 0)
        {
            for (var i = 0; i < 3; i++)
            {
                Rand[i] = (int) (10.0f * URandom.Single());
                Diup[i] = URandom.Boolean();
            }
            _cntrn = 20;
        }
        else
        {
            _cntrn--;
        }
        for (var i = 0; i < 3; i++)
        {
            if (Diup[i])
            {
                Rand[i]++;
                if (Rand[i] == 10)
                {
                    Rand[i] = 0;
                }
            }
            else
            {
                Rand[i]--;
                if (Rand[i] == -1)
                {
                    Rand[i] = 9;
                }
            }
        }

        _trn++;
        if (_trn == 3)
        {
            _trn = 0;
        }
        return Rand[_trn] / 10.0F;
    }

    internal static void Setcloads(int i, int i252, int i253, int i254, int i255)
    {
        if (i254 < 0)
        {
            i254 = 0;
        }
        if (i254 > 10)
        {
            i254 = 10;
        }
        if (i255 < -1500)
        {
            i255 = -1500;
        }
        if (i255 > -500)
        {
            i255 = -500;
        }
        Cldd[0] = i;
        Cldd[1] = i252;
        Cldd[2] = i253;
        Cldd[3] = i254;
        Cldd[4] = i255;
        for (var i256 = 0; i256 < 3; i256++)
        {
            Clds[i256] = (Osky[i256] * Cldd[3] + Cldd[i256]) / (Cldd[3] + 1);
            Clds[i256] = (int) (Clds[i256] + Clds[i256] * (Snap[i256] / 100.0F));
            if (Clds[i256] > 255)
            {
                Clds[i256] = 255;
            }
            if (Clds[i256] < 0)
            {
                Clds[i256] = 0;
            }
        }
    }

    internal static void Setexture(int i, int i261, int i262, int i263)
    {
        if (i263 < 20)
        {
            i263 = 20;
        }
        if (i263 > 60)
        {
            i263 = 60;
        }
        Texture[0] = i;
        Texture[1] = i261;
        Texture[2] = i262;
        Texture[3] = i263;
        i = (Ogrnd[0] * i263 + i) / (1 + i263);
        i261 = (Ogrnd[1] * i263 + i261) / (1 + i263);
        i262 = (Ogrnd[2] * i263 + i262) / (1 + i263);
        Cpol[0] = (int) (i + i * (Snap[0] / 100.0F));
        if (Cpol[0] > 255)
        {
            Cpol[0] = 255;
        }
        if (Cpol[0] < 0)
        {
            Cpol[0] = 0;
        }
        Cpol[1] = (int) (i261 + i261 * (Snap[1] / 100.0F));
        if (Cpol[1] > 255)
        {
            Cpol[1] = 255;
        }
        if (Cpol[1] < 0)
        {
            Cpol[1] = 0;
        }
        Cpol[2] = (int) (i262 + i262 * (Snap[2] / 100.0F));
        if (Cpol[2] > 255)
        {
            Cpol[2] = 255;
        }
        if (Cpol[2] < 0)
        {
            Cpol[2] = 0;
        }
        for (var i264 = 0; i264 < 3; i264++)
        {
            Crgrnd[i264] = (int) ((Cpol[i264] * 0.99 + Cgrnd[i264]) / 2.0);
        }
    }

    internal static void Setfade(int i, int i268, int i269)
    {
        Cfade[0] = (int) (i + i * (Snap[0] / 100.0F));
        if (Cfade[0] > 255)
        {
            Cfade[0] = 255;
        }
        if (Cfade[0] < 0)
        {
            Cfade[0] = 0;
        }
        Cfade[1] = (int) (i268 + i268 * (Snap[1] / 100.0F));
        if (Cfade[1] > 255)
        {
            Cfade[1] = 255;
        }
        if (Cfade[1] < 0)
        {
            Cfade[1] = 0;
        }
        Cfade[2] = (int) (i269 + i269 * (Snap[2] / 100.0F));
        if (Cfade[2] > 255)
        {
            Cfade[2] = 255;
        }
        if (Cfade[2] < 0)
        {
            Cfade[2] = 0;
        }
    }

    internal static void Setgrnd(int i, int i257, int i258)
    {
        Ogrnd[0] = i;
        Ogrnd[1] = i257;
        Ogrnd[2] = i258;
        for (var i259 = 0; i259 < 3; i259++)
        {
            Cpol[i259] = (Ogrnd[i259] * Texture[3] + Texture[i259]) / (1 + Texture[3]);
            Cpol[i259] = (int) (Cpol[i259] + Cpol[i259] * (Snap[i259] / 100.0F));
            if (Cpol[i259] > 255)
            {
                Cpol[i259] = 255;
            }
            if (Cpol[i259] < 0)
            {
                Cpol[i259] = 0;
            }
        }
        Cgrnd[0] = (int) (i + i * (Snap[0] / 100.0F));
        if (Cgrnd[0] > 255)
        {
            Cgrnd[0] = 255;
        }
        if (Cgrnd[0] < 0)
        {
            Cgrnd[0] = 0;
        }
        Cgrnd[1] = (int) (i257 + i257 * (Snap[1] / 100.0F));
        if (Cgrnd[1] > 255)
        {
            Cgrnd[1] = 255;
        }
        if (Cgrnd[1] < 0)
        {
            Cgrnd[1] = 0;
        }
        Cgrnd[2] = (int) (i258 + i258 * (Snap[2] / 100.0F));
        if (Cgrnd[2] > 255)
        {
            Cgrnd[2] = 255;
        }
        if (Cgrnd[2] < 0)
        {
            Cgrnd[2] = 0;
        }
        for (var i260 = 0; i260 < 3; i260++)
        {
            Crgrnd[i260] = (int) ((Cpol[i260] * 0.99 + Cgrnd[i260]) / 2.0);
        }
    }

    internal static void Setpolys(int i, int i265, int i266)
    {
        Cpol[0] = (int) (i + i * (Snap[0] / 100.0F));
        if (Cpol[0] > 255)
        {
            Cpol[0] = 255;
        }
        if (Cpol[0] < 0)
        {
            Cpol[0] = 0;
        }
        Cpol[1] = (int) (i265 + i265 * (Snap[1] / 100.0F));
        if (Cpol[1] > 255)
        {
            Cpol[1] = 255;
        }
        if (Cpol[1] < 0)
        {
            Cpol[1] = 0;
        }
        Cpol[2] = (int) (i266 + i266 * (Snap[2] / 100.0F));
        if (Cpol[2] > 255)
        {
            Cpol[2] = 255;
        }
        if (Cpol[2] < 0)
        {
            Cpol[2] = 0;
        }
        for (var i267 = 0; i267 < 3; i267++)
        {
            Crgrnd[i267] = (int) ((Cpol[i267] * 0.99 + Cgrnd[i267]) / 2.0);
        }
    }

    internal static void Setsky(int i, int i249, int i250)
    {
        Osky[0] = i;
        Osky[1] = i249;
        Osky[2] = i250;
        for (var i251 = 0; i251 < 3; i251++)
        {
            Clds[i251] = (Osky[i251] * Cldd[3] + Cldd[i251]) / (Cldd[3] + 1);
            Clds[i251] = (int) (Clds[i251] + Clds[i251] * (Snap[i251] / 100.0F));
            if (Clds[i251] > 255)
            {
                Clds[i251] = 255;
            }
            if (Clds[i251] < 0)
            {
                Clds[i251] = 0;
            }
        }
        Csky[0] = (int) (i + i * (Snap[0] / 100.0F));
        if (Csky[0] > 255)
        {
            Csky[0] = 255;
        }
        if (Csky[0] < 0)
        {
            Csky[0] = 0;
        }
        Csky[1] = (int) (i249 + i249 * (Snap[1] / 100.0F));
        if (Csky[1] > 255)
        {
            Csky[1] = 255;
        }
        if (Csky[1] < 0)
        {
            Csky[1] = 0;
        }
        Csky[2] = (int) (i250 + i250 * (Snap[2] / 100.0F));
        if (Csky[2] > 255)
        {
            Csky[2] = 255;
        }
        if (Csky[2] < 0)
        {
            Csky[2] = 0;
        }
        var fs = new float[3];
        Color.RGBtoHSB(Csky[0], Csky[1], Csky[2], out fs[0], out fs[1], out fs[2]);
        Darksky = fs[2] < 0.6;
    }

    internal static void Setsnap(int i, int i247, int i248)
    {
        Snap[0] = i;
        Snap[1] = i247;
        Snap[2] = i248;
    }

    internal static void Transaround(ContO conto, ContO conto18, int i)
    {
        var i19 = (conto.X * (20 - i) + conto18.X * i) / 20;
        var i20 = (conto.Y * (20 - i) + conto18.Y * i) / 20;
        var i21 = (conto.Z * (20 - i) + conto18.Z * i) / 20;
        if (!Vert)
        {
            Adv += 2;
        }
        else
        {
            Adv -= 2;
        }
        if (Adv > 900)
        {
            Vert = true;
        }
        if (Adv < -500)
        {
            Vert = false;
        }
        var i22 = 500 + Adv;
        if (i22 < 1000)
        {
            i22 = 1000;
        }
        Y = i20 - Adv;
        if (Y > 10)
        {
            Vert = false;
        }
        X = i19 + (int) ((i19 - i22 - i19) * Cos(Vxz));
        Z = i21 + (int) ((i19 - i22 - i19) * Sin(Vxz));
        Vxz += 2;
        var i23 = 0;
        var i24 = Y;
        if (i24 > 0)
        {
            i24 = 0;
        }
        if (i20 - i24 - Cy < 0)
        {
            i23 = -180;
        }
        var i25 = (int) Math.Sqrt((i21 - Z + Cz) * (i21 - Z + Cz) + (i19 - X - Cx) * (i19 - X - Cx));
        var i26 = (int) (90 + i23 - Math.Atan(i25 / (double) (i20 - i24 - Cy)) / 0.017453292519943295);
        Xz = -Vxz + 90;
        Zy += (i26 - Zy) / 10;
    }

    internal static void Watch(ContO conto, int i)
    {
        if (_td)
        {
            Y = (int) (conto.Y - 300 - 1100.0F * Random());
            X = conto.X + (int) ((conto.X + 400 - conto.X) * Cos(i) - (conto.Z + 5000 - conto.Z) * Sin(i));
            Z = conto.Z + (int) ((conto.X + 400 - conto.X) * Sin(i) + (conto.Z + 5000 - conto.Z) * Cos(i));
            _td = false;
        }
        var i0 = 0;
        if (conto.X - X - Cx > 0)
        {
            i0 = 180;
        }
        var i1 = -(int) (90 + i0 + Math.Atan((conto.Z - Z) / (double) (conto.X - X - Cx)) / 0.017453292519943295);
        i0 = 0;
        if (conto.Y - Y - Cy < 0)
        {
            i0 = -180;
        }
        var i2 = (int) Math.Sqrt((conto.Z - Z) * (conto.Z - Z) + (conto.X - X - Cx) * (conto.X - X - Cx));
        var i3 = (int) (90 + i0 - Math.Atan(i2 / (double) (conto.Y - Y - Cy)) / 0.017453292519943295);
        for ( /**/; i1 < 0; i1 += 360)
        {
        }
        for ( /**/; i1 > 360; i1 -= 360)
        {
        }
        Xz = i1;
        Zy += (i3 - Zy) / 5;
        if ((int) Math.Sqrt((conto.Z - Z) * (conto.Z - Z) + (conto.X - X - Cx) * (conto.X - X - Cx) +
                            (conto.Y - Y - Cy) * (conto.Y - Y - Cy)) > 6000)
        {
            _td = true;
        }
    }

    private static int Xs(int i, int i272)
    {
        if (i272 < Cz)
        {
            i272 = Cz;
        }
        return (i272 - FocusPoint) * (Cx - i) / i272 + i;
    }

    private static int Ys(int i, int i273)
    {
        if (i273 < 10)
        {
            i273 = 10;
        }
        return (i273 - FocusPoint) * (Cy - i) / i273 + i;
    }
}