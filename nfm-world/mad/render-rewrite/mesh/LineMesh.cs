using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class LineMesh
{
    private readonly PolyEffect _material = new(Program._polyShader);
    private readonly Mesh _supermesh;
    private readonly GraphicsDevice _graphicsDevice;
    private readonly VertexBuffer _lineVertexBuffer;
    private readonly IndexBuffer _lineIndexBuffer;
    private readonly int _lineTriangleCount;
    private readonly LineType _lineType;
    private readonly int _lineVertexCount;

    public LineMesh(
        Mesh supermesh,
        GraphicsDevice graphicsDevice,
        IReadOnlyCollection<KeyValuePair<(Vector3 Point0, Vector3 Point1), (Rad3dPoly Poly, Vector3 Centroid, Vector3 Normal)>> lines,
        LineType lineType
    )
    {
        _lineType = lineType;
        var data = new List<Mesh.VertexPositionNormalColorCentroid>(8 * lines.Count);
        var indices = new List<int>(12 * 3 * lines.Count);

        const float halfThickness = 1f;
        Span<Vector3> verts = stackalloc Vector3[LineMeshHelpers.VerticesPerLine];
        Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

        foreach (var line in lines)
        {
            // Create two quads for each line segment to give it some thickness
            var p0 = line.Key.Point0;
            var p1 = line.Key.Point1;
            var poly = line.Value.Poly;
            var centroid = line.Value.Centroid;
            var normal = line.Value.Normal;
            var color = poly.LineType == LineType.Colored
                ? (poly.Color - new Color3(10, 10, 10))
                : poly.LineType == LineType.Charged
                    ? poly.Color
                    : Color.Black;

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, halfThickness, in verts, in inds);
            indices.AddRange(inds);
            foreach (var vert in verts)
            {
                data.Add(new Mesh.VertexPositionNormalColorCentroid(vert, normal, centroid, color, 0.0f));
            }
        }

        var lineVertexBuffer = new VertexBuffer(graphicsDevice,
            Mesh.VertexPositionNormalColorCentroid.VertexDeclaration, data.Count, BufferUsage.None);
        lineVertexBuffer.SetData(data.ToArray());

        var lineIndexBuffer =
            new IndexBuffer(graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        lineIndexBuffer.SetData(indices.ToArray());

        var lineVertexCount = data.Count;
        var lineTriangleCount = indices.Count / 3;

        _supermesh = supermesh;
        _graphicsDevice = graphicsDevice;
        _lineVertexBuffer = lineVertexBuffer;
        _lineIndexBuffer = lineIndexBuffer;
        _lineTriangleCount = lineTriangleCount;
        _lineVertexCount = lineVertexCount;
    }

    public void Render(Camera camera, Lighting? lighting, Matrix matrixWorld)
    {
        _graphicsDevice.SetVertexBuffer(_lineVertexBuffer);
        _graphicsDevice.Indices = _lineIndexBuffer;
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;

        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(matrixWorld);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(matrixWorld)));
        _material.SnapColor?.SetValue(World.Snap.ToVector3());
        _material.IsFullbright?.SetValue(false);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Microsoft.Xna.Framework.Vector3(0, 0, 0));
        _material.ChargedBlinkAmount?.SetValue(_lineType is LineType.Charged && World.ChargedPolyBlink ? World.ChargeAmount : 0.0f);

        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(_supermesh.GetsShadowed);
        _material.Alpha?.SetValue(_supermesh.alphaOverride ?? 1f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.WorldView?.SetValue(matrixWorld * camera.ViewMatrix);
        _material.WorldViewProj?.SetValue(matrixWorld * camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position);

        _material.CurrentTechnique = _material.Techniques["Basic"];

        _material.Expand?.SetValue(_supermesh.Flames.Expand);
        _material.Darken?.SetValue(_supermesh.Flames.Darken);
        _material.RandomFloat?.SetValue(URandom.Single());

        lighting?.SetShadowMapParameters(_material.UnderlyingEffect);

        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _lineVertexCount, 0, _lineTriangleCount);
        }
    }
}