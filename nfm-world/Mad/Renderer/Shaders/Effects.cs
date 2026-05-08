using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld;

public static class Effects
{
    public static PolyEffect Poly { get; private set; }
    public static LineEffect Line { get; private set; }
    public static BasicEffect Chip { get; private set; }
    public static BasicEffect Dust { get; private set; }
    public static BasicEffect Flame { get; private set; }
    public static BasicEffect FixHoop { get; private set; }
    public static Effect Sky { get; private set; }
    public static Effect Ground { get; private set; }
    public static Effect Mountains { get; private set; }

    public static void Initialize(GraphicsDevice graphicsDevice)
    {
        var polyShader = new Effect(graphicsDevice, VFS.ReadAllBytes("./data/shaders/Poly.fxb"))
        {
            Name = "Poly"
        };
        var lineShader = new Effect(graphicsDevice, VFS.ReadAllBytes("./data/shaders/Line.fxb"))
        {
            Name = "Line"
        };
        Sky = new Effect(graphicsDevice, VFS.ReadAllBytes("./data/shaders/Sky.fxb"))
        {
            Name = "Sky"
        };
        Ground = new Effect(graphicsDevice, VFS.ReadAllBytes("./data/shaders/Ground.fxb"))
        {
            Name = "Ground"
        };
        Mountains = new Effect(graphicsDevice, VFS.ReadAllBytes("./data/shaders/Mountains.fxb"))
        {
            Name = "Mountains"
        };

        Poly = new PolyEffect(polyShader);
        Line = new LineEffect(lineShader);
        Chip = new BasicEffect(graphicsDevice)
        {
            Name = "Chip Effect",
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
        Dust = new BasicEffect(graphicsDevice)
        {
            Name = "Dust Effect",
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
        Flame = new BasicEffect(graphicsDevice)
        {
            Name = "Flames Effect",
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
        FixHoop = new BasicEffect(graphicsDevice)
        {
            Name = "FixHoop Electricity Effect",
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
    }
}