// LLM maintained.
//
// A per-frame bump allocator over one GPU buffer: uniforms for every draw, and the copy source for
// buffer and texture updates that have to stay ordered against the draws around them.
//
// Both uses exist because wgpu's queue writes are ordered against submissions rather than against
// the commands being recorded. Two writes to one buffer in a frame collapse to the last one, so
// every draw that reads a value has to own a distinct region of it - hence a ring, reset on submit
// and grown, never recycled mid-frame.

using Wg = Maxine.WebGPU;
using WgpuBuffer = Maxine.WebGPU.Buffer;

namespace NFMWorld.Graphics.WebGPU;

internal sealed unsafe class WebGpuDynamicBuffer : IDisposable
{
    private readonly WebGpuGraphicsDevice _device;
    private readonly string _name;
    private readonly Wg.BufferUsage _usage;
    private readonly ulong _alignment;

    private WgpuBuffer* _handle;
    private ulong _capacity;
    private ulong _cursor;

    /// <summary>
    /// Bumped whenever the underlying buffer is replaced. A bind group or copy that names the old
    /// handle is stale and has to be rebuilt, so callers cache against this rather than the pointer.
    /// </summary>
    internal int Generation { get; private set; }

    internal WgpuBuffer* Handle => _handle;
    internal ulong Capacity => _capacity;

    internal WebGpuDynamicBuffer(
        WebGpuGraphicsDevice device,
        string name,
        Wg.BufferUsage usage,
        ulong alignment,
        ulong initialCapacity
    )
    {
        _device = device;
        _name = name;
        _usage = usage | Wg.BufferUsage.CopyDst | Wg.BufferUsage.CopySrc;
        _alignment = Math.Max(1, alignment);
        Create(Math.Max(initialCapacity, _alignment));
    }

    /// <summary>
    /// Reserves an aligned region and returns where it starts. Grows the buffer rather than wrapping,
    /// because the region handed out earlier in the frame is still referenced by recorded commands.
    /// </summary>
    /// <param name="alignment">
    /// A stricter alignment than the ring's own, for a region only some copies can use. A texture
    /// copy's buffer offset must be a multiple of that format's texel block size - 16 for
    /// <c>Rgba32f</c> - while the ring itself only guarantees WebGPU's 4-byte copy unit.
    /// </param>
    internal ulong Allocate(int size, int alignment = 0)
    {
        var align = Math.Max(_alignment, (ulong)Math.Max(alignment, 1));
        var length = WebGpuInterop.Align((ulong)Math.Max(size, 1), _alignment);

        // The cursor is only kept a multiple of the ring's own alignment, so a stricter request has
        // to move the start; advancing by a ring-aligned length keeps that invariant for later calls.
        var offset = WebGpuInterop.Align(_cursor, align);
        if (offset + length > _capacity)
        {
            var capacity = Math.Max(_capacity * 2, offset + length);
            Create(capacity);
            offset = 0;
        }

        _cursor = offset + length;
        return offset;
    }

    /// <summary>
    /// Copies CPU data into the region at <paramref name="offset"/>. The write is queued
    /// immediately, which is what makes it visible to the submission recorded later in the frame.
    /// </summary>
    internal void Write(ulong offset, ReadOnlySpan<byte> data) => _device.WriteBuffer(_handle, offset, data);

    internal void Reset() => _cursor = 0;

    private void Create(ulong capacity)
    {
        var descriptor = new Wg.BufferDescriptor
        {
            Usage = _usage,
            Size = capacity,
            MappedAtCreation = 0,
        };

        var buffer = _device.Api.DeviceCreateBuffer(_device.Device, &descriptor);
        _device.CheckCreated(buffer is not null, $"{_name} ({capacity} byte(s))");

        if (_handle is not null)
        {
            // Not destroyed, and released only after the next submit: commands already recorded in
            // this frame still name the old buffer, and wgpu resolves their ids when the pass ends.
            var previous = _handle;
            _device.DeferDestroy(() => _device.Api.BufferRelease(previous));
        }

        _handle = buffer;
        _capacity = capacity;
        Generation++;
    }

    public void Dispose()
    {
        if (_handle is null)
            return;

        _device.Api.BufferDestroy(_handle);
        _device.Api.BufferRelease(_handle);
        _handle = null;
    }
}
