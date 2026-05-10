using System.Diagnostics.CodeAnalysis;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld;

public class Lighting
{
    public Camera[] LightCameras;
    public RenderTarget2D?[] ShadowMaps;
    
    [MemberNotNullWhen(true, nameof(CascadeLightCamera))]
    public bool IsCreateShadowMap { get; }
    public int NumCascade;

    public int TotalCascades;

    public Lighting(
        Camera[] lightCameras,
        RenderTarget2D?[] shadowMaps,
        bool isCreateShadowMap = false,
        int numCascade = -1,
        int totalCascades = 3
    )
    {
        LightCameras = lightCameras;
        ShadowMaps = shadowMaps;
        IsCreateShadowMap = isCreateShadowMap;
        TotalCascades = totalCascades;
        NumCascade = numCascade;
        if (numCascade != -1)
        {
            CascadeLightCamera = LightCameras[numCascade];
        }
        else if (isCreateShadowMap)
        {
            throw new InvalidOperationException($"{nameof(numCascade)} must be set if {nameof(isCreateShadowMap)} is set to true");
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
            if (TotalCascades > 0)
            {
                effect.Parameters["ShadowMap0"]?.SetValue(ShadowMaps[0]);

                if (TotalCascades > 1)
                {
                    effect.Parameters["ShadowMap1"]?.SetValue(ShadowMaps[1]);
                    
                    if (TotalCascades > 2)
                    {
                        effect.Parameters["ShadowMap2"]?.SetValue(ShadowMaps[2]);
                    }
                    else
                    {
                        effect.Parameters["ShadowMap2"]?.SetValue((Texture?)null);
                    }
                }
                else
                {
                    effect.Parameters["ShadowMap1"]?.SetValue((Texture?)null);
                    effect.Parameters["ShadowMap2"]?.SetValue((Texture?)null);
                }
            }
            else
            {
                effect.Parameters["ShadowMap0"]?.SetValue((Texture?)null);
                effect.Parameters["ShadowMap1"]?.SetValue((Texture?)null);
                effect.Parameters["ShadowMap2"]?.SetValue((Texture?)null);
            }
        }
        
        effect.Parameters["NumCascades"]?.SetValue(TotalCascades);
    }
}