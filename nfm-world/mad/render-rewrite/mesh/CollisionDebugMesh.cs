using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public sealed class CollisionDebugMesh : GameObject
{
    private LineEffect _material;
    private int lineTriangleCount;
    private IndexBuffer lineIndexBuffer;
    private VertexBuffer lineVertexBuffer;
    private readonly int lineVertexCount;
    private VertexBuffer lineInstanceBuffer;

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

        lineVertexBuffer = new VertexBuffer(GameSparker._graphicsDevice, LineMesh.LineMeshVertexAttribute.VertexDeclaration, data.Count, BufferUsage.None);
        lineVertexBuffer.SetDataEXT(data);
	    
        lineIndexBuffer = new IndexBuffer(GameSparker._graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        lineIndexBuffer.SetDataEXT(indices);
	    
        lineTriangleCount = indices.Count / 3;
        lineVertexCount = data.Count;
        
        lineInstanceBuffer = new DynamicVertexBuffer(GameSparker._graphicsDevice, InstanceData.InstanceDeclaration, 1, BufferUsage.WriteOnly);
        lineInstanceBuffer.SetDataEXT((ReadOnlySpan<InstanceData>)[new InstanceData(MatrixWorld)]);

        _material = new LineEffect(Program._lineShader);

        #endregion
    }

    public override void Render(Camera camera, Lighting? lighting)
    {
        if (lighting?.IsCreateShadowMap == true || !GameSparker.devRenderTrackers) return;
        lineInstanceBuffer.SetDataEXT((ReadOnlySpan<InstanceData>)[new InstanceData(MatrixWorld)]);

        GameSparker._graphicsDevice.SetVertexBuffers(lineVertexBuffer, new VertexBufferBinding(lineInstanceBuffer, 0, 1));
        GameSparker._graphicsDevice.Indices = lineIndexBuffer;

        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.SnapColor?.SetValue(new Color3(100, 100, 100).ToVector3());
        _material.IsFullbright?.SetValue(true);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Vector3(0, 0, 0));
        _material.ChargedBlinkAmount?.SetValue(0.0f);
        _material.HalfThickness?.SetValue(World.OutlineThickness);

        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(false);
        _material.Alpha?.SetValue(1f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.ViewProj?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position);

        _material.CurrentTechnique = _material.Techniques["Basic"];

        _material.Expand?.SetValue(false);
        _material.Darken?.SetValue(1.0f);
        _material.RandomFloat?.SetValue(URandom.Single());

        _material.Glow?.SetValue(false);
        
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            GameSparker._graphicsDevice.DrawInstancedPrimitives(PrimitiveType.TriangleList, 0, 0, lineVertexCount, 0, lineTriangleCount, 1);
        }
    }
}