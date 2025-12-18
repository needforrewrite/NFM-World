using Microsoft.Xna.Framework.Graphics;
using Color = Microsoft.Xna.Framework.Color;
using Matrix = Microsoft.Xna.Framework.Matrix;
using URandom = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

public class Dust
{
    private readonly Mesh _mesh;
    private readonly GraphicsDevice _graphicsDevice;

    private int _ust;
    
    private float[] Sx = new float[20];
    private float[] Sy = new float[20];
    private float[] Sz = new float[20];
    private float[] Osmag = new float[20];
    private int[] Scx = new int[20];
    private int[] Scz = new int[20];
    private int[] Stg = new int[20];
    private float[] _sbln = new float[20];
    private int[,] _srgb = new int[20, 3];
    private float[,] _smag = new float[20, 8];
    private VertexPositionColor[] _verts = new VertexPositionColor[20 * 8];
    private int _vertexCount;
    private int[] _indices = new int[20 * 8 * 3];
    private int _indexCount;
    private readonly BasicEffect _effect;

    public Dust(Mesh mesh, GraphicsDevice graphicsDevice)
    {
        _mesh = mesh;
        _graphicsDevice = graphicsDevice;

        _effect = new BasicEffect(graphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
    }
    
    public void AddDust(int i, float f, float f199, float f200, int i201, int i202, float f203, int i204, bool aabool, int wheelGround)
    {
        var bool205 = false;
        if (i204 > 5 && (i == 0 || i == 2))
        {
            bool205 = true;
        }
        if (i204 < -5 && (i == 1 || i == 3))
        {
            bool205 = true;
        }
        var f206 = (float) ((Math.Sqrt(i201 * i201 + i202 * i202) - 40.0) / 160.0);
        if (f206 > 1.0F)
        {
            f206 = 1.0F;
        }
        if (f206 > 0.2 && !bool205)
        {
            _ust++;
            if (_ust == 20)
            {
                _ust = 0;
            }
            if (!aabool)
            {
                var f207 = URandom.Single();
                Sx[_ust] = ((f + _mesh.Position.X * f207) / (1.0F + f207));
                Sz[_ust] = ((f200 + _mesh.Position.Z * f207) / (1.0F + f207));
                Sy[_ust] = ((f199 + (_mesh.Position.Y - wheelGround) * f207) / (1.0F + f207));
            }
            else
            {
                Sx[_ust] = ((f + (_mesh.Position.X + i201)) / 2.0F);
                Sz[_ust] = ((f200 + (_mesh.Position.Z + i202)) / 2.0F);
                Sy[_ust] = f199;
            }
            if (Sy[i] > 250)
            {
                Sy[i] = 250;
            }
            Osmag[_ust] = f203 * f206;
            Scx[_ust] = i201;
            Scz[_ust] = i202;
            Stg[_ust] = 1;
        }
    }

    public void GameTick()
    {
        _vertexCount = 0;
        _indexCount = 0;
        for (var i137 = 0; i137 < 20; i137++)
        {
            if (Stg[i137] != 0)
            {
                TickDust(i137);
            }
        }
    }

    private void TickDust(int i)
    {
        int[] ais;
        if (Stg[i] == 1)
        {
            _sbln[i] = 0.6F;
            var bool208 = false;
            ais = new int[3];
            for (var i209 = 0; i209 < 3; i209++)
            {
                ais[i209] = (int) (255.0F + 255.0F * (World.Snap[i209] / 100.0F));
                if (ais[i209] > 255)
                {
                    ais[i209] = 255;
                }
                if (ais[i209] < 0)
                {
                    ais[i209] = 0;
                }
            }
            var i210 = (_mesh.Position.X - Trackers.Sx) / 3000;
            if (i210 > Trackers.Ncx)
            {
                i210 = Trackers.Ncx;
            }
            if (i210 < 0)
            {
                i210 = 0;
            }
            var i211 = (_mesh.Position.Z - Trackers.Sz) / 3000;
            if (i211 > Trackers.Ncz)
            {
                i211 = Trackers.Ncz;
            }
            if (i211 < 0)
            {
                i211 = 0;
            }
            for (var i213 = 0; i213 < Trackers.Nt; i213++)
            {
                if (Math.Abs(Trackers.Zy[i213]) != 90 && Math.Abs(Trackers.Xy[i213]) != 90 &&
                    Math.Abs(Sx[i] - Trackers.X[i213]) < Trackers.Radx[i213] &&
                    Math.Abs(Sz[i] - Trackers.Z[i213]) < Trackers.Radz[i213])
                {
                    if (Trackers.Skd[i213] == 0)
                    {
                        _sbln[i] = 0.2F;
                    }
                    if (Trackers.Skd[i213] == 1)
                    {
                        _sbln[i] = 0.4F;
                    }
                    if (Trackers.Skd[i213] == 2)
                    {
                        _sbln[i] = 0.45F;
                    }
                    for (var i214 = 0; i214 < 3; i214++)
                    {
                        _srgb[i, i214] = (Trackers.C[i213][i214] + ais[i214]) / 2;
                    }
                    bool208 = true;
                }
            }
            if (!bool208)
            {
                for (var i215 = 0; i215 < 3; i215++)
                {
                    // TODO this should be Medium.Crgrnd
                    _srgb[i, i215] = (World.GroundColor.Snap(World.Snap)[i215] + ais[i215]) / 2;
                }
            }
            var f = 0.1f + URandom.Single();
            if (f > 1.0F)
            {
                f = 1.0F;
            }
            Scx[i] = (int) (Scx[i] * f);
            Scz[i] = (int) (Scx[i] * f);
            for (var i216 = 0; i216 < 8; i216++)
            {
                _smag[i, i216] = Osmag[i] * URandom.Single() * 50.0F;
            }
            for (var i217 = 0; i217 < 8; i217++)
            {
                var i218 = i217 - 1;
                if (i218 == -1)
                {
                    i218 = 7;
                }
                var i219 = i217 + 1;
                if (i219 == 8)
                {
                    i219 = 0;
                }
                _smag[i, i217] = ((_smag[i, i218] + _smag[i, i219]) / 2.0F + _smag[i, i217]) / 2.0F;
            }
            _smag[i, 6] = _smag[i, 7];
        }

        var baseIndex = _vertexCount;
            
        var i231 = _srgb[i, 0];
        var i232 = _srgb[i, 1];
        var i233 = _srgb[i, 2];
        // TODO apply fog here
            
        var color = new Color3((short)i231, (short)i232, (short)i233);
        var alpha = _sbln[i] - Stg[i] * (_sbln[i] / 8.0F);

        var xnaColor = new Color(color.R / 255f, color.G / 255f, color.B / 255f, alpha);

        // ais = new int[8];
        // var is223 = new int[8];
        
        // ais[0] = Xs((int) (i220 + _smag[i, 0] * 0.9238F * 1.5F), i221);
        // is223[0] = Ys((int) (i222 + _smag[i, 0] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] + _smag[i, 0] * 0.9238F * 1.5F,
            Sy[i] - _smag[i, 7],
            Sz[i] + _smag[i, 0] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[1] = Xs((int) (i220 + _smag[i, 1] * 0.9238F * 1.5F), i221);
        // is223[1] = Ys((int) (i222 - _smag[i, 1] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] + _smag[i, 1] * 0.9238F * 1.5F,
            Sy[i] - _smag[i, 7],
            Sz[i] - _smag[i, 1] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[2] = Xs((int) (i220 + _smag[i, 2] * 0.3826F), i221);
        // is223[2] = Ys((int) (i222 - _smag[i, 2] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] + _smag[i, 2] * 0.3826F,
            Sy[i] - _smag[i, 7],
            Sz[i] - _smag[i, 2] * 0.9238F
        ), xnaColor));
        
        // ais[3] = Xs((int) (i220 - _smag[i, 3] * 0.3826F), i221);
        // is223[3] = Ys((int) (i222 - _smag[i, 3] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] - _smag[i, 3] * 0.3826F,
            Sy[i] - _smag[i, 7],
            Sz[i] - _smag[i, 3] * 0.9238F
        ), xnaColor));
        
        // ais[4] = Xs((int) (i220 - _smag[i, 4] * 0.9238F * 1.5F), i221);
        // is223[4] = Ys((int) (i222 - _smag[i, 4] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] - _smag[i, 4] * 0.9238F * 1.5F,
            Sy[i] - _smag[i, 7],
            Sz[i] - _smag[i, 4] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[5] = Xs((int) (i220 - _smag[i, 5] * 0.9238F * 1.5F), i221);
        // is223[5] = Ys((int) (i222 + _smag[i, 5] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] - _smag[i, 5] * 0.9238F * 1.5F,
            Sy[i] - _smag[i, 7],
            Sz[i] + _smag[i, 5] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[6] = Xs((int) (i220 - _smag[i, 6] * 0.3826F * 1.7F), i221);
        // is223[6] = Ys((int) (i222 + _smag[i, 6] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] - _smag[i, 6] * 0.3826F * 1.7F,
            Sy[i] - _smag[i, 7],
            Sz[i] + _smag[i, 6] * 0.9238F
        ), xnaColor));
        
        // ais[7] = Xs((int) (i220 + _smag[i, 7] * 0.3826F * 1.7F), i221);
        // is223[7] = Ys((int) (i222 + _smag[i, 7] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[i] + _smag[i, 7] * 0.3826F * 1.7F,
            Sy[i] - _smag[i, 7],
            Sz[i] + _smag[i, 7] * 0.9238F
        ), xnaColor));
            
        // make indices of polygon
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 1;
        _indices[_indexCount++] = baseIndex + 2;
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 2;
        _indices[_indexCount++] = baseIndex + 3;
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 3;
        _indices[_indexCount++] = baseIndex + 4;
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 4;
        _indices[_indexCount++] = baseIndex + 5;
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 5;
        _indices[_indexCount++] = baseIndex + 6;
        _indices[_indexCount++] = baseIndex + 0;
        _indices[_indexCount++] = baseIndex + 6;
        _indices[_indexCount++] = baseIndex + 7;
            
        Sx[i] += Scx[i] / (Stg[i] + 1);
        Sz[i] += Scz[i] / (Stg[i] + 1);
        for (var i224 = 0; i224 < 7; i224++)
        {
            _smag[i, i224] += 5.0F + URandom.Single() * 15.0F;
        }
        _smag[i, 7] = _smag[i, 6];

        if (Stg[i] == 7)
        {
            Stg[i] = 0;
        }
        else
        {
            Stg[i]++;
        }
    }

    public void Render(Camera camera)
    {
        if (_vertexCount == 0 || _indexCount == 0)
        {
            return;
        }
        
        _effect.World = Matrix.Identity;
        _effect.View = camera.ViewMatrix;
        _effect.Projection = camera.ProjectionMatrix;
        
        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.DepthStencilState = DepthStencilState.DepthRead;
        _graphicsDevice.BlendState = BlendState.NonPremultiplied;
        foreach (var pass in _effect.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _verts,
                0,
                _vertexCount,
                _indices,
                0,
                _indexCount / 3,
                VertexPositionColor.VertexDeclaration
            );
        }
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
    }
}