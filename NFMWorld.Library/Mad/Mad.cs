using System.Runtime.CompilerServices;
using FixedMathSharp;
using FixedMathSharp.Utility;
using Microsoft.Extensions.Logging;
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

public struct Wheel
{
    public f64Vector3 Position;
    public f64Vector3 Velocity;
}

public class Mad
{
    private static readonly fix64 _tickRate = Physics.PHYSICS_MULTIPLIER_F64;
    private static readonly fix64 _oneOverTickRate = 1 / _tickRate;
    public Boolean Halted = false;

    public bool Btab;
    public int Capcnt;
    public bool Capsized;
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
    /// <summary>
    /// In degrees
    /// </summary>
    public fix64 Cxz;
    public int _dcnt;
    public fix64 Dcomp;
    public bool Wasted;
    public readonly UnlimitedArray<bool> _dominate = [];
    public readonly fix64 _drag = fix64.Half;
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
    /// <summary>
    /// In degrees
    /// </summary>
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

    public fix64 Rcomp;
    public bool Rtab;
    public InlineArray4<Wheel> Wheels;
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
    /// <summary>
    /// In radians
    /// </summary>
    public fix64 LastYaw;
    public fix64 Ucomp;
    public bool Wtouch;
    public int _xtpower;

    public FixedQuaternion CarRotation;

    internal bool IsClientPlayer;
    internal fix64 py = 0;

    public event EventHandler<(float f, int i)> SfxPlayCrash;
    public event EventHandler<(int i, float f)> SfxPlaySkid;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayScrape;
    public event EventHandler<(int i, int i2, int i3)> SfxPlayGscrape;
    public event EventHandler<float> PowerUp;

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

    public Mad(CarStats stat, int im, bool isClientPlayer)
    {
        Stat = stat;
        Im = im;
        IsClientPlayer = isClientPlayer;
        CarRotation = FixedQuaternion.Identity;
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
        // var random = new DeterministicRandom((ulong)(conto.X.rawValue ^ otherconto.X.rawValue ^ conto.Z.rawValue ^ otherconto.Z.rawValue ^ conto.Y.rawValue ^ otherconto.Y.rawValue));
        //
        // var wheelx = new InlineArray4<fix64>();
        // var wheely = new InlineArray4<fix64>();
        // var wheelz = new InlineArray4<fix64>();
        // var otherwheelx = new InlineArray4<fix64>();
        // var otherwheely = new InlineArray4<fix64>();
        // var otherwheelz = new InlineArray4<fix64>();
        //
        // // No hypergliding fixes are needed here because this is only called during collisions
        // // however we need this code or else sparks will come out of the wrong place
        // var bottomy = GetBottomY(this, conto);
        // var otherbottomy = GetBottomY(othermad, otherconto);
        //
        // var wheelGround = GetWheelGround(this, conto, bottomy);
        // var otherWheelGround = GetWheelGround(othermad, otherconto, otherbottomy);
        //
        // for (var i1 = 0; i1 < 4; i1++)
        // {
        //     wheelx[i1] = conto.X + conto.Keyx[i1];
        //     wheely[i1] = conto.Y + bottomy;
        //     wheelz[i1] = conto.Z + conto.Keyz[i1];
        //     otherwheelx[i1] = otherconto.X + otherconto.Keyx[i1];
        //     otherwheely[i1] = otherconto.Y + bottomy;
        //     otherwheelz[i1] = otherconto.Z + otherconto.Keyz[i1];
        // }
        //
        // UMath.Rot(wheelx, wheely, conto.X, conto.Y, conto.Xy, 4);
        // UMath.Rot(wheely, wheelz, conto.Y, conto.Z, conto.Zy, 4);
        // UMath.Rot(wheelx, wheelz, conto.X, conto.Z, conto.Xz, 4);
        // UMath.Rot(otherwheelx, otherwheely, otherconto.X, otherconto.Y, otherconto.Xy, 4);
        // UMath.Rot(otherwheely, otherwheelz, otherconto.Y, otherconto.Z, otherconto.Zy, 4);
        // UMath.Rot(otherwheelx, otherwheelz, otherconto.X, otherconto.Z, otherconto.Xz, 4);
        // if (UMath.Rpy(conto.X, otherconto.X, conto.Y, otherconto.Y, conto.Z, otherconto.Z) <
        //     (conto.MaxR * conto.MaxR + otherconto.MaxR * otherconto.MaxR) * (fix64)1.5f)
        // {
        //     if (!_caught[othermad.Im] && (Speed != 0 || othermad.Speed != 0))
        //     {
        //         var ownMoment = fix64.Abs(Power * Speed * Stat.Moment);
        //         var otherMoment = fix64.Abs(othermad.Power * othermad.Speed * othermad.Stat.Moment);
        //         if (fix64.Abs(ownMoment - otherMoment) > (fix64)0.001f)
        //         {
        //             _dominate[othermad.Im] = ownMoment > otherMoment;
        //         }
        //         else
        //         {
        //             _dominate[othermad.Im] = Stat.Moment > othermad.Stat.Moment;
        //         }
        //
        //         _caught[othermad.Im] = true;
        //     }
        // }
        // else if (_caught[othermad.Im])
        // {
        //     _caught[othermad.Im] = false;
        // }
        // var totalOtherDamage = 0;
        // var totalOwnDamage = 0;
        // if (_dominate[othermad.Im])
        // {
        //     var impactMagnitude =
        //         (int) ((
        //             (Scz[0] - othermad.Scz[0] + Scz[1] - othermad.Scz[1] + Scz[2] - othermad.Scz[2] + Scz[3] - othermad.Scz[3]) *
        //             (Scz[0] - othermad.Scz[0] + Scz[1] - othermad.Scz[1] + Scz[2] - othermad.Scz[2] + Scz[3] - othermad.Scz[3]) +
        //             (Scx[0] - othermad.Scx[0] + Scx[1] - othermad.Scx[1] + Scx[2] - othermad.Scx[2] + Scx[3] - othermad.Scx[3]) *
        //             (Scx[0] - othermad.Scx[0] + Scx[1] - othermad.Scx[1] + Scx[2] - othermad.Scx[2] + Scx[3] - othermad.Scx[3])
        //         ) / 16);
        //     var impactExtraRdius = 7000;
        //     fix64 damageMult = 1;
        //     if (World.UseMultiplayerCollisionModifiers)
        //     {
        //         impactExtraRdius = 28000;
        //         damageMult = (fix64)1.27F;
        //     }
        //     for (var wheel = 0; wheel < 4; wheel++)
        //     {
        //         for (var otherwheel = 0; otherwheel < 4; otherwheel++)
        //         {
        //             if (UMath.Rpy(wheelx[wheel], otherwheelx[otherwheel], wheely[wheel], otherwheely[otherwheel], wheelz[wheel], otherwheelz[otherwheel]) <
        //                 (impactMagnitude + impactExtraRdius) * (othermad.Stat.Comprad + Stat.Comprad))
        //             {
        //                 if (fix64.Abs(Scx[wheel] * Stat.Moment) > fix64.Abs(othermad.Scx[otherwheel] * othermad.Stat.Moment))
        //                 {
        //                     var f130 = othermad.Scx[otherwheel] * Stat.Revpush;
        //                     if (f130 > 300)
        //                     {
        //                         f130 = 300;
        //                     }
        //                     if (f130 < -300)
        //                     {
        //                         f130 = -300;
        //                     }
        //                     var f131 = Scx[wheel] * Stat.Push;
        //                     if (f131 > 300)
        //                     {
        //                         f131 = 300;
        //                     }
        //                     if (f131 < -300)
        //                     {
        //                         f131 = -300;
        //                     }
        //                     othermad.Scx[otherwheel] += f131;
        //                     if (IsClientPlayer)
        //                     {
        //                         othermad._colidim = true;
        //                     }
        //                     totalOtherDamage += othermad.Regx(otherwheel, f131 * Stat.Moment * damageMult, otherconto, random);
        //                     if (othermad._colidim)
        //                     {
        //                         othermad._colidim = false;
        //                     }
        //                     Scx[wheel] -= f130;
        //                     totalOwnDamage += Regx(wheel, -f130 * Stat.Moment * damageMult, conto, random);
        //                     Scy[wheel] -= Stat.Revlift;
        //                     if (IsClientPlayer)
        //                     {
        //                         othermad._colidim = true;
        //                     }
        //                     totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
        //                     if (othermad._colidim)
        //                     {
        //                         othermad._colidim = false;
        //                     }
        //                     if (UMath.RandomBoolean())
        //                     {
        //                         otherconto.Spark(
        //                             (wheelx[wheel] + otherwheelx[otherwheel]) * fix64.Half, 
        //                             (wheely[wheel] + otherwheely[otherwheel]) * fix64.Half,
        //                             (wheelz[wheel] + otherwheelz[otherwheel]) * fix64.Half, 
        //                             (othermad.Scx[otherwheel] + Scx[wheel]) * fix64.Quarter,
        //                             (othermad.Scy[otherwheel] + Scy[wheel]) * fix64.Quarter,
        //                             (othermad.Scz[otherwheel] + Scz[wheel]) * fix64.Quarter,
        //                             2,
        //                             (wheelGround + otherWheelGround) / 2
        //                         );
        //                     }
        //                 }
        //                 if (fix64.Abs(Scz[wheel] * Stat.Moment) > fix64.Abs(othermad.Scz[otherwheel] * othermad.Stat.Moment))
        //                 {
        //                     var f132 = othermad.Scz[otherwheel] * Stat.Revpush;
        //                     if (f132 > 300)
        //                     {
        //                         f132 = 300;
        //                     }
        //                     if (f132 < -300)
        //                     {
        //                         f132 = -300;
        //                     }
        //                     var f133 = Scz[wheel] * Stat.Push;
        //                     if (f133 > 300)
        //                     {
        //                         f133 = 300;
        //                     }
        //                     if (f133 < -300)
        //                     {
        //                         f133 = -300;
        //                     }
        //                     othermad.Scz[otherwheel] += f133;
        //                     if (IsClientPlayer)
        //                     {
        //                         othermad._colidim = true;
        //                     }
        //                     totalOtherDamage += othermad.Regz(otherwheel, f133 * Stat.Moment * damageMult, otherconto, random);
        //                     if (othermad._colidim)
        //                     {
        //                         othermad._colidim = false;
        //                     }
        //                     Scz[wheel] -= f132;
        //                     totalOwnDamage += Regz(wheel, -f132 * Stat.Moment * damageMult, conto, random);
        //                     Scy[wheel] -= Stat.Revlift;
        //                     if (IsClientPlayer)
        //                     {
        //                         othermad._colidim = true;
        //                     }
        //                     totalOtherDamage += othermad.Regy(otherwheel, Stat.Revlift * 7, otherconto, random);
        //                     if (othermad._colidim)
        //                     {
        //                         othermad._colidim = false;
        //                     }
        //                     if (UMath.RandomBoolean())
        //                     {
        //                         otherconto.Spark(
        //                             (wheelx[wheel] + otherwheelx[otherwheel]) * fix64.Half, 
        //                             (wheely[wheel] + otherwheely[otherwheel]) * fix64.Half,
        //                             (wheelz[wheel] + otherwheelz[otherwheel]) * fix64.Half,
        //                             (othermad.Scx[otherwheel] + Scx[wheel]) * fix64.Quarter,
        //                             (othermad.Scy[otherwheel] + Scy[wheel]) * fix64.Quarter, 
        //                             (othermad.Scz[otherwheel] + Scz[wheel]) * fix64.Quarter,
        //                             2,
        //                             (wheelGround + otherWheelGround) / 2);
        //                     }
        //                 }
        //                 if (IsClientPlayer)
        //                 {
        //                     othermad.Lastcolido = 70;
        //                 }
        //                 if (othermad.IsClientPlayer)
        //                 {
        //                     Lastcolido = 70;
        //                 }
        //                 othermad.Scy[otherwheel] -= Stat.Lift;
        //             }
        //         }
        //     }
        // }
        // // if (XTGraphics.Multion == 1)
        // // {
        // //     if (othermad.Im == XTGraphics.Im && i != 0)
        // //     {
        // //         XTGraphics.Dcrashes[Im] += i;
        // //     }
        // //     if (Im == XTGraphics.Im && i125 != 0)
        // //     {
        // //         XTGraphics.Dcrashes[othermad.Im] += i125;
        // //     }
        // // }
    }

    private static int GetWheelGround(Mad mad, ContO conto, fix64 bottomy)
    {
        int wheelGround;
        if (World.IsHyperglidingEnabled)
        {
            wheelGround = (int)((bottomy * _oneOverTickRate) * (fix64.One - _tickRate));
            if (!mad.Capsized)
            {
                wheelGround = -wheelGround;
            }
        }
        else
        {
            wheelGround = mad.Capsized ? mad.Stat.Flipy + mad.Squash : -conto.Grat;
        }

        return wheelGround;
    }

    private static fix64 GetBottomY(Mad mad, ContO conto)
    {
        fix64 bottomy;
        if (World.IsHyperglidingEnabled)
        {
            if (mad.Capsized)
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
        // Sin(roll)  = how far the local right axis has tilted off horizontal
        fix64 sinRoll  = (CarRotation * Right).Y;

        // Sin(pitch) = how far the local forward axis has tilted off horizontal  
        fix64 sinPitch = (CarRotation * Forward).Y;

        fix64 rebound = (fix64.Abs(sinRoll) + fix64.Abs(sinPitch)) / (fix64)3;

        fix64 maxAngleRebound = (fix64)(0.4F); // capping at 0.4 doesn't do much, max is two thirds
        rebound = fix64.Min(rebound, maxAngleRebound);
    
        // part 2: the bigger the bounce stat, the bigger the bounce
        rebound += Stat.Bounce;
        fix64 minRebound = (fix64)(1.1F);
        rebound = fix64.Max(rebound, minRebound);
    
        // Regy(wi, fix64.Abs(Wheels[wi].Velocity.Y * rebound), conto, random);
        // if scy is > 0 then we are going down, apply the rebound bounce
        if (Wheels[wi].Velocity.Y > 0)
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
            Wheels[wi].Velocity.Y = (fix64)(-1) * Wheels[wi].Velocity.Y * (rebound - fix64.One);
    }

    public void Drive(Control control, ContO conto, IStage stage)
    {
        DeterministicRandom random = new((ulong)(conto.X.rawValue ^ conto.Y.rawValue ^ conto.Z.rawValue));

        FrameTrace.AddMessage($"xz: {0:0.00}, mxz: {Mxz:0.00}, lxz: {_lxz:0.00}, fxz: {_fxz:0.00}, cxz: {Cxz:0.00}");
        FrameTrace.AddMessage($"xy: {0:0.00}, pxy: {0:0.00}, zy: {0:0.00}, pzy: {0:0.00}");
        FrameTrace.AddMessage(
            $"Travxz: {Travxz:0.00}, Travxy: {Travxy:0.00}, Travzy: {Travzy:0.00}, Surfing: {Surfer}");

        // if the car's angled down (car rotation's -Y vector is +Y)
        var localUp = CarRotation * Up;
        var localForward = CarRotation * Forward;
        var localRight = CarRotation * Right;
        Capsized = localUp.Y > 0; // Up=(0,-1,0) so localUp.Y=-1 when upright; Y>0 means the roof faces ground = capsized

        FrameTrace.AddMessage($"CarRotation: {CarRotation:0.00}, CapSized: {Capsized}");
        FrameTrace.AddMessage($"localUp: {localUp:0.00}, localForward: {localForward:0.00}, localRight: {localRight:0.00}");
        
        // maxine: this controls hypergliding. to fix hypergliding, set to 0, then update wheelGround to prevent
        // car getting stuck in the ground
        // we multiply it by tickrate because the effect caused by hypergliding is applied every tick
        fix64 bottomy = GetBottomY(this, conto);

        control.Zyinv = Capsized;
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
            var avgVerticalVelocity =
                (Wheels[0].Velocity.Y + Wheels[1].Velocity.Y + Wheels[2].Velocity.Y + Wheels[3].Velocity.Y) *
                fix64.Quarter;
            for (var w = 0; w < 4; w++)
            {
                Wheels[w].Velocity.Y = avgVerticalVelocity;
            }

            Loop = 2;
        } //
        
        FrameTrace.AddMessage($"Loop: {Loop}");

        if (!Wasted)
        {
            if (Loop == 2)
            {
                if (control.Up)
                {
                    if (Ucomp == 0)
                    {
                        Ucomp = 10 + (Wheels[0].Velocity.Y + 50) / 20;
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
                        Ucomp += fix64.Half * Stat.Airs * _tickRate; //
                    }

                    // Forward direction projected onto XZ plane (world-space yaw direction)
                    var zneg = f64Vector3.Dot(localUp, Up) >= 0 ? 1 : -1;

                    airx = -Stat.Airc * localForward.X * zneg * _tickRate;
                    airz = Stat.Airc * localForward.Z * zneg * _tickRate;
                }
                else if (Ucomp != 0 && Ucomp > -2)
                {
                    Ucomp -= fix64.Half * Stat.Airs * _tickRate; //
                }

                if (control.Down)
                {
                    if (Dcomp == 0)
                    {
                        Dcomp = 10 + (Wheels[0].Velocity.Y + 50) / 20;
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
                        Dcomp += fix64.Half * Stat.Airs * _tickRate; //
                    }

                    airy = -Stat.Airc * _tickRate;
                }
                else if (Dcomp != 0 && Ucomp > -2)
                {
                    Dcomp -= fix64.Half * Stat.Airs * _tickRate;
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

                    airx = -Stat.Airc * localRight.X * _tickRate;
                    airz = -Stat.Airc * localRight.Z * _tickRate;
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

                    airx = Stat.Airc * localRight.X * _tickRate;
                    airz = Stat.Airc * localRight.Z * _tickRate;
                }
                else if (Rcomp > 0) //
                {
                    Rcomp -= 2 * Stat.Airs * _tickRate;
                }

                var pitchDelta = FixedQuaternion.AngleAxis((Dcomp - Ucomp) * _tickRate, Right);
                var rollDelta = FixedQuaternion.AngleAxis((Rcomp - Lcomp) * _tickRate, Forward);

                CarRotation = CarRotation * pitchDelta * rollDelta;
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
                            Speed -= (Stat.Acelf.AsSpan()[i16] * fix64.Half + f15 * Stat.Acelf.AsSpan()[i16] / 196) *
                                     _tickRate;
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
                            Speed += (Stat.Acelf.AsSpan()[i18] * fix64.Half + f15 * Stat.Acelf.AsSpan()[i18] / 196) *
                                     _tickRate;
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

                    var pitchDelta = FixedQuaternion.AngleAxis((Dcomp - Ucomp) * _tickRate, Right);
                    var rollDelta = FixedQuaternion.AngleAxis((Rcomp - Lcomp) * _tickRate, Forward);

                    CarRotation = CarRotation * pitchDelta * rollDelta;
                }
            }
        }

        var f20 = 20 * Speed / (154 * Stat.Simag);
        if (f20 > 20)
        {
            f20 = 20;
        }

        conto.Wzy -= (f20 * _tickRate);
        conto.Wzy %= 360;

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

        FrameTrace.AddMessage($"Wtouch: {Wtouch}, Gtouch: {Gtouch}, i21: {i21}, conto.Wxz: {conto.Wxz}");

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

                var yawDelta = FixedQuaternion.AngleAxis(conto.Wxz / i21 * _tickRate, Up);
                CarRotation = CarRotation * yawDelta;
            }

            Wtouch = false;
            Gtouch = false;
        }
        else
        {
            var yawDelta = FixedQuaternion.AngleAxis(_fxz * _tickRate, Up);
            CarRotation = CarRotation * yawDelta;
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

        // reset wheel positions this frame
        for (var w = 0; w < 4; w++)
        {
            Wheels[w].Position.X = conto.Keyx[w] + conto.X;
            Wheels[w].Position.Y = bottomy + conto.Y;
            Wheels[w].Position.Z = conto.Z + conto.Keyz[w];
            Wheels[w].Velocity.Y += 7 * _tickRate;
        }

        // rotate wheels along car's angle
        var carPosition = new f64Vector3(conto.X, conto.Y, conto.Z);
        for (var w = 0; w < 4; w++)
        {
            Wheels[w].Position = f64Vector3.Rotate(Wheels[w].Position, carPosition, CarRotation);
        }

        var wasMtouch = false;

        // clamp the x/z wheel velocities around the mean
        {
            var avgVelX = ((Wheels[0].Velocity.X + Wheels[1].Velocity.X + Wheels[2].Velocity.X + Wheels[3].Velocity.X) * fix64.Quarter);
            var avgVelZ = ((Wheels[0].Velocity.Z + Wheels[1].Velocity.Z + Wheels[2].Velocity.Z + Wheels[3].Velocity.Z) * fix64.Quarter);
            for (var w = 0; w < 4; w++)
            {
                if (Wheels[w].Velocity.X - avgVelX > 200)
                {
                    Wheels[w].Velocity.X = 200 + avgVelX;
                }

                if (Wheels[w].Velocity.X - avgVelX < -200)
                {
                    Wheels[w].Velocity.X = avgVelX - 200;
                }

                if (Wheels[w].Velocity.Z - avgVelZ > 200)
                {
                    Wheels[w].Velocity.Z = 200 + avgVelZ;
                }

                if (Wheels[w].Velocity.Z - avgVelZ < -200)
                {
                    Wheels[w].Velocity.Z = avgVelZ - 200;
                }

                FrameTrace.AddMessage($"Scx[{w}]: {Wheels[w].Velocity.X:0.00}, Scz[{w}]: {Wheels[w].Velocity.Z:0.00}, Scy[{w}]: {Wheels[w].Velocity.Y:0.00}");
            }
        }

        {
            var avgVelX = ((Wheels[0].Velocity.X + Wheels[1].Velocity.X + Wheels[2].Velocity.X + Wheels[3].Velocity.X) * fix64.Quarter) * _tickRate;
            var avgVelZ = ((Wheels[0].Velocity.Z + Wheels[1].Velocity.Z + Wheels[2].Velocity.Z + Wheels[3].Velocity.Z) * fix64.Quarter) * _tickRate;
            // apply velocities to wheels
            for (var w = 0; w < 4; w++)
            {
                Wheels[w].Position.Y += Wheels[w].Velocity.Y * _tickRate;
                Wheels[w].Position.X += avgVelX;
                Wheels[w].Position.Z += avgVelZ;
            } //
        }

        var surfaceType = 1;
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
                    surfaceType = collidable.Skid;
                }
            }
        }

        // maxine: we counteract the reduced bottomy from hypergliding here
        int wheelGround = GetWheelGround(this, conto, bottomy);

        var yaw = fix64.Atan2(localForward.X, localForward.Z);
        if (Mtouch)
        {
            // Jacher: 1/_tickrate for traction; Txz is set on previous tick so we need to scale
            var traction = Stat.Grip;

            traction -= fix64.Abs(LastYaw - yaw) * fix64.RadToDeg * (_oneOverTickRate) * Speed / 250;
            if (control.Handb)
            {
                traction -= fix64.Abs(LastYaw - yaw) * fix64.RadToDeg * (_oneOverTickRate) * 4;
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
            
            FrameTrace.AddMessage($"Speed: {Speed:0.00}, Traction: {traction:0.00}, SurfaceType: {surfaceType}");

            var velocity = localForward * Speed;
            if (Capsized || Wasted || Halted)
            {
                velocity = f64Vector3.Zero;
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
                if (fix64.Abs(Wheels[j].Velocity.X - velocity.X) > traction * _tickRate)
                {
                    Wheels[j].Velocity.X += traction * (velocity.X - Wheels[j].Velocity.X).Sign() * _tickRate;
                }
                else
                {
                    Wheels[j].Velocity.X = velocity.X;
                }

                if (fix64.Abs(Wheels[j].Velocity.Z - velocity.Z) > traction * _tickRate)
                {
                    Wheels[j].Velocity.Z += traction * (velocity.Z - Wheels[j].Velocity.Z).Sign() * _tickRate;
                }
                else
                {
                    Wheels[j].Velocity.Z = velocity.Z;
                }

                if (fix64.Abs(Wheels[j].Velocity.Y - velocity.Y) > traction * _tickRate)
                {
                    // Jacher: decouple this from tickrate
                    // this reduces bouncing when AB-ing, but at what cost?
                    // oteek: if decoupled slanted ramps make car bounce for no reason for a bit
                    Wheels[j].Velocity.Y += traction * (velocity.Y - Wheels[j].Velocity.Y).Sign() * _tickRate;
                }
                else
                {
                    Wheels[j].Velocity.Y = velocity.Y;
                } //

                // maxine: maybe this should be scaled to tickrate?
                if (traction < Stat.Grip)
                {
                    if (fix64.Abs(LastYaw - yaw) > fix64.Half)
                    {
                        _dcnt++;
                    }
                    else
                    {
                        _dcnt = 0;
                    }

                    if (_dcnt > 40 * traction / Stat.Grip || Capsized)
                    {
                        fix64 f42 = 1;
                        if (surfaceType != 0)
                        {
                            f42 = (fix64)(1.2F);
                        }

                        if (random.NextFixed6401() > (fix64)0.65f)
                        {
                            conto.Dust(j, Wheels[j].Position.X, Wheels[j].Position.Y, Wheels[j].Position.Z, (int)Wheels[j].Velocity.X, (int)Wheels[j].Velocity.Z,
                                f42 * Stat.Simag, (int)_tilt, Capsized && Mtouch, wheelGround);
                            if ( /*Im == XTGraphics.Im &&*/ !Capsized)
                            {
                                SfxPlaySkid(this, (surfaceType, (float)fix64.Sqrt(Wheels[j].Velocity.X * Wheels[j].Velocity.X + Wheels[j].Velocity.Z * Wheels[j].Velocity.Z)));
                                //XTPart2.Skidf(Im, i32,
                                //    (fix64) Math.Sqrt(Scx[i41] * Scx[i41] + Scz[i41] * Scz[i41]));
                            }
                        }
                    }
                    else
                    {
                        if (surfaceType == 1 && random.NextFixed6401() > (fix64)0.8f)
                        {
                            conto.Dust(j, Wheels[j].Position.X, Wheels[j].Position.Y, Wheels[j].Position.Z, (int)Wheels[j].Velocity.X, (int)Wheels[j].Velocity.Z,
                                (fix64)1.1F * Stat.Simag, (int)_tilt, Capsized && Mtouch, wheelGround);
                        }

                        if ((surfaceType == 2 || surfaceType == 3) && random.NextFixed6401() > (fix64)0.6f)
                        {
                            conto.Dust(j, Wheels[j].Position.X, Wheels[j].Position.Y, Wheels[j].Position.Z, (int)Wheels[j].Velocity.X, (int)Wheels[j].Velocity.Z,
                                (fix64)1.15F * Stat.Simag, (int)_tilt, Capsized && Mtouch, wheelGround);
                        }
                    }
                }
                else if (_dcnt != 0)
                {
                    _dcnt = Math.Max(_dcnt - 2, 0);
                }

                if (surfaceType == 3 || surfaceType == 4)
                {
                    int k = random.Next(4); // choose 4 wheels randomly to bounce up, usually some wheel will be chosen twice, which means another wheel is not chosen, causing tilt
                    fix64 bumpLift = surfaceType == 3 ? -100 : -150;
                    fix64 rng = (fix64)0.55F;
                    Wheels[k].Velocity.Y = bumpLift * rng * Speed / Stat.Swits[2] * (Stat.Bounce - (fix64)0.3F);
                }
            }

            LastYaw = yaw; // CHK1

            fix64 scxsum = 0;
            fix64 sczsum = 0;
            // 4 = nwheels
            for (int j = 0; j < 4; ++j)
            {
                scxsum += Wheels[j].Velocity.X;
                sczsum += Wheels[j].Velocity.Z;
            }

            fix64 scxavg = scxsum * fix64.Quarter; /* nwheels */
            fix64 sczavg = sczsum * fix64.Quarter;
            fix64 scxz = fix64.Hypot(sczavg, scxavg);

            Mxz = fix64.Atan2(-scxsum, sczsum) * fix64.RadToDeg;

            if (Skid == 2)
            {
                if (!Capsized)
                {
                    Speed = scxz * UMath.Cos(Mxz + yaw * fix64.RadToDeg);
                }

                Skid = 0;
            }

            if (Capsized && scxsum == 0 && sczsum == 0)
            {
                surfaceType = 0;
            } //

            Mtouch = false;
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
        for (var w = 0; w < 4; w++)
        {
            isWheelGrounded[w] = false;
            if (Wheels[w].Position.Y > (groundY - (fix64)5))
            {
                nGroundedWheels++;
                Wtouch = true;
                Gtouch = true;
                Mtouch = true;
                if (!wasMtouch && Wheels[w].Velocity.Y != 7)
                {
                    var v = Wheels[w].Velocity.Y / (fix64)(333.33F);
                    if (v > (fix64)(0.3F))
                    {
                        v = (fix64)(0.3F);
                    }

                    if (surfaceType == 0)
                    {
                        v += (fix64)1.1f;
                    }
                    else
                    {
                        v += (fix64)1.2f;
                    }

                    conto.Dust(w, Wheels[w].Position.X, Wheels[w].Position.Y, Wheels[w].Position.Z, (int)Wheels[w].Velocity.X, (int)Wheels[w].Velocity.Z,
                        v * Stat.Simag,
                        0, Capsized && Mtouch, wheelGround);
                } // CHK2

                Wheels[w].Position.Y = groundY;
                f48 += Wheels[w].Position.Y - groundY;
                isWheelGrounded[w] = true;

                // bounceRebound(w, conto, random);
            }
        }

        // OmarTrackPieceCollision(control, conto, wheelx, wheely, wheelz, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out hitVertical, isWheelGrounded, random);
        PhyTrackPieceCollision(stage, control, conto, groundY, wheelYThreshold, wheelGround, ref nGroundedWheels, wasMtouch, surfaceType, out var hitVertical, isWheelGrounded, random);
        
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

        var centerPos = (Wheels[0].Position + Wheels[1].Position + Wheels[2].Position + Wheels[3].Position) * fix64.Quarter;

        // calculate translation based on wheel origin
        fix64 wheelXTranslation = 0;
        fix64 wheelZTranslation = 0;
        var offset = new f64Vector3();
        {
            for (var i = 0; i < 4; i++)
            {
                wheelXTranslation += conto.Keyx[i];
                wheelZTranslation += conto.Keyz[i];
            }

            wheelXTranslation *= fix64.Quarter;
            wheelZTranslation *= fix64.Quarter;

            offset += wheelXTranslation * -localRight;
            offset += wheelZTranslation * -localForward;
            offset += bottomy * localUp;
        }
        
        offset += new f64Vector3(airx, 0, airz);

        conto.X = centerPos.X + offset.X;
        conto.Z = centerPos.Z + offset.Z;
        conto.Y = centerPos.Y + offset.Y;

        // Fit CarRotation to the terrain plane defined by grounded wheel positions.
        // Uses a three-point plane through wheels 0-2; sign is corrected against the
        // car's current up direction so the normal always points away from the surface.
        // if (nGroundedWheels >= 3)
        {
            var terrainNormal = f64Vector3.Cross(
                Wheels[1].Position - Wheels[0].Position,
                Wheels[2].Position - Wheels[0].Position
            ).Normal;

            // Ensure it faces the same half-space as localUp
            if (f64Vector3.Dot(terrainNormal, localUp) < fix64.Zero)
                terrainNormal = -terrainNormal;

            // Project localForward onto the terrain plane to preserve yaw
            var terrainForwardVec = localForward - terrainNormal * f64Vector3.Dot(localForward, terrainNormal);
            var terrainForward = terrainForwardVec.SqrMagnitude > (fix64)0.001f
                ? terrainForwardVec.Normal
                : localForward;

            // LookRotation(forward, -terrainNormal) produces the correct rotation in Y-down:
            // right = Cross(-terrainNormal, forward) → (+X on flat ground when facing +Z)
            CarRotation = FixedQuaternion.LookRotation(terrainForward, -terrainNormal);
        }

        if (fix64.Abs(Speed) > 10 || !Mtouch)
        {
            conto.Rotation = FixedQuaternion.Slerp(conto.Rotation, CarRotation, fix64.Half * _tickRate);
        }
        if (Wtouch && !Capsized)
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

            var rollDelta = FixedQuaternion.AngleAxis(_tilt * _tickRate, Forward);
            // technically not the same as nfm... nfm writes yaw directly and pitch/roll via pxy/pzy...
            // but this makes sense to me...
            conto.Rotation = CarRotation * rollDelta;
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
            var pitch = (int)((random.NextFixed6401() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            var roll = (int)((random.NextFixed6401() * 6 * Speed / Stat.Swits[2] - 3 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            
            // technically this should be applied on the right/forward vectors of the conto.Rotation after we
            // change it above... but i think this is fine...
            var pitchDelta = FixedQuaternion.AngleAxis(pitch * _tickRate, Right);
            var rollDelta = FixedQuaternion.AngleAxis(roll * _tickRate, Forward);
            conto.Rotation = conto.Rotation * pitchDelta * rollDelta;
        }
        if (Wtouch && surfaceType == 1)
        {
            var pitch = (int)((random.NextFixed6401() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            var roll = (int)((random.NextFixed6401() * 4 * Speed / Stat.Swits[2] - 2 * Speed / Stat.Swits[2]) *
                                          (Stat.Bounce - (fix64)0.3f));
            
            var pitchDelta = FixedQuaternion.AngleAxis(pitch * _tickRate, Right);
            var rollDelta = FixedQuaternion.AngleAxis(roll * _tickRate, Forward);
            conto.Rotation = conto.Rotation * pitchDelta * rollDelta;
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
                _lxz = yaw;
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
            if (_lxz != yaw)
            {
                Travxz += (_lxz - yaw) * _tickRate;
                _lxz = yaw;
            }
            if (_srfcnt < (10 * (_oneOverTickRate)))
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
                                _xtpower = (int)(200 * _oneOverTickRate);
                            }
                            else
                            {
                                _xtpower = (int)(100 * _oneOverTickRate);
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
                        if (fix64.Abs(Wheels[i96].Velocity.Z) < 70 && fix64.Abs(Wheels[i96].Velocity.X) < 70)
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
                        // TODO flip the car over...
                        // Pxy += 180;
                        // conto.Xy += 180;
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
    private void PhyTrackPieceCollision(
        IStage stage, Control control, ContO conto,
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
            var position = Wheels[k].Position - new f64Vector3(0, wheelGround, 0);
            var velocity = Wheels[k].Velocity;
            
            if (!isWheelTouchingPiece[k])
            {
                Logging.Info("start wheel");
                foreach (var collidable in stage.RetrievePointCollidables(Wheels[k].Position.X, Wheels[k].Position.Z))
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
                                    Logging.Info($"TRI[{i/3}] p0=({(float)p0.X:F0},{(float)p0.Y:F0},{(float)p0.Z:F0}) inTri={inTri} surfY={(float)surfaceY:F0} localWheel=({(float)localPosition.X:F0},{(float)localPosition.Y:F0},{(float)localPosition.Z:F0})");
                                }
                            }
                            
                            // Ground/ramp triangle: snap wheel Y to surface (local space, then convert back)
                            if (triangleData.IsGround)
                            {
                                if (TriangleMesh.ResolveGround(p0, p1, p2, localPosition, triangleData) is { } groundHit)
                                {
                                    Logging.Info(triangleData.IsGround
                                        ? $"ground triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2}, groundness {(float)groundness:F2})"
                                        : $"wall triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2})");
    
                                    touching |= 1 << k;
                                    ++nGroundedWheels;
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
                                        Logging.Info($"ramp lift: {zTmp} liftDivider: {liftDivider:F2} total: {zTmp / liftDivider:F2}");
                                        Wheels[k].Position.Y -= zTmp / liftDivider;
                                    }
    
                                    if (!wasMtouch && Wheels[k].Position.Y != 7 /* * checkpoints.gravity */ * _tickRate)
                                    {
                                        fix64 dustMag = Wheels[k].Position.Y / (fix64)(333.33F);
                                        if (dustMag > (fix64)(0.3F))
                                            dustMag = (fix64)(0.3F);
                                        if (surfaceType == 0)
                                            dustMag += (fix64)1.1f;
                                        else
                                            dustMag += (fix64)1.2f;
                                        conto.Dust(k, Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, (int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Z,
                                            dustMag * Stat.Simag, 0, Capsized && Mtouch, (int)wheelGround);
                                    }
    
                                    // newY is in local space; RotateXz doesn't affect Y, so just add object Y
                                    Wheels[k].Position.Y = groundHit.newY + boxMesh.GameObjectPosition.Y + wheelGround;
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
                                    Logging.Info(triangleData.IsGround
                                        ? $"ground triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2}, groundness {(float)groundness:F2})"
                                        : $"wall triangle (normal {(float)normalizedNormal.X:F2}, {(float)normalizedNormal.Y:F2}, {(float)normalizedNormal.Z:F2})");
    
                                    // Rotate local-space push/impact back to world space
                                    var worldDelta = wallHit.positionDelta.RotateXz(boxMesh.GameObjectXz);
                                    var worldImpact = wallHit.impactComponent.RotateXz(boxMesh.GameObjectXz);
    
                                    for (int w = 0; w < 4; w++)
                                    {
                                        Wheels[w].Position.X += worldDelta.X;
                                        Wheels[w].Position.Z += worldDelta.Z;
                                    }
    
                                    _crank[0, k]++;
                                    if (_crank[0, k] > 1)
                                    {
                                        conto.Spark(Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, Wheels[k].Velocity.X, Wheels[k].Velocity.Y, Wheels[k].Velocity.Z, 0,
                                            (int)wheelGround);
                                        SfxPlayScrape(this, ((int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Y, (int)Wheels[k].Velocity.Z));
                                    }
    
                                    var reboundVelocityDelta = worldImpact * (-GetReboundMul(wasMtouch));
                                    Regz(k, reboundVelocityDelta.Length() * 1, conto, random);
                                    Wheels[k].Velocity.X += reboundVelocityDelta.X;
                                    Wheels[k].Velocity.Z += reboundVelocityDelta.Z;
    
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
                            Wtouch = true;
                            Gtouch = true;
    
                            if (!wasMtouch && Wheels[k].Velocity.Y != 7 /* * checkpoints.gravity */ * _tickRate)
                            {
                                fix64 dustMag = Wheels[k].Velocity.Y / (fix64)(333.33F);
                                if (dustMag > (fix64)(0.3F))
                                    dustMag = (fix64)(0.3F);
                                if (surfaceType == 0)
                                    dustMag += (fix64)1.1f;
                                else
                                    dustMag += (fix64)1.2f;
                                conto.Dust(k, Wheels[k].Position.X, Wheels[k].Position.X, Wheels[k].Position.Z, (int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Z, dustMag * Stat.Simag, 0, Capsized && Mtouch, (int)wheelGround);
                            }
                            Wheels[k].Position.Y = collision.newY + wheelGround; // snap wheel to the surface
                            
                            // sparks and scrape
                            if (Capsized && collidable.Skid is 0 or 1)
                            {
                                conto.Spark(Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, Wheels[k].Velocity.X, Wheels[k].Velocity.Y, Wheels[k].Velocity.Z, 1, (int)wheelGround);
                                //if (Im == /*this.xt.im*/ 0)
                                SfxPlayGscrape(this, ((int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Y, (int)Wheels[k].Velocity.Z));
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
                                Wheels[w].Position.X += collision.positionDelta.X;
                                Wheels[w].Position.Y += collision.positionDelta.Y;
                                Wheels[w].Position.Z += collision.positionDelta.Z;
                            }
                            
                            // sparks and scrapes
                            if (collidable.Skid != 2)
                                _crank[0, k]++;
                            if (collidable.Skid == 5 && random.NextFixed6401() > fix64.Half)
                                _crank[0, k]++;
                            if (_crank[0, k] > 1)
                            {
                                conto.Spark(Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, Wheels[k].Velocity.X, Wheels[k].Velocity.Y, Wheels[k].Velocity.Z, 0, (int)wheelGround);
                                //if (Im == /*this.xt.im*/ 0)
                                SfxPlayScrape(this, ((int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Y, (int)Wheels[k].Velocity.Z));
                            }
    
                            // z rebound CHK5
                            f64Vector3 reboundVelocityDelta = collision.impactComponent * (-GetReboundMul(wasMtouch));
                            Regz(k, reboundVelocityDelta.Length() * collidable.Damage, conto, random);
                            Wheels[k].Velocity.X += reboundVelocityDelta.X;
                            Wheels[k].Velocity.Y += reboundVelocityDelta.Y;
                            Wheels[k].Velocity.Z += reboundVelocityDelta.Z;
    
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
                                Logging.Info($"ramp lift: {collision.zTmp} liftDivider: {liftDivider:F2} total: {collision.zTmp / liftDivider}");
                                Wheels[k].Velocity.Y -= collision.zTmp / liftDivider;
                            }
    
                            if (collision.zTmp > -30)
                            {
                                if (collidable.Skid == 2)
                                    nWheelsDirtRamp++;
                                else
                                    nWheelsRoadRamp++;
                                
                                Wtouch = true;
                                Gtouch = false;
    
                                // sparks and scrape
                                if (Capsized && (collidable.Skid == 0 || collidable.Skid == 1))
                                {
                                    conto.Spark(Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, Wheels[k].Velocity.X, Wheels[k].Velocity.Y, Wheels[k].Velocity.Z, 1, (int)wheelGround);
                                    //if (Im == /*this.xt.im*/ 0)
                                    SfxPlayGscrape(this, ((int)Wheels[k].Velocity.X, (int)Wheels[k].Velocity.Y, (int)Wheels[k].Velocity.Z));
                                }
    
                                if (!wasMtouch && surfaceType != 0)
                                {
                                    fix64 dustMag = (fix64)1.4F;
                                    conto.Dust(k, Wheels[k].Position.X, Wheels[k].Position.Y, Wheels[k].Position.Z, (int)Wheels[k].Velocity.Y, (int)Wheels[k].Velocity.Z, dustMag * Stat.Simag, 0, Capsized && Mtouch, (int)wheelGround);
                                }
                            }
                            
                            Wheels[k].Position.X = collision.newPosition.X;
                            Wheels[k].Position.Y = collision.newPosition.Y + wheelGround;
                            Wheels[k].Position.Z = collision.newPosition.Z;
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
        // Sin(roll)  = how far the local right axis has tilted off horizontal
        fix64 sinRoll  = (fix64)(CarRotation * Right).Y;

        // Sin(pitch) = how far the local forward axis has tilted off horizontal  
        fix64 sinPitch = (fix64)(CarRotation * Forward).Y;
        
        var reboundMul = (fix64.Abs(sinRoll) + fix64.Abs(sinPitch));
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
    //     /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Multion >= 2)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
    //     {
    //         abool = true;
    //     }*/
    //     f *= Stat.Dammult;
    //     if (fix64.Abs(f) > 100)
    //     {
    //         //Record.Recx(i, f, Im);
    //         if (f > 100)
    //         {
    //             f -= 100;
    //         }
    //         if (f < -100)
    //         {
    //             f += 100;
    //         }
    //         Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
    //         if (/*Im == XTGraphics.Im*/true || _colidim)
    //         {
    //             SfxPlayCrash(this, ((int)f, 0));
    //             //XTGraphics.Acrash(Im, f, 0);
    //         }
    //         for (var i111 = 0; i111 < 40; i111++)
    //         {
    //             fix64 f112 = 0;
    //             for (var i113 = 0; i113 < 4; i113++)
    //             {
    //                 f112 = f / 20 * random.NextFixed6401();
    //                 if (abool)
    //                 {
    //                     Hitmag += (int)fix64.Abs(f112);
    //                     i110 += (int)fix64.Abs(f112);
    //                 }
    //             }
    //         }
    //     }
        return i110;
    }
    
    private int Regy(int i, fix64 f, ContO conto, DeterministicRandom random)
    {
        conto.DamageY(Stat, i, f, Mtouch, _nbsq, Squash);
        var i97 = 0;
    //     var abool = true;
    //     /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Multion >= 2)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
    //     {
    //         abool = true;
    //     }*/
    //     f *= Stat.Dammult;
    //     if (f > 100)
    //     {
    //         //Record.Recy(i, f, Mtouch, Im);
    //         f -= 100;
    //         var i98 = 0;
    //         var i99 = 0;
    //         var i100 = conto.Zy;
    //         var i101 = conto.Xy;
    //         for ( /**/; i100 < 360; i100 += 360)
    //         {
    //         }
    //         for ( /**/; i100 > 360; i100 -= 360)
    //         {
    //         }
    //         if (i100 < 210 && i100 > 150)
    //         {
    //             i98 = -1;
    //         }
    //         if (i100 > 330 || i100 < 30)
    //         {
    //             i98 = 1;
    //         }
    //         for ( /**/; i101 < 360; i101 += 360)
    //         {
    //         }
    //         for ( /**/; i101 > 360; i101 -= 360)
    //         {
    //         }
    //         if (i101 < 210 && i101 > 150)
    //         {
    //             i99 = -1;
    //         }
    //         if (i101 > 330 || i101 < 30)
    //         {
    //             i99 = 1;
    //         }
    //         if (i99 * i98 == 0)
    //         {
    //             Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
    //         }
    //         
    //         if (/*Im == XTGraphics.Im ||*/true || _colidim)
    //         {
    //             SfxPlayCrash(this, ((int)f, i99 * i98));
    //             //XTGraphics.Acrash(Im, f, i99 * i98);
    //         }
    //         if (i99 * i98 == 0 || Mtouch)
    //         {
    //             for (var i102 = 0; i102 < 40; i102++)
    //             {
    //                 fix64 f103 = 0;
    //                 for (var i104 = 0; i104 < 4; i104++)
    //                 {
    //                     f103 = f / 20 * random.NextFixed6401();
    //                     if (abool)
    //                     {
    //                         Hitmag += (int)fix64.Abs(f103);
    //                         i97 += (int)fix64.Abs(f103);
    //                     }
    //                 }
    //             }
    //         }
    //         if (i99 * i98 == -1)
    //         {
    //             if (_nbsq > 0)
    //             {
    //                 var i105 = 0;
    //                 var i106 = 1;
    //                 for (var i107 = 0; i107 < 40; i107++)
    //                 {
    //                     fix64 f108 = 0;
    //                     for (var i109 = 0; i109 < 4; i109++)
    //                     {
    //                         f108 = f / 15 * random.NextFixed6401();
    //                         i105 += (int)f108;
    //                         i106++;
    //                         if (abool)
    //                         {
    //                             Hitmag += (int)fix64.Abs(f108);
    //                             i97 += (int)fix64.Abs(f108);
    //                         }
    //                     }
    //                 }
    //                 Squash += i105 / i106;
    //                 _nbsq = 0;
    //             }
    //             else
    //             {
    //                 _nbsq++;
    //             }
    //         }
    //     }
    return i97;
    }
    
    private int Regz(int i, fix64 f, ContO conto, DeterministicRandom random)
    {
    conto.DamageZ(Stat, i, f);
    var i114 = 0;
    var abool = true;
    //     /*if (XTGraphics.Multion == 1 && XTGraphics.Im != Im)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Multion >= 2)
    //     {
    //         abool = false;
    //     }
    //     if (XTGraphics.Lan && XTGraphics.Multion >= 1 && XTGraphics.Isbot[Im])
    //     {
    //         abool = true;
    //     }*/
    //     f *= Stat.Dammult;
    //     if (fix64.Abs(f) > 100)
    //     {
    //         //Record.Recz(i, f, Im);
    //         if (f > 100)
    //         {
    //             f -= 100;
    //         }
    //         if (f < -100)
    //         {
    //             f += 100;
    //         }
    //         Shakedam = (int)((fix64.Abs(f) + Shakedam) * fix64.Half);
    //         
    //         if (/*Im == XTGraphics.Im ||*/true || _colidim)
    //         {
    //             SfxPlayCrash(this, ((int)f, 0));
    //             //XTGraphics.Acrash(Im, f, 0);
    //         }
    //         for (var i115 = 0; i115 < 40; i115++)
    //         {
    //             fix64 f116 = 0;
    //             for (var i117 = 0; i117 < 4; i117++)
    //             {
    //                 f116 = f / 20 * random.NextFixed6401();
    //                 if (abool)
    //                 {
    //                     Hitmag += (int)fix64.Abs(f116);
    //                     i114 += (int)fix64.Abs(f116);
    //                 }
    //             }
    //         }
    //     }
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
        Wheels = new InlineArray4<Wheel>();
        CarRotation = FixedQuaternion.Identity;
        Speed = 0;
        _forca = (fix64.Sqrt(conto.Keyz[0] * conto.Keyz[0] + conto.Keyx[0] * conto.Keyx[0]) +
                  fix64.Sqrt(conto.Keyz[1] * conto.Keyz[1] + conto.Keyx[1] * conto.Keyx[1]) +
                  fix64.Sqrt(conto.Keyz[2] * conto.Keyz[2] + conto.Keyx[2] * conto.Keyx[2]) +
                  fix64.Sqrt(conto.Keyz[3] * conto.Keyz[3] + conto.Keyx[3] * conto.Keyx[3])) / 10000 *
                 (Stat.Bounce - (fix64)0.3f);
        Mtouch = false;
        Wtouch = false;
        LastYaw = 0;
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