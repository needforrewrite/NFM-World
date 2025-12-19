using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public static class World
{
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
    
    // clouds (without snap)
    public static int[] Clouds = [210, 210, 210, 1, -1000];

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
    }

    private static int _tick = 0;
    
    public static bool ChargedPolyBlink;
    public static float ChargeAmount;
    public static int ChargedBlinkCountdown;
    public static void GameTick()
    {
        if (_tick == 2) // delay all operations by 3 ticks because of the adjusted tickrate
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
        else
        {
            _tick++;
        }
    }
}