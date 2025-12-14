using Microsoft.Xna.Framework.Graphics;
using Vector3 = Stride.Core.Mathematics.Vector3;

namespace NFMWorld.Mad;
using URandom = NFMWorld.Util.Random;

public class NoZBufferMesh : Mesh
{
    public NoZBufferMesh(GraphicsDevice graphicsDevice, string code) : base(graphicsDevice, code)
    {
    }

    public NoZBufferMesh(GraphicsDevice graphicsDevice, Rad3d rad) : base(graphicsDevice, rad)
    {
    }

    public NoZBufferMesh(Mesh baseMesh, Vector3 position, Euler rotation) : base(baseMesh, position, rotation)
    {
    }

    public override void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        GraphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        base.Render(camera, lightCamera, isCreateShadowMap);
        GraphicsDevice.DepthStencilState = DepthStencilState.Default;
    }
}

public class Environment
{
    public static GroundPolys MakePolys(
        int sx, int ncx, int sz, int ncz, int stagePartCount, // newpolys
        GraphicsDevice graphicsDevice
    )
    {
        #region setgrnd
        
        int[] cpol = [215, 210, 210];
        int[] ogrnd = [205, 200, 200];
        int[] texture = [0, 0, 0, 50];
        int[] crgrnd = [205, 200, 200];
        
        for (int i_259 = 0; i_259 < 3; i_259++) {
            cpol[i_259] = (World.GroundColor[i_259] * texture[3] + texture[i_259]) / (1 + texture[3]);
            cpol[i_259] = (int)(cpol[i_259] + cpol[i_259] * (World.Snap[i_259] / 100.0F));
            if (cpol[i_259] > 255) {
                cpol[i_259] = 255;
            }

            if (cpol[i_259] < 0) {
                cpol[i_259] = 0;
            }
        }
        
        var cgrnd = World.GroundColor.Snap(World.Snap);
        for (int i_260 = 0; i_260 < 3; i_260++) {
            crgrnd[i_260] = (int)((cpol[i_260] * 0.99 + cgrnd[i_260]) / 2.0);
        }
        #endregion
        
        #region setexture

        var t1 = World.Texture[0];
        var t2 = World.Texture[1];
        var t3 = World.Texture[2];
        var t4 = World.Texture[3];

        if (World.HasTexture) {
            if (t4 < 20)
            {
                t4 = 20;
            }
            if (t4 > 60)
            {
                t4 = 60;
            }
            texture[0] = t1;
            texture[1] = t2;
            texture[2] = t3;
            texture[3] = t4;
            t1 = (ogrnd[0] * t4 + t1) / (1 + t4);
            t2 = (ogrnd[1] * t4 + t2) / (1 + t4);
            t3 = (ogrnd[2] * t4 + t3) / (1 + t4);
            cpol[0] = (int) (t1 + t1 * (World.Snap[0] / 100.0F));
            if (cpol[0] > 255)
            {
                cpol[0] = 255;
            }
            if (cpol[0] < 0)
            {
                cpol[0] = 0;
            }
            cpol[1] = (int) (t2 + t2 * (World.Snap[1] / 100.0F));
            if (cpol[1] > 255)
            {
                cpol[1] = 255;
            }
            if (cpol[1] < 0)
            {
                cpol[1] = 0;
            }
            cpol[2] = (int) (t3 + t3 * (World.Snap[2] / 100.0F));
            if (cpol[2] > 255)
            {
                cpol[2] = 255;
            }
            if (cpol[2] < 0)
            {
                cpol[2] = 0;
            }
            for (var i264 = 0; i264 < 3; i264++)
            {
                crgrnd[i264] = (int) ((cpol[i264] * 0.99 + cgrnd[i264]) / 2.0);
            }
        }

        #endregion
        
        #region newpolys
        
        var nrw = ncx / 1200 + 9;
        var ncl = ncz / 1200 + 9;
        var sgpx = sx - 4800;
        var sgpz = sz - 4800;

        var ogpx = new int[nrw * ncl, 8];
        var ogpz = new int[nrw * ncl, 8];
        var pvr = new float[nrw * ncl, 8];
        var cgpx = new int[nrw * ncl];
        var cgpz = new int[nrw * ncl];
        var pmx = new int[nrw * ncl];
        var pcv = new float[nrw * ncl];
        {
            var random = new URandom((stagePartCount + World.GroundColor[0] + World.GroundColor[1] + World.GroundColor[2]) * 1671);
    
            var i39 = 0;
            var i40 = 0;
            for (var i41 = 0; i41 < nrw * ncl; i41++)
            {
                cgpx[i41] = sgpx + i39 * 1200 + (int)(random.NextDouble() * 1000.0 - 500.0);
                cgpz[i41] = sgpz + i40 * 1200 + (int)(random.NextDouble() * 1000.0 - 500.0);
                for (var i42 = 0; i42 < Trackers.Nt; i42++)
                {
                    if (Trackers.Zy[i42] == 0 && Trackers.Xy[i42] == 0)
                    {
                        if (Trackers.Radx[i42] < Trackers.Radz[i42] &&
                            Math.Abs(cgpz[i41] - Trackers.Z[i42]) < Trackers.Radz[i42])
                        {
                            for (;
                                 Math.Abs(cgpx[i41] - Trackers.X[i42]) < Trackers.Radx[i42];
                                 cgpx[i41] +=
                                     (int)(random.NextDouble() * Trackers.Radx[i42] * 2.0 - Trackers.Radx[i42]))
                            {
                            }
                        }

                        if (Trackers.Radz[i42] < Trackers.Radx[i42] &&
                            Math.Abs(cgpx[i41] - Trackers.X[i42]) < Trackers.Radx[i42])
                        {
                            for (;
                                 Math.Abs(cgpz[i41] - Trackers.Z[i42]) < Trackers.Radz[i42];
                                 cgpz[i41] +=
                                     (int)(random.NextDouble() * Trackers.Radz[i42] * 2.0 - Trackers.Radz[i42]))
                            {
                            }
                        }
                    }
                }

                if (++i39 == nrw)
                {
                    i39 = 0;
                    i40++;
                }
            }

            for (var i43 = 0; i43 < nrw * ncl; i43++)
            {
                var f = (float)(0.3 + 1.6 * random.NextDouble());
                ogpx[i43, 0] = 0;
                ogpz[i43, 0] = (int)((100.0 + random.NextDouble() * 760.0) * f);
                ogpx[i43, 1] = (int)((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
                ogpz[i43, 1] = ogpx[i43, 1];
                ogpx[i43, 2] = (int)((100.0 + random.NextDouble() * 760.0) * f);
                ogpz[i43, 2] = 0;
                ogpx[i43, 3] = (int)((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
                ogpz[i43, 3] = -ogpx[i43, 3];
                ogpx[i43, 4] = 0;
                ogpz[i43, 4] = -(int)((100.0 + random.NextDouble() * 760.0) * f);
                ogpx[i43, 5] = -(int)((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
                ogpz[i43, 5] = ogpx[i43, 5];
                ogpx[i43, 6] = -(int)((100.0 + random.NextDouble() * 760.0) * f);
                ogpz[i43, 6] = 0;
                ogpx[i43, 7] = -(int)((100.0 + random.NextDouble() * 760.0) * 0.7071 * f);
                ogpz[i43, 7] = -ogpx[i43, 7];
                for (var i44 = 0; i44 < 8; i44++)
                {
                    var i45 = i44 - 1;
                    if (i45 == -1)
                    {
                        i45 = 7;
                    }

                    var i46 = i44 + 1;
                    if (i46 == 8)
                    {
                        i46 = 0;
                    }

                    ogpx[i43, i44] = ((ogpx[i43, i45] + ogpx[i43, i46]) / 2 + ogpx[i43, i44]) / 2;
                    ogpz[i43, i44] = ((ogpz[i43, i45] + ogpz[i43, i46]) / 2 + ogpz[i43, i44]) / 2;
                    pvr[i43, i44] = (float)(1.1 + random.NextDouble() * 0.8);
                    var i47 = (int)Math.Sqrt(
                        (int)(ogpx[i43, i44] * ogpx[i43, i44] * pvr[i43, i44] * pvr[i43, i44] +
                              ogpz[i43, i44] * ogpz[i43, i44] * pvr[i43, i44] * pvr[i43, i44]));
                    if (i47 > pmx[i43])
                    {
                        pmx[i43] = i47;
                    }
                }

                pcv[i43] = (float)(0.97 + random.NextDouble() * 0.03);
                if (pcv[i43] > 1.0F)
                {
                    pcv[i43] = 1.0F;
                }

                if (random.NextDouble() > random.NextDouble())
                {
                    pcv[i43] = 1.0F;
                }
            }
        }

        #endregion

        var verts = new List<Rad3dPoly>();
        var points = new List<Vector3>();

        #region writePolys1

        for (int i = 0; i < nrw * ncl; i++) {
            points.Clear();
            
            int pr = (int)((cpol[0] * pcv[i] + cgrnd[0]) / 2.0F);
            int pg = (int)((cpol[1] * pcv[i] + cgrnd[1]) / 2.0F);
            int pb = (int)((cpol[2] * pcv[i] + cgrnd[2]) / 2.0F);
            var color = new Color3((short)pr, (short)pg, (short)pb);

            for (int j = 0; j < 8; j++) {
                int px = (int)(ogpx[i,j] * pvr[i,j] + cgpx[i]);
                int pz = (int)(ogpz[i,j] * pvr[i,j] + cgpz[i]);
                int py = 250;
                points.Add(new Vector3(px, py, pz));
            }

            var poly = new Rad3dPoly(color, null, PolyType.Flat, null, points.ToArray());
            verts.Add(poly);
        }
        
        #endregion

        #region writePolys2

        
        for (int i = 0; i < nrw * ncl; i++) {
            points.Clear();

            int pr = (int)(cpol[0] * pcv[i]);
            int pg = (int)(cpol[1] * pcv[i]);
            int pb = (int)(cpol[2] * pcv[i]);
            var color = new Color3((short)pr, (short)pg, (short)pb);

            for (int j = 0; j < 8; j++) {
                int px = ogpx[i,j] + cgpx[i];
                int pz = ogpz[i,j] + cgpz[i];
                int py = 250;
                points.Add(new Vector3(px, py, pz));
            }

            var poly = new Rad3dPoly(color, null, PolyType.Flat, null, points.ToArray());
            verts.Add(poly);
        }

        #endregion

        return new GroundPolys(graphicsDevice, verts.ToArray());
    }
}