using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class LineEffect(Effect effect)
{
    public EffectParameter? WorldViewProj { get; } = effect.Parameters["WorldViewProj"];
    public EffectParameter? ScreenParams { get; } = effect.Parameters["ScreenParams"];
    public EffectParameter? Thickness { get; } = effect.Parameters["Thickness"];
    public EffectParameter? MiterThreshold { get; } = effect.Parameters["MiterThreshold"];
    public EffectParameter? IsFullbright { get; } = effect.Parameters["IsFullbright"];
    public EffectParameter? UseBaseColor { get; } = effect.Parameters["UseBaseColor"];
    public EffectParameter? BaseColor { get; } = effect.Parameters["BaseColor"];
    public EffectParameter? SnapColor { get; } = effect.Parameters["SnapColor"];

    /// <inheritdoc cref="Effect.Parameters"/>
    public EffectParameterCollection Parameters => effect.Parameters;

    /// <inheritdoc cref="Effect.Techniques"/>
    public EffectTechniqueCollection Techniques => effect.Techniques;

    /// <inheritdoc cref="Effect.CurrentTechnique"/>
    public EffectTechnique CurrentTechnique { get => effect.CurrentTechnique; set => effect.CurrentTechnique = value; }
}