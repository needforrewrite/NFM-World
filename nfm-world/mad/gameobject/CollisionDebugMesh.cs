using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.mad.rad;
using nfm_world_library.util;
using nfm_world.camera;
using nfm_world.renderable.mesh.render_elements;
using nfm_world.renderable.mesh.utils;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.gameobject;

public sealed class CollisionDebugMesh : GameObject, IDisposable
{
    private readonly uint _lineIndexCount;
    private readonly GpuBuffer _lineVertexBuffer;
    private readonly GpuBuffer _lineIndexBuffer;
    private readonly GpuBuffer _lineInstanceBuffer;

    public CollisionDebugMesh(Span<Rad3dBoxDef> boxes)
    {
        #region Debug boxes
        
        // disp 0
        const int linesPerPolygon = 16;
        
        var data = new List<LineMesh.LineMeshVertexAttribute>(LineMeshHelpers.VerticesPerLine * linesPerPolygon * boxes.Length);
        var indices = new List<int>(LineMeshHelpers.IndicesPerLine * linesPerPolygon * boxes.Length);
        void AddLine(Vector3 p0, Vector3 p1, Color3 color, float mult = 1)
        {
            // Create two quads for each line segment to give it some thickness
            
            Span<LineMesh.LineMeshVertexAttribute> verts = stackalloc LineMesh.LineMeshVertexAttribute[LineMeshHelpers.VerticesPerLine];
            Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, default, default, color, 0f, in verts, in inds);
            indices.AddRange(inds);
            data.AddRange(verts);
        }
        
        for (var i = 0; i < boxes.Length; i++)
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

        var device = GameSparker._graphicsDevice;
        var vtxCount = (uint)data.Count;
        _lineIndexCount = (uint)indices.Count;

        _lineVertexBuffer = GpuBuffer.Create<LineMesh.LineMeshVertexAttribute>(device, BufferUsageFlags.Vertex, vtxCount);
        _lineIndexBuffer = GpuBuffer.Create<int>(device, BufferUsageFlags.Index, _lineIndexCount);
        _lineInstanceBuffer = GpuBuffer.Create<InstanceData>(device, BufferUsageFlags.Vertex, 1);

        using var vtxTransfer = TransferBuffer.Create<LineMesh.LineMeshVertexAttribute>(device, TransferBufferUsage.Upload, vtxCount);
        var vtxSpan = vtxTransfer.Map<LineMesh.LineMeshVertexAttribute>(false);
        CollectionsMarshal.AsSpan(data).CopyTo(vtxSpan);
        vtxTransfer.Unmap();

        using var idxTransfer = TransferBuffer.Create<int>(device, TransferBufferUsage.Upload, _lineIndexCount);
        var idxSpan = idxTransfer.Map<int>(false);
        CollectionsMarshal.AsSpan(indices).CopyTo(idxSpan);
        idxTransfer.Unmap();

        using var instTransfer = TransferBuffer.Create<InstanceData>(device, TransferBufferUsage.Upload, 1);
        var instSpan = instTransfer.Map<InstanceData>(false);
        instSpan[0] = new InstanceData(MatrixWorld);
        instTransfer.Unmap();

        var cmd = device.AcquireCommandBuffer();
        var copyPass = cmd.BeginCopyPass();
        copyPass.UploadToBuffer<LineMesh.LineMeshVertexAttribute>(
            vtxTransfer, _lineVertexBuffer, 0, 0, vtxCount, false);
        copyPass.UploadToBuffer<int>(
            idxTransfer, _lineIndexBuffer, 0, 0, _lineIndexCount, false);
        copyPass.UploadToBuffer<InstanceData>(
            instTransfer, _lineInstanceBuffer, 0, 0, 1, false);
        cmd.EndCopyPass(copyPass);
        device.Submit(cmd);

        #endregion
    }

    ~CollisionDebugMesh()
    {
        Dispose(false);
    }

    public override void UploadBuffers(CopyPass copyPass)
    {
        base.UploadBuffers(copyPass);
        if (!GameSparker.devRenderTrackers) return;

        var device = GameSparker._graphicsDevice;
        using var instTransfer = TransferBuffer.Create<InstanceData>(device, TransferBufferUsage.Upload, 1);
        var instSpan = instTransfer.Map<InstanceData>(false);
        instSpan[0] = new InstanceData(MatrixWorld);
        instTransfer.Unmap();

        copyPass.UploadToBuffer<InstanceData>(instTransfer, _lineInstanceBuffer, 0, 0, 1, true);
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true || !GameSparker.devRenderTrackers) return;

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        var fog = new FogParams
        {
            Color = (Vector3)World.Fog.Snap(World.Snap),
            Distance = World.FadeFrom,
            Density = World.FogDensity / (World.FogDensity + 1f)
        };

        cmd.PushVertexUniformData(new LineVertexUniforms
        {
            View = camera.ViewMatrix,
            Projection = camera.ProjectionMatrix,
            ViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            CameraPosition = camera.Position,
            Alpha = 1f,
            SnapColor = (Vector3)new Color3(100, 100, 100),
            Darken = 1.0f,
            LightDirection = World.LightDirection,
            RandomFloat = URandom.Single(),
            EnvironmentLight = new Vector2(World.BlackPoint, World.WhitePoint),
            IsFullbright = true,
            UseBaseColor = false,
            BaseColor = Vector3.Zero,
            Expand = false,
            Fog = fog,
            HalfThickness = World.OutlineThickness,
            ChargedBlinkAmount = 0f
        });

        cmd.PushFragmentUniformData(new LineFragUniforms
        {
            Shadow = default
        });

        pass.BindGraphicsPipeline(Pipelines.Line);
        pass.BindVertexBuffers(
            new BufferBinding(_lineVertexBuffer, 0),
            new BufferBinding(_lineInstanceBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_lineIndexBuffer, 0), IndexElementSize.ThirtyTwo);
        pass.DrawIndexedPrimitives(_lineIndexCount, 1, 0, 0, 0);
    }

    private void ReleaseUnmanagedResources()
    {
        _lineIndexBuffer.Dispose();
        _lineVertexBuffer.Dispose();
        _lineInstanceBuffer.Dispose();
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