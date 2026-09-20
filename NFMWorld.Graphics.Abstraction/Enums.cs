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

    /// <summary>Block-compressed, 4 bits/pixel, no alpha (or 1-bit punch-through alpha).</summary>
    Dxt1,
    /// <summary>Block-compressed, 8 bits/pixel, sharp (non-interpolated) alpha.</summary>
    Dxt3,
    /// <summary>Block-compressed, 8 bits/pixel, interpolated alpha.</summary>
    Dxt5,

    /// <summary>32-bit single-channel float - used for shadow-cascade depth render targets.</summary>
    Single,
}

[Flags]
public enum ClearOptions
{
    None = 0,
    Color = 1 << 0,
    Depth = 1 << 1,
    Stencil = 1 << 2,
}
