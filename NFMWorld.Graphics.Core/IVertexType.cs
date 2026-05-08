using System.Runtime.CompilerServices;

namespace NFMWorld.Graphics.Core;

/// <summary>
/// Implemented by vertex structs to describe their layout for pipeline creation.
/// </summary>
public interface IVertexType
{
    static abstract VertexElementFormat[] Formats { get; }
    static abstract uint[] Offsets { get; }
}

/// <summary>
/// Helper to build <see cref="VertexInputState"/> from <see cref="IVertexType"/> implementations.
/// </summary>
public static class VertexInputStateHelper
{
    public static VertexInputState CreateSingleBinding<T>(
        uint slot = 0,
        VertexInputRate inputRate = VertexInputRate.Vertex,
        uint stepRate = 0,
        uint locationOffset = 0
    ) where T : unmanaged, IVertexType
    {
        var formats = T.Formats;
        var offsets = T.Offsets;
        var attributes = new VertexAttribute[formats.Length];
        for (int i = 0; i < formats.Length; i++)
        {
            attributes[i] = new VertexAttribute
            {
                Location = locationOffset + (uint)i,
                BufferSlot = slot,
                Format = formats[i],
                Offset = offsets[i],
            };
        }

        return new VertexInputState
        {
            VertexBufferDescriptions =
            [
                new VertexBufferDescription
                {
                    Slot = slot,
                    Pitch = (uint)Unsafe.SizeOf<T>(),
                    InputRate = inputRate,
                    InstanceStepRate = stepRate,
                }
            ],
            VertexAttributes = attributes,
        };
    }
}
