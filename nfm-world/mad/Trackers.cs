using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

internal class Trackers
{
    private static BasicEffect lineEffect;
    private static int lineTriangleCount;
    private static IndexBuffer? lineIndexBuffer;
    private static VertexBuffer? lineVertexBuffer;

    //private Trackers() {}
    internal static readonly int[][] C = ArrayExt.New<int>(75000, 3);

    internal static readonly UnlimitedArray<int> Dam = [];
    internal static readonly UnlimitedArray<bool> Decor = [];
    internal static int Ncx;
    internal static int Ncz;
    internal static readonly UnlimitedArray<bool> Notwall = [];
    internal static int Nt = 0;
    internal static readonly UnlimitedArray<int> Radx = [];
    internal static readonly UnlimitedArray<int> Rady = [];
    internal static readonly UnlimitedArray<int> Radz = [];
    internal static readonly UnlimitedArray<int> Skd = [];
    internal static int Sx;
    internal static int Sz;
    internal static readonly UnlimitedArray<int> X = [];
    internal static readonly UnlimitedArray<int> Xy = [];
    internal static readonly UnlimitedArray<int> Y = [];
    internal static readonly UnlimitedArray<int> Z = [];
    internal static readonly UnlimitedArray<int> Zy = [];

    internal static void LoadTrackers(IReadOnlyList<Mesh> elements, int sx, int ncx, int sz, int ncz)
    {
        foreach (var element in elements)
        {
            var xz = (int)element.Rotation.Xz.Degrees;
            for (var i = 0; i < element.Boxes.Length; i++)
            {
                Xy[Nt] = (int) (element.Boxes[i].Xy * UMath.Cos(xz) - element.Boxes[i].Zy * UMath.Sin(xz));
                Zy[Nt] = (int) (element.Boxes[i].Zy * UMath.Cos(xz) + element.Boxes[i].Xy * UMath.Sin(xz));
                for (var c = 0; c < 3; c++)
                {
                    C[Nt][c] = (int) (element.Boxes[i].Color[c] + element.Boxes[i].Color[c] * (World.Snap[c] / 100.0F));
                    if (C[Nt][c] > 255)
                    {
                        C[Nt][c] = 255;
                    }
                    if (C[Nt][c] < 0)
                    {
                        C[Nt][c] = 0;
                    }
                }
                X[Nt] = (int) (element.Position.X + element.Boxes[i].Translation.X * UMath.Cos(xz) - element.Boxes[i].Translation.Z * UMath.Sin(xz));
                Z[Nt] = (int) (element.Position.Z + element.Boxes[i].Translation.Z * UMath.Cos(xz) + element.Boxes[i].Translation.X * UMath.Sin(xz));
                Y[Nt] = (int)(element.Position.Y + element.Boxes[i].Translation.Y);
                Skd[Nt] = element.Boxes[i].Skid;
                Dam[Nt] = element.Boxes[i].Damage;
                Notwall[Nt] = element.Boxes[i].NotWall;
                Decor[Nt] = false;
                var i86 = Math.Abs(xz);
                if (i86 == 180)
                {
                    i86 = 0;
                }
                Radx[Nt] = (int) Math.Abs(element.Boxes[i].Radius.X * UMath.Cos(i86) + element.Boxes[i].Radius.Z * UMath.Sin(i86));
                Radz[Nt] = (int) Math.Abs(element.Boxes[i].Radius.X * UMath.Sin(i86) + element.Boxes[i].Radius.Z * UMath.Cos(i86));
                Rady[Nt] = (int) element.Boxes[i].Radius.Y;
                Nt++;
            }
        }
        
        Console.WriteLine(Nt);
        
        Sx = sx;
        Sz = sz;
        Ncx = ncx / 3000;
        if (Ncx <= 0)
        {
            Ncx = 1;
        }
        Ncz = ncz / 3000;
        if (Ncz <= 0)
        {
            Ncz = 1;
        }
        
        // maxine: remove trackers.sect which was assigned here. it's not used anymore.
        
        for (var i = 0; i < Nt; i++)
        {
            if (Dam[i] == 167)
            {
                Dam[i] = 1;
            }
        }

        Ncx--;
        Ncz--;

        #region Debug boxes
        
        var lbx = new int[Nt,4];
        var lby = new int[Nt,4];
        var lbz = new int[Nt,4];
        var rbx = new int[Nt,4];
        var rby = new int[Nt,4];
        var rbz = new int[Nt,4];
        var fbx = new int[Nt,4];
        var fby = new int[Nt,4];
        var fbz = new int[Nt,4];
        var bbx = new int[Nt,4];
        var bby = new int[Nt,4];
        var bbz = new int[Nt,4];
        for(int i = 0; i < Nt; i++)
        {
            // right box
            rbx[i,0] = X[i] + Radx[i];
            rby[i,0] = Y[i] + Rady[i];
            rbz[i,0] = Z[i] + Radz[i];
            rbx[i,1] = X[i] + Radx[i];
            rby[i,1] = Y[i] + Rady[i];
            rbz[i,1] = Z[i] - Radz[i];
            rbx[i,2] = X[i] + Radx[i];
            rby[i,2] = Y[i] - Rady[i];
            rbz[i,2] = Z[i] - Radz[i];
            rbx[i,3] = X[i] + Radx[i];
            rby[i,3] = Y[i] - Rady[i];
            rbz[i,3] = Z[i] + Radz[i];
            // left box
            lbx[i,0] = X[i] - Radx[i];
            lby[i,0] = Y[i] + Rady[i];
            lbz[i,0] = Z[i] + Radz[i];
            lbx[i,1] = X[i] - Radx[i];
            lby[i,1] = Y[i] + Rady[i];
            lbz[i,1] = Z[i] - Radz[i];
            lbx[i,2] = X[i] - Radx[i];
            lby[i,2] = Y[i] - Rady[i];
            lbz[i,2] = Z[i] - Radz[i];
            lbx[i,3] = X[i] - Radx[i];
            lby[i,3] = Y[i] - Rady[i];
            lbz[i,3] = Z[i] + Radz[i];
            // front box
            fbx[i,0] = X[i] - Radx[i];
            fby[i,0] = Y[i] + Rady[i];
            fbz[i,0] = Z[i] + Radz[i];
            fbx[i,1] = X[i] + Radx[i];
            fby[i,1] = Y[i] + Rady[i];
            fbz[i,1] = Z[i] + Radz[i];
            fbx[i,2] = X[i] + Radx[i];
            fby[i,2] = Y[i] - Rady[i];
            fbz[i,2] = Z[i] + Radz[i];
            fbx[i,3] = X[i] - Radx[i];
            fby[i,3] = Y[i] - Rady[i];
            fbz[i,3] = Z[i] + Radz[i];
            // back box
            bbx[i,0] = X[i] - Radx[i];
            bby[i,0] = Y[i] + Rady[i];
            bbz[i,0] = Z[i] - Radz[i];
            bbx[i,1] = X[i] + Radx[i];
            bby[i,1] = Y[i] + Rady[i];
            bbz[i,1] = Z[i] - Radz[i];
            bbx[i,2] = X[i] + Radx[i];
            bby[i,2] = Y[i] - Rady[i];
            bbz[i,2] = Z[i] - Radz[i];
            bbx[i,3] = X[i] - Radx[i];
            bby[i,3] = Y[i] - Rady[i];
            bbz[i,3] = Z[i] - Radz[i];
        }
        
        // disp 0
        const int linesPerPolygon = 16;
        
        var data = new List<VertexPositionColor>(LineMeshHelpers.VerticesPerLine * linesPerPolygon * Nt);
        var indices = new List<int>(LineMeshHelpers.IndicesPerLine * linesPerPolygon * Nt);
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
        
        void drawPolygon3D(Span<int> px, Span<int> py, Span<int> pz, int pn, Color3 color)
        {
            for (int i = 0; i < pn; i++)
            {
                var p0 = new Vector3(px[i], py[i], pz[i]);
                var p1 = new Vector3(px[(i + 1) % pn], py[(i + 1) % pn], pz[(i + 1) % pn]);
                AddLine(p0, p1, color);
            }
        }
        
        for (int i = 0; i < Nt; i++)
        {
            var color = Rady[i] <= 1 ? new Color3(255, 0, 0) : new Color3(255, 255, 255);
            drawPolygon3D(
                [lbx[i,0], lbx[i,1], lbx[i,2], lbx[i,3]],
                [lby[i,0], lby[i,1], lby[i,2], lby[i,3]],
                [lbz[i,0], lbz[i,1], lbz[i,2], lbz[i,3]],
                4,
                color
            ); // draw left box
            drawPolygon3D(
                [rbx[i,0], rbx[i,1], rbx[i,2], rbx[i,3]],
                [rby[i,0], rby[i,1], rby[i,2], rby[i,3]],
                [rbz[i,0], rbz[i,1], rbz[i,2], rbz[i,3]],
                4,
                color
            ); // draw right box
            drawPolygon3D(
                [fbx[i,0], fbx[i,1], fbx[i,2], fbx[i,3]],
                [fby[i,0], fby[i,1], fby[i,2], fby[i,3]],
                [fbz[i,0], fbz[i,1], fbz[i,2], fbz[i,3]],
                4,
                color
            ); // draw front box
            drawPolygon3D(
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

    public static void RenderDebugTrackers(Camera camera)
    {
        GameSparker._graphicsDevice.SetVertexBuffer(lineVertexBuffer);
        GameSparker._graphicsDevice.Indices = lineIndexBuffer;
        
        lineEffect.View = camera.ViewMatrix;
        lineEffect.Projection = camera.ProjectionMatrix;
        foreach (var pass in lineEffect.CurrentTechnique.Passes)
        {
            pass.Apply();
    
            GameSparker._graphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, lineTriangleCount);
        }
    }
}