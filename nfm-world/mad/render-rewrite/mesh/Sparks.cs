using Microsoft.Xna.Framework.Graphics;
using NFMWorld.Util;
using Stride.Core.Mathematics;

namespace NFMWorld.Mad;

public class Sparks
{
    private readonly Mesh _mesh;
    private readonly GraphicsDevice _graphicsDevice;

    internal int Sprk;
    private int _sprkat;
    internal float Srx;
    internal float Sry;
    internal float Srz;
    internal float Rcx;
    internal float Rcy;
    internal float Rcz;
    private int[] _rtg = new int[100];
    private float[] _rx = new float[100];
    private float[] _ry = new float[100];
    private float[] _rz = new float[100];
    private float[] _vrx = new float[100];
    private float[] _vry = new float[100];
    private float[] _vrz = new float[100];
    
    private LineMesh.LineMeshVertexAttribute[] _lineVertices = new LineMesh.LineMeshVertexAttribute[100 * LineMeshHelpers.VerticesPerLine];
    private int[] _lineIndices = new int[100 * LineMeshHelpers.IndicesPerLine];
    private int _vertexCount;
    private int _triangleCount;
    private readonly LineEffect _material;
    private int _sparkCount;

    public Sparks(Mesh mesh, GraphicsDevice graphicsDevice)
    {
        _mesh = mesh;
        _graphicsDevice = graphicsDevice;

        _material = new LineEffect(Program._lineShader);

        _sprkat = _mesh.Wheels.FirstOrDefault().Sparkat;
    }
    
    public void AddSpark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        if (type != 1)
        {
            Srx = (wheelx - _sprkat * UMath.Sin(_mesh.Rotation.Xz.Degrees));
            Sry = (wheely - wheelGround - _sprkat * UMath.Cos(_mesh.Rotation.Zy.Degrees) * UMath.Cos(_mesh.Rotation.Xy.Degrees));
            Srz = (wheelz + _sprkat * UMath.Cos(_mesh.Rotation.Xz.Degrees));
            Sprk = 1;
        }
        else
        {
            Sprk++;
            if (Sprk == 4)
            {
                Srx = (_mesh.Position.X + scx);
                Sry = wheely - wheelGround;
                Srz = (_mesh.Position.Z + scz);
                Sprk = 5;
            }
            else
            {
                Srx = wheelx;
                Sry = wheely - wheelGround;
                Srz = wheelz;
            }
        }
        if (type == 2)
        {
            Sprk = 6;
        }
        Rcx = scx;
        Rcy = scy;
        Rcz = scz;
    }

    public void GameTick()
    {
        _vertexCount = 0;
        _triangleCount = 0;
        if (Sprk != 0)
        {
            var i = (int) (Math.Sqrt(Rcx * Rcx + Rcy * Rcy + Rcz * Rcz) / 10.0);
            if (i > 5)
            {
                if (i > 33)
                {
                    i = 33;
                }
                var i241 = 0;
                for (var i242 = 0; i242 < 100; i242++)
                {
                    if (_rtg[i242] == 0)
                    {
                        _rtg[i242] = 1;
                        i241++;
                        _sparkCount++;
                    }
                    if (i241 == i)
                    {
                        break;
                    }
                }
            }
        }
        
        if (_sparkCount == 0)
        {
            // Fast exit if no sparks are active
            return;
        }

        Span<LineMesh.LineMeshVertexAttribute> verts = stackalloc LineMesh.LineMeshVertexAttribute[LineMeshHelpers.VerticesPerLine];
        Span<int> inds = stackalloc int[LineMeshHelpers.IndicesPerLine];

        for (var i = 0; i < 100; i++)
        {
            if (_rtg[i] != 0)
            {
                if (_rtg[i] == 1)
                {
                    if (Sprk < 5)
                    {
                        _rx[i] = Srx + 3 - (URandom.Single() * 6.7F);
                        _ry[i] = Sry + 3 - (URandom.Single() * 6.7F);
                        _rz[i] = Srz + 3 - (URandom.Single() * 6.7F);
                    }
                    else
                    {
                        _rx[i] = Srx + 10 - (URandom.Single() * 20.0F);
                        _ry[i] = Sry - (URandom.Single() * 4.0F);
                        _rz[i] = Srz + 10 - (URandom.Single() * 20.0F);
                    }
                    var i243 = MathF.Sqrt(Rcx * Rcx + Rcy * Rcy + Rcz * Rcz);
                    if (float.IsNaN(i243) || float.IsInfinity(i243)) i243 = 1.0F;
                    i243 = Math.Clamp(i243, 1, 100); // prevent division by zero

                    var f = 0.2F + 0.4F * URandom.Single();
                    var f244 = URandom.Single() * URandom.Single() * URandom.Single();
                    var f245 = 1.0F;
                    if (URandom.Boolean())
                    {
                        if (URandom.Boolean())
                        {
                            f245 *= -1.0F;
                        }
                        _vrx[i] = -((Rcx + i243 * (1.0F - Rcx / i243) * f244 * f245) * f);
                    }
                    if (URandom.Boolean())
                    {
                        if (URandom.Boolean())
                        {
                            f245 *= -1.0F;
                        }
                        if (Sprk == 5)
                        {
                            f245 = 1.0F;
                        }
                        _vry[i] = -((Rcy + i243 * (1.0F - Rcy / i243) * f244 * f245) * f);
                    }
                    if (URandom.Boolean())
                    {
                        if (URandom.Boolean())
                        {
                            f245 *= -1.0F;
                        }
                        _vrz[i] = -((Rcz + i243 * (1.0F - Rcz / i243) * f244 * f245) * f);
                    }
                }
                _rx[i] = (_rx[i] + _vrx[i]);
                _ry[i] = (_ry[i] + _vry[i]);
                _rz[i] = (_rz[i] + _vrz[i]);
                var start = new Vector3(_rx[i], _ry[i], _rz[i]);
                var end = new Vector3(_rx[i] + _vrx[i], _ry[i] + _vry[i], _rz[i] + _vrz[i]);
                var color = new Color3(255, (short)(197 - 30 * _rtg[i]), 0);
                // TODO apply fog to color
                
                // draw line
                LineMeshHelpers.CreateLineMesh(start, end, _vertexCount, default, default, color, 0f, verts, inds);
                for (var v = 0; v < LineMeshHelpers.VerticesPerLine; v++)
                {
                    _lineVertices[_vertexCount + v] = verts[v];
                }
                for (var t = 0; t < LineMeshHelpers.IndicesPerLine; t++)
                {
                    _lineIndices[_triangleCount * 3 + t] = _vertexCount + inds[t];
                }
                _vertexCount += LineMeshHelpers.VerticesPerLine;
                _triangleCount += LineMeshHelpers.IndicesPerLine / 3;
                
                _vrx[i] *= 0.8F;
                _vry[i] *= 0.8F;
                _vrz[i] *= 0.8F;
                if (_rtg[i] == 9)
                {
                    _rtg[i] = 0;
                    _sparkCount--;
                }
                else
                {
                    _rtg[i]++;
                }
            }
        }
        Sprk = 0;
    }

    public void Render(Camera camera)
    {
        if (_vertexCount == 0 || _triangleCount == 0) return;
        
        // If a parameter is null that means the HLSL compiler optimized it out.
        _material.World?.SetValue(Matrix.Identity);
        _material.WorldInverseTranspose?.SetValue(Matrix.Transpose(Matrix.Invert(Matrix.Identity)));
        _material.SnapColor?.SetValue(new Color3(100, 100, 100).ToVector3());
        _material.IsFullbright?.SetValue(true);
        _material.UseBaseColor?.SetValue(false);
        _material.BaseColor?.SetValue(new Vector3(0, 0, 0));
        _material.ChargedBlinkAmount?.SetValue(0.0f);
        _material.HalfThickness?.SetValue(World.OutlineThickness);

        _material.LightDirection?.SetValue(World.LightDirection);
        _material.FogColor?.SetValue(World.Fog.Snap(World.Snap).ToVector3());
        _material.FogDistance?.SetValue(World.FadeFrom);
        _material.FogDensity?.SetValue(World.FogDensity / (World.FogDensity + 1));
        _material.EnvironmentLight?.SetValue(new Vector2(World.BlackPoint, World.WhitePoint));
        _material.DepthBias?.SetValue(0.00005f);
        _material.GetsShadowed?.SetValue(false);
        _material.Alpha?.SetValue(1f);

        _material.View?.SetValue(camera.ViewMatrix);
        _material.Projection?.SetValue(camera.ProjectionMatrix);
        _material.WorldView?.SetValue(camera.ViewMatrix);
        _material.WorldViewProj?.SetValue(camera.ViewMatrix * camera.ProjectionMatrix);
        _material.CameraPosition?.SetValue(camera.Position);

        _material.CurrentTechnique = _material.Techniques["Basic"];

        _material.Expand?.SetValue(false);
        _material.Darken?.SetValue(1.0f);
        _material.RandomFloat?.SetValue(URandom.Single());

        _material.Glow?.SetValue(false);

        _graphicsDevice.RasterizerState = RasterizerState.CullClockwise;
        foreach (var pass in _material.CurrentTechnique.Passes)
        {
            pass.Apply();

            _graphicsDevice.DrawUserIndexedPrimitives(
                PrimitiveType.TriangleList,
                _lineVertices,
                0,
                _vertexCount,
                _lineIndices,
                0,
                _triangleCount,
                LineMesh.LineMeshVertexAttribute.VertexDeclaration
            );
        }
        _graphicsDevice.RasterizerState = RasterizerState.CullCounterClockwise;
    }
}