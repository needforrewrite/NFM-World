using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public static class World
{
    public static float FogDensity = 0.857f; // TODO ASSIGN
    public static Vector3 LightDirection = new Vector3(0, 1, 0);
    public static int FadeFrom;
    public static float Density;
    public static float BlackPoint = 0.37f;
    public static float WhitePoint = 0.63f;
    public static int Ground = 250;
    public static Color3 Snap;
    public static Color3 Fog;
    public static bool LightsOn;
    public static Color3 Sky;
    public static Color3 GroundColor;
}