using Microsoft.Xna.Framework.Graphics;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Flames
{
    private int _embos;
    private readonly Mesh _mesh;
    private readonly GraphicsDevice _graphicsDevice;
    private int[] _pa, _pb;

    private VertexPositionColor[] _triangles;
    private readonly BasicEffect _flameEffect;

    public bool Expand;

    private int _tick;
    public float Darken = 1f;

    public Flames(Mesh mesh, GraphicsDevice graphicsDevice)
    {
        _mesh = mesh;
        _graphicsDevice = graphicsDevice;
        _pa = new int[mesh.Polys.Length];
        _pb = new int[mesh.Polys.Length];
        
        _flameEffect = new BasicEffect(graphicsDevice)
        {
            LightingEnabled = false,
            TextureEnabled = false,
            VertexColorEnabled = true
        };
        _triangles = new VertexPositionColor[9 * mesh.Polys.Length];
    }

    public void GameTick()
    {
        if (_mesh.Wasted)
        {
            if (++_tick == GameSparker.OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
            {
                if (_embos <= 11)
                {
                    Expand = URandom.Boolean();
                }
                else
                {
                    Expand = false;
                }

                if (_embos is > 7 and <= 9)
                {
                    Darken = 0.7f;
                }
                else if (_embos is > 9 and <= 10)
                {
                    Darken = 0.6f;
                }
                else if (_embos is > 10 and <= 12)
                {
                    Darken = 0.5f;
                }

                if (_embos == 12)
                {
                    _mesh.ChipWasted();
                }

                if (_embos == 13)
                {
                    Darken = 0.4f;
                }

                if (_embos < 70)
                {
                    _embos++;
                }
                else
                {
                    _embos = 16;
                }

                if (_embos == 16)
                {
                    for (var i = 0; i < _mesh.Polys.Length; i++)
                    {
                        var n = _mesh.Polys[i].Points.Length;
                        _pa[i] = URandom.Int(0, n);
                        for (_pb[i] = URandom.Int(0, n); _pa[i] == _pb[i]; _pb[i] = URandom.Int(0, n))
                        {
                        }
                    }
                }

                _tick = 0;
            }
        }
    }

    public void Render(Camera camera)
    {
        if (_embos >= 16)
        {
            Span<float> outerX = stackalloc float[3];
            Span<float> outerY = stackalloc float[3];
            Span<float> outerZ = stackalloc float[3];
            Span<float> innerX = stackalloc float[3];
            Span<float> innerY = stackalloc float[3];
            Span<float> innerZ = stackalloc float[3];
            for (var i = 0; i < _mesh.Polys.Length; i++)
            {
                var poly = _mesh.Polys[i];
                
                var i45 = 1;
                var i46 = 1;
                int i47;
                for (i47 = (int)Math.Abs(_mesh.Rotation.Zy.Degrees); i47 > 270; i47 -= 360)
                {
                }
                i47 = Math.Abs(i47);
                if (i47 > 90)
                {
                    i45 = -1;
                }
                int i48;
                for (i48 = (int)Math.Abs(_mesh.Rotation.Xy.Degrees); i48 > 270; i48 -= 360)
                {
                }
                i48 = Math.Abs(i48);
                if (i48 > 90)
                {
                    i46 = -1;
                }
                
                outerX[0] = poly.Points[_pa[i]].X;
                outerY[0] = poly.Points[_pa[i]].Y;
                outerZ[0] = poly.Points[_pa[i]].Z;
                outerX[1] = poly.Points[_pb[i]].X;
                outerY[1] = poly.Points[_pb[i]].Y;
                outerZ[1] = poly.Points[_pb[i]].Z;
                
                while (Math.Abs(outerX[0] - outerX[1]) > 100)
                {
                    if (outerX[1] > outerX[0])
                    {
                        outerX[1] -= 30;
                    }
                    else
                    {
                        outerX[1] += 30;
                    }
                }

                while (Math.Abs(outerZ[0] - outerZ[1]) > 100)
                {
                    if (outerZ[1] > outerZ[0])
                    {
                        outerZ[1] -= 30;
                    }
                    else
                    {
                        outerZ[1] += 30;
                    }
                }
                
                var i51 = (int) (Math.Abs(outerX[0] - outerX[1]) / 3 * (0.5 - URandom.Single()));
                var i52 = (int) (Math.Abs(outerZ[0] - outerZ[1]) / 3 * (0.5 - URandom.Single()));
                outerX[2] = (outerX[0] + outerX[1]) / 2 + i51;
                outerZ[2] = (outerZ[0] + outerZ[1]) / 2 + i52;
                var i53 = (int) ((Math.Abs(outerX[0] - outerX[1]) + Math.Abs(outerZ[0] - outerZ[1])) / 1.5 * (URandom.Single() / 2.0F + 0.5));
                outerY[2] = (outerY[0] + outerY[1]) / 2 - i45 * i46 * i53;
                
                
                var r = (int) (255.0F + 255.0F * (World.Snap[0] / 400.0F));
                if (r > 255)
                {
                    r = 255;
                }
                if (r < 0)
                {
                    r = 0;
                }
                var g = (int) (169.0F + 169.0F * (World.Snap[1] / 300.0F));
                if (g > 255)
                {
                    g = 255;
                }
                if (g < 0)
                {
                    g = 0;
                }
                var b = (int) (89.0F + 89.0F * (World.Snap[2] / 200.0F));
                if (b > 255)
                {
                    b = 255;
                }
                if (b < 0)
                {
                    b = 0;
                }

                var outerColor = new Color3((short)r, (short)g, (short)b);
                
                // inner flame
                
                innerX[0] = poly.Points[_pa[i]].X;
                innerY[0] = poly.Points[_pa[i]].Y;
                innerZ[0] = poly.Points[_pa[i]].Z;
                innerX[1] = poly.Points[_pb[i]].X;
                innerY[1] = poly.Points[_pb[i]].Y;
                innerZ[1] = poly.Points[_pb[i]].Z;
                while (Math.Abs(innerX[0] - innerX[1]) > 100)
                {
                    if (innerX[1] > innerX[0])
                    {
                        innerX[1] -= 30;
                    }
                    else
                    {
                        innerX[1] += 30;
                    }
                }

                while (Math.Abs(innerZ[0] - innerZ[1]) > 100)
                {
                    if (innerZ[1] > innerZ[0])
                    {
                        innerZ[1] -= 30;
                    }
                    else
                    {
                        innerZ[1] += 30;
                    }
                }

                innerX[2] = (innerX[0] + innerX[1]) / 2 + i51;
                innerZ[2] = (innerZ[0] + innerZ[1]) / 2 + i52;
                i53 = (int) (i53 * 0.8);
                innerY[2] = (innerY[0] + innerY[1]) / 2 - i45 * i46 * i53;
                
                r = (int) (255.0F + 255.0F * (World.Snap[0] / 400.0F));
                if (r > 255)
                {
                    r = 255;
                }
                if (r < 0)
                {
                    r = 0;
                }
                g = (int) (207.0F + 207.0F * (World.Snap[1] / 300.0F));
                if (g > 255)
                {
                    g = 255;
                }
                if (g < 0)
                {
                    g = 0;
                }
                b = (int) (136.0F + 136.0F * (World.Snap[2] / 200.0F));
                if (b > 255)
                {
                    b = 255;
                }
                if (b < 0)
                {
                    b = 0;
                }
                
                var innerColor = new Color3((short)r, (short)g, (short)b);
                
                // We build the outer flame out of two triangles, so that it doesn't overlap with the inner flame.
                // These triangles share a vertex with the inner flame's center triangle.
                var triBase = i * 9;
                var outer0 = new Vector3(outerX[0], outerY[0], outerZ[0]); // anchor left
                var outer1 = new Vector3(outerX[1], outerY[1], outerZ[1]); // anchor right
                var outer2 = new Vector3(outerX[2], outerY[2], outerZ[2]); // top
                var inner0 = new Vector3(innerX[0], innerY[0], innerZ[0]); // anchor left
                var inner1 = new Vector3(innerX[1], innerY[1], innerZ[1]); // anchor right
                var inner2 = new Vector3(innerX[2], innerY[2], innerZ[2]); // top
                
                // cutout of the outer flame
                _triangles[triBase + 0] = new VertexPositionColor(outer0, outerColor);
                _triangles[triBase + 1] = new VertexPositionColor(outer2, outerColor);
                _triangles[triBase + 2] = new VertexPositionColor(inner2, outerColor);
                _triangles[triBase + 3] = new VertexPositionColor(outer1, outerColor);
                _triangles[triBase + 4] = new VertexPositionColor(outer2, outerColor);
                _triangles[triBase + 5] = new VertexPositionColor(inner2, outerColor);
                
                 // inner flame
                _triangles[triBase + 6] = new VertexPositionColor(inner0, innerColor);
                _triangles[triBase + 7] = new VertexPositionColor(inner1, innerColor);
                _triangles[triBase + 8] = new VertexPositionColor(inner2, innerColor);
            }
            
            
            _flameEffect.World = _mesh.MatrixWorld;
            _flameEffect.View = camera.ViewMatrix;
            _flameEffect.Projection = camera.ProjectionMatrix;
        
            _graphicsDevice.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _flameEffect.CurrentTechnique.Passes)
            {
                pass.Apply();

                _graphicsDevice.DrawUserPrimitives(
                    PrimitiveType.TriangleList,
                    _triangles,
                    0,
                    3 * _mesh.Polys.Length,
                    VertexPositionColor.VertexDeclaration
                );
            }
            _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        }
    }
}