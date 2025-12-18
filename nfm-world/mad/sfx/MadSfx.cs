using NFMWorld.Mad;

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
    private bool[] pengs = new bool[5];

    private Mad Mad;

    public MadSfx(Mad mad)
    {
        Mad = mad;

        Mad.SfxPlayCrash += SfxPlayCrash;
        Mad.SfxPlayScrape += SfxPlayScrape;
        Mad.SfxPlayGscrape += SfxPlayGscrape;
        Mad.SfxPlaySkid += SfxPlaySkid;
    }

    private void SparkEng(int i, int i263, CarStats stats)
    {
        if (lcn != i263)
        {
            for (int i264 = 0; i264 < 5; i264++)
                if (pengs[i264])
                {
                    SfxLibrary.engs[stats.Enginsignature, i264].Stop();
                    pengs[i264] = false;
                }
            lcn = i263;
        }
        i++;
        for (int i265 = 0; i265 < 5; i265++)
            if (i == i265)
            {
                if (!pengs[i265])
                {
                    SfxLibrary.engs[stats.Enginsignature, i265].Loop();
                    pengs[i265] = true;
                }
            }
            else if (pengs[i265])
            {
                SfxLibrary.engs[stats.Enginsignature, i265].Stop();
                pengs[i265] = false;
            }
    }

    private void StopAirs()
    {
        for (int i271 = 0; i271 < 6; i271++)
        {
            SfxLibrary.air[i271].Stop();
        }
    }

    public void Tick(Control control, Mad mad, CarStats stats)
    {
        if (/*(fase == 0 || fase == 7001) && starcnt < 35 && cntwis != 8 && !mutes*/true)
        {
            bool bool1 = control.Up && mad.Speed > 0.0F || control.Down && mad.Speed < 10.0F;
            bool bool257 = mad.Skid == 1 && control.Handb || Math.Abs(mad.Scz[0] - (mad.Scz[1] + mad.Scz[0] + mad.Scz[2] + mad.Scz[3]) / 4.0F) > 1.0F || Math.Abs(mad.Scx[0] - (mad.Scx[1] + mad.Scx[0] + mad.Scx[2] + mad.Scx[3]) / 4.0F) > 1.0F;
            bool bool258 = false;
            if (control.Up && mad.Speed < 10.0F)
            {
                bool257 = true;
                bool1 = true;
                bool258 = true;
            }
            if (bool1 && mad.Mtouch)
            {
                if (!mad.BadLanding)
                {
                    if (!bool257)
                    {
                        if (mad.Power != 98.0F)
                        {
                            if (Math.Abs(mad.Speed) > 0.0F && Math.Abs(mad.Speed) <= stats.Swits[0])
                            {
                                int i259 = (int)(3.0F * Math.Abs(mad.Speed) / stats.Swits[0]);
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
                                SparkEng(i259, mad.Cn, stats);
                            }
                            if (Math.Abs(mad.Speed) > stats.Swits[0] && Math.Abs(mad.Speed) <= stats.Swits[1])
                            {
                                int i260 = (int)(3.0F * (Math.Abs(mad.Speed) - stats.Swits[0]) / (stats.Swits[1] - stats.Swits[0]));
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
                                SparkEng(i260, mad.Cn, stats);
                            }
                            if (Math.Abs(mad.Speed) > stats.Swits[1] && Math.Abs(mad.Speed) <= stats.Swits[2])
                            {
                                int i261 = (int)(3.0F * (Math.Abs(mad.Speed) - stats.Swits[1]) / (stats.Swits[2] - stats.Swits[1]));
                                SparkEng(i261, mad.Cn, stats);
                            }
                        }
                        else
                        {
                            int i262 = 2;
                            if (pwait == 0)
                            {
                                if (Math.Abs(mad.Speed) > stats.Swits[1])
                                {
                                    i262 = 3;
                                }
                            }
                            else
                            {
                                pwait--;
                            }
                            SparkEng(i262, mad.Cn, stats);
                        }
                    }
                    else
                    {
                        SparkEng(-1, mad.Cn, stats);
                        if (bool258)
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
                    SparkEng(3, mad.Cn, stats);
                }
                grrd = false;
                aird = false;
            }
            else
            {
                pwait = 15;
                if (!mad.Mtouch && !grrd && Random.Shared.NextDouble() > 0.4)
                {
                    SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)].Loop();
                    stopcnt = 5;
                    grrd = true;
                }
                if (!mad.Wtouch && !aird)
                {
                    StopAirs();
                    SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)].Loop();
                    stopcnt = 10;
                    aird = true;
                }
                SparkEng(-1, mad.Cn, stats);
            }
            if (mad.Cntdest != 0 && cntwis < 7)
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
            SparkEng(-2, mad.Cn, stats);
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
        if (mad.Newcar)
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
        if (mad.Cntdest != 0 && cntwis < 7)
        {
            if (mad.Wasted)
            {
                cntwis++;
            }
        }
        else
        {
            if (mad.Cntdest == 0)
            {
                cntwis = 0;
            }
            if (cntwis == 7)
            {
                cntwis = 8;
            }
        }
    }

    private void SfxPlayScrape(object? sender, (int i, int i266, int i267) position)
    {
        if (bfscrape == 0 && Math.Sqrt(position.i * position.i + position.i266 * position.i266 + position.i267 * position.i267) / 10.0 > 10.0)
        {
            int i268 = 0;
            if (Random.Shared.NextInt64() > Random.Shared.NextInt64())
            {
                i268 = 1;
            }
            if (i268 == 0)
            {
                sturn1 = 0;
                sturn0++;
                if (sturn0 == 3)
                {
                    i268 = 1;
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
                    i268 = 0;
                    sturn0 = 1;
                    sturn1 = 0;
                }
            }
            //if (!mutes) {
            SfxLibrary.scrape[i268].Play();
            //}
            bfscrape = (int)(5 * (1 / GameSparker.PHYSICS_MULTIPLIER));
        }
    }

    private void SfxPlayGscrape(object? sender, (int i, int i269, int i270) position)
    {
        if ((bfsc1 == 0 || bfsc2 == 0) && Math.Sqrt(position.i * position.i + position.i269 * position.i269 + position.i270 * position.i270) / 10.0 > 15.0)
            if (bfsc1 == 0)
            {
                //if (!mutes) {
                SfxLibrary.scrape[2].Stop();
                SfxLibrary.scrape[2].Play();
                //}
                bfsc1 = (int)(12 * (1 / GameSparker.PHYSICS_MULTIPLIER));
                bfsc2 = (int)(6 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            }
            else
            {
                //if (!mutes) {
                SfxLibrary.scrape[3].Stop();
                SfxLibrary.scrape[3].Play();
                //}
                bfsc2 = (int)(12 * (1 / GameSparker.PHYSICS_MULTIPLIER));
                bfsc1 = (int)(6 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            }
    }

    private void SfxPlayCrash(object? sender, (float f, int i) crashData)
    {
#if USE_BASS
        if (bfcrash == 0)
        {
            if (crashData.i == 0)
            {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F)
                {
                    //if (!mutes) {
                    SfxLibrary.lowcrash[crshturn].Play();
                    //}
                    bfcrash = (int)(2 * (1 / GameSparker.PHYSICS_MULTIPLIER)); ;
                }
                if (Math.Abs(crashData.f) >= 170.0F)
                {
                    //if (!mutes) {
                    SfxLibrary.crash[crshturn].Play();
                    //}
                    bfcrash = (int)(2 * (1 / GameSparker.PHYSICS_MULTIPLIER)); ;
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
                    //if (!mutes) {
                    SfxLibrary.lowcrash[2].Play();
                    //}
                    bfcrash = (int)(2 * (1 / GameSparker.PHYSICS_MULTIPLIER));
                }
                if (Math.Abs(crashData.f) > 170.0F)
                {
                    //if (!mutes) {
                    SfxLibrary.crash[2].Play();
                    //}
                    bfcrash = (int)(2 * (1 / GameSparker.PHYSICS_MULTIPLIER));
                }
            }
            if (crashData.i == 1)
            {
                //if (!mutes) {
                SfxLibrary.tires?.Play();
                //}
                bfcrash = (int)(3 * (1 / GameSparker.PHYSICS_MULTIPLIER));
            }
        }
#endif
    }

    private void SfxPlaySkid(object? sender, (int surfaceType, float skidIntensity) skidData)
    {
#if USE_BASS
        if (bfcrash == 0 && bfskid == 0 && skidData.skidIntensity > 150.0F)
        {
            if (skidData.surfaceType == 0)
            {
                //if (!mutes) {
                SfxLibrary.skid[skflg].Play();
                //skid[skflg].play();
                //}
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
                //if (!mutes) {
                SfxLibrary.dustskid[dskflg].Play();
                //dustskid[dskflg].play();
                //}
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
            bfskid = (int)(35 * (1 / GameSparker.PHYSICS_MULTIPLIER));
        }
#endif
    }
}