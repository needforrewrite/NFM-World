using GraphicsDevice = nfm_world.compat.GraphicsDeviceCompat;
using DepthStencilState = nfm_world.compat.DepthStencilState;
using RasterizerState = nfm_world.compat.RasterizerState;
using BlendState = nfm_world.compat.BlendState;
using SamplerState = nfm_world.compat.SamplerState;
using VertexElementFormat = nfm_world.compat.VertexElementFormat;
using nfm_world.compat;
using Microsoft.Xna.Framework;
using MoonWorks.Graphics;

namespace nfm_world;

public struct InstanceData(Matrix world, bool getsShadowed = false, float alphaOverride = 1.0f, bool isFullbright = false, bool glow = false)
    : MoonWorks.Graphics.IVertexType
{
    public static VertexDeclaration InstanceDeclaration { get; } = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7)
    );

    public static MoonWorks.Graphics.VertexElementFormat[] Formats =>
    [
        MoonWorks.Graphics.VertexElementFormat.Float4, // location 0: World Row 0
        MoonWorks.Graphics.VertexElementFormat.Float4, // location 1: World Row 1
        MoonWorks.Graphics.VertexElementFormat.Float4, // location 2: World Row 2
        MoonWorks.Graphics.VertexElementFormat.Float4, // location 3: World Row 3
        MoonWorks.Graphics.VertexElementFormat.Float4  // location 4: AdditionalData
    ];

    public static uint[] Offsets => [0, 16, 32, 48, 64];
    
    public Matrix World = Matrix.Transpose(world);
    public Vector4 AdditionalData = new(getsShadowed ? 1.0f : 0.0f, alphaOverride, isFullbright ? 1.0f : 0.0f, glow ? 1.0f : 0.0f); // x: GetsShadowed (1.0 or 0.0), y: AlphaOverride, z: IsFullbright (1.0 or 0.0), w: Glow (1.0 or 0.0)
}