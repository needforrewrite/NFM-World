using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using NFMWorldLibrary.Rad;
using NFMWorldLibrary.Util;
using NFMWorldMath;

namespace NFMWorld;

public sealed class CollisionDebugMesh : GameObject, IDisposable, IImmediateRenderElement
{
    private int lineTriangleCount;
    private IBuffer? lineIndexBuffer;
    private IBuffer? lineVertexBuffer;
    private readonly int lineVertexCount;
    private IBuffer? lineInstanceBuffer;

    public CollisionDebugMesh(IReadOnlyList<Rad3dBoxDef> boxes)
    {
        if (boxes.Count < 1) return;

        #region Debug boxes

        // disp 0
        const int linesPerPolygon = 16;

        var data = new List<LineMesh.LineMeshVertexAttribute>(LineMeshHelpers.VerticesPerLine * linesPerPolygon * boxes.Count);
        var indices = new List<int>(LineMeshHelpers.IndicesPerLine * linesPerPolygon * boxes.Count);
        void AddLine(Vector3 p0, Vector3 p1, Color3 color, float mult = 1)
        {
            // Create two quads for each line segment to give it some thickness

            Span<LineMesh.LineMeshVertexAttribute> verts = stackalloc LineMesh.LineMeshVertexAttribute[LineMeshHelpers.VerticesPerLine];
            Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, default, default, color, 0f, in verts, in inds);
            indices.AddRange(inds);
            data.AddRange(verts);
        }

        for (var i = 0; i < boxes.Count; i++)
        {
            var box = boxes[i];
            var center = (Vector3)box.Translation;
            var radius = (Vector3)box.Radius;

            // Define the 8 corners of the box
            ReadOnlySpan<Vector3> corners =
            [
                new(center.X - radius.X, center.Y - radius.Y, center.Z - radius.Z), // 0: left-bottom-back
                new(center.X + radius.X, center.Y - radius.Y, center.Z - radius.Z), // 1: right-bottom-back
                new(center.X + radius.X, center.Y + radius.Y, center.Z - radius.Z), // 2: right-top-back
                new(center.X - radius.X, center.Y + radius.Y, center.Z - radius.Z), // 3: left-top-back
                new(center.X - radius.X, center.Y - radius.Y, center.Z + radius.Z), // 4: left-bottom-front
                new(center.X + radius.X, center.Y - radius.Y, center.Z + radius.Z), // 5: right-bottom-front
                new(center.X + radius.X, center.Y + radius.Y, center.Z + radius.Z), // 6: right-top-front
                new(center.X - radius.X, center.Y + radius.Y, center.Z + radius.Z)  // 7: left-top-front
            ];

            // Define the 12 edges as pairs of corner indices
            Span<(int, int, bool isVertical)> edges = new (int, int, bool)[12]
            {
                // Bottom face
                (0, 1, false), (1, 5, false), (5, 4, false), (4, 0, false),
                // Top face
                (3, 2, false), (2, 6, false), (6, 7, false), (7, 3, false),
                // Vertical edges
                (0, 3, true), (1, 2, true), (5, 6, true), (4, 7, true)
            };

            // Check if this is a selected box (yellow color = 255,255,0)
            bool isSelected = box.Color.R == 255 && box.Color.G == 255 && box.Color.B == 0;

            var normalColor = box.Radius.Y <= 1 ? new Color3(255, 0, 0) : new Color3(255, 255, 255);
            var solidSideColor = new Color3(0, 255, 0);
            var flatColor = new Color3(0, 0, 255);
            var selectedColor = new Color3(255, 255, 0); // Yellow for selection

            // Determine which faces are solid
            bool leftSolid = box.Xy == 90;
            bool rightSolid = box.Xy == -90;
            bool backSolid = box.Zy == 90;
            bool frontSolid = box.Zy == -90;
            bool isFlat = box.Xy is not 90 and not -90 && box.Zy is not 90 and not -90;

            foreach (var (i0, i1, isVertical) in edges)
            {
                var p0 = corners[i0];
                var p1 = corners[i1];

                // Determine color based on which face the edge belongs to
                var edgeColor = normalColor;

                // If this box is selected, override all colors with yellow
                if (isSelected)
                {
                    edgeColor = selectedColor;
                }
                else
                {
                    // Check which face(s) this edge belongs to
                    bool isLeft = p0.X < center.X && p1.X < center.X;
                    bool isRight = p0.X > center.X && p1.X > center.X;
                    bool isFront = p0.Z > center.Z && p1.Z > center.Z;
                    bool isBack = p0.Z < center.Z && p1.Z < center.Z;

                    if (isLeft && leftSolid) edgeColor = solidSideColor;
                    else if (isRight && rightSolid) edgeColor = solidSideColor;
                    else if (isFront && frontSolid) edgeColor = solidSideColor;
                    else if (isBack && backSolid) edgeColor = solidSideColor;
                }

                AddLine(p0, p1, edgeColor, edgeColor == solidSideColor || isSelected ? 2f : 1f);

                // Add flat representation if applicable
                if (isFlat && !isVertical)
                {
                    var flatP0 = new Vector3(p0.X, center.Y, p0.Z);
                    var flatP1 = new Vector3(p1.X, center.Y, p1.Z);

                    var angle = new Euler(AngleSingle.ZeroAngle, AngleSingle.FromDegrees(180 - box.Zy), AngleSingle.FromDegrees(180 - box.Xy));

                    // Rotate around center
                    var rotationMatrix = Matrix.CreateFromEuler(angle);
                    var translatedP0 = flatP0 - center;
                    var translatedP1 = flatP1 - center;
                    var rotatedP0 = Vector3.Transform(translatedP0, rotationMatrix) + center;
                    var rotatedP1 = Vector3.Transform(translatedP1, rotationMatrix) + center;

                    // Use yellow if selected, otherwise blue for flat plane
                    var flatEdgeColor = isSelected ? selectedColor : flatColor;
                    AddLine(rotatedP0, rotatedP1, flatEdgeColor, 2f);
                }
            }
        }

        var vertexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(data));
        lineVertexBuffer = GameSparker.NewGraphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Immutable, vertexBytes.Length), vertexBytes);

        var indexBytes = MemoryMarshal.AsBytes(CollectionsMarshal.AsSpan(indices));
        lineIndexBuffer = GameSparker.NewGraphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Index, BufferUsage.Immutable, indexBytes.Length, IndexFormat.UInt32), indexBytes);

        lineTriangleCount = indices.Count / 3;
        lineVertexCount = data.Count;

        lineInstanceBuffer = GameSparker.NewGraphicsDevice.CreateBuffer(
            new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, InstanceData.Stride),
            MemoryMarshal.AsBytes((ReadOnlySpan<InstanceData>)[new InstanceData(MatrixWorld)]));

        #endregion
    }

    ~CollisionDebugMesh()
    {
        Dispose(false);
    }

    /// <summary>
    /// Legacy immediate render path for editor usage.
    /// </summary>
    public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true || !GameSparker.devRenderTrackers) return;
        if (lineInstanceBuffer == null || lineVertexBuffer == null || lineIndexBuffer == null) return;

        cb.UpdateBuffer(lineInstanceBuffer, MemoryMarshal.AsBytes((ReadOnlySpan<InstanceData>)[new InstanceData(MatrixWorld)]));

        var p = Effects.LineParameters;

        cb.SetPipeline(Effects.LinePipeline);
        cb.SetVertexBuffer(0, lineVertexBuffer, LineMesh.LineMeshVertexAttribute.Stride);
        cb.SetVertexBuffer(1, lineInstanceBuffer, InstanceData.Stride);
        cb.SetIndexBuffer(lineIndexBuffer);

        p.SnapColor.SetValue(cb, new Color3(100, 100, 100));
        p.IsFullbright.SetValue(cb, true);
        p.UseBaseColor.SetValue(cb, false);
        p.BaseColor.SetValue(cb, new Vector3(0, 0, 0));
        p.ChargedBlinkAmount.SetValue(cb, 0.0f);
        p.HalfThickness.SetValue(cb, World.OutlineThickness);

        // Collision debug lines are editor/debug overlays, so gameplay outline modes must not hide them.
        LineEffectDistantOutlineSettings.Apply(cb, DistantOutlineBehavior.AlwaysRender);

        p.LightDirection.SetValue(cb, World.LightDirection);
        p.FogColor.SetValue(cb, (Vector3)World.Fog.Snap(World.Snap));
        p.FogDistance.SetValue(cb, World.FadeFrom);
        p.FogLogDensity.SetValue(cb, World.FogLogDensity);
        p.EnvironmentLight.SetValue(cb, new Vector2(World.BlackPoint, World.WhitePoint));
        p.DepthBias.SetValue(cb, 0.00005f);
        p.Alpha.SetValue(cb, 1f);

        p.View.SetValue(cb, camera.ViewMatrix);
        p.Projection.SetValue(cb, camera.ProjectionMatrix);
        p.ViewProj.SetValue(cb, camera.ViewMatrix * camera.ProjectionMatrix);
        p.CameraPosition.SetValue(cb, camera.Position);

        p.Expand.SetValue(cb, false);
        p.Darken.SetValue(cb, 1.0f);
        p.RandomFloat.SetValue(cb, URandom.Single());

        cb.DrawIndexedInstanced(baseVertex: 0, startIndex: 0, primitiveCount: lineTriangleCount, instanceCount: 1);
    }

    public override void SubmitDraws(RenderQueue queue, Camera camera, Lighting? lighting, RenderPass pass)
    {
        if (pass.IsShadow || !GameSparker.devRenderTrackers) return;

        if (lineVertexBuffer == null || lineIndexBuffer == null || lineInstanceBuffer == null) return;

        queue.AddImmediate(SortKey.Create(RenderBucket.CollisionDebugMesh), this);
    }

    private void ReleaseUnmanagedResources()
    {
        lineIndexBuffer?.Dispose();
        lineVertexBuffer?.Dispose();
        lineInstanceBuffer?.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
