using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

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

    public void SetShadowMapParameters(Effect effect)
    {
        if (LightCameras.Length > 0)
        {
            effect.Parameters["LightViewProj0"]?.SetValue(LightCameras[0].ViewProjectionMatrix);
        }

        if (LightCameras.Length > 1)
        {
            effect.Parameters["LightViewProj1"]?.SetValue(LightCameras[1].ViewProjectionMatrix);
        }

        if (LightCameras.Length > 2)
        {
            effect.Parameters["LightViewProj2"]?.SetValue(LightCameras[2].ViewProjectionMatrix);
        }

        if (!IsCreateShadowMap)
        {
            if (ShadowMaps.Length > 0)
            {
                effect.Parameters["ShadowMap0"]?.SetValue(ShadowMaps[0]);
            }

            if (ShadowMaps.Length > 1)
            {
                effect.Parameters["ShadowMap1"]?.SetValue(ShadowMaps[1]);
            }

            if (ShadowMaps.Length > 2)
            {
                effect.Parameters["ShadowMap2"]?.SetValue(ShadowMaps[2]);
            }
        }
    }
}