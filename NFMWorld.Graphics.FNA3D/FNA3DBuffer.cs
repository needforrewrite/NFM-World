namespace NFMWorld.Graphics.FNA3D;

internal sealed class FNA3DBuffer : IBuffer
{
    public IntPtr Device { get; }
    public IntPtr Handle { get; private set; }
    public BufferKind Kind { get; }
    public BufferUsage Usage { get; }
    public int SizeInBytes { get; }
    public IndexFormat IndexFormat { get; }

    public FNA3DBuffer(IntPtr device, IntPtr handle, BufferDesc desc)
    {
        Device = device;
        Handle = handle;
        Kind = desc.Kind;
        Usage = desc.Usage;
        SizeInBytes = desc.SizeInBytes;
        IndexFormat = desc.IndexFormat;
    }

    public void Dispose()
    {
        if (Handle == IntPtr.Zero) return;
        if (Kind == BufferKind.Vertex)
            FNA3D_AddDisposeVertexBuffer(Device, Handle);
        else
            FNA3D_AddDisposeIndexBuffer(Device, Handle);
        Handle = IntPtr.Zero;
    }
}
