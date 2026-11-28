using NFMWorldLibrary.Rad;

namespace NFMWorld;

internal sealed record EnvironmentMeshData(PositionColorVertex[] Vertices, uint[] Indices)
{
    public static EnvironmentMeshData Prepare(Rad3dPoly[] polygons)
    {
        var vertices = new List<PositionColorVertex>();
        var indices = new List<uint>();

        foreach (var polygon in polygons)
        {
            var offset = (uint)vertices.Count;
            foreach (var point in polygon.Points)
                vertices.Add(new(point, (Color)polygon.Color));
            foreach (var index in polygon.Triangles)
                indices.Add(index + offset);
        }

        return new(vertices.ToArray(), indices.ToArray());
    }
}
