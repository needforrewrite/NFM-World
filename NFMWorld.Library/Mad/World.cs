using System.Runtime.CompilerServices;

namespace NFMWorldLibrary;

public static partial class World
{
    /// <summary>
    /// Adds extra collision radius and damage to compensate for lag.
    /// </summary>
    public static bool UseMultiplayerCollisionModifiers = true;
    public static float OutlineThickness = 1f;
    public static bool IsHyperglidingEnabled = true;
    public static int MountainSeed;
    public static float MountainCoverage;
    public static float CloudCoverage;
    public static bool HasPolys;
    public static bool HasClouds;
    public static bool HasTexture;
    public static float FogDensity = 6;
    public static Vector3 LightDirection = new Vector3(0, 1, 0);
    public static int FadeFrom;
    public static float BlackPoint = 0.37f;
    public static float WhitePoint = 0.63f;
    public static int Ground = 250;
    public static Color3 Snap;
    public static Color3 Fog;
    public static bool LightsOn;
    public static Color3 Sky;
    public static Color3 GroundColor;
    public static bool DrawClouds;
    public static bool DrawMountains;
    public static bool DrawStars;
    public static bool DrawPolys;
    public static Color3 GroundPolysColor;

    // texture (without snap)
    public static int[] Texture = [0, 0, 0, 50];
    
    // clouds (without sky applied)
    public static int[] Clouds = [210, 210, 210, 1, -1000];
    // clouds (with sky applied)
    public static Color3 CloudColor;

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
        DrawStars = true;
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

    private static int _tick = 0;
    
    public static bool ChargedPolyBlink;
    public static float ChargeAmount;
    public static int ChargedBlinkCountdown;
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
        for (int i251 = 0; i251 < 3; i251++) {
            CloudColor[i251] = (short)((Sky[i251] * Clouds[3] + Clouds[i251]) / (Clouds[3] + 1));
            // CloudColor[i251] = (short) (CloudColor[i251] + CloudColor[i251] * (Snap[i251] / 100.0F));
            if (CloudColor[i251] > 255) {
                CloudColor[i251] = 255;
            }
            if (CloudColor[i251] < 0) {
                CloudColor[i251] = 0;
            }
        }
    }

    public static void SetGround(Color3 groundColor) {
        GroundColor = groundColor;
        for (int i259 = 0; i259 < 3; i259++) {
            GroundPolysColor[i259] = (short)((GroundColor[i259] * Texture[3] + Texture[i259]) / (1 + Texture[3]));
            // GroundPolysColor[i259] = (int) (GroundPolysColor[i259] + GroundPolysColor[i259] * (Snap[i259] / 100.0F));
            if (GroundPolysColor[i259] > 255) {
                GroundPolysColor[i259] = 255;
            }
            if (GroundPolysColor[i259] < 0) {
                GroundPolysColor[i259] = 0;
            }
        }
        // for (int i260 = 0; i260 < 3; i260++) {
        //     crgrnd[i260] = (int) ((cpol[i260] * 0.99 + cgrnd[i260]) / 2.0);
        // }
    }

    public static void SetTexture(InlineArray4<int> texture) {
        if (texture[3] < 20) {
            texture[3] = 20;
        }
        if (texture[3] > 60) {
            texture[3] = 60;
        }

        Texture = [..texture];

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