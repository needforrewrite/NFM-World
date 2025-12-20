using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class PolyEffect(Effect effect)
{
    public Effect UnderlyingEffect => effect;
    public EffectParameter? World { get; } = effect.Parameters["World"];
    public EffectParameter? WorldInverseTranspose { get; } = effect.Parameters["WorldInverseTranspose"];
    public EffectParameter? View { get; } = effect.Parameters["View"];
    public EffectParameter? Projection { get; } = effect.Parameters["Projection"];
    public EffectParameter? WorldView { get; } = effect.Parameters["WorldView"];
    public EffectParameter? WorldViewProj { get; } = effect.Parameters["WorldViewProj"];
    public EffectParameter? CameraPosition { get; } = effect.Parameters["CameraPosition"];
    public EffectParameter? SnapColor { get; } = effect.Parameters["SnapColor"];
    public EffectParameter? IsFullbright { get; } = effect.Parameters["IsFullbright"];
    public EffectParameter? UseBaseColor { get; } = effect.Parameters["UseBaseColor"];
    public EffectParameter? BaseColor { get; } = effect.Parameters["BaseColor"];
    public EffectParameter? LightDirection { get; } = effect.Parameters["LightDirection"];
    public EffectParameter? FogColor { get; } = effect.Parameters["FogColor"];
    public EffectParameter? FogDistance { get; } = effect.Parameters["FogDistance"];
    public EffectParameter? FogDensity { get; } = effect.Parameters["FogDensity"];
    public EffectParameter? EnvironmentLight { get; } = effect.Parameters["EnvironmentLight"];
    public EffectParameter? DepthBias { get; } = effect.Parameters["DepthBias"];

    public EffectParameter? LightViewProj { get; } = effect.Parameters["LightViewProj"];
    public EffectParameter? ShadowMap { get; } = effect.Parameters["ShadowMap"];
    public EffectParameter? GetsShadowed { get; } = effect.Parameters["GetsShadowed"];
    public EffectParameter? Alpha { get; } = effect.Parameters["Alpha"];
    public EffectParameter? Expand { get; } = effect.Parameters["Expand"];
    public EffectParameter? RandomFloat { get; } = effect.Parameters["RandomFloat"];
    public EffectParameter? Darken { get; } = effect.Parameters["Darken"];
    public EffectParameter? Glow { get; } = effect.Parameters["Glow"];
    
    public EffectParameter? ChargedBlinkAmount { get; } = effect.Parameters["ChargedBlinkAmount"];

    /// <inheritdoc cref="Effect.Parameters"/>
    public EffectParameterCollection Parameters => effect.Parameters;

    /// <inheritdoc cref="Effect.Techniques"/>
    public EffectTechniqueCollection Techniques => effect.Techniques;

    /// <inheritdoc cref="Effect.CurrentTechnique"/>
    public EffectTechnique CurrentTechnique { get => effect.CurrentTechnique; set => effect.CurrentTechnique = value; }

}