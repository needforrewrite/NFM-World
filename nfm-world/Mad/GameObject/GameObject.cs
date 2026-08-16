using NFMWorldLibrary;

namespace NFMWorld;

public class GameObject : Transform, IRenderable
{
    public IReadOnlyList<GameObject> Children { get; set; } = [];

    public override IReadOnlyList<ITransform> ChildTransforms => Children;

    /// <summary>
    /// Submit draws to the unified render queue. Default implementation recurses into children.
    /// </summary>
    public virtual void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        foreach (var child in Children)
        {
            child.SubmitDraws(queue, camera, lighting, pass);
        }
    }

    public override void OnBeforeRender(float alpha)
    {
        base.OnBeforeRender(alpha);
        foreach (var child in Children)
        {
            child.OnBeforeRender(alpha);
        }
    }

    public override void GameTick(BackendStage? stage = null)
    {
        base.GameTick(stage);
        foreach (var child in Children)
        {
            child.GameTick(stage);
        }
    }
}