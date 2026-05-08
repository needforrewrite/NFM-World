using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world_library.util;
using nfm_world.camera;
using nfm_world.gameobject;
using nfm_world.renderable.mesh.utils;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world.renderable.mesh.render_elements.effects;

public class Sparks : IDisposable
{
    private readonly ClientCar _car;
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
    private int _sparkCount;
    private readonly GpuBuffer _vertexBuffer;
    private readonly GpuBuffer _indexBuffer;
    private readonly GpuBuffer _instanceBuffer;

    public Sparks(ClientCar car, GraphicsDevice graphicsDevice)
    {
        _car = car;
        _graphicsDevice = graphicsDevice;

        _sprkat = _car.Wheels.FirstOrDefault().Sparkat;

        _vertexBuffer = GpuBuffer.Create<LineMesh.LineMeshVertexAttribute>(graphicsDevice, BufferUsageFlags.Vertex,
            (uint)(100 * LineMeshHelpers.VerticesPerLine));
        _indexBuffer = GpuBuffer.Create<int>(graphicsDevice, BufferUsageFlags.Index,
            (uint)(100 * LineMeshHelpers.IndicesPerLine));

        // Single identity instance for instanced rendering
        _instanceBuffer = GpuBuffer.Create<InstanceData>(graphicsDevice, BufferUsageFlags.Vertex, 1);
        var instTransfer = TransferBuffer.Create<InstanceData>(graphicsDevice, TransferBufferUsage.Upload, 1);
        var instSpan = instTransfer.Map<InstanceData>(false);
        instSpan[0] = new InstanceData(Matrix.Identity);
        instTransfer.Unmap();
        var instCmd = graphicsDevice.AcquireCommandBuffer();
        var instCopy = instCmd.BeginCopyPass();
        instCopy.UploadToBuffer(
            new TransferBufferLocation(instTransfer, 0),
            new BufferRegion(_instanceBuffer, 0, (uint)Marshal.SizeOf<InstanceData>()),
            false);
        instCmd.EndCopyPass(instCopy);
        graphicsDevice.Submit(instCmd);
        instTransfer.Dispose();
    }
    
    ~Sparks()
    {
        Dispose(false);
    }

    public void AddSpark(float wheelx, float wheely, float wheelz, float scx, float scy, float scz, int type, int wheelGround)
    {
        if (type != 1)
        {
            Srx = (wheelx - _sprkat * UMath.SinUnsafe((float)_car.Rotation.Xz.Degrees));
            Sry = (wheely - wheelGround - _sprkat * UMath.CosUnsafe((float)_car.Rotation.Zy.Degrees) * UMath.CosUnsafe((float)_car.Rotation.Xy.Degrees));
            Srz = (wheelz + _sprkat * UMath.CosUnsafe((float)_car.Rotation.Xz.Degrees));
            Sprk = 1;
        }
        else
        {
            Sprk++;
            if (Sprk == 4)
            {
                Srx = ((float)_car.Position.X + scx);
                Sry = wheely - wheelGround;
                Srz = ((float)_car.Position.Z + scz);
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

    private int _tick;

    public void GameTick()
    {
        if (++_tick == Physics
                .OriginalTicksPerNewTick) // delay all operations by 3 ticks because of the adjusted tickrate
        {
            _vertexCount = 0;
            _triangleCount = 0;

            if (Sprk != 0)
            {
                var i = (int)(Math.Sqrt(Rcx * Rcx + Rcy * Rcy + Rcz * Rcz) / 10.0);
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

            Span<LineMesh.LineMeshVertexAttribute> verts =
                stackalloc LineMesh.LineMeshVertexAttribute[LineMeshHelpers.VerticesPerLine];
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

            if (_vertexCount > 0 && _triangleCount > 0)
            {
                WorldGame.ResourceUploader.SetBufferData(_vertexBuffer, 0, _lineVertices.AsSpan(0, _vertexCount));
                WorldGame.ResourceUploader.SetBufferData(_indexBuffer, 0, _lineIndices.AsSpan(0, _triangleCount * 3));
            }

            Sprk = 0;

            _tick = 0;
        }
    }

    public void Render(Camera camera)
    {
        if (_vertexCount == 0 || _triangleCount == 0) return;

        var cmd = RenderState.Cmd;
        var pass = RenderState.Pass;
        if (cmd == null || pass == null) return;

        var fog = new FogParams
        {
            Color = (Vector3)World.Fog.Snap(World.Snap),
            Distance = World.FadeFrom,
            Density = World.FogDensity / (World.FogDensity + 1f)
        };

        cmd.PushVertexUniformData(new LineVertexUniforms
        {
            View = camera.ViewMatrix,
            Projection = camera.ProjectionMatrix,
            ViewProj = camera.ViewMatrix * camera.ProjectionMatrix,
            CameraPosition = camera.Position,
            Alpha = 1f,
            SnapColor = (Vector3)new Color3(100, 100, 100),
            Darken = 1.0f,
            LightDirection = World.LightDirection,
            RandomFloat = URandom.Single(),
            EnvironmentLight = new Vector2(World.BlackPoint, World.WhitePoint),
            IsFullbright = true,
            UseBaseColor = false,
            BaseColor = Vector3.Zero,
            Expand = false,
            Fog = fog,
            HalfThickness = World.OutlineThickness,
            ChargedBlinkAmount = 0f
        });

        cmd.PushFragmentUniformData(new LineFragUniforms
        {
            Shadow = default
        });

        pass.BindGraphicsPipeline(Pipelines.Line);
        pass.BindVertexBuffers(
            new BufferBinding(_vertexBuffer, 0),
            new BufferBinding(_instanceBuffer, 0));
        pass.BindIndexBuffer(new BufferBinding(_indexBuffer, 0), IndexElementSize.ThirtyTwo);
        pass.DrawIndexedPrimitives((uint)(_triangleCount * 3), 1, 0, 0, 0);
    }

    private void ReleaseUnmanagedResources()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
        _instanceBuffer.Dispose();
    }

    private void Dispose(bool disposing)
    {
        ReleaseUnmanagedResources();
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}