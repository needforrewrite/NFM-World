using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class CollisionDebugMesh : Transform
{
    private BasicEffect lineEffect;
    private int lineTriangleCount;
    private IndexBuffer? lineIndexBuffer;
    private VertexBuffer? lineVertexBuffer;
    private readonly int lineVertexCount;

    public CollisionDebugMesh(Span<Rad3dBoxDef> boxes)
    {
        #region Debug boxes
        
        // disp 0
        const int linesPerPolygon = 16;
        
        var data = new List<VertexPositionColor>(LineMeshHelpers.VerticesPerLine * linesPerPolygon * boxes.Length);
        var indices = new List<int>(LineMeshHelpers.IndicesPerLine * linesPerPolygon * boxes.Length);
        void AddLine(Vector3 p0, Vector3 p1, Color3 color, float mult = 1)
        {
            const float halfThickness = 2f;
            // Create two quads for each line segment to give it some thickness
            
            Span<Vector3> verts = stackalloc Vector3[LineMeshHelpers.VerticesPerLine];
            Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

            // LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, halfThickness * mult, in verts, in inds); TODO
            indices.AddRange(inds);
            foreach (var vert in verts)
            {
                data.Add(new VertexPositionColor(vert, color));
            }
        }
        
        for (var i = 0; i < boxes.Length; i++)
        {
            var box = boxes[i];
            var center = box.Translation;
            var radius = box.Radius;

            // Define the 8 corners of the box
            Span<Vector3> corners = new Vector3[8]
            {
                new(center.X - radius.X, center.Y - radius.Y, center.Z - radius.Z), // 0: left-bottom-back
                new(center.X + radius.X, center.Y - radius.Y, center.Z - radius.Z), // 1: right-bottom-back
                new(center.X + radius.X, center.Y + radius.Y, center.Z - radius.Z), // 2: right-top-back
                new(center.X - radius.X, center.Y + radius.Y, center.Z - radius.Z), // 3: left-top-back
                new(center.X - radius.X, center.Y - radius.Y, center.Z + radius.Z), // 4: left-bottom-front
                new(center.X + radius.X, center.Y - radius.Y, center.Z + radius.Z), // 5: right-bottom-front
                new(center.X + radius.X, center.Y + radius.Y, center.Z + radius.Z), // 6: right-top-front
                new(center.X - radius.X, center.Y + radius.Y, center.Z + radius.Z)  // 7: left-top-front
            };

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

        lineVertexBuffer = new VertexBuffer(GameSparker._graphicsDevice, VertexPositionColor.VertexDeclaration, data.Count, BufferUsage.None);
        lineVertexBuffer.SetData(data.ToArray());
	    
        lineIndexBuffer = new IndexBuffer(GameSparker._graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        lineIndexBuffer.SetData(indices.ToArray());
	    
        lineTriangleCount = indices.Count / 3;
        lineVertexCount = data.Count;
        
        lineEffect = new BasicEffect(GameSparker._graphicsDevice)
        {
            VertexColorEnabled = true
        };
        
        #endregion
    }

    public void Render(Camera camera)
    {
        GameSparker._graphicsDevice.SetVertexBuffer(lineVertexBuffer);
        GameSparker._graphicsDevice.Indices = lineIndexBuffer;

        lineEffect.World = MatrixWorld;
        lineEffect.View = camera.ViewMatrix;
        lineEffect.Projection = camera.ProjectionMatrix;
        foreach (var pass in lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            GameSparker._graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, lineVertexCount, 0, lineTriangleCount);
        }
    }
}