using GraphicsDevice = nfm_world.compat.GraphicsDeviceCompat;
using nfm_world.compat;
using MoonWorks.Graphics;
using nfm_world.camera;
using nfm_world.shaders;

namespace nfm_world;

public class Lighting
{
    public Camera[] LightCameras;
    public RenderTarget2D[] ShadowMaps;
    public bool IsCreateShadowMap;
    public int NumCascade;

    public Lighting(Camera[] lightCameras, RenderTarget2D[] shadowMaps, bool isCreateShadowMap = false, int numCascade = -1)
    {
        LightCameras = lightCameras;
        ShadowMaps = shadowMaps;
        IsCreateShadowMap = isCreateShadowMap;
        NumCascade = numCascade;
        if (numCascade != -1)
        {
            CascadeLightCamera = LightCameras[numCascade];
        }
    }

    public Camera? CascadeLightCamera;

    /// <summary>
    /// Creates a ShadowParams struct matching the HLSL ShadowParams cbuffer.
    /// </summary>
    public ShadowParams ToShadowParams(float depthBias = 0.00005f)
    {
        var sp = new ShadowParams { DepthBias = depthBias };
        if (LightCameras.Length > 0) sp.LightViewProj0 = LightCameras[0].ViewProjectionMatrix;
        if (LightCameras.Length > 1) sp.LightViewProj1 = LightCameras[1].ViewProjectionMatrix;
        if (LightCameras.Length > 2) sp.LightViewProj2 = LightCameras[2].ViewProjectionMatrix;
        return sp;
    }

    /// <summary>
    /// Binds shadow map textures to fragment sampler slots 0-2.
    /// </summary>
    public void BindShadowMaps(RenderPass pass)
    {
        if (ShadowMaps.Length >= 3)
        {
            pass.BindFragmentSamplers(0,
                new TextureSamplerBinding(ShadowMaps[0].Texture, Pipelines.ShadowSampler),
                new TextureSamplerBinding(ShadowMaps[1].Texture, Pipelines.ShadowSampler),
                new TextureSamplerBinding(ShadowMaps[2].Texture, Pipelines.ShadowSampler));
        }
    }

    /// <summary>Sets shadow map parameters on FNA Effect (compat path for non-ported renderers).</summary>
    public void SetShadowMapParameters(Effect effect)
    {
        if (LightCameras.Length > 0)
            effect.Parameters["LightViewProj0"]?.SetValue(LightCameras[0].ViewProjectionMatrix);
        if (LightCameras.Length > 1)
            effect.Parameters["LightViewProj1"]?.SetValue(LightCameras[1].ViewProjectionMatrix);
        if (LightCameras.Length > 2)
            effect.Parameters["LightViewProj2"]?.SetValue(LightCameras[2].ViewProjectionMatrix);

        if (!IsCreateShadowMap)
        {
            if (ShadowMaps.Length > 0)
                effect.Parameters["ShadowMap0"]?.SetValue(ShadowMaps[0]);
            if (ShadowMaps.Length > 1)
                effect.Parameters["ShadowMap1"]?.SetValue(ShadowMaps[1]);
            if (ShadowMaps.Length > 2)
                effect.Parameters["ShadowMap2"]?.SetValue(ShadowMaps[2]);
        }
    }
}