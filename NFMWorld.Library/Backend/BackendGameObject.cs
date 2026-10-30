using NFMWorld.PluginSystem;
using NFMWorldLibrary.FixedMath;

namespace NFMWorldLibrary.Backend;

[PluginVisible]
public abstract partial class BackendGameObject : ITransform
{
    [PluginVisible]
    public List<BackendGameObject> Children { get; } = [];
    IReadOnlyList<ITransform> ITransform.ChildTransforms => Children;

    [PluginVisible]
    public BackendGameObject? Parent { get; set; }
    ITransform? ITransform.Parent => Parent;

    [PluginVisible]
    public f64Vector3 Position { get; set; }
    
    [PluginVisible]
    public f64Euler Rotation { get; set; }
}