using System.Runtime.CompilerServices;
using FixedMathSharp.Utility;
using NFMWorldLibrary.Collision;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

/*************************************
 *
 *************************************
 *
 * IF YOU CHANGE ANYTHING HERE RELATING TO PHYSICS, MAKE SURE TO UPDATE THE VERSION NUMBER OF SAVED DEMOS
 * AS ALL DEMOS WILL DESYNC IF THE COLLISIONS OR PHYSICS ARE UPDATED.
 *
 *************************************
 *
**************************************/

namespace NFMWorldLibrary;

public class Mad
{
    private static readonly fix64 _tickRate = Physics.PHYSICS_MULTIPLIER_F64;
    public Boolean Halted = false;

    public event EventHandler<(float f, int i)> SfxPlayCrash;
    public event EventHandler<(int i, float f)> SfxPlaySkid;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayScrape;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayGscrape;
    public event EventHandler<float> PowerUp;

    public bool Btab;
    public int Capcnt;
    public bool BadLanding;
    public readonly UnlimitedArray<bool> _caught = [];
    public CarStats Stat;
    public int Cn;
    public int Cntdest;
    public int _cntouch;
    
    /// <summary>
    /// Is colliding with the client player car
    /// </summary>
    public bool _colidim;
    public readonly int[,] _crank = new int[4, 4];
    public readonly int[,] _lcrank = new int[4, 4];
    public fix64 Cxz;
    public int _dcnt;
    public fix64 Dcomp;
    public bool Wasted;
    public readonly UnlimitedArray<bool> _dominate = [];
    public readonly fix64 _drag = (fix64)(0.5F);
    public int _fixes = -1;
    public fix64 _forca;
    public bool Ftab;
    public fix64 _fxz;
    public bool Gtouch;
    public int Hitmag;
    public int Im;
    public int Lastcolido;
    public fix64 Lcomp;
    public sbyte Loop;
    public fix64 _lxz;
    public bool Mtouch;
    public fix64 Mxz;
    public int _nbsq;
    public bool Newcar;
    public int Newedcar;
    public int _nmlt = 1;
    public bool Nofocus;
    public int Outshakedam = 0;
    public bool Pd;
    public bool Pl;
    public int _pmlt = 1;
    public int Point;
    public fix64 Power = 75;
    public fix64 Powerup;
    public bool Pr;
    public bool Pu;
    public bool Pushed;

    public fix64 Pxy;
    public fix64 Pzy;
    public fix64 Rcomp;
    public bool Rtab;
    public InlineArray4<fix64> Scx;
    public InlineArray4<fix64> Scy;
    public InlineArray4<fix64> Scz;
    public int Shakedam;
    public sbyte Skid;
    public fix64 Speed;
    public int Squash;
    public int _srfcnt;
    public bool Surfer;
    public fix64 _tilt;
    public fix64 Travxy;
    public fix64 Travxz;
    public fix64 Travzy;
    public int Trcnt;
    public fix64 Txz;
    public fix64 Ucomp;
    public bool Wtouch;
    public int _xtpower;

    internal bool IsClientPlayer;

    public Mad(CarStats stat, int im, bool isClientPlayer)
    {
        Stat = stat;
        Im = im;
        IsClientPlayer = isClientPlayer;
    }

    public void SetStat(CarStats stat)
    {
        Stat = stat;
    }

    public bool pointInBox(fix64 px, fix64 py, fix64 pz, fix64 bx, fix64 by, fix64 bz, fix64 szx, fix64 szy, fix64 szz)
    {
        return px > bx - szx && px < bx + szx && pz > bz - szz && pz < bz + szz && py > by - szy && py < by + (szy == fix64.Zero ? 100 : szy);
    }

    public void Colide(ContO conto, Mad othermad, ContO otherconto)
    {
        var random = new DeterministicRandom((ulong)(conto.X.Value.m_rawValue ^ otherconto.X.Value.m_rawValue ^ conto.Z.Value.m_rawValue ^ otherconto.Z.Value.m_rawValue ^ conto.Y.Value.m_rawValue ^ otherconto.Y.Value.m_rawValue));
        
        var wheelx = new InlineArray4<fix64>();
        var wheely = new InlineArray4<fix64>();
        var wheelz = new InlineArray4<fix64>();
        var otherwheelx = new InlineArray4<fix64>();
        var otherwheely = new InlineArray4<fix64>();
        var otherwheelz = new InlineArray4<fix64>();
        
        // No hypergliding fixes are needed here because this is only called during collisions
        // however we need this code or else sparks will come out of the wrong place
        var bottomy = GetBottomY(this, conto);
        var otherbottomy = GetBottomY(othermad, otherconto);

        var wheelGround = GetWheelGround(this, conto, bottomy);
        var otherWheelGround = GetWheelGround(othermad, otherconto, otherbottomy);

        for (var i1 = 0; i1 < 4; i1++)
        {
            wheelx[i1] = conto.X + conto.Keyx[i1];
            wheely[i1] = conto.Y + bottomy;
            wheelz[i1] = conto.Z + conto.Keyz[i1];
            otherwheelx[i1] = otherconto.X + otherconto.Keyx[i1];
            otherwheely[i1] = otherconto.Y + bottomy;
            otherwheelz[i1] = otherconto.Z + otherconto.Keyz[i1];
        }

        UMath.Rot(wheelx, wheely, conto.X, conto.Y, conto.Xy, 4);
        UMath.Rot(wheely, wheelz, conto.Y, conto.Z, conto.Zy, 4);
        UMath.Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        UMath.Rot(otherwheelx, otherwheely, otherconto.X, otherconto.Y, otherconto.Xy, 4);
        UMath.Rot(otherwheely, otherwheelz, otherconto.Y, otherconto.Z, otherconto.Zy, 4);
        UMath.Rot(otherwheelx, otherwheelz, otherconto.X, otherconto.Z, otherconto.Xz, 4);
        if (UMath.Rpy(conto.X, otherconto.X, conto.Y, otherconto.Y, conto.Z, otherconto.Z) <
            (conto.MaxR * conto.MaxR + otherconto.MaxR * otherconto.MaxR) * (fix64)1.5f)
        {
            if (!_caught[othermad.Im] && (Speed != 0 || othermad.Speed != 0))
            {
                if (fix64.Abs(fix64.Abs(Power * Speed * Stat.Moment) - fix64.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment)) > (fix64)0.001f)
                {
                    _dominate[othermad.Im] = fix64.Abs(Power * Speed * Stat.Moment) > fix64.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment);
                }
                else
                {
                    _dominate[othermad.Im] = Stat.Moment > othermad.Stat.Moment;
                }

                _caught[othermad.Im] = true;
            }
        }
        else if (_caught[othermad.Im])
        {
            _caught[othermad.Im] = false;
        }
        var totalOtherDamage = 0;
        var totalOwnDamage = 0;
        if (_dominate[othermad.Im])
        {
            var impactMagnitude =
                (int) ((
                    (Scz[0] - othermad.Scz[0] + Scz[1] - othermad.Scz[1] + Scz[2] - othermad.Scz[2] + Scz[3] - othermad.Scz[3]) *
                    (Scz[0] - othermad.Scz[0] + Scz[1] - othermad.Scz[1] + Scz[2] - othermad.Scz[2] + Scz[3] - othermad.Scz[3]) +
                    (Scx[0] - othermad.Scx[0] + Scx[1] - othermad.Scx[1] + Scx[2] - othermad.Scx[2] + Scx[3] - othermad.Scx[3]) *
                    (Scx[0] - othermad.Scx[0] + Scx[1] - othermad.Scx[1] + Scx[2] - othermad.Scx[2] + Scx[3] - othermad.Scx[3])
                ) / 16);
            var impactExtraRdius = 7000;
            fix64 damageMult = 1;
            if (World.UseMultiplayerCollisionModifiers)
            {
                impactExtraRdius = 28000;
                damageMult = (fix64)1.27F;
            }
            for (var wheel = 0; wheel < 4; wheel++)
            {
                for (var otherwheel = 0; otherwheel < 4; otherwheel++)
                {
                    if (UMath.Rpy(wheelx[wheel], otherwheelx[otherwheel], wheely[wheel], otherwheely[otherwheel], wheelz[wheel], otherwheelz[otherwheel]) <
                        (impactMagnitude + impactExtraRdius) * (othermad.Stat.Comprad + Stat.Comprad))
                    {
                        if (fix64.Abs(Scx[wheel] * Stat.Moment) > fix64.Abs(othermad.Scx[otherwheel] * othermad.Stat.Moment))
                        {
                            var f130 = othermad.Scx[otherwheel] * Stat.Revpush;
                            if (f130 > 300)
                            {
                                f130 = 300;
                            }
                            if (f130 < (fix64)(-300.0F))
                            {
                                f130 = (fix64)(-300.0F);
                            }
                            var f131 = Scx[wheel] * Stat.Push;
                            if (f131 > 300)
                            {
                                f131 = 300;
                            }
                            if (f131 < (fix64)(-300.0F))
                            {
                                f131 = (fix64)(-300.0F);
                            }
                            othermad.Scx[otherwheel] += f131;
                            if (IsClientPlayer)
                            {
                                othermad._colidim = true;
                            }
                            totalOtherDamage += othermad.Regx(otherwheel, f131 * Stat.Moment * damageMult, otherconto, random);
                            if (othermad._colidim)
                            {
                                othermad._colidim = false;
                            }
                            Scx[wheel] -= f130;
                            totalOwnDamage += Regx(wheel, -f130 * Stat.Moment * damageMult, conto, random);
                            Scy[wheel] -= Stat.Revlift;
                            if (IsClientPlayer)
                            {
                                othermad._colidim = true;
                            }
                            totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
                            if (othermad._colidim)
                            {
                                othermad._colidim = false;
                            }
                            if (UMath.RandomBoolean())
                            {
                                otherconto.Spark(
                                    (wheelx[wheel] + otherwheelx[otherwheel]) / 2, 
                                    (wheely[wheel] + otherwheely[otherwheel]) / 2,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) / 2, 
                                    (othermad.Scx[otherwheel] + Scx[wheel]) / 4,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) / 4,
                                    (othermad.Scz[otherwheel] + Scz[wheel]) / 4,
                                    2,
                                    (wheelGround + otherWheelGround) / 2
                                );
                            }
                        }
                        if (fix64.Abs(Scz[wheel] * Stat.Moment) > fix64.Abs(othermad.Scz[otherwheel] * othermad.Stat.Moment))
                        {
                            var f132 = othermad.Scz[otherwheel] * Stat.Revpush;
                            if (f132 > 300)
                            {
                                f132 = 300;
                            }
                            if (f132 < (fix64)(-300.0F))
                            {
                                f132 = (fix64)(-300.0F);
                            }
                            var f133 = Scz[wheel] * Stat.Push;
                            if (f133 > 300)
                            {
                                f133 = 300;
                            }
                            if (f133 < (fix64)(-300.0F))
                            {
                                f133 = (fix64)(-300.0F);
                            }
                            othermad.Scz[otherwheel] += f133;
                            if (IsClientPlayer)
                            {
                                othermad._colidim = true;
                            }
                            totalOtherDamage += othermad.Regz(otherwheel, f133 * Stat.Moment * damageMult, otherconto, random);
                            if (othermad._colidim)
                            {
                                othermad._colidim = false;
                            }
                            Scz[wheel] -= f132;
                            totalOwnDamage += Regz(wheel, -f132 * Stat.Moment * damageMult, conto, random);
                            Scy[wheel] -= Stat.Revlift;
                            if (IsClientPlayer)
                            {
                                othermad._colidim = true;
                            }
                            totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
                            if (othermad._colidim)
                            {
                                othermad._colidim = false;
                            }
                            if (UMath.RandomBoolean())
                            {
                                otherconto.Spark(
                                    (wheelx[wheel] + otherwheelx[otherwheel]) / 2, 
                                    (wheely[wheel] + otherwheely[otherwheel]) / 2,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) / 2,
                                    (othermad.Scx[otherwheel] + Scx[wheel]) / 4,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) / 4, 
                                    (othermad.Scz[otherwheel] + Scz[wheel]) / 4,
                                    2,
                                    (wheelGround + otherWheelGround) / 2);
                            }
                        }
                        if (IsClientPlayer)
                        {
                            othermad.Lastcolido = 70;
                        }
                        if (othermad.IsClientPlayer)
                        {
                            Lastcolido = 70;
                        }
                        othermad.Scy[otherwheel] -= Stat.Lift;
                    }
                }
            }
        }
        // if (XTGraphics.Multion == 1)
        // {
        //     if (othermad.Im == XTGraphics.Im && i != 0)
        //     {
        //         XTGraphics.Dcrashes[Im] += i;
        //     }
        //     if (Im == XTGraphics.Im && i125 != 0)
        //     {
        //         XTGraphics.Dcrashes[othermad.Im] += i125;
        //     }
        // }
    }

    private static int GetWheelGround(Mad mad, ContO conto, fix64 bottomy)
    {
        int wheelGround;
        if (World.IsHyperglidingEnabled)
        {
            wheelGround = (int)((bottomy * (fix64)1f / _tickRate) * ((fix64)1f - _tickRate));
            if (!mad.BadLanding)
            {
                wheelGround = -wheelGround;
            }
        }
        else
        {
            wheelGround = mad.BadLanding ? mad.Stat.Flipy + mad.Squash : -conto.Grat;
        }

        return wheelGround;
    }

    private static fix64 GetBottomY(Mad mad, ContO conto)
    {
        fix64 bottomy;
        if (World.IsHyperglidingEnabled)
        {
            if (mad.BadLanding)
            {
                bottomy = (mad.Stat.Flipy + mad.Squash) * _tickRate;
            }
            else
            {
                bottomy = conto.Grat * _tickRate;
            }
        }
        else
        {
            bottomy = 0;
        }

        return bottomy;
    }

    public event EventHandler Distruct;

    public void bounceRebound(int wi, ContO conto, DeterministicRandom random)
    {
        // part 1: the closer we are to 90/-90 in Pxy or Pzy, the bigger the bounce
        fix64 rebound = (fix64.Abs(UMath.Sin(Pxy)) + fix64.Abs(UMath.Sin(Pzy))) / (fix64)3;
        fix64 maxAngleRebound = (fix64)(0.4F); // capping at 0.4 doesn't do much, max is two thirds
        rebound = fix64.Min(rebound, maxAngleRebound);

        // part 2: the bigger the bounce stat, the bigger the bounce
        rebound += Stat.Bounce;
        fix64 minRebound = (fix64)(1.1F);
        rebound = fix64.Max(rebound, minRebound);

        Regy(wi, fix64.Abs(Scy[wi] * rebound), conto, random);
        // if scy is > 0 then we are going down, apply the rebound bounce
        if (Scy[wi] > 0)
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
            // Scy[wi] -= fix64.Abs(Scy[wi] * rebound);
            // Scy[wi] -= Scy[wi] * rebound; // don't need the abs, both are always positive
            Scy[wi] = (fix64)(-1) * Scy[wi] * (rebound - (fix64)1);
    }
    internal int Mtcount = 0;
    internal fix64 py = 0;

    public void Drive(Control control, ContO conto, IStage stage)
    {
        DeterministicRandom random = new((ulong)(conto.X.Value.m_rawValue ^ conto.Y.Value.m_rawValue ^ conto.Z.Value.m_rawValue));

        FrameTrace.AddMessage($"xz: {conto.Xz:0.00}, mxz: {Mxz:0.00}, lxz: {_lxz:0.00}, fxz: {_fxz:0.00}, cxz: {Cxz:0.00}");
        FrameTrace.AddMessage($"xy: {conto.Xy:0.00}, pxy: {Pxy:0.00}, zy: {conto.Zy:0.00}, pzy: {Pzy:0.00}");
        FrameTrace.AddMessage($"Travxz: {Travxz:0.00}, Travxy: {Travxy:0.00}, Travzy: {Travzy:0.00}, Surfing: {Surfer}");

        var xneg = 1;
        var zneg = 1;
        var zyinv = false;
        var revspeed = false;
        var hitVertical = false;
        BadLanding = false;
        if (!Mtouch) Mtcount++; //DS-addons: Bad landing hotfix
        fix64 zyangle;
        for (zyangle = fix64.Abs(Pzy); zyangle > 360; zyangle -= 360)
        {
            /* empty */
        }

        fix64 xyangle;
        for (xyangle = fix64.Abs(Pxy); xyangle > 360; xyangle -= 360)
        {
            /* empty */
        }

        fix64 zy;
        for (zy = fix64.Abs(Pzy); zy > 270; zy -= 360)
        {
        }

        zy = fix64.Abs(zy);
        if (zy > 90)
        {
            zyinv = true;
        }

        var xyinv = false;
        fix64 xy;
        for (xy = fix64.Abs(Pxy); xy > 270; xy -= 360)
        {
        }

        xy = fix64.Abs(xy);
        if (xy > 90)
        {
            xyinv = true;
            zneg = -1;
        }


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

        // maxine: this controls hypergliding. to fix hypergliding, set to 0, then update wheelGround to prevent
        // car getting stuck in the ground
        // we multiply it by tickrate because the effect caused by hypergliding is applied every tick
        fix64 bottomy = GetBottomY(this, conto);

        control.Zyinv = zyinv;
        //

        fix64 airx = 0;
        fix64 airz = 0;
        fix64 airy = 0;
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

            Ucomp = 0;
            Dcomp = 0;
            Lcomp = 0;
            Rcomp = 0;
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
            var f13 = (Scy[0] + Scy[1] + Scy[2] + Scy[3]) / 4;
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
                    if (Ucomp == 0)
                    {
                        Ucomp = 10 + (Scy[0] + 50) / 20;
                        if (Ucomp < 5)
                        {
                            Ucomp = 5;
                        }

                        if (Ucomp > 10)
                        {
                            Ucomp = 10;
                        }

                        Ucomp *= Stat.Airs;
                    }

                    if (Ucomp < 20)
                    {
                        Ucomp += (fix64)0.5f * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Sin(conto.Xz) * zneg * _tickRate;
                    airz = Stat.Airc * UMath.Cos(conto.Xz) * zneg * _tickRate;
                }
                else if (Ucomp != 0 && Ucomp > -2)
                {
                    Ucomp -= (fix64)0.5f * Stat.Airs * _tickRate; //
                }

                if (control.Down)
                {
                    if (Dcomp == 0)
                    {
                        Dcomp = 10 + (Scy[0] + 50) / 20;
                        if (Dcomp < 5)
                        {
                            Dcomp = 5;
                        }

                        if (Dcomp > 10)
                        {
                            Dcomp = 10;
                        }

                        Dcomp *= Stat.Airs;
                    }

                    if (Dcomp < 20)
                    {
                        Dcomp += (fix64)0.5f * Stat.Airs * _tickRate; //
                    }

                    airy = -Stat.Airc * _tickRate;
                }
                else if (Dcomp != 0 && Ucomp > -2)
                {
                    Dcomp -= (fix64)0.5f * Stat.Airs * _tickRate;
                } //

                if (control.Left)
                {
                    if (Lcomp == 0)
                    {
                        Lcomp = 5;
                    }

                    if (Lcomp < 20) // maxine: scale to tickrate
                    {
                        Lcomp += 2 * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = -Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Lcomp > 0)
                {
                    Lcomp -= 2 * Stat.Airs * _tickRate; //
                }

                if (control.Right) //
                {
                    if (Rcomp == 0)
                    {
                        Rcomp = 5;
                    }

                    if (Rcomp < 20) // maxine: scale to tickrate
                    {
                        Rcomp += 2 * Stat.Airs * _tickRate;
                    }

                    airx = Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Rcomp > 0) //
                {
                    Rcomp -= 2 * Stat.Airs * _tickRate;
                }

                Pzy = UMath.QuantizeTowardsZero((Pzy + (Dcomp - Ucomp) * UMath.Cos(Pxy) * _tickRate), _tickRate); //
                if (zyinv)
                {
                    conto.Xz = UMath.QuantizeTowardsZero(conto.Xz + ((Dcomp - Ucomp) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }
                else
                {
                    conto.Xz = UMath.QuantizeTowardsZero(conto.Xz - ((Dcomp - Ucomp) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }

                Pxy = UMath.QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
            }
            else
            {
                //
                var f15 = Power;
                if (f15 < 40)
                {
                    f15 = 40;
                }

                if (control.Down)
                {
                    if (Speed > 0)
                    {
                        Speed -= Stat.Handb / 2 * _tickRate;
                    }
                    else
                    {
                        var i16 = 0;
                        for (var i17 = 0; i17 < 2; i17++)
                        {
                            if (Speed <= -(Stat.Swits[i17] / 2 + f15 * Stat.Swits[i17] / 196))
                            {
                                i16++;
                            }
                        }

                        if (i16 != 2)
                        {
                            //
                            Speed -= (Stat.Acelf.AsSpan()[i16] / 2 + f15 * Stat.Acelf.AsSpan()[i16] / 196) * _tickRate;
                        }
                        else
                        {
                            Speed = -(Stat.Swits[1] / 2 + f15 * Stat.Swits[1] / 196);
                        }
                    }
                }

                if (control.Up)
                {
                    if (Speed < 0) //
                    {
                        Speed += Stat.Handb * _tickRate;
                    }
                    else
                    {
                        var i18 = 0;
                        for (var i19 = 0; i19 < 3; i19++)
                        {
                            if (Speed >= Stat.Swits[i19] / 2 + f15 * Stat.Swits[i19] / 196)
                            {
                                i18++;
                            }
                        }

                        if (i18 != 3)
                        {
                            Speed += (Stat.Acelf.AsSpan()[i18] / 2 + f15 * Stat.Acelf.AsSpan()[i18] / 196) * _tickRate;
                        }
                        else
                        {
                            Speed = Stat.Swits[2] / 2 + f15 * Stat.Swits[2] / 196;
                        }
                    }
                } //

                if (control.Handb && fix64.Abs(Speed) > Stat.Handb)
                {
                    if (Speed < 0)
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
                            if (Lcomp == 0)
                            {
                                Lcomp = 5 * Stat.Airs * _tickRate;
                            }

                            if (Lcomp < 20)
                            {
                                Lcomp += 2 * Stat.Airs * _tickRate;
                            }
                        }
                    } //
                    else
                    {
                        if (Lcomp > 0)
                        {
                            Lcomp -= 2 * Stat.Airs * _tickRate;
                        }

                        Pl = false;
                    } //

                    if (control.Right)
                    {
                        if (!Pr)
                        {
                            if (Rcomp == 0)
                            {
                                Rcomp = 5 * Stat.Airs * _tickRate;
                            }

                            if (Rcomp < 20)
                            {
                                Rcomp += 2 * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Rcomp > 0)
                        {
                            Rcomp -= 2 * Stat.Airs * _tickRate;
                        }

                        Pr = false;
                    } //

                    if (control.Up)
                    {
                        if (!Pu)
                        {
                            if (Ucomp == 0)
                            {
                                Ucomp = 5 * Stat.Airs * _tickRate;
                            }

                            if (Ucomp < 20)
                            {
                                Ucomp += 2 * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Ucomp > 0)
                        {
                            Ucomp -= 2 * Stat.Airs * _tickRate;
                        }

                        Pu = false;
                    }

                    if (control.Down)
                    {
                        if (!Pd)
                        {
                            if (Dcomp == 0)
                            {
                                Dcomp = 5 * Stat.Airs * _tickRate;
                            }

                            if (Dcomp < 20)
                            {
                                Dcomp += 2 * Stat.Airs * _tickRate;
                            }
                        }
                    }
                    else
                    {
                        if (Dcomp > 0)
                        {
                            Dcomp -= 2 * Stat.Airs * _tickRate;
                        }

                        Pd = false;
                    }

                    Pzy = UMath.QuantizeTowardsZero((Pzy + ((Dcomp - Ucomp) * UMath.Cos(Pxy)) * _tickRate), _tickRate);
                    if (zyinv)
                    {
                        conto.Xz = UMath.QuantizeTowardsZero(conto.Xz + (((Dcomp - Ucomp) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }
                    else
                    {
                        conto.Xz = UMath.QuantizeTowardsZero(conto.Xz - (((Dcomp - Ucomp) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }

                    Pxy = UMath.QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
                }
            }
        }

        var f20 = 20 * Speed / (154 * Stat.Simag);
        if (f20 > 20)
        {
            f20 = 20;
        }

        conto.Wzy -= (f20 * _tickRate); // maxine: remove int cast. i dont think it belongs here
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
            conto.Wxz -= ((fix64)Stat.Turn * _tickRate);
            if (conto.Wxz < -36)
            {
                conto.Wxz = -36;
            }
        }

        if (control.Left)
        {
            conto.Wxz += ((fix64)Stat.Turn * _tickRate);
            if (conto.Wxz > 36)
            {
                conto.Wxz = 36;
            }
        } //

        if (conto.Wxz != 0 && !control.Left && !control.Right)
        {
            if (fix64.Abs(Speed) < 10)
            {
                if (fix64.Abs(conto.Wxz) == 1)
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
                if (fix64.Abs(conto.Wxz) < Stat.Turn * 2)
                {
                    conto.Wxz = 0;
                }

                if (conto.Wxz > 0)
                {
                    conto.Wxz -= ((fix64)Stat.Turn * 2 * _tickRate);
                }

                if (conto.Wxz < 0)
                {
                    conto.Wxz += ((fix64)Stat.Turn * 2 * _tickRate);
                }
            }
        } //

        var i21 = Speed != 0 ? (int)(3600 / (Speed * Speed)) : int.MaxValue;
        if (i21 < 5)
        {
            i21 = 5;
        }

        if (Speed < 0)
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

                conto.Xz += (conto.Wxz / i21 * _tickRate);
            }

            Wtouch = false;
            Gtouch = false;
        }
        else
        {
            conto.Xz += (_fxz * _tickRate);
        } //

        if (Speed > 30 || Speed < -100)
        {
            while (UMath.SafeAbs(Mxz - Cxz) > 180)
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
            if (UMath.SafeAbs(Mxz - Cxz) < 30)
            {
                Cxz += (Mxz - Cxz) / 4 * _tickRate; //
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


        var wheelx = new InlineArray4<fix64>();
        var wheelz = new InlineArray4<fix64>();
        var wheely = new InlineArray4<fix64>();
        for (var i24 = 0; i24 < 4; i24++)
        {
            wheelx[i24] = conto.Keyx[i24] + conto.X;
            wheely[i24] = bottomy + conto.Y;
            wheelz[i24] = conto.Z + conto.Keyz[i24];
            Scy[i24] += 7 * _tickRate;
        }

        UMath.Rot(wheelx, wheely, conto.X, conto.Y, Pxy, 4);
        UMath.Rot(wheely, wheelz, conto.Y, conto.Z, Pzy, 4);
        UMath.Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        var wasMtouch = false;
        var i26 = ((Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4);
        var i27 = ((Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4);
        for (var wheelid = 0; wheelid < 4; wheelid++)
        {
            if (Scx[wheelid] - i26 > 200)
            {
                Scx[wheelid] = 200 + i26;
            }

            if (Scx[wheelid] - i26 < -200)
            {
                Scx[wheelid] = i26 - 200;
            }

            if (Scz[wheelid] - i27 > 200)
            {
                Scz[wheelid] = 200 + i27;
            }

            if (Scz[wheelid] - i27 < -200)
            {
                Scz[wheelid] = i27 - 200;
            }
            
            FrameTrace.AddMessage($"Scx[{wheelid}]: {Scx[wheelid]:0.00}, Scz[{wheelid}]: {Scz[wheelid]:0.00}, Scy[{wheelid}]: {Scy[wheelid]:0.00}");
        }

        for (var i29 = 0; i29 < 4; i29++)
        {
            wheely[i29] += Scy[i29] * _tickRate;
            wheelx[i29] += (Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4 * _tickRate;
            wheelz[i29] += (Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4 * _tickRate;
        } //

        var surfaceType = 1;
        foreach (var collidable in stage.RetrievePointCollidables(conto.X, conto.Z))
        {
            var box = collidable.Box;
            // bumps don't have rady defined so it is 0
            // the collision check that was here only checks x and z and allows y to be anything
            // this means if there is a floating road over a bumpy side road, you still hit the bumps on the road above
            // to fix this fix the bumpy side models to have some proper rady and propagate the rady value instead of 10^9
            var rad = new f64Vector3(box.Radius.X, 1000000000, box.Radius.Z);
            var trackersPosition = box.Translation;
            var contoXz = collidable.GameObjectXz;
            var contoPosition = collidable.GameObjectPosition;
            var position = new f64Vector3(conto.X, conto.Y, conto.Z);
            var theBox = new CollisionBox(rad, trackersPosition, contoXz, contoPosition);
            if (theBox.ResolveCollision(position) is not null)
            {
                surfaceType = box.Skid;
            }
        }

        // maxine: we counteract the reduced bottomy from hypergliding here
        int wheelGround = GetWheelGround(this, conto, bottomy);

        if (Mtouch)
        {
            // Jacher: 1/_tickrate for traction; Txz is set on previous tick so we need to scale
            var traction = Stat.Grip;
            traction -= fix64.Abs(Txz - conto.Xz) * (1 / _tickRate) * Speed / 250;
            if (control.Handb)
            {
                traction -= fix64.Abs(Txz - conto.Xz) * (1 / _tickRate) * 4;
            }

            if (traction < Stat.Grip)
            {
                if (Skid != 2)
                {
                    Skid = 1;
                }

                Speed -= Speed / 100 * _tickRate;
            } //
            else if (Skid == 1)
            {
                Skid = 2;
            }

            if (surfaceType == 1)
            {
                traction *= (fix64)0.75f;
            }

            if (surfaceType == 2)
            {
                traction *= (fix64)0.55f;
            }

            var speedx = -(Speed * UMath.Sin(conto.Xz) * UMath.Cos(Pzy));
            var speedz = (Speed * UMath.Cos(conto.Xz) * UMath.Cos(Pzy));
            var speedy = -(Speed * UMath.Sin(Pzy));
            if (BadLanding || Wasted || Halted)
            {
                speedx = 0;
                speedz = 0;
                speedy = 0;
                traction = Stat.Grip / 5;
                Speed -= 2 * (Speed).Sign() * _tickRate;
            } //

            if (fix64.Abs(Speed) > _drag * _tickRate)
            {
                Speed -= _drag * Speed.Sign() * _tickRate;
            }
            else
            {
                Speed = 0;
            }

            if (Cn == 8 && traction < 5)
            {
                traction = 5;
            }

            if (traction < 1)
            {
                traction = 1;
            } //

            fix64 minTraction = 1;
            traction = fix64.Max(traction, minTraction);

            for (var j = 0; j < 4; j++)
            {
                // maxine: traction fixes by Jacher. done slightly different but same result
                if (fix64.Abs(Scx[j] - speedx) > traction * _tickRate)
                {
                    Scx[j] += traction * (speedx - Scx[j]).Sign() * _tickRate;
                }
                else
                {
                    Scx[j] = speedx;
                }

                if (fix64.Abs(Scz[j] - speedz) > traction * _tickRate)
                {
                    Scz[j] += traction * (speedz - Scz[j]).Sign() * _tickRate;
                }
                else
                {
                    Scz[j] = speedz;
                }

                if (fix64.Abs(Scy[j] - speedy) > traction * _tickRate)
                {
                    // Jacher: decouple this from tickrate
                    // this reduces bouncing when AB-ing, but at what cost?
                    // oteek: if decoupled slanted ramps make car bounce for no reason for a bit
                    Scy[j] += traction * (speedy - Scy[j]).Sign() * _tickRate;
                }
                else
                {
                    Scy[j] = speedy;
                } //

                // maxine: maybe this should be scaled to tickrate?
                if (traction < Stat.Grip)
                {
                    if (fix64.Abs(Txz - conto.Xz) > fix64.Half)
                    {
                        _dcnt++;
                    }
                    else
                    {
                        _dcnt = 0;
                    }

                    if (_dcnt > 40 * traction / Stat.Grip || BadLanding)
                    {
                        fix64 f42 = 1;
                        if (surfaceType != 0)
                        {
                            f42 = (fix64)(1.2F);
                        }

                        if (random.NextF64() > (fix64)0.65f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                f42 * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                            if ( /*Im == XTGraphics.Im &&*/ !BadLanding)
                            {
                                SfxPlaySkid(this, (surfaceType, (float)fix64.Sqrt(Scx[j] * Scx[j] + Scz[j] * Scz[j])));
                                //XTPart2.Skidf(Im, i32,
                                //    (fix64) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (surfaceType == 1 && random.NextF64() > (fix64)0.8f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                (fix64)1.1F * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                        }

                        if ((surfaceType == 2 || surfaceType == 3) && random.NextF64() > (fix64)0.6f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                (fix64)1.15F * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
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
                        k = (int)fix64.Floor(random.NextF64() * 4); // choose 4 wheels randomly to bounce up, usually some wheel will be chosen twice, which means another wheel is not chosen, causing tilt
                    fix64 bumpLift = surfaceType == 3 ? (fix64)(-100F) : (fix64)(-150F);
                    fix64 rng = (fix64)0.55F;
                    Scy[k] = bumpLift * rng * Speed / Stat.Swits[2] * (Stat.Bounce - (fix64)0.3F);
                }
            }

            Txz = conto.Xz; // CHK1

            fix64 scxsum = 0;
            fix64 sczsum = 0;
            // 4 = nwheels
            for (int j = 0; j < 4; ++j)
            {
                scxsum += Scx[j];
                sczsum += Scz[j];
            }

            fix64 scxavg = scxsum / 4; /* nwheels */
            fix64 sczavg = sczsum / 4;
            fix64 scxz = fix64.Hypot(sczavg, scxavg);

            Mxz = (int)(UMath.dAtan2(-scxsum, sczsum));

            if (Skid == 2)
            {
                if (!BadLanding)
                {
                    Speed = scxz * UMath.Cos(Mxz - conto.Xz) * (revspeed ? -1 : 1);
                }

                Skid = 0;
            }

            if (BadLanding && scxsum == 0 && sczsum == 0)
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
        fix64 groundY = 250 + wheelGround;
        fix64 wheelYThreshold = (fix64)5f;
        fix64 f48 = 0;
        for (var i49 = 0; i49 < 4; i49++)
        {
            isWheelGrounded[i49] = false;
            if (wheely[i49] > (groundY - (fix64)5f))
            {
                nGroundedWheels++;
                Wtouch = true;
                Gtouch = true;
                if (!wasMtouch && Scy[i49] != 7)
                {
                    var f50 = Scy[i49] / (fix64)(333.33F);
                    if (f50 > (fix64)(0.3F))
                    {
                        f50 = (fix64)(0.3F);
                    }

                    if (surfaceType == 0)
                    {
                        f50 += (fix64)1.1f;
                    }
                    else
                    {
                        f50 += (fix64)1.2f;
                    }

                    conto.Dust(i49, wheelx[i49], wheely[i49], wheelz[i49], (int)Scx[i49], (int)Scz[i49],
                        f50 * Stat.Simag,
                        0, BadLanding && Mtouch, wheelGround);
                } // CHK2

                wheely[i49] = groundY;
                f48 += wheely[i49] - groundY;
                isWheelGrounded[i49] = true;

                bounceRebound(i49, conto, random);
            }
        }

        // OmarTrackPieceCollision(control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, random);
        PhyTrackPieceCollision(stage, control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, random);

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
        // i_81 = d > 1 ? 0 : (fix64) dAcos(ratio) * sgn;
        // `d` was an unused double set to 0.0 and never used. GO figure.
        fix64 i_81 = 0;
        if (Scy[2] != Scy[0])
        {
            fix64 sgn = Scy[2] < Scy[0] ? -1 : 1;
            fix64 ratio = UMath.Hypot3(wheelz[0] - wheelz[2], wheely[0] - wheely[2], wheelx[0] - wheelx[2]) / (Math.Abs(conto.Keyz[0]) + Math.Abs(conto.Keyz[2]));
            i_81 = ratio >= 1 ? sgn : UMath.dAcos(ratio) * sgn; // the d > 1 ? 0 part was different in the original code, but this I think makes more sense
        }
        fix64 i_82 = 0;
        if (Scy[3] != Scy[1])
        {
            fix64 sgn = Scy[3] < Scy[1] ? -1 : 1;
            fix64 ratio = UMath.Hypot3(wheelz[1] - wheelz[3], wheely[1] - wheely[3], wheelx[1] - wheelx[3]) / (Math.Abs(conto.Keyz[1]) + Math.Abs(conto.Keyz[3]));
            i_82 = ratio >= 1 ? sgn : UMath.dAcos(ratio) * sgn;
        }
        fix64 i_83 = 0;
        if (Scy[1] != Scy[0])
        {
            fix64 sgn = Scy[1] < Scy[0] ? -1 : 1;
            fix64 ratio = UMath.Hypot3(wheelz[0] - wheelz[1], wheely[0] - wheely[1], wheelx[0] - wheelx[1]) / (Math.Abs(conto.Keyx[0]) + Math.Abs(conto.Keyx[1]));
            i_83 = ratio >= 1 ? sgn : UMath.dAcos(ratio) * sgn;
        }
        fix64 i_84 = 0;
        if (Scy[3] != Scy[2])
        {
            fix64 sgn = Scy[3] < Scy[2] ? -1 : 1;
            fix64 ratio = UMath.Hypot3(wheelz[2] - wheelz[3], wheely[2] - wheely[3], wheelx[2] - wheelx[3]) / (Math.Abs(conto.Keyx[2]) + Math.Abs(conto.Keyx[3]));
            i_84 = ratio >= 1 ? sgn : UMath.dAcos(ratio) * sgn;
        }

        if (hitVertical)
        {
            fix64 i_85;
            for (i_85 = fix64.Abs(conto.Xz + 45); i_85 > 180; i_85 -= 360) { }
            _pmlt = fix64.Abs(i_85) > 90 ? 1 : -1;
            for (i_85 = fix64.Abs(conto.Xz - 45); i_85 > 180; i_85 -= 360) { }
            _nmlt = fix64.Abs(i_85) > 90 ? 1 : -1;
        }

        // I think this line, among other things, is responsible for causing flatspins after glitching on the edge of a ramp
        conto.Xz += _tickRate * _forca * (Scz[0] * _nmlt - Scz[1] * _pmlt + Scz[2] * _pmlt - Scz[3] * _nmlt + Scx[0] * _pmlt + Scx[1] * _nmlt - Scx[2] * _nmlt - Scx[3] * _pmlt);

        // maxine: angle assist to make hypergliding easier
        if (!control.Left && !control.Right)
        {
            var assistxz = conto.Xz;
            while (assistxz < 0)
            {
                assistxz += 360;
            }

            assistxz %= 90;
            if (assistxz > (fix64)89.5f || assistxz < (fix64)0.5f)
            {
                conto.Xz = fix64.Round(conto.Xz / 90) * 90;
            }
            FrameTrace.AddMessage($"assistxz: {assistxz:0.00}, conto.Xz: {conto.Xz:0.00}");
        }

        if (fix64.Abs(i_82) > fix64.Abs(i_81))
        {
            i_81 = i_82;
        }
        if (fix64.Abs(i_84) > fix64.Abs(i_83))
        {
            i_83 = i_84;
        }

        // CHK11
        if (!Mtouch && py < 0/* && this.mtCount > 15*/)
        {
            var zeroanglezy = fix64.Min(zyangle, 360 - zyangle); //distance from 0 degrees in the zy-plane
            var flipanglezy = fix64.Abs(zyangle - 180); //distance from 180 degrees in the zy-plane
            if (zeroanglezy <= flipanglezy && zyangle < 180 || flipanglezy < zeroanglezy && zyangle >= 180) //the landing adjustment mechanism
            {
                if (Pzy > 0) //Pzy can be negative, so this needs to be accounted for
                {
                    Pzy -= UMath.QuantizeTowardsZero(fix64.Abs(i_81) * _tickRate, _tickRate);
                }
                else
                {
                    Pzy += UMath.QuantizeTowardsZero(fix64.Abs(i_81) * _tickRate, _tickRate);
                }
            }
            if (zeroanglezy <= flipanglezy && zyangle >= 180 || flipanglezy < zeroanglezy && zyangle < 180) //similar to above, just in reverse
            {
                if (Pzy > 0)
                {
                    Pzy += UMath.QuantizeTowardsZero(fix64.Abs(i_81) * _tickRate, _tickRate);
                }
                else
                {
                    Pzy -= UMath.QuantizeTowardsZero(fix64.Abs(i_81) * _tickRate, _tickRate);
                }
            }
            var zeroanglexy = fix64.Min(xyangle, 360 - xyangle); //distance from 0 degrees in the xy-plane
            var flipanglexy = fix64.Abs(xyangle - 180); //distance from 180 degrees in the xy-plane
            if (zeroanglexy <= flipanglexy && xyangle < 180 || flipanglexy < zeroanglexy && xyangle >= 180) //same as above, just for the xy-plane
            {
                if (Pxy > 0) //again, Pxy can be negative
                {
                    Pxy -= UMath.QuantizeTowardsZero(fix64.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy += UMath.QuantizeTowardsZero(fix64.Abs(i_83) * _tickRate, _tickRate);
                }
            }
            if (zeroanglexy <= flipanglexy && xyangle >= 180 || flipanglexy < zeroanglexy && xyangle < 180)
            {
                if (Pxy > 0)
                {
                    Pxy += UMath.QuantizeTowardsZero(fix64.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy -= UMath.QuantizeTowardsZero(fix64.Abs(i_83) * _tickRate, _tickRate);
                }
            }
        }
        else
        {
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
        if (nGroundedWheels == 4)
        {
            int i_86 = 0;
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
                Pzy = 180;
                conto.Zy = 180;
                i_86++;
            }
            if (Pzy > 350 || Pzy < 10)
            {
                Pzy = 0;
                conto.Zy = 0;
                i_86++;
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
                Pxy = 180;
                conto.Xy = 180;
                i_86++;
            }
            if (Pxy > 350 || Pxy < 10)
            {
                Pxy = 0;
                conto.Xy = 0;
                i_86++;
            }
            if (i_86 == 2)
            {
                Mtouch = true; //DS-addons: Bad landing hotfix
            }
        }
        if (!Mtouch && Wtouch)
        {
            if (_cntouch == 10)
            {
                Mtouch = true; //DS-addons: Bad landing hotfix
            }
            else
            {
                _cntouch++;
            }
        }
        else
            _cntouch = 0; // CHK12
                          //DS-addons: Bad landing hotfix

        fix64 newy = ((wheely[0] + wheely[1] + wheely[2] + wheely[3]) / 4 - bottomy * UMath.Cos(Pzy) * UMath.Cos(Pxy) + airy);
        py = conto.Y - newy;
        conto.Y = newy;
        //conto.y = (int) ((fs_23[0] + fs_23[1] + fs_23[2] + fs_23[3]) / 4 - (fix64) i_10 * Cos(this.Pzy) * Cos(this.Pxy) + f_12);
        //
        if (zyinv)
            xneg = -1;
        else
            xneg = 1;

        FrameTrace.AddMessage($"x: {airx:0.00}, z: {airz:0.00}, sum: {UMath.Sin(Pxy):0.00}, sum2: {UMath.Sin(Pzy):0.00}");

        // CHK13
        // car sliding fix by jacher: do not adjust to tickrate
        conto.X = ((wheelx[0] - conto.Keyx[0] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[0] * UMath.Sin(conto.Xz) +
            wheelx[1] - conto.Keyx[1] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[1] * UMath.Sin(conto.Xz) +
            wheelx[2] - conto.Keyx[2] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[2] * UMath.Sin(conto.Xz) +
            wheelx[3] - conto.Keyx[3] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[3] * UMath.Sin(conto.Xz)) / 4
            + bottomy * UMath.Sin(Pxy) * UMath.Cos(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Sin(conto.Xz) + airx);

        conto.Z = ((wheelz[0] - xneg * conto.Keyz[0] * UMath.Cos(conto.Xz) - conto.Keyx[0] * UMath.Sin(conto.Xz)
            + wheelz[1] - xneg * conto.Keyz[1] * UMath.Cos(conto.Xz) - conto.Keyx[1] * UMath.Sin(conto.Xz)
            + wheelz[2] - xneg * conto.Keyz[2] * UMath.Cos(conto.Xz) - conto.Keyx[2] * UMath.Sin(conto.Xz)
            + wheelz[3] - xneg * conto.Keyz[3] * UMath.Cos(conto.Xz) - conto.Keyx[3] * UMath.Sin(conto.Xz)) / 4
            + bottomy * UMath.Sin(Pxy) * UMath.Sin(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Cos(conto.Xz) + airz);

        if (fix64.Abs(Speed) > 10 || !Mtouch)
        {
            if (fix64.Abs(Pxy - conto.Xy) >= 4)
            {
                if (Pxy > conto.Xy)
                {
                    conto.Xy += (2 + (Pxy - conto.Xy) / 2);
                }
                else
                {
                    conto.Xy -= (2 + (conto.Xy - Pxy) / 2);
                }
            }
            else
            {
                conto.Xy = Pxy;
            }
            if (fix64.Abs(Pzy - conto.Zy) >= 4)
            {
                if (Pzy > conto.Zy)
                {
                    conto.Zy += (2 + (Pzy - conto.Zy) / 2);
                }
                else
                {
                    conto.Zy -= (2 + (conto.Zy - Pzy) / 2);
                }
            }
            else
            {
                conto.Zy = Pzy;
            }
        } // CHK14
        if (Wtouch && !BadLanding)
        {
            var f87 = (Speed / (fix64)Stat.Swits[2] * 14 * (Stat.Bounce - (fix64)0.4f));
            if (control.Left && _tilt < f87 && _tilt >= 0)
            {
                _tilt += (fix64)0.4f * _tickRate;
            }
            else if (control.Right && _tilt > -f87 && _tilt <= 0)
            {
                _tilt -= (fix64)0.4f * _tickRate;
            }
            else if (fix64.Abs(_tilt) > 3 * (Stat.Bounce - (fix64)0.4f))
            {
                if (_tilt > 0)
                {
                    _tilt -= 3 * (Stat.Bounce - (fix64)0.3f) * _tickRate;
                }
                else
                {
                    _tilt += 3 * (Stat.Bounce - (fix64)0.3f) * _tickRate;
                }
            }
            else
            {
                _tilt = 0;
            }
            conto.Xy += _tilt * _tickRate;
            FrameTrace.AddMessage("y before tilt: " + conto.Y);
            if (Gtouch)
            {
                conto.Y -= (int)((_tilt / (fix64)1.5f) * _tickRate);
            }
            FrameTrace.AddMessage("y after tilt: " + conto.Y);
            FrameTrace.AddMessage("tilt: " + _tilt);
        }
        else if (_tilt != 0)
        {
            _tilt = 0;
        }
        if (Wtouch && surfaceType == 2)
        {
            conto.Zy += (int)((random.NextF64() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            conto.Xy += (int)((random.NextF64() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
        }
        if (Wtouch && surfaceType == 1)
        {
            conto.Zy += (int)((random.NextF64() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            conto.Xy += (int)((random.NextF64() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
        } // CHK15
        if (Hitmag >= Stat.Maxmag && !Wasted)
        {
            Distruct(this, EventArgs.Empty);
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
                    if (fix64.Abs(conto.Z - CheckPoints.Z[i92]) <
                        60 + fix64.Abs(Scz[0] + Scz[1] + Scz[2] + Scz[3]) / 4 &&
                        fix64.Abs(conto.X - CheckPoints.X[i92]) < 700 &&
                        fix64.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
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
                    if (fix64.Abs(conto.X - CheckPoints.X[i92]) <
                        60 + fix64.Abs(Scx[0] + Scx[1] + Scx[2] + Scx[3]) / 4 &&
                        fix64.Abs(conto.Z - CheckPoints.Z[i92]) < 700 &&
                        fix64.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
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
                        CheckPoints.Z[_focus] / 10)) > 800.0F)
                {
                    Missedcp = 1;
                }
                if (Missedcp == -2 && Math.Sqrt(Py(conto.X / 10, CheckPoints.X[_focus] / 10, conto.Z / 10,
                        CheckPoints.Z[_focus] / 10)) < 400.0F)
                {
                    Missedcp = 0;
                }
                if (Missedcp != 0 && Mtouch && Math.Sqrt(Py(conto.X / 10, CheckPoints.X[_focus] / 10, conto.Z / 10,
                        CheckPoints.Z[_focus] / 10)) < 250.0F)
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
                        if (fix64.Abs(conto.Z - CheckPoints.Fz[i93]) < 200 && Py(conto.X / 100,
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
                    else if (fix64.Abs(conto.X - CheckPoints.Fx[i93]) < 200 && Py(conto.Z / 100,
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
                Travxy += ((Rcomp - Lcomp) * _tickRate);
                if (fix64.Abs(Travxy) > 135)
                {
                    Rtab = true;
                }
                Travzy += ((Ucomp - Dcomp) * _tickRate);
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
                Travxz += (_lxz - conto.Xz) * _tickRate;
                _lxz = conto.Xz;
            }
            if (_srfcnt < (10 * (1/_tickRate)))
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
                        bool JustSurfer = true;
                        Powerup = 0;
                        if (fix64.Abs(Travxy) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(Travxy) / 24;
                        }
                        else if (Rtab)
                        {
                            JustSurfer = false;
                            Powerup += 30;
                        }
                        if (fix64.Abs(Travzy) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(Travzy) / 18;
                        }
                        else
                        {
                            if (Ftab)
                            {
                                JustSurfer = false;
                                Powerup += 40;
                            }
                            if (Btab)
                            {
                                JustSurfer = false;
                                Powerup += 40;
                            }
                        }
                        if (fix64.Abs(Travxz) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(Travxz) / 18;
                        }
                        if (Surfer)
                        {
                            Powerup += 30;
                        }
                        Power += Powerup;

                        // dont invoke powerup if we only did a surf...
                        if(!JustSurfer) PowerUp?.Invoke(this, (float)Powerup);
                        
                        /*if (Im == XTGraphics.Im && (int) Powerup > Record.Powered && Record.Wasted == 0 &&
                            (Powerup > 60 || CheckPoints.Stage == 1 || CheckPoints.Stage == 2))
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
                        if (Power > 98)
                        {
                            Power = 98;
                            if (Powerup > 150)
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
                        if (fix64.Abs(Scz[i96]) < 70 && fix64.Abs(Scx[i96]) < 70)
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
                        Speed = 0;
                        conto.Y += Stat.Flipy;
                        Pxy += 180;
                        conto.Xy += 180;
                        Capcnt = 0;
                    }
                }
            }
            if (Trcnt == 0 && Speed != 0)
            {
                if (_xtpower == 0)
                {
                    if (Power > 0)
                    {
                        Power -= (Power * Power * Power / Stat.Powerloss) * _tickRate;
                    }
                    else
                    {
                        Power = 0;
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
                    Record.Whenwasted = (int) (185 + RandomSFloat() * 20);
                }
            }
        }*/
    }


    // input: number of grounded wheels to medium
    // output: hitVertical when colliding against a wall
    private void PhyTrackPieceCollision(IStage stage, Control control, ContO conto, Span<fix64> wheelx, Span<fix64> wheely, Span<fix64> wheelz,
        fix64 groundY, fix64 wheelYThreshold, fix64 wheelGround, ref int nGroundedWheels, bool wasMtouch,
        int surfaceType, out bool hitVertical, Span<bool> isWheelGrounded, DeterministicRandom random)
    {
        hitVertical = false;

        var isWheelTouchingPiece = new InlineArray4<bool>(); // nwheels

        int touching = 0; //Phy-addons: Fix sliding on floating pieces
        int nWheelsRoadRamp = 0;
        int nWheelsDirtRamp = 0;
        for (int k = 0; k < 4; k++)
        {
            var position = new f64Vector3(wheelx[k], wheely[k] - wheelGround, wheelz[k]);
            var velocity = new f64Vector3(Scx[k], Scy[k], Scz[k]);

            if (!isWheelTouchingPiece[k])
            {
                foreach (var collidable in stage.RetrievePointCollidables(wheelx[k], wheelz[k]))
                {
                    if (collidable.BoxRoad is {} boxRoad)
                    {
                        if (boxRoad.ResolveCollision(position) is { } collision)
                        {
                            touching |= 1 << k;
                            ++nGroundedWheels;
                            Wtouch = true;
                            Gtouch = true;

                            if (!wasMtouch && Scy[k] != 7 /* * checkpoints.gravity */ * _tickRate)
                            {
                                fix64 dustMag = Scy[k] / (fix64)(333.33F);
                                if (dustMag > (fix64)(0.3F))
                                    dustMag = (fix64)(0.3F);
                                if (surfaceType == 0)
                                    dustMag += (fix64)1.1f;
                                else
                                    dustMag += (fix64)1.2f;
                                conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], dustMag * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                            }
                            wheely[k] = collision.newY + wheelGround; // snap wheel to the surface
                            
                            // sparks and scrape
                            if (BadLanding && collidable.Box.Skid is 0 or 1)
                            {
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                //if (Im == /*this.xt.im*/ 0)
                                SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                            }

                            bounceRebound(k, conto, random);
                            isWheelTouchingPiece[k] = true;
                            break;
                        }
                    }
                    else if (collidable.BoxWall is {} boxWall)
                    {
                        if (boxWall.ResolveCollision(position, velocity) is { } collision)
                        {
                            for (int w = 0; w < 4; w++) {
                                wheelx[w] += collision.positionDelta.X;
                                wheely[w] += collision.positionDelta.Y;
                                wheelz[w] += collision.positionDelta.Z;
                            }
                            
                            // sparks and scrapes
                            if (collidable.Box.Skid != 2)
                                _crank[0, k]++;
                            if (collidable.Box.Skid == 5 && random.NextF64() > (fix64)0.5f)
                                _crank[0, k]++;
                            if (_crank[0, k] > 1)
                            {
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                                //if (Im == /*this.xt.im*/ 0)
                                SfxPlayScrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                            }

                            // z rebound CHK5
                            f64Vector3 reboundVelocityDelta = collision.impactComponent * (-GetReboundMul(wasMtouch));
                            Regz(k, reboundVelocityDelta.Length() * collidable.Box.Damage, conto, random);
                            Scx[k] += reboundVelocityDelta.X;
                            Scy[k] += reboundVelocityDelta.Y;
                            Scz[k] += reboundVelocityDelta.Z;

                            Skid = 2;
                            hitVertical = true;
                            isWheelTouchingPiece[k] = true;
                            if (!collidable.Box.NotWall) {
                                control.Wall = 9999;
                            }
                            break;
                        }
                    }
                    else if (collidable.BoxRamp is {} boxRamp)
                    {
                        if (boxRamp.ResolveCollision(position) is { } collision)
                        {
                            var liftDivider = 1 + (50 - Math.Abs(collidable.Box.Zy)) / (fix64)30;
                            if (liftDivider < 1)
                                liftDivider = 1;
                            if (collision.zTmp > 0 && collision.zTmp < 200) {
                                Scy[k] -= collision.zTmp / liftDivider;
                            }

                            if (collision.zTmp > -30)
                            {
                                if (collidable.Box.Skid == 2)
                                    nWheelsDirtRamp++;
                                else
                                    nWheelsRoadRamp++;
                                
                                Wtouch = true;
                                Gtouch = false;

                                // sparks and scrape
                                if (BadLanding && (collidable.Box.Skid == 0 || collidable.Box.Skid == 1))
                                {
                                    conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                    //if (Im == /*this.xt.im*/ 0)
                                    SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                                }

                                if (!wasMtouch && surfaceType != 0)
                                {
                                    fix64 dustMag = (fix64)1.4F;
                                    conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], dustMag * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                                }
                            }
                            
                            wheelx[k] = collision.newPosition.X;
                            wheely[k] = collision.newPosition.Y + wheelGround;
                            wheelz[k] = collision.newPosition.Z;
                            isWheelTouchingPiece[k] = true;
                            break;
                        }
                    }
                }
            }
        }
    }

    private fix64 GetReboundMul(bool wasMtouch)
    {
        var reboundMul = fix64.Abs(UMath.Cos(Pxy)) + fix64.Abs(UMath.Cos(Pzy));
        reboundMul /= 4;
        if (reboundMul > (fix64)0.3F)
            reboundMul = (fix64)0.3F;
        if (wasMtouch)
            reboundMul = 0;
        reboundMul += Stat.Bounce - (fix64)0.2f;
        if (reboundMul < (fix64)1.1f)
            reboundMul = (fix64)1.1F;
        return reboundMul;
    }

    private int Regx(int i, fix64 f, ContO conto, DeterministicRandom random)
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
        if (fix64.Abs(f) > 100)
        {
            //Record.Recx(i, f, Im);
            if (f > 100)
            {
                f -= 100;
            }
            if (f < -100)
            {
                f += 100;
            }
            Shakedam = (int)((fix64.Abs(f) + Shakedam) / 2);
            if (/*Im == XTGraphics.Im*/true || _colidim)
            {
                SfxPlayCrash(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i111 = 0; i111 < 40; i111++)
            {
                fix64 f112 = 0;
                for (var i113 = 0; i113 < 4; i113++)
                {
                    f112 = f / 20 * random.NextF64();
                    if (abool)
                    {
                        Hitmag += (int)fix64.Abs(f112);
                        i110 += (int)fix64.Abs(f112);
                    }
                }
            }
        }
        return i110;
    }

    private int Regy(int i, fix64 f, ContO conto, DeterministicRandom random)
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
        if (f > 100)
        {
            //Record.Recy(i, f, Mtouch, Im);
            f -= 100;
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
                Shakedam = (int)((fix64.Abs(f) + Shakedam) / 2);
            }
            
            if (/*Im == XTGraphics.Im ||*/true || _colidim)
            {
                SfxPlayCrash(this, ((int)f, i99 * i98));
                //XTGraphics.Acrash(Im, f, i99 * i98);
            }
            if (i99 * i98 == 0 || Mtouch)
            {
                for (var i102 = 0; i102 < 40; i102++)
                {
                    fix64 f103 = 0;
                    for (var i104 = 0; i104 < 4; i104++)
                    {
                        f103 = f / 20 * random.NextF64();
                        if (abool)
                        {
                            Hitmag += (int)fix64.Abs(f103);
                            i97 += (int)fix64.Abs(f103);
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
                        fix64 f108 = 0;
                        for (var i109 = 0; i109 < 4; i109++)
                        {
                            f108 = f / 15 * random.NextF64();
                            i105 += (int)f108;
                            i106++;
                            if (abool)
                            {
                                Hitmag += (int)fix64.Abs(f108);
                                i97 += (int)fix64.Abs(f108);
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

    private int Regz(int i, fix64 f, ContO conto, DeterministicRandom random)
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
        if (fix64.Abs(f) > 100)
        {
            //Record.Recz(i, f, Im);
            if (f > 100)
            {
                f -= 100;
            }
            if (f < -100)
            {
                f += 100;
            }
            Shakedam = (int)((fix64.Abs(f) + Shakedam) / 2);
            
            if (/*Im == XTGraphics.Im ||*/true || _colidim)
            {
                SfxPlayCrash(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i115 = 0; i115 < 40; i115++)
            {
                fix64 f116 = 0;
                for (var i117 = 0; i117 < 4; i117++)
                {
                    f116 = f / 20 * random.NextF64();
                    if (abool)
                    {
                        Hitmag += (int)fix64.Abs(f116);
                        i114 += (int)fix64.Abs(f116);
                    }
                }
            }
        }
        return i114;
    }

    public void Reseto(int i, ContO conto)
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
        Speed = 0;
        for (var i1 = 0; i1 < 4; i1++)
        {
            Scy[i1] = 0;
            Scx[i1] = 0;
            Scz[i1] = 0;
        }
        _forca = (fix64.Sqrt(conto.Keyz[0] * conto.Keyz[0] + conto.Keyx[0] * conto.Keyx[0]) +
                  fix64.Sqrt(conto.Keyz[1] * conto.Keyz[1] + conto.Keyx[1] * conto.Keyx[1]) +
                  fix64.Sqrt(conto.Keyz[2] * conto.Keyz[2] + conto.Keyx[2] * conto.Keyx[2]) +
                  fix64.Sqrt(conto.Keyz[3] * conto.Keyz[3] + conto.Keyx[3] * conto.Keyx[3])) / 10000 *
                 (Stat.Bounce - (fix64)0.3f);
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
        Ucomp = 0;
        Dcomp = 0;
        Lcomp = 0;
        Rcomp = 0;
        _lxz = 0;
        Travxy = 0;
        Travzy = 0;
        Travxz = 0;
        Rtab = false;
        Ftab = false;
        Btab = false;
        Powerup = 0;
        _xtpower = 0;
        Trcnt = 0;
        Capcnt = 0;
        _tilt = 0;
        for (var i2 = 0; i2 < 4; i2++)
        {
            for (var i3 = 0; i3 < 4; i3++)
            {
                _crank[i2, i3] = 0;
                _lcrank[i2, i3] = 0;
            }
        }
        //Pcleared = CheckPoints.Pcs;
        Nofocus = false;
        Power = 98;
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
}