using Maxine.Extensions;
using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorld.DriverInterface.DriverInterface;

namespace NFMWorld.Sfx;

public class MadSfx : IDisposable
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
    private bool pwastd = false;
    private CarDestructionStage _destructionStage;
    private int lcn = 0;
    private bool[] pengs = new bool[7];
    private int _engineSignature;
    private static readonly int DriftConfirmationTicks = Math.Max(1, (int)MathF.Ceiling(Physics.TargetTps * 0.1f));
    private int _driftTicks;

    private CarPhysics _carPhysics;
    private readonly BackendCar _car;
    private readonly Dictionary<(ISoundClip Sample, CrashSoundContact? Contact), ISoundClip> _sounds = [];
    private readonly CrashSoundQueue<MadSfx> _crashSounds;
    private readonly List<(ISoundClip Sample, CrashSoundContact? Contact)> _finishedSounds = [];

    public bool Mute = false;
    public bool PlayPowerUp { get; set; }

    public MadSfx(BackendCar car, CrashSoundQueue<MadSfx> crashSounds)
    {
        _car = car;
        _crashSounds = crashSounds;
        _carPhysics = car.CarPhysics;

        _carPhysics.SfxPlayCrash += SfxPlayCrash;
        _carPhysics.SfxPlayScrape += SfxPlayScrape;
        _carPhysics.SfxPlayGscrape += SfxPlayGscrape;
        _carPhysics.SfxPlaySkid += SfxPlaySkid;
        _carPhysics.PowerUp += SfxPlayPowerup;
    }

    private Vector3 SoundPosition => new((float)_car.Position.X, (float)_car.Position.Y, (float)_car.Position.Z);

    private ISoundClip Sound(
        ISoundClip sample,
        CrashSoundContact? contact = null,
        Vector3? position = null,
        bool followPosition = false
    )
    {
        if (!_sounds.TryGetValue((sample, contact), out var instance))
        {
            instance = sample.CreateSpatialInstance(followPosition);
            _sounds.Add((sample, contact), instance);
        }

        instance.SetPosition(position ?? SoundPosition);
        return instance;
    }

    private ISoundClip ImpactSound(ISoundClip sample, CrashSoundEvent crash) => Sound(sample, crash.Contact, crash.Position);

    private ISoundClip? OptionalSound(ISoundClip? sample) => sample is null ? null : Sound(sample);

    private void SfxPlayPowerup(object? sender, float f)
    {
        if (!Mute && PlayPowerUp) SfxLibrary.powerup?.Play();
    }

    internal void PlayFixSound()
    {
        if (!Mute && SfxLibrary.carfixed is { } sample) Sound(sample, followPosition: true).Play();
    }

    public void Stop()
    {
        for (var gear = 0; gear < 5; gear++)
        {
            if (!pengs[gear]) continue;
            Sound(SfxLibrary.engs[_engineSignature, gear]).Stop();
            pengs[gear] = false;
        }

        if (grrd || aird || stopcnt > 0)
            StopAirs();
        if (pwastd)
            OptionalSound(SfxLibrary.wastd)?.Stop();

        foreach (var sound in _sounds.Values) sound.Stop();

        grrd = false;
        aird = false;
        pwastd = false;
        stopcnt = 0;
        pwait = 7;
        _driftTicks = 0;
    }

    public void Dispose()
    {
        _crashSounds.RemoveCar(_carPhysics.Im);
        Stop();
        _carPhysics.SfxPlayCrash -= SfxPlayCrash;
        _carPhysics.SfxPlayScrape -= SfxPlayScrape;
        _carPhysics.SfxPlayGscrape -= SfxPlayGscrape;
        _carPhysics.SfxPlaySkid -= SfxPlaySkid;
        _carPhysics.PowerUp -= SfxPlayPowerup;
        foreach (var sound in _sounds.Values) sound.Dispose();
        _sounds.Clear();
        _finishedSounds.Clear();
    }

    private void SparkEng(int cgear, int cn, CarStats stats)
    {
        if (!Mute)
        {
            if (_engineSignature != stats.Enginsignature)
                Stop();
            _engineSignature = stats.Enginsignature;

            if (lcn != cn)
            {
                for (int gear = 0; gear < 5; gear++)
                    if (pengs[gear])
                    {
                        Sound(SfxLibrary.engs[stats.Enginsignature, gear]).Stop();
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
                        Sound(SfxLibrary.engs[stats.Enginsignature, gear]).Loop();
                        pengs[gear] = true;
                    }
                }
                else if (pengs[gear])
                {
                    Sound(SfxLibrary.engs[stats.Enginsignature, gear]).Stop();
                    pengs[gear] = false;
                }
        }
    }

    private void StopAirs()
    {
        for (int airs = 0; airs < 6; airs++)
        {
            if (_sounds.TryGetValue((SfxLibrary.air[airs], null), out var sound) && sound.IsActive)
                sound.Stop();
        }
    }

    private bool HasLateralTireSlip()
    {
        var heading = (float)_car.Rotation.Yaw.Degrees;
        for (var wheel = 0; wheel < 4; wheel++)
            if (SkidSoundFilter.HasLateralSlip((float)_carPhysics.Scx[wheel], (float)_carPhysics.Scz[wheel], heading))
                return true;
        return false;
    }

    private bool HasDriftSlip()
    {
        var velocityX = 0f;
        var velocityZ = 0f;
        for (var wheel = 0; wheel < 4; wheel++)
        {
            velocityX += (float)_carPhysics.Scx[wheel] * 0.25f;
            velocityZ += (float)_carPhysics.Scz[wheel] * 0.25f;
        }

        return SkidSoundFilter.HasDriftSlip(velocityX, velocityZ, (float)_car.Rotation.Yaw.Degrees);
    }

    private void StopSkidSounds()
    {
        for (var i = 0; i < 3; i++)
        {
            if (_sounds.TryGetValue((SfxLibrary.skid[i], null), out var road) && road.IsActive) road.Stop();
            if (_sounds.TryGetValue((SfxLibrary.dustskid[i], null), out var dirt) && dirt.IsActive) dirt.Stop();
        }
    }

    private void UpdateDestructionSound(CarDestructionStage stage)
    {
        if (!Mute && stage == CarDestructionStage.WhiteSparks)
        {
            if (!pwastd)
            {
                OptionalSound(SfxLibrary.wastd)?.Loop();
                pwastd = true;
            }
        }
        else if (pwastd)
        {
            OptionalSound(SfxLibrary.wastd)?.Stop();
            pwastd = false;
        }

        if (stage != CarDestructionStage.Fire && _destructionStage == CarDestructionStage.Fire)
            OptionalSound(SfxLibrary.firewasted)?.Stop();
        if (!Mute && stage == CarDestructionStage.Fire && _destructionStage != CarDestructionStage.Fire)
            OptionalSound(SfxLibrary.firewasted)?.Play();
        _destructionStage = stage;
    }

    public void Tick(Control control, CarPhysics carPhysics, CarStats stats, CarDestructionStage destructionStage)
    {
        var position = SoundPosition;
        foreach (var (key, sound) in _sounds)
        {
            if (key.Contact is not null && !sound.IsActive)
            {
                sound.Dispose();
                _finishedSounds.Add(key);
            }
            else if (key.Contact is null)
                sound.SetPosition(position);
        }

        foreach (var key in _finishedSounds) _sounds.Remove(key);
        _finishedSounds.Clear();
        if (!Mute)
        {
            var lateralSlip = carPhysics.Wtouch && !carPhysics.BadLanding && HasLateralTireSlip();
            if (!lateralSlip) StopSkidSounds();
            if (carPhysics.Wtouch && !carPhysics.BadLanding && !carPhysics.Wasted && HasDriftSlip())
                _driftTicks = Math.Min(_driftTicks + 1, DriftConfirmationTicks);
            else _driftTicks = 0;
            var drifting = _driftTicks >= DriftConfirmationTicks;
            if ( /*(fase == 0 || fase == 7001) && starcnt < 35 && !mutes*/true)
            {
                bool moving = !carPhysics.Wasted
                              && ((control.Up && carPhysics.Speed > (fix64)0.0F)
                                  || (control.Down && carPhysics.Speed < (fix64)10.0F));
                bool revbraking = false;
                if (!carPhysics.Wasted && control.Up && carPhysics.Speed < (fix64)10.0F)
                {
                    moving = true;
                    revbraking = true;
                }

                if (moving && carPhysics.Wtouch)
                {
                    if (!revbraking && !drifting)
                    {
                        StopAirs();
                        stopcnt = 0;
                    }

                    if (!carPhysics.BadLanding)
                    {
                        if (!revbraking && !drifting)
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
                                    int i260 = (int)((fix64)3.0F
                                                     * (fix64.Abs(carPhysics.Speed) - stats.Swits[0])
                                                     / (stats.Swits[1] - stats.Swits[0]));
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
                                    int i261 = (int)((fix64)3.0F
                                                     * (fix64.Abs(carPhysics.Speed) - stats.Swits[1])
                                                     / (stats.Swits[2] - stats.Swits[1]));
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
                                    Sound(SfxLibrary.air[5]).Loop();
                                    stopcnt = 10;
                                }
                            }
                            else if (stopcnt <= -2)
                            {
                                Sound(SfxLibrary.air[2 + (int)(Random.Shared.NextDouble() * 3.0F)]).Loop();
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
                else if (carPhysics.Wasted)
                {
                    SparkEng(-2, carPhysics.Cn, stats);
                    StopAirs();
                    grrd = false;
                    aird = false;
                    stopcnt = 0;
                    pwait = 7;
                }
                else
                {
                    pwait = 15;
                    if (carPhysics.Wtouch)
                    {
                        StopAirs();
                        stopcnt = 0;
                        grrd = false;
                        aird = false;
                    }

                    if (!carPhysics.Wtouch && !grrd && Random.Shared.NextDouble() > 0.4)
                    {
                        Sound(SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)]).Loop();
                        stopcnt = 5;
                        grrd = true;
                    }

                    if (!carPhysics.Wtouch && !aird)
                    {
                        StopAirs();
                        Sound(SfxLibrary.air[(int)(Random.Shared.NextDouble() * 4.0F)]).Loop();
                        stopcnt = 10;
                        aird = true;
                    }

                    SparkEng(-1, carPhysics.Cn, stats);
                }
            }
            else
            {
                SparkEng(-2, carPhysics.Cn, stats);
                if (pwastd)
                {
                    OptionalSound(SfxLibrary.wastd)?.Stop();
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
        }

        UpdateDestructionSound(destructionStage);
    }

    private void SfxPlayScrape(object? sender, (int i, int i266, int i267) position)
    {
        if (!Mute
            && bfscrape == 0
            && Math.Sqrt(position.i * position.i + position.i266 * position.i266 + position.i267 * position.i267) / 10.0 > 10.0)
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

            Sound(SfxLibrary.scrape[scrapes]).Play();
            bfscrape = (int)(5 * (1 / Physics.PHYSICS_MULTIPLIER));
        }
    }

    private void SfxPlayGscrape(object? sender, (int i, int i269, int i270) position)
    {
        if (!Mute
            && (bfsc1 == 0 || bfsc2 == 0)
            && Math.Sqrt(position.i * position.i + position.i269 * position.i269 + position.i270 * position.i270) / 10.0 > 15.0)
            if (bfsc1 == 0)
            {
                Sound(SfxLibrary.scrape[2]).Stop();
                Sound(SfxLibrary.scrape[2]).Play();
                bfsc1 = (int)(12 * (1 / Physics.PHYSICS_MULTIPLIER));
                bfsc2 = (int)(6 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
            else
            {
                Sound(SfxLibrary.scrape[3]).Stop();
                Sound(SfxLibrary.scrape[3]).Play();
                bfsc2 = (int)(12 * (1 / Physics.PHYSICS_MULTIPLIER));
                bfsc1 = (int)(6 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
    }

    private void SfxPlayCrash(object? sender, CrashSoundEvent crash)
    {
        if (!Mute && (crash.Kind == 1 || MathF.Abs(crash.Strength / Physics.PHYSICS_MULTIPLIER) > 25))
            _crashSounds.Enqueue(this, crash);
    }

    internal void PlayCrashSound(CrashSoundEvent crash)
    {
        var crashData = (f: crash.Strength / Physics.PHYSICS_MULTIPLIER, i: crash.Kind);
        if (!Mute)
        {
            if (crashData.i == 0)
            {
                if (Math.Abs(crashData.f) > 25.0F && Math.Abs(crashData.f) < 170.0F)
                {
                    ImpactSound(SfxLibrary.lowcrash[crshturn], crash).Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }

                if (Math.Abs(crashData.f) >= 170.0F)
                {
                    ImpactSound(SfxLibrary.crash[crshturn], crash).Play();
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
                    ImpactSound(SfxLibrary.lowcrash[2], crash).Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }

                if (Math.Abs(crashData.f) >= 170.0F)
                {
                    ImpactSound(SfxLibrary.crash[2], crash).Play();
                    bfcrash = (int)(2 * (1 / Physics.PHYSICS_MULTIPLIER));
                }
            }

            if (crashData.i == 1)
            {
                if (SfxLibrary.tires is { } tires) ImpactSound(tires, crash).Play();
                bfcrash = (int)(3 * (1 / Physics.PHYSICS_MULTIPLIER));
            }
        }
    }

    private void SfxPlaySkid(object? sender, (SurfaceType surfaceType, float skidIntensity) skidData)
    {
        if (!Mute && bfcrash == 0 && bfskid == 0 && skidData.skidIntensity > 150.0F)
        {
            if (skidData.surfaceType == SurfaceType.Road)
            {
                Sound(SfxLibrary.skid[skflg], followPosition: true).Play();
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
                Sound(SfxLibrary.dustskid[dskflg], followPosition: true).Play();
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
