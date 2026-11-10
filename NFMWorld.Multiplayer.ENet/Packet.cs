namespace ENet;

/// <summary>
/// Managed replacement for <c>ENetPacket</c>. Packets are reference counted by the protocol
/// engine, so a packet queued to several peers (or split into fragments) stays alive until the
/// last command referencing it is released.
/// </summary>
public struct Packet : IDisposable
{
    internal sealed class PacketData
    {
        public byte[] Buffer = [];
        public int Length;
        public PacketFlags Flags;
        public int ReferenceCount;
        public bool Destroyed;

        public void AddReference() => ++ReferenceCount;

        public void ReleaseReference()
        {
            if (ReferenceCount > 0)
                --ReferenceCount;
        }

        /// <summary>Engine-owned release: the packet is destroyed once nothing references it.</summary>
        public void ReleaseAndDestroyIfUnreferenced()
        {
            ReleaseReference();

            if (ReferenceCount == 0)
            {
                Flags |= PacketFlags.Sent;
                Destroyed = true;
            }
        }
    }

    internal PacketData? Content;

    internal Packet(PacketData content)
    {
        Content = content;
    }

    public readonly bool IsSet => Content is { Destroyed: false };

    public readonly int Length
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.Length;
        }
    }

    public readonly PacketFlags Flags
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.Flags;
        }
    }

    public readonly ReadOnlySpan<byte> Data
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.Buffer.AsSpan(0, Content.Length);
        }
    }

    public readonly ReadOnlyMemory<byte> Memory
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.Buffer.AsMemory(0, Content.Length);
        }
    }

    public readonly Span<byte> DataSpan
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.Buffer.AsSpan(0, Content.Length);
        }
    }

    public readonly bool HasReferences
    {
        get
        {
            ThrowIfNotCreated();
            return Content!.ReferenceCount > 0;
        }
    }

    public readonly byte[] ToArray() => Data.ToArray();

    public void Dispose()
    {
        if (Content is { } content)
        {
            if (content.ReferenceCount == 0)
                content.Destroyed = true;

            Content = null;
        }
    }

    public void Create(byte[] data) => Create(data, data.Length, PacketFlags.None);

    public void Create(byte[] data, int length) => Create(data, length, PacketFlags.None);

    public void Create(byte[] data, PacketFlags flags) => Create(data, data.Length, flags);

    public void Create(byte[] data, int length, PacketFlags flags)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        Create(data.AsSpan(0, length), flags);
    }

    public void Create(byte[] data, int offset, int length, PacketFlags flags)
    {
        ArgumentNullException.ThrowIfNull(data);
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);

        if (offset + length > data.Length)
            throw new ArgumentOutOfRangeException(nameof(length));

        Create(data.AsSpan(offset, length), flags);
    }

    public void Create(ReadOnlySpan<byte> data, PacketFlags flags = PacketFlags.None)
    {
        var buffer = new byte[data.Length];
        data.CopyTo(buffer);

        Content = new PacketData
        {
            Buffer = buffer,
            Length = data.Length,
            Flags = flags
        };
    }

    private readonly void ThrowIfNotCreated()
    {
        if (Content is null || Content.Destroyed)
            throw new InvalidOperationException("Packet not created");
    }

    internal readonly void ThrowIfNotCreatedPublic() => ThrowIfNotCreated();
}
