namespace NFMWorld.Graphics;

/// <summary>
/// Descriptor for creating an <see cref="IBuffer"/>. FNA3D only exposes vertex and index buffers
/// natively (shader parameters go through Effect parameters, not a bindable uniform buffer), so
/// <see cref="BufferKind"/> is intentionally scoped to what the first backend can actually implement.
/// </summary>
public readonly record struct BufferDesc(
    BufferKind Kind,
    BufferUsage Usage,
    int SizeInBytes,
    IndexFormat IndexFormat = IndexFormat.UInt16);

public interface IBuffer : IDisposable
{
    BufferKind Kind { get; }
    BufferUsage Usage { get; }
    int SizeInBytes { get; }
}
