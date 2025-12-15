using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
using Stride.Core.Mathematics;
using Matrix = Microsoft.Xna.Framework.Matrix;
using URandom = NFMWorld.Util.Random;

namespace NFMWorld.Mad;

public class FixHoop : Mesh
{
    public bool Rotated;

    private const int CntLines = 4;
    
    private readonly int[] _edl = new int[CntLines];
    private readonly int[] _edr = new int[CntLines];
    private readonly int[] _elc = new int[CntLines];

    private BasicEffect _fixhoopEffect;

    private VertexPositionColor[] _vertices = new VertexPositionColor[8*CntLines];
    private short[] _indices = new short[18*CntLines];

    public FixHoop(Mesh baseMesh, Vector3 position, Euler rotation) : base(baseMesh, position, rotation)
    {
        _fixhoopEffect = new BasicEffect(GraphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
    }

    private void RenderFixHoop(Camera camera)
    {
        for (var i = 0; i < 4; i++)
        {
            PrepareLine(i);
        }
        _fixhoopEffect.World = Matrix.CreateTranslation(Position.ToXna());
        _fixhoopEffect.View = camera.ViewMatrix;
        _fixhoopEffect.Projection = camera.ProjectionMatrix;
        
        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        foreach (var pass in _fixhoopEffect.CurrentTechnique.Passes)
        {
            pass.Apply();

            GraphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _vertices,
                0,
                8*CntLines,
                _indices,
                0,
                6*CntLines,
                VertexPositionColor.VertexDeclaration
            );
        }
        GraphicsDevice.RasterizerState = RasterizerState.CullClockwise;
    }

    private void PrepareLine(int idx)
    {
        Span<int> x = stackalloc int[8];
        Span<int> y = stackalloc int[8];
        Span<int> z = stackalloc int[8];
        if (_elc[idx] == 0)
        {
            _edl[idx] = (int)(380.0F - URandom.Single() * 760.0F);
            _edr[idx] = (int)(380.0F - URandom.Single() * 760.0F);
            _elc[idx] = 1;
        }

        var yl = (int)(_edl[idx] + (190.0F - URandom.Single() * 380.0F));
        var yr = (int)(_edr[idx] + (190.0F - URandom.Single() * 380.0F));
        var x2nd = (int)(URandom.Single() * 126.0F);
        var x1st = (int)(URandom.Single() * 126.0F);
        for (var i = 0; i < 8; i++)
        {
            z[i] = 0;
        }

        x[0] = -504;
        y[0] = -_edl[idx] - 5 - URandom.Int(0, 5);
        x[1] = -252 + x1st;
        y[1] = -yl - 5 - URandom.Int(0, 5);
        x[2] = +252 - x2nd;
        y[2] = -yr - 5 - URandom.Int(0, 5);
        x[3] = +504;
        y[3] = -_edr[idx] - 5 - URandom.Int(0, 5);
        x[4] = +504;
        y[4] = -_edr[idx] + 5 + URandom.Int(0, 5);
        x[5] = +252 - x2nd;
        y[5] = -yr + 5 + URandom.Int(0, 5);
        x[6] = -252 + x1st;
        y[6] = -yl + 5 + URandom.Int(0, 5);
        x[7] = -504;
        y[7] = -_edl[idx] + 5 + URandom.Int(0, 5);
        if (Rotated)
        {
            UMath.Rot(x, z, 0, 0, 90f, 8);
        }
        
        var r = (int) (160.0F + 160.0F * (World.Snap[0] / 500.0F));
        if (r > 255)
        {
            r = 255;
        }
        if (r < 0)
        {
            r = 0;
        }
        var g = (int) (238.0F + 238.0F * (World.Snap[1] / 500.0F));
        if (g > 255)
        {
            g = 255;
        }
        if (g < 0)
        {
            g = 0;
        }
        var b = (int) (255.0F + 255.0F * (World.Snap[2] / 500.0F));
        if (b > 255)
        {
            b = 255;
        }
        if (b < 0)
        {
            b = 0;
        }
        r = (r * 2 + 214 * (_elc[idx] - 1)) / (_elc[idx] + 1);
        g = (g * 2 + 236 * (_elc[idx] - 1)) / (_elc[idx] + 1);
        var color = new Color3((short)r,(short) g, (short)b);

        int startVertIdx = idx * 8;
        _vertices[startVertIdx + 0] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[0], y[0], z[0]), color.ToXna());
        _vertices[startVertIdx + 1] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[1], y[1], z[1]), color.ToXna());
        _vertices[startVertIdx + 2] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[2], y[2], z[2]), color.ToXna());
        _vertices[startVertIdx + 3] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[3], y[3], z[3]), color.ToXna());
        _vertices[startVertIdx + 4] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[4], y[4], z[4]), color.ToXna());
        _vertices[startVertIdx + 5] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[5], y[5], z[5]), color.ToXna());
        _vertices[startVertIdx + 6] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[6], y[6], z[6]), color.ToXna());
        _vertices[startVertIdx + 7] = new VertexPositionColor(new Microsoft.Xna.Framework.Vector3(x[7], y[7], z[7]), color.ToXna());
        
        // vertices represents an outline of a polygon with 8 vertices
        // we need to create indices for 4 triangles to fill the shape

        int startTriIdx = idx * 18;
        _indices[startTriIdx + 0] = (short)(startVertIdx + 0);
        _indices[startTriIdx + 1] = (short)(startVertIdx + 1);
        _indices[startTriIdx + 2] = (short)(startVertIdx + 7);
        _indices[startTriIdx + 3] = (short)(startVertIdx + 1);
        _indices[startTriIdx + 4] = (short)(startVertIdx + 6);
        _indices[startTriIdx + 5] = (short)(startVertIdx + 7);
        _indices[startTriIdx + 6] = (short)(startVertIdx + 1);
        _indices[startTriIdx + 7] = (short)(startVertIdx + 2);
        _indices[startTriIdx + 8] = (short)(startVertIdx + 6);
        _indices[startTriIdx + 9] = (short)(startVertIdx + 2);
        _indices[startTriIdx + 10] = (short)(startVertIdx + 5);
        _indices[startTriIdx + 11] = (short)(startVertIdx + 6);
        _indices[startTriIdx + 12] = (short)(startVertIdx + 2);
        _indices[startTriIdx + 13] = (short)(startVertIdx + 3);
        _indices[startTriIdx + 14] = (short)(startVertIdx + 5);
        _indices[startTriIdx + 15] = (short)(startVertIdx + 3);
        _indices[startTriIdx + 16] = (short)(startVertIdx + 4);
        _indices[startTriIdx + 17] = (short)(startVertIdx + 5);

        if (_elc[idx] > URandom.Single() * 60.0F)
        {
            _elc[idx] = 0;
        }
        else
        {
            _elc[idx]++;
        }
    }

    public override void Render(Camera camera, Camera? lightCamera, bool isCreateShadowMap = false)
    {
        base.Render(camera, lightCamera, isCreateShadowMap);
        if (!isCreateShadowMap)
        {
            RenderFixHoop(camera);
        }
    }

    public override void GameTick()
    {
        if (!Rotated || Rotation.Xz != AngleSingle.ZeroAngle)
        {
            var xy = Rotation.Xy.Degrees;
            xy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (xy > 360)
            {
                xy -= 360;
            }
            Rotation = Rotation with { Xy = AngleSingle.FromDegrees(xy) };
        }
        else
        {
            var zy = Rotation.Zy.Degrees;
            zy += 11 * GameSparker.PHYSICS_MULTIPLIER;
            if (zy > 360)
            {
                zy -= 360;
            }
            Rotation = Rotation with { Zy = AngleSingle.FromDegrees(zy) };
        }
    }
}