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
    private float[] Scx = new float[20];
    private float[] Scz = new float[20];
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
    
    public void AddDust(int wheelidx, float wheelx, float wheely, float wheelz, int scx, int scz, float simag, int tilt, bool onRoof, int wheelGround)
    {
        var noDust = false;
        if (tilt > 5 && wheelidx is 0 or 2)
        {
            noDust = true;
        }
        if (tilt < -5 && wheelidx is 1 or 3)
        {
            noDust = true;
        }
        var dist = (float) ((Math.Sqrt(scx * scx + scz * scz) - 40.0) / 160.0);
        if (dist > 1.0F)
        {
            dist = 1.0F;
        }
        if (dist > 0.2 && !noDust)
        {
            _ust++;
            if (_ust == 20)
            {
                _ust = 0;
            }
            if (!onRoof)
            {
                var rand = URandom.Single();
                Sx[_ust] = ((wheelx + _mesh.Position.X * rand) / (1.0F + rand));
                Sz[_ust] = ((wheelz + _mesh.Position.Z * rand) / (1.0F + rand));
                Sy[_ust] = ((wheely + (_mesh.Position.Y - wheelGround) * rand) / (1.0F + rand));
            }
            else
            {
                Sx[_ust] = ((wheelx + (_mesh.Position.X + scx)) / 2.0F);
                Sz[_ust] = ((wheelz + (_mesh.Position.Z + scz)) / 2.0F);
                Sy[_ust] = wheely;
            }
            if (Sy[wheelidx] > 250)
            {
                Sy[wheelidx] = 250;
            }
            Osmag[_ust] = simag * dist;
            Scx[_ust] = scx;
            Scz[_ust] = scz;
            Stg[_ust] = 1;
        }
    }

    public void GameTick()
    {
        _vertexCount = 0;
        _indexCount = 0;
        for (var dust = 0; dust < 20; dust++)
        {
            if (Stg[dust] != 0)
            {
                TickDust(dust);
            }
        }
    }

    private void TickDust(int dust)
    {
        Span<int> baseColor = stackalloc int[3];
        if (Stg[dust] == 1)
        {
            _sbln[dust] = 0.6F;
            var trackersColor = false;
            for (var i = 0; i < 3; i++)
            {
                baseColor[i] = (int) (255.0F + 255.0F * (World.Snap[i] / 100.0F));
                if (baseColor[i] > 255)
                {
                    baseColor[i] = 255;
                }
                if (baseColor[i] < 0)
                {
                    baseColor[i] = 0;
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
                    Math.Abs(Sx[dust] - Trackers.X[i213]) < Trackers.Radx[i213] &&
                    Math.Abs(Sz[dust] - Trackers.Z[i213]) < Trackers.Radz[i213])
                {
                    if (Trackers.Skd[i213] == 0)
                    {
                        _sbln[dust] = 0.2F;
                    }
                    if (Trackers.Skd[i213] == 1)
                    {
                        _sbln[dust] = 0.4F;
                    }
                    if (Trackers.Skd[i213] == 2)
                    {
                        _sbln[dust] = 0.45F;
                    }
                    for (var i214 = 0; i214 < 3; i214++)
                    {
                        _srgb[dust, i214] = (Trackers.C[i213][i214] + baseColor[i214]) / 2;
                    }
                    trackersColor = true;
                }
            }
            if (!trackersColor)
            {
                for (var i215 = 0; i215 < 3; i215++)
                {
                    // TODO this should be Medium.Crgrnd
                    _srgb[dust, i215] = (World.GroundColor.Snap(World.Snap)[i215] + baseColor[i215]) / 2;
                }
            }
            var f = 0.1f + URandom.Single();
            if (f > 1.0F)
            {
                f = 1.0F;
            }
            Scx[dust] = (Scx[dust] * f);
            Scz[dust] = (Scx[dust] * f);
            for (var i216 = 0; i216 < 8; i216++)
            {
                _smag[dust, i216] = Osmag[dust] * URandom.Single() * 50.0F;
            }
            for (var vert = 0; vert < 8; vert++)
            {
                var lastVert = vert - 1;
                if (lastVert == -1)
                {
                    lastVert = 7;
                }
                var nextVert = vert + 1;
                if (nextVert == 8)
                {
                    nextVert = 0;
                }
                _smag[dust, vert] = ((_smag[dust, lastVert] + _smag[dust, nextVert]) / 2.0F + _smag[dust, vert]) / 2.0F;
            }
            _smag[dust, 6] = _smag[dust, 7];
        }

        var baseIndex = _vertexCount;
            
        var r = _srgb[dust, 0];
        var g = _srgb[dust, 1];
        var b = _srgb[dust, 2];
        // TODO apply fog here
            
        var color = new Color3((short)r, (short)g, (short)b);
        var alpha = _sbln[dust] - Stg[dust] * (_sbln[dust] / 8.0F);

        var xnaColor = new Color(color.R / 255f, color.G / 255f, color.B / 255f, alpha);

        // ais = new int[8];
        // var is223 = new int[8];
        
        // ais[0] = Xs((int) (i220 + _smag[i, 0] * 0.9238F * 1.5F), i221);
        // is223[0] = Ys((int) (i222 + _smag[i, 0] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] + _smag[dust, 0] * 0.9238F * 1.5F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] + _smag[dust, 0] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[1] = Xs((int) (i220 + _smag[i, 1] * 0.9238F * 1.5F), i221);
        // is223[1] = Ys((int) (i222 - _smag[i, 1] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] + _smag[dust, 1] * 0.9238F * 1.5F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] - _smag[dust, 1] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[2] = Xs((int) (i220 + _smag[i, 2] * 0.3826F), i221);
        // is223[2] = Ys((int) (i222 - _smag[i, 2] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] + _smag[dust, 2] * 0.3826F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] - _smag[dust, 2] * 0.9238F
        ), xnaColor));
        
        // ais[3] = Xs((int) (i220 - _smag[i, 3] * 0.3826F), i221);
        // is223[3] = Ys((int) (i222 - _smag[i, 3] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] - _smag[dust, 3] * 0.3826F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] - _smag[dust, 3] * 0.9238F
        ), xnaColor));
        
        // ais[4] = Xs((int) (i220 - _smag[i, 4] * 0.9238F * 1.5F), i221);
        // is223[4] = Ys((int) (i222 - _smag[i, 4] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] - _smag[dust, 4] * 0.9238F * 1.5F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] - _smag[dust, 4] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[5] = Xs((int) (i220 - _smag[i, 5] * 0.9238F * 1.5F), i221);
        // is223[5] = Ys((int) (i222 + _smag[i, 5] * 0.3826F * 1.5F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] - _smag[dust, 5] * 0.9238F * 1.5F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] + _smag[dust, 5] * 0.3826F * 1.5F
        ), xnaColor));
        
        // ais[6] = Xs((int) (i220 - _smag[i, 6] * 0.3826F * 1.7F), i221);
        // is223[6] = Ys((int) (i222 + _smag[i, 6] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] - _smag[dust, 6] * 0.3826F * 1.7F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] + _smag[dust, 6] * 0.9238F
        ), xnaColor));
        
        // ais[7] = Xs((int) (i220 + _smag[i, 7] * 0.3826F * 1.7F), i221);
        // is223[7] = Ys((int) (i222 + _smag[i, 7] * 0.9238F), i221);
        _verts[_vertexCount++] = (new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(
            Sx[dust] + _smag[dust, 7] * 0.3826F * 1.7F,
            Sy[dust] - _smag[dust, 7],
            Sz[dust] + _smag[dust, 7] * 0.9238F
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
            
        Sx[dust] += Scx[dust] / (Stg[dust] + 1);
        Sz[dust] += Scz[dust] / (Stg[dust] + 1);
        for (var vert = 0; vert < 7; vert++)
        {
            _smag[dust, vert] += 5.0F + URandom.Single() * 15.0F;
        }
        _smag[dust, 7] = _smag[dust, 6];

        if (Stg[dust] == 7)
        {
            Stg[dust] = 0;
        }
        else
        {
            Stg[dust]++;
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