using NFMWorldLibrary.Multiplayer;

namespace NFMWorldLibrary.Backend;

public partial class BackendStage
{
    private readonly Dictionary<int, StageObject> _destructibles = [];
    private readonly Dictionary<int, f64Vector3> _breakVelocities = [];
    public event Action<int, StageImpact>? ObjectHit;
    public event Action<StageObject, f64Vector3, bool>? ObjectBroken;
    public event Action<StageObject, bool>? ObjectDamaged;
    public event Action? DestructionReset;

    private void InitializeDestructibles()
    {
        for (var i = 0; i < Pieces.Count; i++)

            if (Pieces[i] is StageObject { Durability: > 0 } obj)
            {
                obj.DestructionId = i;
                _destructibles.Add(i, obj);
            }
    }

    public StageObject? GetDestructible(int id) => _destructibles.GetValueOrDefault(id);

    public static int ImpactDamage(f64Vector3 impulse)
    {
        var speed = (double)impulse.LengthNoOverflow();

        return (int)Math.Clamp((speed - 20) * 2, 0, 100_000);
    }

    public void ReportObjectHit(int carIndex, StageImpact impact)
    {
        if (ObjectHit is { } report)
            report(carIndex, impact);
        else
            DamageObject(impact);
    }

    public StagePieceState? DamageObject(StageImpact impact)
    {
        if (GetDestructible(impact.PieceId) is not { IsDestroyed: false } obj)

            return null;
        var damage = ImpactDamage(impact.Impulse);

        if (damage == 0)

            return null;
        var totalDamage = (int)Math.Min((long)obj.Damage + damage, obj.Durability);
        var direction = impact.Impulse / impact.Impulse.LengthNoOverflow();
        var state = new StagePieceState
        {
            PieceId = impact.PieceId,
            Damage = totalDamage,
            Velocity = impact.Velocity,
            Deformation = obj.Deformation + direction * (totalDamage - obj.Damage)
        };
        ApplyPieceState(state);

        return state;
    }

    public void ApplyPieceState(StagePieceState state, bool shatter = true)
    {
        if (GetDestructible(state.PieceId) is not { IsDestroyed: false } obj)

            return;
        if (state.Damage <= obj.Damage)

            return;
        obj.Damage = Math.Clamp(state.Damage, 0, obj.Durability);
        obj.Deformation = state.Deformation;

        if (!obj.IsDestroyed)
        {
            ObjectDamaged?.Invoke(obj, shatter);

            return;
        }

        _breakVelocities[state.PieceId] = state.Velocity;
        Pieces.Remove(obj);
        ObjectBroken?.Invoke(obj, state.Velocity, shatter);
    }

    public IEnumerable<StagePieceState> CaptureDestruction()
    {
        foreach (var (id, obj) in _destructibles)

            if (obj.Damage > 0)

                yield return new StagePieceState
                {
                    PieceId = id, Damage = obj.Damage, Velocity = _breakVelocities.GetValueOrDefault(id),
                    Deformation = obj.Deformation
                };
    }

    public void ResetDestruction()
    {
        foreach (var (id, obj) in _destructibles.OrderBy(entry => entry.Key))
        {
            if (obj.IsDestroyed)
                Pieces.Insert(Math.Min(id, Pieces.Count), obj);
            obj.Damage = 0;
            obj.Deformation = default;
        }

        _breakVelocities.Clear();
        DestructionReset?.Invoke();
    }
}
