using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorld.Shaders;
using NFMWorldLibrary;

namespace NFMWorld;

public class Lighting
{
    public IReadOnlyList<Camera> LightCameras;
    public IReadOnlyList<IRenderTarget?> ShadowMaps;

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
        IReadOnlyList<IRenderTarget?> shadowMaps,
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
        IRenderTarget?[] shadowMaps,
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

        int TextureSlotOf(string name)
        {
            foreach (var texture in reflection.Textures)
            {
                if (texture.Name == name) return texture.Slot;
            }
            return -1;
        }

        void SetShadowMapTexture(string name, IRenderTarget? shadowMap)
        {
            var slot = TextureSlotOf(name);
            if (slot < 0 || shadowMap is null) return;
            cb.SetShaderResource(slot, shadowMap.ColorTexture, Effects.ShadowMapSampler);
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

        // SetUniform copies raw bytes straight into the Effect parameter's value storage with no
        // type conversion, so the span's element type has to match the shader's declared type -
        // a float parameter written from an int lands a denormal (3 -> 4.2e-45) and reads as 0.
        void SetFloat(string name, float value)
        {
            var slot = SlotOf(name);
            if (slot < 0) return;
            Span<float> v = [value];
            cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
        }

        void SetFloat3(string name, Vector3 value)
        {
            var slot = SlotOf(name);
            if (slot < 0) return;
            Span<float> v = [value.X, value.Y, value.Z];
            cb.SetUniform(slot, MemoryMarshal.AsBytes(v));
        }

        // LightViewProj is the *sampling* half of shadow mapping: the pixel shader uses it to find the
        // texel a world point occupies in the cascade, while the cascade itself was written using the
        // light camera's View and Projection *separately* (Submesh.Render's shadow branch). That split
        // is what makes this the right place to correct the V convention, and the only place - the
        // correction cannot disturb the pass that produced the map.
        //
        // The correction is one sign, and it is needed because the shaders are shared across backends
        // whose framebuffer origins differ. applyShadowingSingle does
        // `shadowTexCoord.y = 1.0f - shadowTexCoord.y`, which is right for a top-left origin (D3D,
        // Metal, and FNA - whose MojoShader flipped Y in the vertex shader so that its GL targets came
        // out top-down too) and mirrored for a bottom-left one (our GL backends, which have no
        // equivalent flip in the emitted GLSL). Negating the second *column* negates
        // lightingPosition.y, so the shader's own `1 - y` then lands where it should: the OpenGL smoke
        // replays this exact lookup and asserts the depth it finds, so a regression here fails a test
        // rather than quietly shifting every shadow in the world by a mirror.
        var flipShadowSampleV = GameSparker.NewGraphicsDevice.HasBottomLeftFramebufferOrigin;

        Matrix ShadowSampleMatrix(Matrix lightViewProjection)
        {
            if (!flipShadowSampleV)
                return lightViewProjection;

            // Column 1, not row 1: the shaders use row-vector math - mul(worldPos, lightViewProj) - so
            // the y output is the second column. This is the same convention SetUniform's matrix
            // transpose rests on, seen from the other side.
            lightViewProjection.M12 = -lightViewProjection.M12;
            lightViewProjection.M22 = -lightViewProjection.M22;
            lightViewProjection.M32 = -lightViewProjection.M32;
            lightViewProjection.M42 = -lightViewProjection.M42;
            return lightViewProjection;
        }

        if (LightCameras.Count > 0)
        {
            SetMatrix("LightViewProj0", ShadowSampleMatrix(LightCameras[0].ViewProjectionMatrix));
        }

        if (LightCameras.Count > 1)
        {
            SetMatrix("LightViewProj1", ShadowSampleMatrix(LightCameras[1].ViewProjectionMatrix));
        }

        if (LightCameras.Count > 2)
        {
            SetMatrix("LightViewProj2", ShadowSampleMatrix(LightCameras[2].ViewProjectionMatrix));
        }

        // NumCascades gates shadow-map *sampling* in Mad.fxh's PS_IsShadowed. It must only be
        // non-zero when the cascade textures are actually bound - a shader sampling an unbound
        // sampler gets D3D11's zero-fill, i.e. "the shadow map is empty, everything is in shadow",
        // halving the brightness of anything inside the light frustum (PS_ApplyShadowing) and
        // flattening the whole scene. It is declared `float` in Mad.fxh; see SetFloat.
        var usableShadowMaps = 0;
        for (var i = 0; i < ShadowMaps.Count && i < TotalCascades; i++)
        {
            if (ShadowMaps[i] is not null)
            {
                usableShadowMaps++;
            }
        }

        SetFloat("NumCascades", usableShadowMaps);

        if (ShadowMaps.Count > 0) SetShadowMapTexture("ShadowMap0", ShadowMaps[0]);
        if (ShadowMaps.Count > 1) SetShadowMapTexture("ShadowMap1", ShadowMaps[1]);
        if (ShadowMaps.Count > 2) SetShadowMapTexture("ShadowMap2", ShadowMaps[2]);

        SetFloat3("LightDirection", World.LightDirection);
    }
}