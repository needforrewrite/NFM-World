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

    public MadSfx(Mad mad)
    {
        mad.SfxPlayCrash += SfxPlayCrash;
        mad.SfxPlayScrape += SfxPlayScrape;
        mad.SfxPlayGscrape += SfxPlayGscrape;
        mad.SfxPlaySkid += SfxPlaySkid;
    }

    public void Tick()
    {
        if (bfcrash > 0) {
            bfcrash--;
        }
        if (bfskid > 0) {
            bfskid--;
        }
        if (bfscrape > 0) {
            bfscrape--;
        }
        if (bfsc1 > 0) {
            bfsc1--;
        }
        if (bfsc2 > 0) {
            bfsc2--;
        }
    }

    private void SfxPlayScrape(object? sender, (int i, int i266, int i267) position)
    {
        if (bfscrape == 0 && Math.Sqrt(position.i * position.i + position.i266 * position.i266 + position.i267 * position.i267) / 10.0 > 10.0) {
            int i268 = 0;
            if (Random.Shared.NextInt64() > Random.Shared.NextInt64()) {
                i268 = 1;
            }
            if (i268 == 0) {
                sturn1 = 0;
                sturn0++;
                if (sturn0 == 3) {
                    i268 = 1;
                    sturn1 = 1;
                    sturn0 = 0;
                }
            } else {
                sturn0 = 0;
                sturn1++;
                if (sturn1 == 3) {
                    i268 = 0;
                    sturn0 = 1;
                    sturn1 = 0;
                }
            }
            //if (!mutes) {
                SfxLibrary.scrape[i268].Play();
            //}
            bfscrape = (int)(5 * (1/GameSparker.PHYSICS_MULTIPLIER));
        }
    }

    private void SfxPlayGscrape(object? sender, (int i, int i269, int i270) position)
    {
        if ((bfsc1 == 0 || bfsc2 == 0) && Math.Sqrt(position.i * position.i + position.i269 * position.i269 + position.i270 * position.i270) / 10.0 > 15.0)
            if (bfsc1 == 0) {
                //if (!mutes) {
                    SfxLibrary.scrape[2].Stop();
                    SfxLibrary.scrape[2].Play();
                //}
                bfsc1 = (int)(12 * (1/GameSparker.PHYSICS_MULTIPLIER));
                bfsc2 = (int)(6 * (1/GameSparker.PHYSICS_MULTIPLIER));
            } else {
                //if (!mutes) {
                    SfxLibrary.scrape[3].Stop();
                    SfxLibrary.scrape[3].Play();
                //}
                bfsc2 = (int)(12 * (1/GameSparker.PHYSICS_MULTIPLIER));
                bfsc1 = (int)(6 * (1/GameSparker.PHYSICS_MULTIPLIER));
            }
    }

    private void SfxPlayCrash(object? sender, (float f, int i) crashData)
    {
#if USE_BASS
        if (bfcrash == 0) {
            if (crashData.i == 0) {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F) {
                    //if (!mutes) {
                        SfxLibrary.lowcrash[crshturn].Play();
                    //}
                    bfcrash = (int)(2 * (1/GameSparker.PHYSICS_MULTIPLIER));;
                }
                if (Math.Abs(crashData.f) >= 170.0F) {
                    //if (!mutes) {
                        SfxLibrary.crash[crshturn].Play();
                    //}
                    bfcrash = (int)(2 * (1/GameSparker.PHYSICS_MULTIPLIER));;
                }
                if (Math.Abs(crashData.f) > 25.0F) {
                    if (crashup) {
                        crshturn--;
                    } else {
                        crshturn++;
                    }
                    if (crshturn == -1) {
                        crshturn = 2;
                    }
                    if (crshturn == 3) {
                        crshturn = 0;
                    }
                }
            }
            if (crashData.i == -1) {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F) {
                    //if (!mutes) {
                        SfxLibrary.lowcrash[2].Play();
                    //}
                    bfcrash = (int)(2 * (1/GameSparker.PHYSICS_MULTIPLIER));
                }
                if (Math.Abs(crashData.f) > 170.0F) {
                    //if (!mutes) {
                        SfxLibrary.crash[2].Play();
                    //}
                    bfcrash = (int)(2 * (1/GameSparker.PHYSICS_MULTIPLIER));
                }
            }
            if (crashData.i == 1) {
                //if (!mutes) {
                    SfxLibrary.tires?.Play();
                //}
                bfcrash = (int)(3 * (1/GameSparker.PHYSICS_MULTIPLIER));
            }
        }
#endif
    }

    private void SfxPlaySkid(object? sender, (int surfaceType, float skidIntensity) skidData)
    {
#if USE_BASS
        if (bfcrash == 0 && bfskid == 0 && skidData.skidIntensity > 150.0F) {
            if (skidData.surfaceType == 0) {
                //if (!mutes) {
                    SfxLibrary.skid[skflg].Play();
                    //skid[skflg].play();
                //}
                if (skidup) {
                    skflg++;
                } else {
                    skflg--;
                }
                if (skflg == 3) {
                    skflg = 0;
                }
                if (skflg == -1) {
                    skflg = 2;
                }
            } else {
                //if (!mutes) {
                    SfxLibrary.dustskid[dskflg].Play();
                    //dustskid[dskflg].play();
                //}
                if (skidup) {
                    dskflg++;
                } else {
                    dskflg--;
                }
                if (dskflg == 3) {
                    dskflg = 0;
                }
                if (dskflg == -1) {
                    dskflg = 2;
                }
            }
            bfskid = (int)(35 * (1/GameSparker.PHYSICS_MULTIPLIER));
        }
#endif
    }
}