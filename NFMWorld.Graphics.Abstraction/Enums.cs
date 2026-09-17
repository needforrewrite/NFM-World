namespace NFMWorld.Graphics;

public enum BufferKind
{
    Vertex,
    Index,
}

public enum BufferUsage
{
    /// <summary>Content is set once at creation and never updated.</summary>
    Immutable,

    /// <summary>Content is expected to be overwritten frequently (discard-on-update semantics).</summary>
    Dynamic,
}

public enum IndexFormat
{
    UInt16,
    UInt32,
}

public enum PrimitiveTopology
{
    TriangleList,
    TriangleStrip,
    LineList,
    LineStrip,
    PointList,
}

public enum TextureFormat
{
    Rgba8,
    Bgra8,
    Depth24Stencil8,
    R8,
}

[Flags]
public enum ClearOptions
{
    None = 0,
    Color = 1 << 0,
    Depth = 1 << 1,
    Stencil = 1 << 2,
}
