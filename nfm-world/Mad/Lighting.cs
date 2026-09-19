using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorldLibrary;

namespace NFMWorld;

public class Lighting
{
    public IReadOnlyList<Camera> LightCameras;
    public IReadOnlyList<RenderTarget2D?> ShadowMaps;

    /// <summary>
    /// Describes the current render pass (shadow cascade or main colour pass).
    /// Replaces the boolean <see cref="IsCreateShadowMap"/> plus cascade-index pattern.
    /// </summary>
    public RenderPass RenderPass { get; }

    [MemberNotNullWhen(true, nameof(CascadeLightCamera))]
    public bool IsCreateShadowMap => RenderPass.IsShadow;

    public int NumCascade => RenderPass.CascadeIndex;

    public int TotalCascades => RenderPass.TotalCascades;

    /// <summary>
    /// New-style constructor using <see cref="RenderPass"/>.
    /// </summary>
    public Lighting(
        IReadOnlyList<Camera> lightCameras,
        IReadOnlyList<RenderTarget2D?> shadowMaps,
        RenderPass renderPass
    )
    {
        LightCameras = lightCameras;
        ShadowMaps = shadowMaps;
        RenderPass = renderPass;

        if (renderPass.IsShadow && renderPass.CascadeIndex >= 0)
        {
            CascadeLightCamera = LightCameras[renderPass.CascadeIndex];
        }
    }

    /// <summary>
    /// Legacy constructor. Prefer the <see cref="RenderPass"/>-based overload.
    /// </summary>
    public Lighting(
        IReadOnlyList<Camera> lightCameras,
        RenderTarget2D?[] shadowMaps,
        bool isCreateShadowMap = false,
        int numCascade = -1,
        int totalCascades = 3
    )
        : this(
            lightCameras,
            shadowMaps,
            isCreateShadowMap
                ? NFMWorld.RenderPass.Shadow(numCascade, totalCascades)
                : NFMWorld.RenderPass.Main(totalCascades))
    {
    }

    public Camera? CascadeLightCamera;

    /// <summary>
    /// Sets the shadow-related uniforms shared by every shader that samples the cascade shadow
    /// maps, resolving each by name against <paramref name="reflection"/> the same way the
    /// generated <c>*EffectParameters</c> types do (see <c>Shaders/Parameters.cs</c>'s doc comment).
    /// </summary>
    /// <remarks>
    /// TODO(Milestone 5 Stage B follow-up): the ShadowMap0/1/2 texture bindings are not set here -
    /// <c>NFMWorld.Graphics.FNA3D.MojoShaderEffectReflection</c> doesn't populate
    /// <c>ShaderReflection.Textures</c>/<c>Samplers</c> yet (always empty lists), so there's no
    /// slot to bind them against, and <see cref="WorldGame.RebuildCascades"/> (which would create
    /// the actual shadow render targets) is itself still stubbed from Milestone 5 Stage A. Only the
    /// scalar/matrix uniforms below are wired up.
    /// </remarks>
    public void SetShadowMapParameters(ICommandBuffer cb, ShaderReflection reflection)
    {
        int SlotOf(string name)
        {
            foreach (var uniform in reflection.Uniforms)
            {
                if (uniform.Name == name) return uniform.Offset;
            }
            return -1;
        }

        void SetMatrix(string name, Matrix m)
        {
            var slot = SlotOf(name);
            if (slot < 0) return;
            Span<float> v =
            [
                m.M11, m.M12, m.M13, m.M14,
                m.M21, m.M22, m.M23, m.M24,
                m.M31, m.M32, m.M33, m.M34,
                m.M41, m.M42, m.M43, m.M44,
            ];
            cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
        }

        void SetInt(string name, int value)
        {
            var slot = SlotOf(name);
            if (slot < 0) return;
            Span<int> v = [value];
            cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
        }

        void SetFloat3(string name, Vector3 value)
        {
            var slot = SlotOf(name);
            if (slot < 0) return;
            Span<float> v = [value.X, value.Y, value.Z];
            cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
        }

        if (LightCameras.Count > 0)
        {
            SetMatrix("LightViewProj0", LightCameras[0].ViewProjectionMatrix);
        }

        if (LightCameras.Count > 1)
        {
            SetMatrix("LightViewProj1", LightCameras[1].ViewProjectionMatrix);
        }

        if (LightCameras.Count > 2)
        {
            SetMatrix("LightViewProj2", LightCameras[2].ViewProjectionMatrix);
        }

        // NumCascades gates shadow-map *sampling* in Mad.fxh's PS_IsShadowed. It must only be
        // non-zero when the cascade textures are actually bound: this backend can't bind textures
        // yet (see this method's remarks) and ShadowMaps is empty today, so leaving it at the
        // pass's cascade count made every shader sample an unbound sampler - which D3D11 reads as
        // 0, i.e. "the shadow map is empty, everything is in shadow" - halving the brightness of
        // anything inside the light frustum (PS_ApplyShadowing) and flattening the whole scene.
        var usableShadowMaps = 0;
        for (var i = 0; i < ShadowMaps.Count && i < TotalCascades; i++)
        {
            if (ShadowMaps[i] is not null)
            {
                usableShadowMaps++;
            }
        }

        SetInt("NumCascades", usableShadowMaps);

        SetFloat3("LightDirection", World.LightDirection);
    }
}