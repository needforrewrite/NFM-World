using System.Collections.Immutable;
using NFMWorld.Util;

namespace NFMWorld.Mad;

public class GameObject : Transform, IImmediateRenderable
{
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