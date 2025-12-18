using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;

public static class SfxLibrary
{
    public static ISoundClip[] crash = new ISoundClip[3];
    public static ISoundClip[] lowcrash = new ISoundClip[3];
    public static ISoundClip[] skid = new ISoundClip[3];
    public static ISoundClip[] dustskid = new ISoundClip[3];
    public static ISoundClip[] scrape = new ISoundClip[4];

    public static ISoundClip? tires;

    public static void LoadSounds()
    {
#if USE_BASS
        crash[0] = IBackend.Backend.GetSound("data/sound/crash1.wav");
        crash[1] = IBackend.Backend.GetSound("data/sound/crash2.wav");
        crash[2] = IBackend.Backend.GetSound("data/sound/crash3.wav");

        lowcrash[0] = IBackend.Backend.GetSound("data/sound/lowcrash1.wav");
        lowcrash[1] = IBackend.Backend.GetSound("data/sound/lowcrash2.wav");
        lowcrash[2] = IBackend.Backend.GetSound("data/sound/lowcrash3.wav");

        skid[0] = IBackend.Backend.GetSound("data/sound/skid1.wav");
        skid[1] = IBackend.Backend.GetSound("data/sound/skid2.wav");
        skid[2] = IBackend.Backend.GetSound("data/sound/skid3.wav");

        dustskid[0] = IBackend.Backend.GetSound("data/sound/dustskid1.wav");
        dustskid[1] = IBackend.Backend.GetSound("data/sound/dustskid2.wav");
        dustskid[2] = IBackend.Backend.GetSound("data/sound/dustskid3.wav");

        scrape[0] = IBackend.Backend.GetSound("data/sound/scrape1.wav");
        scrape[1] = IBackend.Backend.GetSound("data/sound/scrape2.wav");
        scrape[2] = IBackend.Backend.GetSound("data/sound/scrape2.wav");
        scrape[3] = IBackend.Backend.GetSound("data/sound/scrape2.wav");

        tires = IBackend.Backend.GetSound("data/sound/tires.wav");
#endif
    }
}