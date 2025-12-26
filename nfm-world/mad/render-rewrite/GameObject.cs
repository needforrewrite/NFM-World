using System.Collections.Immutable;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameObject : Transform, IImmediateRenderable
{
    public IReadOnlyList<GameObject> Children { get; set; } = [];

    /// <summary>
    /// Gets mesh render data for instanced rendering.
    /// </summary>
    /// <param name="lighting">The lighting</param>
    /// <returns>Meshes and matrices to render</returns>
    public virtual IEnumerable<RenderData> GetRenderData(Lighting? lighting)
    {
        foreach (var child in Children)
        foreach (var renderData in child.GetRenderData(lighting))
        {
            yield return renderData;
        }
    }

    public virtual void Render(Camera camera, Lighting? lighting)
    {
        foreach (var child in Children)
        {
            child.Render(camera, lighting);
        }
    }

    public virtual void OnBeforeRender()
    {
        foreach (var child in Children)
        {
            child.OnBeforeRender();
        }
    }
}

public readonly record struct RenderData(
    IInstancedRenderElement RenderElement,
    Matrix World,
    bool GetsShadowed = true,
    float AlphaOverride = 1.0f,
    bool IsFullbright = false,
    bool Glow = false,
    int RenderOrder = 0
)
{
    public InstanceData ToInstanceData() => new(World, GetsShadowed, AlphaOverride, IsFullbright, Glow);
}

public struct InstanceData(Matrix world, bool getsShadowed = false, float alphaOverride = 1.0f, bool isFullbright = false, bool glow = false)
{
    public static VertexDeclaration InstanceDeclaration { get; } = new VertexDeclaration
    (
        new VertexElement(0,  VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 3),
        new VertexElement(16, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 4),
        new VertexElement(32, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 5),
        new VertexElement(48, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 6),
        new VertexElement(64, VertexElementFormat.Vector4, VertexElementUsage.TextureCoordinate, 7)
    );
    
    public Matrix World = Matrix.Transpose(world);
    public Vector4 AdditionalData = new(getsShadowed ? 1.0f : 0.0f, alphaOverride, isFullbright ? 1.0f : 0.0f, glow ? 1.0f : 0.0f); // x: GetsShadowed (1.0 or 0.0), y: AlphaOverride, z: IsFullbright (1.0 or 0.0), w: Glow (1.0 or 0.0)
}