using NFMWorld.DriverInterface;
using NFMWorld.SkiaDriver;

public static class SfxLibrary
{
    public static ISoundClip[] crash = new ISoundClip[3];
    public static ISoundClip[] lowcrash = new ISoundClip[3];
    public static ISoundClip[] skid = new ISoundClip[3];
    public static ISoundClip[] dustskid = new ISoundClip[3];
    public static ISoundClip[] scrape = new ISoundClip[4];
    public static ISoundClip[] air = new ISoundClip[6];

    public static ISoundClip[,] engs = new ISoundClip[5,5]; // [engine, level]

    public static ISoundClip? wastd;
    public static ISoundClip? firewasted;

    public static ISoundClip[] countdown = new ISoundClip[4];


    public static ISoundClip? tires;

    public static void LoadSounds()
    {
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

        for (int engine = 0; engine < 5; engine++)
        {
            for (int level = 0; level < 5; level++)
            {
                string path = GetEngineSoundPath(GetEngineSignature(engine), level);
                engs[engine, level] = IBackend.Backend.GetSound(path);
            }
        }

        wastd = IBackend.Backend.GetSound("data/sound/wasted.wav");
        firewasted = IBackend.Backend.GetSound("data/sound/firewasted.wav");

        air[0] = IBackend.Backend.GetSound("data/sound/air0.wav");
        air[1] = IBackend.Backend.GetSound("data/sound/air1.wav");
        air[2] = IBackend.Backend.GetSound("data/sound/air2.wav");
        air[3] = IBackend.Backend.GetSound("data/sound/air3.wav");
        air[4] = IBackend.Backend.GetSound("data/sound/air4.wav");
        air[5] = IBackend.Backend.GetSound("data/sound/air5.wav");

        countdown[0] = IBackend.Backend.GetSound("data/sound/go.wav");
        countdown[1] = IBackend.Backend.GetSound("data/sound/one.wav");
        countdown[2] = IBackend.Backend.GetSound("data/sound/two.wav");
        countdown[3] = IBackend.Backend.GetSound("data/sound/three.wav");
    }

    private static string GetEngineSignature(int engine)
    {
        return engine switch
        {
            0 => "normal",
            1 => "v8",
            2 => "retro",
            3 => "power",
            4 => "diesel",
            _ => "normal",
        };
    }

    private static string GetEngineSoundPath(string engineSignature, int level)
    {
        return $"data/sound/engine/{engineSignature}/{level}.wav";
    }
}