using NFMWorldLibrary;
using NFMWorldLibrary.Backend;
using NFMWorld.DriverInterface.DriverInterface;
using NFMWorld.Sfx;

namespace NFMWorld;

public partial class ClientStageRenderer
{
    private readonly Queue<(StageObject Object, f64Vector3 Velocity, bool Shatter)> _pendingBreaks = new();
    private readonly Queue<(StageObject Object, int Damage, bool PlaySound)> _pendingDamage = new();
    private readonly List<StageDebris> _debris = [];
    private readonly List<ISoundClip> _objectSounds = [];
    private bool _restoreDestructibles;

    private void InitializeDestructionRendering()
    {
        backendStage.ObjectBroken += OnObjectBroken;
        backendStage.ObjectDamaged += OnObjectDamaged;
        backendStage.DestructionReset += OnDestructionReset;
    }

    private void OnObjectBroken(StageObject obj, f64Vector3 velocity, bool shatter)
        => _pendingBreaks.Enqueue((obj, velocity, shatter));

    private void OnObjectDamaged(StageObject obj, bool playSound)
        => _pendingDamage.Enqueue((obj, obj.Damage, playSound));

    private void OnDestructionReset()
    {
        _pendingBreaks.Clear();
        _pendingDamage.Clear();
        _restoreDestructibles = true;
    }

    public override void GameTick(BackendStage? stage = null)
    {
        TickGroundPulse();

        if (_restoreDestructibles)
        {
            _restoreDestructibles = false;
            foreach (var debris in _debris)
                debris.Dispose();
            _debris.Clear();
            ClearObjectSounds();
            DetectChanges();
        }

        while (_pendingDamage.TryDequeue(out var damaged))
            if (damaged.PlaySound)
                PlayStageSound(SfxLibrary.lowcrash[(int)(((long)damaged.Object.DestructionId + damaged.Damage)
                                                         % SfxLibrary.lowcrash.Length)], damaged.Object
                );

        while (_pendingBreaks.TryDequeue(out var broken))
        {
            if (_cachedObjects.Remove(broken.Object, out var visual))
            {
                _mutableChildren.Remove(visual);
                visual.Dispose();
            }

            if (!broken.Shatter)

                continue;
            PlayStageSound(SfxLibrary.objectBreak[broken.Object.DestructionId % SfxLibrary.objectBreak.Length], broken.Object);

            if (_debris.Count >= 8)
            {
                _debris[0].Dispose();
                _debris.RemoveAt(0);
            }

            _debris.Add(new StageDebris(broken.Object, broken.Velocity) { Parent = this });
        }

        for (var i = _debris.Count - 1; i >= 0; i--)
        {
            _debris[i].GameTick(stage);

            if (!_debris[i].Expired)

                continue;
            _debris[i].Dispose();
            _debris.RemoveAt(i);
        }

        base.GameTick(stage);

        for (var i = _objectSounds.Count - 1; i >= 0; i--)
        {
            if (_objectSounds[i].IsActive)

                continue;
            _objectSounds[i].Dispose();
            _objectSounds.RemoveAt(i);
        }
    }

    public override void OnBeforeRender(float alpha)
    {
        base.OnBeforeRender(alpha);
        foreach (var debris in _debris)
            debris.OnBeforeRender(alpha);
    }

    private void DisposeDestructionRendering()
    {
        backendStage.ObjectBroken -= OnObjectBroken;
        backendStage.ObjectDamaged -= OnObjectDamaged;
        backendStage.DestructionReset -= OnDestructionReset;
        _pendingBreaks.Clear();
        _pendingDamage.Clear();
        foreach (var debris in _debris)
            debris.Dispose();
        _debris.Clear();
        ClearObjectSounds();
    }

    internal void PlayStageSound(ISoundClip? sample, StageObject obj)
    {
        if (sample is null)

            return;
        var sound = sample.CreateSpatialInstance();
        _objectSounds.Add(sound);
        sound.SetPosition((Vector3)obj.Position);
        sound.Play();
    }

    private void ClearObjectSounds()
    {
        foreach (var sound in _objectSounds)
            sound.Dispose();
        _objectSounds.Clear();
    }
}
