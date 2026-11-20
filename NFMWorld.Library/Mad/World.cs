using Maxine.Extensions.Collections;
using NFMWorld.PluginSystem;

namespace NFMWorldLibrary;

public static partial class World
{
    [PluginVisible] public static float OutlineThickness = 1f;
    [PluginVisible] public static bool PixelOutlines;
    [PluginVisible] public static bool IsHyperglidingEnabled = true;
    [PluginVisible] public static int MountainSeed;
    [PluginVisible] public static float MountainCoverage;
    [PluginVisible] public static float CloudCoverage;
    [PluginVisible] public static bool HasPolys;
    [PluginVisible] public static bool HasClouds;
    [PluginVisible] public static bool HasTexture;
    [PluginVisible] public static float FogDensity = 6;

    [PluginVisible] public static Vector3 LightDirection = new Vector3(0, 1, 0);
    [PluginVisible] public static int FadeFrom;
    [PluginVisible] public static float BlackPoint = 0.37f;
    [PluginVisible] public static float WhitePoint = 0.63f;
    [PluginVisible] public static int Ground = 250;
    [PluginVisible] public static Color3 Snap;
    [PluginVisible] public static Color3 Fog;
    [PluginVisible] public static bool LightsOn;
    [PluginVisible] public static Color3 Sky;
    [PluginVisible] public static Color3 GroundColor;
    [PluginVisible] public static bool DrawClouds;
    [PluginVisible] public static bool DrawMountains;
    [PluginVisible] public static bool DrawStars;
    [PluginVisible] public static bool DrawPolys;
    [PluginVisible] public static Color3 GroundPolysColor;

    // texture (without snap)
    [PluginVisible] public static int[] Texture = [0, 0, 0, 50];

    // clouds (without sky applied)
    [PluginVisible] public static int[] Clouds = [210, 210, 210, 1, -1000];

    // clouds (with sky applied)
    [PluginVisible] public static Color3 CloudColor;

    private static int _tick = 0;

    public static bool ChargedPolyBlink;
    public static float ChargeAmount;
    public static int ChargedBlinkCountdown;

    /// <summary>
    /// <see cref="FogDensity"/> in the form the shaders consume: the log2 of the normalized
    /// density. Shaders evaluate <c>exp2(depthTerm * FogLogDensity)</c> instead of
    /// <c>pow(density, depthTerm)</c>, so the log2 is paid once per frame on the CPU instead of
    /// once per pixel (or per vertex) in the shader.
    /// Clamped away from zero because log2(0) is -infinity, which the shader would then multiply
    /// by a depth term of 0 and turn into NaN.
    /// </summary>
    [PluginVisible]
    public static float FogLogDensity
    {
        get
        {
            var density = FogDensity / (FogDensity + 1f);
            return float.IsFinite(density) && density > 0f
                ? MathF.Log2(MathF.Max(density, 1e-6f))
                : MathF.Log2(1e-6f);
        }
    }

    public static void ResetValues()
    {
        HasTexture = false;
        HasClouds = false;
        HasPolys = false;
        CloudCoverage = 1;
        MountainCoverage = 1;
        LightsOn = false;
        DrawClouds = true;
        DrawMountains = true;
        DrawStars = false;
        DrawPolys = true;
        MountainSeed = URandom.Int(0, 100000);
        FogDensity = 0.857f;
        LightDirection = new Vector3(0, 1, 0);
        Snap = new Color3(20, 20, 20);
        Fog = new Color3(150, 150, 150);
        Sky = new Color3(100, 150, 255);
        GroundColor = new Color3(100, 200, 100);
        GroundPolysColor = new Color3(120, 180, 120);
        FadeFrom = 8000;
        Clouds = [210, 210, 210, 1, -1000];
        CloudColor = new Color3(210, 210, 210);
    }

    public static void GameTick()
    {
        if (++_tick == Physics.OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
        {
            if (ChargedBlinkCountdown > 0)
            {
                ChargedPolyBlink = false;
                ChargedBlinkCountdown--;
            }
            else
            {
                if (ChargedPolyBlink)
                {
                    ChargedPolyBlink = false;
                }
                else
                {
                    ChargedPolyBlink = true;
                    ChargeAmount = URandom.Single() * 15.0F - 6.0F;
                }

                _tick = 0;
            }
        }
    }

    // osky = no snap
    // csky = with snap
    public static void SetSky(Color3 skyColor)
    {
        Sky = skyColor;
        // osky[0] = i;
        // osky[1] = i249;
        // osky[2] = i250;
        for (int i251 = 0; i251 < 3; i251++)
        {
            CloudColor[i251] = (short)((Sky[i251] * Clouds[3] + Clouds[i251]) / (Clouds[3] + 1));
            // CloudColor[i251] = (short) (CloudColor[i251] + CloudColor[i251] * (Snap[i251] / 100.0F));
            if (CloudColor[i251] > 255)
            {
                CloudColor[i251] = 255;
            }

            if (CloudColor[i251] < 0)
            {
                CloudColor[i251] = 0;
            }
        }
    }

    public static void SetGround(Color3 groundColor)
    {
        GroundColor = groundColor;
        for (int i259 = 0; i259 < 3; i259++)
        {
            GroundPolysColor[i259] = (short)((GroundColor[i259] * Texture[3] + Texture[i259]) / (1 + Texture[3]));
            // GroundPolysColor[i259] = (int) (GroundPolysColor[i259] + GroundPolysColor[i259] * (Snap[i259] / 100.0F));
            if (GroundPolysColor[i259] > 255)
            {
                GroundPolysColor[i259] = 255;
            }

            if (GroundPolysColor[i259] < 0)
            {
                GroundPolysColor[i259] = 0;
            }
        }
        // for (int i260 = 0; i260 < 3; i260++) {
        //     crgrnd[i260] = (int) ((cpol[i260] * 0.99 + cgrnd[i260]) / 2.0);
        // }
    }

    public static void SetTexture(IReadOnlyList<int> inTexture)
    {
        var texture = new InlineArray4Ex<int>();
        texture[0] = inTexture[0];
        texture[1] = inTexture[1];
        texture[2] = inTexture[2];
        texture[3] = inTexture[3];

        if (texture[3] < 20)
        {
            texture[3] = 20;
        }

        if (texture[3] > 60)
        {
            texture[3] = 60;
        }

        Texture = [.. texture];

        texture[0] = (GroundColor[0] * texture[3] + texture[0]) / (1 + texture[3]);
        texture[1] = (GroundColor[1] * texture[3] + texture[1]) / (1 + texture[3]);
        texture[2] = (GroundColor[2] * texture[3] + texture[2]) / (1 + texture[3]);
        GroundPolysColor[0] = (short)texture[0];
        GroundPolysColor[1] = (short)texture[1];
        GroundPolysColor[2] = (short)texture[2];
        // for (int i264 = 0; i264 < 3; i264++) {
        //     crgrnd[i264] = (int) ((cpol[i264] * 0.99 + cgrnd[i264]) / 2.0);
        // }
    }
}
