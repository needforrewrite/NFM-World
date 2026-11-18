using TightPack;

namespace NFMWorldLibrary.Multiplayer;

[TightPackable, TightPackAllowSerialize]
public partial struct StageImpact
{
    [TightPackOrder(0)] public int PieceId;
    [TightPackOrder(1)] public f64Vector3 Impulse;
    [TightPackOrder(2)] public f64Vector3 Velocity;
}

[TightPackable, TightPackAllowSerialize]
public partial struct StagePieceState
{
    [TightPackOrder(0)] public int PieceId;
    [TightPackOrder(1)] public int Damage;
    [TightPackOrder(2)] public f64Vector3 Velocity;
    [TightPackOrder(3)] public f64Vector3 Deformation;
}
