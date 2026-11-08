namespace NFMWorldLibrary;

public readonly record struct CrashSoundContact(int FirstCar, int FirstPoint, int SecondCar, int SecondPoint, int Surface)
{
    public static CrashSoundContact Cars(int car, int point, int otherCar, int otherPoint)
        => car < otherCar ? new(car, point, otherCar, otherPoint, -1) : new(otherCar, otherPoint, car, point, -1);

    public static CrashSoundContact Obstacle(int car, int point, int surface) => new(car, point, -1, -1, surface);

    public bool Involves(int car) => FirstCar == car || SecondCar == car;
}

public readonly record struct CrashSoundEvent(CrashSoundContact Contact, Vector3 Position, float Strength, int Kind);

public sealed class CrashSoundQueue<TSource> where TSource : class
{
    private readonly record struct Pending(TSource Source, CrashSoundEvent Sound);

    private readonly record struct Delayed(Pending Pending, long Tick);

    private static readonly int CrashSpacingTicks = Math.Max(1, (int)MathF.Ceiling(Physics.TargetTps * 0.1f));
    private readonly Dictionary<CrashSoundContact, Pending> _pending = [];
    private readonly List<Delayed> _delayed = [];
    private readonly Dictionary<int, long> _crashAvailableAt = [];
    private readonly List<int> _expiredCars = [];
    private readonly Dictionary<CrashSoundContact, long> _cooldowns = [];
    private readonly List<CrashSoundContact> _expired = [];
    private readonly Dictionary<TSource, Pending> _landings = [];
    private readonly Dictionary<TSource, (long Until, CrashSoundContact Contact)> _landingCooldowns = [];
    private readonly List<TSource> _expiredLandings = [];
    private long _tick;

    public void Enqueue(TSource source, CrashSoundEvent sound)
    {
        if (!float.IsFinite(sound.Strength)) return;
        if (_pending.TryGetValue(sound.Contact, out var previous))
        {
            if (sound.Kind == 1 && previous.Sound.Kind != 1) return;
            if ((sound.Kind == 1) == (previous.Sound.Kind == 1)
                && MathF.Abs(sound.Strength) <= MathF.Abs(previous.Sound.Strength)) return;
        }

        _pending[sound.Contact] = new Pending(source, sound);
    }

    public void Flush(Action<TSource, CrashSoundEvent> play)
    {
        _tick++;
        foreach (var (contact, until) in _cooldowns)
            if (until <= _tick)
                _expired.Add(contact);
        foreach (var contact in _expired) _cooldowns.Remove(contact);
        _expired.Clear();
        foreach (var (source, cooldown) in _landingCooldowns)
            if (cooldown.Until <= _tick)
                _expiredLandings.Add(source);
        foreach (var source in _expiredLandings) _landingCooldowns.Remove(source);
        _expiredLandings.Clear();
        foreach (var (car, tick) in _crashAvailableAt)
            if (tick <= _tick)
                _expiredCars.Add(car);
        foreach (var car in _expiredCars) _crashAvailableAt.Remove(car);
        _expiredCars.Clear();

        for (var i = 0; i < _delayed.Count;)
        {
            var delayed = _delayed[i];
            if (delayed.Tick > _tick)
            {
                i++;
                continue;
            }

            play(delayed.Pending.Source, delayed.Pending.Sound);
            _delayed.RemoveAt(i);
        }

        foreach (var (contact, pending) in _pending)
        {
            var cooldownUntil = _tick + Math.Max(1, (int)(2 / Physics.PHYSICS_MULTIPLIER));
            if (_cooldowns.TryGetValue(contact, out var until))
            {
                if (pending.Sound.Kind != 1) _cooldowns[contact] = Math.Max(until, cooldownUntil);
                continue;
            }

            if (pending.Sound.Kind == 1)
            {
                if (!_landings.TryGetValue(pending.Source, out var previous)
                    || MathF.Abs(pending.Sound.Strength) > MathF.Abs(previous.Sound.Strength))
                    _landings[pending.Source] = pending;
                continue;
            }

            var queuedIndex = -1;
            for (var i = 0; i < _delayed.Count; i++)
            {
                var queuedContact = _delayed[i].Pending.Sound.Contact;
                if (!queuedContact.Involves(contact.FirstCar)
                    && (contact.SecondCar < 0 || !queuedContact.Involves(contact.SecondCar))) continue;
                queuedIndex = i;
                break;
            }

            if (queuedIndex >= 0)
            {
                var queued = _delayed[queuedIndex];
                var queuedContact = queued.Pending.Sound.Contact;
                if (queuedContact.FirstCar == contact.FirstCar
                    && queuedContact.SecondCar == contact.SecondCar
                    && MathF.Abs(pending.Sound.Strength) > MathF.Abs(queued.Pending.Sound.Strength))
                    _delayed[queuedIndex] = queued with { Pending = pending };
                _cooldowns[contact] = Math.Max(cooldownUntil, queued.Tick + 1);
                continue;
            }

            var playAt = Math.Max(_tick, _crashAvailableAt.GetValueOrDefault(contact.FirstCar));
            if (contact.SecondCar >= 0)
                playAt = Math.Max(playAt, _crashAvailableAt.GetValueOrDefault(contact.SecondCar));
            _crashAvailableAt[contact.FirstCar] = playAt + CrashSpacingTicks;
            if (contact.SecondCar >= 0) _crashAvailableAt[contact.SecondCar] = playAt + CrashSpacingTicks;
            if (playAt == _tick) play(pending.Source, pending.Sound);
            else _delayed.Add(new Delayed(pending, playAt));
            _cooldowns[contact] = playAt + Math.Max(1, (int)(2 / Physics.PHYSICS_MULTIPLIER));
        }

        foreach (var (source, pending) in _landings)
        {
            if (_landingCooldowns.ContainsKey(source)) continue;
            play(source, pending.Sound);
            _landingCooldowns[source] = (_tick + Math.Max(1, (int)(3 / Physics.PHYSICS_MULTIPLIER)), pending.Sound.Contact);
        }

        _landings.Clear();
        _pending.Clear();
    }

    public void RemoveCar(int car)
    {
        _delayed.RemoveAll(delayed => delayed.Pending.Sound.Contact.Involves(car));
        _crashAvailableAt.Remove(car);
        foreach (var contact in _pending.Keys.Where(contact => contact.Involves(car)).ToArray())
            _pending.Remove(contact);
        foreach (var contact in _cooldowns.Keys.Where(contact => contact.Involves(car)).ToArray())
            _cooldowns.Remove(contact);
        foreach (var source in _landingCooldowns.Where(entry => entry.Value.Contact.Involves(car)).Select(entry => entry.Key)
                     .ToArray())
            _landingCooldowns.Remove(source);
    }

    public void Clear()
    {
        _pending.Clear();
        _delayed.Clear();
        _crashAvailableAt.Clear();
        _expiredCars.Clear();
        _cooldowns.Clear();
        _expired.Clear();
        _landings.Clear();
        _landingCooldowns.Clear();
        _expiredLandings.Clear();
        _tick = 0;
    }
}
