using System;
using NFMWorld.Util;

namespace NFMWorld.Mad;

internal class Mad
{
    internal bool Btab;
    internal int Capcnt;
    internal bool Capsized;
    private readonly bool[] _caught = new bool[8];
    internal Stat Stat;
    internal int Clear;
    internal int Cn;
    internal int Cntdest;
    private int _cntouch;
    private bool _colidim;
    private readonly int[,] _crank = new int[4, 4];
    internal int Cxz;
    private int _dcnt;
    internal float Dcomp;
    internal bool Dest;
    private readonly bool[] _dominate = new bool[8];
    private readonly float _drag = 0.5F;
    private int _fixes = -1;
    private int _focus = -1;
    private float _forca;
    internal bool Ftab;
    private int _fxz;
    internal bool Gtouch;
    internal int Hitmag;
    internal int Im;
    internal int Lastcolido;
    internal float Lcomp;
    private readonly int[,] _lcrank = new int[4, 4];
    internal int Loop;
    private int _lxz;
    internal int Missedcp;
    internal bool Mtouch;
    internal int Mxz;
    private int _nbsq;
    internal bool Newcar;
    internal int Newedcar;
    internal int Nlaps;
    private int _nmlt = 1;
    internal bool Nofocus;
    internal int Outshakedam = 0;
    internal int Pcleared;
    internal bool Pd;
    internal bool Pl;
    private int _pmlt = 1;
    internal int Point;
    internal float Power = 75.0F;
    internal float Powerup;
    internal bool Pr;
    internal bool Pu;
    internal bool Pushed;
    internal int Pxy;
    internal int Pzy;
    internal float Rcomp;
    private int _rpdcatch;
    internal bool Rtab;
    internal readonly float[] Scx = new float[4];
    internal readonly float[] Scy = new float[4];
    internal readonly float[] Scz = new float[4];
    internal int Shakedam;
    internal int Skid;
    internal float Speed;
    internal int Squash;
    private int _srfcnt;
    internal bool Surfer;
    private float _tilt;
    internal int Travxy;
    internal int Travxz;
    internal int Travzy;
    internal int Trcnt;
    internal int Txz;
    internal float Ucomp;
    internal bool Wtouch;
    private int _xtpower;

    internal Mad(Stat stat, int i)
    {
        Stat = stat;
        Im = i;
    }

    public void SetStat(Stat stat)
    {
        Stat = stat;
    }

/*
    internal void Colide(ContO conto, Mad mad118, ContO conto119)
    {
        var fs = new float[4];
        var fs120 = new float[4];
        var fs121 = new float[4];
        var fs122 = new float[4];
        var fs123 = new float[4];
        var fs124 = new float[4];
        for (var i1 = 0; i1 < 4; i1++)
        {
            fs[i1] = conto.X + conto.Keyx[i1];
            if (Capsized)
            {
                fs120[i1] = conto.Y + Stat.Flipy + Squash;
            }
            else
            {
                fs120[i1] = conto.Y + conto.Grat;
            }
            fs121[i1] = conto.Z + conto.Keyz[i1];
            fs122[i1] = conto119.X + conto119.Keyx[i1];
            if (Capsized)
            {
                fs123[i1] = conto119.Y + mad118.Stat.Flipy + mad118.Squash;
            }
            else
            {
                fs123[i1] = conto119.Y + conto119.Grat;
            }
            fs124[i1] = conto119.Z + conto119.Keyz[i1];
        }
        Rot(fs, fs120, conto.X, conto.Y, conto.Xy, 4);
        Rot(fs120, fs121, conto.Y, conto.Z, conto.Zy, 4);
        Rot(fs, fs121, conto.X, conto.Z, conto.Xz, 4);
        Rot(fs122, fs123, conto119.X, conto119.Y, conto119.Xy, 4);
        Rot(fs123, fs124, conto119.Y, conto119.Z, conto119.Zy, 4);
        Rot(fs122, fs124, conto119.X, conto119.Z, conto119.Xz, 4);
        if (Rpy(conto.X, conto119.X, conto.Y, conto119.Y, conto.Z, conto119.Z) <
            (conto.MaxR * conto.MaxR + conto119.MaxR * conto119.MaxR) * 1.5)
        {
            if (!_caught[mad118.Im] && (Speed != 0.0F || mad118.Speed != 0.0F))
            {
                if (Math.Abs(Power * Speed * Stat.Moment) !=
                    Math.Abs(mad118.Power * mad118.Speed * mad118.Stat.Moment))
                {
                    _dominate[mad118.Im] = Math.Abs(Power * Speed * Stat.Moment) >
                                           Math.Abs(mad118.Power * mad118.Speed * mad118.Stat.Moment);
                }
                else
                {
                    _dominate[mad118.Im] = Stat.Moment > mad118.Stat.Moment;
                }

                _caught[mad118.Im] = true;
            }
        }
        else if (_caught[mad118.Im])
        {
            _caught[mad118.Im] = false;
        }
        var i = 0;
        var i125 = 0;
        if (_dominate[mad118.Im])
        {
            var i126 =
                (int) (((Scz[0] - mad118.Scz[0] + Scz[1] - mad118.Scz[1] + Scz[2] - mad118.Scz[2] + Scz[3] -
                         mad118.Scz[3]) *
                        (Scz[0] - mad118.Scz[0] + Scz[1] - mad118.Scz[1] + Scz[2] - mad118.Scz[2] + Scz[3] -
                         mad118.Scz[3]) +
                        (Scx[0] - mad118.Scx[0] + Scx[1] - mad118.Scx[1] + Scx[2] - mad118.Scx[2] + Scx[3] -
                         mad118.Scx[3]) * (Scx[0] - mad118.Scx[0] + Scx[1] - mad118.Scx[1] + Scx[2] -
                            mad118.Scx[2] + Scx[3] - mad118.Scx[3])) / 16.0F);
            var i127 = 7000;
            var f = 1.0F;
            if (XTGraphics.Multion != 0)
            {
                i127 = 28000;
                f = 1.27F;
            }
            for (var i128 = 0; i128 < 4; i128++)
            {
                for (var i129 = 0; i129 < 4; i129++)
                {
                    if (Rpy(fs[i128], fs122[i129], fs120[i128], fs123[i129], fs121[i128], fs124[i129]) <
                        (i126 + i127) * (mad118.Stat.Comprad + Stat.Comprad))
                    {
                        if (Math.Abs(Scx[i128] * Stat.Moment) > Math.Abs(mad118.Scx[i129] * mad118.Stat.Moment))
                        {
                            var f130 = mad118.Scx[i129] * Stat.Revpush;
                            if (f130 > 300.0F)
                            {
                                f130 = 300.0F;
                            }
                            if (f130 < -300.0F)
                            {
                                f130 = -300.0F;
                            }
                            var f131 = Scx[i128] * Stat.Push;
                            if (f131 > 300.0F)
                            {
                                f131 = 300.0F;
                            }
                            if (f131 < -300.0F)
                            {
                                f131 = -300.0F;
                            }
                            mad118.Scx[i129] += f131;
                            if (Im == XTGraphics.Im)
                            {
                                mad118._colidim = true;
                            }
                            i += mad118.Regx(i129, f131 * Stat.Moment * f, conto119);
                            if (mad118._colidim)
                            {
                                mad118._colidim = false;
                            }
                            Scx[i128] -= f130;
                            i125 += Regx(i128, -f130 * Stat.Moment * f, conto);
                            Scy[i128] -= Stat.Revlift;
                            if (Im == XTGraphics.Im)
                            {
                                mad118._colidim = true;
                            }
                            i += mad118.Regy(i129, Stat.Revlift * 7, conto119);
                            if (mad118._colidim)
                            {
                                mad118._colidim = false;
                            }
                            if (Medium.RandomBoolean())
                            {
                                conto119.Spark((fs[i128] + fs122[i129]) / 2.0F, (fs120[i128] + fs123[i129]) / 2.0F,
                                    (fs121[i128] + fs124[i129]) / 2.0F, (mad118.Scx[i129] + Scx[i128]) / 4.0F,
                                    (mad118.Scy[i129] + Scy[i128]) / 4.0F, (mad118.Scz[i129] + Scz[i128]) / 4.0F,
                                    2);
                            }
                        }
                        if (Math.Abs(Scz[i128] * Stat.Moment) > Math.Abs(mad118.Scz[i129] * mad118.Stat.Moment))
                        {
                            var f132 = mad118.Scz[i129] * Stat.Revpush;
                            if (f132 > 300.0F)
                            {
                                f132 = 300.0F;
                            }
                            if (f132 < -300.0F)
                            {
                                f132 = -300.0F;
                            }
                            var f133 = Scz[i128] * Stat.Push;
                            if (f133 > 300.0F)
                            {
                                f133 = 300.0F;
                            }
                            if (f133 < -300.0F)
                            {
                                f133 = -300.0F;
                            }
                            mad118.Scz[i129] += f133;
                            if (Im == XTGraphics.Im)
                            {
                                mad118._colidim = true;
                            }
                            i += mad118.Regz(i129, f133 * Stat.Moment * f, conto119);
                            if (mad118._colidim)
                            {
                                mad118._colidim = false;
                            }
                            Scz[i128] -= f132;
                            i125 += Regz(i128, -f132 * Stat.Moment * f, conto);
                            Scy[i128] -= Stat.Revlift;
                            if (Im == XTGraphics.Im)
                            {
                                mad118._colidim = true;
                            }
                            i += mad118.Regy(i129, Stat.Revlift * 7, conto119);
                            if (mad118._colidim)
                            {
                                mad118._colidim = false;
                            }
                            if (Medium.RandomBoolean())
                            {
                                conto119.Spark((fs[i128] + fs122[i129]) / 2.0F, (fs120[i128] + fs123[i129]) / 2.0F,
                                    (fs121[i128] + fs124[i129]) / 2.0F, (mad118.Scx[i129] + Scx[i128]) / 4.0F,
                                    (mad118.Scy[i129] + Scy[i128]) / 4.0F, (mad118.Scz[i129] + Scz[i128]) / 4.0F,
                                    2);
                            }
                        }
                        if (Im == XTGraphics.Im)
                        {
                            mad118.Lastcolido = 70;
                        }
                        if (mad118.Im == XTGraphics.Im)
                        {
                            Lastcolido = 70;
                        }
                        mad118.Scy[i129] -= Stat.Lift;
                    }
                }
            }
        }
        if (XTGraphics.Multion == 1)
        {
            if (mad118.Im == XTGraphics.Im && i != 0)
            {
                XTGraphics.Dcrashes[Im] += i;
            }
            if (Im == XTGraphics.Im && i125 != 0)
            {
                XTGraphics.Dcrashes[mad118.Im] += i125;
            }
        }
    }*/

    private void Distruct(ContO conto)
    {
        for (var i = 0; i < conto.Npl; i++)
        {
            if (conto.P[i].Wz == 0 || conto.P[i].Gr == -17 || conto.P[i].Gr == -16)
            {
                conto.P[i].Embos = 1;
            }
        }
    }

    internal void Drive(Control control, ContO conto)
    {
        var i = 1;
        var i4 = 1;
        var abool = false;
        var bool5 = false;
        var bool6 = false;
        Capsized = false;
        int i7;
        for (i7 = Math.Abs(Pzy); i7 > 270; i7 -= 360)
        {
        }
        i7 = Math.Abs(i7);
        if (i7 > 90)
        {
            abool = true;
        }
        var bool8 = false;
        int i9;
        for (i9 = Math.Abs(Pxy); i9 > 270; i9 -= 360)
        {
        }
        i9 = Math.Abs(i9);
        if (i9 > 90)
        {
            bool8 = true;
            i4 = -1;
        }
        var i10 = conto.Grat;
        if (abool)
        {
            if (bool8)
            {
                bool8 = false;
                bool5 = true;
            }
            else
            {
                bool8 = true;
                Capsized = true;
            }
            i = -1;
        }
        else if (bool8)
        {
            Capsized = true;
        }
        if (Capsized)
        {
            i10 = Stat.Flipy + Squash;
        }
        control.Zyinv = abool;
        var f = 0.0F;
        var f11 = 0.0F;
        var f12 = 0.0F;
        if (Mtouch)
        {
            Loop = 0;
        }
        if (Wtouch)
        {
            if (Loop == 2 || Loop == -1)
            {
                Loop = -1;
                if (control.Left)
                {
                    Pl = true;
                }
                if (control.Right)
                {
                    Pr = true;
                }
                if (control.Up)
                {
                    Pu = true;
                }
                if (control.Down)
                {
                    Pd = true;
                }
            }
            Ucomp = 0.0F;
            Dcomp = 0.0F;
            Lcomp = 0.0F;
            Rcomp = 0.0F;
        }
        if (control.Handb)
        {
            if (!Pushed)
            {
                if (!Wtouch)
                {
                    if (Loop == 0)
                    {
                        Loop = 1;
                    }
                }
                else if (Gtouch)
                {
                    Pushed = true;
                }
            }
        }
        else
        {
            Pushed = false;
        }
        if (Loop == 1)
        {
            var f13 = (Scy[0] + Scy[1] + Scy[2] + Scy[3]) / 4.0F;
            for (var i14 = 0; i14 < 4; i14++)
            {
                Scy[i14] = f13;
            }
            Loop = 2;
        }
        if (!Dest)
        {
            if (Loop == 2)
            {
                if (control.Up)
                {
                    if (Ucomp == 0.0F)
                    {
                        Ucomp = 10.0F + (Scy[0] + 50.0F) / 20.0F;
                        if (Ucomp < 5.0F)
                        {
                            Ucomp = 5.0F;
                        }
                        if (Ucomp > 10.0F)
                        {
                            Ucomp = 10.0F;
                        }
                        Ucomp *= Stat.Airs;
                    }
                    if (Ucomp < 20.0F)
                    {
                        Ucomp += (int) (0.5 * Stat.Airs);
                    }
                    f = -Stat.Airc * Medium.Sin(conto.Xz) * i4;
                    f11 = Stat.Airc * Medium.Cos(conto.Xz) * i4;
                }
                else if (Ucomp != 0.0F && Ucomp > -2.0F)
                {
                    Ucomp -= (int) (0.5 * Stat.Airs);
                }
                if (control.Down)
                {
                    if (Dcomp == 0.0F)
                    {
                        Dcomp = 10.0F + (Scy[0] + 50.0F) / 20.0F;
                        if (Dcomp < 5.0F)
                        {
                            Dcomp = 5.0F;
                        }
                        if (Dcomp > 10.0F)
                        {
                            Dcomp = 10.0F;
                        }
                        Dcomp *= Stat.Airs;
                    }
                    if (Dcomp < 20.0F)
                    {
                        Dcomp += (int) (0.5 * Stat.Airs);
                    }
                    f12 = -Stat.Airc;
                }
                else if (Dcomp != 0.0F && Ucomp > -2.0F)
                {
                    Dcomp -= (int) (0.5 * Stat.Airs);
                }
                if (control.Left)
                {
                    if (Lcomp == 0.0F)
                    {
                        Lcomp = 5.0F;
                    }
                    if (Lcomp < 20.0F)
                    {
                        Lcomp += 2.0F * Stat.Airs;
                    }
                    f = -Stat.Airc * Medium.Cos(conto.Xz) * i;
                    f11 = -Stat.Airc * Medium.Sin(conto.Xz) * i;
                }
                else if (Lcomp > 0.0F)
                {
                    Lcomp -= 2.0F * Stat.Airs;
                }
                if (control.Right)
                {
                    if (Rcomp == 0.0F)
                    {
                        Rcomp = 5.0F;
                    }
                    if (Rcomp < 20.0F)
                    {
                        Rcomp += 2.0F * Stat.Airs;
                    }
                    f = Stat.Airc * Medium.Cos(conto.Xz) * i;
                    f11 = Stat.Airc * Medium.Sin(conto.Xz) * i;
                }
                else if (Rcomp > 0.0F)
                {
                    Rcomp -= 2.0F * Stat.Airs;
                }
                Pzy += (int) ((Dcomp - Ucomp) * Medium.Cos(Pxy));
                if (abool)
                {
                    conto.Xz += (int) ((Dcomp - Ucomp) * Medium.Sin(Pxy));
                }
                else
                {
                    conto.Xz -= (int) ((Dcomp - Ucomp) * Medium.Sin(Pxy));
                }
                Pxy += (int) (Rcomp - Lcomp);
            }
            else
            {
                var f15 = Power;
                if (f15 < 40.0F)
                {
                    f15 = 40.0F;
                }
                if (control.Down)
                {
                    if (Speed > 0.0F)
                    {
                        Speed -= Stat.Handb / 2;
                    }
                    else
                    {
                        var i16 = 0;
                        for (var i17 = 0; i17 < 2; i17++)
                        {
                            if (Speed <= -(Stat.Swits[i17] / 2 + f15 * Stat.Swits[i17] / 196.0F))
                            {
                                i16++;
                            }
                        }

                        if (i16 != 2)
                        {
                            Speed -= Stat.Acelf[i16] / 2.0F + f15 * Stat.Acelf[i16] / 196.0F;
                        }
                        else
                        {
                            Speed = -(Stat.Swits[1] / 2 + f15 * Stat.Swits[1] / 196.0F);
                        }
                    }
                }

                if (control.Up)
                {
                    if (Speed < 0.0F)
                    {
                        Speed += Stat.Handb;
                    }
                    else
                    {
                        var i18 = 0;
                        for (var i19 = 0; i19 < 3; i19++)
                        {
                            if (Speed >= Stat.Swits[i19] / 2 + f15 * Stat.Swits[i19] / 196.0F)
                            {
                                i18++;
                            }
                        }

                        if (i18 != 3)
                        {
                            Speed += Stat.Acelf[i18] / 2.0F + f15 * Stat.Acelf[i18] / 196.0F;
                        }
                        else
                        {
                            Speed = Stat.Swits[2] / 2 + f15 * Stat.Swits[2] / 196.0F;
                        }
                    }
                }

                if (control.Handb && Math.Abs(Speed) > Stat.Handb)
                {
                    if (Speed < 0.0F)
                    {
                        Speed += Stat.Handb;
                    }
                    else
                    {
                        Speed -= Stat.Handb;
                    }
                }

                if (Loop == -1 && conto.Y < 100)
                {
                    if (control.Left)
                    {
                        if (!Pl)
                        {
                            if (Lcomp == 0.0F)
                            {
                                Lcomp = 5.0F * Stat.Airs;
                            }
                            if (Lcomp < 20.0F)
                            {
                                Lcomp += 2.0F * Stat.Airs;
                            }
                        }
                    }
                    else
                    {
                        if (Lcomp > 0.0F)
                        {
                            Lcomp -= 2.0F * Stat.Airs;
                        }
                        Pl = false;
                    }
                    if (control.Right)
                    {
                        if (!Pr)
                        {
                            if (Rcomp == 0.0F)
                            {
                                Rcomp = 5.0F * Stat.Airs;
                            }
                            if (Rcomp < 20.0F)
                            {
                                Rcomp += 2.0F * Stat.Airs;
                            }
                        }
                    }
                    else
                    {
                        if (Rcomp > 0.0F)
                        {
                            Rcomp -= 2.0F * Stat.Airs;
                        }
                        Pr = false;
                    }
                    if (control.Up)
                    {
                        if (!Pu)
                        {
                            if (Ucomp == 0.0F)
                            {
                                Ucomp = 5.0F * Stat.Airs;
                            }
                            if (Ucomp < 20.0F)
                            {
                                Ucomp += 2.0F * Stat.Airs;
                            }
                        }
                    }
                    else
                    {
                        if (Ucomp > 0.0F)
                        {
                            Ucomp -= 2.0F * Stat.Airs;
                        }
                        Pu = false;
                    }
                    if (control.Down)
                    {
                        if (!Pd)
                        {
                            if (Dcomp == 0.0F)
                            {
                                Dcomp = 5.0F * Stat.Airs;
                            }
                            if (Dcomp < 20.0F)
                            {
                                Dcomp += 2.0F * Stat.Airs;
                            }
                        }
                    }
                    else
                    {
                        if (Dcomp > 0.0F)
                        {
                            Dcomp -= 2.0F * Stat.Airs;
                        }
                        Pd = false;
                    }
                    Pzy += (int) ((Dcomp - Ucomp) * Medium.Cos(Pxy));
                    if (abool)
                    {
                        conto.Xz += (int) ((Dcomp - Ucomp) * Medium.Sin(Pxy));
                    }
                    else
                    {
                        conto.Xz -= (int) ((Dcomp - Ucomp) * Medium.Sin(Pxy));
                    }
                    Pxy += (int) (Rcomp - Lcomp);
                }
            }
        }

        var f20 = 20.0F * Speed / (154.0F * Stat.Simag);
        if (f20 > 20.0F)
        {
            f20 = 20.0F;
        }
        conto.Wzy -= (int) (f20);
        if (conto.Wzy < -30)
        {
            conto.Wzy += 30;
        }
        if (conto.Wzy > 30)
        {
            conto.Wzy -= 30;
        }
        if (control.Right)
        {
            conto.Wxz -= Stat.Turn;
            if (conto.Wxz < -36)
            {
                conto.Wxz = -36;
            }
        }
        if (control.Left)
        {
            conto.Wxz += Stat.Turn;
            if (conto.Wxz > 36)
            {
                conto.Wxz = 36;
            }
        }
        if (conto.Wxz != 0 && !control.Left && !control.Right)
        {
            if (Math.Abs(Speed) < 10.0F)
            {
                if (Math.Abs(conto.Wxz) == 1)
                {
                    conto.Wxz = 0;
                }
                if (conto.Wxz > 0)
                {
                    conto.Wxz--;
                }
                if (conto.Wxz < 0)
                {
                    conto.Wxz++;
                }
            }
            else
            {
                if (Math.Abs(conto.Wxz) < Stat.Turn * 2)
                {
                    conto.Wxz = 0;
                }
                if (conto.Wxz > 0)
                {
                    conto.Wxz -= Stat.Turn * 2;
                }
                if (conto.Wxz < 0)
                {
                    conto.Wxz += Stat.Turn * 2;
                }
            }
        }

        var i21 = (int) (3600.0F / (Speed * Speed));
        if (i21 < 5)
        {
            i21 = 5;
        }
        if (Speed < 0.0F)
        {
            i21 = -i21;
        }
        if (Wtouch)
        {
            if (!Capsized)
            {
                if (!control.Handb)
                {
                    _fxz = conto.Wxz / (i21 * 3);
                }
                else
                {
                    _fxz = conto.Wxz / i21;
                }
                conto.Xz += conto.Wxz / i21;
            }
            Wtouch = false;
            Gtouch = false;
        }
        else
        {
            conto.Xz += _fxz;
        }
        if (Speed > 30.0F || Speed < -100.0F)
        {
            while (SafeMath.Abs(Mxz - Cxz) > 180)
            {
                if (Cxz > Mxz)
                {
                    Cxz -= 360;
                }
                else if (Cxz < Mxz)
                {
                    Cxz += 360;
                }
            }

            if (SafeMath.Abs(Mxz - Cxz) < 30)
            {
                Cxz += (int) ((Mxz - Cxz) / 4.0F);
            }
            else
            {
                if (Cxz > Mxz)
                {
                    Cxz -= 10;
                }
                if (Cxz < Mxz)
                {
                    Cxz += 10;
                }
            }
        }
        var fs = new float[4];
        var fs22 = new float[4];
        var fs23 = new float[4];
        for (var i24 = 0; i24 < 4; i24++)
        {
            fs[i24] = conto.Keyx[i24] + conto.X;
            fs23[i24] = i10 + conto.Y;
            fs22[i24] = conto.Z + conto.Keyz[i24];
            Scy[i24] += 7.0F;
        }
        Rot(fs, fs23, conto.X, conto.Y, Pxy, 4);
        Rot(fs23, fs22, conto.Y, conto.Z, Pzy, 4);
        Rot(fs, fs22, conto.X, conto.Z, conto.Xz, 4);
        var bool25 = false;
        double d;
        var i26 = (int) ((Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4.0F);
        var i27 = (int) ((Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4.0F);
        for (var i28 = 0; i28 < 4; i28++)
        {
            if (Scx[i28] - i26 > 200.0F)
            {
                Scx[i28] = 200 + i26;
            }
            if (Scx[i28] - i26 < -200.0F)
            {
                Scx[i28] = i26 - 200;
            }
            if (Scz[i28] - i27 > 200.0F)
            {
                Scz[i28] = 200 + i27;
            }
            if (Scz[i28] - i27 < -200.0F)
            {
                Scz[i28] = i27 - 200;
            }
        }
        for (var i29 = 0; i29 < 4; i29++)
        {
            fs23[i29] += Scy[i29];
            fs[i29] += (Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4.0F;
            fs22[i29] += (Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4.0F;
        }
        var i30 = (conto.X - Trackers.Sx) / 3000;
        if (i30 > Trackers.Ncx)
        {
            i30 = Trackers.Ncx;
        }
        if (i30 < 0)
        {
            i30 = 0;
        }
        var i31 = (conto.Z - Trackers.Sz) / 3000;
        if (i31 > Trackers.Ncz)
        {
            i31 = Trackers.Ncz;
        }
        if (i31 < 0)
        {
            i31 = 0;
        }
        var i32 = 1;
        for (var i33 = 0; i33 < Trackers.Sect[i30, i31].Length; i33++)
        {
            var i34 = Trackers.Sect[i30, i31][i33];
            if (Math.Abs(Trackers.Zy[i34]) != 90 && Math.Abs(Trackers.Xy[i34]) != 90 &&
                Math.Abs(conto.X - Trackers.X[i34]) < Trackers.Radx[i34] &&
                Math.Abs(conto.Z - Trackers.Z[i34]) < Trackers.Radz[i34] &&
                (!Trackers.Decor[i34] || Medium.Resdown != 2 /*|| XTGraphics.Multion != 0*/))
            {
                i32 = Trackers.Skd[i34];
            }
        }
        if (Mtouch)
        {
            var f35 = Stat.Grip;
            f35 -= Math.Abs(Txz - conto.Xz) * Speed / 250.0F;
            if (control.Handb)
            {
                f35 -= Math.Abs(Txz - conto.Xz) * 4;
            }
            if (f35 < Stat.Grip)
            {
                if (Skid != 2)
                {
                    Skid = 1;
                }
                Speed -= Speed / 100.0F;
            }
            else if (Skid == 1)
            {
                Skid = 2;
            }
            if (i32 == 1)
            {
                f35 = (int) (f35 * 0.75);
            }
            if (i32 == 2)
            {
                f35 = (int) (f35 * 0.55);
            }
            var i36 = -(int) (Speed * Medium.Sin(conto.Xz) * Medium.Cos(Pzy));
            var i37 = (int) (Speed * Medium.Cos(conto.Xz) * Medium.Cos(Pzy));
            var i38 = -(int) (Speed * Medium.Sin(Pzy));
            if (Capsized || Dest /*|| CheckPoints.Haltall*/)
            {
                i36 = 0;
                i37 = 0;
                i38 = 0;
                f35 = Stat.Grip / 5.0F;
                if (Speed > 0.0F)
                {
                    Speed -= 2.0F;
                }
                else
                {
                    Speed += 2.0F;
                }
            }
            if (Math.Abs(Speed) > _drag)
            {
                if (Speed > 0.0F)
                {
                    Speed -= _drag;
                }
                else
                {
                    Speed += _drag;
                }
            }
            else
            {
                Speed = 0.0F;
            }
            if (Cn == 8 && f35 < 5.0F)
            {
                f35 = 5.0F;
            }
            if (f35 < 1.0F)
            {
                f35 = 1.0F;
            }
            var f39 = 0.0F;
            var f40 = 0.0F;
            for (var i41 = 0; i41 < 4; i41++)
            {
                if (Math.Abs(Scx[i41] - i36) > f35)
                {
                    if (Scx[i41] < i36)
                    {
                        Scx[i41] += f35;
                    }
                    else
                    {
                        Scx[i41] -= f35;
                    }
                }
                else
                {
                    Scx[i41] = i36;
                }
                if (Math.Abs(Scz[i41] - i37) > f35)
                {
                    if (Scz[i41] < i37)
                    {
                        Scz[i41] += f35;
                    }
                    else
                    {
                        Scz[i41] -= f35;
                    }
                }
                else
                {
                    Scz[i41] = i37;
                }
                if (Math.Abs(Scy[i41] - i38) > f35)
                {
                    if (Scy[i41] < i38)
                    {
                        Scy[i41] += f35;
                    }
                    else
                    {
                        Scy[i41] -= f35;
                    }
                }
                else
                {
                    Scy[i41] = i38;
                }
                if (f35 < Stat.Grip)
                {
                    if (Txz != conto.Xz)
                    {
                        _dcnt++;
                    }
                    else if (_dcnt != 0)
                    {
                        _dcnt = 0;
                    }
                    if (_dcnt > 40.0F * f35 / Stat.Grip || Capsized)
                    {
                        var f42 = 1.0F;
                        if (i32 != 0)
                        {
                            f42 = 1.2F;
                        }
                        if (Medium.Random() > 0.65)
                        {
                            conto.Dust(i41, fs[i41], fs23[i41], fs22[i41], (int) Scx[i41], (int) Scz[i41],
                                f42 * Stat.Simag, (int) _tilt, Capsized && Mtouch);
                            if (/*Im == XTGraphics.Im &&*/ !Capsized)
                            {
                                //XTPart2.Skidf(Im, i32,
                                //    (float) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (i32 == 1 && Medium.Random() > 0.8)
                        {
                            conto.Dust(i41, fs[i41], fs23[i41], fs22[i41], (int) Scx[i41], (int) Scz[i41],
                                1.1F * Stat.Simag, (int) _tilt, Capsized && Mtouch);
                        }
                        if ((i32 == 2 || i32 == 3) && Medium.Random() > 0.6)
                        {
                            conto.Dust(i41, fs[i41], fs23[i41], fs22[i41], (int) Scx[i41], (int) Scz[i41],
                                1.15F * Stat.Simag, (int) _tilt, Capsized && Mtouch);
                        }
                    }
                }
                else if (_dcnt != 0)
                {
                    _dcnt -= 2;
                    if (_dcnt < 0)
                    {
                        _dcnt = 0;
                    }
                }
                if (i32 == 3)
                {
                    var i43 = (int) (Medium.Random() * 4.0F);
                    Scy[i43] = (float) (-100.0F * Medium.Random() * (Speed / Stat.Swits[2]) * (Stat.Bounce - 0.3));
                }
                if (i32 == 4)
                {
                    var i44 = (int) (Medium.Random() * 4.0F);
                    Scy[i44] = (float) (-150.0F * Medium.Random() * (Speed / Stat.Swits[2]) * (Stat.Bounce - 0.3));
                }
                f39 += Scx[i41];
                f40 += Scz[i41];
            }
            Txz = conto.Xz;
            if (f39 > 0.0F)
            {
                i = -1;
            }
            else
            {
                i = 1;
            }
            d = f40 / Math.Sqrt(f39 * f39 + f40 * f40);
            Mxz = (int) (Math.Acos(d) / 0.017453292519943295 * i);
            if (Skid == 2)
            {
                if (!Capsized)
                {
                    f39 /= 4.0F;
                    f40 /= 4.0F;
                    if (bool5)
                    {
                        Speed = -((float) Math.Sqrt(f39 * f39 + f40 * f40) * Medium.Cos(Mxz - conto.Xz));
                    }
                    else
                    {
                        Speed = (float) Math.Sqrt(f39 * f39 + f40 * f40) * Medium.Cos(Mxz - conto.Xz);
                    }
                }
                Skid = 0;
            }
            if (Capsized && f39 == 0.0F && f40 == 0.0F)
            {
                i32 = 0;
            }
            Mtouch = false;
            bool25 = true;
        }
        else if (Skid != 2)
        {
            Skid = 2;
        }
        var i45 = 0;
        var bools = new bool[4];
        var bools46 = new bool[4];
        var bools47 = new bool[4];
        var f48 = 0.0F;
        for (var i49 = 0; i49 < 4; i49++)
        {
            bools46[i49] = false;
            bools47[i49] = false;
            if (fs23[i49] > 245.0F)
            {
                i45++;
                Wtouch = true;
                Gtouch = true;
                if (!bool25 && Scy[i49] != 7.0F)
                {
                    var f50 = Scy[i49] / 333.33F;
                    if (f50 > 0.3)
                    {
                        f50 = 0.3F;
                    }
                    if (i32 == 0)
                    {
                        f50 += 1.1f;
                    }
                    else
                    {
                        f50 += 1.2f;
                    }
                    conto.Dust(i49, fs[i49], fs23[i49], fs22[i49], (int) Scx[i49], (int) Scz[i49], f50 * Stat.Simag,
                        0, Capsized && Mtouch);
                }
                fs23[i49] = 250.0F;
                bools47[i49] = true;
                f48 += fs23[i49] - 250.0F;
                var f51 = Math.Abs(Medium.Sin(Pxy)) + Math.Abs(Medium.Sin(Pzy));
                f51 /= 3.0F;
                if (f51 > 0.4)
                {
                    f51 = 0.4F;
                }
                f51 += Stat.Bounce;
                if (f51 < 1.1)
                {
                    f51 = 1.1F;
                }
                Regy(i49, Math.Abs(Scy[i49] * f51), conto);
                if (Scy[i49] > 0.0F)
                {
                    Scy[i49] -= Math.Abs(Scy[i49] * f51);
                }
                if (Capsized)
                {
                    bools46[i49] = true;
                }
            }
            bools[i49] = false;
        }
        if (i45 != 0)
        {
            f48 /= i45;
            for (var i52 = 0; i52 < 4; i52++)
            {
                if (!bools47[i52])
                {
                    fs23[i52] -= f48;
                }
            }
        }
        var i53 = 0;
        for (var i54 = 0; i54 < Trackers.Sect[i30, i31].Length; i54++)
        {
            var i55 = Trackers.Sect[i30, i31][i54];
            var i56 = 0;
            var i57 = 0;
            for (var i58 = 0; i58 < 4; i58++)
            {
                if (bools46[i58] && (Trackers.Skd[i55] == 0 || Trackers.Skd[i55] == 1) &&
                    fs[i58] > Trackers.X[i55] - Trackers.Radx[i55] &&
                    fs[i58] < Trackers.X[i55] + Trackers.Radx[i55] &&
                    fs22[i58] > Trackers.Z[i55] - Trackers.Radz[i55] &&
                    fs22[i58] < Trackers.Z[i55] + Trackers.Radz[i55])
                {
                    conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 1);
                    //if (Im == XTGraphics.Im)
                    //{
                        //XTGraphics.Gscrape(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                    //}
                }
                if (!bools[i58] && fs[i58] > Trackers.X[i55] - Trackers.Radx[i55] &&
                    fs[i58] < Trackers.X[i55] + Trackers.Radx[i55] &&
                    fs22[i58] > Trackers.Z[i55] - Trackers.Radz[i55] &&
                    fs22[i58] < Trackers.Z[i55] + Trackers.Radz[i55] &&
                    fs23[i58] > Trackers.Y[i55] - Trackers.Rady[i55] &&
                    fs23[i58] < Trackers.Y[i55] + Trackers.Rady[i55] &&
                    (!Trackers.Decor[i55] || Medium.Resdown != 2 /*|| XTGraphics.Multion != 0*/))
                {
                    if (Trackers.Xy[i55] == 0 && Trackers.Zy[i55] == 0 && Trackers.Y[i55] != 250 &&
                        fs23[i58] > Trackers.Y[i55] - 5)
                    {
                        i57++;
                        Wtouch = true;
                        Gtouch = true;
                        if (!bool25 && Scy[i58] != 7.0F)
                        {
                            var f59 = Scy[i58] / 333.33F;
                            if (f59 > 0.3)
                            {
                                f59 = 0.3F;
                            }
                            if (i32 == 0)
                            {
                                f59 += 1.1f;
                            }
                            else
                            {
                                f59 += 1.2f;
                            }
                            conto.Dust(i58, fs[i58], fs23[i58], fs22[i58], (int) Scx[i58], (int) Scz[i58],
                                f59 * Stat.Simag, 0, Capsized && Mtouch);
                        }
                        fs23[i58] = Trackers.Y[i55];
                        if (Capsized && (Trackers.Skd[i55] == 0 || Trackers.Skd[i55] == 1))
                        {
                            conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 1);
                            //if (Im == XTGraphics.Im)
                            //{
                            //    XTGraphics.Gscrape(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                            //}
                        }
                        var f60 = Math.Abs(Medium.Sin(Pxy)) + Math.Abs(Medium.Sin(Pzy));
                        f60 /= 3.0F;
                        if (f60 > 0.4)
                        {
                            f60 = 0.4F;
                        }
                        f60 += Stat.Bounce;
                        if (f60 < 1.1)
                        {
                            f60 = 1.1F;
                        }
                        Regy(i58, Math.Abs(Scy[i58] * f60), conto);
                        if (Scy[i58] > 0.0F)
                        {
                            Scy[i58] -= Math.Abs(Scy[i58] * f60);
                        }
                        bools[i58] = true;
                    }
                    if (Trackers.Zy[i55] == -90 && fs22[i58] < Trackers.Z[i55] + Trackers.Radz[i55] &&
                        (Scz[i58] < 0.0F || Trackers.Radz[i55] == 287))
                    {
                        for (var i61 = 0; i61 < 4; i61++)
                        {
                            if (i58 != i61 && fs22[i61] >= Trackers.Z[i55] + Trackers.Radz[i55])
                            {
                                fs22[i61] -= fs22[i58] - (Trackers.Z[i55] + Trackers.Radz[i55]);
                            }
                        }

                        fs22[i58] = Trackers.Z[i55] + Trackers.Radz[i55];
                        if (Trackers.Skd[i55] != 2)
                        {
                            _crank[0, i58]++;
                        }
                        if (Trackers.Skd[i55] == 5 && Medium.RandomBoolean())
                        {
                            _crank[0, i58]++;
                        }
                        if (_crank[0, i58] > 1)
                        {
                            conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 0);
                            //if (Im == XTGraphics.Im)
                            //{
                            //    XTPart2.Scrapef(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                            //}
                        }
                        var f62 = Math.Abs(Medium.Cos(Pxy)) + Math.Abs(Medium.Cos(Pzy));
                        f62 /= 4.0F;
                        if (f62 > 0.3)
                        {
                            f62 = 0.3F;
                        }
                        if (bool25)
                        {
                            f62 = 0.0F;
                        }
                        f62 += Stat.Bounce - 0.2f;
                        if (f62 < 1.1)
                        {
                            f62 = 1.1F;
                        }
                        Regz(i58, Math.Abs(Scz[i58] * f62 * Trackers.Dam[i55]), conto);
                        Scz[i58] += Math.Abs(Scz[i58] * f62);
                        Skid = 2;
                        bool6 = true;
                        bools[i58] = true;
                        if (!Trackers.Notwall[i55])
                        {
                            control.Wall = i55;
                        }
                    }
                    if (Trackers.Zy[i55] == 90 && fs22[i58] > Trackers.Z[i55] - Trackers.Radz[i55] &&
                        (Scz[i58] > 0.0F || Trackers.Radz[i55] == 287))
                    {
                        for (var i63 = 0; i63 < 4; i63++)
                        {
                            if (i58 != i63 && fs22[i63] <= Trackers.Z[i55] - Trackers.Radz[i55])
                            {
                                fs22[i63] -= fs22[i58] - (Trackers.Z[i55] - Trackers.Radz[i55]);
                            }
                        }

                        fs22[i58] = Trackers.Z[i55] - Trackers.Radz[i55];
                        if (Trackers.Skd[i55] != 2)
                        {
                            _crank[1, i58]++;
                        }
                        if (Trackers.Skd[i55] == 5 && Medium.RandomBoolean())
                        {
                            _crank[1, i58]++;
                        }
                        if (_crank[1, i58] > 1)
                        {
                            conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 0);
                            /*if (Im == XTGraphics.Im)
                            {
                                XTPart2.Scrapef(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                            }*/
                        }
                        var f64 = Math.Abs(Medium.Cos(Pxy)) + Math.Abs(Medium.Cos(Pzy));
                        f64 /= 4.0F;
                        if (f64 > 0.3)
                        {
                            f64 = 0.3F;
                        }
                        if (bool25)
                        {
                            f64 = 0.0F;
                        }
                        f64 += Stat.Bounce - 0.2f;
                        if (f64 < 1.1)
                        {
                            f64 = 1.1F;
                        }
                        Regz(i58, -Math.Abs(Scz[i58] * f64 * Trackers.Dam[i55]), conto);
                        Scz[i58] -= Math.Abs(Scz[i58] * f64);
                        Skid = 2;
                        bool6 = true;
                        bools[i58] = true;
                        if (!Trackers.Notwall[i55])
                        {
                            control.Wall = i55;
                        }
                    }
                    if (Trackers.Xy[i55] == -90 && fs[i58] < Trackers.X[i55] + Trackers.Radx[i55] &&
                        (Scx[i58] < 0.0F || Trackers.Radx[i55] == 287))
                    {
                        for (var i65 = 0; i65 < 4; i65++)
                        {
                            if (i58 != i65 && fs[i65] >= Trackers.X[i55] + Trackers.Radx[i55])
                            {
                                fs[i65] -= fs[i58] - (Trackers.X[i55] + Trackers.Radx[i55]);
                            }
                        }

                        fs[i58] = Trackers.X[i55] + Trackers.Radx[i55];
                        if (Trackers.Skd[i55] != 2)
                        {
                            _crank[2, i58]++;
                        }
                        if (Trackers.Skd[i55] == 5 && Medium.RandomBoolean())
                        {
                            _crank[2, i58]++;
                        }
                        if (_crank[2, i58] > 1)
                        {
                            conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 0);
                            /*if (Im == XTGraphics.Im)
                            {
                                XTPart2.Scrapef(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                            }*/
                        }
                        var f66 = Math.Abs(Medium.Cos(Pxy)) + Math.Abs(Medium.Cos(Pzy));
                        f66 /= 4.0F;
                        if (f66 > 0.3)
                        {
                            f66 = 0.3F;
                        }
                        if (bool25)
                        {
                            f66 = 0.0F;
                        }
                        f66 += Stat.Bounce - 0.2f;
                        if (f66 < 1.1)
                        {
                            f66 = 1.1F;
                        }
                        Regx(i58, Math.Abs(Scx[i58] * f66 * Trackers.Dam[i55]), conto);
                        Scx[i58] += Math.Abs(Scx[i58] * f66);
                        Skid = 2;
                        bool6 = true;
                        bools[i58] = true;
                        if (!Trackers.Notwall[i55])
                        {
                            control.Wall = i55;
                        }
                    }
                    if (Trackers.Xy[i55] == 90 && fs[i58] > Trackers.X[i55] - Trackers.Radx[i55] &&
                        (Scx[i58] > 0.0F || Trackers.Radx[i55] == 287))
                    {
                        for (var i67 = 0; i67 < 4; i67++)
                        {
                            if (i58 != i67 && fs[i67] <= Trackers.X[i55] - Trackers.Radx[i55])
                            {
                                fs[i67] -= fs[i58] - (Trackers.X[i55] - Trackers.Radx[i55]);
                            }
                        }

                        fs[i58] = Trackers.X[i55] - Trackers.Radx[i55];
                        if (Trackers.Skd[i55] != 2)
                        {
                            _crank[3, i58]++;
                        }
                        if (Trackers.Skd[i55] == 5 && Medium.RandomBoolean())
                        {
                            _crank[3, i58]++;
                        }
                        if (_crank[3, i58] > 1)
                        {
                            conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 0);
                            /*if (Im == XTGraphics.Im)
                            {
                                XTPart2.Scrapef(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                            }*/
                        }
                        var f68 = Math.Abs(Medium.Cos(Pxy)) + Math.Abs(Medium.Cos(Pzy));
                        f68 /= 4.0F;
                        if (f68 > 0.3)
                        {
                            f68 = 0.3F;
                        }
                        if (bool25)
                        {
                            f68 = 0.0F;
                        }
                        f68 += Stat.Bounce - 0.2f;
                        if (f68 < 1.1)
                        {
                            f68 = 1.1F;
                        }
                        Regx(i58, -Math.Abs(Scx[i58] * f68 * Trackers.Dam[i55]), conto);
                        Scx[i58] -= Math.Abs(Scx[i58] * f68);
                        Skid = 2;
                        bool6 = true;
                        bools[i58] = true;
                        if (!Trackers.Notwall[i55])
                        {
                            control.Wall = i55;
                        }
                    }
                    if (Trackers.Zy[i55] != 0 && Trackers.Zy[i55] != 90 && Trackers.Zy[i55] != -90)
                    {
                        var i69 = 90 + Trackers.Zy[i55];
                        var f70 = 1.0F + (50 - Math.Abs(Trackers.Zy[i55])) / 30.0F;
                        if (f70 < 1.0F)
                        {
                            f70 = 1.0F;
                        }
                        var f71 = Trackers.Y[i55] +
                                  ((fs23[i58] - Trackers.Y[i55]) * Medium.Cos(i69) -
                                   (fs22[i58] - Trackers.Z[i55]) * Medium.Sin(i69));
                        var f72 = Trackers.Z[i55] +
                                  ((fs23[i58] - Trackers.Y[i55]) * Medium.Sin(i69) +
                                   (fs22[i58] - Trackers.Z[i55]) * Medium.Cos(i69));
                        if (f72 > Trackers.Z[i55] && f72 < Trackers.Z[i55] + 200)
                        {
                            Scy[i58] -= (f72 - Trackers.Z[i55]) / f70;
                            f72 = Trackers.Z[i55];
                        }
                        if (f72 > Trackers.Z[i55] - 30)
                        {
                            if (Trackers.Skd[i55] == 2)
                            {
                                i56++;
                            }
                            else
                            {
                                i53++;
                            }
                            Wtouch = true;
                            Gtouch = false;
                            if (Capsized && (Trackers.Skd[i55] == 0 || Trackers.Skd[i55] == 1))
                            {
                                conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 1);
                                /*if (Im == XTGraphics.Im)
                                {
                                    XTGraphics.Gscrape(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                                }*/
                            }
                            if (!bool25 && i32 != 0)
                            {
                                var f73 = 1.4F;
                                conto.Dust(i58, fs[i58], fs23[i58], fs22[i58], (int) Scx[i58], (int) Scz[i58],
                                    f73 * Stat.Simag, 0, Capsized && Mtouch);
                            }
                        }
                        fs23[i58] = Trackers.Y[i55] +
                                    ((f71 - Trackers.Y[i55]) * Medium.Cos(-i69) -
                                     (f72 - Trackers.Z[i55]) * Medium.Sin(-i69));
                        fs22[i58] = Trackers.Z[i55] +
                                    ((f71 - Trackers.Y[i55]) * Medium.Sin(-i69) +
                                     (f72 - Trackers.Z[i55]) * Medium.Cos(-i69));
                        bools[i58] = true;
                    }
                    if (Trackers.Xy[i55] != 0 && Trackers.Xy[i55] != 90 && Trackers.Xy[i55] != -90)
                    {
                        var i74 = 90 + Trackers.Xy[i55];
                        var f75 = 1.0F + (50 - Math.Abs(Trackers.Xy[i55])) / 30.0F;
                        if (f75 < 1.0F)
                        {
                            f75 = 1.0F;
                        }
                        var f76 = Trackers.Y[i55] +
                                  ((fs23[i58] - Trackers.Y[i55]) * Medium.Cos(i74) -
                                   (fs[i58] - Trackers.X[i55]) * Medium.Sin(i74));
                        var f77 = Trackers.X[i55] +
                                  ((fs23[i58] - Trackers.Y[i55]) * Medium.Sin(i74) +
                                   (fs[i58] - Trackers.X[i55]) * Medium.Cos(i74));
                        if (f77 > Trackers.X[i55] && f77 < Trackers.X[i55] + 200)
                        {
                            Scy[i58] -= (f77 - Trackers.X[i55]) / f75;
                            f77 = Trackers.X[i55];
                        }
                        if (f77 > Trackers.X[i55] - 30)
                        {
                            if (Trackers.Skd[i55] == 2)
                            {
                                i56++;
                            }
                            else
                            {
                                i53++;
                            }
                            Wtouch = true;
                            Gtouch = false;
                            if (Capsized && (Trackers.Skd[i55] == 0 || Trackers.Skd[i55] == 1))
                            {
                                conto.Spark(fs[i58], fs23[i58], fs22[i58], Scx[i58], Scy[i58], Scz[i58], 1);
                                /*if (Im == XTGraphics.Im)
                                {
                                    XTGraphics.Gscrape(Im, (int) Scx[i58], (int) Scy[i58], (int) Scz[i58]);
                                }*/
                            }
                            if (!bool25 && i32 != 0)
                            {
                                var f78 = 1.4F;
                                conto.Dust(i58, fs[i58], fs23[i58], fs22[i58], (int) Scx[i58], (int) Scz[i58],
                                    f78 * Stat.Simag, 0, Capsized && Mtouch);
                            }
                        }
                        fs23[i58] = Trackers.Y[i55] +
                                    ((f76 - Trackers.Y[i55]) * Medium.Cos(-i74) -
                                     (f77 - Trackers.X[i55]) * Medium.Sin(-i74));
                        fs[i58] = Trackers.X[i55] +
                                  ((f76 - Trackers.Y[i55]) * Medium.Sin(-i74) +
                                   (f77 - Trackers.X[i55]) * Medium.Cos(-i74));
                        bools[i58] = true;
                    }
                }
            }
            if (i56 == 4)
            {
                Mtouch = true;
            }
            if (i57 == 4)
            {
                i45 = 4;
            }
        }
        if (i53 == 4)
        {
            Mtouch = true;
        }
        for (var i79 = 0; i79 < 4; i79++)
        {
            for (var i80 = 0; i80 < 4; i80++)
            {
                if (_crank[i79, i80] == _lcrank[i79, i80])
                {
                    _crank[i79, i80] = 0;
                }
                _lcrank[i79, i80] = _crank[i79, i80];
            }
        }
        var i81 = 0;
        var i82 = 0;
        var i83 = 0;
        var i84 = 0;
        if (Scy[2] != Scy[0])
        {
            if (Scy[2] < Scy[0])
            {
                i = -1;
            }
            else
            {
                i = 1;
            }
            d = Math.Sqrt((fs22[0] - fs22[2]) * (fs22[0] - fs22[2]) + (fs23[0] - fs23[2]) * (fs23[0] - fs23[2]) +
                          (fs[0] - fs[2]) * (fs[0] - fs[2])) / (Math.Abs(conto.Keyz[0]) + Math.Abs(conto.Keyz[2]));
            if (d >= 0.9998)
            {
                i81 = i;
            }
            else
            {
                i81 = (int) (Math.Acos(d) / 0.017453292519943295 * i);
            }
        }
        if (Scy[3] != Scy[1])
        {
            if (Scy[3] < Scy[1])
            {
                i = -1;
            }
            else
            {
                i = 1;
            }
            d = Math.Sqrt((fs22[1] - fs22[3]) * (fs22[1] - fs22[3]) + (fs23[1] - fs23[3]) * (fs23[1] - fs23[3]) +
                          (fs[1] - fs[3]) * (fs[1] - fs[3])) / (Math.Abs(conto.Keyz[1]) + Math.Abs(conto.Keyz[3]));
            if (d >= 0.9998)
            {
                i82 = i;
            }
            else
            {
                i82 = (int) (Math.Acos(d) / 0.017453292519943295 * i);
            }
        }
        if (Scy[1] != Scy[0])
        {
            if (Scy[1] < Scy[0])
            {
                i = -1;
            }
            else
            {
                i = 1;
            }
            d = Math.Sqrt((fs22[0] - fs22[1]) * (fs22[0] - fs22[1]) + (fs23[0] - fs23[1]) * (fs23[0] - fs23[1]) +
                          (fs[0] - fs[1]) * (fs[0] - fs[1])) / (Math.Abs(conto.Keyx[0]) + Math.Abs(conto.Keyx[1]));
            if (d >= 0.9998)
            {
                i83 = i;
            }
            else
            {
                i83 = (int) (Math.Acos(d) / 0.017453292519943295 * i);
            }
        }
        if (Scy[3] != Scy[2])
        {
            if (Scy[3] < Scy[2])
            {
                i = -1;
            }
            else
            {
                i = 1;
            }
            d = Math.Sqrt((fs22[2] - fs22[3]) * (fs22[2] - fs22[3]) + (fs23[2] - fs23[3]) * (fs23[2] - fs23[3]) +
                          (fs[2] - fs[3]) * (fs[2] - fs[3])) / (Math.Abs(conto.Keyx[2]) + Math.Abs(conto.Keyx[3]));
            if (d >= 0.9998)
            {
                i84 = i;
            }
            else
            {
                i84 = (int) (Math.Acos(d) / 0.017453292519943295 * i);
            }
        }
        if (bool6)
        {
            int i85;
            for (i85 = Math.Abs(conto.Xz + 45); i85 > 180; i85 -= 360)
            {
            }
            if (Math.Abs(i85) > 90)
            {
                _pmlt = 1;
            }
            else
            {
                _pmlt = -1;
            }
            for (i85 = Math.Abs(conto.Xz - 45); i85 > 180; i85 -= 360)
            {
            }
            if (Math.Abs(i85) > 90)
            {
                _nmlt = 1;
            }
            else
            {
                _nmlt = -1;
            }
        }
        conto.Xz += (int) (_forca * (Scz[0] * _nmlt - Scz[1] * _pmlt + Scz[2] * _pmlt - Scz[3] * _nmlt +
            Scx[0] * _pmlt +
            Scx[1] * _nmlt - Scx[2] * _nmlt - Scx[3] * _pmlt));
        if (Math.Abs(i82) > Math.Abs(i81))
        {
            i81 = i82;
        }
        if (Math.Abs(i84) > Math.Abs(i83))
        {
            i83 = i84;
        }
        if (!abool)
        {
            Pzy += i81;
        }
        else
        {
            Pzy -= i81;
        }
        if (!bool8)
        {
            Pxy += i83;
        }
        else
        {
            Pxy -= i83;
        }
        if (i45 == 4)
        {
//# of touching wheels
            var i86 = 0;
            while (Pzy < 360)
            {
                Pzy += 360;
                conto.Zy += 360;
            }
            while (Pzy > 360)
            {
                Pzy -= 360;
                conto.Zy -= 360;
            }
            if (Pzy < 190 && Pzy > 170)
            {
//player zy angle
                // rounds off the angle, capsizing the player
                Pzy = 180;
                conto.Zy = 180;
                i86++;
            }
            if (Pzy > 350 || Pzy < 10)
            {
                // this ais the opposite, rounds off the angle but lands properly
                Pzy = 0;
                conto.Zy = 0;
                i86++;
            }
            while (Pxy < 360)
            {
                Pxy += 360;
                conto.Xy += 360;
            }
            while (Pxy > 360)
            {
                Pxy -= 360;
                conto.Xy -= 360;
            }
            if (Pxy < 190 && Pxy > 170)
            {
//same as above but for xy
                Pxy = 180;
                conto.Xy = 180;
                i86++;
            }
            if (Pxy > 350 || Pxy < 10)
            {
                Pxy = 0;
                conto.Xy = 0;
                i86++;
            }
            if (i86 == 2)
            {
                Mtouch = true;
            }
        }
        if (!Mtouch && Wtouch)
        {
            if (_cntouch == 10)
            {
                Mtouch = true;
            }
            else
            {
                _cntouch++;
            }
        }
        else
        {
            _cntouch = 0;
        }
        conto.Y = (int) ((fs23[0] + fs23[1] + fs23[2] + fs23[3]) / 4.0F - i10 * Medium.Cos(Pzy) * Medium.Cos(Pxy) +
                         f12);
        if (abool)
        {
            i = -1;
        }
        else
        {
            i = 1;
        }
        conto.X = (int) ((fs[0] - conto.Keyx[0] * Medium.Cos(conto.Xz) + i * conto.Keyz[0] * Medium.Sin(conto.Xz) +
                fs[1] - conto.Keyx[1] * Medium.Cos(conto.Xz) + i * conto.Keyz[1] * Medium.Sin(conto.Xz) +
                fs[2] - conto.Keyx[2] * Medium.Cos(conto.Xz) + i * conto.Keyz[2] * Medium.Sin(conto.Xz) +
                fs[3] - conto.Keyx[3] * Medium.Cos(conto.Xz) + i * conto.Keyz[3] * Medium.Sin(conto.Xz)) /
            4.0F + i10 * Medium.Sin(Pxy) * Medium.Cos(conto.Xz) -
            i10 * Medium.Sin(Pzy) * Medium.Sin(conto.Xz) + f);
        conto.Z = (int) ((fs22[0] - i * conto.Keyz[0] * Medium.Cos(conto.Xz) -
                          conto.Keyx[0] * Medium.Sin(conto.Xz) + fs22[1] -
                          i * conto.Keyz[1] * Medium.Cos(conto.Xz) - conto.Keyx[1] * Medium.Sin(conto.Xz) +
                          fs22[2] - i * conto.Keyz[2] * Medium.Cos(conto.Xz) -
                          conto.Keyx[2] * Medium.Sin(conto.Xz) + fs22[3] -
                          i * conto.Keyz[3] * Medium.Cos(conto.Xz) - conto.Keyx[3] * Medium.Sin(conto.Xz)) / 4.0F +
            i10 * Medium.Sin(Pxy) * Medium.Sin(conto.Xz) -
            i10 * Medium.Sin(Pzy) * Medium.Cos(conto.Xz) + f11);
        if (Math.Abs(Speed) > 10.0F || !Mtouch)
        {
            if (Math.Abs(Pxy - conto.Xy) >= 4)
            {
                if (Pxy > conto.Xy)
                {
                    conto.Xy += 2 + (Pxy - conto.Xy) / 2;
                }
                else
                {
                    conto.Xy -= 2 + (conto.Xy - Pxy) / 2;
                }
            }
            else
            {
                conto.Xy = Pxy;
            }
            if (Math.Abs(Pzy - conto.Zy) >= 4)
            {
                if (Pzy > conto.Zy)
                {
                    conto.Zy += 2 + (Pzy - conto.Zy) / 2;
                }
                else
                {
                    conto.Zy -= 2 + (conto.Zy - Pzy) / 2;
                }
            }
            else
            {
                conto.Zy = Pzy;
            }
        }
        if (Wtouch && !Capsized)
        {
            var f87 = (float) (Speed / Stat.Swits[2] * 14.0F * (Stat.Bounce - 0.4));
            if (control.Left && _tilt < f87 && _tilt >= 0.0F)
            {
                _tilt += 0.4f;
            }
            else if (control.Right && _tilt > -f87 && _tilt <= 0.0F)
            {
                _tilt -= 0.4f;
            }
            else if (Math.Abs(_tilt) > 3.0 * (Stat.Bounce - 0.4))
            {
                if (_tilt > 0.0F)
                {
                    _tilt -= 3.0f * (Stat.Bounce - 0.3f);
                }
                else
                {
                    _tilt += 3.0f * (Stat.Bounce - 0.3f);
                }
            }
            else
            {
                _tilt = 0.0F;
            }
            conto.Xy += (int) _tilt;
            if (Gtouch)
            {
                conto.Y -= (int) (_tilt / 1.5f);
            }
        }
        else if (_tilt != 0.0F)
        {
            _tilt = 0.0F;
        }
        if (Wtouch && i32 == 2)
        {
            conto.Zy += (int) ((Medium.Random() * 6.0F * Speed / Stat.Swits[2] - 3.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
            conto.Xy += (int) ((Medium.Random() * 6.0F * Speed / Stat.Swits[2] - 3.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
        }
        if (Wtouch && i32 == 1)
        {
            conto.Zy += (int) ((Medium.Random() * 4.0F * Speed / Stat.Swits[2] - 2.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
            conto.Xy += (int) ((Medium.Random() * 4.0F * Speed / Stat.Swits[2] - 2.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
        }
        if (Hitmag >= Stat.Maxmag && !Dest)
        {
            Distruct(conto);
            if (Cntdest == 7)
            {
                Dest = true;
            }
            else
            {
                Cntdest++;
            }
            if (Cntdest == 1)
            {
                //Record.Dest[Im] = 300;
            }
        }
        if (conto.Dist == 0)
        {
            for (var i88 = 0; i88 < conto.Npl; i88++)
            {
                if (conto.P[i88].Chip != 0)
                {
                    conto.P[i88].Chip = 0;
                }
                if (conto.P[i88].Embos != 0)
                {
                    conto.P[i88].Embos = 13;
                }
            }
        }
        var i89 = 0;
        var i90 = 0;
        var i91 = 0;
        if (Nofocus)
        {
            i4 = 1;
        }
        else
        {
            i4 = 7;
        }
        /*for (var i92 = 0; i92 < CheckPoints.N; i92++)
        {
            if (CheckPoints.Typ[i92] > 0)
            {
                i91++;
                if (CheckPoints.Typ[i92] == 1)
                {
                    if (Clear == i91 + Nlaps * CheckPoints.Nsp)
                    {
                        i4 = 1;
                    }
                    if (Math.Abs(conto.Z - CheckPoints.Z[i92]) <
                        60.0F + Math.Abs(Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4.0F &&
                        Math.Abs(conto.X - CheckPoints.X[i92]) < 700 &&
                        Math.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
                        Clear == i91 + Nlaps * CheckPoints.Nsp - 1)
                    {
                        Clear = i91 + Nlaps * CheckPoints.Nsp;
                        Pcleared = i92;
                        _focus = -1;
                    }
                }
                if (CheckPoints.Typ[i92] == 2)
                {
                    if (Clear == i91 + Nlaps * CheckPoints.Nsp)
                    {
                        i4 = 1;
                    }
                    if (Math.Abs(conto.X - CheckPoints.X[i92]) <
                        60.0F + Math.Abs(Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4.0F &&
                        Math.Abs(conto.Z - CheckPoints.Z[i92]) < 700 &&
                        Math.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
                        Clear == i91 + Nlaps * CheckPoints.Nsp - 1)
                    {
                        Clear = i91 + Nlaps * CheckPoints.Nsp;
                        Pcleared = i92;
                        _focus = -1;
                    }
                }
            }
            if (Py(conto.X / 100, CheckPoints.X[i92] / 100, conto.Z / 100, CheckPoints.Z[i92] / 100) * i4 < i90 ||
                i90 == 0)
            {
                i89 = i92;
                i90 = Py(conto.X / 100, CheckPoints.X[i92] / 100, conto.Z / 100, CheckPoints.Z[i92] / 100) * i4;
            }
        }
        if (Clear == i91 + Nlaps * CheckPoints.Nsp)
        {
            Nlaps++;
            if (XTGraphics.Multion == 1 && Im == XTGraphics.Im)
            {
                if (XTGraphics.Laptime < XTGraphics.Fastestlap || XTGraphics.Fastestlap == 0)
                {
                    XTGraphics.Fastestlap = XTGraphics.Laptime;
                }
                XTGraphics.Laptime = 0;
            }
        }
        if (Im == XTGraphics.Im)
        {
            if (XTGraphics.Multion == 1 && XTGraphics.Starcnt == 0)
            {
                XTGraphics.Laptime++;
            }
            for (Medium.Checkpoint = Clear;
                 Medium.Checkpoint >= CheckPoints.Nsp;
                 Medium.Checkpoint -= CheckPoints.Nsp)
            {
            }
            if (Clear == CheckPoints.Nlaps * CheckPoints.Nsp - 1)
            {
                Medium.Lastcheck = true;
            }
            if (CheckPoints.Haltall)
            {
                Medium.Lastcheck = false;
            }
        }
        if (_focus == -1)
        {
            if (Im == XTGraphics.Im)
            {
                i89 += 2;
            }
            else
            {
                i89++;
            }
            if (!Nofocus)
            {
                i91 = Pcleared + 1;
                if (i91 >= CheckPoints.N)
                {
                    i91 = 0;
                }
                while (CheckPoints.Typ[i91] <= 0)
                {
                    if (++i91 >= CheckPoints.N)
                    {
                        i91 = 0;
                    }
                }

                if (i89 > i91 && (Clear != Nlaps * CheckPoints.Nsp || i89 < Pcleared))
                {
                    i89 = i91;
                    _focus = i89;
                }
            }
            if (i89 >= CheckPoints.N)
            {
                i89 -= CheckPoints.N;
            }
            if (CheckPoints.Typ[i89] == -3)
            {
                i89 = 0;
            }
            if (Im == XTGraphics.Im)
            {
                if (Missedcp != -1)
                {
                    Missedcp = -1;
                }
            }
            else if (Missedcp != 0)
            {
                Missedcp = 0;
            }
        }
        else
        {
            i89 = _focus;
            if (Im == XTGraphics.Im)
            {
                if (Missedcp == 0 && Mtouch && Math.Sqrt(Py(conto.X / 10, CheckPoints.X[_focus] / 10, conto.Z / 10,
                        CheckPoints.Z[_focus] / 10)) > 800.0)
                {
                    Missedcp = 1;
                }
                if (Missedcp == -2 && Math.Sqrt(Py(conto.X / 10, CheckPoints.X[_focus] / 10, conto.Z / 10,
                        CheckPoints.Z[_focus] / 10)) < 400.0)
                {
                    Missedcp = 0;
                }
                if (Missedcp != 0 && Mtouch && Math.Sqrt(Py(conto.X / 10, CheckPoints.X[_focus] / 10, conto.Z / 10,
                        CheckPoints.Z[_focus] / 10)) < 250.0)
                {
                    Missedcp = 68;
                }
            }
            else
            {
                Missedcp = 1;
            }
            if (Nofocus)
            {
                _focus = -1;
                Missedcp = 0;
            }
        }
        if (Nofocus)
        {
            Nofocus = false;
        }
        Point = i89;
        if (_fixes != 0)
        {
            if (Medium.Noelec == 0)
            {
                for (var i93 = 0; i93 < CheckPoints.Fn; i93++)
                {
                    if (!CheckPoints.Roted[i93])
                    {
                        if (Math.Abs(conto.Z - CheckPoints.Fz[i93]) < 200 && Py(conto.X / 100,
                                CheckPoints.Fx[i93] / 100, conto.Y / 100, CheckPoints.Fy[i93] / 100) < 30)
                        {
                            if (conto.Dist == 0)
                            {
                                conto.Fcnt = 8;
                            }
                            else
                            {
                                if (Im == XTGraphics.Im && !conto.Fix && !XTGraphics.Mutes)
                                {
                                    XTGraphics.Carfixed.Play();
                                }
                                conto.Fix = true;
                            }
                            Record.Fix[Im] = 300;
                        }
                    }
                    else if (Math.Abs(conto.X - CheckPoints.Fx[i93]) < 200 && Py(conto.Z / 100,
                                 CheckPoints.Fz[i93] / 100, conto.Y / 100, CheckPoints.Fy[i93] / 100) < 30)
                    {
                        if (conto.Dist == 0)
                        {
                            conto.Fcnt = 8;
                        }
                        else
                        {
                            if (Im == XTGraphics.Im && !conto.Fix && !XTGraphics.Mutes)
                            {
                                XTGraphics.Carfixed.Play();
                            }
                            conto.Fix = true;
                        }
                        Record.Fix[Im] = 300;
                    }
                }
            }
        }
        else
        {
            for (var i94 = 0; i94 < CheckPoints.Fn; i94++)
            {
                if (Rpy(conto.X / 100, CheckPoints.Fx[i94] / 100, conto.Y / 100, CheckPoints.Fy[i94] / 100,
                        conto.Z / 100, CheckPoints.Fz[i94] / 100) < 760)
                {
                    Medium.Noelec = 2;
                }
            }
        }*/
        if (conto.Fcnt == 7 || conto.Fcnt == 8)
        {
            Squash = 0;
            _nbsq = 0;
            Hitmag = 0;
            Cntdest = 0;
            Dest = false;
            Newcar = true;
            conto.Fcnt = 9;
            if (_fixes > 0)
            {
                _fixes--;
            }
        }
        if (Newedcar != 0)
        {
            Newedcar--;
            if (Newedcar == 10)
            {
                Newcar = false;
            }
        }
        if (!Mtouch)
        {
            if (Trcnt != 1)
            {
                Trcnt = 1;
                _lxz = conto.Xz;
            }
            if (Loop == 2 || Loop == -1)
            {
                Travxy += (int) (Rcomp - Lcomp);
                if (Math.Abs(Travxy) > 135)
                {
                    Rtab = true;
                }
                Travzy += (int) (Ucomp - Dcomp);
                if (Travzy > 135)
                {
                    Ftab = true;
                }
                if (Travzy < -135)
                {
                    Btab = true;
                }
            }
            if (_lxz != conto.Xz)
            {
                Travxz += _lxz - conto.Xz;
                _lxz = conto.Xz;
            }
            if (_srfcnt < 10)
            {
                if (control.Wall != -1)
                {
                    Surfer = true;
                }
                _srfcnt++;
            }
        }
        else if (!Dest)
        {
            if (!Capsized)
            {
                if (Capcnt != 0)
                {
                    Capcnt = 0;
                }
                if (Gtouch && Trcnt != 0)
                {
                    if (Trcnt == 9)
                    {
                        Powerup = 0.0F;
                        if (Math.Abs(Travxy) > 90)
                        {
                            Powerup += Math.Abs(Travxy) / 24.0F;
                        }
                        else if (Rtab)
                        {
                            Powerup += 30.0F;
                        }
                        if (Math.Abs(Travzy) > 90)
                        {
                            Powerup += Math.Abs(Travzy) / 18.0F;
                        }
                        else
                        {
                            if (Ftab)
                            {
                                Powerup += 40.0F;
                            }
                            if (Btab)
                            {
                                Powerup += 40.0F;
                            }
                        }
                        if (Math.Abs(Travxz) > 90)
                        {
                            Powerup += Math.Abs(Travxz) / 18.0F;
                        }
                        if (Surfer)
                        {
                            Powerup += 30.0F;
                        }
                        Power += Powerup;
                        /*if (Im == XTGraphics.Im && (int) Powerup > Record.Powered && Record.Wasted == 0 &&
                            (Powerup > 60.0F || CheckPoints.Stage == 1 || CheckPoints.Stage == 2))
                        {
                            _rpdcatch = 30;
                            if (Record.Hcaught)
                            {
                                Record.Powered = (int) Powerup;
                            }
                            if (XTGraphics.Multion == 1 && Powerup > XTGraphics.Beststunt)
                            {
                                XTGraphics.Beststunt = (int) Powerup;
                            }
                        }*/
                        if (Power > 98.0F)
                        {
                            Power = 98.0F;
                            if (Powerup > 150.0F)
                            {
                                _xtpower = 200;
                            }
                            else
                            {
                                _xtpower = 100;
                            }
                        }
                    }
                    if (Trcnt == 10)
                    {
                        Travxy = 0;
                        Travzy = 0;
                        Travxz = 0;
                        Ftab = false;
                        Rtab = false;
                        Btab = false;
                        Trcnt = 0;
                        _srfcnt = 0;
                        Surfer = false;
                    }
                    else
                    {
                        Trcnt++;
                    }
                }
            }
            else
            {
                if (Trcnt != 0)
                {
                    Travxy = 0;
                    Travzy = 0;
                    Travxz = 0;
                    Ftab = false;
                    Rtab = false;
                    Btab = false;
                    Trcnt = 0;
                    _srfcnt = 0;
                    Surfer = false;
                }
                if (Capcnt == 0)
                {
                    var i95 = 0;
                    for (var i96 = 0; i96 < 4; i96++)
                    {
                        if (Math.Abs(Scz[i96]) < 70.0F && Math.Abs(Scx[i96]) < 70.0F)
                        {
                            i95++;
                        }
                    }

                    if (i95 == 4)
                    {
                        Capcnt = 1;
                    }
                }
                else
                {
                    Capcnt++;
                    if (Capcnt == 30)
                    {
                        Speed = 0.0F;
                        conto.Y += Stat.Flipy;
                        Pxy += 180;
                        conto.Xy += 180;
                        Capcnt = 0;
                    }
                }
            }
            if (Trcnt == 0 && Speed != 0.0F)
            {
                if (_xtpower == 0)
                {
                    if (Power > 0.0F)
                    {
                        Power -= Power * Power * Power / Stat.Powerloss;
                    }
                    else
                    {
                        Power = 0.0F;
                    }
                }
                else
                {
                    _xtpower--;
                }
            }
        }
        if (/*Im == XTGraphics.Im*/Im == 0)
        {
            if (control.Wall != -1)
            {
                control.Wall = -1;
            }
        }
        else if (Lastcolido != 0 && !Dest)
        {
            Lastcolido--;
        }
        /*if (Dest)
        {
            if (CheckPoints.Dested[Im] == 0)
            {
                if (Lastcolido == 0)
                {
                    CheckPoints.Dested[Im] = 1;
                }
                else
                {
                    CheckPoints.Dested[Im] = 2;
                }
            }
        }
        else if (CheckPoints.Dested[Im] != 0 && CheckPoints.Dested[Im] != 3)
        {
            CheckPoints.Dested[Im] = 0;
        }
        if (Im == XTGraphics.Im && Record.Wasted == 0 && _rpdcatch != 0)
        {
            _rpdcatch--;
            if (_rpdcatch == 0)
            {
                Record.Cotchinow(Im);
                if (Record.Hcaught)
                {
                    Record.Whenwasted = (int) (185.0F + Medium.Random() * 20.0F);
                }
            }
        }*/
    }

    private int Py(int i, int i145, int i146, int i147)
    {
        return (i - i145) * (i - i145) + (i146 - i147) * (i146 - i147);
    }

    private int Regx(int i, float f, ContO conto)
    {
        var i110 = 0;
        var abool = true;
        /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
        {
            abool = false;
        }
        if (XTGraphics.Multion >= 2)
        {
            abool = false;
        }
        if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
        {
            abool = true;
        }*/
        f *= Stat.Dammult;
        if (Math.Abs(f) > 100.0F)
        {
            //Record.Recx(i, f, Im);
            if (f > 100.0F)
            {
                f -= 100.0F;
            }
            if (f < -100.0F)
            {
                f += 100.0F;
            }
            Shakedam = (int) ((Math.Abs(f) + Shakedam) / 2.0F);
            /*if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, 0);
            }*/
            for (var i111 = 0; i111 < conto.Npl; i111++)
            {
                var f112 = 0.0F;
                for (var i113 = 0; i113 < conto.P[i111].N; i113++)
                {
                    if (conto.P[i111].Wz == 0 && Py(conto.Keyx[i], conto.P[i111].Ox[i113], conto.Keyz[i],
                            conto.P[i111].Oz[i113]) < Stat.Clrad)
                    {
                        f112 = f / 20.0F * Medium.Random();
                        conto.P[i111].Oz[i113] -= (int) (f112 * Medium.Sin(conto.Xz) * Medium.Cos(conto.Zy));
                        conto.P[i111].Ox[i113] += (int) (f112 * Medium.Cos(conto.Xz) * Medium.Cos(conto.Xy));
                        if (abool)
                        {
                            Hitmag += (int) Math.Abs(f112);
                            i110 += (int) Math.Abs(f112);
                        }
                    }
                }

                if (f112 != 0.0F)
                {
                    if (Math.Abs(f112) >= 1.0F)
                    {
                        conto.P[i111].Chip = 1;
                        conto.P[i111].Ctmag = f112;
                    }
                    if (!conto.P[i111].Nocol && conto.P[i111].Glass != 1)
                    {
                        if (conto.P[i111].Bfase > 20 && conto.P[i111].HSB[1] > 0.25)
                        {
                            conto.P[i111].HSB[1] = 0.25F;
                        }
                        if (conto.P[i111].Bfase > 25 && conto.P[i111].HSB[2] > 0.7)
                        {
                            conto.P[i111].HSB[2] = 0.7F;
                        }
                        if (conto.P[i111].Bfase > 30 && conto.P[i111].HSB[1] > 0.15)
                        {
                            conto.P[i111].HSB[1] = 0.15F;
                        }
                        if (conto.P[i111].Bfase > 35 && conto.P[i111].HSB[2] > 0.6)
                        {
                            conto.P[i111].HSB[2] = 0.6F;
                        }
                        if (conto.P[i111].Bfase > 40)
                        {
                            conto.P[i111].HSB[0] = 0.075F;
                        }
                        if (conto.P[i111].Bfase > 50 && conto.P[i111].HSB[2] > 0.5)
                        {
                            conto.P[i111].HSB[2] = 0.5F;
                        }
                        if (conto.P[i111].Bfase > 60)
                        {
                            conto.P[i111].HSB[0] = 0.05F;
                        }
                        conto.P[i111].Bfase += (int) Math.Abs(f112);
                        new Color(conto.P[i111].C[0], conto.P[i111].C[1], conto.P[i111].C[2]);
                        var color = Color.GetHSBColor(conto.P[i111].HSB[0], conto.P[i111].HSB[1],
                            conto.P[i111].HSB[2]);
                        conto.P[i111].C[0] = color.R;
                        conto.P[i111].C[1] = color.G;
                        conto.P[i111].C[2] = color.B;
                    }
                    if (conto.P[i111].Glass == 1)
                    {
                        conto.P[i111].Gr += (int) Math.Abs(f112 * 1.5);
                    }
                }
            }
        }
        return i110;
    }

    private int Regy(int i, float f, ContO conto)
    {
        var i97 = 0;
        var abool = true;
        /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
        {
            abool = false;
        }
        if (XTGraphics.Multion >= 2)
        {
            abool = false;
        }
        if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
        {
            abool = true;
        }*/
        f *= Stat.Dammult;
        if (f > 100.0F)
        {
            //Record.Recy(i, f, Mtouch, Im);
            f -= 100.0F;
            var i98 = 0;
            var i99 = 0;
            var i100 = conto.Zy;
            var i101 = conto.Xy;
            for ( /**/; i100 < 360; i100 += 360)
            {
            }
            for ( /**/; i100 > 360; i100 -= 360)
            {
            }
            if (i100 < 210 && i100 > 150)
            {
                i98 = -1;
            }
            if (i100 > 330 || i100 < 30)
            {
                i98 = 1;
            }
            for ( /**/; i101 < 360; i101 += 360)
            {
            }
            for ( /**/; i101 > 360; i101 -= 360)
            {
            }
            if (i101 < 210 && i101 > 150)
            {
                i99 = -1;
            }
            if (i101 > 330 || i101 < 30)
            {
                i99 = 1;
            }
            if (i99 * i98 == 0)
            {
                Shakedam = (int) ((Math.Abs(f) + Shakedam) / 2.0F);
            }
            /*
            if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, i99 * i98);
            }*/
            if (i99 * i98 == 0 || Mtouch)
            {
                for (var i102 = 0; i102 < conto.Npl; i102++)
                {
                    var f103 = 0.0F;
                    for (var i104 = 0; i104 < conto.P[i102].N; i104++)
                    {
                        if (conto.P[i102].Wz == 0 && Py(conto.Keyx[i], conto.P[i102].Ox[i104], conto.Keyz[i],
                                conto.P[i102].Oz[i104]) < Stat.Clrad)
                        {
                            f103 = f / 20.0F * Medium.Random();
                            conto.P[i102].Oz[i104] += (int) (f103 * Medium.Sin(i100));
                            conto.P[i102].Ox[i104] -= (int) (f103 * Medium.Sin(i101));
                            if (abool)
                            {
                                Hitmag += (int) Math.Abs(f103);
                                i97 += (int) Math.Abs(f103);
                            }
                        }
                    }

                    if (f103 != 0.0F)
                    {
                        if (Math.Abs(f103) >= 1.0F)
                        {
                            conto.P[i102].Chip = 1;
                            conto.P[i102].Ctmag = f103;
                        }
                        if (!conto.P[i102].Nocol && conto.P[i102].Glass != 1)
                        {
                            if (conto.P[i102].Bfase > 20 && conto.P[i102].HSB[1] > 0.25)
                            {
                                conto.P[i102].HSB[1] = 0.25F;
                            }
                            if (conto.P[i102].Bfase > 25 && conto.P[i102].HSB[2] > 0.7)
                            {
                                conto.P[i102].HSB[2] = 0.7F;
                            }
                            if (conto.P[i102].Bfase > 30 && conto.P[i102].HSB[1] > 0.15)
                            {
                                conto.P[i102].HSB[1] = 0.15F;
                            }
                            if (conto.P[i102].Bfase > 35 && conto.P[i102].HSB[2] > 0.6)
                            {
                                conto.P[i102].HSB[2] = 0.6F;
                            }
                            if (conto.P[i102].Bfase > 40)
                            {
                                conto.P[i102].HSB[0] = 0.075F;
                            }
                            if (conto.P[i102].Bfase > 50 && conto.P[i102].HSB[2] > 0.5)
                            {
                                conto.P[i102].HSB[2] = 0.5F;
                            }
                            if (conto.P[i102].Bfase > 60)
                            {
                                conto.P[i102].HSB[0] = 0.05F;
                            }
                            conto.P[i102].Bfase += (int) f103;
                            new Color(conto.P[i102].C[0], conto.P[i102].C[1], conto.P[i102].C[2]);
                            var color = Color.GetHSBColor(conto.P[i102].HSB[0], conto.P[i102].HSB[1],
                                conto.P[i102].HSB[2]);
                            conto.P[i102].C[0] = color.R;
                            conto.P[i102].C[1] = color.G;
                            conto.P[i102].C[2] = color.B;
                        }
                        if (conto.P[i102].Glass == 1)
                        {
                            conto.P[i102].Gr += (int) Math.Abs(f103 * 1.5);
                        }
                    }
                }
            }
            if (i99 * i98 == -1)
            {
                if (_nbsq > 0)
                {
                    var i105 = 0;
                    var i106 = 1;
                    for (var i107 = 0; i107 < conto.Npl; i107++)
                    {
                        var f108 = 0.0F;
                        for (var i109 = 0; i109 < conto.P[i107].N; i109++)
                        {
                            if (conto.P[i107].Wz == 0)
                            {
                                f108 = f / 15.0F * Medium.Random();
                                if ((Math.Abs(conto.P[i107].Oy[i109] - Stat.Flipy - Squash) < Stat.Msquash * 3 ||
                                     conto.P[i107].Oy[i109] < Stat.Flipy + Squash) && Squash < Stat.Msquash)
                                {
                                    conto.P[i107].Oy[i109] += (int) f108;
                                    i105 += (int) f108;
                                    i106++;
                                    if (abool)
                                    {
                                        Hitmag += (int) Math.Abs(f108);
                                        i97 += (int) Math.Abs(f108);
                                    }
                                }
                            }
                        }

                        if (conto.P[i107].Glass == 1)
                        {
                            conto.P[i107].Gr += 5;
                        }
                        else if (f108 != 0.0F)
                        {
                            conto.P[i107].Bfase += (int) f108;
                        }
                        if (Math.Abs(f108) >= 1.0F)
                        {
                            conto.P[i107].Chip = 1;
                            conto.P[i107].Ctmag = f108;
                        }
                    }
                    Squash += i105 / i106;
                    _nbsq = 0;
                }
                else
                {
                    _nbsq++;
                }
            }
        }
        return i97;
    }

    private int Regz(int i, float f, ContO conto)
    {
        var i114 = 0;
        var abool = true;
        /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
        {
            abool = false;
        }
        if (XTGraphics.Multion >= 2)
        {
            abool = false;
        }
        if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
        {
            abool = true;
        }*/
        f *= Stat.Dammult;
        if (Math.Abs(f) > 100.0F)
        {
            //Record.Recz(i, f, Im);
            if (f > 100.0F)
            {
                f -= 100.0F;
            }
            if (f < -100.0F)
            {
                f += 100.0F;
            }
            Shakedam = (int) ((Math.Abs(f) + Shakedam) / 2.0F);
            /*
            if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, 0);
            }*/
            for (var i115 = 0; i115 < conto.Npl; i115++)
            {
                var f116 = 0.0F;
                for (var i117 = 0; i117 < conto.P[i115].N; i117++)
                {
                    if (conto.P[i115].Wz == 0 && Py(conto.Keyx[i], conto.P[i115].Ox[i117], conto.Keyz[i],
                            conto.P[i115].Oz[i117]) < Stat.Clrad)
                    {
                        f116 = f / 20.0F * Medium.Random();
                        conto.P[i115].Oz[i117] += (int) (f116 * Medium.Cos(conto.Xz) * Medium.Cos(conto.Zy));
                        conto.P[i115].Ox[i117] += (int) (f116 * Medium.Sin(conto.Xz) * Medium.Cos(conto.Xy));
                        if (abool)
                        {
                            Hitmag += (int) Math.Abs(f116);
                            i114 += (int) Math.Abs(f116);
                        }
                    }
                }

                if (f116 != 0.0F)
                {
                    if (Math.Abs(f116) >= 1.0F)
                    {
                        conto.P[i115].Chip = 1;
                        conto.P[i115].Ctmag = f116;
                    }
                    if (!conto.P[i115].Nocol && conto.P[i115].Glass != 1)
                    {
                        if (conto.P[i115].Bfase > 20 && conto.P[i115].HSB[1] > 0.25)
                        {
                            conto.P[i115].HSB[1] = 0.25F;
                        }
                        if (conto.P[i115].Bfase > 25 && conto.P[i115].HSB[2] > 0.7)
                        {
                            conto.P[i115].HSB[2] = 0.7F;
                        }
                        if (conto.P[i115].Bfase > 30 && conto.P[i115].HSB[1] > 0.15)
                        {
                            conto.P[i115].HSB[1] = 0.15F;
                        }
                        if (conto.P[i115].Bfase > 35 && conto.P[i115].HSB[2] > 0.6)
                        {
                            conto.P[i115].HSB[2] = 0.6F;
                        }
                        if (conto.P[i115].Bfase > 40)
                        {
                            conto.P[i115].HSB[0] = 0.075F;
                        }
                        if (conto.P[i115].Bfase > 50 && conto.P[i115].HSB[2] > 0.5)
                        {
                            conto.P[i115].HSB[2] = 0.5F;
                        }
                        if (conto.P[i115].Bfase > 60)
                        {
                            conto.P[i115].HSB[0] = 0.05F;
                        }
                        conto.P[i115].Bfase += (int) Math.Abs(f116);
                        new Color(conto.P[i115].C[0], conto.P[i115].C[1], conto.P[i115].C[2]);
                        var color = Color.GetHSBColor(conto.P[i115].HSB[0], conto.P[i115].HSB[1],
                            conto.P[i115].HSB[2]);
                        conto.P[i115].C[0] = color.R;
                        conto.P[i115].C[1] = color.G;
                        conto.P[i115].C[2] = color.B;
                    }
                    if (conto.P[i115].Glass == 1)
                    {
                        conto.P[i115].Gr += (int) Math.Abs(f116 * 1.5);
                    }
                }
            }
        }
        return i114;
    }

    internal void Reseto(int i, ContO conto)
    {
        Cn = i;
        for (var i0 = 0; i0 < 8; i0++)
        {
            _dominate[i0] = false;
            _caught[i0] = false;
        }
        Mxz = 0;
        Cxz = 0;
        Pzy = 0;
        Pxy = 0;
        Speed = 0.0F;
        for (var i1 = 0; i1 < 4; i1++)
        {
            Scy[i1] = 0.0F;
            Scx[i1] = 0.0F;
            Scz[i1] = 0.0F;
        }
        _forca = ((float) Math.Sqrt(conto.Keyz[0] * conto.Keyz[0] + conto.Keyx[0] * conto.Keyx[0]) +
                  (float) Math.Sqrt(conto.Keyz[1] * conto.Keyz[1] + conto.Keyx[1] * conto.Keyx[1]) +
                  (float) Math.Sqrt(conto.Keyz[2] * conto.Keyz[2] + conto.Keyx[2] * conto.Keyx[2]) +
                  (float) Math.Sqrt(conto.Keyz[3] * conto.Keyz[3] + conto.Keyx[3] * conto.Keyx[3])) / 10000.0F *
                 (float) (Stat.Bounce - 0.3);
        Mtouch = false;
        Wtouch = false;
        Txz = 0;
        _fxz = 0;
        _pmlt = 1;
        _nmlt = 1;
        _dcnt = 0;
        Skid = 0;
        Pushed = false;
        Gtouch = false;
        Pl = false;
        Pr = false;
        Pd = false;
        Pu = false;
        Loop = 0;
        Ucomp = 0.0F;
        Dcomp = 0.0F;
        Lcomp = 0.0F;
        Rcomp = 0.0F;
        _lxz = 0;
        Travxy = 0;
        Travzy = 0;
        Travxz = 0;
        Rtab = false;
        Ftab = false;
        Btab = false;
        Powerup = 0.0F;
        _xtpower = 0;
        Trcnt = 0;
        Capcnt = 0;
        _tilt = 0.0F;
        for (var i2 = 0; i2 < 4; i2++)
        {
            for (var i3 = 0; i3 < 4; i3++)
            {
                _crank[i2, i3] = 0;
                _lcrank[i2, i3] = 0;
            }
        }
        //Pcleared = CheckPoints.Pcs;
        Clear = 0;
        Nlaps = 0;
        _focus = -1;
        Missedcp = 0;
        Nofocus = false;
        Power = 98.0F;
        Lastcolido = 0;
        //CheckPoints.Dested[Im] = 0;
        Squash = 0;
        _nbsq = 0;
        Hitmag = 0;
        Cntdest = 0;
        Dest = false;
        Newcar = false;
        if (/*Im == XTGraphics.Im*/Im == 0)
        {
            Medium.Checkpoint = -1;
            Medium.Lastcheck = false;
        }
        _rpdcatch = 0;
        Newedcar = 0;
        _fixes = -1;
        /*if (CheckPoints.Nfix == 1)
        {
            _fixes = 4;
        }
        if (CheckPoints.Nfix == 2)
        {
            _fixes = 3;
        }
        if (CheckPoints.Nfix == 3)
        {
            _fixes = 2;
        }
        if (CheckPoints.Nfix == 4)
        {
            _fixes = 1;
        }*/
    }

    private void Rot(float[] fs, float[] fs134, int i, int i135, int i136, int i137)
    {
        if (i136 != 0)
        {
            for (var i138 = 0; i138 < i137; i138++)
            {
                var f = fs[i138];
                var f139 = fs134[i138];
                fs[i138] = i + ((f - i) * Medium.Cos(i136) - (f139 - i135) * Medium.Sin(i136));
                fs134[i138] = i135 + ((f - i) * Medium.Sin(i136) + (f139 - i135) * Medium.Cos(i136));
            }
        }
    }

    private int Rpy(float f, float f140, float f141, float f142, float
        f143, float f144)
    {
        return (int) ((f - f140) * (f - f140) + (f141 - f142) * (f141 - f142) + (f143 - f144) * (f143 - f144));
    }
}