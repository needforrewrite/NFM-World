using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
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

        for (int i_259 = 0; i_259 < 3; i_259++)
        {
            cpol[i_259] = (World.GroundColor[i_259] * texture[3] + texture[i_259]) / (1 + texture[3]);
            cpol[i_259] = (int)(cpol[i_259] + cpol[i_259] * (World.Snap[i_259] / 100.0F));
            if (cpol[i_259] > 255)
            {
                cpol[i_259] = 255;
            }

            if (cpol[i_259] < 0)
            {
                cpol[i_259] = 0;
            }
        }

        var cgrnd = World.GroundColor.Snap(World.Snap);
        for (int i_260 = 0; i_260 < 3; i_260++)
        {
            crgrnd[i_260] = (int)((cpol[i_260] * 0.99 + cgrnd[i_260]) / 2.0);
        }

        #endregion

        #region setexture

        var t1 = World.Texture[0];
        var t2 = World.Texture[1];
        var t3 = World.Texture[2];
        var t4 = World.Texture[3];

        if (World.HasTexture)
        {
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
            cpol[0] = (int)(t1 + t1 * (World.Snap[0] / 100.0F));
            if (cpol[0] > 255)
            {
                cpol[0] = 255;
            }

            if (cpol[0] < 0)
            {
                cpol[0] = 0;
            }

            cpol[1] = (int)(t2 + t2 * (World.Snap[1] / 100.0F));
            if (cpol[1] > 255)
            {
                cpol[1] = 255;
            }

            if (cpol[1] < 0)
            {
                cpol[1] = 0;
            }

            cpol[2] = (int)(t3 + t3 * (World.Snap[2] / 100.0F));
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
                crgrnd[i264] = (int)((cpol[i264] * 0.99 + cgrnd[i264]) / 2.0);
            }
        }

        #endregion

        #region setpolys

        if (World.HasPolys)
        {
            int r = World.GroundPolysColor[0];
            int g = World.GroundPolysColor[1];
            int b = World.GroundPolysColor[2];
            
            cpol[0] = (int)(r + r * (World.Snap[0] / 100.0F));
            if (cpol[0] > 255) {
                cpol[0] = 255;
            }

            if (cpol[0] < 0) {
                cpol[0] = 0;
            }

            cpol[1] = (int)(g + g * (World.Snap[1] / 100.0F));
            if (cpol[1] > 255) {
                cpol[1] = 255;
            }

            if (cpol[1] < 0) {
                cpol[1] = 0;
            }

            cpol[2] = (int)(b + b * (World.Snap[2] / 100.0F));
            if (cpol[2] > 255) {
                cpol[2] = 255;
            }

            if (cpol[2] < 0) {
                cpol[2] = 0;
            }

            for (int i_267 = 0; i_267 < 3; i_267++) {
                crgrnd[i_267] = (int)((cpol[i_267] * 0.99 + cgrnd[i_267]) / 2.0);
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
            var random =
                new URandom(
                    (stagePartCount + World.GroundColor[0] + World.GroundColor[1] + World.GroundColor[2]) * 1671);

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

        for (int i = 0; i < nrw * ncl; i++)
        {
            points.Clear();

            int pr = (int)((cpol[0] * pcv[i] + cgrnd[0]) / 2.0F);
            int pg = (int)((cpol[1] * pcv[i] + cgrnd[1]) / 2.0F);
            int pb = (int)((cpol[2] * pcv[i] + cgrnd[2]) / 2.0F);
            var color = new Color3((short)pr, (short)pg, (short)pb);

            for (int j = 0; j < 8; j++)
            {
                int px = (int)(ogpx[i, j] * pvr[i, j] + cgpx[i]);
                int pz = (int)(ogpz[i, j] * pvr[i, j] + cgpz[i]);
                int py = 250;
                points.Add(new Vector3(px, py, pz));
            }

            var poly = new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray());
            verts.Add(poly);
        }

        #endregion

        #region writePolys2

        for (int i = 0; i < nrw * ncl; i++)
        {
            points.Clear();

            int pr = (int)(cpol[0] * pcv[i]);
            int pg = (int)(cpol[1] * pcv[i]);
            int pb = (int)(cpol[2] * pcv[i]);
            var color = new Color3((short)pr, (short)pg, (short)pb);

            for (int j = 0; j < 8; j++)
            {
                int px = ogpx[i, j] + cgpx[i];
                int pz = ogpz[i, j] + cgpz[i];
                int py = 250;
                points.Add(new Vector3(px, py, pz));
            }

            var poly = new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray());
            verts.Add(poly);
        }

        #endregion

        return new GroundPolys(graphicsDevice, verts.ToArray());
    }

    public static GroundPolys MakeClouds(
        int maxl, int maxr, int maxb, int maxt, // newclouds
        GraphicsDevice graphicsDevice)
    {
        #region setclouds

        int[] cldd = [210, 210, 210, 1, -1000];
        int[] clds = [210, 210, 210];

        if (World.HasClouds)
        {
            int i = World.Clouds[0];
            int i252 = World.Clouds[1];
            int i253 = World.Clouds[2];
            int i254 = World.Clouds[3];
            int i255 = World.Clouds[4];
            if (i254 < 0)
            {
                i254 = 0;
            }

            if (i254 > 10)
            {
                i254 = 10;
            }

            if (i255 < -1500)
            {
                i255 = -1500;
            }

            if (i255 > -500)
            {
                i255 = -500;
            }

            cldd[0] = i;
            cldd[1] = i252;
            cldd[2] = i253;
            cldd[3] = i254;
            cldd[4] = i255;
            for (var i256 = 0; i256 < 3; i256++)
            {
                clds[i256] = (World.Sky[i256] * cldd[3] + cldd[i256]) / (cldd[3] + 1);
                clds[i256] = (int)(clds[i256] + clds[i256] * (World.Snap[i256] / 100.0F));
                if (clds[i256] > 255)
                {
                    clds[i256] = 255;
                }

                if (clds[i256] < 0)
                {
                    clds[i256] = 0;
                }
            }
        }
        else
        {
            #region setsky

            
            for (int i_251 = 0; i_251 < 3; i_251++) {
                clds[i_251] = (World.Sky[i_251] * cldd[3] + cldd[i_251]) / (cldd[3] + 1);
                clds[i_251] = (int)(clds[i_251] + clds[i_251] * (World.Sky[i_251] / 100.0F));
                if (clds[i_251] > 255) {
                    clds[i_251] = 255;
                }

                if (clds[i_251] < 0) {
                    clds[i_251] = 0;
                }
            }

            #endregion
        }

        #endregion

        #region newclouds

        maxl = maxl / 20 - 10000;
        maxr = maxr / 20 + 10000;
        maxb = maxb / 20 - 10000;
        maxt = maxt / 20 + 10000;
        var noc = (int)(((maxr - maxl) * (maxt - maxb) / 16666667) * World.CloudCoverage);
        var clx = new int[noc];
        var clz = new int[noc];
        var cmx = new int[noc];
        var clax = new int[noc, 3, 12];
        var clay = new int[noc, 3, 12];
        var claz = new int[noc, 3, 12];
        var clc = new int[noc, 2, 6, 3];

        for (int i_91 = 0; i_91 < noc; i_91++)
        {
            clx[i_91] = (int)(maxl + (maxr - maxl) * URandom.Single());
            clz[i_91] = (int)(maxb + (maxt - maxb) * URandom.Single());
            float f = (float)(0.25 + URandom.Single() * 1.25);
            float f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 0] = (int)(f_92 * 0.3826);
            claz[i_91, 0, 0] = (int)(f_92 * 0.9238);
            clay[i_91, 0, 0] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 1] = (int)(f_92 * 0.7071);
            claz[i_91, 0, 1] = (int)(f_92 * 0.7071);
            clay[i_91, 0, 1] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 2] = (int)(f_92 * 0.9238);
            claz[i_91, 0, 2] = (int)(f_92 * 0.3826);
            clay[i_91, 0, 2] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 3] = (int)(f_92 * 0.9238);
            claz[i_91, 0, 3] = -((int)(f_92 * 0.3826));
            clay[i_91, 0, 3] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 4] = (int)(f_92 * 0.7071);
            claz[i_91, 0, 4] = -((int)(f_92 * 0.7071));
            clay[i_91, 0, 4] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 5] = (int)(f_92 * 0.3826);
            claz[i_91, 0, 5] = -((int)(f_92 * 0.9238));
            clay[i_91, 0, 5] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 6] = -((int)(f_92 * 0.3826));
            claz[i_91, 0, 6] = -((int)(f_92 * 0.9238));
            clay[i_91, 0, 6] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 7] = -((int)(f_92 * 0.7071));
            claz[i_91, 0, 7] = -((int)(f_92 * 0.7071));
            clay[i_91, 0, 7] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 8] = -((int)(f_92 * 0.9238));
            claz[i_91, 0, 8] = -((int)(f_92 * 0.3826));
            clay[i_91, 0, 8] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 9] = -((int)(f_92 * 0.9238));
            claz[i_91, 0, 9] = (int)(f_92 * 0.3826);
            clay[i_91, 0, 9] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 10] = -((int)(f_92 * 0.7071));
            claz[i_91, 0, 10] = (int)(f_92 * 0.7071);
            clay[i_91, 0, 10] = (int)((25.0 - URandom.Single() * 50.0) * f);
            f_92 = (float)((200.0 + URandom.Single() * 700.0) * f);
            clax[i_91, 0, 11] = -((int)(f_92 * 0.3826));
            claz[i_91, 0, 11] = (int)(f_92 * 0.9238);
            clay[i_91, 0, 11] = (int)((25.0 - URandom.Single() * 50.0) * f);

            for (int i_93 = 0; i_93 < 12; i_93++)
            {
                int i_94 = i_93 - 1;
                if (i_94 == -1)
                {
                    i_94 = 11;
                }

                int i_95 = i_93 + 1;
                if (i_95 == 12)
                {
                    i_95 = 0;
                }

                clax[i_91, 0, i_93] = ((clax[i_91, 0, i_94] + clax[i_91, 0, i_95]) / 2 + clax[i_91, 0, i_93]) / 2;
                clay[i_91, 0, i_93] = ((clay[i_91, 0, i_94] + clay[i_91, 0, i_95]) / 2 + clay[i_91, 0, i_93]) / 2;
                claz[i_91, 0, i_93] = ((claz[i_91, 0, i_94] + claz[i_91, 0, i_95]) / 2 + claz[i_91, 0, i_93]) / 2;
            }

            for (int i_96 = 0; i_96 < 12; i_96++)
            {
                f_92 = (float)(1.2 + 0.6 * URandom.Single());
                clax[i_91, 1, i_96] = (int)(clax[i_91, 0, i_96] * f_92);
                claz[i_91, 1, i_96] = (int)(claz[i_91, 0, i_96] * f_92);
                clay[i_91, 1, i_96] = (int)(clay[i_91, 0, i_96] - 100.0 * URandom.Single());
                f_92 = (float)(1.1 + 0.3 * URandom.Single());
                clax[i_91, 2, i_96] = (int)(clax[i_91, 1, i_96] * f_92);
                claz[i_91, 2, i_96] = (int)(claz[i_91, 1, i_96] * f_92);
                clay[i_91, 2, i_96] = (int)(clay[i_91, 1, i_96] - 240.0 * URandom.Single());
            }

            cmx[i_91] = 0;

            for (int i_97 = 0; i_97 < 12; i_97++)
            {
                int i_98 = i_97 - 1;
                if (i_98 == -1)
                {
                    i_98 = 11;
                }

                int i_99 = i_97 + 1;
                if (i_99 == 12)
                {
                    i_99 = 0;
                }

                clay[i_91, 1, i_97] = ((clay[i_91, 1, i_98] + clay[i_91, 1, i_99]) / 2 + clay[i_91, 1, i_97]) / 2;
                clay[i_91, 2, i_97] = ((clay[i_91, 2, i_98] + clay[i_91, 2, i_99]) / 2 + clay[i_91, 2, i_97]) / 2;
                int i_100 = (int)MathF.Sqrt(clax[i_91, 2, i_97] * clax[i_91, 2, i_97] +
                                            claz[i_91, 2, i_97] * claz[i_91, 2, i_97]);
                if (i_100 > cmx[i_91])
                {
                    cmx[i_91] = i_100;
                }
            }

            for (int i_101 = 0; i_101 < 6; i_101++)
            {
                double d = URandom.Single();
                double d_102 = URandom.Single();

                for (int i_103 = 0; i_103 < 3; i_103++)
                {
                    f_92 = clds[i_103] * 1.05F - clds[i_103];
                    clc[i_91, 0, i_101, i_103] = (int)(clds[i_103] + f_92 * d);
                    if (clc[i_91, 0, i_101, i_103] > 255)
                    {
                        clc[i_91, 0, i_101, i_103] = 255;
                    }

                    if (clc[i_91, 0, i_101, i_103] < 0)
                    {
                        clc[i_91, 0, i_101, i_103] = 0;
                    }

                    clc[i_91, 1, i_101, i_103] = (int)(clds[i_103] * 1.05F + f_92 * d_102);
                    if (clc[i_91, 1, i_101, i_103] > 255)
                    {
                        clc[i_91, 1, i_101, i_103] = 255;
                    }

                    if (clc[i_91, 1, i_101, i_103] < 0)
                    {
                        clc[i_91, 1, i_101, i_103] = 0;
                    }
                }
            }
        }

        #endregion

        #region writeClouds

        const int div = 20;

        var polys = new List<Rad3dPoly>(noc * (12 * 2 + 1));
        var points = new List<Vector3>(12);

        for (int i = 0; i < noc; i++)
        {
            int[,] px = new int[3, 12];
            int[,] py = new int[3, 12];
            int[,] pz = new int[3, 12];

            for (int j = 0; j < 3; j++)
            {
                for (int k = 0; k < 12; k++)
                {
                    px[j, k] = (clax[i, j, k] + clx[i]) * div;
                    py[j, k] = (clay[i, j, k] + cldd[4] - 250) * div + 250;
                    pz[j, k] = (claz[i, j, k] + clz[i]) * div;
                }
            }

            for (int j = 0; j < 12; j++)
            {
                var color = new Color3((short)clc[i, 1, j / 2, 0], (short)clc[i, 1, j / 2, 1],
                    (short)clc[i, 1, j / 2, 2]);

                points.Clear();
                for (int k = 0; k < 6; k++)
                {
                    int l = 0;
                    int m = 1;
                    if (k == 0)
                    {
                        l = j;
                    }

                    if (k == 1)
                    {
                        l = j + 1;
                        if (l >= 12)
                        {
                            l -= 12;
                        }
                    }

                    if (k == 2)
                    {
                        l = j + 2;
                        if (l >= 12)
                        {
                            l -= 12;
                        }
                    }

                    if (k == 3)
                    {
                        l = j + 2;
                        if (l >= 12)
                        {
                            l -= 12;
                        }

                        m = 2;
                    }

                    if (k == 4)
                    {
                        l = j + 1;
                        if (l >= 12)
                        {
                            l -= 12;
                        }

                        m = 2;
                    }

                    if (k == 5)
                    {
                        l = j;
                        m = 2;
                    }

                    points.Add(new Vector3(px[m, l], py[m, l], pz[m, l]));
                }

                polys.Add(new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray()));
            }

            for (int j = 0; j < 12; j++)
            {
                var color = new Color3((short)clc[i, 1, j / 2, 0], (short)clc[i, 1, j / 2, 1],
                    (short)clc[i, 1, j / 2, 2]);

                points.Clear();
                for (int k = 0; k < 6; k++)
                {
                    int lx = 0;
                    int mx = 0;
                    if (k == 0)
                    {
                        lx = j;
                    }

                    if (k == 1)
                    {
                        lx = j + 1;
                        if (lx >= 12)
                        {
                            lx -= 12;
                        }
                    }

                    if (k == 2)
                    {
                        lx = j + 2;
                        if (lx >= 12)
                        {
                            lx -= 12;
                        }
                    }

                    if (k == 3)
                    {
                        lx = j + 2;
                        if (lx >= 12)
                        {
                            lx -= 12;
                        }

                        mx = 1;
                    }

                    if (k == 4)
                    {
                        lx = j + 1;
                        if (lx >= 12)
                        {
                            lx -= 12;
                        }

                        mx = 1;
                    }

                    if (k == 5)
                    {
                        lx = j;
                        mx = 1;
                    }

                    points.Add(new Vector3(px[mx, lx], py[mx, lx], pz[mx, lx]));
                }

                polys.Add(new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray()));
            }

            {
                var color = new Color3((short)clds[0], (short)clds[1], (short)clds[2]);

                points.Clear();

                for (int j = 0; j < 12; j++)
                {
                    points.Add(new Vector3(px[0, j], py[0, j], pz[0, j]));
                }

                polys.Add(new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray()));
            }
        }

        #endregion

        return new GroundPolys(graphicsDevice, polys.ToArray());
    }
    
    public static Mountains MakeMountains(
        int maxl, int maxr, int maxb, int maxt, // newmountains
        GraphicsDevice graphicsDevice
    )
    {
        #region newmountains

        var random = new URandom(World.MountainSeed);
        var nmt = (int) (20.0 + 10.0 * random.NextDouble() * World.MountainCoverage);
        var i170 = (maxl + maxr) / 60;
        var i171 = (maxb + maxt) / 60;
        var i172 = Math.Max(maxr - maxl, maxt - maxb) / 60;
        var mrd = new int[nmt];
        var nmv = new int[nmt];
        var mtx = new int[nmt][];
        var mty = new int[nmt][];
        var mtz = new int[nmt][];
        var mtc = new int[nmt][][];
        var ais = new int[nmt];
        var is173 = new int[nmt];
        for (var i174 = 0; i174 < nmt; i174++)
        {
            int i175;
            float f;
            float f176;
            ais[i174] = (int) (10000.0 + random.NextDouble() * 10000.0);
            var i177 = (int) (random.NextDouble() * 360.0);
            if (random.NextDouble() > random.NextDouble())
            {
                f = (float) (0.2 + random.NextDouble() * 0.35);
                f176 = (float) (0.2 + random.NextDouble() * 0.35);
                nmv[i174] = (int) (f * (24.0 + 16.0 * random.NextDouble()));
                i175 = (int) (85.0 + 10.0 * random.NextDouble());
            }
            else
            {
                f = (float) (0.3 + random.NextDouble() * 1.1);
                f176 = (float) (0.2 + random.NextDouble() * 0.35);
                nmv[i174] = (int) (f * (12.0 + 8.0 * random.NextDouble()));
                i175 = (int) (104.0 - 10.0 * random.NextDouble());
            }
            mtx[i174] = new int[nmv[i174] * 2];
            mty[i174] = new int[nmv[i174] * 2];
            mtz[i174] = new int[nmv[i174] * 2];
            mtc[i174] = ArrayExt.New<int>(nmv[i174], 3);
            for (var i178 = 0; i178 < nmv[i174]; i178++)
            {
                mtx[i174][i178] =
                    (int) ((i178 * 500 + (random.NextDouble() * 800.0 - 400.0) - 250 * (nmv[i174] - 1)) * f);
                mtx[i174][i178 + nmv[i174]] =
                    (int) ((i178 * 500 + (random.NextDouble() * 800.0 - 400.0) - 250 * (nmv[i174] - 1)) * f);
                mtx[i174][nmv[i174]] = (int) (mtx[i174][0] - (100.0 + random.NextDouble() * 600.0) * f);
                mtx[i174][nmv[i174] * 2 - 1] =
                    (int) (mtx[i174][nmv[i174] - 1] + (100.0 + random.NextDouble() * 600.0) * f);
                if (i178 == 0 || i178 == nmv[i174] - 1)
                {
                    mty[i174][i178] = (int) ((-400.0 - 1200.0 * random.NextDouble()) * f176 + World.Ground);
                }
                if (i178 == 1 || i178 == nmv[i174] - 2)
                {
                    mty[i174][i178] = (int) ((-1000.0 - 1450.0 * random.NextDouble()) * f176 + World.Ground);
                }
                if (i178 > 1 && i178 < nmv[i174] - 2)
                {
                    mty[i174][i178] = (int) ((-1600.0 - 1700.0 * random.NextDouble()) * f176 + World.Ground);
                }
                mty[i174][i178 + nmv[i174]] = World.Ground - 70;
                mtz[i174][i178] = i171 + i172 + ais[i174];
                mtz[i174][i178 + nmv[i174]] = i171 + i172 + ais[i174];
                var f179 = (float) (0.5 + random.NextDouble() * 0.5);
                mtc[i174][i178][0] = (int) (170.0F * f179 + 170.0F * f179 * (World.Snap[0] / 100.0F));
                if (mtc[i174][i178][0] > 255)
                {
                    mtc[i174][i178][0] = 255;
                }
                if (mtc[i174][i178][0] < 0)
                {
                    mtc[i174][i178][0] = 0;
                }
                mtc[i174][i178][1] = (int) (i175 * f179 + 85.0F * f179 * (World.Snap[1] / 100.0F));
                if (mtc[i174][i178][1] > 255)
                {
                    mtc[i174][i178][1] = 255;
                }
                if (mtc[i174][i178][1] < 1)
                {
                    mtc[i174][i178][1] = 0;
                }
                mtc[i174][i178][2] = 0;
            }
            for (var i180 = 1; i180 < nmv[i174] - 1; i180++)
            {
                var i181 = i180 - 1;
                var i182 = i180 + 1;
                mty[i174][i180] = ((mty[i174][i181] + mty[i174][i182]) / 2 + mty[i174][i180]) / 2;
            }
            UMath.Rot(mtx[i174], mtz[i174], i170, i171, i177, nmv[i174] * 2);
            is173[i174] = 0;
        }
        for (var i183 = 0; i183 < nmt; i183++)
        {
            for (var i184 = i183 + 1; i184 < nmt; i184++)
            {
                if (ais[i183] < ais[i184])
                {
                    is173[i183]++;
                }
                else
                {
                    is173[i184]++;
                }
            }

            mrd[is173[i183]] = i183;
        }
        
        #endregion

        #region writeMountains
        
        const int div = 30;
        
        var polys = new List<Rad3dPoly>();
        var points = new List<Vector3>();
        
        var cgrnd = World.GroundColor.Snap(World.Snap);

        for (int i = 0; i < nmt; i++) {
            int[] mx = new int[nmv[mrd[i]] * 2];
            int[] my = new int[nmv[mrd[i]] * 2];
            int[] mz = new int[nmv[mrd[i]] * 2];

            for (int j = 0; j < nmv[mrd[i]] * 2; j++) {
                mx[j] = mtx[mrd[i]][j] * div;
                my[j] = (mty[mrd[i]][j] - 250 + 70) * div + 250;
                mz[j] = mtz[mrd[i]][j] * div;
            }

            for (int j = 0; j < nmv[mrd[i]] - 1; j++) {
                points.Clear();
                int mr = (int)((mtc[mrd[i]][j][0] + cgrnd[0]) / 2.0F);
                int mg = (int)((mtc[mrd[i]][j][1] + cgrnd[1]) / 2.0F);
                int mb = (int)((mtc[mrd[i]][j][2] + cgrnd[2]) / 2.0F);
                var color = new Color3((short)mr, (short)mg, (short)mb);

                for (int k = 0; k < 4; k++) {
                    int l = k + j;
                    if (k == 2) {
                        l = j + nmv[mrd[i]] + 1;
                    }

                    if (k == 3) {
                        l = j + nmv[mrd[i]];
                    }

                    points.Add(new Vector3(mx[l], my[l], mz[l]));
                }

                polys.Add(new Rad3dPoly(color, null, PolyType.Flat, null, 0.0f, points.ToArray()));
            }
        }

        #endregion

        return new Mountains(graphicsDevice, polys.ToArray());
    }
}