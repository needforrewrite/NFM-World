using System.Runtime.CompilerServices;
using FixedMathSharp;
using FixedMathSharp.Utility;
using Lua;
using Microsoft.Extensions.Logging;
using nfm_world_library.Lua;
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

// public struct CollisionSubstep
// {
//     public InlineArray4<fix64> wheelx;
//     public InlineArray4<fix64> wheely;
//     public InlineArray4<fix64> wheelz;
// }

[LuaVisible]
public partial class CarPhysics
{
    public enum SurfaceType
    {
        Road = 0,
        OffTrack = 1,
        OffRoad = 2,
        Bump = 3,
        BumpySides = 4,
        Spikes = 5
    }
    
    private static readonly fix64 _tickRate = Physics.PHYSICS_MULTIPLIER_F64;
    private static readonly fix64 _oneOverTickRate = 1 / _tickRate;
    
    [LuaName("halted")]
    public bool Halted = false;

    [LuaName("btab")]
    public bool BackwardsTabletop;
    
    [LuaName("capcnt")]
    public int CapsizedCounter;
    
    [LuaName("capsized")]
    public bool BadLanding;
    
    [LuaName("caught")]
    public readonly UnlimitedArray<bool> _caught = [];
    
    [LuaName("stat")]
    public CarStats Stat;

    [LuaName("cn")]
    public int Cn;
    
    [LuaName("cntdest")]
    public int Cntdest;
    
    [LuaName("cntouch")]
    public int _cntouch;
    
    /// <summary>
    /// Is colliding with the client player car
    /// </summary>
    [LuaName("colliding_with_client_player")]
    public bool _collidingWithClientPlayer;
    
    public readonly int[,] _crank = new int[4, 4];
    
    public readonly int[,] _lcrank = new int[4, 4];
    
    [LuaName("cxz")]
    public fix64 Cxz;
    
    [LuaName("static_camera_xz")]
    public fix64 StaticCameraXz;
    
    [LuaName("dcnt")]
    public int _dcnt;
    
    [LuaName("dcomp")]
    public fix64 DownComponent;
    
    [LuaName("lcomp")]
    public fix64 LeftComponent;
    
    [LuaName("wasted")]
    public bool Wasted;
    
    [LuaName("dominate")]
    public readonly UnlimitedArray<bool> _dominate = [];
    
    [LuaName("drag")]
    public readonly fix64 _drag = fix64.Half;
    
    [LuaName("fixes")]
    public int _fixes = -1;
    
    [LuaName("forca")]
    public fix64 _forca;
    
    [LuaName("ftab")]
    public bool ForwardTabletop;
    
    [LuaName("turn_xz")]
    public fix64 _turnXz;
    
    [LuaName("gtouch")]
    public bool Gtouch;
    
    [LuaName("hitmag")]
    public int DamagePoints;
    
    [LuaName("im")]
    public int Im;
    
    [LuaName("lastcolido")]
    public int Lastcolido;
    
    [LuaName("loop")]
    public sbyte StuntState;
    
    [LuaName("lxz")]
    public fix64 _lxz;
    
    [LuaName("mtouch")]
    public bool Mtouch;
    
    [LuaName("mxz")]
    public fix64 Mxz;
    
    [LuaName("num_roof_damage")]
    public int _numRoofDamage;
    
    [LuaName("newcar")]
    public bool Newcar;
    
    [LuaName("newedcar")]
    public int Newedcar;
    
    [LuaName("nmlt")]
    public int _nmlt = 1;
    
    [LuaName("nofocus")]
    public bool Nofocus;
    
    [LuaName("outshakedam")]
    public int Outshakedam = 0;
    
    [LuaName("pd")]
    public bool PressDown;
    
    [LuaName("pl")]
    public bool PressLeft;
    
    [LuaName("pmlt")]
    public int _pmlt = 1;
    
    [LuaName("point")]
    public int Point;
    
    [LuaName("power")]
    public fix64 Power = 98;
    
    [LuaName("powerup")]
    public fix64 Powerup;
    
    [LuaName("pr")]
    public bool PressRight;
    
    [LuaName("pu")]
    public bool PressUp;
    
    [LuaName("pushed")]
    public bool Pushed;

    [LuaName("pxy")]
    public fix64 Pxy;
    
    [LuaName("pzy")]
    public fix64 Pzy;
    
    [LuaName("rcomp")]
    public fix64 RightComponent;
    
    [LuaName("rtab")]
    public bool RightTabletop;
    
    [LuaName("scx")]
    public LuaArray<fix64> Scx = new(4);
    
    [LuaName("scy")]
    public LuaArray<fix64> Scy = new(4);
    
    [LuaName("scz")]
    public LuaArray<fix64> Scz = new(4);
    
    [LuaName("shakedam")]
    public int Shakedam;
    
    [LuaName("skid")]
    public sbyte Skid;
    
    [LuaName("speed")]
    public fix64 Speed;
    
    [LuaName("roof_damage")]
    public int RoofDamage;
    
    [LuaName("surf_count")]
    public int _surfCount;
    
    [LuaName("surfing")]
    public bool Surfing;
    
    [LuaName("tilt")]
    public fix64 _tilt;
    
    [LuaName("total_stunt_xy")]
    public fix64 TotalStuntXy;
    
    [LuaName("total_stunt_xz")]
    public fix64 TotalStuntXz;
    
    [LuaName("total_stunt_zy")]
    public fix64 TotalStuntZy;
    
    [LuaName("tcnt")]
    public int TabletopCounter;
    
    [LuaName("txz")]
    public fix64 Txz;
    
    [LuaName("ucomp")]
    public fix64 UpComponent;
    
    [LuaName("wtouch")]
    public bool Wtouch;
    
    [LuaName("xtpower")]
    public int _xtpower;

    [LuaName("is_client_player")]
    internal bool IsClientPlayer;
    
    [LuaName("mtcount")]
    internal int Mtcount = 0;
    
    [LuaName("py")]
    internal fix64 py = 0;

    [LuaHidden] public event EventHandler<(float f, int i)>? SfxPlayCrash;
    [LuaHidden] public event EventHandler<(SurfaceType i, float f)>? SfxPlaySkid;
    [LuaHidden] public event EventHandler<(int i, int i2, int i3)>? SfxPlayScrape;
    [LuaHidden] public event EventHandler<(int i, int i2, int i3)>? SfxPlayGscrape;
    [LuaHidden] public event EventHandler<float>? PowerUp;

    private static f64Vector3 Up => new(0, -1, 0);
    private static f64Vector3 Forward => new(0, 0, 1);
    private static f64Vector3 Right => new(1, 0, 0);

    // private InlineArray2<CollisionSubstep> collisionSubsteps;
    // private bool collisionSubstepSwitch; // if false: [0] is current, if true: [1] is current
    // private const int NumSubsteps = 2;
    //
    // // Gets collision substeps in order [previous, current]
    // private void GetCollisionSubsteps(out InlineArray2<CollisionSubstep> substeps)
    // {
    //     if (collisionSubstepSwitch)
    //     {
    //         substeps = collisionSubsteps;
    //         return;
    //     }
    //     substeps = new InlineArray2<CollisionSubstep>();
    //     substeps[0] = collisionSubsteps[1];
    //     substeps[1] = collisionSubsteps[0];
    // }
    //
    // // Call this at the end of the collision step to set the current substep's wheel positions for use in the next tick's collisions
    // private void SetCurrentCollisionSubstep(in CollisionSubstep currentSubstep)
    // {
    //     if (collisionSubstepSwitch)
    //     {
    //         collisionSubsteps[0] = currentSubstep;
    //     }
    //     else
    //     {
    //         collisionSubsteps[1] = currentSubstep;
    //     }
    //
    //     collisionSubstepSwitch = !collisionSubstepSwitch;
    // }

    public CarPhysics(CarStats stat, int im, bool isClientPlayer)
    {
        Stat = stat;
        Im = im;
        IsClientPlayer = isClientPlayer;
    }

    [LuaName("collide")]
    public void Collide(IInGameCar self, CarPhysics othermad, IInGameCar other)
    {
        ContO conto = new ContO(self);
        ContO otherconto = new ContO(other);
        
        var random = new DeterministicRandom((ulong)(conto.X.rawValue ^ otherconto.X.rawValue ^ conto.Z.rawValue ^ otherconto.Z.rawValue ^ conto.Y.rawValue ^ otherconto.Y.rawValue));
        
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
                var ownMoment = fix64.Abs(Power * Speed * Stat.Moment);
                var otherMoment = fix64.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment);
                if (fix64.Abs(ownMoment - otherMoment) > (fix64)0.001f)
                {
                    _dominate[othermad.Im] = ownMoment > otherMoment;
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
            var a = (Scz[0] - othermad.Scz[0] + Scz[1] - othermad.Scz[1] + Scz[2] - othermad.Scz[2] + Scz[3] - othermad.Scz[3]);
            var b = (Scx[0] - othermad.Scx[0] + Scx[1] - othermad.Scx[1] + Scx[2] - othermad.Scx[2] + Scx[3] - othermad.Scx[3]);
            var impactMagnitude = (int) ((a * a + b * b) / 16);
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
                            if (f130 < -300)
                            {
                                f130 = -300;
                            }
                            var f131 = Scx[wheel] * Stat.Push;
                            if (f131 > 300)
                            {
                                f131 = 300;
                            }
                            if (f131 < -300)
                            {
                                f131 = -300;
                            }
                            othermad.Scx[otherwheel] += f131;
                            if (IsClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = true;
                            }
                            totalOtherDamage += othermad.Regx(otherwheel, f131 * Stat.Moment * damageMult, otherconto, random);
                            if (othermad._collidingWithClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = false;
                            }
                            Scx[wheel] -= f130;
                            totalOwnDamage += Regx(wheel, -f130 * Stat.Moment * damageMult, conto, random);
                            Scy[wheel] -= Stat.Revlift;
                            if (IsClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = true;
                            }
                            totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
                            if (othermad._collidingWithClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = false;
                            }
                            if (UMath.RandomBoolean())
                            {
                                otherconto.Spark(
                                    (wheelx[wheel] + otherwheelx[otherwheel]) * fix64.Half, 
                                    (wheely[wheel] + otherwheely[otherwheel]) * fix64.Half,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) * fix64.Half, 
                                    (othermad.Scx[otherwheel] + Scx[wheel]) * fix64.Quarter,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) * fix64.Quarter,
                                    (othermad.Scz[otherwheel] + Scz[wheel]) * fix64.Quarter,
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
                            if (f132 < -300)
                            {
                                f132 = -300;
                            }
                            var f133 = Scz[wheel] * Stat.Push;
                            if (f133 > 300)
                            {
                                f133 = 300;
                            }
                            if (f133 < -300)
                            {
                                f133 = -300;
                            }
                            othermad.Scz[otherwheel] += f133;
                            if (IsClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = true;
                            }
                            totalOtherDamage += othermad.Regz(otherwheel, f133 * Stat.Moment * damageMult, otherconto, random);
                            if (othermad._collidingWithClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = false;
                            }
                            Scz[wheel] -= f132;
                            totalOwnDamage += Regz(wheel, -f132 * Stat.Moment * damageMult, conto, random);
                            Scy[wheel] -= Stat.Revlift;
                            if (IsClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = true;
                            }
                            totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
                            if (othermad._collidingWithClientPlayer)
                            {
                                othermad._collidingWithClientPlayer = false;
                            }
                            if (UMath.RandomBoolean())
                            {
                                otherconto.Spark(
                                    (wheelx[wheel] + otherwheelx[otherwheel]) * fix64.Half, 
                                    (wheely[wheel] + otherwheely[otherwheel]) * fix64.Half,
                                    (wheelz[wheel] + otherwheelz[otherwheel]) * fix64.Half,
                                    (othermad.Scx[otherwheel] + Scx[wheel]) * fix64.Quarter,
                                    (othermad.Scy[otherwheel] + Scy[wheel]) * fix64.Quarter, 
                                    (othermad.Scz[otherwheel] + Scz[wheel]) * fix64.Quarter,
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

    private static int GetWheelGround(CarPhysics carPhysics, ContO conto, fix64 bottomy)
    {
        int wheelGround;
        if (World.IsHyperglidingEnabled)
        {
            wheelGround = (int)((bottomy * _oneOverTickRate) * (fix64.One - _tickRate));
            if (!carPhysics.BadLanding)
            {
                wheelGround = -wheelGround;
            }
        }
        else
        {
            wheelGround = carPhysics.BadLanding ? carPhysics.Stat.Flipy + carPhysics.RoofDamage : -conto.Grat;
        }

        return wheelGround;
    }

    private static fix64 GetBottomY(CarPhysics carPhysics, ContO conto)
    {
        fix64 bottomy;
        if (World.IsHyperglidingEnabled)
        {
            if (carPhysics.BadLanding)
            {
                bottomy = (carPhysics.Stat.Flipy + carPhysics.RoofDamage) * _tickRate;
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

    public event EventHandler? Distruct;

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
            Scy[wi] = (fix64)(-1) * Scy[wi] * (rebound - fix64.One);
    }

    [LuaName("drive")]
    public void Drive(Control control, IInGameCar car, IStage stage)
    {
        ContO conto = new ContO(car);
        DeterministicRandom random = new((ulong)(conto.X.rawValue ^ conto.Y.rawValue ^ conto.Z.rawValue));

        FrameTrace.AddMessage($"position: {conto.X:0.00},{conto.Y:0.00},{conto.Z:0.00}");
        FrameTrace.AddMessage($"xz: {conto.Xz:0.00}, mxz: {Mxz:0.00}, lxz: {_lxz:0.00}, fxz: {_turnXz:0.00}, cxz: {Cxz:0.00}");
        FrameTrace.AddMessage($"xy: {conto.Xy:0.00}, pxy: {Pxy:0.00}, zy: {conto.Zy:0.00}, pzy: {Pzy:0.00}, xz: {conto.Xz:0.00}");
        FrameTrace.AddMessage($"Travxz: {TotalStuntXz:0.00}, Travxy: {TotalStuntXy:0.00}, Travzy: {TotalStuntZy:0.00}, Surfing: {Surfing}");

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
            StuntState = 0;
        }

        if (StuntState == 0)
        {
            StaticCameraXz = conto.Xz * xneg;
        }

        if (Wtouch)
        {
            if (StuntState == 2 || StuntState == -1)
            {
                StuntState = -1;
                if (control.Left)
                {
                    PressLeft = true;
                }

                if (control.Right)
                {
                    PressRight = true;
                }

                if (control.Up)
                {
                    PressUp = true;
                }

                if (control.Down)
                {
                    PressDown = true;
                }
            }

            UpComponent = 0;
            DownComponent = 0;
            LeftComponent = 0;
            RightComponent = 0;
        } //

        if (control.Handb)
        {
            if (!Pushed)
            {
                if (!Wtouch)
                {
                    if (StuntState == 0)
                    {
                        StuntState = 1;
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

        if (StuntState == 1)
        {
            var f13 = (Scy[0] + Scy[1] + Scy[2] + Scy[3]) * fix64.Quarter;
            for (var i14 = 0; i14 < 4; i14++)
            {
                Scy[i14] = f13;
            }

            StuntState = 2;
        } //

        if (!Wasted)
        {
            if (StuntState == 2)
            {
                if (control.Up)
                {
                    if (UpComponent == 0)
                    {
                        UpComponent = 10 + (Scy[0] + 50) / 20;
                        if (UpComponent < 5)
                        {
                            UpComponent = 5;
                        }

                        if (UpComponent > 10)
                        {
                            UpComponent = 10;
                        }

                        UpComponent *= Stat.Airs;
                    }

                    if (UpComponent < 20)
                    {
                        UpComponent += fix64.Half * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Sin(conto.Xz) * zneg * _tickRate;
                    airz = Stat.Airc * UMath.Cos(conto.Xz) * zneg * _tickRate;
                }
                else if (UpComponent != 0 && UpComponent > -2)
                {
                    UpComponent -= fix64.Half * Stat.Airs * _tickRate; //
                }

                if (control.Down)
                {
                    if (DownComponent == 0)
                    {
                        DownComponent = 10 + (Scy[0] + 50) / 20;
                        if (DownComponent < 5)
                        {
                            DownComponent = 5;
                        }

                        if (DownComponent > 10)
                        {
                            DownComponent = 10;
                        }

                        DownComponent *= Stat.Airs;
                    }

                    if (DownComponent < 20)
                    {
                        DownComponent += fix64.Half * Stat.Airs * _tickRate; //
                    }

                    airy = -Stat.Airc * _tickRate;
                }
                else if (DownComponent != 0 && UpComponent > -2)
                {
                    DownComponent -= fix64.Half * Stat.Airs * _tickRate;
                } //

                if (control.Left)
                {
                    if (LeftComponent == 0)
                    {
                        LeftComponent = 5;
                    }

                    if (LeftComponent < 20) // maxine: scale to tickrate
                    {
                        LeftComponent += 2 * Stat.Airs * _tickRate; //
                    }

                    airx = -Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = -Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (LeftComponent > 0)
                {
                    LeftComponent -= 2 * Stat.Airs * _tickRate; //
                }

                if (control.Right) //
                {
                    if (RightComponent == 0)
                    {
                        RightComponent = 5;
                    }

                    if (RightComponent < 20) // maxine: scale to tickrate
                    {
                        RightComponent += 2 * Stat.Airs * _tickRate;
                    }

                    airx = Stat.Airc * UMath.Cos(conto.Xz) * xneg * _tickRate;
                    airz = Stat.Airc * UMath.Sin(conto.Xz) * xneg * _tickRate;
                }
                else if (RightComponent > 0) //
                {
                    RightComponent -= 2 * Stat.Airs * _tickRate;
                }

                Pzy = UMath.QuantizeTowardsZero((Pzy + (DownComponent - UpComponent) * UMath.Cos(Pxy) * _tickRate), _tickRate); //
                if (zyinv)
                {
                    conto.Xz = UMath.QuantizeTowardsZero(conto.Xz + ((DownComponent - UpComponent) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }
                else
                {
                    conto.Xz = UMath.QuantizeTowardsZero(conto.Xz - ((DownComponent - UpComponent) * UMath.Sin(Pxy) * _tickRate), _tickRate);
                }

                Pxy = UMath.QuantizeTowardsZero((Pxy + (RightComponent - LeftComponent) * _tickRate), _tickRate);
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
                            Speed -= (Stat.Acelf.AsSpan()[i16] * fix64.Half + f15 * Stat.Acelf.AsSpan()[i16] / 196) * _tickRate;
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
                            Speed += (Stat.Acelf.AsSpan()[i18] * fix64.Half + f15 * Stat.Acelf.AsSpan()[i18] / 196) * _tickRate;
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

                if (StuntState == -1 && conto.Y < 100)
                {
                    if (control.Left)
                    {
                        if (!PressLeft)
                        {
                            if (LeftComponent == 0)
                            {
                                LeftComponent = 5 * Stat.Airs * _tickRate;
                            }

                            if (LeftComponent < 20)
                            {
                                LeftComponent += 2 * Stat.Airs * _tickRate;
                            }
                        }
                    } //
                    else
                    {
                        if (LeftComponent > 0)
                        {
                            LeftComponent -= 2 * Stat.Airs * _tickRate;
                        }

                        PressLeft = false;
                    } //

                    if (control.Right)
                    {
                        if (!PressRight)
                        {
                            if (RightComponent == 0)
                            {
                                RightComponent = 5 * Stat.Airs * _tickRate;
                            }

                            if (RightComponent < 20)
                            {
                                RightComponent += 2 * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (RightComponent > 0)
                        {
                            RightComponent -= 2 * Stat.Airs * _tickRate;
                        }

                        PressRight = false;
                    } //

                    if (control.Up)
                    {
                        if (!PressUp)
                        {
                            if (UpComponent == 0)
                            {
                                UpComponent = 5 * Stat.Airs * _tickRate;
                            }

                            if (UpComponent < 20)
                            {
                                UpComponent += 2 * Stat.Airs * _tickRate;
                            }
                        } //
                    }
                    else
                    {
                        if (UpComponent > 0)
                        {
                            UpComponent -= 2 * Stat.Airs * _tickRate;
                        }

                        PressUp = false;
                    }

                    if (control.Down)
                    {
                        if (!PressDown)
                        {
                            if (DownComponent == 0)
                            {
                                DownComponent = 5 * Stat.Airs * _tickRate;
                            }

                            if (DownComponent < 20)
                            {
                                DownComponent += 2 * Stat.Airs * _tickRate;
                            }
                        }
                    }
                    else
                    {
                        if (DownComponent > 0)
                        {
                            DownComponent -= 2 * Stat.Airs * _tickRate;
                        }

                        PressDown = false;
                    }

                    Pzy = UMath.QuantizeTowardsZero((Pzy + ((DownComponent - UpComponent) * UMath.Cos(Pxy)) * _tickRate), _tickRate);
                    if (zyinv)
                    {
                        conto.Xz = UMath.QuantizeTowardsZero(conto.Xz + (((DownComponent - UpComponent) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }
                    else
                    {
                        conto.Xz = UMath.QuantizeTowardsZero(conto.Xz - (((DownComponent - UpComponent) * UMath.Sin(Pxy)) * _tickRate), _tickRate);
                    }

                    Pxy = UMath.QuantizeTowardsZero((Pxy + (RightComponent - LeftComponent) * _tickRate), _tickRate);
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
            conto.WheelXz -= ((fix64)Stat.Turn * _tickRate);
            if (conto.WheelXz < -Stat.TurnRadius)
            {
                conto.WheelXz = -Stat.TurnRadius;
            }
        }

        if (control.Left)
        {
            conto.WheelXz += ((fix64)Stat.Turn * _tickRate);
            if (conto.WheelXz > Stat.TurnRadius)
            {
                conto.WheelXz = Stat.TurnRadius;
            }
        } //

        if (conto.WheelXz != 0 && !control.Left && !control.Right)
        {
            if (fix64.Abs(Speed) < 10)
            {
                if (fix64.Abs(conto.WheelXz) == 1)
                {
                    conto.WheelXz = 0;
                }

                if (conto.WheelXz > 0)
                {
                    conto.WheelXz--; // tick rate for this stuff?
                }

                if (conto.WheelXz < 0)
                {
                    conto.WheelXz++;
                }
            }
            else
            {
                if (fix64.Abs(conto.WheelXz) < Stat.Turn * 2)
                {
                    conto.WheelXz = 0;
                }

                if (conto.WheelXz > 0)
                {
                    conto.WheelXz -= ((fix64)Stat.Turn * 2 * _tickRate);
                }

                if (conto.WheelXz < 0)
                {
                    conto.WheelXz += ((fix64)Stat.Turn * 2 * _tickRate);
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
                    _turnXz = conto.WheelXz / (i21 * 3);
                }
                else
                {
                    _turnXz = conto.WheelXz / i21;
                }

                conto.Xz += (conto.WheelXz / i21 * _tickRate);
            }

            Wtouch = false;
            Gtouch = false;
        }
        else
        {
            conto.Xz += (_turnXz * _tickRate);
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
                Cxz += (Mxz - Cxz) * fix64.Quarter * _tickRate; //
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
        var i26 = ((Scx[0] + Scx[1] + Scx[2] + Scx[3]) * fix64.Quarter);
        var i27 = ((Scz[0] + Scz[1] + Scz[2] + Scz[3]) * fix64.Quarter);
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
            wheelx[i29] += (Scx[0] + Scx[1] + Scx[2] + Scx[3]) * fix64.Quarter * _tickRate;
            wheelz[i29] += (Scz[0] + Scz[1] + Scz[2] + Scz[3]) * fix64.Quarter * _tickRate;
        } //

        var surfaceType = SurfaceType.OffRoad;
        var surfaceTracMul = fix64.One;
        foreach (var collidable in stage.RetrievePointCollidables(conto.X, conto.Z))
        {
            if (collidable.TryGetValue(out ShapeRoad boxRoad))
            {
                // bumps don't have rady defined so it is 0
                // the collision check that was here only checks x and z and allows y to be anything
                // this means if there is a floating road over a bumpy side road, you still hit the bumps on the road above
                // to fix this fix the bumpy side models to have some proper rady and propagate the rady value instead of 10^9
                var rad = new f64Vector3(boxRoad.Radius.X, 1000000000, boxRoad.Radius.Z);
                var trackersPosition = boxRoad.TrackersPosition;
                var contoXz = boxRoad.GameObjectXz;
                var contoPosition = boxRoad.GameObjectPosition;
                var position = new f64Vector3(conto.X, conto.Y, conto.Z);
                var theBox = new CollisionBox(rad, trackersPosition, contoXz, contoPosition);
                if (theBox.ResolveCollision(position) is not null)
                {
                    surfaceType = collidable.SurfaceType;
                    surfaceTracMul = collidable.TractionMultiplier;
                }
            }
        }

        // maxine: we counteract the reduced bottomy from hypergliding here
        int wheelGround = GetWheelGround(this, conto, bottomy);

        if (Mtouch)
        {
            // Jacher: 1/_tickrate for traction; Txz is set on previous tick so we need to scale
            var traction = Stat.Grip;
            traction -= fix64.Abs(Txz - conto.Xz) * (_oneOverTickRate) * Speed / 250;
            if (control.Handb)
            {
                traction -= fix64.Abs(Txz - conto.Xz) * (_oneOverTickRate) * 4;
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

            if (surfaceTracMul != fix64.One)
            {
                traction *= surfaceTracMul;
            }
            else if (surfaceType == SurfaceType.Road)
            {
                traction *= Stat.RoadGrip ?? 1;
            }
            else if (surfaceType == SurfaceType.OffTrack)
            {
                traction *= Stat.OffTrackGrip ?? (fix64)0.75f;
            }
            else if (surfaceType == SurfaceType.OffRoad)
            {
                traction *= Stat.OffRoadGrip ?? (fix64)0.55f;
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
                        if (surfaceType != SurfaceType.Road)
                        {
                            f42 = (fix64)(1.2F);
                        }

                        if (random.NextFixed6401() > (fix64)0.65f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                f42 * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                            if (IsClientPlayer && !BadLanding)
                            {
                                SfxPlaySkid?.Invoke(this, (surfaceType, (float)fix64.Sqrt(Scx[j] * Scx[j] + Scz[j] * Scz[j])));
                                //XTPart2.Skidf(Im, i32,
                                //    (fix64) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (surfaceType == SurfaceType.OffTrack && random.NextFixed6401() > (fix64)0.8f)
                        {
                            conto.Dust(j, wheelx[j], wheely[j], wheelz[j], (int)Scx[j], (int)Scz[j],
                                (fix64)1.1F * Stat.Simag, (int)_tilt, BadLanding && Mtouch, wheelGround);
                        }

                        if ((surfaceType == SurfaceType.OffRoad || surfaceType == SurfaceType.Bump) && random.NextFixed6401() > (fix64)0.6f)
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

                if (surfaceType == SurfaceType.Bump || surfaceType == SurfaceType.BumpySides)
                {
                    int k = random.Next(4); // choose 4 wheels randomly to bounce up, usually some wheel will be chosen twice, which means another wheel is not chosen, causing tilt
                    fix64 bumpLift = surfaceType == SurfaceType.Bump ? -100 : -150;
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

            fix64 scxavg = scxsum * fix64.Quarter; /* nwheels */
            fix64 sczavg = sczsum * fix64.Quarter;
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
                surfaceType = SurfaceType.Road;
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
        var isWheelGrounded = new InlineArray4<bool>();
        var wheelContactNormal = new InlineArray4<f64Vector3>();
        fix64 groundY = 250 + wheelGround;
        fix64 wheelYThreshold = (fix64)5f;
        fix64 f48 = 0;
        for (var i49 = 0; i49 < 4; i49++)
        {
            isWheelGrounded[i49] = false;
            if (wheely[i49] > (groundY - (fix64)5))
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

                    if (surfaceType == SurfaceType.Road)
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
                wheelContactNormal[i49] = Up;

                bounceRebound(i49, conto, random);
            }
        }

        // OmarTrackPieceCollision(control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, random);
        PhyTrackPieceCollision(stage, control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, wheelContactNormal, random);

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

        if (hitVertical)
        {
            fix64 i;
            for (i = fix64.Abs(conto.Xz + 45); i > 180; i -= 360) { }
            _pmlt = fix64.Abs(i) > 90 ? 1 : -1;
            for (i = fix64.Abs(conto.Xz - 45); i > 180; i -= 360) { }
            _nmlt = fix64.Abs(i) > 90 ? 1 : -1;
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
            if (assistxz > (fix64)89.5f || assistxz < fix64.Half)
            {
                conto.Xz = fix64.Round(conto.Xz / 90) * 90;
            }
            FrameTrace.AddMessage($"assistxz: {assistxz:0.00}, conto.Xz: {conto.Xz:0.00}");
        }

        // Surface orientation from plane fitting.
        
        var nWheelsOnSurface = 0;
        if (isWheelGrounded[0]) nWheelsOnSurface++;
        if (isWheelGrounded[1]) nWheelsOnSurface++;
        if (isWheelGrounded[2]) nWheelsOnSurface++;
        if (isWheelGrounded[3]) nWheelsOnSurface++;

        // Only invoke plane fit when at least one wheel is grounded, or when bumping (Scy is uneven)
        // Commented out because this seems to be unnecessary, the plane-fit is well behaved in the air.
        // Uncomment it if the plane-fit starts misbehaving in the air.
        // Update: Uncommented because Audi reported it still happening.
        // Update 2: Remove nWheelsOnSurface > 0 condition. This makes bouncing more predictable (more
        // like original NFM), at the cost of worse behavior on surfaces. 
        if (Scy[0] != Scy[1] || Scy[0] != Scy[2] || Scy[0] != Scy[3])
        {
            var wheelpos = new InlineArray4<f64Vector3>();

            for (var i = 0; i < 4; i++)
            {
                wheelpos[i] = new f64Vector3(wheelx[i], wheely[i] - wheelGround, wheelz[i]);
            }

            var terrainNormal1 = f64Vector3.Cross(
                wheelpos[1] - wheelpos[0],
                wheelpos[2] - wheelpos[0]
            ).Normal;

            var terrainNormal2 = f64Vector3.Cross(
                wheelpos[3] - wheelpos[1],
                wheelpos[2] - wheelpos[1]
            ).Normal;

            var terrainNormal = (terrainNormal1 + terrainNormal2).Normal;

            // Half-space check: terrainNormal must point in the same hemisphere
            // as the car's up. Only the Y component matters — X and Z depend on
            // yaw (conto.Xz), which a naive localUp at Xz=0 gets wrong when
            // Xz ≠ 0 (e.g. facing -Z on a steep ramp).
            //   terrainNormal.Y < 0  ⟹  world-up    ⟹  car should be upright
            //   car's up.Y = -cos(Pxy)·cos(Pzy) < 0 ⟹  upright; > 0 ⟹ inverted
            // Flip the normal if its Y sign differs from the car's up-Y sign.
            var carUpY = -UMath.Cos(Pxy) * UMath.Cos(Pzy);
            var needsFlip = terrainNormal.Y * carUpY < fix64.Zero;
            FrameTrace.AddMessage($"flipCheck: tn.Y={terrainNormal.Y:0.000}, carUpY={carUpY:0.000}, needsFlip={needsFlip}, Pxy={Pxy:0.0}, Pzy={Pzy:0.0}");
            if (needsFlip)
                terrainNormal = -terrainNormal;

            // When |terrainNormal.Y| ≈ 0 the cross-product plane fit is
            // degenerate — at Pzy≈±90° all wheels share the same Z, at
            // Pxy≈±90° they share the same X.  Fall back to computing
            // pitch / roll directly from wheel height differences, which
            // works at any orientation.
            if (fix64.Abs(terrainNormal.Y) < (fix64)0.05f)
            {
                var avgFrontY = (wheelpos[0].Y + wheelpos[1].Y) * fix64.Half;
                var avgRearY  = (wheelpos[2].Y + wheelpos[3].Y) * fix64.Half;
                var avgLeftY  = (wheelpos[0].Y + wheelpos[2].Y) * fix64.Half;
                var avgRightY = (wheelpos[1].Y + wheelpos[3].Y) * fix64.Half;

                var wheelbase = fix64.Abs(conto.Keyz[0] - conto.Keyz[2]);
                var track    = fix64.Abs(conto.Keyx[0] - conto.Keyx[1]);

                var pitchTarget = fix64.Atan2(avgFrontY - avgRearY, wheelbase) * fix64.RadToDeg;
                var rollTarget  = fix64.Atan2(avgLeftY - avgRightY, track)    * fix64.RadToDeg;

                // Unwrap targets relative to current conto angles
                while (pitchTarget - conto.Zy > 180) pitchTarget -= 360;
                while (pitchTarget - conto.Zy < -180) pitchTarget += 360;
                while (rollTarget - conto.Xy > 180) rollTarget -= 360;
                while (rollTarget - conto.Xy < -180) rollTarget += 360;

                // Nudge toward the target at a limited rate to prevent
                // oscillation from frame-to-frame noise.
                var maxDelta = (fix64)5;
                Pzy = FixedMathSharp.FixedMath.MoveTowards(Pzy, pitchTarget, maxDelta);
                Pxy = FixedMathSharp.FixedMath.MoveTowards(Pxy, rollTarget, maxDelta);

                FrameTrace.AddMessage(
                    $"wheelFit: frontY={avgFrontY:0.0}, rearY={avgRearY:0.0}, leftY={avgLeftY:0.0}, rightY={avgRightY:0.0}, "
                    + $"pitchT={pitchTarget:0.0}°, rollT={rollTarget:0.0}°, → Pxy={Pxy:0.0}°, Pzy={Pzy:0.0}°");
            }
            else
            {
                // Undo yaw before decomposing into Pxy/Pzy.
                // Rotation order: Rx(Pxy)·Rz(Pzy)·Ry(Xz) applied to up=(0,-1,0):
                //   up = (sinP·cosXz + cosP·sinZ·sinXz,  -cosP·cosZ,  sinP·sinXz - cosP·sinZ·cosXz)
                // Solving for Pxy, Pzy given up=terrainNormal and Xz:
                var cosXz = UMath.Cos(conto.Xz);
                var sinXz = UMath.Sin(conto.Xz);
                var sinP = terrainNormal.X * cosXz + terrainNormal.Z * sinXz;
                var cosP_sinZ = terrainNormal.X * sinXz - terrainNormal.Z * cosXz;
                var cosP_cosZ = -terrainNormal.Y; // = cos(Pxy)·cos(Pzy)

                // |cos(Pxy)| = sqrt(cosP_cosZ² + cosP_sinZ²)  because cos²Z+sin²Z=1
                var absCosP = fix64.Sqrt(cosP_cosZ * cosP_cosZ + cosP_sinZ * cosP_sinZ);

                // Guard: when |cos(Pxy)| ≈ 0 (car within a few degrees of ±90°
                // roll) the decomposition amplifies noise — sinZ/cosZ both divide
                // by cosP ≈ 0.  Skip and let stabilizers hold.
                if (absCosP > (fix64)0.05f)
                {
                    // cosP_cosZ = cos(Pxy)·cos(Pzy) = -terrainNormal.Y
                    //   cosP_cosZ > 0 → upright hemisphere → cosP > 0
                    //   cosP_cosZ < 0 → one of cosP, cosZ is negative.
                    //
                    // Use the same detection the xyinv/zyinv flags use to decide which
                    // axis carries the inversion: if the raw Pzy is past ±90°, Pzy
                    // is inverted so cosZ < 0 and cosP > 0. Otherwise Pxy is inverted
                    // so cosP < 0.
                    //
                    // Recompute the raw angles here because loop controls may have
                    // changed Pxy/Pzy since the top of the tick.
                    var rawXy = fix64.Abs(Pxy);
                    while (rawXy > 270) rawXy -= 360;
                    rawXy = fix64.Abs(rawXy);
                    var rawZy = fix64.Abs(Pzy);
                    while (rawZy > 270) rawZy -= 360;
                    rawZy = fix64.Abs(rawZy);

                    var cosP = cosP_cosZ >= fix64.Zero
                        ? (rawXy > (fix64)90 && rawZy > (fix64)90 ? -absCosP : absCosP)
                        : rawZy > (fix64)90 ? absCosP       // Pzy is the inverted axis → cosP > 0
                            : rawXy > (fix64)90 ? -absCosP       // Pxy is the inverted axis → cosP < 0
                                : absCosP;                            // neither > 90°, assume upright

                    // Derive sin(Pzy) and cos(Pzy) by dividing the known products
                    // by cosP — this correctly undoes the 180° shift that atan2
                    // would introduce when cosP < 0.
                    var sinZ = cosP_sinZ / cosP;
                    var cosZ = cosP_cosZ / cosP;

                    // Guard: when |cosZ| ≈ 0 (Pzy near ±90°), the plane-fit can't
                    // reliably determine Pzy — tiny noise in wheel positions flips
                    // atan2 between +90° and -90°.  Only update Pxy.
                    if (fix64.Abs(cosP_cosZ) > (fix64)0.05f)
                        Pzy = fix64.Atan2(sinZ, cosZ) * fix64.RadToDeg;
                    Pxy = fix64.Atan2(sinP, cosP) * fix64.RadToDeg;

                    // Unwrap so Pxy/Pzy stay within 180° of conto.Xy/conto.Zy.
                    // atan2 outputs [-180°, 180°] which wraps at ±180°; the
                    // interpolation block below would see a 358° jump instead of 2°.
                    while (Pxy - conto.Xy > 180) Pxy -= 360;
                    while (Pxy - conto.Xy < -180) Pxy += 360;
                    while (Pzy - conto.Zy > 180) Pzy -= 360;
                    while (Pzy - conto.Zy < -180) Pzy += 360;

                    FrameTrace.AddMessage($"terrainFit: cosP_cosZ={cosP_cosZ:0.000}, rawXy={rawXy:0.0}, rawZy={rawZy:0.0}, cosP={cosP:0.000}, sinP={sinP:0.000}, → Pxy={Pxy:0.0}°, Pzy={Pzy:0.0}°");
                }
            }
        }

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

        fix64 newy = ((wheely[0] + wheely[1] + wheely[2] + wheely[3]) * fix64.Quarter - bottomy * UMath.Cos(Pzy) * UMath.Cos(Pxy) + airy);
        py = conto.Y - newy;
        conto.Y = newy;
        //conto.y = (int) ((fs_23[0] + fs_23[1] + fs_23[2] + fs_23[3]) * fix64.Quarter - (fix64) i_10 * Cos(this.Pzy) * Cos(this.Pxy) + f_12);
        //
        if (zyinv)
            xneg = -1;
        else
            xneg = 1;

        FrameTrace.AddMessage($"airx: {airx:0.00}, airz: {airz:0.00}, sum: {UMath.Sin(Pxy):0.00}, sum2: {UMath.Sin(Pzy):0.00}");

        // CHK13
        // car sliding fix by jacher: do not adjust to tickrate
        conto.X = ((wheelx[0] - conto.Keyx[0] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[0] * UMath.Sin(conto.Xz) +
            wheelx[1] - conto.Keyx[1] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[1] * UMath.Sin(conto.Xz) +
            wheelx[2] - conto.Keyx[2] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[2] * UMath.Sin(conto.Xz) +
            wheelx[3] - conto.Keyx[3] * UMath.Cos(conto.Xz) + xneg * conto.Keyz[3] * UMath.Sin(conto.Xz)) * fix64.Quarter
            + bottomy * UMath.Sin(Pxy) * UMath.Cos(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Sin(conto.Xz) + airx);

        conto.Z = ((wheelz[0] - xneg * conto.Keyz[0] * UMath.Cos(conto.Xz) - conto.Keyx[0] * UMath.Sin(conto.Xz)
            + wheelz[1] - xneg * conto.Keyz[1] * UMath.Cos(conto.Xz) - conto.Keyx[1] * UMath.Sin(conto.Xz)
            + wheelz[2] - xneg * conto.Keyz[2] * UMath.Cos(conto.Xz) - conto.Keyx[2] * UMath.Sin(conto.Xz)
            + wheelz[3] - xneg * conto.Keyz[3] * UMath.Cos(conto.Xz) - conto.Keyx[3] * UMath.Sin(conto.Xz)) * fix64.Quarter
            + bottomy * UMath.Sin(Pxy) * UMath.Sin(conto.Xz) - bottomy * UMath.Sin(Pzy) * UMath.Cos(conto.Xz) + airz);

        if (fix64.Abs(Speed) > 10 || !Mtouch)
        {
            // if (fix64.Abs(Pxy - conto.Xy) >= 4)
            // {
            //     if (Pxy > conto.Xy)
            //     {
            //         conto.Xy += (2 + (Pxy - conto.Xy) * fix64.Half);
            //     }
            //     else
            //     {
            //         conto.Xy -= (2 + (conto.Xy - Pxy) * fix64.Half);
            //     }
            // }
            // else
            {
                conto.Xy = Pxy;
            }
            // if (fix64.Abs(Pzy - conto.Zy) >= 4)
            // {
            //     if (Pzy > conto.Zy)
            //     {
            //         conto.Zy += (2 + (Pzy - conto.Zy) * fix64.Half);
            //     }
            //     else
            //     {
            //         conto.Zy -= (2 + (conto.Zy - Pzy) * fix64.Half);
            //     }
            // }
            // else
            {
                conto.Zy = Pzy;
            }
            FrameTrace.AddMessage($"AFT xy: {conto.Xy:0.00}, pxy: {Pxy:0.00}, zy: {conto.Zy:0.00}, pzy: {Pzy:0.00}, xz: {conto.Xz:0.00}");
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
        if (Wtouch && surfaceType == SurfaceType.OffRoad)
        {
            conto.Zy += (int)((random.NextFixed6401() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            conto.Xy += (int)((random.NextFixed6401() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
        }
        if (Wtouch && surfaceType == SurfaceType.OffTrack)
        {
            conto.Zy += (int)((random.NextFixed6401() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            conto.Xy += (int)((random.NextFixed6401() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
        } // CHK15
        if (DamagePoints >= Stat.Maxmag && !Wasted)
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
            if (TabletopCounter != 1)
            {
                TabletopCounter = 1;
                _lxz = conto.Xz;
            }
            if (StuntState == 2 || StuntState == -1)
            {
                TotalStuntXy += ((RightComponent - LeftComponent) * _tickRate);
                if (fix64.Abs(TotalStuntXy) > 135)
                {
                    RightTabletop = true;
                }
                TotalStuntZy += ((UpComponent - DownComponent) * _tickRate);
                if (TotalStuntZy > 135)
                {
                    ForwardTabletop = true;
                }
                if (TotalStuntZy < -135)
                {
                    BackwardsTabletop = true;
                }
            }
            if (_lxz != conto.Xz)
            {
                TotalStuntXz += (_lxz - conto.Xz) * _tickRate;
                _lxz = conto.Xz;
            }
            if (_surfCount < (10 * (_oneOverTickRate)))
            {
                if (control.Wall != -1)
                {
                    Surfing = true;
                }
                _surfCount++;
            }
        }
        else if (!Wasted)
        {
            if (!BadLanding)
            {
                if (CapsizedCounter != 0)
                {
                    CapsizedCounter = 0;
                }
                if (Gtouch && TabletopCounter != 0)
                {
                    if (TabletopCounter == 9)
                    {
                        bool JustSurfer = true;
                        Powerup = 0;
                        if (fix64.Abs(TotalStuntXy) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(TotalStuntXy) / 24;
                        }
                        else if (RightTabletop)
                        {
                            JustSurfer = false;
                            Powerup += 30;
                        }
                        if (fix64.Abs(TotalStuntZy) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(TotalStuntZy) / 18;
                        }
                        else
                        {
                            if (ForwardTabletop)
                            {
                                JustSurfer = false;
                                Powerup += 40;
                            }
                            if (BackwardsTabletop)
                            {
                                JustSurfer = false;
                                Powerup += 40;
                            }
                        }
                        if (fix64.Abs(TotalStuntXz) > 90)
                        {
                            JustSurfer = false;
                            Powerup += fix64.Abs(TotalStuntXz) / 18;
                        }
                        if (Surfing)
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
                                _xtpower = (int)(200 * _oneOverTickRate);
                            }
                            else
                            {
                                _xtpower = (int)(100 * _oneOverTickRate);
                            }
                        }
                    } // CHK17
                    if (TabletopCounter == 10)
                    {
                        TotalStuntXy = 0;
                        TotalStuntZy = 0;
                        TotalStuntXz = 0;
                        ForwardTabletop = false;
                        RightTabletop = false;
                        BackwardsTabletop = false;
                        TabletopCounter = 0;
                        _surfCount = 0;
                        Surfing = false;
                    }
                    else
                    {
                        TabletopCounter++;
                    }
                }
            }
            else
            {
                if (TabletopCounter != 0)
                {
                    TotalStuntXy = 0;
                    TotalStuntZy = 0;
                    TotalStuntXz = 0;
                    ForwardTabletop = false;
                    RightTabletop = false;
                    BackwardsTabletop = false;
                    TabletopCounter = 0;
                    _surfCount = 0;
                    Surfing = false;
                }
                if (CapsizedCounter == 0)
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
                        CapsizedCounter = 1;
                    }
                }
                else
                {
                    CapsizedCounter++;
                    if (CapsizedCounter == 30)
                    {
                        Speed = 0;
                        conto.Y += Stat.Flipy;
                        Pxy += 180;
                        conto.Xy += 180;
                        CapsizedCounter = 0;
                    }
                }
            }
            if (TabletopCounter == 0 && Speed != 0)
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
    private void PhyTrackPieceCollision(
        IStage stage, Control control, ContO conto,
        Span<fix64> wheelx, Span<fix64> wheely, Span<fix64> wheelz,
        fix64 groundY, fix64 wheelYThreshold, fix64 wheelGround, ref int nGroundedWheels, bool wasMtouch,
        SurfaceType surfaceType, out bool hitVertical, Span<bool> isWheelGrounded, Span<f64Vector3> wheelContactNormal,
        DeterministicRandom random)
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
                FrameTrace.AddMessage("start wheel");
                foreach (var collidable in stage.RetrievePointCollidables(wheelx[k], wheelz[k]))
                {
                    if (collidable.TryGetValue(out ShapeMesh boxMesh))
                    {
                        var collisionMesh = boxMesh.CollisionMesh;

                        // Transform wheel into object-local space (1 transform per mesh, not 3 per triangle)
                        var localPosition = (position - boxMesh.GameObjectPosition).RotateXz(-boxMesh.GameObjectXz);
                        var localVelocity = velocity.RotateXz(-boxMesh.GameObjectXz);

                        for (var i = 0; i < collisionMesh.Indices.Length; i += 3)
                        {
                            // Vertices are already in object-local space — no transform needed
                            var p0 = collisionMesh.Vertices[collisionMesh.Indices[i]];
                            var p1 = collisionMesh.Vertices[collisionMesh.Indices[i + 1]];
                            var p2 = collisionMesh.Vertices[collisionMesh.Indices[i + 2]];
                            
                            if (!TriangleMesh.PointInTriangleAABB(collisionMesh.Aabb[i / 3], localPosition)) continue;

                            var edge1 = p1 - p0;
                            var edge2 = p2 - p0;
                            var normal = f64Vector3.Cross(edge1, edge2);
                            // Compute length via float to avoid fix64 overflow on large triangles
                            var floatLength = normal.LengthNoOverflow();
                            if (floatLength < (fix64)1e-3f) continue; // degenerate triangle
                            var normalizedNormal = new f64Vector3(normal.X / floatLength, normal.Y / floatLength, normal.Z / floatLength);
                            var groundness = -normal.Y / floatLength;
                            var toPoint = localPosition - p0;
                            var triangleData = new TriangleMesh.TriangleData(edge1, edge2, normalizedNormal, groundness, toPoint);

                            if (k == 0)
                            {
                                // Find closest triangle center to wheel in XZ (local space)
                                var center = (p0 + p1 + p2) / (fix64)3;
                                var dxz = fix64.Sqrt((center.X - localPosition.X) * (center.X - localPosition.X) + (center.Z - localPosition.Z) * (center.Z - localPosition.Z));
                                if ((float)dxz < 500 && groundness > (fix64)0.3f)
                                {
                                    var inTri = TriangleMesh.DebugPointInTriangle(edge1, edge2, toPoint);
                                    var surfaceY = fix64.Abs(normalizedNormal.Y) > (fix64)1e-6
                                        ? p0.Y - (normalizedNormal.X * (localPosition.X - p0.X) + normalizedNormal.Z * (localPosition.Z - p0.Z)) / normalizedNormal.Y
                                        : 999;
                                    FrameTrace.AddMessage($"TRI[{i/3}] p0=({(float)p0.X:F0},{(float)p0.Y:F0},{(float)p0.Z:F0}) inTri={inTri} surfY={(float)surfaceY:F0} localWheel=({(float)localPosition.X:F0},{(float)localPosition.Y:F0},{(float)localPosition.Z:F0})");
                                }
                            }
                            
                            // Ground/ramp triangle: snap wheel Y to surface (local space, then convert back)
                            if (triangleData.IsGround)
                            {
                                if (TriangleMesh.ResolveGround(p0, p1, p2, localPosition, triangleData) is { } groundHit)
                                {
                                    FrameTrace.AddMessage(triangleData.IsGround
                                        ? $"ground triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2}, groundness {(float)groundness:F2})"
                                        : $"wall triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2})");

                                    touching |= 1 << k;
                                    ++nGroundedWheels;
                                    
                                    isWheelGrounded[k] = true;
                                    // normalizedNormal is in object-local XZ space; RotateXz brings it to world space.
                                    // groundness > 0 ⟹ normalizedNormal.Y < 0, which is the -Y (up) direction in Y-down. ✓
                                    wheelContactNormal[k] = normalizedNormal.RotateXz(boxMesh.GameObjectXz);
                                    
                                    Wtouch = true;
                                    Gtouch = true;

                                    // Lift: reduce downward velocity proportional to ramp penetration depth
                                    // Matches BoxRamp's Scy[k] -= zTmp / liftDivider logic
                                    var zTmp = localPosition.Y - groundHit.newY;
                                    // if (zTmp > 0 && zTmp < 200)
                                    {
                                        var rampAngleDeg = fix64.Acos(fix64.Clamp(triangleData.Groundness, -1, 1)) * fix64.RadToDeg;
                                        var liftDivider = 1 + (50 - fix64.Abs(rampAngleDeg)) / (fix64)30;
                                        if (liftDivider < 4) liftDivider = 4;
                                        FrameTrace.AddMessage($"ramp lift: {zTmp} liftDivider: {liftDivider:F2} total: {zTmp / liftDivider:F2}");
                                        Scy[k] -= zTmp / liftDivider;
                                    }

                                    if (!wasMtouch && Scy[k] != 7 /* * checkpoints.gravity */ * _tickRate)
                                    {
                                        fix64 dustMag = Scy[k] / (fix64)(333.33F);
                                        if (dustMag > (fix64)(0.3F))
                                            dustMag = (fix64)(0.3F);
                                        if (surfaceType == SurfaceType.Road)
                                            dustMag += (fix64)1.1f;
                                        else
                                            dustMag += (fix64)1.2f;
                                        conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k],
                                            dustMag * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                                    }

                                    // newY is in local space; RotateXz doesn't affect Y, so just add object Y
                                    wheely[k] = groundHit.newY + boxMesh.GameObjectPosition.Y + wheelGround;
                                    // TODO: this makes going up mesh ramps janky. but we ideally want to allow bouncing on mesh collisions, so we need a better solution 
                                    // bounceRebound(k, conto, random);
                                    isWheelTouchingPiece[k] = true;
                                    // break; this makes it possible to phase through walls when on top of a raised mesh ground, but prevents being snapped out the side of a ramp, as long as the ground collision happens first
                                }
                            }
                            else
                            {
                                // Wall triangle: horizontal push-back (in local space, then rotate back)
                                if (TriangleMesh.ResolveWall(p0, p1, p2, localPosition, localVelocity, triangleData) is { } wallHit)
                                {
                                    FrameTrace.AddMessage(triangleData.IsGround
                                        ? $"ground triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2}, groundness {(float)groundness:F2})"
                                        : $"wall triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2})");

                                    // Rotate local-space push/impact back to world space
                                    var worldDelta = wallHit.positionDelta.RotateXz(boxMesh.GameObjectXz);
                                    var worldImpact = wallHit.impactComponent.RotateXz(boxMesh.GameObjectXz);

                                    for (int w = 0; w < 4; w++)
                                    {
                                        wheelx[w] += worldDelta.X;
                                        wheelz[w] += worldDelta.Z;
                                    }

                                    _crank[0, k]++;
                                    if (_crank[0, k] > 1)
                                    {
                                        conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0,
                                            (int)wheelGround);

                                        if (IsClientPlayer)
                                        {
                                            SfxPlayScrape?.Invoke(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                                        }
                                    }

                                    var reboundVelocityDelta = worldImpact * (-GetReboundMul(wasMtouch));
                                    Regz(k, reboundVelocityDelta.Length() * 1, conto, random);
                                    Scx[k] += reboundVelocityDelta.X;
                                    Scz[k] += reboundVelocityDelta.Z;

                                    hitVertical = true;
                                    isWheelTouchingPiece[k] = true;
                                    // break; this makes it possible to phase through walls when on top of a raised mesh ground, but prevents being snapped out the side of a ramp, as long as the ground collision happens first
                                }
                            }
                        }
                    }
                    else if (collidable.TryGetValue(out ShapeHull boxHull))
                    {
                        // TODO later
                    }
                    else if (collidable.TryGetValue(out ShapeRoad boxRoad))
                    {
                        if (boxRoad.ResolveCollision(position) is { } collision)
                        {
                            touching |= 1 << k;
                            ++nGroundedWheels;
                            isWheelGrounded[k] = true;
                            wheelContactNormal[k] = Up;
                            Wtouch = true;
                            Gtouch = true;

                            if (!wasMtouch && Scy[k] != 7 /* * checkpoints.gravity */ * _tickRate)
                            {
                                fix64 dustMag = Scy[k] / (fix64)(333.33F);
                                if (dustMag > (fix64)(0.3F))
                                    dustMag = (fix64)(0.3F);
                                if (surfaceType == SurfaceType.Road)
                                    dustMag += (fix64)1.1f;
                                else
                                    dustMag += (fix64)1.2f;
                                conto.Dust(k, wheelx[k], wheely[k], wheelz[k], (int)Scx[k], (int)Scz[k], dustMag * Stat.Simag, 0, BadLanding && Mtouch, (int)wheelGround);
                            }
                            wheely[k] = collision.newY + wheelGround; // snap wheel to the surface
                            
                            // sparks and scrape
                            if (BadLanding && collidable.SurfaceType is SurfaceType.Road or SurfaceType.OffTrack)
                            {
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                //if (Im == /*this.xt.im*/ 0)
                                if (IsClientPlayer)
                                {
                                    SfxPlayGscrape?.Invoke(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                                }
                            }

                            bounceRebound(k, conto, random);
                            isWheelTouchingPiece[k] = true;
                            break;
                        }
                    }
                    else if (collidable.TryGetValue(out ShapeWall boxWall))
                    {
                        if (boxWall.ResolveCollision(position, velocity) is { } collision)
                        {
                            for (int w = 0; w < 4; w++) {
                                wheelx[w] += collision.positionDelta.X;
                                wheely[w] += collision.positionDelta.Y;
                                wheelz[w] += collision.positionDelta.Z;
                            }
                            
                            // sparks and scrapes
                            if (collidable.SurfaceType != SurfaceType.OffRoad)
                                _crank[0, k]++;
                            if (collidable.SurfaceType == SurfaceType.Spikes && random.NextFixed6401() > fix64.Half)
                                _crank[0, k]++;
                            if (_crank[0, k] > 1)
                            {
                                conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 0, (int)wheelGround);
                                if (IsClientPlayer)
                                {
                                    SfxPlayScrape?.Invoke(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                                }
                            }

                            // z rebound CHK5
                            f64Vector3 reboundVelocityDelta = collision.impactComponent * (-GetReboundMul(wasMtouch));
                            Regz(k, reboundVelocityDelta.Length() * collidable.Damage, conto, random);
                            Scx[k] += reboundVelocityDelta.X;
                            Scy[k] += reboundVelocityDelta.Y;
                            Scz[k] += reboundVelocityDelta.Z;

                            Skid = 2;
                            hitVertical = true;
                            isWheelTouchingPiece[k] = true;
                            if (!collidable.NotWall) {
                                control.Wall = 9999;
                            }
                            break;
                        }
                    }
                    else if (collidable.TryGetValue(out ShapeRamp boxRamp))
                    {
                        if (boxRamp.ResolveCollision(position) is { } collision)
                        {
                            var liftDivider = 1 + (50 - fix64.Abs(boxRamp.TrackersZy)) / (fix64)30;
                            if (liftDivider < 1)
                                liftDivider = 1;
                            if (collision.zTmp > 0 && collision.zTmp < 200) {
                                FrameTrace.AddMessage($"ramp lift: {collision.zTmp} liftDivider: {liftDivider:F2} total: {collision.zTmp / liftDivider}");
                                Scy[k] -= collision.zTmp / liftDivider;
                            }

                            isWheelGrounded[k] = true;
                            // Ramp surface normal in world space.
                            // In the ramp's zy-rotated local frame the surface is Z=0 and the car
                            // sits at Z>0, so the surface-up direction is (0,0,-1) in that frame.
                            // Undo the ZY tilt then the XZ object rotations to reach world space.
                            wheelContactNormal[k] = new f64Vector3(fix64.Zero, fix64.Zero, (fix64)(-1))
                                .RotateZy((boxRamp.TrackersZy + 90))
                                .RotateXz(boxRamp.TrackersXz)
                                .RotateXz(boxRamp.GameObjectXz);

                            if (collision.zTmp > -30)
                            {
                                if (collidable.SurfaceType == SurfaceType.OffRoad)
                                    nWheelsDirtRamp++;
                                else
                                    nWheelsRoadRamp++;
                                
                                Wtouch = true;
                                Gtouch = false;

                                // sparks and scrape
                                if (BadLanding && collidable.SurfaceType is SurfaceType.Road or SurfaceType.OffTrack)
                                {
                                    conto.Spark(wheelx[k], wheely[k], wheelz[k], Scx[k], Scy[k], Scz[k], 1, (int)wheelGround);
                                    if (IsClientPlayer)
                                    {
                                        SfxPlayGscrape?.Invoke(this, ((int)Scx[k], (int)Scy[k], (int)Scz[k]));
                                    }
                                }

                                if (!wasMtouch && surfaceType != SurfaceType.Road)
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
        conto.DamageX(i, f);

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
            Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
            if (IsClientPlayer || _collidingWithClientPlayer)
            {
                SfxPlayCrash?.Invoke(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i111 = 0; i111 < 40; i111++)
            {
                fix64 f112 = 0;
                for (var i113 = 0; i113 < 4; i113++)
                {
                    f112 = f / 20 * random.NextFixed6401();
                    if (abool)
                    {
                        DamagePoints += (int)fix64.Abs(f112);
                        i110 += (int)fix64.Abs(f112);
                    }
                }
            }
        }
        return i110;
    }

    private int Regy(int i, fix64 f, ContO conto, DeterministicRandom random)
    {
        conto.DamageY(i, f, Mtouch, _numRoofDamage, RoofDamage);
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
                Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
            }
            
            if (IsClientPlayer || _collidingWithClientPlayer)
            {
                SfxPlayCrash?.Invoke(this, ((int)f, i99 * i98));
                //XTGraphics.Acrash(Im, f, i99 * i98);
            }
            if (i99 * i98 == 0 || Mtouch)
            {
                for (var i102 = 0; i102 < 40; i102++)
                {
                    fix64 f103 = 0;
                    for (var i104 = 0; i104 < 4; i104++)
                    {
                        f103 = f / 20 * random.NextFixed6401();
                        if (abool)
                        {
                            DamagePoints += (int)fix64.Abs(f103);
                            i97 += (int)fix64.Abs(f103);
                        }
                    }
                }
            }
            if (i99 * i98 == -1)
            {
                if (_numRoofDamage > 0)
                {
                    var dividend = 0;
                    var divisor = 1;
                    for (var i107 = 0; i107 < 40; i107++)
                    {
                        fix64 f108 = 0;
                        for (var i109 = 0; i109 < 4; i109++)
                        {
                            f108 = f / 15 * random.NextFixed6401();
                            dividend += (int)f108;
                            divisor++;
                            if (abool)
                            {
                                DamagePoints += (int)fix64.Abs(f108);
                                i97 += (int)fix64.Abs(f108);
                            }
                        }
                    }
                    RoofDamage += dividend / divisor;
                    _numRoofDamage = 0;
                }
                else
                {
                    _numRoofDamage++;
                }
            }
        }
        return i97;
    }

    private int Regz(int i, fix64 f, ContO conto, DeterministicRandom random)
    {
        conto.DamageZ(i, f);
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
            Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
            
            if (IsClientPlayer || _collidingWithClientPlayer)
            {
                SfxPlayCrash?.Invoke(this, ((int)f, 0));
                //XTGraphics.Acrash(Im, f, 0);
            }
            for (var i115 = 0; i115 < 40; i115++)
            {
                fix64 f116 = 0;
                for (var i117 = 0; i117 < 4; i117++)
                {
                    f116 = f / 20 * random.NextFixed6401();
                    if (abool)
                    {
                        DamagePoints += (int)fix64.Abs(f116);
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
        _turnXz = 0;
        _pmlt = 1;
        _nmlt = 1;
        _dcnt = 0;
        Skid = 0;
        Pushed = false;
        Gtouch = false;
        PressLeft = false;
        PressRight = false;
        PressDown = false;
        PressUp = false;
        StuntState = 0;
        UpComponent = 0;
        DownComponent = 0;
        LeftComponent = 0;
        RightComponent = 0;
        _lxz = 0;
        TotalStuntXy = 0;
        TotalStuntZy = 0;
        TotalStuntXz = 0;
        RightTabletop = false;
        ForwardTabletop = false;
        BackwardsTabletop = false;
        Powerup = 0;
        _xtpower = 0;
        TabletopCounter = 0;
        CapsizedCounter = 0;
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
        RoofDamage = 0;
        _numRoofDamage = 0;
        DamagePoints = 0;
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

    public void FinishedFix()
    {
        RoofDamage = 0;
        _numRoofDamage = 0;
        DamagePoints = 0;
        Cntdest = 0;
        Wasted = false;
        Newcar = true;
        if (_fixes > 0)
        {
            _fixes--;
        }
    }
}