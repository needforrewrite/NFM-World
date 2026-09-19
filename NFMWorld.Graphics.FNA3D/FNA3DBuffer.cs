namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DBuffer(IntPtr device, IntPtr handle, BufferDesc desc) : TrackLeaks, IBuffer
{
    public IntPtr Device { get; } = device;
    public IntPtr Handle { get; private set; } = handle;
    public BufferKind Kind { get; } = desc.Kind;
    public BufferUsage Usage { get; } = desc.Usage;
    public int SizeInBytes { get; } = desc.SizeInBytes;
    public IndexFormat IndexFormat { get; } = desc.IndexFormat;
    
    public override void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        if (Kind == BufferKind.Vertex)
            FNA3D_AddDisposeVertexBuffer(Device, Handle);
        else
            FNA3D_AddDisposeIndexBuffer(Device, Handle);
        Handle = IntPtr.Zero;
        GC.SuppressFinalize(this);
    }
}
