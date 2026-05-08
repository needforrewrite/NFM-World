using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld;

public static class VertexPacker
{
    public static VertexDeclaration Pack(params ReadOnlySpan<Element> elements)
    {
        return Pack(elements, 1);
    }
    
    public static VertexDeclaration Pack(
        ReadOnlySpan<Element> elements,
        int minimumPackWidth
    )
    {
        var offset = 0;
        
        var vertexElements = new VertexElement[elements.Length];
        for (var i = 0; i < elements.Length; i++)
        {
            vertexElements[i] = new VertexElement(
                offset,
                elements[i].ElementFormat,
                elements[i].ElementUsage,
                elements[i].UsageIndex
            );
            
            offset += Math.Max(GetTypeSize(elements[i].ElementFormat), minimumPackWidth);
        }
        
        return new VertexDeclaration(vertexElements);
    }

    private static int GetTypeSize(VertexElementFormat elementFormat)
    {
        switch (elementFormat)
        {
            case VertexElementFormat.Single:
                return 4;
            case VertexElementFormat.Vector2:
                return 8;
            case VertexElementFormat.Vector3:
                return 12;
            case VertexElementFormat.Vector4:
                return 16;
            case VertexElementFormat.Color:
                return 4;
            case VertexElementFormat.Byte4:
                return 4;
            case VertexElementFormat.Short2:
                return 4;
            case VertexElementFormat.Short4:
                return 8;
            case VertexElementFormat.NormalizedShort2:
                return 4;
            case VertexElementFormat.NormalizedShort4:
                return 8;
            case VertexElementFormat.HalfVector2:
                return 4;
            case VertexElementFormat.HalfVector4:
                return 8;
        }
        return 0;
    }

    public readonly record struct Element(
        VertexElementFormat ElementFormat,
        VertexElementUsage ElementUsage,
        int UsageIndex = 0
    );
}