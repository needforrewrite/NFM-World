using nfm_world_library.Lua;
using NFMWorldLibrary.FixedMath;
using NFMWorldLibrary.Util;

namespace NFMWorldLibrary.Backend;

[LuaVisible]
public abstract partial class BackendGameObject : ITransform
{
    [LuaName("children")]
    public LuaList<BackendGameObject> Children { get; } = [];
    IReadOnlyList<ITransform> ITransform.ChildTransforms => Children;

    [LuaName("parent")]
    public BackendGameObject? Parent { get; set; }
    ITransform? ITransform.Parent => Parent;

    [LuaName("position")]
    public f64Vector3 Position { get; set; }
    
    [LuaName("rotation")]
    public f64Euler Rotation { get; set; }
}