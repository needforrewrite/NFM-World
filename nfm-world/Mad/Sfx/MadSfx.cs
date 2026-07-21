using Maxine.Extensions;
using NFMWorldLibrary;
using NFMWorldLibrary.FixedMath;

namespace NFMWorld.Sfx;

public class MadSfx
{
    private int bfcrash = 0;
    private int bfskid = 0;
    private bool skidup = false;
    private int skflg = 0;
    private int dskflg = 0;
    private int crshturn = 0;
    private bool crashup = false;
    private int bfscrape = 0;
    private int sturn0 = 0;
    private int sturn1 = 0;
    private int bfsc1 = 0;
    private int bfsc2 = 0;
    private int pwait = 7;
    private int stopcnt = 0;
    private bool grrd = false;
    private bool aird = false;
    private int cntwis = 0;
    private bool pwastd = false;
    private int lcn = 0;
    private bool[] pengs = new bool[7];

    private CarPhysics _carPhysics;

    public bool Mute = false;

    public MadSfx(CarPhysics carPhysics)
    {
        _carPhysics = carPhysics;

        _carPhysics.SfxPlayCrash += SfxPlayCrash;
        _carPhysics.SfxPlayScrape += SfxPlayScrape;
        _carPhysics.SfxPlayGscrape += SfxPlayGscrape;
        _carPhysics.SfxPlaySkid += SfxPlaySkid;
        _carPhysics.PowerUp += SfxPlayPowerup;
    }

    private void SfxPlayPowerup(object? sender, float f)
    {
        if(!Mute) SfxLibrary.powerup?.Play();
    }

    private void SparkEng(int cgear, int cn, CarStats stats)
    {
        if (!Mute)
        {
            if (lcn != cn)
            {
                for (int gear = 0; gear < 5; gear++)
                    if (pengs[gear])
                    {
                        SfxLibrary.engs[stats.Enginsignature, gear].Stop();
                        pengs[gear] = false;
                    }
                lcn = cn;
            }
            cgear++;
            for (int gear = 0; gear < 5; gear++)
                if (cgear == gear)
                {
                    if (!pengs[gear])
                    {
                        SfxLibrary.engs[stats.Enginsignature, gear].Loop();
                        pengs[gear] = true;
                    }
                }
                else if (pengs[gear])
                {
                    SfxLibrary.engs[stats.Enginsignature, gear].Stop();
                    pengs[gear] = false;
                }
        }

    }

    private void StopAirs()
    {
        for (int airs = 0; airs < 6; airs++)
        {
            SfxLibrary.air[airs].Stop();
        }
    }

    public void Tick(Control control, CarPhysics carPhysics, CarStats stats)
    {
        if (!Mute)
        {
            if (/*(fase == 0 || fase == 7001) && starcnt < 35 && cntwis != 8 && !mutes*/true)
            {
                bool moving = control.Up && carPhysics.Speed > (fix64)0.0F || control.Down && carPhysics.Speed < (fix64)10.0F;
                bool drifting = carPhysics.Skid == 1 && control.Handb || fix64.Abs(carPhysics.Scz[0] - (carPhysics.Scz[1] + carPhysics.Scz[0] + carPhysics.Scz[2] + carPhysics.Scz[3]) / (fix64)4.0F) > (fix64)1.0F || fix64.Abs(carPhysics.Scx[0] - (carPhysics.Scx[1] + carPhysics.Scx[0] + carPhysics.Scx[2] + carPhysics.Scx[3]) / (fix64)4.0F) > (fix64)1.0F;
                bool revbraking = false;
                if (control.Up && carPhysics.Speed < (fix64)10.0F)
                {
                    drifting = true;
                    moving = true;
                    revbraking = true;
                }
                if (moving && carPhysics.Mtouch)
                {
                    if (!carPhysics.BadLanding)
                    {
                        if (!drifting)
                        {
                            if (carPhysics.Power != (fix64)98.0F)
                            {
                                if (fix64.Abs(carPhysics.Speed) > (fix64)0.0F && fix64.Abs(carPhysics.Speed) <= stats.Swits[0])
                                {
                                    int i259 = (int)((fix64)3.0F * fix64.Abs(carPhysics.Speed) / stats.Swits[0]);
                                    if (i259 == 2)
                                    {
                                        if (pwait == 0)
                                        {
                                            i259 = 0;
                                        }
                                        else
                                        {
                                            pwait--;
                                        }
                                    }
                                    else
                                    {
                                        pwait = 7;
                                    }
                                    SparkEng(i259, carPhysics.Cn, stats);
                                }
                                if (fix64.Abs(carPhysics.Speed) > stats.Swits[0] && fix64.Abs(carPhysics.Speed) <= stats.Swits[1])
                                {
                                    int i260 = (int)((fix64)3.0F * (fix64.Abs(carPhysics.Speed) - stats.Swits[0]) / (stats.Swits[1] - stats.Swits[0]));
                                    if (i260 == 2)
                                    {
                                        if (pwait == 0)
                                        {
                                            i260 = 0;
                                        }
                                        else
                                        {
                                            pwait--;
                                        }
                                    }
                                    else
                                    {
                                        pwait = 7;
                                    }
                                    SparkEng(i260, carPhysics.Cn, stats);
                                }
                                if (fix64.Abs(carPhysics.Speed) > stats.Swits[1] && fix64.Abs(carPhysics.Speed) <= stats.Swits[2])
                                {
                                    int i261 = (int)((fix64)3.0F * (fix64.Abs(carPhysics.Speed) - stats.Swits[1]) / (stats.Swits[2] - stats.Swits[1]));
                                    SparkEng(i261, carPhysics.Cn, stats);
                                }
                            }
                            else
                            {
                                int i262 = 2;
                                if (pwait == 0)
                                {
                                    if (fix64.Abs(carPhysics.Speed) > stats.Swits[1])
                                    {
                                        i262 = 3;
                                    }
                                }
                                else
                                {
                                    pwait--;
                                }
                                SparkEng(i262, carPhysics.Cn, stats);
                            }
                        }
                        else
                        {
                            SparkEng(-1, carPhysics.Cn, stats);
                            if (revbraking)
                            {
                                if (stopcnt <= 0)
                                {
                                    SfxLibrary.air[5].Loop();
                                    stopcnt = 10;
                                }
                            }
                            else if (stopcnt <= -2)
                            {
                                SfxLibrary.air[2 + (int)(Random.Shared.NextDouble() * 3.0F)].Loop();
                                stopcnt = 7;
                            }
                        }
                    }
                    else
                    {
                        SparkEng(3, carPhysics.Cn, stats);
                    }
                    grrd = false;
                    aird = false;
                }
                else
                {
                    pwait = 15;
                    if (!carPhysics.Mtouch && !grrd && Random.Shared.NextDouble() > 0.4)
                    {
                        SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)].Loop();
                        stopcnt = 5;
                        grrd = true;
                    }
                    if (!carPhysics.Wtouch && !aird)
                    {
                        StopAirs();
                        SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)].Loop();
                        stopcnt = 10;
                        aird = true;
                    }
                    SparkEng(-1, carPhysics.Cn, stats);
                }
                if (carPhysics.Cntdest != 0 && cntwis < 7)
                {
                    if (!pwastd)
                    {
                        SfxLibrary.wastd?.Loop();
                        pwastd = true;
                    }
                }
                else
                {
                    if (pwastd)
                    {
                        SfxLibrary.wastd?.Stop();
                        pwastd = false;
                    }
                    if (cntwis == 7/* && !mutes*/)
                    {
                        SfxLibrary.firewasted?.Play();
                    }
                }
            }
            else
            {
                SparkEng(-2, carPhysics.Cn, stats);
                if (pwastd)
                {
                    SfxLibrary.wastd?.Stop();
                    pwastd = false;
                }
            }
            if (stopcnt != -20)
            {
                if (stopcnt == 1)
                {
                    StopAirs();
                }
                stopcnt--;
            }
            if (bfcrash != 0)
            {
                bfcrash--;
            }
            if (bfscrape != 0)
            {
                bfscrape--;
            }
            if (bfsc1 != 0)
            {
                bfsc1--;
            }
            if (bfsc2 != 0)
            {
                bfsc2--;
            }
            if (bfskid != 0)
            {
                bfskid--;
            }
            if (carPhysics.Newcar)
            {
                cntwis = 0;
            }
            /*if (fase == 0 || fase == 7001 || fase == 6 || fase == -1 || fase == -2 || fase == -3 || fase == -4 || fase == -5) {
            if (mutes != control.mutes) {
                mutes = control.mutes;
            }
            if (control.mutem != mutem) {
                mutem = control.mutem;
                if (mutem) {
                    if (loadedt) {
                        strack.setPaused(true);
                    }
                } else if (loadedt) {
                    strack.setPaused(false);
                }
            }
        }*/
            if (carPhysics.Cntdest != 0 && cntwis < 7)
            {
                if (carPhysics.Wasted)
                {
                    cntwis++;
                }
            }
            else
            {
                if (carPhysics.Cntdest == 0)
                {
                    cntwis = 0;
                }
                if (cntwis == 7)
                {
                    cntwis = 8;
                }
            }
        }
    }

    private void SfxPlayScrape(object? sender, (int i, int i266, int i267) position)
    {
        if (!Mute && bfscrape == 0 && Math.Sqrt(position.i * position.i + position.i266 * position.i266 + position.i267 * position.i267) / 10.0 > 10.0)
        {
            int scrapes = 0;
            if (Random.Shared.NextBoolean())
            {
                scrapes = 1;
            }
            if (scrapes == 0)
            {
                sturn1 = 0;
                sturn0++;
                if (sturn0 == 3)
                {
                    scrapes = 1;
                    sturn1 = 1;
                    sturn0 = 0;
                }
            }
            else
            {
                sturn0 = 0;
                sturn1++;
                if (sturn1 == 3)
                {
                    scrapes = 0;
                    sturn0 = 1;
                    sturn1 = 0;
                }
            }
            SfxLibrary.scrape[scrapes].Play();
            bfscrape = (int)(5 * (1 / Physics.PHYSICS_MULTIPLIER));
        }
    }

    private void SfxPlayGscrape(object? sender, (int i, int i269, int i270) position)
    {
        if (!Mute && (bfsc1 == 0 || bfsc2 == 0) && Math.Sqrt(position.i * position.i + position.i269 * position.i269 + position.i270 * position.i270) / 10.0 > 15.0)
            if (bfsc1 == 0)
            {
                SfxLibrary.scrape[2].Stop();
                SfxLibrary.scrape[2].Play();
                bfsc1 = (int)(12 * (1 / Physics.PHYSICS_MULTIPLIER));
                bfsc2 = (int)(6 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
            else
            {
                SfxLibrary.scrape[3].Stop();
                SfxLibrary.scrape[3].Play();
                bfsc2 = (int)(12 * (1 / Physics.PHYSICS_MULTIPLIER));
                bfsc1 = (int)(6 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
    }

    private void SfxPlayCrash(object? sender, (float f, int i) crashData)
    {
        crashData.f *= 1 / Physics.PHYSICS_MULTIPLIER;
        if (!Mute && bfcrash == 0)
        {
            if (crashData.i == 0)
            {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F)
                {
                    SfxLibrary.lowcrash[crshturn].Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }
                if (Math.Abs(crashData.f) >= 170.0F)
                {
                    SfxLibrary.crash[crshturn].Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }
                if (Math.Abs(crashData.f) > 25.0F)
                {
                    if (crashup)
                    {
                        crshturn--;
                    }
                    else
                    {
                        crshturn++;
                    }
                    if (crshturn == -1)
                    {
                        crshturn = 2;
                    }
                    if (crshturn == 3)
                    {
                        crshturn = 0;
                    }
                }
            }
            if (crashData.i == -1)
            {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F)
                {
                    SfxLibrary.lowcrash[2].Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }
                if (Math.Abs(crashData.f) > 170.0F)
                {
                    SfxLibrary.crash[2].Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }
            }
            if (crashData.i == 1)
            {
                SfxLibrary.tires?.Play();
                bfcrash = (int)(3 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
        }
    }

    private void SfxPlaySkid(object? sender, (CarPhysics.SurfaceType surfaceType, float skidIntensity) skidData)
    {
        if (!Mute && bfcrash == 0 && bfskid == 0 && skidData.skidIntensity > 150.0F)
        {
            if (skidData.surfaceType == CarPhysics.SurfaceType.Road)
            {
                SfxLibrary.skid[skflg].Play();
                if (skidup)
                {
                    skflg++;
                }
                else
                {
                    skflg--;
                }
                if (skflg == 3)
                {
                    skflg = 0;
                }
                if (skflg == -1)
                {
                    skflg = 2;
                }
            }
            else
            {
                SfxLibrary.dustskid[dskflg].Play();
                if (skidup)
                {
                    dskflg++;
                }
                else
                {
                    dskflg--;
                }
                if (dskflg == 3)
                {
                    dskflg = 0;
                }
                if (dskflg == -1)
                {
                    dskflg = 2;
                }
            }
            bfskid = (int)(35 * (1 / Physics.PHYSICS_MULTIPLIER));
        }
    }
}