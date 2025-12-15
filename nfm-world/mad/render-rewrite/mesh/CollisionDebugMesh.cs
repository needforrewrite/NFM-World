using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class CollisionDebugMesh : Transform
{
    private BasicEffect lineEffect;
    private int lineTriangleCount;
    private IndexBuffer? lineIndexBuffer;
    private VertexBuffer? lineVertexBuffer;

    public CollisionDebugMesh(Span<Rad3dBoxDef> boxes)
    {
        #region Debug boxes
        
        Span<float> albx = stackalloc float[boxes.Length * 4];
        Span<float> alby = stackalloc float[boxes.Length * 4];
        Span<float> albz = stackalloc float[boxes.Length * 4];
        Span<float> arbx = stackalloc float[boxes.Length * 4];
        Span<float> arby = stackalloc float[boxes.Length * 4];
        Span<float> arbz = stackalloc float[boxes.Length * 4];
        Span<float> afbx = stackalloc float[boxes.Length * 4];
        Span<float> afby = stackalloc float[boxes.Length * 4];
        Span<float> afbz = stackalloc float[boxes.Length * 4];
        Span<float> abbx = stackalloc float[boxes.Length * 4];
        Span<float> abby = stackalloc float[boxes.Length * 4];
        Span<float> abbz = stackalloc float[boxes.Length * 4];
        
        var lbx = Span2D<float>.Create(albx, boxes.Length, 4);
        var lby = Span2D<float>.Create(alby, boxes.Length, 4);
        var lbz = Span2D<float>.Create(albz, boxes.Length, 4);
        var rbx = Span2D<float>.Create(arbx, boxes.Length, 4);
        var rby = Span2D<float>.Create(arby, boxes.Length, 4);
        var rbz = Span2D<float>.Create(arbz, boxes.Length, 4);
        var fbx = Span2D<float>.Create(afbx, boxes.Length, 4);
        var fby = Span2D<float>.Create(afby, boxes.Length, 4);
        var fbz = Span2D<float>.Create(afbz, boxes.Length, 4);
        var bbx = Span2D<float>.Create(abbx, boxes.Length, 4);
        var bby = Span2D<float>.Create(abby, boxes.Length, 4);
        var bbz = Span2D<float>.Create(abbz, boxes.Length, 4);
        
        for(var i = 0; i < boxes.Length; i++)
        {
            // right box
            rbx[i,0] = boxes[i].Translation.X + boxes[i].Radius.X;
            rby[i,0] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            rbz[i,0] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            rbx[i,1] = boxes[i].Translation.X + boxes[i].Radius.X;
            rby[i,1] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            rbz[i,1] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            rbx[i,2] = boxes[i].Translation.X + boxes[i].Radius.X;
            rby[i,2] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            rbz[i,2] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            rbx[i,3] = boxes[i].Translation.X + boxes[i].Radius.X;
            rby[i,3] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            rbz[i,3] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            // left box
            lbx[i,0] = boxes[i].Translation.X - boxes[i].Radius.X;
            lby[i,0] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            lbz[i,0] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            lbx[i,1] = boxes[i].Translation.X - boxes[i].Radius.X;
            lby[i,1] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            lbz[i,1] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            lbx[i,2] = boxes[i].Translation.X - boxes[i].Radius.X;
            lby[i,2] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            lbz[i,2] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            lbx[i,3] = boxes[i].Translation.X - boxes[i].Radius.X;
            lby[i,3] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            lbz[i,3] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            // front box
            fbx[i,0] = boxes[i].Translation.X - boxes[i].Radius.X;
            fby[i,0] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            fbz[i,0] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            fbx[i,1] = boxes[i].Translation.X + boxes[i].Radius.X;
            fby[i,1] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            fbz[i,1] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            fbx[i,2] = boxes[i].Translation.X + boxes[i].Radius.X;
            fby[i,2] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            fbz[i,2] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            fbx[i,3] = boxes[i].Translation.X - boxes[i].Radius.X;
            fby[i,3] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            fbz[i,3] = boxes[i].Translation.Z + boxes[i].Radius.Z;
            // back box
            bbx[i,0] = boxes[i].Translation.X - boxes[i].Radius.X;
            bby[i,0] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            bbz[i,0] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            bbx[i,1] = boxes[i].Translation.X + boxes[i].Radius.X;
            bby[i,1] = boxes[i].Translation.Y + boxes[i].Radius.Y;
            bbz[i,1] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            bbx[i,2] = boxes[i].Translation.X + boxes[i].Radius.X;
            bby[i,2] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            bbz[i,2] = boxes[i].Translation.Z - boxes[i].Radius.Z;
            bbx[i,3] = boxes[i].Translation.X - boxes[i].Radius.X;
            bby[i,3] = boxes[i].Translation.Y - boxes[i].Radius.Y;
            bbz[i,3] = boxes[i].Translation.Z - boxes[i].Radius.Z;
        }
        
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

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, halfThickness * mult, in verts, in inds);
            indices.AddRange(inds);
            foreach (var vert in verts)
            {
                data.Add(new VertexPositionColor(vert.ToXna(), color.ToXna()));
            }
        }
        
        void DrawPolygon3D(Span<float> px, Span<float> py, Span<float> pz, int pn, Color3 color, float mult = 1)
        {
            for (var i = 0; i < pn; i++)
            {
                var p0 = new Vector3(px[i], py[i], pz[i]);
                var p1 = new Vector3(px[(i + 1) % pn], py[(i + 1) % pn], pz[(i + 1) % pn]);
                AddLine(p0, p1, color, mult);
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

            var normalColor = box.Radius.Y <= 1 ? new Color3(255, 0, 0) : new Color3(255, 255, 255);
            var solidSideColor = new Color3(0, 255, 0);
            var flatColor = new Color3(0, 0, 255);

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
                
                // Check which face(s) this edge belongs to
                bool isLeft = p0.X < center.X && p1.X < center.X;
                bool isRight = p0.X > center.X && p1.X > center.X;
                bool isFront = p0.Z > center.Z && p1.Z > center.Z;
                bool isBack = p0.Z < center.Z && p1.Z < center.Z;

                if (isLeft && leftSolid) edgeColor = solidSideColor;
                else if (isRight && rightSolid) edgeColor = solidSideColor;
                else if (isFront && frontSolid) edgeColor = solidSideColor;
                else if (isBack && backSolid) edgeColor = solidSideColor;

                AddLine(p0, p1, edgeColor, edgeColor == solidSideColor ? 2f : 1f);

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
                    var rotatedP0 = Vector3.TransformCoordinate(translatedP0, rotationMatrix) + center;
                    var rotatedP1 = Vector3.TransformCoordinate(translatedP1, rotationMatrix) + center;

                    AddLine(rotatedP0, rotatedP1, flatColor, 2f);
                }
            }
        }

        lineVertexBuffer = new VertexBuffer(GameSparker._graphicsDevice, VertexPositionColor.VertexDeclaration, data.Count, BufferUsage.None);
        lineVertexBuffer.SetData(data.ToArray());
	    
        lineIndexBuffer = new IndexBuffer(GameSparker._graphicsDevice, IndexElementSize.ThirtyTwoBits, indices.Count, BufferUsage.None);
        lineIndexBuffer.SetData(indices.ToArray());
	    
        lineTriangleCount = indices.Count / 3;
        
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
    
            GameSparker._graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, lineTriangleCount);
        }
    }
}