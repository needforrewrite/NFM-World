using System.Diagnostics;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class Mad
{
    private static readonly float _tickRate = GameSparker.PHYSICS_MULTIPLIER;

    internal bool Btab;
    internal int Capcnt;
    internal bool BadLanding;
    private readonly bool[] _caught = new bool[8];
    internal Stat Stat;
    internal int Clear;
    internal int Cn;
    internal int Cntdest;
    private int _cntouch;
    private bool _colidim;
    private readonly int[,] _crank = new int[4, 4];
    internal float Cxz;
    private int _dcnt;
    internal float Dcomp;
    internal bool Wasted;
    private readonly bool[] _dominate = new bool[8];
    private readonly float _drag = 0.5F;
    private int _fixes = -1;
    private int _focus = -1;
    private float _forca;
    internal bool Ftab;
    private float _fxz;
    internal bool Gtouch;
    internal int Hitmag;
    internal int Im;
    internal int Lastcolido;
    internal float Lcomp;
    private readonly int[,] _lcrank = new int[4, 4];
    internal int Loop;
    private float _lxz;
    internal int Missedcp;
    internal bool Mtouch;
    internal float Mxz;
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

    internal float Pxy
    {
        set
        {
            if (float.IsNaN(value))
            {
                Debugger.Break();
            }
            field = value;
        }
        get;
    }
    internal float Pzy
    {
        set
        {
            if (float.IsNaN(value))
            {
                Debugger.Break();
            }
            field = value;
        }
        get;
    }
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
    internal float Travxz;
    internal int Travzy;
    internal int Trcnt;
    internal float Txz;
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

    public bool pointInBox(float px, float py, float pz, float bx, float by, float bz, float szx, float szy, float szz)
    {
        return px > bx - szx && px < bx + szx && pz > bz - szz && pz < bz + szz && py > by - szy && py < by + Math.Max(szy, 5);
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
                                if (UMath.RandomBoolean())
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
                                if (UMath.RandomBoolean())
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
        conto.Wasted = true;
    }
    public void bounceRebound(int wi, ContO conto)
    {
        // part 1: the closer we are to 90/-90 in Pxy or Pzy, the bigger the bounce
        float rebound = (Math.Abs(UMath.Sin(Pxy)) + Math.Abs(UMath.Sin(Pzy))) / 3;
        float maxAngleRebound = 0.4F; // capping at 0.4 doesn't do much, max is two thirds
        rebound = Math.Min(rebound, maxAngleRebound);

        // part 2: the bigger the bounce stat, the bigger the bounce
        rebound += CarDefine.Bounce[Cn];
        float minRebound = 1.1F;
        rebound = Math.Max(rebound, minRebound);

        Regy(wi, Math.Abs(Scy[wi] * rebound), conto);
        // if scy is > 0 then we are going down, apply the rebound bounce
        if (Scy[wi] > 0.0F)
            // we are subtracting scy * f_51 from scy
            // so, for example, if f_51 is 1.1 (which is the minimum bounce)
            // the result will be = scy - (1.1 * scy)
            // which is just 0.1 * scy
            // this also means the bigger the scy, the bigger the rebound
            // this means, unless the bounce stat is too high
            // f_51 will be below 2
            // which means the result will be some
            // c * scy
            // where c is below 1, leading to exponential decay in rebounds

            // I decided to rewrite this to the form which I think is most readable
            // but all three are equivalent
            // Scy[wi] -= Math.Abs(Scy[wi] * rebound);
            // Scy[wi] -= Scy[wi] * rebound; // don't need the abs, both are always positive
            Scy[wi] = -1 * Scy[wi] * (rebound - 1);
    }

    public void bounceReboundZ(int ti, int wi, ContO conto, bool wasMtouch/*, Trackers trackers, CheckPoints checkpoints*/) {
        float rebound = Math.Abs(UMath.Cos(Pxy)) + Math.Abs(UMath.Cos(Pzy)) / 4;
        float maxAngleRebound = 0.3F;
        rebound = Math.Min(rebound, maxAngleRebound);
//        if (wasMtouch)
//            rebound = 0;
        rebound += CarDefine.Bounce[Cn] - 0.2F;
        float minRebound = 1.1F;
        rebound = Math.Max(rebound, minRebound);
        Regz(wi, -1 * Scz[wi] * rebound * Trackers.Dam[ti] /** checkpoints.dam*/, conto);
        Scz[wi] = -1 * Scz[wi] * (rebound - 1);
    }

    public void bounceReboundX(int ti, int wi, ContO conto, bool wasMtouch/*, Trackers trackers, CheckPoints checkpoints*/) {
        float rebound = Math.Abs(UMath.Cos(Pxy)) + Math.Abs(UMath.Cos(Pzy)) / 4;
        float maxAngleRebound = 0.3F;
        rebound = Math.Min(rebound, maxAngleRebound);
//        if (wasMtouch)
//            rebound = 0;
        rebound += CarDefine.Bounce[Cn] - 0.2F;
        float minRebound = 1.1F;
        rebound = Math.Max(rebound, minRebound);
        Regx(wi, -1 * Scx[wi] * rebound * Trackers.Dam[ti]/* * checkpoints.dam*/, conto);
        Scx[wi] = -1 * Scx[wi] * (rebound - 1);
    }

    int Mtcount = 0;
    int py = 0;

    internal void Drive(Control control, ContO conto)
    {
        FrameTrace.AddMessage($"xz: {conto.Xz:0.00}, mxz: {Mxz:0.00}, lxz: {_lxz:0.00}, fxz: {_fxz:0.00}, cxz: {Cxz:0.00}");
        FrameTrace.AddMessage($"xy: {conto.Xy:0.00}, pxy: {Pxy:0.00}, zy: {conto.Zy:0.00}, pzy: {Pzy:0.00}");
        
        var xneg = 1;
        var zneg = 1;
        var zyinv = false;
        var revspeed = false;
        var hitVertical = false;
        BadLanding = false;
        if (!Mtouch) Mtcount++; //DS-addons: Bad landing hotfix
        float zyangle;
        for (zyangle = Math.Abs(Pzy); zyangle > 360; zyangle -= 360)
        {
            /* empty */
        }

        float xyangle;
        for (xyangle = Math.Abs(Pxy); xyangle > 360; xyangle -= 360)
        {
            /* empty */
        }

        float zy;
        for (zy = Math.Abs(Pzy); zy > 270; zy -= 360)
        {
        }

        zy = Math.Abs(zy);
        if (zy > 90)
        {
            zyinv = true;
        }

        var xyinv = false;
        float xy;
        for (xy = Math.Abs(Pxy); xy > 270; xy -= 360)
        {
        }

        xy = Math.Abs(xy);
        if (xy > 90)
        {
            xyinv = true;
            zneg = -1;
        }


        var bottomy = conto.Grat;
        if (zyinv)
        {
            if (xyinv)
            {
                xyinv = false;
                revspeed = true;
            }
            else
            {
                xyinv = true;
                BadLanding = true;
            }

            xneg = -1;
        }
        else if (xyinv)
        {
            BadLanding = true;
        }

        if (BadLanding)
        {
            bottomy = Stat.Flipy + Squash;
        }

        control.Zyinv = zyinv;
        //

        var airx = 0.0F;
        var airz = 0.0F;
        var airy = 0.0F;
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
        } //

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
        } //

        if (!Wasted)
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
                        Ucomp += 0.5f * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Sin(conto.Xz) * zneg * _tickRate;
                    airz = Stat.Airc * UMath.Cos(conto.Xz) * zneg * _tickRate;
                }
                else if (Ucomp != 0.0F && Ucomp > -2.0F)
                {
                    Ucomp -= 0.5f * Stat.Airs * _tickRate; //
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
                        Dcomp += 0.5f * Stat.Airs * _tickRate; //
                    }

                    airy = -Stat.Airc * _tickRate;
                }
                else if (Dcomp != 0.0F && Ucomp > -2.0F)
                {
                    Dcomp -= 0.5f * Stat.Airs * _tickRate;
                } //

                if (control.Left)
                {
                    if (Lcomp == 0.0F)
                    {
                        Lcomp = 5.0F;
                    }

                    if (Lcomp < 20.0F) // maxine: scale to tickrate
                    {
                        Lcomp += 2.0F * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = -Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Lcomp > 0.0F)
                {
                    Lcomp -= 2.0F * Stat.Airs * _tickRate; //
                }

                if (control.Right) //
                {
                    if (Rcomp == 0.0F)
                    {
                        Rcomp = 5.0F;
                    }

                    if (Rcomp < 20.0F) // maxine: scale to tickrate
                    {
                        Rcomp += 2.0F * Stat.Airs * _tickRate;
                    }

                    airx = Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Rcomp > 0.0F) //
                {
                    Rcomp -= 2.0F * Stat.Airs * _tickRate;
                }

                Pzy = QuantizeTowardsZero((Pzy + (Dcomp - Ucomp) * UMath.Cos(Pxy) * _tickRate), _tickRate); //
                if (zyinv)
                {
                    conto.Xz = QuantizeTowardsZero(conto.Xz + ((Dcomp - Ucomp) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }
                else
                {
                    conto.Xz = QuantizeTowardsZero(conto.Xz - ((Dcomp - Ucomp) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }

                Pxy = QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
            }
            else
            {
                //
                var f15 = Power;
                if (f15 < 40.0F)
                {
                    f15 = 40.0F;
                }

                if (control.Down)
                {
                    if (Speed > 0.0F)
                    {
                        Speed -= Stat.Handb / 2 * _tickRate;
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
                            //
                            Speed -= (Stat.Acelf[i16] / 2.0F + f15 * Stat.Acelf[i16] / 196.0F) * _tickRate;
                        }
                        else
                        {
                            Speed = -(Stat.Swits[1] / 2 + f15 * Stat.Swits[1] / 196.0F);
                        }
                    }
                }

                if (control.Up)
                {
                    if (Speed < 0.0F) //
                    {
                        Speed += Stat.Handb * _tickRate;
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
                            Speed += (Stat.Acelf[i18] / 2.0F + f15 * Stat.Acelf[i18] / 196.0F) * _tickRate;
                        }
                        else
                        {
                            Speed = Stat.Swits[2] / 2 + f15 * Stat.Swits[2] / 196.0F;
                        }
                    }
                } //

                if (control.Handb && Math.Abs(Speed) > Stat.Handb)
                {
                    if (Speed < 0.0F)
                    {
                        Speed += Stat.Handb * _tickRate;
                    }
                    else
                    {
                        Speed -= Stat.Handb * _tickRate;
                    }
                } //

                if (Loop == -1 && conto.Y < 100)
                {
                    if (control.Left)
                    {
                        if (!Pl)
                        {
                            if (Lcomp == 0.0F)
                            {
                                Lcomp = 5.0F * Stat.Airs * _tickRate;
                            }

                            if (Lcomp < 20.0F)
                            {
                                Lcomp += 2.0F * Stat.Airs * _tickRate;
                            }
                        }
                    } //
                    else
                    {
                        if (Lcomp > 0.0F)
                        {
                            Lcomp -= 2.0F * Stat.Airs * _tickRate;
                        }

                        Pl = false;
                    } //

                    if (control.Right)
                    {
                        if (!Pr)
                        {
                            if (Rcomp == 0.0F)
                            {
                                Rcomp = 5.0F * Stat.Airs * _tickRate;
                            }

                            if (Rcomp < 20.0F)
                            {
                                Rcomp += 2.0F * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Rcomp > 0.0F)
                        {
                            Rcomp -= 2.0F * Stat.Airs * _tickRate;
                        }

                        Pr = false;
                    } //

                    if (control.Up)
                    {
                        if (!Pu)
                        {
                            if (Ucomp == 0.0F)
                            {
                                Ucomp = 5.0F * Stat.Airs * _tickRate;
                            }

                            if (Ucomp < 20.0F)
                            {
                                Ucomp += 2.0F * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Ucomp > 0.0F)
                        {
                            Ucomp -= 2.0F * Stat.Airs * _tickRate;
                        }

                        Pu = false;
                    }

                    if (control.Down)
                    {
                        if (!Pd)
                        {
                            if (Dcomp == 0.0F)
                            {
                                Dcomp = 5.0F * Stat.Airs * _tickRate;
                            }

                            if (Dcomp < 20.0F)
                            {
                                Dcomp += 2.0F * Stat.Airs * _tickRate;
                            }
                        }
                    }
                    else
                    {
                        if (Dcomp > 0.0F)
                        {
                            Dcomp -= 2.0F * Stat.Airs * _tickRate;
                        }

                        Pd = false;
                    }

                    Pzy = QuantizeTowardsZero((Pzy + ((Dcomp - Ucomp) * UMath.Cos(Pxy)) * _tickRate), _tickRate);
                    if (zyinv)
                    {
                        conto.Xz = QuantizeTowardsZero(conto.Xz + (((Dcomp - Ucomp) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }
                    else
                    {
                        conto.Xz = QuantizeTowardsZero(conto.Xz - (((Dcomp - Ucomp) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }

                    Pxy = QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
                }
            }
        }

        var f20 = 20.0F * Speed / (154.0F * Stat.Simag);
        if (f20 > 20.0F)
        {
            f20 = 20.0F;
        }

        conto.Wzy -= f20 * _tickRate; // maxine: remove int cast. i dont think it belongs here
        // commented out in phys physics
        //        if (conto.Wzy < -30)
        //        {
        //            conto.Wzy += 30;
        //        }
        //        if (conto.Wzy > 30)
        //        {
        //            conto.Wzy -= 30;
        //        }
        if (control.Right)
        {
            conto.Wxz -= Stat.Turn * _tickRate;
            if (conto.Wxz < -36)
            {
                conto.Wxz = -36;
            }
        }

        if (control.Left)
        {
            conto.Wxz += Stat.Turn * _tickRate;
            if (conto.Wxz > 36)
            {
                conto.Wxz = 36;
            }
        } //

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
                    conto.Wxz--; // tick rate for this stuff?
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
                    conto.Wxz -= Stat.Turn * 2 * _tickRate;
                }

                if (conto.Wxz < 0)
                {
                    conto.Wxz += Stat.Turn * 2 * _tickRate;
                }
            }
        } //

        var i21 = (int)(3600.0F / (Speed * Speed));
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
            if (!BadLanding)
            {
                if (!control.Handb)
                {
                    _fxz = conto.Wxz / (i21 * 3);
                }
                else
                {
                    _fxz = conto.Wxz / i21;
                }

                conto.Xz += conto.Wxz / i21 * _tickRate;
            }

            Wtouch = false;
            Gtouch = false;
        }
        else
        {
            conto.Xz += _fxz * _tickRate;
        } //

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

            //
            if (SafeMath.Abs(Mxz - Cxz) < 30)
            {
                Cxz += (Mxz - Cxz) / 4.0F * _tickRate; //
            }
            else
            {
                if (Cxz > Mxz)
                {
                    Cxz -= 10 * _tickRate;
                }

                if (Cxz < Mxz)
                {
                    Cxz += 10 * _tickRate;
                }
            }
        }

        var wheelx = new float[4];
        var wheelz = new float[4];
        var wheely = new float[4];
        for (var i24 = 0; i24 < 4; i24++)
        {
            wheelx[i24] = conto.Keyx[i24] + conto.X;
            wheely[i24] = bottomy + conto.Y;
            wheelz[i24] = conto.Z + conto.Keyz[i24];
            Scy[i24] += 7.0F * _tickRate;
        }

        UMath.Rot(wheelx, wheely, conto.X, conto.Y, Pxy, 4);
        UMath.Rot(wheely, wheelz, conto.Y, conto.Z, Pzy, 4);
        UMath.Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        var wasMtouch = false;
        var i26 = (int)((Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4.0F);
        var i27 = (int)((Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4.0F);
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
            wheely[i29] += Scy[i29] * _tickRate;
            wheelx[i29] += (Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4.0F * _tickRate;
            wheelz[i29] += (Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4.0F * _tickRate;
        } //

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

        var surfaceType = 1;
        for (var i = 0; i < Trackers.Nt; i++) // maxine: remove trackers.sect use here
        {
            if (Math.Abs(Trackers.Zy[i]) != 90 && Math.Abs(Trackers.Xy[i]) != 90 &&
                Math.Abs(conto.X - Trackers.X[i]) < Trackers.Radx[i] &&
                Math.Abs(conto.Z - Trackers.Z[i]) < Trackers.Radz[i])
            {
                surfaceType = Trackers.Skd[i];
            }
        } //

        if (Mtouch)
        {
            // Jacher: 1/_tickrate for traction; Txz is set on previous tick so we need to scale
            var traction = Stat.Grip;
            traction -= Math.Abs(Txz - conto.Xz) * (1 / _tickRate) * Speed / 250.0F;
            if (control.Handb)
            {
                traction -= Math.Abs(Txz - conto.Xz) * (1 / _tickRate) * 4;
            }

            if (traction < Stat.Grip)
            {
                if (Skid != 2)
                {
                    Skid = 1;
                }

                Speed -= Speed / 100.0F * _tickRate;
            } //
            else if (Skid == 1)
            {
                Skid = 2;
            }

            if (surfaceType == 1)
            {
                traction = (int)(traction * 0.75);
            }

            if (surfaceType == 2)
            {
                traction = (int)(traction * 0.55);
            }

            var speedx = -(int)(Speed * UMath.Sin(conto.Xz) * UMath.Cos(Pzy));
            var speedz = (int)(Speed * UMath.Cos(conto.Xz) * UMath.Cos(Pzy));
            var speedy = -(int)(Speed * UMath.Sin(Pzy));
            if (BadLanding || Wasted /*|| CheckPoints.Haltall*/)
            {
                speedx = 0;
                speedz = 0;
                speedy = 0;
                traction = Stat.Grip / 5.0F;
                Speed -= 2.0F * Math.Sign(Speed) * _tickRate;
            } //

            if (Math.Abs(Speed) > _drag * _tickRate)
            {
                Speed -= _drag * Math.Sign(Speed) * _tickRate;
            }
            else
            {
                Speed = 0.0F;
            }

            if (Cn == 8 && traction < 5.0F)
            {
                traction = 5.0F;
            }

            if (traction < 1.0F)
            {
                traction = 1.0F;
            } //

            float minTraction = 1.0f;
            traction = Math.Max(traction, minTraction);

            for (var j = 0; j < 4; j++)
            {
                // maxine: traction fixes by Jacher. done slightly different but same result
                if (Math.Abs(Scx[j] - speedx) > traction * _tickRate)
                {
                    Scx[j] += traction * Math.Sign(speedx - Scx[j]) * _tickRate;
                }
                else
                {
                    Scx[j] = speedx;
                }

                if (Math.Abs(Scz[j] - speedz) > traction * _tickRate)
                {
                    Scz[j] += traction * Math.Sign(speedz - Scz[j]) * _tickRate;
                }
                else
                {
                    Scz[j] = speedz;
                }

                if (Math.Abs(Scy[j] - speedy) > traction * _tickRate)
                {
                    // Jacher: decouple this from tickrate
                    // this reduces bouncing when AB-ing, but at what cost?
                    // oteek: if decoupled slanted ramps make car bounce for no reason for a bit
                    Scy[j] += traction * Math.Sign(speedy - Scy[j]) * _tickRate;
                }
                else
                {
                    Scy[j] = speedy;
                } //

                // maxine: maybe this should be scaled to tickrate?
                if (traction < Stat.Grip)
                {
                    if (Txz != conto.Xz)
                    {
                        _dcnt++;
                    }
                    else
                    {
                        _dcnt = 0;
                    }

                    if (_dcnt > 40.0F * traction / Stat.Grip || BadLanding)
                    {
                        var f42 = 1.0F;
                        if (surfaceType != 0)
                        {
                            f42 = 1.2F;
                        }

                        if (UMath.Random() > 0.65)
                        {
                            // conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                            //     f42 * Stat.Simag, (int)_tilt, BadLanding && Mtouch);
                            if ( /*Im == XTGraphics.Im &&*/ !BadLanding)
                            {
                                //XTPart2.Skidf(Im, i32,
                                //    (float) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (surfaceType == 1 && UMath.Random() > 0.8)
                        {
                            // conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                            //     1.1F * Stat.Simag, (int)_tilt, BadLanding && Mtouch);
                        }

                        if ((surfaceType == 2 || surfaceType == 3) && UMath.Random() > 0.6)
                        {
                            // conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                            //     1.15F * Stat.Simag, (int)_tilt, BadLanding && Mtouch);
                        }
                    }
                }
                else if (_dcnt != 0)
                {
                    _dcnt = Math.Max(_dcnt - 2, 0);
                }

                if (surfaceType == 3 || surfaceType == 4)
                {
                    int
                        k = Util.Random.Int(0,
                            4); // choose 4 wheels randomly to bounce up, usually some wheel will be chosen twice, which means another wheel is not chosen, causing tilt
                    float bumpLift = surfaceType == 3 ? -100F : -150F;
                    float rng = 0.75F;
                    Scy[k] = bumpLift * rng * Speed * _tickRate / CarDefine.Swits[Cn, 2] * (CarDefine.Bounce[Cn] - 0.3F);
                }
            }

            Txz = conto.Xz; // CHK1

            float scxsum = 0;
            float sczsum = 0;
            // 4 = nwheels
            for (int j = 0; j < 4; ++j)
            {
                scxsum += Scx[j];
                sczsum += Scz[j];
            }

            float scxavg = scxsum / 4; /* nwheels */
            float sczavg = sczsum / 4;
            float scxz = float.Hypot(sczavg, scxavg);

            Mxz = (int)dAtan2(-scxsum, sczsum);

            if (Skid == 2)
            {
                if (!BadLanding)
                {
                    Speed = scxz * UMath.Cos(Mxz - conto.Xz) * (revspeed ? -1 : 1);
                }

                Skid = 0;
            }

            if (BadLanding && scxsum == 0.0F && sczsum == 0.0F)
            {
                surfaceType = 0;
            } //

            Mtouch = false;
            Mtcount = 0;
            wasMtouch = true;
        }
        else
        {
            Skid = 2;
        }

        var nGroundedWheels = 0;
        Span<bool> isWheelGrounded = stackalloc bool[4];
        float groundY = 250f;
        float wheelYThreshold = 5f;
        float f48 = 0.0F;
        for (var i49 = 0; i49 < 4; i49++)
        {
            isWheelGrounded[i49] = false;
            if (wheely[i49] > 245.0F)
            {
                nGroundedWheels++;
                Wtouch = true;
                Gtouch = true;
                if (!wasMtouch && Scy[i49] != 7.0F)
                {
                    var f50 = Scy[i49] / 333.33F;
                    if (f50 > 0.3F)
                    {
                        f50 = 0.3F;
                    }

                    if (surfaceType == 0)
                    {
                        f50 += 1.1f;
                    }
                    else
                    {
                        f50 += 1.2f;
                    }

                    // conto.Dust(i49, wheelx[i49], wheely[i49], wheelz[i49], (int)Scx[i49], (int)Scz[i49],
                    //     f50 * Stat.Simag,
                    //     0, BadLanding && Mtouch);
                } // CHK2

                wheely[i49] = 250.0F;
                f48 += wheely[i49] - 250.0F;
                isWheelGrounded[i49] = true;

                bounceRebound(i49, conto);
            }
        }

        OmarTrackPieceCollision(control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical);

        // sparks and scrapes
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

        // Jacher: change all this to float. The old code was blatantly wrong:
        // i_81 = d > 1 ? 0 : (float) dAcos(ratio) * sgn;
        // `d` was an unused double set to 0.0 and never used. GO figure.
        float i_81 = 0;
        if (Scy[2] != Scy[0]) {
            float sgn = Scy[2] < Scy[0] ? -1 : 1;
            float ratio = Hypot3(wheelz[0] - wheelz[2], wheely[0] - wheely[2], wheelx[0] - wheelx[2]) / (Math.Abs(conto.Keyz[0]) + Math.Abs(conto.Keyz[2]));
            i_81 = ratio >= 1 ? sgn : (float) dAcos(ratio) * sgn; // the d > 1 ? 0 part was different in the original code, but this I think makes more sense
        }
        float i_82 = 0;
        if (Scy[3] != Scy[1]) {
            float sgn = Scy[3] < Scy[1] ? -1 : 1;
            float ratio = Hypot3(wheelz[1] - wheelz[3], wheely[1] - wheely[3], wheelx[1] - wheelx[3]) / (Math.Abs(conto.Keyz[1]) + Math.Abs(conto.Keyz[3]));
            i_82 = ratio >= 1 ? sgn : (float) dAcos(ratio) * sgn;
        }
        float i_83 = 0;
        if (Scy[1] != Scy[0]) {
            float sgn = Scy[1] < Scy[0] ? -1 : 1;
            float ratio = Hypot3(wheelz[0] - wheelz[1], wheely[0] - wheely[1], wheelx[0] - wheelx[1]) / (Math.Abs(conto.Keyx[0]) + Math.Abs(conto.Keyx[1]));
            i_83 = ratio >= 1 ? sgn : (float) dAcos(ratio) * sgn;
        }
        float i_84 = 0;
        if (Scy[3] != Scy[2]) {
            float sgn = Scy[3] < Scy[2] ? -1 : 1;
            float ratio = Hypot3(wheelz[2] - wheelz[3], wheely[2] - wheely[3], wheelx[2] - wheelx[3]) / (Math.Abs(conto.Keyx[2]) + Math.Abs(conto.Keyx[3]));
            i_84 = ratio >= 1 ? sgn : (float) dAcos(ratio) * sgn;
        }

        if (hitVertical) {
            float i_85;
            for (i_85 = Math.Abs(conto.Xz + 45); i_85 > 180; i_85 -= 360) {}
            _pmlt = Math.Abs(i_85) > 90 ? 1 : -1;
            for (i_85 = Math.Abs(conto.Xz - 45); i_85 > 180; i_85 -= 360) {}
            _nmlt = Math.Abs(i_85) > 90 ? 1 : -1;
        }

        // I think this line, among other things, is responsible for causing flatspins after glitching on the edge of a ramp
        conto.Xz += _tickRate * _forca * (Scz[0] * _nmlt - Scz[1] * _pmlt + Scz[2] * _pmlt - Scz[3] * _nmlt + Scx[0] * _pmlt + Scx[1] * _nmlt - Scx[2] * _nmlt - Scx[3] * _pmlt);
        
        if (Math.Abs(i_82) > Math.Abs(i_81))
        {
            i_81 = i_82;
        }
        if (Math.Abs(i_84) > Math.Abs(i_83))
        {
            i_83 = i_84;
        }

        // CHK11
        if (!Mtouch && py < 0/* && this.mtCount > 15*/) {
            var zeroanglezy = Math.Min(zyangle, 360 - zyangle); //distance from 0 degrees in the zy-plane
            var flipanglezy = Math.Abs(zyangle - 180); //distance from 180 degrees in the zy-plane
            if(zeroanglezy <= flipanglezy && zyangle < 180 || flipanglezy < zeroanglezy && zyangle >= 180) //the landing adjustment mechanism
            {
            	if(Pzy > 0) //Pzy can be negative, so this needs to be accounted for
                {
                    Pzy -= QuantizeTowardsZero(Math.Abs(i_81) * _tickRate, _tickRate); 
                }
                else
                {
                    Pzy += QuantizeTowardsZero(Math.Abs(i_81) * _tickRate, _tickRate);
                }
            }
            if(zeroanglezy <= flipanglezy && zyangle >= 180 || flipanglezy < zeroanglezy && zyangle < 180) //similar to above, just in reverse
            {
            	if(Pzy > 0)
                {
                    Pzy += QuantizeTowardsZero(Math.Abs(i_81) * _tickRate, _tickRate);
                }
                else
                {
                    Pzy -= QuantizeTowardsZero(Math.Abs(i_81) * _tickRate, _tickRate);
                }
            } 
            var zeroanglexy = Math.Min(xyangle, 360 - xyangle); //distance from 0 degrees in the xy-plane
            var flipanglexy = Math.Abs(xyangle - 180); //distance from 180 degrees in the xy-plane
            if(zeroanglexy <= flipanglexy && xyangle < 180 || flipanglexy < zeroanglexy && xyangle >= 180) //same as above, just for the xy-plane
            {
            	if(Pxy > 0) //again, Pxy can be negative
                {
                    Pxy -= QuantizeTowardsZero(Math.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy += QuantizeTowardsZero(Math.Abs(i_83) * _tickRate, _tickRate);
                }
            }
            if(zeroanglexy <= flipanglexy && xyangle >= 180 || flipanglexy < zeroanglexy && xyangle < 180)
            {
            	if (Pxy > 0)
                {
                    Pxy += QuantizeTowardsZero(Math.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy -= QuantizeTowardsZero(Math.Abs(i_83) * _tickRate, _tickRate);
                }
            }
        } else {
            if (!zyinv)
                Pzy += i_81;
            else
                Pzy -= i_81;
            if (!xyinv)
                Pxy += i_83;
            else
                Pxy -= i_83;
        }
        //
        if (nGroundedWheels == 4) {
            int i_86 = 0;
            while (Pzy < 360) {
                Pzy += 360;
                conto.Zy += 360;
            }
            while (Pzy > 360) {
                Pzy -= 360;
                conto.Zy -= 360;
            }
            if (Pzy < 190 && Pzy > 170) {
                Pzy = 180;
                conto.Zy = 180;
                i_86++;
            }
            if (Pzy > 350 || Pzy < 10) {
                Pzy = 0;
                conto.Zy = 0;
                i_86++;
            }
            while (Pxy < 360) {
                Pxy += 360;
                conto.Xy += 360;
            }
            while (Pxy > 360) {
                Pxy -= 360;
                conto.Xy -= 360;
            }
            if (Pxy < 190 && Pxy > 170) {
                Pxy = 180;
                conto.Xy = 180;
                i_86++;
            }
            if (Pxy > 350 || Pxy < 10) {
                Pxy = 0;
                conto.Xy = 0;
                i_86++;
            }
            if (i_86 == 2) {
                Mtouch = true; //DS-addons: Bad landing hotfix
            }
        }
        if (!Mtouch && Wtouch) {
            if (_cntouch == 10) {
                Mtouch = true; //DS-addons: Bad landing hotfix
            } else {
                _cntouch++;
            }
        } else
            _cntouch = 0; // CHK12
        //DS-addons: Bad landing hotfix
        int newy = (int) ((wheely[0] + wheely[1] + wheely[2] + wheely[3]) / 4.0F - bottomy * UMath.Cos(Pzy) * UMath.Cos(Pxy) + airy);
        py = conto.Y - newy;
        conto.Y = newy;
        //conto.y = (int) ((fs_23[0] + fs_23[1] + fs_23[2] + fs_23[3]) / 4.0F - (float) i_10 * UMath.Cos(this.Pzy) * UMath.Cos(this.Pxy) + f_12);
        //
        if (zyinv)
            xneg = -1;
        else
            xneg = 1;

        FrameTrace.AddMessage($"x: {airx:0.00}, z: {airz:0.00}, sum: {UMath.Sin(Pxy):0.00}, sum2: {UMath.Sin(Pzy):0.00}");

        // CHK13
        // car sliding fix by jacher: do not adjust to tickrate
        conto.X = (int) ((wheelx[0] - conto.Keyx[0] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[0] * UMath.Sin(conto.Xz) + 
            wheelx[1] - conto.Keyx[1] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[1] * UMath.Sin(conto.Xz) + 
            wheelx[2] - conto.Keyx[2] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[2] * UMath.Sin(conto.Xz) + 
            wheelx[3] - conto.Keyx[3] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[3] * UMath.Sin(conto.Xz)) / 4.0F 
            + bottomy * UMath.Sin(Pxy) * UMath.Cos(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Sin(conto.Xz) + airx);
            
        conto.Z = (int) (
            (wheelz[0] - xneg * conto.Keyz[0] * UMath.Cos(conto.Xz) - conto.Keyx[0] * UMath.Sin(conto.Xz)
            + wheelz[1] - xneg * conto.Keyz[1] * UMath.Cos(conto.Xz) - conto.Keyx[1] * UMath.Sin(conto.Xz) 
            + wheelz[2] - xneg * conto.Keyz[2] * UMath.Cos(conto.Xz) - conto.Keyx[2] * UMath.Sin(conto.Xz) 
            + wheelz[3] - xneg * conto.Keyz[3] * UMath.Cos(conto.Xz) - conto.Keyx[3] * UMath.Sin(conto.Xz)) / 4.0F 
            + bottomy * UMath.Sin(Pxy) * UMath.Sin(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Cos(conto.Xz) + airz);

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
        } // CHK14
        if (Wtouch && !BadLanding)
        {
            var f87 = (Speed / Stat.Swits[2] * 14.0F * (Stat.Bounce - 0.4f));
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
            conto.Xy += (int)_tilt;
            if (Gtouch)
            {
                conto.Y -= (int)(_tilt / 1.5f);
            }
        }
        else if (_tilt != 0.0F)
        {
            _tilt = 0.0F;
        }
        if (Wtouch && surfaceType == 2)
        {
            conto.Zy += (int)((UMath.Random() * 6.0F * Speed / Stat.Swits[2] - 3.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
            conto.Xy += (int)((UMath.Random() * 6.0F * Speed / Stat.Swits[2] - 3.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
        }
        if (Wtouch && surfaceType == 1)
        {
            conto.Zy += (int)((UMath.Random() * 4.0F * Speed / Stat.Swits[2] - 2.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
            conto.Xy += (int)((UMath.Random() * 4.0F * Speed / Stat.Swits[2] - 2.0F * Speed / Stat.Swits[2]) *
                               (Stat.Bounce - 0.3));
        } // CHK15
        if (Hitmag >= Stat.Maxmag && !Wasted)
        {
            Distruct(conto);
            if (Cntdest == 7)
            {
                Wasted = true;
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
        var i89 = 0;
        var i90 = 0;
        var i91 = 0;
        if (Nofocus)
        {
            zneg = 1;
        }
        else
        {
            zneg = 7;
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
        }*/ // CHK16
        if (conto.Fcnt is 7 or 8)
        {
            Squash = 0;
            _nbsq = 0;
            Hitmag = 0;
            Cntdest = 0;
            Wasted = false;
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
                Travxy += (int)(Rcomp - Lcomp);
                if (Math.Abs(Travxy) > 135)
                {
                    Rtab = true;
                }
                Travzy += (int)(Ucomp - Dcomp);
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
        else if (!Wasted)
        {
            if (!BadLanding)
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
                                _xtpower = (int)(200 / _tickRate);
                            }
                            else
                            {
                                _xtpower = (int)(100 / _tickRate);
                            }
                        }
                    } // CHK17
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
                        Power -= (Power * Power * Power / Stat.Powerloss) * _tickRate;
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
        } // CHK18
        if (/*Im == XTGraphics.Im*/Im == 0)
        {
            if (control.Wall != -1)
            {
                control.Wall = -1;
            }
        }
        else if (Lastcolido != 0 && !Wasted)
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
                    Record.Whenwasted = (int) (185.0F + UMath.Random() * 20.0F);
                }
            }
        }*/
    }

    // input: number of grounded wheels to medium
    // output: hitVertical when colliding against a wall
    private void OmarTrackPieceCollision(Control control, ContO conto, float[] wheelx, float[] wheely, float[] wheelz,
        float groundY, float wheelYThreshold, ref int nGroundedWheels, bool wasMtouch, int surfaceType, out bool hitVertical)
    {
        hitVertical = false;

        Span<bool> isWheelTouchingPiece = [false, false, false, false]; // nwheels

        int nWheelsRoadRamp = 0;
        int nWheelsDirtRamp = 0;
        for (int j = 0; j < Trackers.Nt; j++)
        {
            for (int k = 0; k < 4; k++)
            {
                // the part below just makes sparks and scrape noises
                // this looks wrong though? there is no rady check
                //                if (isWheelGrounded[k] && Capsized && (Trackers.skd[j] == 0 || Trackers.skd[j] == 1) && wheelx[k] > (float) (Trackers.X[j] - Trackers.radx[j]) && wheelx[k] < (float) (Trackers.X[j] + Trackers.radx[j]) && wheelz[k] > (float) (Trackers.Z[j] - Trackers.radz[j]) && wheelz[k] < (float) (Trackers.Z[j] + Trackers.radz[j])) {
                //                    conto.sprk(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1);
                //                    if (this.im == this.xt.im)
                //                        this.xt.gscrape((int) Scx[k], (int) Scy[k], (int) Scz[k]);
                //                }

                // find the first piece that I am colliding with, snap wheel to it and stop
                if ( // CHK3
                    !isWheelTouchingPiece[k] &&
                    pointInBox(wheelx[k], wheely[k], wheelz[k], Trackers.X[j], Trackers.Y[j], Trackers.Z[j], Trackers.Radx[j], Trackers.Rady[j], Trackers.Radz[j])
                   )
                {
                    // ignore y == groundY because those are likely road pieces, which could make us break the loop early and miss a ramp
                    // this is also the reason why on the ground road and ramp ordering does not matter
                    // but when using floating pieces, you have to make sure the ramp comes first in the code
                    if (Trackers.Xy[j] == 0 && Trackers.Zy[j] == 0 && Trackers.Y[j] != groundY && wheely[k] > Trackers.Y[j] - wheelYThreshold)
                    {
                        ++nGroundedWheels;
                        Wtouch = true;
                        Gtouch = true;

                        // more dust stuff
                        if (!wasMtouch && Scy[k] != 7.0F /* * checkpoints.gravity */ * _tickRate)
                        { //Phy-addons: Recharged mode
                            float f_59 = Scy[k] / 333.33F;
                            if (f_59 > 0.3F)
                                f_59 = 0.3F;
                            if (surfaceType == 0)
                                f_59 += 1.1f;
                            else
                                f_59 += 1.2f;
                            // conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_59 * CarDefine.Simag[Cn], 0, BadLanding && Mtouch);
                        }

                        wheely[k] = Trackers.Y[j]; // snap wheel to the surface

                        // sparks and scrape
                        if (BadLanding && (Trackers.Skd[j] == 0 || Trackers.Skd[j] == 1))
                        {
                            // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1);
                            //if (Im == /*this.xt.im*/ 0)
                            //this.xt.gscrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                        }

                        bounceRebound(k, conto);
                        isWheelTouchingPiece[k] = true;
                    } // CHK4
                    // here we handle cases where zy is -90
                    // we are checking that we are approaching the surface from the "solid" side
                    // remember that surfaces in NFM are only 1 sided, going the other way doesnt
                    // result in a collision
                    // I don't know why the radz 287 part is there, this smells like there is some
                    // particular piece with radz 287 with special behavior
                    if (Trackers.Zy[j] == -90 && wheelz[k] < Trackers.Z[j] + Trackers.Radz[j] && (Scz[k] < 0.0F /*|| Trackers.radz[j] == 287*/))
                    {
                        // this next part looks like we are moving all wheels away from the wall
                        for (int l = 0; l < 4 /* nwheels */; l++)
                        {
                            if (k != l && wheelz[l] >= Trackers.Z[j] + Trackers.Radz[j])
                                wheelz[l] -= wheelz[k] - (Trackers.Z[j] + Trackers.Radz[j]);
                        }
                        wheelz[k] = Trackers.Z[j] + Trackers.Radz[j];

                        // sparks and scrapes
                        if (Trackers.Skd[j] != 2)
                            _crank[0, k]++;
                        if (Trackers.Skd[j] == 5 && Util.Random.Boolean())
                            _crank[0, k]++;
                        if (_crank[0, k] > 1)
                        {
                            // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0);
                            //if (Im == /*this.xt.im*/ 0)
                            //    this.xt.scrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                        }

                        // z rebound CHK5
                        bounceReboundZ(j, k, conto, wasMtouch/*, Trackers, checkpoints*/);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j])
                            control.Wall = j;
                    }
                    if (Trackers.Zy[j] == 90 && wheelz[k] > Trackers.Z[j] - Trackers.Radz[j] && (Scz[k] > 0.0F /*|| Trackers.radz[j] == 287*/))
                    {
                        //
                        for (int l = 0; l < 4 /* nwheels */; l++)
                        {
                            if (k != l && wheelz[l] <= Trackers.Z[j] - Trackers.Radz[j])
                                wheelz[l] -= wheelz[k] - (Trackers.Z[j] - Trackers.Radz[j]);
                        }
                        wheelz[k] = Trackers.Z[j] - Trackers.Radz[j];

                        //
                        if (Trackers.Skd[j] != 2)
                            _crank[1, k]++;
                        if (Trackers.Skd[j] == 5 && Util.Random.Boolean())
                            _crank[1, k]++;
                        if (_crank[1, k] > 1)
                        {
                            // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0);
                            //if (this.im == this.xt.im)
                            //    this.xt.scrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                        }

                        bounceReboundZ(j, k, conto, wasMtouch/*, Trackers, checkpoints*/);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j])
                            control.Wall = j;
                    } // CHK6
                    if (Trackers.Xy[j] == -90 && wheelx[k] < Trackers.X[j] + Trackers.Radx[j] && (Scx[k] < 0.0F /*|| Trackers.radx[j] == 287*/))
                    {
                        //
                        for (int l = 0; l < 4 /* nwheels */; l++)
                        {
                            if (k != l && wheelx[l] >= Trackers.X[j] + Trackers.Radx[j])
                                wheelx[l] -= wheelx[k] - (Trackers.X[j] + Trackers.Radx[j]);
                        }
                        wheelx[k] = Trackers.X[j] + Trackers.Radx[j];

                        //
                        if (Trackers.Skd[j] != 2)
                            _crank[2, k]++;
                        if (Trackers.Skd[j] == 5 && Util.Random.Boolean())
                            _crank[2, k]++;
                        if (_crank[2, k] > 1)
                        {
                            // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0);
                            //if (this.im == this.xt.im)
                            //    this.xt.scrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                        }

                        bounceReboundX(j, k, conto, wasMtouch/*, Trackers, checkpoints*/);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j])
                            control.Wall = j;
                    } // CHK7
                    if (Trackers.Xy[j] == 90 && wheelx[k] > Trackers.X[j] - Trackers.Radx[j] && (Scx[k] > 0.0F /*|| Trackers.radx[j] == 287*/))
                    {
                        //
                        for (int l = 0; l < 4 /* nwheels */; l++)
                        {
                            if (k != l && wheelx[l] <= Trackers.X[j] - Trackers.Radx[j])
                                wheelx[l] -= wheelx[k] - (Trackers.X[j] - Trackers.Radx[j]);
                        }
                        wheelx[k] = Trackers.X[j] - Trackers.Radx[j];

                        //
                        if (Trackers.Skd[j] != 2)
                            _crank[3, k]++;
                        if (Trackers.Skd[j] == 5 && Util.Random.Boolean())
                            _crank[3, k]++;
                        if (_crank[3, k] > 1)
                        {
                            // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0);
                            //if (this.im == this.xt.im)
                            //    this.xt.scrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                        }

                        bounceReboundX(j, k, conto, wasMtouch/*, Trackers, checkpoints*/);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j])
                            control.Wall = j;
                    } // CHK8
                    if (Trackers.Zy[j] != 0 && Trackers.Zy[j] != 90 && Trackers.Zy[j] != -90)
                    {
                        // perpendicular angle
                        int pAngle = 90 + Trackers.Zy[j];

                        // this looks like it might be a failed attempt at calculating where a normal vector
                        // from the wheel would intersect the surface of the ramp?
                        // if that's what this is then it is incorrect
                        // what this actually does is it creates a vector that is the vector
                        // from (tz, ty) to (wz, wy) but rotated by (zy + 90) degrees around (tz, ty)
                        // https://www.geogebra.org/geometry/vhaznznv
                        // let's call this rotated vector (rz, ry)
                        float ry = Trackers.Y[j] + ((wheely[k] - Trackers.Y[j]) * UMath.Cos(pAngle) - (wheelz[k] - Trackers.Z[j]) * UMath.Sin(pAngle));
                        float rz = Trackers.Z[j] + ((wheely[k] - Trackers.Y[j]) * UMath.Sin(pAngle) + (wheelz[k] - Trackers.Z[j]) * UMath.Cos(pAngle));

                        // commenting this whole if and its body out makes us phase through ramps
                        // making this always true makes us snap to ramps even when we are airborne
                        // on two way ramps and high low ramps for example
                        // this logic surely is not correct, it's just some random nonsense that happens
                        // to work for the limited set of trackpieces that we currently have

                        // upon further inspection, it makes sense we check that rz > Trackers.Z
                        // because further down, we do scy -= (rz - Trackers.Z), implying
                        // rz - Trackers.Z should be positive in order to lift the other,
                        // otherwise, we would drag it downwards instead
                        // I don't understand why the second check against z + 200 is here
                        // I tried removing it and I don't see a difference
                        if (rz > Trackers.Z[j] /*&& rz < Trackers.Z[j] + 200*/)
                        {
                            float maxZy = 50;
                            float liftDivider = 1.0F + (maxZy - Math.Abs(Trackers.Zy[j])) / 30.0F;
                            liftDivider = Math.Max(liftDivider, 1.0F); // this implies we shouldn't make ramps with surfaces steeper than 50
                            Scy[k] -= (rz - Trackers.Z[j]) / liftDivider; // this is what actually causes us to lift off
                            rz = Trackers.Z[j];
                        }

                        // this if also smells like shit
                        // it doesn't make sense that this should be the condition for this code
                        // worth noting that if we hit the previous if statement, we are guaranteed to hit this one
                        // because the previous block sets rz = Trackers.Z
                        // still, removing this causes stunts to lock up sometimes after taking a ramp
                        if (rz > Trackers.Z[j] - 30)
                        {
                            // could probably change this to something cleaner
                            // we could use something other than the surface type to decide if all 4 wheels are on
                            // the same piece
                            // (this is later used to decide if we should be driving or sliding)
                            if (Trackers.Skd[j] == 2)
                                nWheelsDirtRamp++;
                            else
                                nWheelsRoadRamp++;

                            Wtouch = true;
                            Gtouch = false;

                            // sparks and scrapes
                            if (BadLanding && (Trackers.Skd[j] == 0 || Trackers.Skd[j] == 1))
                            {
                                // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1);
                                //if (this.im == this.xt.im)
                                //    this.xt.gscrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                            }

                            // dust
                            if (!wasMtouch && surfaceType != 0)
                            {
                                float f_73 = 1.4F;
                                // conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_73 * CarDefine.Simag[Cn], 0, BadLanding && Mtouch);
                            }
                        }

                        // interestingly if ry and rz are what they were on initialization, these assignments do nothing
                        // it seems the intention with this whole block was to rotate the t -> w vector, manipulate it
                        // then rotate back. If the surface being intersected is not ramping us up, then no changes are
                        // made to the vector and the rotation back just reverses the previous operation, making no changes
                        wheely[k] = Trackers.Y[j] + ((ry - Trackers.Y[j]) * UMath.Cos(-pAngle) - (rz - Trackers.Z[j]) * UMath.Sin(-pAngle));
                        wheelz[k] = Trackers.Z[j] + ((ry - Trackers.Y[j]) * UMath.Sin(-pAngle) + (rz - Trackers.Z[j]) * UMath.Cos(-pAngle));

                        isWheelTouchingPiece[k] = true;
                    } // CHK9
                    if (Trackers.Xy[j] != 0 && Trackers.Xy[j] != 90 && Trackers.Xy[j] != -90)
                    {
                        int pAngle = 90 + Trackers.Xy[j];

                        float ry = Trackers.Y[j] + ((wheely[k] - Trackers.Y[j]) * UMath.Cos(pAngle) - (wheelx[k] - Trackers.X[j]) * UMath.Sin(pAngle));
                        float rx = Trackers.X[j] + ((wheely[k] - Trackers.Y[j]) * UMath.Sin(pAngle) + (wheelx[k] - Trackers.X[j]) * UMath.Cos(pAngle));
                        if (rx > Trackers.X[j] /*&& rx < Trackers.X[j] + 200*/)
                        {
                            float maxXy = 50;
                            float liftDivider = 1.0F + (maxXy - Math.Abs(Trackers.Xy[j])) / 30.0F;
                            liftDivider = Math.Max(liftDivider, 1.0F);
                            Scy[k] -= (rx - Trackers.X[j]) / liftDivider;
                            rx = Trackers.X[j];
                        }
                        if (rx > Trackers.X[j] - 30)
                        {
                            if (Trackers.Skd[j] == 2)
                                nWheelsDirtRamp++;
                            else
                                nWheelsRoadRamp++;

                            Wtouch = true;
                            Gtouch = false;

                            if (BadLanding && (Trackers.Skd[j] == 0 || Trackers.Skd[j] == 1))
                            {
                                // conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1);
                                //if (this.im == this.xt.im)
                                //    this.xt.gscrape((int)Scx[k], (int)Scy[k], (int)Scz[k]);
                            }

                            if (!wasMtouch && surfaceType != 0)
                            {
                                float f_78 = 1.4F;
                                // conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_78 * CarDefine.Simag[Cn], 0, BadLanding && Mtouch);
                            }
                        }

                        wheely[k] = Trackers.Y[j] + ((ry - Trackers.Y[j]) * UMath.Cos(-pAngle) - (rx - Trackers.X[j]) * UMath.Sin(-pAngle));
                        wheelx[k] = Trackers.X[j] + ((ry - Trackers.Y[j]) * UMath.Sin(-pAngle) + (rx - Trackers.X[j]) * UMath.Cos(-pAngle));

                        isWheelTouchingPiece[k] = true;
                    }
                }
            }
        }

        // CHK10
        // if all wheels are on the same style of surface, then we have grip on ramps
        if (nWheelsDirtRamp == 4 /* nwheels */ || nWheelsRoadRamp == 4 /* nwheels */)
            Mtouch = true;
    }

    private int Regx(int i, float f, ContO conto)
    {
        conto.DamageX(Stat, i, f);

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
            Shakedam = (int)((Math.Abs(f) + Shakedam) / 2.0F);
            /*if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, 0);
            }*/
            for (var i111 = 0; i111 < 40; i111++)
            {
                var f112 = 0.0F;
                for (var i113 = 0; i113 < 4; i113++)
                {
                    f112 = f / 20.0F * UMath.Random();
                    if (abool)
                    {
                        Hitmag += (int)Math.Abs(f112);
                        i110 += (int)Math.Abs(f112);
                    }
                }
            }
        }
        return i110;
    }

    private int Regy(int i, float f, ContO conto)
    {
        conto.DamageY(Stat, i, f, Mtouch, _nbsq, Squash);
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
                Shakedam = (int)((Math.Abs(f) + Shakedam) / 2.0F);
            }
            /*
            if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, i99 * i98);
            }*/
            if (i99 * i98 == 0 || Mtouch)
            {
                for (var i102 = 0; i102 < 40; i102++)
                {
                    var f103 = 0.0F;
                    for (var i104 = 0; i104 < 4; i104++)
                    {
                        f103 = f / 20.0F * UMath.Random();
                        if (abool)
                        {
                            Hitmag += (int)Math.Abs(f103);
                            i97 += (int)Math.Abs(f103);
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
                    for (var i107 = 0; i107 < 40; i107++)
                    {
                        var f108 = 0.0F;
                        for (var i109 = 0; i109 < 4; i109++)
                        {
                            f108 = f / 15.0F * UMath.Random();
                            i105 += (int)f108;
                            i106++;
                            if (abool)
                            {
                                Hitmag += (int)Math.Abs(f108);
                                i97 += (int)Math.Abs(f108);
                            }
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
        conto.DamageZ(Stat, i, f);
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
            Shakedam = (int)((Math.Abs(f) + Shakedam) / 2.0F);
            /*
            if (Im == XTGraphics.Im || _colidim)
            {
                XTGraphics.Acrash(Im, f, 0);
            }*/
            for (var i115 = 0; i115 < 40; i115++)
            {
                var f116 = 0.0F;
                for (var i117 = 0; i117 < 4; i117++)
                {
                    f116 = f / 20.0F * UMath.Random();
                    if (abool)
                    {
                        Hitmag += (int)Math.Abs(f116);
                        i114 += (int)Math.Abs(f116);
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
        _forca = ((float)Math.Sqrt(conto.Keyz[0] * conto.Keyz[0] + conto.Keyx[0] * conto.Keyx[0]) +
                  (float)Math.Sqrt(conto.Keyz[1] * conto.Keyz[1] + conto.Keyx[1] * conto.Keyx[1]) +
                  (float)Math.Sqrt(conto.Keyz[2] * conto.Keyz[2] + conto.Keyx[2] * conto.Keyx[2]) +
                  (float)Math.Sqrt(conto.Keyz[3] * conto.Keyz[3] + conto.Keyx[3] * conto.Keyx[3])) / 10000.0F *
                 (float)(Stat.Bounce - 0.3);
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
        Wasted = false;
        Newcar = false;
        if (/*Im == XTGraphics.Im*/Im == 0)
        {
            // Medium.Checkpoint = -1;
            // Medium.Lastcheck = false;
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

    private static int Rpy(float x1, float x2, float y1, float y2, float z1, float z2)
    {
        return (int)((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2) + (z1 - z2) * (z1 - z2));
    }

    public static float Hypot3(float x, float y, float z) {
        return float.Sqrt(x * x + y * y + z * z);
    }

    public static float dAcos(float a) {
        return float.Acos(a) / 2 / MathF.PI * 360;
    }

    public static float dAtan2(float y, float x) {
        return float.Atan2(y, x) / 2 / MathF.PI * 360;
    }
    
    public static float QuantizeTowardsZero(float value, float step)
    {
        // Scale by step size
        float scaled = value / step;

        // Truncate towards zero
        float truncated = (float)(scaled > 0 ? Math.Floor(scaled) : Math.Ceiling(scaled));

        // Scale back
        return truncated * step;
    }
}