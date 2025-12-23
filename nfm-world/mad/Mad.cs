using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.IO.Compression;
using FixedMathSharp.Utility;
using NFMWorld.Util;
using SoftFloat;

namespace NFMWorld.Mad;

public class Mad
{
    private static readonly sfloat _tickRate = (sfloat)GameSparker.PHYSICS_MULTIPLIER;
    public Boolean Halted = false;

    public event EventHandler<(float f, int i)> SfxPlayCrash;
    public event EventHandler<(int i, float f)> SfxPlaySkid;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayScrape;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayGscrape;

    internal bool Btab;
    internal int Capcnt;
    internal bool BadLanding;
    private readonly bool[] _caught = new bool[8];
    internal CarStats Stat;
    internal int Clear;
    internal int Cn;
    internal int Cntdest;
    private int _cntouch;
    
    /// <summary>
    /// Is colliding with the client player car
    /// </summary>
    private bool _colidim;
    private readonly int[,] _crank = new int[4, 4];
    internal sfloat Cxz;
    private int _dcnt;
    internal sfloat Dcomp;
    internal bool Wasted;
    private readonly bool[] _dominate = new bool[8];
    private readonly sfloat _drag = (sfloat)(0.5F);
    private int _fixes = -1;
    private int _focus = -1;
    private sfloat _forca;
    internal bool Ftab;
    private sfloat _fxz;
    internal bool Gtouch;
    internal int Hitmag;
    internal int Im;
    internal int Lastcolido;
    internal sfloat Lcomp;
    private readonly int[,] _lcrank = new int[4, 4];
    internal int Loop;
    private sfloat _lxz;
    internal int Missedcp;
    internal bool Mtouch;
    internal sfloat Mxz;
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
    internal sfloat Power = (sfloat)(75.0F);
    internal sfloat Powerup;
    internal bool Pr;
    internal bool Pu;
    internal bool Pushed;

    internal sfloat Pxy;
    internal sfloat Pzy;
    internal sfloat Rcomp;
    private int _rpdcatch;
    internal bool Rtab;
    internal readonly sfloat[] Scx = new sfloat[4];
    internal readonly sfloat[] Scy = new sfloat[4];
    internal readonly sfloat[] Scz = new sfloat[4];
    internal int Shakedam;
    internal int Skid;
    internal sfloat Speed;
    internal int Squash;
    private int _srfcnt;
    internal bool Surfer;
    private sfloat _tilt;
    internal int Travxy;
    internal sfloat Travxz;
    internal int Travzy;
    internal int Trcnt;
    internal sfloat Txz;
    internal sfloat Ucomp;
    internal bool Wtouch;
    private int _xtpower;

    private bool IsClientPlayer;

    internal Mad(CarStats stat, int im, bool isClientPlayer)
    {
        Stat = stat;
        Im = im;
        IsClientPlayer = isClientPlayer;
    }

    public void SetStat(CarStats stat)
    {
        Stat = stat;
    }

    public bool pointInBox(sfloat px, sfloat py, sfloat pz, sfloat bx, sfloat by, sfloat bz, sfloat szx, sfloat szy, sfloat szz)
    {
        return px > bx - szx && px < bx + szx && pz > bz - szz && pz < bz + szz && py > by - szy && py < by + (szy == sfloat.Zero ? 100 : szy);
    }

    internal void Colide(ContO conto, Mad othermad, ContO otherconto)
    {
        var random = new DeterministicRandom((ulong)(conto.X.Value.m_rawValue ^ otherconto.X.Value.m_rawValue ^ conto.Z.Value.m_rawValue ^ otherconto.Z.Value.m_rawValue ^ conto.Y.Value.m_rawValue ^ otherconto.Y.Value.m_rawValue));
        
        Span<sfloat> wheelx = stackalloc sfloat[4];
        Span<sfloat> wheely = stackalloc sfloat[4];
        Span<sfloat> wheelz = stackalloc sfloat[4];
        Span<sfloat> otherwheelx = stackalloc sfloat[4];
        Span<sfloat> otherwheely = stackalloc sfloat[4];
        Span<sfloat> otherwheelz = stackalloc sfloat[4];
        
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
        Rot(wheelx, wheely, conto.X, conto.Y, conto.Xy, 4);
        Rot(wheely, wheelz, conto.Y, conto.Z, conto.Zy, 4);
        Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        Rot(otherwheelx, otherwheely, otherconto.X, otherconto.Y, otherconto.Xy, 4);
        Rot(otherwheely, otherwheelz, otherconto.Y, otherconto.Z, otherconto.Zy, 4);
        Rot(otherwheelx, otherwheelz, otherconto.X, otherconto.Z, otherconto.Xz, 4);
        if (Rpy(conto.X, otherconto.X, conto.Y, otherconto.Y, conto.Z, otherconto.Z) <
            (conto.MaxR * conto.MaxR + otherconto.MaxR * otherconto.MaxR) * (sfloat)1.5f)
        {
            if (!_caught[othermad.Im] && (Speed != (sfloat)0.0F || othermad.Speed != (sfloat)0.0F))
            {
                if (sfloat.Abs(sfloat.Abs(Power * Speed * Stat.Moment) - sfloat.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment)) > (sfloat)0.001f)
                {
                    _dominate[othermad.Im] = sfloat.Abs(Power * Speed * Stat.Moment) > sfloat.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment);
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
                ) / (sfloat)16.0F);
            var impactExtraRdius = 7000;
            var damageMult = (sfloat)1.0F;
            if (World.UseMultiplayerCollisionModifiers)
            {
                impactExtraRdius = 28000;
                damageMult = (sfloat)1.27F;
            }
            for (var wheel = 0; wheel < 4; wheel++)
            {
                for (var otherwheel = 0; otherwheel < 4; otherwheel++)
                {
                    if (Rpy(wheelx[wheel], otherwheelx[otherwheel], wheely[wheel], otherwheely[otherwheel], wheelz[wheel], otherwheelz[otherwheel]) <
                        (impactMagnitude + impactExtraRdius) * (othermad.Stat.Comprad + Stat.Comprad))
                    {
                        if (sfloat.Abs(Scx[wheel] * Stat.Moment) > sfloat.Abs(othermad.Scx[otherwheel] * othermad.Stat.Moment))
                        {
                            var f130 = othermad.Scx[otherwheel] * Stat.Revpush;
                            if (f130 > (sfloat)300.0F)
                            {
                                f130 = (sfloat)300.0F;
                            }
                            if (f130 < (sfloat)(-300.0F))
                            {
                                f130 = (sfloat)(-300.0F);
                            }
                            var f131 = Scx[wheel] * Stat.Push;
                            if (f131 > (sfloat)300.0F)
                            {
                                f131 = (sfloat)300.0F;
                            }
                            if (f131 < (sfloat)(-300.0F))
                            {
                                f131 = (sfloat)(-300.0F);
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
                                    (wheelx[wheel] + otherwheelx[otherwheel]) / (sfloat)2.0F, 
                                    (wheely[wheel] + otherwheely[otherwheel]) / (sfloat)2.0F,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) / (sfloat)2.0F, 
                                    (othermad.Scx[otherwheel] + Scx[wheel]) / (sfloat)4.0F,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) / (sfloat)4.0F,
                                    (othermad.Scz[otherwheel] + Scz[wheel]) / (sfloat)4.0F,
                                    2,
                                    (wheelGround + otherWheelGround) / 2
                                );
                            }
                        }
                        if (sfloat.Abs(Scz[wheel] * Stat.Moment) > sfloat.Abs(othermad.Scz[otherwheel] * othermad.Stat.Moment))
                        {
                            var f132 = othermad.Scz[otherwheel] * Stat.Revpush;
                            if (f132 > (sfloat)300.0F)
                            {
                                f132 = (sfloat)300.0F;
                            }
                            if (f132 < (sfloat)(-300.0F))
                            {
                                f132 = (sfloat)(-300.0F);
                            }
                            var f133 = Scz[wheel] * Stat.Push;
                            if (f133 > (sfloat)300.0F)
                            {
                                f133 = (sfloat)300.0F;
                            }
                            if (f133 < (sfloat)(-300.0F))
                            {
                                f133 = (sfloat)(-300.0F);
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
                                    (wheelx[wheel] + otherwheelx[otherwheel]) / (sfloat)2.0F, 
                                    (wheely[wheel] + otherwheely[otherwheel]) / (sfloat)2.0F,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) / (sfloat)2.0F,
                                    (othermad.Scx[otherwheel] + Scx[wheel]) / (sfloat)4.0F,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) / (sfloat)4.0F, 
                                    (othermad.Scz[otherwheel] + Scz[wheel]) / (sfloat)4.0F,
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

    private static int GetWheelGround(Mad mad, ContO conto, sfloat bottomy)
    {
        int wheelGround;
        if (World.IsHyperglidingEnabled)
        {
            wheelGround = (int)((bottomy * (sfloat)1f / _tickRate) * ((sfloat)1f - _tickRate));
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

    private static sfloat GetBottomY(Mad mad, ContO conto)
    {
        sfloat bottomy;
        if (World.IsHyperglidingEnabled)
        {
            if (mad.BadLanding)
            {
                bottomy = (sfloat)((mad.Stat.Flipy + mad.Squash) * _tickRate);
            }
            else
            {
                bottomy = (sfloat)(conto.Grat * _tickRate);
            }
        }
        else
        {
            bottomy = 0;
        }

        return bottomy;
    }

    private void Distruct(ContO conto)
    {
        conto.Wasted = true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Sin(sfloat deg)
    {
        var sin = sfloat.Sin(deg * sfloat.DegToRad);
        if (sfloat.WithinEpsilon(sin, 0))
            return 0;
        if (sfloat.WithinEpsilon(sin, -1))
            return -1;
        if (sfloat.WithinEpsilon(sin, 1))
            return 1;
        return sin;
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Cos(sfloat deg)
    {
        var cos = sfloat.Cos(deg * sfloat.DegToRad);
        if (sfloat.WithinEpsilon(cos, 0))
            return 0;
        if (sfloat.WithinEpsilon(cos, -1))
            return -1;
        if (sfloat.WithinEpsilon(cos, 1))
            return 1;
        return cos;
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Sin(int deg)
    {
        return Sin((sfloat)deg);
    }
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Sin(float deg)
    {
        return Sin((sfloat)deg);
    }
        
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Cos(int deg)
    {
        return Cos((sfloat)deg);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static sfloat Cos(float deg)
    {
        return Cos((sfloat)deg);
    }

    public void bounceRebound(int wi, ContO conto, DeterministicRandom random)
    {
        // part 1: the closer we are to 90/-90 in Pxy or Pzy, the bigger the bounce
        sfloat rebound = (sfloat.Abs(Sin(Pxy)) + sfloat.Abs(Sin(Pzy))) / (sfloat)3;
        sfloat maxAngleRebound = (sfloat)(0.4F); // capping at 0.4 doesn't do much, max is two thirds
        rebound = sfloat.Min(rebound, maxAngleRebound);

        // part 2: the bigger the bounce stat, the bigger the bounce
        rebound += Stat.Bounce;
        sfloat minRebound = (sfloat)(1.1F);
        rebound = sfloat.Max(rebound, minRebound);

        Regy(wi, sfloat.Abs(Scy[wi] * rebound), conto, random);
        // if scy is > 0 then we are going down, apply the rebound bounce
        if (Scy[wi] > (sfloat)(0.0F))
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
            // Scy[wi] -= sfloat.Abs(Scy[wi] * rebound);
            // Scy[wi] -= Scy[wi] * rebound; // don't need the abs, both are always positive
            Scy[wi] = (sfloat)(-1) * Scy[wi] * (rebound - (sfloat)1);
    }

    public void bounceReboundZ(int ti, int wi, ContO conto, bool wasMtouch/*, Trackers trackers, CheckPoints checkpoints*/, DeterministicRandom random)
    {
        sfloat rebound = sfloat.Abs(Cos(Pxy)) + sfloat.Abs(Cos(Pzy)) / 4;
        sfloat maxAngleRebound = (sfloat)0.3F;
        rebound = sfloat.Min(rebound, maxAngleRebound);
        //        if (wasMtouch)
        //            rebound = 0;
        rebound += Stat.Bounce - (sfloat)0.2F;
        sfloat minRebound = (sfloat)1.1F;
        rebound = sfloat.Max(rebound, minRebound);
        Regz(wi, -1 * Scz[wi] * rebound * Trackers.Dam[ti] /** checkpoints.dam*/, conto, random);
        Scz[wi] = -1 * Scz[wi] * (rebound - 1);
    }

    public void bounceReboundX(int ti, int wi, ContO conto, bool wasMtouch/*, Trackers trackers, CheckPoints checkpoints*/, DeterministicRandom random)
    {
        sfloat rebound = sfloat.Abs(Cos(Pxy)) + sfloat.Abs(Cos(Pzy)) / 4;
        sfloat maxAngleRebound = (sfloat)0.3F;
        rebound = sfloat.Min(rebound, maxAngleRebound);
        //        if (wasMtouch)
        //            rebound = 0;
        rebound += Stat.Bounce - (sfloat)0.2F;
        sfloat minRebound = (sfloat)1.1F;
        rebound = sfloat.Max(rebound, minRebound);
        Regx(wi, -1 * Scx[wi] * rebound * Trackers.Dam[ti]/* * checkpoints.dam*/, conto, random);
        Scx[wi] = -1 * Scx[wi] * (rebound - 1);
    }

    int Mtcount = 0;
    sfloat py = 0;

    internal void Drive(Control control, ContO conto)
    {
        DeterministicRandom random = new((ulong)(conto.X.Value.m_rawValue ^ conto.Y.Value.m_rawValue ^ conto.Z.Value.m_rawValue));

        FrameTrace.AddMessage($"xz: {conto.Xz:0.00}, mxz: {Mxz:0.00}, lxz: {_lxz:0.00}, fxz: {_fxz:0.00}, cxz: {Cxz:0.00}");
        FrameTrace.AddMessage($"xy: {conto.Xy:0.00}, pxy: {Pxy:0.00}, zy: {conto.Zy:0.00}, pzy: {Pzy:0.00}");
        FrameTrace.AddMessage($"Travxz: {Travxz}, Travxy: {Travxy}, Travzy: {Travzy}, Surfing: {Surfer}");

        var xneg = 1;
        var zneg = 1;
        var zyinv = false;
        var revspeed = false;
        var hitVertical = false;
        BadLanding = false;
        if (!Mtouch) Mtcount++; //DS-addons: Bad landing hotfix
        sfloat zyangle;
        for (zyangle = sfloat.Abs(Pzy); zyangle > 360; zyangle -= 360)
        {
            /* empty */
        }

        sfloat xyangle;
        for (xyangle = sfloat.Abs(Pxy); xyangle > 360; xyangle -= 360)
        {
            /* empty */
        }

        sfloat zy;
        for (zy = sfloat.Abs(Pzy); zy > 270; zy -= 360)
        {
        }

        zy = sfloat.Abs(zy);
        if (zy > 90)
        {
            zyinv = true;
        }

        var xyinv = false;
        sfloat xy;
        for (xy = sfloat.Abs(Pxy); xy > 270; xy -= 360)
        {
        }

        xy = sfloat.Abs(xy);
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
        sfloat bottomy = GetBottomY(this, conto);

        control.Zyinv = zyinv;
        //

        var airx = (sfloat)(0.0F);
        var airz = (sfloat)(0.0F);
        var airy = (sfloat)(0.0F);
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

            Ucomp = (sfloat)(0.0F);
            Dcomp = (sfloat)(0.0F);
            Lcomp = (sfloat)(0.0F);
            Rcomp = (sfloat)(0.0F);
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
            var f13 = (Scy[0] + Scy[1] + Scy[2] + Scy[3]) / (sfloat)(4.0F);
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
                    if (Ucomp == (sfloat)(0.0F))
                    {
                        Ucomp = (sfloat)(10.0F) + (Scy[0] + (sfloat)(50.0F)) / (sfloat)(20.0F);
                        if (Ucomp < (sfloat)(5.0F))
                        {
                            Ucomp = (sfloat)(5.0F);
                        }

                        if (Ucomp > (sfloat)(10.0F))
                        {
                            Ucomp = (sfloat)(10.0F);
                        }

                        Ucomp *= Stat.Airs;
                    }

                    if (Ucomp < (sfloat)(20.0F))
                    {
                        Ucomp += (sfloat)0.5f * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * Sin(conto.Xz) * zneg * _tickRate;
                    airz = Stat.Airc * Cos(conto.Xz) * zneg * _tickRate;
                }
                else if (Ucomp != (sfloat)(0.0F) && Ucomp > -(sfloat)(2.0F))
                {
                    Ucomp -= (sfloat)0.5f * Stat.Airs * _tickRate; //
                }

                if (control.Down)
                {
                    if (Dcomp == (sfloat)(0.0F))
                    {
                        Dcomp = (sfloat)(10.0F) + (Scy[0] + (sfloat)(50.0F)) / (sfloat)(20.0F);
                        if (Dcomp < (sfloat)(5.0F))
                        {
                            Dcomp = (sfloat)(5.0F);
                        }

                        if (Dcomp > (sfloat)(10.0F))
                        {
                            Dcomp = (sfloat)(10.0F);
                        }

                        Dcomp *= Stat.Airs;
                    }

                    if (Dcomp < (sfloat)(20.0F))
                    {
                        Dcomp += (sfloat)0.5f * Stat.Airs * _tickRate; //
                    }

                    airy = -Stat.Airc * _tickRate;
                }
                else if (Dcomp != (sfloat)(0.0F) && Ucomp > -(sfloat)(2.0F))
                {
                    Dcomp -= (sfloat)0.5f * Stat.Airs * _tickRate;
                } //

                if (control.Left)
                {
                    if (Lcomp == (sfloat)(0.0F))
                    {
                        Lcomp = (sfloat)(5.0F);
                    }

                    if (Lcomp < (sfloat)(20.0F)) // maxine: scale to tickrate
                    {
                        Lcomp += (sfloat)(2.0F) * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * Cos(conto.Xz) * xneg * _tickRate;
                    airz = -Stat.Airc * Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Lcomp > (sfloat)(0.0F))
                {
                    Lcomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate; //
                }

                if (control.Right) //
                {
                    if (Rcomp == (sfloat)(0.0F))
                    {
                        Rcomp = (sfloat)(5.0F);
                    }

                    if (Rcomp < (sfloat)(20.0F)) // maxine: scale to tickrate
                    {
                        Rcomp += (sfloat)(2.0F) * Stat.Airs * _tickRate;
                    }

                    airx = Stat.Airc * Cos(conto.Xz) * xneg * _tickRate;
                    airz = Stat.Airc * Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (Rcomp > (sfloat)(0.0F)) //
                {
                    Rcomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate;
                }

                Pzy = QuantizeTowardsZero((Pzy + (Dcomp - Ucomp) * Cos(Pxy) * _tickRate), _tickRate); //
                if (zyinv)
                {
                    conto.Xz = QuantizeTowardsZero(conto.Xz + ((Dcomp - Ucomp) * Sin(Pxy) * _tickRate), _tickRate);
                }
                else
                {
                    conto.Xz = QuantizeTowardsZero(conto.Xz - ((Dcomp - Ucomp) * Sin(Pxy) * _tickRate), _tickRate);
                }

                Pxy = QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
            }
            else
            {
                //
                var f15 = Power;
                if (f15 < (sfloat)(40.0F))
                {
                    f15 = (sfloat)(40.0F);
                }

                if (control.Down)
                {
                    if (Speed > (sfloat)(0.0F))
                    {
                        Speed -= Stat.Handb / 2 * _tickRate;
                    }
                    else
                    {
                        var i16 = 0;
                        for (var i17 = 0; i17 < 2; i17++)
                        {
                            if (Speed <= -(Stat.Swits[i17] / 2 + f15 * Stat.Swits[i17] / (sfloat)(196.0F)))
                            {
                                i16++;
                            }
                        }

                        if (i16 != 2)
                        {
                            //
                            Speed -= ((sfloat)Stat.Acelf.AsSpan()[i16] / (sfloat)2.0F + f15 * (sfloat)Stat.Acelf.AsSpan()[i16] / (sfloat)196.0F) * _tickRate;
                        }
                        else
                        {
                            Speed = -(Stat.Swits[1] / 2 + f15 * Stat.Swits[1] / (sfloat)(196.0F));
                        }
                    }
                }

                if (control.Up)
                {
                    if (Speed < (sfloat)(0.0F)) //
                    {
                        Speed += Stat.Handb * _tickRate;
                    }
                    else
                    {
                        var i18 = 0;
                        for (var i19 = 0; i19 < 3; i19++)
                        {
                            if (Speed >= Stat.Swits[i19] / 2 + f15 * Stat.Swits[i19] / (sfloat)(196.0F))
                            {
                                i18++;
                            }
                        }

                        if (i18 != 3)
                        {
                            Speed += ((sfloat)Stat.Acelf.AsSpan()[i18] / (sfloat)2.0F + f15 * (sfloat)Stat.Acelf.AsSpan()[i18] / (sfloat)196.0F) * _tickRate;
                        }
                        else
                        {
                            Speed = Stat.Swits[2] / 2 + f15 * Stat.Swits[2] / (sfloat)(196.0F);
                        }
                    }
                } //

                if (control.Handb && sfloat.Abs(Speed) > Stat.Handb)
                {
                    if (Speed < (sfloat)(0.0F))
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
                            if (Lcomp == (sfloat)(0.0F))
                            {
                                Lcomp = (sfloat)(5.0F) * Stat.Airs * _tickRate;
                            }

                            if (Lcomp < (sfloat)(20.0F))
                            {
                                Lcomp += (sfloat)(2.0F) * Stat.Airs * _tickRate;
                            }
                        }
                    } //
                    else
                    {
                        if (Lcomp > (sfloat)(0.0F))
                        {
                            Lcomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate;
                        }

                        Pl = false;
                    } //

                    if (control.Right)
                    {
                        if (!Pr)
                        {
                            if (Rcomp == (sfloat)(0.0F))
                            {
                                Rcomp = (sfloat)(5.0F) * Stat.Airs * _tickRate;
                            }

                            if (Rcomp < (sfloat)(20.0F))
                            {
                                Rcomp += (sfloat)(2.0F) * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Rcomp > (sfloat)(0.0F))
                        {
                            Rcomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate;
                        }

                        Pr = false;
                    } //

                    if (control.Up)
                    {
                        if (!Pu)
                        {
                            if (Ucomp == (sfloat)(0.0F))
                            {
                                Ucomp = (sfloat)(5.0F) * Stat.Airs * _tickRate;
                            }

                            if (Ucomp < (sfloat)(20.0F))
                            {
                                Ucomp += (sfloat)(2.0F) * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (Ucomp > (sfloat)(0.0F))
                        {
                            Ucomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate;
                        }

                        Pu = false;
                    }

                    if (control.Down)
                    {
                        if (!Pd)
                        {
                            if (Dcomp == (sfloat)(0.0F))
                            {
                                Dcomp = (sfloat)(5.0F) * Stat.Airs * _tickRate;
                            }

                            if (Dcomp < (sfloat)(20.0F))
                            {
                                Dcomp += (sfloat)(2.0F) * Stat.Airs * _tickRate;
                            }
                        }
                    }
                    else
                    {
                        if (Dcomp > (sfloat)(0.0F))
                        {
                            Dcomp -= (sfloat)(2.0F) * Stat.Airs * _tickRate;
                        }

                        Pd = false;
                    }

                    Pzy = QuantizeTowardsZero((Pzy + ((Dcomp - Ucomp) * Cos(Pxy)) * _tickRate), _tickRate);
                    if (zyinv)
                    {
                        conto.Xz = QuantizeTowardsZero(conto.Xz + (((Dcomp - Ucomp) * Sin(Pxy)) * _tickRate), _tickRate);
                    }
                    else
                    {
                        conto.Xz = QuantizeTowardsZero(conto.Xz - (((Dcomp - Ucomp) * Sin(Pxy)) * _tickRate), _tickRate);
                    }

                    Pxy = QuantizeTowardsZero((Pxy + (Rcomp - Lcomp) * _tickRate), _tickRate);
                }
            }
        }

        var f20 = (sfloat)(20.0F) * Speed / ((sfloat)(154.0F) * Stat.Simag);
        if (f20 > (sfloat)(20.0F))
        {
            f20 = (sfloat)(20.0F);
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
            conto.Wxz -= ((sfloat)Stat.Turn * _tickRate);
            if (conto.Wxz < -36)
            {
                conto.Wxz = -36;
            }
        }

        if (control.Left)
        {
            conto.Wxz += ((sfloat)Stat.Turn * _tickRate);
            if (conto.Wxz > 36)
            {
                conto.Wxz = 36;
            }
        } //

        if (conto.Wxz != 0 && !control.Left && !control.Right)
        {
            if (sfloat.Abs(Speed) < (sfloat)(10.0F))
            {
                if (sfloat.Abs(conto.Wxz) == 1)
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
                if (sfloat.Abs(conto.Wxz) < Stat.Turn * 2)
                {
                    conto.Wxz = 0;
                }

                if (conto.Wxz > 0)
                {
                    conto.Wxz -= ((sfloat)Stat.Turn * 2 * _tickRate);
                }

                if (conto.Wxz < 0)
                {
                    conto.Wxz += ((sfloat)Stat.Turn * 2 * _tickRate);
                }
            }
        } //

        var i21 = Speed != 0 ? (int)((sfloat)(3600.0F) / (Speed * Speed)) : int.MaxValue;
        if (i21 < 5)
        {
            i21 = 5;
        }

        if (Speed < (sfloat)(0.0F))
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

        if (Speed > (sfloat)(30.0F) || Speed < -(sfloat)(100.0F))
        {
            while (SafeAbs(Mxz - Cxz) > 180)
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
            if (SafeAbs(Mxz - Cxz) < 30)
            {
                Cxz += (Mxz - Cxz) / (sfloat)(4.0F) * _tickRate; //
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


        Span<sfloat> wheelx = stackalloc sfloat[4];
        Span<sfloat> wheelz = stackalloc sfloat[4];
        Span<sfloat> wheely = stackalloc sfloat[4];
        for (var i24 = 0; i24 < 4; i24++)
        {
            wheelx[i24] = conto.Keyx[i24] + conto.X;
            wheely[i24] = bottomy + conto.Y;
            wheelz[i24] = conto.Z + conto.Keyz[i24];
            Scy[i24] += (sfloat)(7.0F) * _tickRate;
        }

        Rot(wheelx, wheely, conto.X, conto.Y, Pxy, 4);
        Rot(wheely, wheelz, conto.Y, conto.Z, Pzy, 4);
        Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        var wasMtouch = false;
        var i26 = ((Scx[0] + Scx[1] + Scx[2] + Scx[3]) / (sfloat)(4.0F));
        var i27 = ((Scz[0] + Scz[1] + Scz[2] + Scz[3]) / (sfloat)(4.0F));
        for (var wheelid = 0; wheelid < 4; wheelid++)
        {
            if (Scx[wheelid] - i26 > (sfloat)(200.0F))
            {
                Scx[wheelid] = 200 + i26;
            }

            if (Scx[wheelid] - i26 < -(sfloat)(200.0F))
            {
                Scx[wheelid] = i26 - 200;
            }

            if (Scz[wheelid] - i27 > (sfloat)(200.0F))
            {
                Scz[wheelid] = 200 + i27;
            }

            if (Scz[wheelid] - i27 < -(sfloat)(200.0F))
            {
                Scz[wheelid] = i27 - 200;
            }
            
            FrameTrace.AddMessage($"Scx[{wheelid}]: {Scx[wheelid]:0.00}, Scz[{wheelid}]: {Scz[wheelid]:0.00}, Scy[{wheelid}]: {Scy[wheelid]:0.00}");
        }

        for (var i29 = 0; i29 < 4; i29++)
        {
            wheely[i29] += Scy[i29] * _tickRate;
            wheelx[i29] += (Scx[0] + Scx[1] + Scx[2] + Scx[3]) / (sfloat)(4.0F) * _tickRate;
            wheelz[i29] += (Scz[0] + Scz[1] + Scz[2] + Scz[3]) / (sfloat)(4.0F) * _tickRate;
        } //

        var surfaceType = 1;
        for (var i = 0; i < Trackers.Nt; i++) // maxine: remove trackers.sect use here
        {
            if (sfloat.Abs(Trackers.Zy[i]) != 90 && sfloat.Abs(Trackers.Xy[i]) != 90 &&
                sfloat.Abs(conto.X - Trackers.X[i]) < Trackers.Radx[i] &&
                sfloat.Abs(conto.Z - Trackers.Z[i]) < Trackers.Radz[i])
            {
                surfaceType = Trackers.Skd[i];
            }
        } //

        // maxine: we counteract the reduced bottomy from hypergliding here
        int wheelGround = GetWheelGround(this, conto, bottomy);

        if (Mtouch)
        {
            // Jacher: 1/_tickrate for traction; Txz is set on previous tick so we need to scale
            var traction = Stat.Grip;
            traction -= sfloat.Abs(Txz - conto.Xz) * (1 / _tickRate) * Speed / (sfloat)(250.0F);
            if (control.Handb)
            {
                traction -= sfloat.Abs(Txz - conto.Xz) * (1 / _tickRate) * 4;
            }

            if (traction < Stat.Grip)
            {
                if (Skid != 2)
                {
                    Skid = 1;
                }

                Speed -= Speed / (sfloat)(100.0F) * _tickRate;
            } //
            else if (Skid == 1)
            {
                Skid = 2;
            }

            if (surfaceType == 1)
            {
                traction *= (sfloat)0.75f;
            }

            if (surfaceType == 2)
            {
                traction *= (sfloat)0.55f;
            }

            var speedx = -(Speed * Sin(conto.Xz) * Cos(Pzy));
            var speedz = (Speed * Cos(conto.Xz) * Cos(Pzy));
            var speedy = -(Speed * Sin(Pzy));
            if (BadLanding || Wasted || Halted)
            {
                speedx = 0;
                speedz = 0;
                speedy = 0;
                traction = Stat.Grip / (sfloat)(5.0F);
                Speed -= (sfloat)(2.0F) * (Speed).Sign() * _tickRate;
            } //

            if (sfloat.Abs(Speed) > _drag * _tickRate)
            {
                Speed -= _drag * Speed.Sign() * _tickRate;
            }
            else
            {
                Speed = (sfloat)(0.0F);
            }

            if (Cn == 8 && traction < (sfloat)(5.0F))
            {
                traction = (sfloat)(5.0F);
            }

            if (traction < (sfloat)(1.0F))
            {
                traction = (sfloat)(1.0F);
            } //

            sfloat minTraction = (sfloat)1.0f;
            traction = sfloat.Max(traction, minTraction);

            for (var j = 0; j < 4; j++)
            {
                // maxine: traction fixes by Jacher. done slightly different but same result
                if (sfloat.Abs(Scx[j] - speedx) > traction * _tickRate)
                {
                    Scx[j] += traction * (speedx - Scx[j]).Sign() * _tickRate;
                }
                else
                {
                    Scx[j] = speedx;
                }

                if (sfloat.Abs(Scz[j] - speedz) > traction * _tickRate)
                {
                    Scz[j] += traction * (speedz - Scz[j]).Sign() * _tickRate;
                }
                else
                {
                    Scz[j] = speedz;
                }

                if (sfloat.Abs(Scy[j] - speedy) > traction * _tickRate)
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
                    if (Txz != conto.Xz)
                    {
                        _dcnt++;
                    }
                    else
                    {
                        _dcnt = 0;
                    }

                    if (_dcnt > (sfloat)(40.0F) * traction / Stat.Grip || BadLanding)
                    {
                        var f42 = (sfloat)(1.0F);
                        if (surfaceType != 0)
                        {
                            f42 = (sfloat)(1.2F);
                        }

                        if (random.NextSFloat() > (sfloat)0.65f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                f42 * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                            if ( /*Im == XTGraphics.Im &&*/ !BadLanding)
                            {
                                SfxPlaySkid(this, (surfaceType, (float)sfloat.Sqrt(Scx[j] * Scx[j] + Scz[j] * Scz[j])));
                                //XTPart2.Skidf(Im, i32,
                                //    (sfloat) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (surfaceType == 1 && random.NextSFloat() > (sfloat)0.8f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                (sfloat)1.1F * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                        }

                        if ((surfaceType == 2 || surfaceType == 3) && random.NextSFloat() > (sfloat)0.6f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                (sfloat)1.15F * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
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
                        k = (int)sfloat.Floor(random.NextSFloat() * 4); // choose 4 wheels randomly to bounce up, usually some wheel will be chosen twice, which means another wheel is not chosen, causing tilt
                    sfloat bumpLift = surfaceType == 3 ? (sfloat)(-100F) : (sfloat)(-150F);
                    sfloat rng = (sfloat)0.55F;
                    Scy[k] = bumpLift * rng * Speed / Stat.Swits[2] * (Stat.Bounce - (sfloat)0.3F);
                }
            }

            Txz = conto.Xz; // CHK1

            sfloat scxsum = 0;
            sfloat sczsum = 0;
            // 4 = nwheels
            for (int j = 0; j < 4; ++j)
            {
                scxsum += Scx[j];
                sczsum += Scz[j];
            }

            sfloat scxavg = scxsum / 4; /* nwheels */
            sfloat sczavg = sczsum / 4;
            sfloat scxz = sfloat.Hypot(sczavg, scxavg);

            Mxz = (int)(dAtan2(-scxsum, sczsum));

            if (Skid == 2)
            {
                if (!BadLanding)
                {
                    Speed = scxz * Cos(Mxz - conto.Xz) * (revspeed ? -1 : 1);
                }

                Skid = 0;
            }

            if (BadLanding && scxsum == (sfloat)(0.0F) && sczsum == (sfloat)(0.0F))
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
        sfloat groundY = 250 + wheelGround;
        sfloat wheelYThreshold = (sfloat)5f;
        sfloat f48 = (sfloat)(0.0F);
        for (var i49 = 0; i49 < 4; i49++)
        {
            isWheelGrounded[i49] = false;
            if (wheely[i49] > (groundY - (sfloat)5f))
            {
                nGroundedWheels++;
                Wtouch = true;
                Gtouch = true;
                if (!wasMtouch && Scy[i49] != (sfloat)(7.0F))
                {
                    var f50 = Scy[i49] / (sfloat)(333.33F);
                    if (f50 > (sfloat)(0.3F))
                    {
                        f50 = (sfloat)(0.3F);
                    }

                    if (surfaceType == 0)
                    {
                        f50 += (sfloat)1.1f;
                    }
                    else
                    {
                        f50 += (sfloat)1.2f;
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

        OmarTrackPieceCollision(control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, random);

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
        // i_81 = d > 1 ? 0 : (sfloat) dAcos(ratio) * sgn;
        // `d` was an unused double set to 0.0 and never used. GO figure.
        sfloat i_81 = 0;
        if (Scy[2] != Scy[0])
        {
            sfloat sgn = Scy[2] < Scy[0] ? -1 : 1;
            sfloat ratio = Hypot3(wheelz[0] - wheelz[2], wheely[0] - wheely[2], wheelx[0] - wheelx[2]) / (Math.Abs(conto.Keyz[0]) + Math.Abs(conto.Keyz[2]));
            i_81 = ratio >= 1 ? sgn : dAcos(ratio) * sgn; // the d > 1 ? 0 part was different in the original code, but this I think makes more sense
        }
        sfloat i_82 = 0;
        if (Scy[3] != Scy[1])
        {
            sfloat sgn = Scy[3] < Scy[1] ? -1 : 1;
            sfloat ratio = Hypot3(wheelz[1] - wheelz[3], wheely[1] - wheely[3], wheelx[1] - wheelx[3]) / (Math.Abs(conto.Keyz[1]) + Math.Abs(conto.Keyz[3]));
            i_82 = ratio >= 1 ? sgn : dAcos(ratio) * sgn;
        }
        sfloat i_83 = 0;
        if (Scy[1] != Scy[0])
        {
            sfloat sgn = Scy[1] < Scy[0] ? -1 : 1;
            sfloat ratio = Hypot3(wheelz[0] - wheelz[1], wheely[0] - wheely[1], wheelx[0] - wheelx[1]) / (Math.Abs(conto.Keyx[0]) + Math.Abs(conto.Keyx[1]));
            i_83 = ratio >= 1 ? sgn : dAcos(ratio) * sgn;
        }
        sfloat i_84 = 0;
        if (Scy[3] != Scy[2])
        {
            sfloat sgn = Scy[3] < Scy[2] ? -1 : 1;
            sfloat ratio = Hypot3(wheelz[2] - wheelz[3], wheely[2] - wheely[3], wheelx[2] - wheelx[3]) / (Math.Abs(conto.Keyx[2]) + Math.Abs(conto.Keyx[3]));
            i_84 = ratio >= 1 ? sgn : dAcos(ratio) * sgn;
        }

        if (hitVertical)
        {
            sfloat i_85;
            for (i_85 = sfloat.Abs(conto.Xz + 45); i_85 > 180; i_85 -= 360) { }
            _pmlt = sfloat.Abs(i_85) > 90 ? 1 : -1;
            for (i_85 = sfloat.Abs(conto.Xz - 45); i_85 > 180; i_85 -= 360) { }
            _nmlt = sfloat.Abs(i_85) > 90 ? 1 : -1;
        }

        // I think this line, among other things, is responsible for causing flatspins after glitching on the edge of a ramp
        conto.Xz += _tickRate * _forca * (Scz[0] * _nmlt - Scz[1] * _pmlt + Scz[2] * _pmlt - Scz[3] * _nmlt + Scx[0] * _pmlt + Scx[1] * _nmlt - Scx[2] * _nmlt - Scx[3] * _pmlt);

        // maxine: angle assist to make hypergliding easier
        if (!control.Left && !control.Right)
        {
            var assistxz = conto.Xz;
            while (assistxz < 0)
            {
                assistxz += (sfloat)360F;
            }

            assistxz %= (sfloat)90f;
            if (assistxz > (sfloat)89.5f || assistxz < (sfloat)0.5f)
            {
                conto.Xz = (conto.Xz / (sfloat)90.0F) * (sfloat)90.0F;
            }
        }

        if (sfloat.Abs(i_82) > sfloat.Abs(i_81))
        {
            i_81 = i_82;
        }
        if (sfloat.Abs(i_84) > sfloat.Abs(i_83))
        {
            i_83 = i_84;
        }

        // CHK11
        if (!Mtouch && py < 0/* && this.mtCount > 15*/)
        {
            var zeroanglezy = sfloat.Min(zyangle, 360 - zyangle); //distance from 0 degrees in the zy-plane
            var flipanglezy = sfloat.Abs(zyangle - 180); //distance from 180 degrees in the zy-plane
            if (zeroanglezy <= flipanglezy && zyangle < 180 || flipanglezy < zeroanglezy && zyangle >= 180) //the landing adjustment mechanism
            {
                if (Pzy > 0) //Pzy can be negative, so this needs to be accounted for
                {
                    Pzy -= QuantizeTowardsZero(sfloat.Abs(i_81) * _tickRate, _tickRate);
                }
                else
                {
                    Pzy += QuantizeTowardsZero(sfloat.Abs(i_81) * _tickRate, _tickRate);
                }
            }
            if (zeroanglezy <= flipanglezy && zyangle >= 180 || flipanglezy < zeroanglezy && zyangle < 180) //similar to above, just in reverse
            {
                if (Pzy > 0)
                {
                    Pzy += QuantizeTowardsZero(sfloat.Abs(i_81) * _tickRate, _tickRate);
                }
                else
                {
                    Pzy -= QuantizeTowardsZero(sfloat.Abs(i_81) * _tickRate, _tickRate);
                }
            }
            var zeroanglexy = sfloat.Min(xyangle, 360 - xyangle); //distance from 0 degrees in the xy-plane
            var flipanglexy = sfloat.Abs(xyangle - 180); //distance from 180 degrees in the xy-plane
            if (zeroanglexy <= flipanglexy && xyangle < 180 || flipanglexy < zeroanglexy && xyangle >= 180) //same as above, just for the xy-plane
            {
                if (Pxy > 0) //again, Pxy can be negative
                {
                    Pxy -= QuantizeTowardsZero(sfloat.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy += QuantizeTowardsZero(sfloat.Abs(i_83) * _tickRate, _tickRate);
                }
            }
            if (zeroanglexy <= flipanglexy && xyangle >= 180 || flipanglexy < zeroanglexy && xyangle < 180)
            {
                if (Pxy > 0)
                {
                    Pxy += QuantizeTowardsZero(sfloat.Abs(i_83) * _tickRate, _tickRate);
                }
                else
                {
                    Pxy -= QuantizeTowardsZero(sfloat.Abs(i_83) * _tickRate, _tickRate);
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

        sfloat newy = ((wheely[0] + wheely[1] + wheely[2] + wheely[3]) / (sfloat)4.0F - bottomy * Cos(Pzy) * Cos(Pxy) + airy);
        py = conto.Y - newy;
        conto.Y = newy;
        //conto.y = (int) ((fs_23[0] + fs_23[1] + fs_23[2] + fs_23[3]) / (sfloat)(4.0F) - (sfloat) i_10 * Cos(this.Pzy) * Cos(this.Pxy) + f_12);
        //
        if (zyinv)
            xneg = -1;
        else
            xneg = 1;

        FrameTrace.AddMessage($"x: {airx:0.00}, z: {airz:0.00}, sum: {Sin(Pxy):0.00}, sum2: {Sin(Pzy):0.00}");

        // CHK13
        // car sliding fix by jacher: do not adjust to tickrate
        conto.X = ((wheelx[0] - conto.Keyx[0] * Cos(conto.Xz) + xneg * conto.Keyz[0] * Sin(conto.Xz) +
            wheelx[1] - conto.Keyx[1] * Cos(conto.Xz) + xneg * conto.Keyz[1] * Sin(conto.Xz) +
            wheelx[2] - conto.Keyx[2] * Cos(conto.Xz) + xneg * conto.Keyz[2] * Sin(conto.Xz) +
            wheelx[3] - conto.Keyx[3] * Cos(conto.Xz) + xneg * conto.Keyz[3] * Sin(conto.Xz)) / (sfloat)4.0F
            + bottomy * Sin(Pxy) * Cos(conto.Xz) - bottomy * Sin(Pzy) * Sin(conto.Xz) + airx);

        conto.Z = ((wheelz[0] - xneg * conto.Keyz[0] * Cos(conto.Xz) - conto.Keyx[0] * Sin(conto.Xz)
            + wheelz[1] - xneg * conto.Keyz[1] * Cos(conto.Xz) - conto.Keyx[1] * Sin(conto.Xz)
            + wheelz[2] - xneg * conto.Keyz[2] * Cos(conto.Xz) - conto.Keyx[2] * Sin(conto.Xz)
            + wheelz[3] - xneg * conto.Keyz[3] * Cos(conto.Xz) - conto.Keyx[3] * Sin(conto.Xz)) / (sfloat)4.0F
            + bottomy * Sin(Pxy) * Sin(conto.Xz) - bottomy * Sin(Pzy) * Cos(conto.Xz) + airz);

        if (sfloat.Abs(Speed) > (sfloat)(10.0F) || !Mtouch)
        {
            if (sfloat.Abs(Pxy - conto.Xy) >= 4)
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
            if (sfloat.Abs(Pzy - conto.Zy) >= 4)
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
            var f87 = (Speed / (sfloat)Stat.Swits[2] * (sfloat)(14.0F) * (Stat.Bounce - (sfloat)0.4f));
            if (control.Left && _tilt < f87 && _tilt >= (sfloat)(0.0F))
            {
                _tilt += (sfloat)0.4f * _tickRate;
            }
            else if (control.Right && _tilt > -f87 && _tilt <= (sfloat)(0.0F))
            {
                _tilt -= (sfloat)0.4f * _tickRate;
            }
            else if (sfloat.Abs(_tilt) > (sfloat)3.0f * (Stat.Bounce - (sfloat)0.4f))
            {
                if (_tilt > (sfloat)(0.0F))
                {
                    _tilt -= (sfloat)3.0f * (Stat.Bounce - (sfloat)0.3f) * _tickRate;
                }
                else
                {
                    _tilt += (sfloat)3.0f * (Stat.Bounce - (sfloat)0.3f) * _tickRate;
                }
            }
            else
            {
                _tilt = (sfloat)(0.0F);
            }
            conto.Xy += _tilt * _tickRate;
            FrameTrace.AddMessage("y before tilt: " + conto.Y);
            if (Gtouch)
            {
                conto.Y -= (int)((_tilt / (sfloat)1.5f) * _tickRate);
            }
            FrameTrace.AddMessage("y after tilt: " + conto.Y);
            FrameTrace.AddMessage("tilt: " + _tilt);
        }
        else if (_tilt != (sfloat)(0.0F))
        {
            _tilt = (sfloat)(0.0F);
        }
        if (Wtouch && surfaceType == 2)
        {
            conto.Zy += (int)(((sfloat)random.NextSFloat() * (sfloat)6.0F * Speed / Stat.Swits[2] - (sfloat)3.0F * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (sfloat)0.3f));
            conto.Xy += (int)(((sfloat)random.NextSFloat() * (sfloat)6.0F * Speed / Stat.Swits[2] - (sfloat)3.0F * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (sfloat)0.3f));
        }
        if (Wtouch && surfaceType == 1)
        {
            conto.Zy += (int)(((sfloat)random.NextSFloat() * (sfloat)4.0F * Speed / Stat.Swits[2] - (sfloat)2.0F * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (sfloat)0.3f));
            conto.Xy += (int)(((sfloat)random.NextSFloat() * (sfloat)4.0F * Speed / Stat.Swits[2] - (sfloat)2.0F * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (sfloat)0.3f));
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
                    if (sfloat.Abs(conto.Z - CheckPoints.Z[i92]) <
                        (sfloat)(60.0F) + sfloat.Abs(Scz[0] + Scz[1] + Scz[2] + Scz[3]) / (sfloat)(4.0F) &&
                        sfloat.Abs(conto.X - CheckPoints.X[i92]) < 700 &&
                        sfloat.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
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
                    if (sfloat.Abs(conto.X - CheckPoints.X[i92]) <
                        (sfloat)(60.0F) + sfloat.Abs(Scx[0] + Scx[1] + Scx[2] + Scx[3]) / (sfloat)(4.0F) &&
                        sfloat.Abs(conto.Z - CheckPoints.Z[i92]) < 700 &&
                        sfloat.Abs(conto.Y - CheckPoints.Y[i92] + 350) < 450 &&
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
                        if (sfloat.Abs(conto.Z - CheckPoints.Fz[i93]) < 200 && Py(conto.X / 100,
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
                    else if (sfloat.Abs(conto.X - CheckPoints.Fx[i93]) < 200 && Py(conto.Z / 100,
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
                Travxy += (int)((Rcomp - Lcomp) * _tickRate);
                if (Math.Abs(Travxy) > 135)
                {
                    Rtab = true;
                }
                Travzy += (int)((Ucomp - Dcomp) * _tickRate);
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
            if (_srfcnt < (10 * 1/_tickRate))
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
                        Powerup = (sfloat)(0.0F);
                        if (sfloat.Abs(Travxy) > 90)
                        {
                            Powerup += sfloat.Abs(Travxy) / (sfloat)(24.0F);
                        }
                        else if (Rtab)
                        {
                            Powerup += (sfloat)(30.0F);
                        }
                        if (sfloat.Abs(Travzy) > 90)
                        {
                            Powerup += sfloat.Abs(Travzy) / (sfloat)(18.0F);
                        }
                        else
                        {
                            if (Ftab)
                            {
                                Powerup += (sfloat)(40.0F);
                            }
                            if (Btab)
                            {
                                Powerup += (sfloat)(40.0F);
                            }
                        }
                        if (sfloat.Abs(Travxz) > 90)
                        {
                            Powerup += sfloat.Abs(Travxz) / (sfloat)(18.0F);
                        }
                        if (Surfer)
                        {
                            Powerup += (sfloat)(30.0F);
                        }
                        Power += Powerup;
                        /*if (Im == XTGraphics.Im && (int) Powerup > Record.Powered && Record.Wasted == 0 &&
                            (Powerup > (sfloat)(60.0F) || CheckPoints.Stage == 1 || CheckPoints.Stage == 2))
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
                        if (Power > (sfloat)(98.0F))
                        {
                            Power = (sfloat)(98.0F);
                            if (Powerup > (sfloat)(150.0F))
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
                        if (sfloat.Abs(Scz[i96]) < (sfloat)(70.0F) && sfloat.Abs(Scx[i96]) < (sfloat)(70.0F))
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
                        Speed = (sfloat)(0.0F);
                        conto.Y += Stat.Flipy;
                        Pxy += 180;
                        conto.Xy += 180;
                        Capcnt = 0;
                    }
                }
            }
            if (Trcnt == 0 && Speed != (sfloat)(0.0F))
            {
                if (_xtpower == 0)
                {
                    if (Power > (sfloat)(0.0F))
                    {
                        Power -= (Power * Power * Power / Stat.Powerloss) * _tickRate;
                    }
                    else
                    {
                        Power = (sfloat)(0.0F);
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
                    Record.Whenwasted = (int) ((sfloat)(185.0F) + RandomSFloat() * (sfloat)(20.0F));
                }
            }
        }*/
    }

    private static void Rot(Span<sfloat> a, Span<sfloat> b, sfloat offA, sfloat offB, sfloat angle, int len)
    {
        if (angle != 0)
        {
            var sin = Sin(angle);
            var cos = Cos(angle);
            
            for (var i = 0; i < len; i++)
            {
                var pa = a[i];
                var pb = b[i];
                var oa = (pa - offA);
                var ob = (pb - offB);
                a[i] = offA + (oa * cos - ob * sin);
                b[i] = offB + (oa * sin + ob * cos);
            }
        }
    }

    public static int SafeAbs(int value) => value >= 0 ? value : (value == int.MinValue ? int.MaxValue : -value);
    public static sfloat SafeAbs(sfloat value) => value >= 0 ? value : (value == sfloat.MinValue ? sfloat.MaxValue : -value);

    // input: number of grounded wheels to medium
    // output: hitVertical when colliding against a wall
    private void OmarTrackPieceCollision(Control control, ContO conto, Span<sfloat> wheelx, Span<sfloat> wheely, Span<sfloat> wheelz,
        sfloat groundY, sfloat wheelYThreshold, sfloat wheelGround, ref int nGroundedWheels, bool wasMtouch, int surfaceType, out bool hitVertical, Span<bool> isWheelGrounded, DeterministicRandom random)
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
                if (isWheelGrounded[k] && BadLanding && (Trackers.Skd[j] == 0 || Trackers.Skd[j] == 1) && wheelx[k] > (sfloat) (Trackers.X[j] - Trackers.Radx[j]) && wheelx[k] < (sfloat) (Trackers.X[j] + Trackers.Radx[j]) && wheelz[k] > (sfloat) (Trackers.Z[j] - Trackers.Radz[j]) && wheelz[k] < (sfloat) (Trackers.Z[j] + Trackers.Radz[j])) {
                    conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                    SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                }

                // find the first piece that I am colliding with, snap wheel to it and stop
                if ( // CHK3
                    !isWheelTouchingPiece[k] &&
                    pointInBox(wheelx[k], wheely[k] - wheelGround, wheelz[k], Trackers.X[j], Trackers.Y[j], Trackers.Z[j], Trackers.Radx[j], Trackers.Rady[j], Trackers.Radz[j])
                   )
                {
                    // ignore y == groundY because those are likely road pieces, which could make us break the loop early and miss a ramp
                    // this is also the reason why on the ground road and ramp ordering does not matter
                    // but when using floating pieces, you have to make sure the ramp comes first in the code
                    if (Trackers.Xy[j] == 0 && Trackers.Zy[j] == 0 && Trackers.Y[j] != groundY - wheelGround && wheely[k] - wheelGround > Trackers.Y[j] - wheelYThreshold)
                    {
                        ++nGroundedWheels;
                        Wtouch = true;
                        Gtouch = true;

                        // more dust stuff
                        if (!wasMtouch && Scy[k] != (sfloat)(7.0F) /* * checkpoints.gravity */ * _tickRate)
                        { //Phy-addons: Recharged mode
                            sfloat f_59 = Scy[k] / (sfloat)(333.33F);
                            if (f_59 > (sfloat)(0.3F))
                                f_59 = (sfloat)(0.3F);
                            if (surfaceType == 0)
                                f_59 += (sfloat)1.1f;
                            else
                                f_59 += (sfloat)1.2f;
                            conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_59 * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                        }

                        wheely[k] = Trackers.Y[j] + wheelGround; // snap wheel to the surface

                        // sparks and scrape
                        if (BadLanding && (Trackers.Skd[j] == 0 || Trackers.Skd[j] == 1))
                        {
                            conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                            //if (Im == /*this.xt.im*/ 0)
                            SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                        }

                        bounceRebound(k, conto, random);
                        isWheelTouchingPiece[k] = true;
                    } // CHK4
                    // here we handle cases where zy is -90
                    // we are checking that we are approaching the surface from the "solid" side
                    // remember that surfaces in NFM are only 1 sided, going the other way doesnt
                    // result in a collision
                    // I don't know why the radz 287 part is there, this smells like there is some
                    // particular piece with radz 287 with special behavior
                    if (Trackers.Zy[j] == -90 && wheelz[k] < Trackers.Z[j] + Trackers.Radz[j] && (Scz[k] < (sfloat)(0.0F) /*|| Trackers.radz[j] == 287*/))
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
                        if (Trackers.Skd[j] == 5 && random.NextSFloat() > (sfloat)0.5f)
                            _crank[0, k]++;
                        if (_crank[0, k] > 1)
                        {
                            conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                            //if (Im == /*this.xt.im*/ 0)
                            SfxPlayScrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                        }

                        // z rebound CHK5
                        bounceReboundZ(j, k, conto, wasMtouch/*, Trackers, checkpoints*/, random);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j]) {
                            control.Wall = j;
                        }
                    }
                    if (Trackers.Zy[j] == 90 && wheelz[k] > Trackers.Z[j] - Trackers.Radz[j] && (Scz[k] > (sfloat)(0.0F) /*|| Trackers.radz[j] == 287*/))
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
                        if (Trackers.Skd[j] == 5 && random.NextSFloat() > (sfloat)0.5f)
                            _crank[1, k]++;
                        if (_crank[1, k] > 1)
                        {
                            conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                            //if (this.im == this.xt.im)
                            SfxPlayScrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                        }

                        bounceReboundZ(j, k, conto, wasMtouch/*, Trackers, checkpoints*/, random);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j]) {
                            control.Wall = j;
                        }
                    } // CHK6
                    if (Trackers.Xy[j] == -90 && wheelx[k] < Trackers.X[j] + Trackers.Radx[j] && (Scx[k] < (sfloat)(0.0F) /*|| Trackers.radx[j] == 287*/))
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
                        if (Trackers.Skd[j] == 5 && random.NextSFloat() > (sfloat)0.5f)
                            _crank[2, k]++;
                        if (_crank[2, k] > 1)
                        {
                            conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                            //if (this.im == this.xt.im)
                            SfxPlayScrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                        }

                        bounceReboundX(j, k, conto, wasMtouch/*, Trackers, checkpoints*/, random);

                        Skid = 2;
                        hitVertical = true;
                        isWheelTouchingPiece[k] = true;
                        if (!Trackers.Notwall[j]) {
                            control.Wall = j;
                        }
                    } // CHK7
                    if (Trackers.Xy[j] == 90 && wheelx[k] > Trackers.X[j] - Trackers.Radx[j] && (Scx[k] > (sfloat)(0.0F) /*|| Trackers.radx[j] == 287*/))
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
                        if (Trackers.Skd[j] == 5 && random.NextSFloat() > (sfloat)0.5f)
                            _crank[3, k]++;
                        if (_crank[3, k] > 1)
                        {
                            conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                            //if (this.im == this.xt.im)
                            SfxPlayScrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                        }

                        bounceReboundX(j, k, conto, wasMtouch/*, Trackers, checkpoints*/, random);

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
                        sfloat ry = Trackers.Y[j] + wheelGround + ((wheely[k] - Trackers.Y[j] - wheelGround) * Cos(pAngle) - (wheelz[k] - Trackers.Z[j]) * Sin(pAngle));
                        sfloat rz = Trackers.Z[j] + ((wheely[k] - Trackers.Y[j] - wheelGround) * Sin(pAngle) + (wheelz[k] - Trackers.Z[j]) * Cos(pAngle));

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
                        if (rz > (Trackers.Z[j] /*&& rz < Trackers.Z[j] + 200*/))
                        {
                            sfloat maxZy = 50;
                            sfloat liftDivider = (sfloat)(1.0F) + (maxZy - sfloat.Abs(Trackers.Zy[j])) / (sfloat)(30.0F);
                            liftDivider = sfloat.Max(liftDivider, (sfloat)(1.0F)); // this implies we shouldn't make ramps with surfaces steeper than 50
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
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                //if (this.im == this.xt.im)
                                SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                            }

                            // dust
                            if (!wasMtouch && surfaceType != 0)
                            {
                                sfloat f_73 = (sfloat)1.4F;
                                conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_73 * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                            }
                        }

                        // interestingly if ry and rz are what they were on initialization, these assignments do nothing
                        // it seems the intention with this whole block was to rotate the t -> w vector, manipulate it
                        // then rotate back. If the surface being intersected is not ramping us up, then no changes are
                        // made to the vector and the rotation back just reverses the previous operation, making no changes
                        wheely[k] = Trackers.Y[j] + wheelGround + ((ry - Trackers.Y[j] - wheelGround) * Cos(-pAngle) - (rz - Trackers.Z[j]) * Sin(-pAngle));
                        wheelz[k] = Trackers.Z[j] + ((ry - Trackers.Y[j] - wheelGround) * Sin(-pAngle) + (rz - Trackers.Z[j]) * Cos(-pAngle));

                        isWheelTouchingPiece[k] = true;
                    } // CHK9
                    if (Trackers.Xy[j] != 0 && Trackers.Xy[j] != 90 && Trackers.Xy[j] != -90)
                    {
                        int pAngle = 90 + Trackers.Xy[j];

                        sfloat ry = Trackers.Y[j] + wheelGround + ((wheely[k] - Trackers.Y[j] - wheelGround) * Cos(pAngle) - (wheelx[k] - Trackers.X[j]) * Sin(pAngle));
                        sfloat rx = Trackers.X[j] + ((wheely[k] - Trackers.Y[j] - wheelGround) * Sin(pAngle) + (wheelx[k] - Trackers.X[j]) * Cos(pAngle));
                        if (rx > Trackers.X[j] /*&& rx < Trackers.X[j] + 200*/)
                        {
                            sfloat maxXy = 50;
                            sfloat liftDivider = (sfloat)(1.0F) + (maxXy - sfloat.Abs(Trackers.Xy[j])) / (sfloat)(30.0F);
                            liftDivider = sfloat.Max(liftDivider, (sfloat)(1.0F));
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
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                //if (this.im == this.xt.im)
                                SfxPlayGscrape(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                            }

                            if (!wasMtouch && surfaceType != 0)
                            {
                                sfloat f_78 = (sfloat)1.4F;
                                conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], f_78 * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                            }
                        }

                        wheely[k] = Trackers.Y[j] + wheelGround + ((ry - Trackers.Y[j] - wheelGround) * Cos(-pAngle) - (rx - Trackers.X[j]) * Sin(-pAngle));
                        wheelx[k] = Trackers.X[j] + ((ry - Trackers.Y[j] - wheelGround) * Sin(-pAngle) + (rx - Trackers.X[j]) * Cos(-pAngle));

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

    private int Regx(int i, sfloat f, ContO conto, DeterministicRandom random)
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
        if (sfloat.Abs(f) > (sfloat)(100.0F))
        {
            //Record.Recx(i, f, Im);
            if (f > (sfloat)(100.0F))
            {
                f -= (sfloat)(100.0F);
            }
            if (f < -(sfloat)(100.0F))
            {
                f += (sfloat)(100.0F);
            }
            Shakedam = (int)((sfloat.Abs(f) + Shakedam) / (sfloat)2.0F);
            if (/*Im == XTGraphics.Im*/true || _colidim)
            {
                SfxPlayCrash(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i111 = 0; i111 < 40; i111++)
            {
                var f112 = (sfloat)(0.0F);
                for (var i113 = 0; i113 < 4; i113++)
                {
                    f112 = f / (sfloat)20.0F * (sfloat)random.NextSFloat();
                    if (abool)
                    {
                        Hitmag += (int)sfloat.Abs(f112);
                        i110 += (int)sfloat.Abs(f112);
                    }
                }
            }
        }
        return i110;
    }

    private int Regy(int i, sfloat f, ContO conto, DeterministicRandom random)
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
        if (f > (sfloat)(100.0F))
        {
            //Record.Recy(i, f, Mtouch, Im);
            f -= (sfloat)(100.0F);
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
                Shakedam = (int)((sfloat.Abs(f) + Shakedam) / (sfloat)(2.0F));
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
                    var f103 = (sfloat)(0.0F);
                    for (var i104 = 0; i104 < 4; i104++)
                    {
                        f103 = f / (sfloat)20.0F * (sfloat)random.NextSFloat();
                        if (abool)
                        {
                            Hitmag += (int)sfloat.Abs(f103);
                            i97 += (int)sfloat.Abs(f103);
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
                        var f108 = (sfloat)(0.0F);
                        for (var i109 = 0; i109 < 4; i109++)
                        {
                            f108 = f / (sfloat)15.0F * (sfloat)random.NextSFloat();
                            i105 += (int)f108;
                            i106++;
                            if (abool)
                            {
                                Hitmag += (int)sfloat.Abs(f108);
                                i97 += (int)sfloat.Abs(f108);
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

    private int Regz(int i, sfloat f, ContO conto, DeterministicRandom random)
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
        if (sfloat.Abs(f) > (sfloat)(100.0F))
        {
            //Record.Recz(i, f, Im);
            if (f > (sfloat)(100.0F))
            {
                f -= (sfloat)(100.0F);
            }
            if (f < -(sfloat)(100.0F))
            {
                f += (sfloat)(100.0F);
            }
            Shakedam = (int)((sfloat.Abs(f) + Shakedam) / (sfloat)2.0F);
            
            if (/*Im == XTGraphics.Im ||*/true || _colidim)
            {
                SfxPlayCrash(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i115 = 0; i115 < 40; i115++)
            {
                var f116 = (sfloat)(0.0F);
                for (var i117 = 0; i117 < 4; i117++)
                {
                    f116 = f / (sfloat)20.0F * (sfloat)random.NextSFloat();
                    if (abool)
                    {
                        Hitmag += (int)sfloat.Abs(f116);
                        i114 += (int)sfloat.Abs(f116);
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
        Speed = (sfloat)(0.0F);
        for (var i1 = 0; i1 < 4; i1++)
        {
            Scy[i1] = (sfloat)(0.0F);
            Scx[i1] = (sfloat)(0.0F);
            Scz[i1] = (sfloat)(0.0F);
        }
        _forca = (sfloat.Sqrt(conto.Keyz[0] * conto.Keyz[0] + conto.Keyx[0] * conto.Keyx[0]) +
                  sfloat.Sqrt(conto.Keyz[1] * conto.Keyz[1] + conto.Keyx[1] * conto.Keyx[1]) +
                  sfloat.Sqrt(conto.Keyz[2] * conto.Keyz[2] + conto.Keyx[2] * conto.Keyx[2]) +
                  sfloat.Sqrt(conto.Keyz[3] * conto.Keyz[3] + conto.Keyx[3] * conto.Keyx[3])) / (sfloat)(10000.0F) *
                 (Stat.Bounce - (sfloat)0.3f);
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
        Ucomp = (sfloat)(0.0F);
        Dcomp = (sfloat)(0.0F);
        Lcomp = (sfloat)(0.0F);
        Rcomp = (sfloat)(0.0F);
        _lxz = 0;
        Travxy = 0;
        Travzy = 0;
        Travxz = 0;
        Rtab = false;
        Ftab = false;
        Btab = false;
        Powerup = (sfloat)(0.0F);
        _xtpower = 0;
        Trcnt = 0;
        Capcnt = 0;
        _tilt = (sfloat)(0.0F);
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
        Power = (sfloat)(98.0F);
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

    private static int Rpy(sfloat x1, sfloat x2, sfloat y1, sfloat y2, sfloat z1, sfloat z2)
    {
        return (int)((x1 - x2) * (x1 - x2) + (y1 - y2) * (y1 - y2) + (z1 - z2) * (z1 - z2));
    }

    public static sfloat Hypot3(sfloat x, sfloat y, sfloat z)
    {
        return sfloat.Sqrt(x * x + y * y + z * z);
    }

    public static sfloat dAcos(sfloat a)
    {
        return sfloat.Acos(a) * sfloat.RadToDeg;
    }

    public static sfloat dAtan2(sfloat y, sfloat x)
    {
        return sfloat.Atan2(y, x) * sfloat.RadToDeg;
    }

    public static sfloat QuantizeTowardsZero(sfloat value, sfloat step)
    {
        // Scale by step size
        sfloat scaled = value / step;

        // Truncate towards zero
        sfloat truncated = scaled > 0 ? sfloat.Floor(scaled) : sfloat.Ceiling(scaled);

        // Scale back
        return truncated * step;
    }
}