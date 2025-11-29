using System;
using System.Text;
using NFMWorld.Util;
using Random = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

class ContO
{
    internal int Checkpoint;
    internal int Colok;
    private int[] _dam;
    internal bool Decor;
    internal int Disline = 14;
    internal int Disp;
    internal int Dist;
    private readonly int[] _edl;
    private readonly int[] _edr;
    private readonly int[] _elc;
    internal bool Elec;
    internal string Err;
    internal bool Errd;
    internal int Fcnt;
    internal readonly int[] Fcol;
    internal bool Fix;
    internal int Grat;
    internal float Grounded = 1.0F;
    internal readonly int[] Keyx;
    internal readonly int[] Keyz;
    internal int MaxR;
    internal bool Noline;
    private bool[] _notwall;
    internal int Npl;
    internal float[] Osmag;
    internal readonly Plane[] P;
    private bool[] _rbef;
    internal float Rcx;
    internal float Rcy;
    internal float Rcz;
    internal int Roofat;
    internal bool Roted;
    private int[] _rtg;
    private int[] _rx;
    private int[] _ry;
    private int[] _rz;
    private int[] _sav;
    private float[] _sbln;
    internal readonly int[] Scol;
    internal int[] Scx;
    internal int[] Scz;
    internal bool Shadow;
    private int[] _skd;
    private float[,] _smag;
    internal int Sprk;
    private int _sprkat;
    private int[,] _srgb;
    internal int Srx;
    internal int Sry;
    internal int Srz;
    internal int[] Stg;
    internal int[] Sx;
    internal int[] Sy;
    internal int[] Sz;
    private int[,] _tc;
    internal int Tnt;
    private int[] _tradx;
    private int[] _trady;
    private int[] _tradz;
    private int[] _tx;
    private int[] _txy;
    private int[] _ty;
    private int[] _tz;
    private int[] _tzy;
    private int _ust;
    private float[] _vrx;
    private float[] _vry;
    private float[] _vrz;
    internal int Wh;
    internal int Wxz = 0;
    internal int Wzy = 0;
    internal int X;
    internal int Xy;
    internal int Xz;
    internal int Y;
    internal int Z;
    internal int Zy;

    internal ContO(byte[] _is)
    {
        Keyx = new int[4];
        Keyz = new int[4];
        _sprkat = 0;
        Tnt = 0;
        _ust = 0;
        Srx = 0;
        Sry = 0;
        Srz = 0;
        Rcx = 0.0F;
        Rcy = 0.0F;
        Rcz = 0.0F;
        Sprk = 0;
        Elec = false;
        Roted = false;
        _edl = new int[4];
        _edr = new int[4];
        _elc = new[]
        {
            0, 0, 0, 0
        };
        Fix = false;
        Fcnt = 0;
        Checkpoint = 0;
        Fcol = new[]
        {
            0, 0, 0
        };
        Scol = new[]
        {
            0, 0, 0
        };
        Colok = 0;
        Errd = false;
        Err = "";
        Roofat = 0;
        Wh = 0;
        // p = new Plane[286];
        P = new Plane[10000];
        // int[] is0 = new int[286];
        var is0 = new int[10000];
        // for (int i = 0; i < 286; i++)
        // is0[i] = 0;
        int i;
        for (i = 0; i < 10000; i++)
        {
            is0[i] = 0;
        }
        if (Medium.Loadnew)
        {
            for (i = 0; i < 4; i++)
            {
                Keyz[i] = 0;
            }
            Shadow = true;
        }
        var aastring = "";
        var aabool = false;
        var bool1 = false;
        i = 0;
        var f = 1.0F;
        var f2 = 1.0F;
        float[] fs =
        {
            1.0F, 1.0F, 1.0F
        };
        var is3 = new int[8000];
        var is4 = new int[8000];
        var is5 = new int[8000];
        int[] is6 =
        {
            0, 0, 0
        };
        var bool7 = false;
        var wheels = new Wheels();
        var bool8 = false;
        var i9 = 0;
        var i10 = 1;
        var i11 = 0;
        var i12 = 0;
        var i13 = 0;
        var i14 = 0;
        var bool15 = false;
        var bool16 = false;

        var randomcolor = false;
        var randoutline = false;

        try
        {
            foreach (var aline in Encoding.Default.GetString(_is).Split('\n'))
            {
                aastring = aline.Trim();
                if (Npl < 10000 /* 210 */)
                {
                    if (aastring.StartsWith("<p>"))
                    {
                        aabool = true;
                        i = 0;
                        i10 = 0;
                        i11 = 0;
                        i13 = 0;
                        is0[Npl] = 1;
                        if (!bool16)
                        {
                            bool15 = false;
                        }

                        randomcolor = false;
                        randoutline = false;
                    }
                    if (aabool)
                    {
                        if (aastring.StartsWith("gr("))
                        {
                            i10 = Getvalue("gr", aastring, 0);
                        }
                        if (aastring.StartsWith("fs("))
                        {
                            i11 = Getvalue("fs", aastring, 0);
                            is0[Npl] = 2;
                        }
                        if (aastring.StartsWith("c("))
                        {
                            i14 = 0;
                            is6[0] = Getvalue("c", aastring, 0);
                            is6[1] = Getvalue("c", aastring, 1);
                            is6[2] = Getvalue("c", aastring, 2);
                        }
                        if (aastring.StartsWith("glass"))
                        {
                            i14 = 1;
                        }
                        if (aastring.StartsWith("gshadow"))
                        {
                            i14 = 2;
                        }
                        if (aastring.StartsWith("lightF"))
                        {
                            i13 = 1;
                        }
                        if (aastring.StartsWith("light"))
                        {
                            i13 = 1;
                        }
                        if (aastring.StartsWith("lightB"))
                        {
                            i13 = 2;
                        }
                        if (aastring.StartsWith("noOutline"))
                        {
                            bool15 = true;
                        }
//                        if (aastring.StartsWith("random()") || aastring.StartsWith("rainbow()")) {
//                            randomcolor = true;
//                        }
//                        if (aastring.StartsWith("randoutline()")) {
//                            randoutline = true;
//                        }
                        if (aastring.StartsWith("p("))
                        {
                            is3[i] = (int) (Getvalue("p", aastring, 0) * f * f2 * fs[0]);
                            is4[i] = (int) (Getvalue("p", aastring, 1) * f * fs[1]);
                            is5[i] = (int) (Getvalue("p", aastring, 2) * f * fs[2]);
                            var i18 = (int) Math.Sqrt(is3[i] * is3[i] + is4[i] * is4[i] + is5[i] * is5[i]);
                            if (i18 > MaxR)
                            {
                                MaxR = i18;
                            }
                            i++;
                        }
                    }
                    if (aastring.StartsWith("</p>"))
                    {
                        P[Npl] = new Plane(is3, is5, is4, i, is6, i14, i10, i11, 0, 0, 0, Disline, 0, bool7, i13,
                            bool15);
                        if (is6[0] == Fcol[0] && is6[1] == Fcol[1] && is6[2] == Fcol[2] && i14 == 0)
                        {
                            P[Npl].Colnum = 1;
                        }
                        if (is6[0] == Scol[0] && is6[1] == Scol[1] && is6[2] == Scol[2] && i14 == 0)
                        {
                            P[Npl].Colnum = 2;
                        }
                        Npl++;
                        aabool = false;
                    }
                }
                if (aastring.StartsWith("rims("))
                {
                    wheels.Setrims(Getvalue("rims", aastring, 0), Getvalue("rims", aastring, 1),
                        Getvalue("rims", aastring, 2), Getvalue("rims", aastring, 3),
                        Getvalue("rims", aastring, 4));
                }
                if (aastring.StartsWith("w(") && i9 < 4)
                {
                    Keyx[i9] = (int) (Getvalue("w", aastring, 0) * f * fs[0]);
                    Keyz[i9] = (int) (Getvalue("w", aastring, 2) * f * fs[2]);
                    wheels.Make(P, Npl, (int) (Getvalue("w", aastring, 0) * f * f2 * fs[0]),
                        (int) (Getvalue("w", aastring, 1) * f * fs[1]),
                        (int) (Getvalue("w", aastring, 2) * f * fs[2]), Getvalue("w", aastring, 3),
                        (int) (Getvalue("w", aastring, 4) * f * f2), (int) (Getvalue("w", aastring, 5) * f), i12);
                    Npl += 19;
                    if (Medium.Loadnew)
                    {
                        Wh += (int) (Getvalue("w", aastring, 5) * f);
                        if (wheels.Ground > 140)
                        {
                            var string19 = "FRONT";
                            if (Keyz[i9] < 0)
                            {
                                string19 = "BACK";
                            }
                            Err = "Wheels Error:\n" + string19 +
                                  " Wheels floor ais too far below the center of Y Axis of the car!    \n\nPlease decrease the Y value of the " +
                                  string19 + " Wheels or decrease its height.     \n \n";
                            Errd = true;
                            Keyz[i9] = 0;
                            Keyx[i9] = 0;
                        }
                        if (wheels.Ground < -100)
                        {
                            var string20 = "FRONT";
                            if (Keyz[i9] < 0)
                            {
                                string20 = "BACK";
                            }
                            Err = "Wheels Error:\n" + string20 +
                                  " Wheels floor ais too far above the center of Y Axis of the car!    \n\nPlease increase the Y value of the " +
                                  string20 + " Wheels or increase its height.     \n \n";
                            Errd = true;
                            Keyz[i9] = 0;
                            Keyx[i9] = 0;
                        }
                        if (Math.Abs(Keyx[i9]) > 400)
                        {
                            var string21 = "FRONT";
                            if (Keyz[i9] < 0)
                            {
                                string21 = "BACK";
                            }
                            Err = "Wheels Error:\n" + string21 +
                                  " Wheels are too far apart!    \n\nPlease decrease the \u00b1X value of the " +
                                  string21 + " Wheels.     \n \n";
                            Errd = true;
                            Keyz[i9] = 0;
                            Keyx[i9] = 0;
                        }
                        if (Math.Abs(Keyz[i9]) > 700)
                        {
                            if (Keyz[i9] < 0)
                            {
                                Err =
                                    "Wheels Error:\nBACK Wheels are too far backwards from the center of the Z Axis!    \n\nPlease increase the -Z value of the BACK Wheels.     \n \n";
                            }
                            else
                            {
                                Err =
                                    "Wheels Error:\nFRONT Wheels are too far forwards from the center of the Z Axis!    \n\nPlease decrease the +Z value of the FRONT Wheels.     \n \n";
                            }
                            Errd = true;
                            Keyz[i9] = 0;
                            Keyx[i9] = 0;
                        }
                        if ((int) (Getvalue("w", aastring, 4) * f * f2) > 300)
                        {
                            var string22 = "FRONT";
                            if (Keyz[i9] < 0)
                            {
                                string22 = "BACK";
                            }
                            Err = "Wheels Error:\nWidth of the " + string22 +
                                  " Wheels ais too large!    \n\nPlease decrease the width of the " + string22 +
                                  " Wheels.     \n \n";
                            Errd = true;
                            Keyz[i9] = 0;
                            Keyx[i9] = 0;
                        }
                    }
                    i9++;
                }
                if (aastring.StartsWith("tracks"))
                {
                    var i23 = Getvalue("tracks", aastring, 0);
                    _txy = new int[i23];
                    _tzy = new int[i23];
                    _tc = new int[i23, 3];
                    _tradx = new int[i23];
                    _tradz = new int[i23];
                    _trady = new int[i23];
                    _tx = new int[i23];
                    _ty = new int[i23];
                    _tz = new int[i23];
                    _skd = new int[i23];
                    _dam = new int[i23];
                    _notwall = new bool[i23];
                    bool8 = true;
                }
                if (bool8)
                {
                    if (aastring.StartsWith("<track>"))
                    {
                        bool1 = true;
                        _notwall[Tnt] = false;
                        _dam[Tnt] = 1;
                        _skd[Tnt] = 0;
                        _ty[Tnt] = 0;
                        _tx[Tnt] = 0;
                        _tz[Tnt] = 0;
                        _txy[Tnt] = 0;
                        _tzy[Tnt] = 0;
                        _trady[Tnt] = 0;
                        _tradx[Tnt] = 0;
                        _tradz[Tnt] = 0;
                        _tc[Tnt, 0] = 0;
                        _tc[Tnt, 1] = 0;
                        _tc[Tnt, 2] = 0;
                    }
                    if (bool1)
                    {
                        if (aastring.StartsWith("c"))
                        {
                            _tc[Tnt, 0] = Getvalue("c", aastring, 0);
                            _tc[Tnt, 1] = Getvalue("c", aastring, 1);
                            _tc[Tnt, 2] = Getvalue("c", aastring, 2);
                        }
                        if (aastring.StartsWith("xy"))
                        {
                            _txy[Tnt] = Getvalue("xy", aastring, 0);
                        }
                        if (aastring.StartsWith("zy"))
                        {
                            _tzy[Tnt] = Getvalue("zy", aastring, 0);
                        }
                        if (aastring.StartsWith("radx"))
                        {
                            _tradx[Tnt] = (int) (Getvalue("radx", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("rady"))
                        {
                            _trady[Tnt] = (int) (Getvalue("rady", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("radz"))
                        {
                            _tradz[Tnt] = (int) (Getvalue("radz", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("ty"))
                        {
                            _ty[Tnt] = (int) (Getvalue("ty", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("tx"))
                        {
                            _tx[Tnt] = (int) (Getvalue("tx", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("tz"))
                        {
                            _tz[Tnt] = (int) (Getvalue("tz", aastring, 0) * f);
                        }
                        if (aastring.StartsWith("skid"))
                        {
                            _skd[Tnt] = Getvalue("skid", aastring, 0);
                        }
                        if (aastring.StartsWith("dam"))
                        {
                            _dam[Tnt] = 3;
                        }
                        if (aastring.StartsWith("notwall"))
                        {
                            _notwall[Tnt] = true;
                        }
                    }
                    if (aastring.StartsWith("</track>"))
                    {
                        bool1 = false;
                        Tnt++;
                    }
                }
                if (aastring.StartsWith("disp("))
                {
                    Disp = Getvalue("disp", aastring, 0);
                }
                if (aastring.StartsWith("disline("))
                {
                    Disline = Getvalue("disline", aastring, 0) * 2;
                }
                if (aastring.StartsWith("shadow"))
                {
                    Shadow = true;
                }
                if (aastring.StartsWith("stonecold"))
                {
                    Noline = true;
                }
                if (aastring.StartsWith("newstone"))
                {
                    Noline = true;
                    bool15 = true;
                    bool16 = true;
                }
                if (aastring.StartsWith("decorative"))
                {
                    Decor = true;
                }
                if (aastring.StartsWith("road"))
                {
                    bool7 = true;
                }
                if (aastring.StartsWith("notroad"))
                {
                    bool7 = false;
                }
                if (aastring.StartsWith("grounded("))
                {
                    Grounded = Getvalue("grounded", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("div("))
                {
                    f = Getvalue("div", aastring, 0) / 10.0F;
                }
                if (aastring.StartsWith("idiv("))
                {
                    f = Getvalue("idiv", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("iwid("))
                {
                    f2 = Getvalue("iwid", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("ScaleX("))
                {
                    fs[0] = Getvalue("ScaleX", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("ScaleY("))
                {
                    fs[1] = Getvalue("ScaleY", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("ScaleZ("))
                {
                    fs[2] = Getvalue("ScaleZ", aastring, 0) / 100.0F;
                }
                if (aastring.StartsWith("gwgr("))
                {
                    i12 = Getvalue("gwgr", aastring, 0);
                    if (Medium.Loadnew)
                    {
                        if (i12 > 40)
                        {
                            i12 = 40;
                        }
                        if (i12 < 0 && i12 >= -15)
                        {
                            i12 = -16;
                        }
                        if (i12 < -40)
                        {
                            i12 = -40;
                        }
                    }
                }
                if (aastring.StartsWith("1stColor("))
                {
                    Fcol[0] = Getvalue("1stColor", aastring, 0);
                    Fcol[1] = Getvalue("1stColor", aastring, 1);
                    Fcol[2] = Getvalue("1stColor", aastring, 2);
                    Colok++;
                }
                if (aastring.StartsWith("2ndColor("))
                {
                    Scol[0] = Getvalue("2ndColor", aastring, 0);
                    Scol[1] = Getvalue("2ndColor", aastring, 1);
                    Scol[2] = Getvalue("2ndColor", aastring, 2);
                    Colok++;
                }
            }
        }
        catch (Exception exception)
        {
            if (!Errd)
            {
                Err = "Error While Loading 3D Model\n\nLine:     " + aastring + "\n\nError Detail:\n" + exception +
                      "           \n \n";
                Console.WriteLine(Err);
                Errd = true;
            }
        }
        Grat = wheels.Ground;
        _sprkat = wheels.Sparkat;
        if (Shadow)
        {
            Stg = new int[20];
            _rtg = new int[100];
            for (var i24 = 0; i24 < 20; i24++)
            {
                Stg[i24] = 0;
            }
            for (var i25 = 0; i25 < 100; i25++)
            {
                _rtg[i25] = 0;
            }
        }
        if (Medium.Loadnew)
        {
            if (i9 != 0)
            {
                Wh = Wh / i9;
            }
            var bool26 = false;
            for (var i27 = 0; i27 < Npl; i27++)
            {
                var i28 = 0;
                var i29 = P[i27].Ox[0];
                var i30 = P[i27].Ox[0];
                var i31 = P[i27].Oy[0];
                var i32 = P[i27].Oy[0];
                var i33 = P[i27].Oz[0];
                var i34 = P[i27].Oz[0];
                for (var i35 = 0; i35 < P[i27].N; i35++)
                {
                    if (P[i27].Ox[i35] > i29)
                    {
                        i29 = P[i27].Ox[i35];
                    }
                    if (P[i27].Ox[i35] < i30)
                    {
                        i30 = P[i27].Ox[i35];
                    }
                    if (P[i27].Oy[i35] > i31)
                    {
                        i31 = P[i27].Oy[i35];
                    }
                    if (P[i27].Oy[i35] < i32)
                    {
                        i32 = P[i27].Oy[i35];
                    }
                    if (P[i27].Oz[i35] > i33)
                    {
                        i33 = P[i27].Oz[i35];
                    }
                    if (P[i27].Oz[i35] < i34)
                    {
                        i34 = P[i27].Oz[i35];
                    }
                }
                if (Math.Abs(i29 - i30) <= Math.Abs(i31 - i32) && Math.Abs(i29 - i30) <= Math.Abs(i33 - i34))
                {
                    i28 = 1;
                }
                if (Math.Abs(i31 - i32) <= Math.Abs(i29 - i30) && Math.Abs(i31 - i32) <= Math.Abs(i33 - i34))
                {
                    i28 = 2;
                }
                if (Math.Abs(i33 - i34) <= Math.Abs(i29 - i30) && Math.Abs(i33 - i34) <= Math.Abs(i31 - i32))
                {
                    i28 = 3;
                }
                if (i28 == 2 && (!bool26 || (i31 + i32) / 2 < Roofat))
                {
                    Roofat = (i31 + i32) / 2;
                    bool26 = true;
                }
                if (is0[i27] == 1)
                {
                    var i36 = 1000;
                    var i37 = 0;
                    for (var i38 = 0; i38 < P[i27].N; i38++)
                    {
                        var i39 = i38 + 1;
                        if (i39 >= P[i27].N)
                        {
                            i39 -= P[i27].N;
                        }
                        var i40 = i38 + 2;
                        if (i40 >= P[i27].N)
                        {
                            i40 -= P[i27].N;
                        }
                        if (i28 == 1)
                        {
                            var i41 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oz[i38] - P[i27].Oz[i39]) /
                                                 (double) (P[i27].Oy[i38] - P[i27].Oy[i39])) /
                                       0.017453292519943295));
                            var i42 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oz[i40] - P[i27].Oz[i39]) /
                                                 (double) (P[i27].Oy[i40] - P[i27].Oy[i39])) /
                                       0.017453292519943295));
                            if (i41 > 45)
                            {
                                i41 = 90 - i41;
                            }
                            else
                            {
                                i42 = 90 - i42;
                            }
                            if (i41 + i42 < i36)
                            {
                                i36 = i41 + i42;
                                i37 = i38;
                            }
                        }
                        if (i28 == 2)
                        {
                            var i43 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oz[i38] - P[i27].Oz[i39]) /
                                                 (double) (P[i27].Ox[i38] - P[i27].Ox[i39])) /
                                       0.017453292519943295));
                            var i44 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oz[i40] - P[i27].Oz[i39]) /
                                                 (double) (P[i27].Ox[i40] - P[i27].Ox[i39])) /
                                       0.017453292519943295));
                            if (i43 > 45)
                            {
                                i43 = 90 - i43;
                            }
                            else
                            {
                                i44 = 90 - i44;
                            }
                            if (i43 + i44 < i36)
                            {
                                i36 = i43 + i44;
                                i37 = i38;
                            }
                        }
                        if (i28 == 3)
                        {
                            var i45 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oy[i38] - P[i27].Oy[i39]) /
                                                 (double) (P[i27].Ox[i38] - P[i27].Ox[i39])) /
                                       0.017453292519943295));
                            var i46 = Math.Abs(
                                (int) (Math.Atan((P[i27].Oy[i40] - P[i27].Oy[i39]) /
                                                 (double) (P[i27].Ox[i40] - P[i27].Ox[i39])) /
                                       0.017453292519943295));
                            if (i45 > 45)
                            {
                                i45 = 90 - i45;
                            }
                            else
                            {
                                i46 = 90 - i46;
                            }
                            if (i45 + i46 < i36)
                            {
                                i36 = i45 + i46;
                                i37 = i38;
                            }
                        }
                    }
                    if (i37 != 0)
                    {
                        var is47 = new int[P[i27].N];
                        var is48 = new int[P[i27].N];
                        var is49 = new int[P[i27].N];
                        for (var i50 = 0; i50 < P[i27].N; i50++)
                        {
                            is47[i50] = P[i27].Ox[i50];
                            is48[i50] = P[i27].Oy[i50];
                            is49[i50] = P[i27].Oz[i50];
                        }
                        for (var i51 = 0; i51 < P[i27].N; i51++)
                        {
                            var i52 = i51 + i37;
                            if (i52 >= P[i27].N)
                            {
                                i52 -= P[i27].N;
                            }
                            P[i27].Ox[i51] = is47[i52];
                            P[i27].Oy[i51] = is48[i52];
                            P[i27].Oz[i51] = is49[i52];
                        }
                    }
                    if (i28 == 1)
                    {
                        if (Math.Abs(P[i27].Oz[0] - P[i27].Oz[1]) > Math.Abs(P[i27].Oy[0] - P[i27].Oy[1]))
                        {
                            if (P[i27].Oz[0] > P[i27].Oz[1])
                            {
                                if (P[i27].Oy[1] > P[i27].Oy[2])
                                {
                                    P[i27].Fs = 1;
                                }
                                else
                                {
                                    P[i27].Fs = -1;
                                }
                            }
                            else if (P[i27].Oy[1] > P[i27].Oy[2])
                            {
                                P[i27].Fs = -1;
                            }
                            else
                            {
                                P[i27].Fs = 1;
                            }
                        }
                        else if (P[i27].Oy[0] > P[i27].Oy[1])
                        {
                            if (P[i27].Oz[1] > P[i27].Oz[2])
                            {
                                P[i27].Fs = -1;
                            }
                            else
                            {
                                P[i27].Fs = 1;
                            }
                        }
                        else if (P[i27].Oz[1] > P[i27].Oz[2])
                        {
                            P[i27].Fs = 1;
                        }
                        else
                        {
                            P[i27].Fs = -1;
                        }
                    }

                    if (i28 == 2)
                    {
                        if (Math.Abs(P[i27].Oz[0] - P[i27].Oz[1]) > Math.Abs(P[i27].Ox[0] - P[i27].Ox[1]))
                        {
                            if (P[i27].Oz[0] > P[i27].Oz[1])
                            {
                                if (P[i27].Ox[1] > P[i27].Ox[2])
                                {
                                    P[i27].Fs = -1;
                                }
                                else
                                {
                                    P[i27].Fs = 1;
                                }
                            }
                            else if (P[i27].Ox[1] > P[i27].Ox[2])
                            {
                                P[i27].Fs = 1;
                            }
                            else
                            {
                                P[i27].Fs = -1;
                            }
                        }
                        else if (P[i27].Ox[0] > P[i27].Ox[1])
                        {
                            if (P[i27].Oz[1] > P[i27].Oz[2])
                            {
                                P[i27].Fs = 1;
                            }
                            else
                            {
                                P[i27].Fs = -1;
                            }
                        }
                        else if (P[i27].Oz[1] > P[i27].Oz[2])
                        {
                            P[i27].Fs = -1;
                        }
                        else
                        {
                            P[i27].Fs = 1;
                        }
                    }

                    if (i28 == 3)
                    {
                        if (Math.Abs(P[i27].Oy[0] - P[i27].Oy[1]) > Math.Abs(P[i27].Ox[0] - P[i27].Ox[1]))
                        {
                            if (P[i27].Oy[0] > P[i27].Oy[1])
                            {
                                if (P[i27].Ox[1] > P[i27].Ox[2])
                                {
                                    P[i27].Fs = 1;
                                }
                                else
                                {
                                    P[i27].Fs = -1;
                                }
                            }
                            else if (P[i27].Ox[1] > P[i27].Ox[2])
                            {
                                P[i27].Fs = -1;
                            }
                            else
                            {
                                P[i27].Fs = 1;
                            }
                        }
                        else if (P[i27].Ox[0] > P[i27].Ox[1])
                        {
                            if (P[i27].Oy[1] > P[i27].Oy[2])
                            {
                                P[i27].Fs = -1;
                            }
                            else
                            {
                                P[i27].Fs = 1;
                            }
                        }
                        else if (P[i27].Oy[1] > P[i27].Oy[2])
                        {
                            P[i27].Fs = 1;
                        }
                        else
                        {
                            P[i27].Fs = -1;
                        }
                    }

                    var bool53 = false;
                    var bool54 = false;
                    for (var i55 = 0; i55 < Npl; i55++)
                    {
                        if (i55 != i27 && is0[i55] != 0)
                        {
                            var i57 = P[i55].Ox[0];
                            var i58 = P[i55].Ox[0];
                            var i59 = P[i55].Oy[0];
                            var i60 = P[i55].Oy[0];
                            var i61 = P[i55].Oz[0];
                            var i62 = P[i55].Oz[0];
                            for (var i63 = 0; i63 < P[i55].N; i63++)
                            {
                                if (P[i55].Ox[i63] > i57)
                                {
                                    i57 = P[i55].Ox[i63];
                                }
                                if (P[i55].Ox[i63] < i58)
                                {
                                    i58 = P[i55].Ox[i63];
                                }
                                if (P[i55].Oy[i63] > i59)
                                {
                                    i59 = P[i55].Oy[i63];
                                }
                                if (P[i55].Oy[i63] < i60)
                                {
                                    i60 = P[i55].Oy[i63];
                                }
                                if (P[i55].Oz[i63] > i61)
                                {
                                    i61 = P[i55].Oz[i63];
                                }
                                if (P[i55].Oz[i63] < i62)
                                {
                                    i62 = P[i55].Oz[i63];
                                }
                            }
                            var i64 = (i57 + i58) / 2;
                            var i65 = (i59 + i60) / 2;
                            var i66 = (i61 + i62) / 2;
                            var i67 = (i29 + i30) / 2;
                            var i68 = (i31 + i32) / 2;
                            var i69 = (i33 + i34) / 2;
                            if (i28 == 1 && (i65 <= i31 && i65 >= i32 && i66 <= i33 && i66 >= i34 ||
                                             i68 <= i59 && i68 >= i60 && i69 <= i61 && i69 >= i62))
                            {
                                if (i57 < i30)
                                {
                                    bool53 = true;
                                }
                                if (i58 > i29)
                                {
                                    bool54 = true;
                                }
                            }
                            if (i28 == 2 && (i64 <= i29 && i64 >= i30 && i66 <= i33 && i66 >= i34 ||
                                             i67 <= i57 && i67 >= i58 && i69 <= i61 && i69 >= i62))
                            {
                                if (i59 < i32)
                                {
                                    bool53 = true;
                                }
                                if (i60 > i31)
                                {
                                    bool54 = true;
                                }
                            }
                            if (i28 == 3 && (i64 <= i29 && i64 >= i30 && i65 <= i31 && i65 >= i32 ||
                                             i67 <= i57 && i67 >= i58 && i68 <= i59 && i68 >= i60))
                            {
                                if (i61 < i34)
                                {
                                    bool53 = true;
                                }
                                if (i62 > i33)
                                {
                                    bool54 = true;
                                }
                            }
                        }
                        if (bool53 && bool54)
                        {
                            break;
                        }
                    }
                    var bool70 = false;
                    if (bool53 && !bool54)
                    {
                        bool70 = true;
                    }
                    if (bool54 && !bool53)
                    {
                        P[i27].Fs *= -1;
                        bool70 = true;
                    }
                    if (bool53 && bool54)
                    {
                        P[i27].Fs = 0;
                        P[i27].Gr = 40;
                        bool70 = true;
                    }
                    if (!bool70)
                    {
                        var i71 = 0;
                        var i72 = 0;
                        if (i28 == 1)
                        {
                            i71 = (i29 + i30) / 2;
                            i72 = i71;
                        }
                        if (i28 == 2)
                        {
                            i71 = (i31 + i32) / 2;
                            i72 = i71;
                        }
                        if (i28 == 3)
                        {
                            i71 = (i33 + i34) / 2;
                            i72 = i71;
                        }
                        for (var i73 = 0; i73 < Npl; i73++)
                        {
                            if (i73 != i27)
                            {
                                var bool74 = false;
                                var bools = new bool[P[i73].N];
                                for (var i75 = 0; i75 < P[i73].N; i75++)
                                {
                                    bools[i75] = false;
                                    for (var i76 = 0; i76 < P[i27].N; i76++)
                                    {
                                        if (P[i27].Ox[i76] == P[i73].Ox[i75] && P[i27].Oy[i76] == P[i73].Oy[i75] &&
                                            P[i27].Oz[i76] == P[i73].Oz[i75])
                                        {
                                            bools[i75] = true;
                                            bool74 = true;
                                        }
                                    }
                                }
                                if (bool74)
                                {
                                    for (var i77 = 0; i77 < P[i73].N; i77++)
                                    {
                                        if (!bools[i77])
                                        {
                                            if (i28 == 1)
                                            {
                                                if (P[i73].Ox[i77] > i71)
                                                {
                                                    i71 = P[i73].Ox[i77];
                                                }
                                                if (P[i73].Ox[i77] < i72)
                                                {
                                                    i72 = P[i73].Ox[i77];
                                                }
                                            }
                                            if (i28 == 2)
                                            {
                                                if (P[i73].Oy[i77] > i71)
                                                {
                                                    i71 = P[i73].Oy[i77];
                                                }
                                                if (P[i73].Oy[i77] < i72)
                                                {
                                                    i72 = P[i73].Oy[i77];
                                                }
                                            }
                                            if (i28 == 3)
                                            {
                                                if (P[i73].Oz[i77] > i71)
                                                {
                                                    i71 = P[i73].Oz[i77];
                                                }
                                                if (P[i73].Oz[i77] < i72)
                                                {
                                                    i72 = P[i73].Oz[i77];
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }

                        if (i28 == 1)
                        {
                            if ((i71 + i72) / 2 > (i29 + i30) / 2)
                            {
                                P[i27].Fs *= -1;
                            }
                            else if ((i71 + i72) / 2 == (i29 + i30) / 2 && (i29 + i30) / 2 < 0)
                            {
                                P[i27].Fs *= -1;
                            }
                        }

                        if (i28 == 2)
                        {
                            if ((i71 + i72) / 2 > (i31 + i32) / 2)
                            {
                                P[i27].Fs *= -1;
                            }
                            else if ((i71 + i72) / 2 == (i31 + i32) / 2 && (i31 + i32) / 2 < 0)
                            {
                                P[i27].Fs *= -1;
                            }
                        }

                        if (i28 == 3)
                        {
                            if ((i71 + i72) / 2 > (i33 + i34) / 2)
                            {
                                P[i27].Fs *= -1;
                            }
                            else if ((i71 + i72) / 2 == (i33 + i34) / 2 && (i33 + i34) / 2 < 0)
                            {
                                P[i27].Fs *= -1;
                            }
                        }
                    }
                    P[i27].Deltafntyp();
                }
            }
        }
    }

    internal ContO(ContO conto78, int toX, int toY, int toZ, int i81)
    {
        Keyx = new int[4];
        Keyz = new int[4];
        _sprkat = 0;
        Tnt = 0;
        _ust = 0;
        Srx = 0;
        Sry = 0;
        Srz = 0;
        Rcx = 0.0F;
        Rcy = 0.0F;
        Rcz = 0.0F;
        Sprk = 0;
        Elec = false;
        Roted = false;
        _edl = new int[4];
        _edr = new int[4];
        _elc = new[]
        {
            0, 0, 0, 0
        };
        Fix = false;
        Fcnt = 0;
        Checkpoint = 0;
        Fcol = new[]
        {
            0, 0, 0
        };
        Scol = new[]
        {
            0, 0, 0
        };
        Colok = 0;
        Errd = false;
        Err = "";
        Roofat = 0;
        Wh = 0;
        Npl = conto78.Npl;
        MaxR = conto78.MaxR;
        Disp = conto78.Disp;
        Disline = conto78.Disline;
        Noline = conto78.Noline;
        Shadow = conto78.Shadow;
        Grounded = conto78.Grounded;
        Decor = conto78.Decor;
        if (Medium.Loadnew && (i81 == 90 || i81 == -90))
        {
            Grounded += 10000.0F;
        }
        Grat = conto78.Grat;
        _sprkat = conto78._sprkat;
        P = new Plane[conto78.Npl];
        for (var i82 = 0; i82 < Npl; i82++)
        {
            if (conto78.P[i82].Master == 1)
            {
                conto78.P[i82].N = 20;
            }
            P[i82] = new Plane(conto78.P[i82].Ox, conto78.P[i82].Oz, conto78.P[i82].Oy, conto78.P[i82].N,
                conto78.P[i82].Oc, conto78.P[i82].Glass, conto78.P[i82].Gr, conto78.P[i82].Fs, conto78.P[i82].Wx,
                conto78.P[i82].Wy, conto78.P[i82].Wz, conto78.Disline, conto78.P[i82].Bfase, conto78.P[i82].Road,
                conto78.P[i82].Light, conto78.P[i82].Solo);
            P[i82].Project = conto78.P[i82].Project;
        }
        X = toX;
        Y = toY;
        Z = toZ;
        Xz = 0;
        Xy = 0;
        Zy = 0;
        for (var i83 = 0; i83 < Npl; i83++)
        {
            P[i83].Colnum = conto78.P[i83].Colnum;
            P[i83].Master = conto78.P[i83].Master;
            Plane.Rot(P[i83].Ox, P[i83].Oz, 0, 0, i81, P[i83].N);
            P[i83].Loadprojf();
        }
        if (conto78.Tnt != 0)
        {
            for (var i84 = 0; i84 < conto78.Tnt; i84++)
            {
                Trackers.Xy[Trackers.Nt] =
                    (int) (conto78._txy[i84] * Medium.Cos(i81) - conto78._tzy[i84] * Medium.Sin(i81));
                Trackers.Zy[Trackers.Nt] =
                    (int) (conto78._tzy[i84] * Medium.Cos(i81) + conto78._txy[i84] * Medium.Sin(i81));
                for (var i85 = 0; i85 < 3; i85++)
                {
                    Trackers.C[Trackers.Nt][i85] =
                        (int) (conto78._tc[i84, i85] + conto78._tc[i84, i85] * (Medium.Snap[i85] / 100.0F));
                    if (Trackers.C[Trackers.Nt][i85] > 255)
                    {
                        Trackers.C[Trackers.Nt][i85] = 255;
                    }
                    if (Trackers.C[Trackers.Nt][i85] < 0)
                    {
                        Trackers.C[Trackers.Nt][i85] = 0;
                    }
                }
                Trackers.X[Trackers.Nt] =
                    (int) (X + conto78._tx[i84] * Medium.Cos(i81) - conto78._tz[i84] * Medium.Sin(i81));
                Trackers.Z[Trackers.Nt] =
                    (int) (Z + conto78._tz[i84] * Medium.Cos(i81) + conto78._tx[i84] * Medium.Sin(i81));
                Trackers.Y[Trackers.Nt] = Y + conto78._ty[i84];
                Trackers.Skd[Trackers.Nt] = conto78._skd[i84];
                Trackers.Dam[Trackers.Nt] = conto78._dam[i84];
                Trackers.Notwall[Trackers.Nt] = conto78._notwall[i84];
                Trackers.Decor[Trackers.Nt] = Decor;
                var i86 = Math.Abs(i81);
                if (i86 == 180)
                {
                    i86 = 0;
                }
                Trackers.Radx[Trackers.Nt] =
                    (int) Math.Abs(conto78._tradx[i84] * Medium.Cos(i86) + conto78._tradz[i84] * Medium.Sin(i86));
                Trackers.Radz[Trackers.Nt] =
                    (int) Math.Abs(conto78._tradx[i84] * Medium.Sin(i86) + conto78._tradz[i84] * Medium.Cos(i86));
                Trackers.Rady[Trackers.Nt] = conto78._trady[i84];
                Trackers.Nt++;
            }
        }
        for (var i87 = 0; i87 < 4; i87++)
        {
            Keyx[i87] = conto78.Keyx[i87];
            Keyz[i87] = conto78.Keyz[i87];
        }
        if (Shadow)
        {
            Stg = new int[20];
            Sx = new int[20];
            Sy = new int[20];
            Sz = new int[20];
            Scx = new int[20];
            Scz = new int[20];
            Osmag = new float[20];
            _sav = new int[20];
            _smag = new float[20, 8];
            _srgb = new int[20, 3];
            _sbln = new float[20];
            _ust = 0;
            for (var i88 = 0; i88 < 20; i88++)
            {
                Stg[i88] = 0;
            }
            _rtg = new int[100];
            _rbef = new bool[100];
            _rx = new int[100];
            _ry = new int[100];
            _rz = new int[100];
            _vrx = new float[100];
            _vry = new float[100];
            _vrz = new float[100];
            for (var i89 = 0; i89 < 100; i89++)
            {
                _rtg[i89] = 0;
            }
        }
    }

    internal ContO(int i, int i90, int i91, int i92, int i93, int i94)
    {
        Keyx = new int[4];
        Keyz = new int[4];
        _sprkat = 0;
        Tnt = 0;
        _ust = 0;
        Srx = 0;
        Sry = 0;
        Srz = 0;
        Rcx = 0.0F;
        Rcy = 0.0F;
        Rcz = 0.0F;
        Sprk = 0;
        Elec = false;
        Roted = false;
        _edl = new int[4];
        _edr = new int[4];
        _elc = new[]
        {
            0, 0, 0, 0
        };
        Fix = false;
        Fcnt = 0;
        Checkpoint = 0;
        Fcol = new[]
        {
            0, 0, 0
        };
        Scol = new[]
        {
            0, 0, 0
        };
        Colok = 0;
        Errd = false;
        Err = "";
        Roofat = 0;
        Wh = 0;
        X = i92;
        Z = i93;
        Y = i94;
        Xz = 0;
        Xy = 0;
        Zy = 0;
        Grat = 0;
        _sprkat = 0;
        Disline = 4;
        Noline = true;
        Shadow = false;
        Grounded = 115.0F;
        Decor = true;
        Npl = 5;
        P = new Plane[5];
        var random = new Random(i);
        var ais = new int[8];
        var is95 = new int[8];
        var is96 = new int[8];
        var is97 = new int[8];
        var is98 = new int[8];
        float f = i90;
        float f99 = i91;
        if (f99 < 2.0F)
        {
            f99 = 2.0F;
        }
        if (f99 > 6.0F)
        {
            f99 = 6.0F;
        }
        if (f < 2.0F)
        {
            f = 2.0F;
        }
        if (f > 6.0F)
        {
            f = 6.0F;
        }
        f /= 1.5F;
        f99 /= 1.5F;
        f99 *= 1.0F + (f - 2.0F) * 0.1786F;
        var f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[0] = -(int) (f100 * f * 0.7071F);
        is95[0] = (int) (f100 * f * 0.7071F);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[1] = 0;
        is95[1] = (int) (f100 * f);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[2] = (int) (f100 * f * 0.7071);
        is95[2] = (int) (f100 * f * 0.7071);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[3] = (int) (f100 * f);
        is95[3] = 0;
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[4] = (int) (f100 * f * 0.7071);
        is95[4] = -(int) (f100 * f * 0.7071);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[5] = 0;
        is95[5] = -(int) (f100 * f);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[6] = -(int) (f100 * f * 0.7071);
        is95[6] = -(int) (f100 * f * 0.7071);
        f100 = (float) (50.0 + 100.0 * random.NextDouble());
        ais[7] = -(int) (f100 * f);
        is95[7] = 0;
        for (var i101 = 0; i101 < 8; i101++)
        {
            is96[i101] = (int) (ais[i101] * (0.2 + 0.4 * random.NextDouble()));
            is97[i101] = (int) (is95[i101] * (0.2 + 0.4 * random.NextDouble()));
            is98[i101] = -(int) ((10.0 + 15.0 * random.NextDouble()) * f99);
        }
        MaxR = 0;
        for (var i102 = 0; i102 < 8; i102++)
        {
            var i103 = i102 - 1;
            if (i103 == -1)
            {
                i103 = 7;
            }
            var i104 = i102 + 1;
            if (i104 == 8)
            {
                i104 = 0;
            }
            ais[i102] = ((ais[i103] + ais[i104]) / 2 + ais[i102]) / 2;
            is95[i102] = ((is95[i103] + is95[i104]) / 2 + is95[i102]) / 2;
            is96[i102] = ((is96[i103] + is96[i104]) / 2 + is96[i102]) / 2;
            is97[i102] = ((is97[i103] + is97[i104]) / 2 + is97[i102]) / 2;
            is98[i102] = ((is98[i103] + is98[i104]) / 2 + is98[i102]) / 2;
            var i105 = (int) Math.Sqrt(ais[i102] * ais[i102] + is95[i102] * is95[i102]);
            if (i105 > MaxR)
            {
                MaxR = i105;
            }
            i105 = (int) Math.Sqrt(is96[i102] * is96[i102] + is98[i102] * is98[i102] + is97[i102] * is97[i102]);
            if (i105 > MaxR)
            {
                MaxR = i105;
            }
        }
        Disp = MaxR / 17;
        var is106 = new int[3];
        var f107 = -1.0F;
        var f108 = (f / f99 - 0.33F) / 33.4F;
        if (f108 < 0.005)
        {
            f108 = 0.0F;
        }
        if (f108 > 0.057)
        {
            f108 = 0.057F;
        }
        for (var i109 = 0; i109 < 4; i109++)
        {
            var i110 = i109 * 2;
            var i111 = i110 + 2;
            if (i111 == 8)
            {
                i111 = 0;
            }
            var is112 = new int[6];
            var is113 = new int[6];
            var is114 = new int[6];
            is112[0] = ais[i110];
            is112[1] = ais[i110 + 1];
            is112[2] = ais[i111];
            is112[5] = is96[i110];
            is112[4] = is96[i110 + 1];
            is112[3] = is96[i111];
            is114[0] = is95[i110];
            is114[1] = is95[i110 + 1];
            is114[2] = is95[i111];
            is114[5] = is97[i110];
            is114[4] = is97[i110 + 1];
            is114[3] = is97[i111];
            is113[0] = 0;
            is113[1] = 0;
            is113[2] = 0;
            is113[5] = is98[i110];
            is113[4] = is98[i110 + 1];
            is113[3] = is98[i111];
            for (f100 = (float) ((0.17 - f108) * random.NextDouble());
                 Math.Abs(f107 - f100) < 0.03 - f108 * 0.176F;
                 f100 = (float) ((0.17 - f108) * random.NextDouble()))
            {
            }
            f107 = f100;
            for (var i115 = 0; i115 < 3; i115++)
            {
                if (Medium.Trk == 2)
                {
                    is106[i115] = (int) (390.0F / (2.2F + f100 - f108));
                }
                else
                {
                    is106[i115] = (int) ((Medium.Cpol[i115] + Medium.Cgrnd[i115]) / (2.2F + f100 - f108));
                }
            }

            P[i109] = new Plane(is112, is114, is113, 6, is106, 3, -8, 0, 0, 0, 0, Disline, 0, true, 0, false);
        }
        f100 = (float) (0.02 * random.NextDouble());
        for (var i116 = 0; i116 < 3; i116++)
        {
            if (Medium.Trk == 2)
            {
                is106[i116] = (int) (390.0F / (2.15F + f100));
            }
            else
            {
                is106[i116] = (int) ((Medium.Cpol[i116] + Medium.Cgrnd[i116]) / (2.15F + f100));
            }
        }

        P[4] = new Plane(is96, is97, is98, 8, is106, 3, -8, 0, 0, 0, 0, Disline, 0, true, 0, false);
        var is117 = new int[2];
        var is118 = new int[2];
        for (var i119 = 0; i119 < 4; i119++)
        {
            var i120 = i119 * 2 + 1;
            Trackers.Y[Trackers.Nt] = is98[i120] / 2;
            Trackers.Rady[Trackers.Nt] = Math.Abs(is98[i120] / 2);
            if (i119 == 0 || i119 == 2)
            {
                Trackers.Z[Trackers.Nt] = (is95[i120] + is97[i120]) / 2;
                Trackers.Radz[Trackers.Nt] = Math.Abs(Trackers.Z[Trackers.Nt] - is95[i120]);
                i120 = i119 * 2 + 2;
                if (i120 == 8)
                {
                    i120 = 0;
                }
                Trackers.X[Trackers.Nt] = (ais[i119 * 2] + ais[i120]) / 2;
                Trackers.Radx[Trackers.Nt] = Math.Abs(Trackers.X[Trackers.Nt] - ais[i119 * 2]);
            }
            else
            {
                Trackers.X[Trackers.Nt] = (ais[i120] + is96[i120]) / 2;
                Trackers.Radx[Trackers.Nt] = Math.Abs(Trackers.X[Trackers.Nt] - ais[i120]);
                i120 = i119 * 2 + 2;
                if (i120 == 8)
                {
                    i120 = 0;
                }
                Trackers.Z[Trackers.Nt] = (is95[i119 * 2] + is95[i120]) / 2;
                Trackers.Radz[Trackers.Nt] = Math.Abs(Trackers.Z[Trackers.Nt] - is95[i119 * 2]);
            }
            if (i119 == 0)
            {
                is118[0] = Trackers.Z[Trackers.Nt] - Trackers.Radz[Trackers.Nt];
                Trackers.Zy[Trackers.Nt] =
                    (int) (Math.Atan(Trackers.Rady[Trackers.Nt] / (double) Trackers.Radz[Trackers.Nt]) /
                           0.017453292519943295);
                if (Trackers.Zy[Trackers.Nt] > 40)
                {
                    Trackers.Zy[Trackers.Nt] = 40;
                }
                Trackers.Xy[Trackers.Nt] = 0;
            }
            if (i119 == 1)
            {
                is117[0] = Trackers.X[Trackers.Nt] - Trackers.Radx[Trackers.Nt];
                Trackers.Xy[Trackers.Nt] =
                    (int) (Math.Atan(Trackers.Rady[Trackers.Nt] / (double) Trackers.Radx[Trackers.Nt]) /
                           0.017453292519943295);
                if (Trackers.Xy[Trackers.Nt] > 40)
                {
                    Trackers.Xy[Trackers.Nt] = 40;
                }
                Trackers.Zy[Trackers.Nt] = 0;
            }
            if (i119 == 2)
            {
                is118[1] = Trackers.Z[Trackers.Nt] + Trackers.Radz[Trackers.Nt];
                Trackers.Zy[Trackers.Nt] =
                    -(int) (Math.Atan(Trackers.Rady[Trackers.Nt] / (double) Trackers.Radz[Trackers.Nt]) /
                            0.017453292519943295);
                if (Trackers.Zy[Trackers.Nt] < -40)
                {
                    Trackers.Zy[Trackers.Nt] = -40;
                }
                Trackers.Xy[Trackers.Nt] = 0;
            }
            if (i119 == 3)
            {
                is117[1] = Trackers.X[Trackers.Nt] + Trackers.Radx[Trackers.Nt];
                Trackers.Xy[Trackers.Nt] =
                    -(int) (Math.Atan(Trackers.Rady[Trackers.Nt] / (double) Trackers.Radx[Trackers.Nt]) /
                            0.017453292519943295);
                if (Trackers.Xy[Trackers.Nt] < -40)
                {
                    Trackers.Xy[Trackers.Nt] = -40;
                }
                Trackers.Zy[Trackers.Nt] = 0;
            }
            Trackers.X[Trackers.Nt] += X;
            Trackers.Z[Trackers.Nt] += Z;
            Trackers.Y[Trackers.Nt] += Y;
            Sys.ArrayCopy(P[i119].Oc, 0, Trackers.C[Trackers.Nt], 0, 3);
            Trackers.Skd[Trackers.Nt] = 2;
            Trackers.Dam[Trackers.Nt] = 1;
            Trackers.Notwall[Trackers.Nt] = false;
            Trackers.Decor[Trackers.Nt] = true;
            Trackers.Rady[Trackers.Nt] += 10;
            Trackers.Nt++;
        }
        Trackers.Y[Trackers.Nt] = 0;
        for (var i122 = 0; i122 < 8; i122++)
        {
            Trackers.Y[Trackers.Nt] += is98[i122];
        }
        Trackers.Y[Trackers.Nt] = Trackers.Y[Trackers.Nt] / 8;
        Trackers.Y[Trackers.Nt] += Y;
        Trackers.Rady[Trackers.Nt] = 200;
        Trackers.Radx[Trackers.Nt] = is117[0] - is117[1];
        Trackers.Radz[Trackers.Nt] = is118[0] - is118[1];
        Trackers.X[Trackers.Nt] = (is117[0] + is117[1]) / 2 + X;
        Trackers.Z[Trackers.Nt] = (is118[0] + is118[1]) / 2 + Z;
        Trackers.Zy[Trackers.Nt] = 0;
        Trackers.Xy[Trackers.Nt] = 0;
        Sys.ArrayCopy(P[4].Oc, 0, Trackers.C[Trackers.Nt], 0, 3);
        Trackers.Skd[Trackers.Nt] = 4;
        Trackers.Dam[Trackers.Nt] = 1;
        Trackers.Notwall[Trackers.Nt] = false;
        Trackers.Decor[Trackers.Nt] = true;
        Trackers.Nt++;
    }

    internal void D()
    {
        Dist = 0;
        var cx = Medium.Cx + (int) ((X - Medium.X - Medium.Cx) * Medium.Cos(Medium.Xz) - (Z - Medium.Z - Medium.Cz) * Medium.Sin(Medium.Xz));
        var cz = Medium.Cz + (int) ((X - Medium.X - Medium.Cx) * Medium.Sin(Medium.Xz) + (Z - Medium.Z - Medium.Cz) * Medium.Cos(Medium.Xz));
        var cy = Medium.Cz + (int) ((Y - Medium.Y - Medium.Cy) * Medium.Sin(Medium.Zy) + (cz - Medium.Cz) * Medium.Cos(Medium.Zy));
        var i126 = Xs(cx + MaxR, cy) - Xs(cx - MaxR, cy);
        if (Xs(cx + MaxR * 2, cy) > Medium.Iw && Xs(cx - MaxR * 2, cy) < Medium.W && cy > -MaxR &&
            (cy < Medium.Fade[Disline] + MaxR || Medium.Trk != 0) && (i126 > Disp || Medium.Trk != 0) &&
            (!Decor || Medium.Resdown != 2 && Medium.Trk != 1))
        {
            PrepareShadow(cy, cz);

            var i136 = Medium.Cy + (int) ((Y - Medium.Y - Medium.Cy) * Medium.Cos(Medium.Zy) -
                                          (cz - Medium.Cz) * Medium.Sin(Medium.Zy));
            if (Ys(i136 + MaxR, cy) > Medium.Ih && Ys(i136 - MaxR, cy) < Medium.H)
            {
                if (Elec && Medium.Noelec == 0)
                {
                    Electrify();
                }
                if (Fix)
                {
                    Fixit();
                }
                if (Checkpoint != 0 && Checkpoint - 1 == Medium.Checkpoint)
                {
                    i126 = -1;
                }
                if (Shadow)
                {
                    Dist = (int) Math.Sqrt((Medium.X + Medium.Cx - X) * (Medium.X + Medium.Cx - X) +
                                           (Medium.Z - Z) * (Medium.Z - Z) +
                                           (Medium.Y + Medium.Cy - Y) * (Medium.Y + Medium.Cy - Y));
                    for (var i137 = 0; i137 < 20; i137++)
                    {
                        if (Stg[i137] != 0)
                        {
                            Pdust(i137, true);
                        }
                    }

                    Dsprk(true);
                }
                ArrayExt.Sort(P, 0, Npl);

                var npl = Npl - 1;
                P[0].D(P[0], P[1], X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, Wxz, Wzy, Noline, i126);
                for (var j = 1; j < npl; j++)
                {
                    P[j].D(P[j - 1], P[j + 1], X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, Wxz, Wzy,
                        Noline, i126);
                }
                P[npl].D(P[npl - 1], null, X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, Wxz, Wzy, Noline,
                    i126);

                if (Shadow)
                {
                    for (var i143 = 0; i143 < 20; i143++)
                    {
                        if (Stg[i143] != 0)
                        {
                            Pdust(i143, false);
                        }
                    }

                    Dsprk(false);
                }
                Dist = (int) (Math.Sqrt(Math.Sqrt((Medium.X + Medium.Cx - X) * (Medium.X + Medium.Cx - X) + (Medium.Z - Z) * (Medium.Z - Z) + (Medium.Y + Medium.Cy - Y) * (Medium.Y + Medium.Cy - Y))) * Grounded);
            }
        }
        FinishShadow();
    }

    private void FinishShadow()
    {
        if (Shadow && Dist == 0)
        {
            for (var i144 = 0; i144 < 20; i144++)
            {
                Stg[i144] = 0;
            }

            for (var i145 = 0; i145 < 100; i145++)
            {
                _rtg[i145] = 0;
            }

            Sprk = 0;
        }
    }

    private void PrepareShadow(int i125, int i124)
    {
        if (!Shadow)
        {
            return;
        }

        if (!Medium.Crs)
        {
            if (i125 < 2000)
            {
                var aabool = false;
                if (Trackers.Ncx != 0 || Trackers.Ncz != 0)
                {
                    var i127 = (X - Trackers.Sx) / 3000;
                    if (i127 > Trackers.Ncx)
                    {
                        i127 = Trackers.Ncx;
                    }
                    if (i127 < 0)
                    {
                        i127 = 0;
                    }
                    var i128 = (Z - Trackers.Sz) / 3000;
                    if (i128 > Trackers.Ncz)
                    {
                        i128 = Trackers.Ncz;
                    }
                    if (i128 < 0)
                    {
                        i128 = 0;
                    }
                    for (var i129 = Trackers.Sect[i127, i128].Length - 1; i129 >= 0; i129--)
                    {
                        var i130 = Trackers.Sect[i127, i128][i129];
                        if (Math.Abs(Trackers.Zy[i130]) != 90 && Math.Abs(Trackers.Xy[i130]) != 90 &&
                            Math.Abs(X - Trackers.X[i130]) < Trackers.Radx[i130] + MaxR &&
                            Math.Abs(Z - Trackers.Z[i130]) < Trackers.Radz[i130] + MaxR &&
                            (!Trackers.Decor[i130] || Medium.Resdown != 2))
                        {
                            aabool = true;
                            break;
                        }
                    }
                }
                if (aabool)
                {
                    for (var i131 = 0; i131 < Npl; i131++)
                    {
                        P[i131].S(X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, 0);
                    }
                }
                else
                {
                    var i132 = Medium.Cy + (int) ((Medium.Ground - Medium.Cy) * Medium.Cos(Medium.Zy) -
                                                  (i124 - Medium.Cz) * Medium.Sin(Medium.Zy));
                    var i133 = Medium.Cz + (int) ((Medium.Ground - Medium.Cy) * Medium.Sin(Medium.Zy) +
                                                  (i124 - Medium.Cz) * Medium.Cos(Medium.Zy));
                    if (Ys(i132 + MaxR, i133) > 0 && Ys(i132 - MaxR, i133) < Medium.H)
                    {
                        for (var i134 = 0; i134 < Npl; i134++)
                        {
                            P[i134].S(X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, 1);
                        }
                    }
                }
                Medium.Addsp(X - Medium.X, Z - Medium.Z, (int) (MaxR * 0.8));
            }
            else
            {
                Lowshadow(i125);
            }
        }
        else
        {
            for (var i135 = 0; i135 < Npl; i135++)
            {
                P[i135].S(X - Medium.X, Y - Medium.Y, Z - Medium.Z, Xz, Xy, Zy, 2);
            }
        }
    }

    private void Dsprk(bool aabool)
    {
        if (aabool && Sprk != 0)
        {
            var i = (int) (Math.Sqrt(Rcx * Rcx + Rcy * Rcy + Rcz * Rcz) / 10.0);
            if (i > 5)
            {
                var bool240 = false;
                if (Dist < Math.Sqrt((Medium.X + Medium.Cx - Srx) * (Medium.X + Medium.Cx - Srx) +
                                     (Medium.Y + Medium.Cy - Sry) * (Medium.Y + Medium.Cy - Sry) +
                                     (Medium.Z - Srz) * (Medium.Z - Srz)))
                {
                    bool240 = true;
                }
                if (i > 33)
                {
                    i = 33;
                }
                var i241 = 0;
                for (var i242 = 0; i242 < 100; i242++)
                {
                    if (_rtg[i242] == 0)
                    {
                        _rtg[i242] = 1;
                        _rbef[i242] = bool240;
                        i241++;
                    }
                    if (i241 == i)
                    {
                        break;
                    }
                }
            }
        }
        for (var i = 0; i < 100; i++)
        {
            if (_rtg[i] != 0 && (_rbef[i] && aabool || !_rbef[i] && !aabool))
            {
                if (_rtg[i] == 1)
                {
                    if (Sprk < 5)
                    {
                        _rx[i] = Srx + 3 - (int) (Medium.Random() * 6.7);
                        _ry[i] = Sry + 3 - (int) (Medium.Random() * 6.7);
                        _rz[i] = Srz + 3 - (int) (Medium.Random() * 6.7);
                    }
                    else
                    {
                        _rx[i] = Srx + 10 - (int) (Medium.Random() * 20.0F);
                        _ry[i] = Sry - (int) (Medium.Random() * 4.0F);
                        _rz[i] = Srz + 10 - (int) (Medium.Random() * 20.0F);
                    }
                    var i243 = (int) Math.Sqrt(Rcx * Rcx + Rcy * Rcy + Rcz * Rcz);
                    var f = 0.2F + 0.4F * Medium.Random();
                    var f244 = Medium.Random() * Medium.Random() * Medium.Random();
                    var f245 = 1.0F;
                    if (Medium.RandomBoolean())
                    {
                        if (Medium.RandomBoolean())
                        {
                            f245 *= -1.0F;
                        }
                        _vrx[i] = -((Rcx + i243 * (1.0F - Rcx / i243) * f244 * f245) * f);
                    }
                    if (Medium.RandomBoolean())
                    {
                        if (Medium.RandomBoolean())
                        {
                            f245 *= -1.0F;
                        }
                        if (Sprk == 5)
                        {
                            f245 = 1.0F;
                        }
                        _vry[i] = -((Rcy + i243 * (1.0F - Rcy / i243) * f244 * f245) * f);
                    }
                    if (Medium.RandomBoolean())
                    {
                        if (Medium.RandomBoolean())
                        {
                            f245 *= -1.0F;
                        }
                        _vrz[i] = -((Rcz + i243 * (1.0F - Rcz / i243) * f244 * f245) * f);
                    }
                }
                _rx[i] = (int) (_rx[i] + _vrx[i]);
                _ry[i] = (int) (_ry[i] + _vry[i]);
                _rz[i] = (int) (_rz[i] + _vrz[i]);
                var i246 = Medium.Cx + (int) ((_rx[i] - Medium.X - Medium.Cx) * Medium.Cos(Medium.Xz) -
                                              (_rz[i] - Medium.Z - Medium.Cz) * Medium.Sin(Medium.Xz));
                var i247 = Medium.Cz + (int) ((_rx[i] - Medium.X - Medium.Cx) * Medium.Sin(Medium.Xz) +
                                              (_rz[i] - Medium.Z - Medium.Cz) * Medium.Cos(Medium.Xz));
                var i248 = Medium.Cy + (int) ((_ry[i] - Medium.Y - Medium.Cy) * Medium.Cos(Medium.Zy) -
                                              (i247 - Medium.Cz) * Medium.Sin(Medium.Zy));
                i247 = Medium.Cz + (int) ((_ry[i] - Medium.Y - Medium.Cy) * Medium.Sin(Medium.Zy) +
                                          (i247 - Medium.Cz) * Medium.Cos(Medium.Zy));
                var i249 = Medium.Cx + (int) ((_rx[i] - Medium.X - Medium.Cx + _vrx[i]) * Medium.Cos(Medium.Xz) -
                                              (_rz[i] - Medium.Z - Medium.Cz + _vrz[i]) * Medium.Sin(Medium.Xz));
                var i250 = Medium.Cz + (int) ((_rx[i] - Medium.X - Medium.Cx + _vrx[i]) * Medium.Sin(Medium.Xz) +
                                              (_rz[i] - Medium.Z - Medium.Cz + _vrz[i]) * Medium.Cos(Medium.Xz));
                var i251 = Medium.Cy + (int) ((_ry[i] - Medium.Y - Medium.Cy + _vry[i]) * Medium.Cos(Medium.Zy) -
                                              (i250 - Medium.Cz) * Medium.Sin(Medium.Zy));
                i250 = Medium.Cz + (int) ((_ry[i] - Medium.Y - Medium.Cy + _vry[i]) * Medium.Sin(Medium.Zy) +
                                          (i250 - Medium.Cz) * Medium.Cos(Medium.Zy));
                var i252 = Xs(i246, i247);
                var i253 = Ys(i248, i247);
                var i254 = Xs(i249, i250);
                var i255 = Ys(i251, i250);
                if (i252 < Medium.Iw && i254 < Medium.Iw)
                {
                    _rtg[i] = 0;
                }
                if (i252 > Medium.W && i254 > Medium.W)
                {
                    _rtg[i] = 0;
                }
                if (i253 < Medium.Ih && i255 < Medium.Ih)
                {
                    _rtg[i] = 0;
                }
                if (i253 > Medium.H && i255 > Medium.H)
                {
                    _rtg[i] = 0;
                }
                if (_ry[i] > 250)
                {
                    _rtg[i] = 0;
                }
                if (_rtg[i] != 0)
                {
                    var i256 = 255;
                    var i257 = 197 - 30 * _rtg[i];
                    var i258 = 0;
                    for (var i259 = 0; i259 < 16; i259++)
                    {
                        if (i247 > Medium.Fade[i259])
                        {
                            i256 = (i256 * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                            i257 = (i257 * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                            i258 = (i258 * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                        }
                    }

                    G.SetColor(new Color(i256, i257, i258));
                    G.DrawLine(i252, i253, i254, i255);
                    _vrx[i] = _vrx[i] * 0.8F;
                    _vry[i] = _vry[i] * 0.8F;
                    _vrz[i] = _vrz[i] * 0.8F;
                    if (_rtg[i] == 3)
                    {
                        _rtg[i] = 0;
                    }
                    else
                    {
                        _rtg[i]++;
                    }
                }
            }
        }

        if (Sprk != 0)
        {
            Sprk = 0;
        }
    }

    internal void Dust(int i, float f, float f199, float f200, int i201, int i202, float f203, int i204,
        bool aabool)
    {
        var bool205 = false;
        if (i204 > 5 && (i == 0 || i == 2))
        {
            bool205 = true;
        }
        if (i204 < -5 && (i == 1 || i == 3))
        {
            bool205 = true;
        }
        var f206 = (float) ((Math.Sqrt(i201 * i201 + i202 * i202) - 40.0) / 160.0);
        if (f206 > 1.0F)
        {
            f206 = 1.0F;
        }
        if (f206 > 0.2 && !bool205)
        {
            _ust++;
            if (_ust == 20)
            {
                _ust = 0;
            }
            if (!aabool)
            {
                var f207 = Medium.Random();
                Sx[_ust] = (int) ((f + X * f207) / (1.0F + f207));
                Sz[_ust] = (int) ((f200 + Z * f207) / (1.0F + f207));
                Sy[_ust] = (int) ((f199 + Y * f207) / (1.0F + f207));
            }
            else
            {
                Sx[_ust] = (int) ((f + (X + i201)) / 2.0F);
                Sz[_ust] = (int) ((f200 + (Z + i202)) / 2.0F);
                Sy[_ust] = (int) f199;
            }
            if (Sy[i] > 250)
            {
                Sy[i] = 250;
            }
            Osmag[_ust] = f203 * f206;
            Scx[_ust] = i201;
            Scz[_ust] = i202;
            Stg[_ust] = 1;
        }
    }

    private void Electrify()
    {
        for (var i = 0; i < 4; i++)
        {
            if (_elc[i] == 0)
            {
                _edl[i] = (int) (380.0F - Medium.Random() * 760.0F);
                _edr[i] = (int) (380.0F - Medium.Random() * 760.0F);
                _elc[i] = 1;
            }
            var i182 = (int) (_edl[i] + (190.0F - Medium.Random() * 380.0F));
            var i183 = (int) (_edr[i] + (190.0F - Medium.Random() * 380.0F));
            var i184 = (int) (Medium.Random() * 126.0F);
            var i185 = (int) (Medium.Random() * 126.0F);
            var ais = new int[8];
            var is186 = new int[8];
            var is187 = new int[8];
            for (var i188 = 0; i188 < 8; i188++)
            {
                is187[i188] = Z - Medium.Z;
            }
            ais[0] = X - Medium.X - 504;
            is186[0] = Y - Medium.Y - _edl[i] - 5 - (int) (Medium.Random() * 5.0F);
            ais[1] = X - Medium.X - 252 + i185;
            is186[1] = Y - Medium.Y - i182 - 5 - (int) (Medium.Random() * 5.0F);
            ais[2] = X - Medium.X + 252 - i184;
            is186[2] = Y - Medium.Y - i183 - 5 - (int) (Medium.Random() * 5.0F);
            ais[3] = X - Medium.X + 504;
            is186[3] = Y - Medium.Y - _edr[i] - 5 - (int) (Medium.Random() * 5.0F);
            ais[4] = X - Medium.X + 504;
            is186[4] = Y - Medium.Y - _edr[i] + 5 + (int) (Medium.Random() * 5.0F);
            ais[5] = X - Medium.X + 252 - i184;
            is186[5] = Y - Medium.Y - i183 + 5 + (int) (Medium.Random() * 5.0F);
            ais[6] = X - Medium.X - 252 + i185;
            is186[6] = Y - Medium.Y - i182 + 5 + (int) (Medium.Random() * 5.0F);
            ais[7] = X - Medium.X - 504;
            is186[7] = Y - Medium.Y - _edl[i] + 5 + (int) (Medium.Random() * 5.0F);
            if (Roted)
            {
                Rot(ais, is187, X - Medium.X, Z - Medium.Z, 90, 8);
            }
            Rot(ais, is187, Medium.Cx, Medium.Cz, Medium.Xz, 8);
            Rot(is186, is187, Medium.Cy, Medium.Cz, Medium.Zy, 8);
            var aabool = true;
            var i189 = 0;
            var i190 = 0;
            var i191 = 0;
            var i192 = 0;
            var is193 = new int[8];
            var is194 = new int[8];
            for (var i195 = 0; i195 < 8; i195++)
            {
                is193[i195] = Xs(ais[i195], is187[i195]);
                is194[i195] = Ys(is186[i195], is187[i195]);
                if (is194[i195] < Medium.Ih || is187[i195] < 10)
                {
                    i189++;
                }
                if (is194[i195] > Medium.H || is187[i195] < 10)
                {
                    i190++;
                }
                if (is193[i195] < Medium.Iw || is187[i195] < 10)
                {
                    i191++;
                }
                if (is193[i195] > Medium.W || is187[i195] < 10)
                {
                    i192++;
                }
            }
            if (i191 == 8 || i189 == 8 || i190 == 8 || i192 == 8)
            {
                aabool = false;
            }
            if (aabool)
            {
                var i196 = (int) (160.0F + 160.0F * (Medium.Snap[0] / 500.0F));
                if (i196 > 255)
                {
                    i196 = 255;
                }
                if (i196 < 0)
                {
                    i196 = 0;
                }
                var i197 = (int) (238.0F + 238.0F * (Medium.Snap[1] / 500.0F));
                if (i197 > 255)
                {
                    i197 = 255;
                }
                if (i197 < 0)
                {
                    i197 = 0;
                }
                var i198 = (int) (255.0F + 255.0F * (Medium.Snap[2] / 500.0F));
                if (i198 > 255)
                {
                    i198 = 255;
                }
                if (i198 < 0)
                {
                    i198 = 0;
                }
                i196 = (i196 * 2 + 214 * (_elc[i] - 1)) / (_elc[i] + 1);
                i197 = (i197 * 2 + 236 * (_elc[i] - 1)) / (_elc[i] + 1);
                if (Medium.Trk == 1)
                {
                    i196 = 255;
                    i197 = 128;
                    i198 = 0;
                }
                G.SetColor(new Color(i196, i197, i198));
                G.FillPolygon(is193, is194, 8);
                if (is187[0] < 4000)
                {
                    i196 = (int) (150.0F + 150.0F * (Medium.Snap[0] / 500.0F));
                    if (i196 > 255)
                    {
                        i196 = 255;
                    }
                    if (i196 < 0)
                    {
                        i196 = 0;
                    }
                    i197 = (int) (227.0F + 227.0F * (Medium.Snap[1] / 500.0F));
                    if (i197 > 255)
                    {
                        i197 = 255;
                    }
                    if (i197 < 0)
                    {
                        i197 = 0;
                    }
                    i198 = (int) (255.0F + 255.0F * (Medium.Snap[2] / 500.0F));
                    if (i198 > 255)
                    {
                        i198 = 255;
                    }
                    if (i198 < 0)
                    {
                        i198 = 0;
                    }
                    G.SetColor(new Color(i196, i197, i198));
                    G.DrawPolygon(is193, is194, 8);
                }
            }
            if (_elc[i] > Medium.Random() * 60.0F)
            {
                _elc[i] = 0;
            }
            else
            {
                _elc[i]++;
            }
        }
        if (!Roted || Xz != 0)
        {
            Xy += 11;
            if (Xy > 360)
            {
                Xy -= 360;
            }
        }
        else
        {
            Zy += 11;
            if (Zy > 360)
            {
                Zy -= 360;
            }
        }
    }

    private void Fixit()
    {
        if (Fcnt == 1)
        {
            for (var i = 0; i < Npl; i++)
            {
                P[i].HSB[0] = 0.57F;
                P[i].HSB[2] = 0.8F;
                P[i].HSB[1] = 0.8F;
                var color = Color.GetHSBColor(P[i].HSB[0], P[i].HSB[1], P[i].HSB[2]);
                var i167 = (int) (color.R + color.R * (Medium.Snap[0] / 100.0F));
                if (i167 > 255)
                {
                    i167 = 255;
                }
                if (i167 < 0)
                {
                    i167 = 0;
                }
                var i168 = (int) (color.G + color.G * (Medium.Snap[1] / 100.0F));
                if (i168 > 255)
                {
                    i168 = 255;
                }
                if (i168 < 0)
                {
                    i168 = 0;
                }
                var i169 = (int) (color.B + color.B * (Medium.Snap[2] / 100.0F));
                if (i169 > 255)
                {
                    i169 = 255;
                }
                if (i169 < 0)
                {
                    i169 = 0;
                }
                Color.RGBtoHSB(i167, i168, i169, P[i].HSB);
                P[i].Flx = 1;
            }
        }
        if (Fcnt == 2)
        {
            for (var i = 0; i < Npl; i++)
            {
                P[i].Flx = 1;
            }
        }
        if (Fcnt == 4)
        {
            for (var i = 0; i < Npl; i++)
            {
                P[i].Flx = 3;
            }
        }
        if ((Fcnt == 1 || Fcnt > 2) && Fcnt != 9)
        {
            var ais = new int[8];
            var is170 = new int[8];
            var is171 = new int[4];
            for (var j = 0; j < 4; j++)
            {
                ais[j] = Keyx[j] + X - Medium.X;
                is170[j] = Grat + Y - Medium.Y;
                is171[j] = Keyz[j] + Z - Medium.Z;
            }
            Rot(ais, is170, X - Medium.X, Y - Medium.Y, Xy, 4);
            Rot(is170, is171, Y - Medium.Y, Z - Medium.Y, Zy, 4);
            Rot(ais, is171, X - Medium.X, Z - Medium.Z, Xz, 4);
            Rot(ais, is171, Medium.Cx, Medium.Cz, Medium.Xz, 4);
            Rot(is170, is171, Medium.Cy, Medium.Cz, Medium.Zy, 4);
            var i = 0;
            var i172 = 0;
            var i173 = 0;
            for (var i174 = 0; i174 < 4; i174++)
            {
                for (var i175 = 0; i175 < 4; i175++)
                {
                    if (Math.Abs(ais[i174] - ais[i175]) > i)
                    {
                        i = Math.Abs(ais[i174] - ais[i175]);
                    }
                    if (Math.Abs(is170[i174] - is170[i175]) > i172)
                    {
                        i172 = Math.Abs(is170[i174] - is170[i175]);
                    }
                    if (Py(ais[i174], ais[i175], is170[i174], is170[i175]) > i173)
                    {
                        i173 = Py(ais[i174], ais[i175], is170[i174], is170[i175]);
                    }
                }
            }
            i173 = (int) (Math.Sqrt(i173) / 1.5);
            if (i < i173)
            {
                i = i173;
            }
            if (i172 < i173)
            {
                i172 = i173;
            }
            var i176 = Medium.Cx + (int) ((X - Medium.X - Medium.Cx) * Medium.Cos(Medium.Xz) -
                                          (Z - Medium.Z - Medium.Cz) * Medium.Sin(Medium.Xz));
            var i177 = Medium.Cz + (int) ((X - Medium.X - Medium.Cx) * Medium.Sin(Medium.Xz) +
                                          (Z - Medium.Z - Medium.Cz) * Medium.Cos(Medium.Xz));
            var i178 = Medium.Cy + (int) ((Y - Medium.Y - Medium.Cy) * Medium.Cos(Medium.Zy) -
                                          (i177 - Medium.Cz) * Medium.Sin(Medium.Zy));
            i177 = Medium.Cz + (int) ((Y - Medium.Y - Medium.Cy) * Medium.Sin(Medium.Zy) +
                                      (i177 - Medium.Cz) * Medium.Cos(Medium.Zy));
            ais[0] = Xs((int) (i176 - i / 0.8 - Medium.Random() * (i / 2.4)), i177);
            is170[0] = Ys((int) (i178 - i172 / 1.92 - Medium.Random() * (i172 / 5.67)), i177);
            ais[1] = Xs((int) (i176 - i / 0.8 - Medium.Random() * (i / 2.4)), i177);
            is170[1] = Ys((int) (i178 + i172 / 1.92 + Medium.Random() * (i172 / 5.67)), i177);
            ais[2] = Xs((int) (i176 - i / 1.92 - Medium.Random() * (i / 5.67)), i177);
            is170[2] = Ys((int) (i178 + i172 / 0.8 + Medium.Random() * (i172 / 2.4)), i177);
            ais[3] = Xs((int) (i176 + i / 1.92 + Medium.Random() * (i / 5.67)), i177);
            is170[3] = Ys((int) (i178 + i172 / 0.8 + Medium.Random() * (i172 / 2.4)), i177);
            ais[4] = Xs((int) (i176 + i / 0.8 + Medium.Random() * (i / 2.4)), i177);
            is170[4] = Ys((int) (i178 + i172 / 1.92 + Medium.Random() * (i172 / 5.67)), i177);
            ais[5] = Xs((int) (i176 + i / 0.8 + Medium.Random() * (i / 2.4)), i177);
            is170[5] = Ys((int) (i178 - i172 / 1.92 - Medium.Random() * (i172 / 5.67)), i177);
            ais[6] = Xs((int) (i176 + i / 1.92 + Medium.Random() * (i / 5.67)), i177);
            is170[6] = Ys((int) (i178 - i172 / 0.8 - Medium.Random() * (i172 / 2.4)), i177);
            ais[7] = Xs((int) (i176 - i / 1.92 - Medium.Random() * (i / 5.67)), i177);
            is170[7] = Ys((int) (i178 - i172 / 0.8 - Medium.Random() * (i172 / 2.4)), i177);
            if (Fcnt == 3)
            {
                Rot(ais, is170, Xs(i176, i177), Ys(i178, i177), 22, 8);
            }
            if (Fcnt == 4)
            {
                Rot(ais, is170, Xs(i176, i177), Ys(i178, i177), 22, 8);
            }
            if (Fcnt == 5)
            {
                Rot(ais, is170, Xs(i176, i177), Ys(i178, i177), 0, 8);
            }
            if (Fcnt == 6)
            {
                Rot(ais, is170, Xs(i176, i177), Ys(i178, i177), -22, 8);
            }
            if (Fcnt == 7)
            {
                Rot(ais, is170, Xs(i176, i177), Ys(i178, i177), -22, 8);
            }
            var i179 = (int) (191.0F + 191.0F * (Medium.Snap[0] / 350.0F));
            if (i179 > 255)
            {
                i179 = 255;
            }
            if (i179 < 0)
            {
                i179 = 0;
            }
            var i180 = (int) (232.0F + 232.0F * (Medium.Snap[1] / 350.0F));
            if (i180 > 255)
            {
                i180 = 255;
            }
            if (i180 < 0)
            {
                i180 = 0;
            }
            var i181 = (int) (255.0F + 255.0F * (Medium.Snap[2] / 350.0F));
            if (i181 > 255)
            {
                i181 = 255;
            }
            if (i181 < 0)
            {
                i181 = 0;
            }
            G.SetColor(new Color(i179, i180, i181));
            G.SetAntialiasing(false);
            G.FillPolygon(ais, is170, 8);
            G.SetAntialiasing(true);
            ais[0] = Xs((int) (i176 - i - Medium.Random() * (i / 4)), i177);
            is170[0] = Ys((int) (i178 - i172 / 2.4 - Medium.Random() * (i172 / 9.6)), i177);
            ais[1] = Xs((int) (i176 - i - Medium.Random() * (i / 4)), i177);
            is170[1] = Ys((int) (i178 + i172 / 2.4 + Medium.Random() * (i172 / 9.6)), i177);
            ais[2] = Xs((int) (i176 - i / 2.4 - Medium.Random() * (i / 9.6)), i177);
            is170[2] = Ys((int) (i178 + i172 + Medium.Random() * (i172 / 4)), i177);
            ais[3] = Xs((int) (i176 + i / 2.4 + Medium.Random() * (i / 9.6)), i177);
            is170[3] = Ys((int) (i178 + i172 + Medium.Random() * (i172 / 4)), i177);
            ais[4] = Xs((int) (i176 + i + Medium.Random() * (i / 4)), i177);
            is170[4] = Ys((int) (i178 + i172 / 2.4 + Medium.Random() * (i172 / 9.6)), i177);
            ais[5] = Xs((int) (i176 + i + Medium.Random() * (i / 4)), i177);
            is170[5] = Ys((int) (i178 - i172 / 2.4 - Medium.Random() * (i172 / 9.6)), i177);
            ais[6] = Xs((int) (i176 + i / 2.4 + Medium.Random() * (i / 9.6)), i177);
            is170[6] = Ys((int) (i178 - i172 - Medium.Random() * (i172 / 4)), i177);
            ais[7] = Xs((int) (i176 - i / 2.4 - Medium.Random() * (i / 9.6)), i177);
            is170[7] = Ys((int) (i178 - i172 - Medium.Random() * (i172 / 4)), i177);
            i179 = (int) (213.0F + 213.0F * (Medium.Snap[0] / 350.0F));
            if (i179 > 255)
            {
                i179 = 255;
            }
            if (i179 < 0)
            {
                i179 = 0;
            }
            i180 = (int) (239.0F + 239.0F * (Medium.Snap[1] / 350.0F));
            if (i180 > 255)
            {
                i180 = 255;
            }
            if (i180 < 0)
            {
                i180 = 0;
            }
            i181 = (int) (255.0F + 255.0F * (Medium.Snap[2] / 350.0F));
            if (i181 > 255)
            {
                i181 = 255;
            }
            if (i181 < 0)
            {
                i181 = 0;
            }
            G.SetColor(new Color(i179, i180, i181));
            G.SetAntialiasing(false);
            G.FillPolygon(ais, is170, 8);
            G.SetAntialiasing(true);
        }
        if (Fcnt > 7)
        {
            Fcnt = 0;
            Fix = false;
        }
        else
        {
            Fcnt++;
        }
    }

    public int Getpy(int i, int i267, int i268)
    {
        return (i - X) / 10 * ((i - X) / 10) + (i267 - Y) / 10 * ((i267 - Y) / 10) +
               (i268 - Z) / 10 * ((i268 - Z) / 10);
    }

    private int Getvalue(string aastring, string string262, int i)
    {
//TODO
        return Utility.Getint(aastring, string262, i);
    }

    private void Lowshadow(int i)
    {
        var ais = new int[4];
        var is146 = new int[4];
        var is147 = new int[4];
        var i148 = 1;
        int i149;
        for (i149 = Math.Abs(Zy); i149 > 270; i149 -= 360)
        {
        }
        i149 = Math.Abs(i149);
        if (i149 > 90)
        {
            i148 = -1;
        }
        ais[0] = (int) (Keyx[0] * 1.2 + X - Medium.X);
        is147[0] = (int) ((Keyz[0] + 30) * i148 * 1.2 + Z - Medium.Z);
        ais[1] = (int) (Keyx[1] * 1.2 + X - Medium.X);
        is147[1] = (int) ((Keyz[1] + 30) * i148 * 1.2 + Z - Medium.Z);
        ais[2] = (int) (Keyx[3] * 1.2 + X - Medium.X);
        is147[2] = (int) ((Keyz[3] - 30) * i148 * 1.2 + Z - Medium.Z);
        ais[3] = (int) (Keyx[2] * 1.2 + X - Medium.X);
        is147[3] = (int) ((Keyz[2] - 30) * i148 * 1.2 + Z - Medium.Z);
        Rot(ais, is147, X - Medium.X, Z - Medium.Z, Xz, 4);
        var i150 = (int) (Medium.Crgrnd[0] / 1.5);
        var i151 = (int) (Medium.Crgrnd[1] / 1.5);
        var i152 = (int) (Medium.Crgrnd[2] / 1.5);
        for (var i153 = 0; i153 < 4; i153++)
        {
            is146[i153] = Medium.Ground;
        }
        if (Trackers.Ncx != 0 || Trackers.Ncz != 0)
        {
            var i154 = (X - Trackers.Sx) / 3000;
            if (i154 > Trackers.Ncx)
            {
                i154 = Trackers.Ncx;
            }
            if (i154 < 0)
            {
                i154 = 0;
            }
            var i155 = (Z - Trackers.Sz) / 3000;
            if (i155 > Trackers.Ncz)
            {
                i155 = Trackers.Ncz;
            }
            if (i155 < 0)
            {
                i155 = 0;
            }
            for (var i156 = Trackers.Sect[i154, i155].Length - 1; i156 >= 0; i156--)
            {
                var i157 = Trackers.Sect[i154, i155][i156];
                var i158 = 0;
                for (var i159 = 0; i159 < 4; i159++)
                {
                    if (Math.Abs(Trackers.Zy[i157]) != 90 && Math.Abs(Trackers.Xy[i157]) != 90 &&
                        Trackers.Rady[i157] != 801 &&
                        Math.Abs(ais[i159] - (Trackers.X[i157] - Medium.X)) < Trackers.Radx[i157] &&
                        Math.Abs(is147[i159] - (Trackers.Z[i157] - Medium.Z)) < Trackers.Radz[i157] &&
                        (!Trackers.Decor[i157] || Medium.Resdown != 2))
                    {
                        i158++;
                    }
                }

                if (i158 > 2)
                {
                    for (var i160 = 0; i160 < 4; i160++)
                    {
                        is146[i160] = Trackers.Y[i157] - Medium.Y;
                        if (Trackers.Zy[i157] != 0)
                        {
                            is146[i160] = (int) (is146[i160] +
                                (is147[i160] -
                                 (Trackers.Z[i157] - Medium.Z - Trackers.Radz[i157])) *
                                Medium.Sin(Trackers.Zy[i157]) /
                                Medium.Sin(90 - Trackers.Zy[i157]) - Trackers.Radz[i157] *
                                Medium.Sin(Trackers.Zy[i157]) /
                                Medium.Sin(90 - Trackers.Zy[i157]));
                        }
                        if (Trackers.Xy[i157] != 0)
                        {
                            is146[i160] = (int) (is146[i160] +
                                (ais[i160] - (Trackers.X[i157] - Medium.X - Trackers.Radx[i157])) *
                                Medium.Sin(Trackers.Xy[i157]) /
                                Medium.Sin(90 - Trackers.Xy[i157]) - Trackers.Radx[i157] *
                                Medium.Sin(Trackers.Xy[i157]) /
                                Medium.Sin(90 - Trackers.Xy[i157]));
                        }
                    }
                    i150 = (int) (Trackers.C[i157][0] / 1.5);
                    i151 = (int) (Trackers.C[i157][1] / 1.5);
                    i152 = (int) (Trackers.C[i157][2] / 1.5);
                    break;
                }
            }
        }
        Rot(ais, is147, Medium.Cx, Medium.Cz, Medium.Xz, 4);
        Rot(is146, is147, Medium.Cy, Medium.Cz, Medium.Zy, 4);
        var aabool = true;
        var i161 = 0;
        var i162 = 0;
        var i163 = 0;
        var i164 = 0;
        for (var i165 = 0; i165 < 4; i165++)
        {
            ais[i165] = Xs(ais[i165], is147[i165]);
            is146[i165] = Ys(is146[i165], is147[i165]);
            if (is146[i165] < Medium.Ih || is147[i165] < 10)
            {
                i161++;
            }
            if (is146[i165] > Medium.H || is147[i165] < 10)
            {
                i162++;
            }
            if (ais[i165] < Medium.Iw || is147[i165] < 10)
            {
                i163++;
            }
            if (ais[i165] > Medium.W || is147[i165] < 10)
            {
                i164++;
            }
        }
        if (i163 == 4 || i161 == 4 || i162 == 4 || i164 == 4)
        {
            aabool = false;
        }
        if (aabool)
        {
            for (var i166 = 0; i166 < 16; i166++)
            {
                if (i > Medium.Fade[i166])
                {
                    i150 = (i150 * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                    i151 = (i151 * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                    i152 = (i152 * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                }
            }

            G.SetColor(new Color(i150, i151, i152));
            G.SetAntialiasing(false);
            G.FillPolygon(ais, is146, 4);
            G.SetAntialiasing(true);
        }
    }

    private void Pdust(int i, bool aabool)
    {
        if (aabool)
        {
            _sav[i] = (int) Math.Sqrt((Medium.X + Medium.Cx - Sx[i]) * (Medium.X + Medium.Cx - Sx[i]) +
                                      (Medium.Y + Medium.Cy - Sy[i]) * (Medium.Y + Medium.Cy - Sy[i]) +
                                      (Medium.Z - Sz[i]) * (Medium.Z - Sz[i]));
        }
        if (aabool && _sav[i] > Dist || !aabool && _sav[i] <= Dist)
        {
            int[] ais;
            if (Stg[i] == 1)
            {
                _sbln[i] = 0.6F;
                var bool208 = false;
                ais = new int[3];
                for (var i209 = 0; i209 < 3; i209++)
                {
                    ais[i209] = (int) (255.0F + 255.0F * (Medium.Snap[i209] / 100.0F));
                    if (ais[i209] > 255)
                    {
                        ais[i209] = 255;
                    }
                    if (ais[i209] < 0)
                    {
                        ais[i209] = 0;
                    }
                }
                var i210 = (X - Trackers.Sx) / 3000;
                if (i210 > Trackers.Ncx)
                {
                    i210 = Trackers.Ncx;
                }
                if (i210 < 0)
                {
                    i210 = 0;
                }
                var i211 = (Z - Trackers.Sz) / 3000;
                if (i211 > Trackers.Ncz)
                {
                    i211 = Trackers.Ncz;
                }
                if (i211 < 0)
                {
                    i211 = 0;
                }
                for (var i212 = 0; i212 < Trackers.Sect[i210, i211].Length; i212++)
                {
                    var i213 = Trackers.Sect[i210, i211][i212];
                    if (Math.Abs(Trackers.Zy[i213]) != 90 && Math.Abs(Trackers.Xy[i213]) != 90 &&
                        Math.Abs(Sx[i] - Trackers.X[i213]) < Trackers.Radx[i213] &&
                        Math.Abs(Sz[i] - Trackers.Z[i213]) < Trackers.Radz[i213])
                    {
                        if (Trackers.Skd[i213] == 0)
                        {
                            _sbln[i] = 0.2F;
                        }
                        if (Trackers.Skd[i213] == 1)
                        {
                            _sbln[i] = 0.4F;
                        }
                        if (Trackers.Skd[i213] == 2)
                        {
                            _sbln[i] = 0.45F;
                        }
                        for (var i214 = 0; i214 < 3; i214++)
                        {
                            _srgb[i, i214] = (Trackers.C[i213][i214] + ais[i214]) / 2;
                        }
                        bool208 = true;
                    }
                }
                if (!bool208)
                {
                    for (var i215 = 0; i215 < 3; i215++)
                    {
                        _srgb[i, i215] = (Medium.Crgrnd[i215] + ais[i215]) / 2;
                    }
                }
                var f = (float) (0.1 + Medium.Random());
                if (f > 1.0F)
                {
                    f = 1.0F;
                }
                Scx[i] = (int) (Scx[i] * f);
                Scz[i] = (int) (Scx[i] * f);
                for (var i216 = 0; i216 < 8; i216++)
                {
                    _smag[i, i216] = Osmag[i] * Medium.Random() * 50.0F;
                }
                for (var i217 = 0; i217 < 8; i217++)
                {
                    var i218 = i217 - 1;
                    if (i218 == -1)
                    {
                        i218 = 7;
                    }
                    var i219 = i217 + 1;
                    if (i219 == 8)
                    {
                        i219 = 0;
                    }
                    _smag[i, i217] = ((_smag[i, i218] + _smag[i, i219]) / 2.0F + _smag[i, i217]) / 2.0F;
                }
                _smag[i, 6] = _smag[i, 7];
            }
            var i220 = Medium.Cx + (int) ((Sx[i] - Medium.X - Medium.Cx) * Medium.Cos(Medium.Xz) -
                                          (Sz[i] - Medium.Z - Medium.Cz) * Medium.Sin(Medium.Xz));
            var i221 = Medium.Cz + (int) ((Sx[i] - Medium.X - Medium.Cx) * Medium.Sin(Medium.Xz) +
                                          (Sz[i] - Medium.Z - Medium.Cz) * Medium.Cos(Medium.Xz));
            var i222 = Medium.Cy + (int) ((Sy[i] - Medium.Y - Medium.Cy - _smag[i, 7]) * Medium.Cos(Medium.Zy) -
                                          (i221 - Medium.Cz) * Medium.Sin(Medium.Zy));
            i221 = Medium.Cz + (int) ((Sy[i] - Medium.Y - Medium.Cy - _smag[i, 7]) * Medium.Sin(Medium.Zy) +
                                      (i221 - Medium.Cz) * Medium.Cos(Medium.Zy));
            Sx[i] += Scx[i] / (Stg[i] + 1);
            Sz[i] += Scz[i] / (Stg[i] + 1);
            ais = new int[8];
            var is223 = new int[8];
            ais[0] = Xs((int) (i220 + _smag[i, 0] * 0.9238F * 1.5F), i221);
            is223[0] = Ys((int) (i222 + _smag[i, 0] * 0.3826F * 1.5F), i221);
            ais[1] = Xs((int) (i220 + _smag[i, 1] * 0.9238F * 1.5F), i221);
            is223[1] = Ys((int) (i222 - _smag[i, 1] * 0.3826F * 1.5F), i221);
            ais[2] = Xs((int) (i220 + _smag[i, 2] * 0.3826F), i221);
            is223[2] = Ys((int) (i222 - _smag[i, 2] * 0.9238F), i221);
            ais[3] = Xs((int) (i220 - _smag[i, 3] * 0.3826F), i221);
            is223[3] = Ys((int) (i222 - _smag[i, 3] * 0.9238F), i221);
            ais[4] = Xs((int) (i220 - _smag[i, 4] * 0.9238F * 1.5F), i221);
            is223[4] = Ys((int) (i222 - _smag[i, 4] * 0.3826F * 1.5F), i221);
            ais[5] = Xs((int) (i220 - _smag[i, 5] * 0.9238F * 1.5F), i221);
            is223[5] = Ys((int) (i222 + _smag[i, 5] * 0.3826F * 1.5F), i221);
            ais[6] = Xs((int) (i220 - _smag[i, 6] * 0.3826F * 1.7F), i221);
            is223[6] = Ys((int) (i222 + _smag[i, 6] * 0.9238F), i221);
            ais[7] = Xs((int) (i220 + _smag[i, 7] * 0.3826F * 1.7F), i221);
            is223[7] = Ys((int) (i222 + _smag[i, 7] * 0.9238F), i221);
            for (var i224 = 0; i224 < 7; i224++)
            {
                _smag[i, i224] += 5.0F + Medium.Random() * 15.0F;
            }
            _smag[i, 7] = _smag[i, 6];
            var bool225 = true;
            var i226 = 0;
            var i227 = 0;
            var i228 = 0;
            var i229 = 0;
            for (var i230 = 0; i230 < 8; i230++)
            {
                if (is223[i230] < Medium.Ih || i221 < 10)
                {
                    i226++;
                }
                if (is223[i230] > Medium.H || i221 < 10)
                {
                    i227++;
                }
                if (ais[i230] < Medium.Iw || i221 < 10)
                {
                    i228++;
                }
                if (ais[i230] > Medium.W || i221 < 10)
                {
                    i229++;
                }
            }
            if (i228 == 4 || i226 == 4 || i227 == 4 || i229 == 4)
            {
                bool225 = false;
            }
            if (bool225)
            {
                var i231 = _srgb[i, 0];
                var i232 = _srgb[i, 1];
                var i233 = _srgb[i, 2];
                for (var i234 = 0; i234 < 16; i234++)
                {
                    if (_sav[i] > Medium.Fade[i234])
                    {
                        i231 = (i231 * Medium.Fogd + Medium.Cfade[0]) / (Medium.Fogd + 1);
                        i232 = (i232 * Medium.Fogd + Medium.Cfade[1]) / (Medium.Fogd + 1);
                        i233 = (i233 * Medium.Fogd + Medium.Cfade[2]) / (Medium.Fogd + 1);
                    }
                }

                G.SetColor(new Color(i231, i232, i233));
                var f = _sbln[i] - Stg[i] * (_sbln[i] / 8.0F);
                G.SetAlpha(f);
                G.FillPolygon(ais, is223, 8);
                G.SetAlpha(1.0F);
            }
            if (Stg[i] == 7)
            {
                Stg[i] = 0;
            }
            else
            {
                Stg[i]++;
            }
        }
    }

    private int Py(int i, int i269, int i270, int i271)
    {
        return (i - i269) * (i - i269) + (i270 - i271) * (i270 - i271);
    }

    private void Rot(int[] ais, int[] is272, int i, int i273, float i274, int i275)
    {
        if ((int)i274 != 0)
        {
            for (var i276 = 0; i276 < i275; i276++)
            {
                var i277 = ais[i276];
                var i278 = is272[i276];
                ais[i276] = i + (int) ((i277 - i) * Medium.Cos(i274) - (i278 - i273) * Medium.Sin(i274));
                is272[i276] = i273 + (int) ((i277 - i) * Medium.Sin(i274) + (i278 - i273) * Medium.Cos(i274));
            }
        }
    }

    internal void Spark(float f, float f235, float f236, float f237, float f238, float f239, int i)
    {
        if (i != 1)
        {
            Srx = (int) (f - _sprkat * Medium.Sin(Xz));
            Sry = (int) (f235 - _sprkat * Medium.Cos(Zy) * Medium.Cos(Xy));
            Srz = (int) (f236 + _sprkat * Medium.Cos(Xz));
            Sprk = 1;
        }
        else
        {
            Sprk++;
            if (Sprk == 4)
            {
                Srx = (int) (X + f237);
                Sry = (int) f235;
                Srz = (int) (Z + f239);
                Sprk = 5;
            }
            else
            {
                Srx = (int) f;
                Sry = (int) f235;
                Srz = (int) f236;
            }
        }
        if (i == 2)
        {
            Sprk = 6;
        }
        Rcx = f237;
        Rcy = f238;
        Rcz = f239;
    }

    private int Xs(int i, int i260)
    {
        if (i260 < 50)
        {
            i260 = 50;
        }
        return (i260 - Medium.FocusPoint) * (Medium.Cx - i) / i260 + i;
    }

    private int Ys(int i, int i261)
    {
        if (i261 < 50)
        {
            i261 = 50;
        }
        return (i261 - Medium.FocusPoint) * (Medium.Cy - i) / i261 + i;
    }
}