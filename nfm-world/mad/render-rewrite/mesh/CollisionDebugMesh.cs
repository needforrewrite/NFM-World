using CommunityToolkit.HighPerformance;
using Microsoft.Xna.Framework.Graphics;
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
        void AddLine(Vector3 p0, Vector3 p1, Color3 color)
        {
            const float halfThickness = 2f;
            // Create two quads for each line segment to give it some thickness
            
            Span<Vector3> verts = stackalloc Vector3[LineMeshHelpers.VerticesPerLine];
            Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

            LineMeshHelpers.CreateLineMesh(p0, p1, data.Count, halfThickness, in verts, in inds);
            indices.AddRange(inds);
            foreach (var vert in verts)
            {
                data.Add(new VertexPositionColor(vert.ToXna(), color.ToXna()));
            }
        }
        
        void DrawPolygon3D(Span<float> px, Span<float> py, Span<float> pz, int pn, Color3 color)
        {
            for (var i = 0; i < pn; i++)
            {
                var p0 = new Vector3(px[i], py[i], pz[i]);
                var p1 = new Vector3(px[(i + 1) % pn], py[(i + 1) % pn], pz[(i + 1) % pn]);
                AddLine(p0, p1, color);
            }
        }
        
        for (var i = 0; i < boxes.Length; i++)
        {
            var color = boxes[i].Radius.Y <= 1 ? new Color3(255, 0, 0) : new Color3(255, 255, 255);
            DrawPolygon3D(
                [lbx[i,0], lbx[i,1], lbx[i,2], lbx[i,3]],
                [lby[i,0], lby[i,1], lby[i,2], lby[i,3]],
                [lbz[i,0], lbz[i,1], lbz[i,2], lbz[i,3]],
                4,
                color
            ); // draw left box
            DrawPolygon3D(
                [rbx[i,0], rbx[i,1], rbx[i,2], rbx[i,3]],
                [rby[i,0], rby[i,1], rby[i,2], rby[i,3]],
                [rbz[i,0], rbz[i,1], rbz[i,2], rbz[i,3]],
                4,
                color
            ); // draw right box
            DrawPolygon3D(
                [fbx[i,0], fbx[i,1], fbx[i,2], fbx[i,3]],
                [fby[i,0], fby[i,1], fby[i,2], fby[i,3]],
                [fbz[i,0], fbz[i,1], fbz[i,2], fbz[i,3]],
                4,
                color
            ); // draw front box
            DrawPolygon3D(
                [bbx[i,0], bbx[i,1], bbx[i,2], bbx[i,3]],
                [bby[i,0], bby[i,1], bby[i,2], bby[i,3]],
                [bbz[i,0], bbz[i,1], bbz[i,2], bbz[i,3]],
                4,
                color
            ); // draw back box
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