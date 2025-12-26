using System.Buffers;
using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LuzFaltex.Core.Collections;
using Microsoft.Xna.Framework.Graphics;

namespace NFMWorld.Mad;

public class Scene
{
    private readonly GraphicsDevice _graphicsDevice;
    private readonly Camera _camera;
    private readonly Camera[] _lightCameras;
    public readonly List<GameObject> Objects;
    private readonly RenderDataCache _renderDataCache;

    public Scene(GraphicsDevice graphicsDevice, IEnumerable<GameObject> objects, Camera camera, Camera[] lightCameras)
    {
        _graphicsDevice = graphicsDevice;
        _camera = camera;
        _lightCameras = lightCameras;
        Objects = [..objects];
        _renderDataCache = new RenderDataCache(graphicsDevice);
    }

    public void Render(bool useShadowMapping)
    {
        _camera.OnBeforeRender();
        foreach (var lightCamera in _lightCameras)
        {
            lightCamera.OnBeforeRender();
        }
        
        foreach (var renderable in Objects)
        {
            renderable.OnBeforeRender();
        }
        
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.DepthStencilState = DepthStencilState.Default;

        // CREATE SHADOW MAP

        if (useShadowMapping)
        {
            for (var cascade = 0; cascade < Math.Min(_lightCameras.Length, Program.shadowRenderTargets.Length); cascade++)
            {
                // Set our render target to our floating point render target
                _graphicsDevice.SetRenderTarget(Program.shadowRenderTargets[cascade]);

                // Clear the render target to white or all 1's
                // We set the clear to white since that represents the 
                // furthest the object could be away
                _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.White);

                RenderInternal(true, cascade);
            }
            
            _graphicsDevice.SetRenderTarget(null);
        }

        // DRAW WITH SHADOW MAP
        
        _graphicsDevice.Clear(Microsoft.Xna.Framework.Color.CornflowerBlue);

        _graphicsDevice.SamplerStates[1] = SamplerState.PointClamp;
        _graphicsDevice.SamplerStates[2] = SamplerState.PointClamp;
        _graphicsDevice.SamplerStates[3] = SamplerState.PointClamp;

        RenderInternal();

    }
    
    private class RenderDataCache(GraphicsDevice graphicsDevice) : IEnumerable<(DynamicVertexBuffer Buffer, int InstanceCount, IInstancedRenderElement Element)>
    {
        private class CachedRenderData(
            List<RenderData> renderData
        )
        {
            public List<RenderData> RenderData = renderData;
            public List<RenderData> OldRenderData = [];
            public DynamicVertexBuffer? VertexBuffer = null;
            public int HashCode = 0;
        }

        private Dictionary<IInstancedRenderElement, CachedRenderData> _cache = new();

        private static int GetHashCode(ReadOnlySpan<RenderData> renderData)
        {
            var hc = renderData.Length;
            foreach (var val in renderData)
            {
                hc = unchecked(hc * 314159 + val.GetHashCode());
            }
            return hc;
        }
        
        private static bool AreRenderDataListsEqual(ReadOnlySpan<RenderData> a, ReadOnlySpan<RenderData> b, int aHashCode, int bHashCode)
        {
            if (aHashCode != bHashCode)
                return false;
            if (a.Length != b.Length)
                return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i]))
                    return false;
            }
            return true;
        }

        private readonly List<IInstancedRenderElement> _elementsToPrune = new();
        public void Clear()
        {
            // Delete any instance not rendered for two consecutive frames.
            foreach (var (element, data) in _cache)
            {
                if (data.RenderData.Count == 0)
                {
                    _elementsToPrune.Add(element);
                }
                else
                {
                    CollectionsMarshal.SetCount(data.RenderData, 0);
                }
            }
            
            foreach (var element in _elementsToPrune)
            {
                if (_cache.TryGetValue(element, out var data))
                {
                    data.VertexBuffer?.Dispose();
                    _cache.Remove(element);
                }
            }
        }

        public void Add(RenderData renderData)
        {
            ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(_cache, renderData.RenderElement, out var exists);
            if (!exists)
            {
                entry = new CachedRenderData([renderData]);
            }
            else
            {
                entry!.RenderData.Add(renderData);
            }
        }

        public IEnumerator<(DynamicVertexBuffer Buffer, int InstanceCount, IInstancedRenderElement Element)> GetEnumerator()
        {
            foreach (var (renderElement, cachedRenderData) in _cache)
            {
                var instances = cachedRenderData.RenderData;
                if (instances.Count == 0) continue;
                
                var oldInstances = cachedRenderData.OldRenderData;
                
                var currentHashCode = GetHashCode(CollectionsMarshal.AsSpan(instances));
                var oldHashCode = cachedRenderData.HashCode;
                
                if (cachedRenderData.VertexBuffer == null ||
                    !AreRenderDataListsEqual(
                        CollectionsMarshal.AsSpan(instances),
                        CollectionsMarshal.AsSpan(oldInstances),
                        currentHashCode,
                        oldHashCode
                    ))
                {
                    using var instanceDataArray = MemoryPool<InstanceData>.Shared.Rent(instances.Count);
                    var instanceDataArraySpan = instanceDataArray.Memory.Span.Slice(0, instances.Count);
                    var i = 0;
                    foreach (var renderData in instances)
                    {
                        instanceDataArraySpan[i++] = renderData.ToInstanceData();
                    }

                    if (cachedRenderData.VertexBuffer == null || cachedRenderData.VertexBuffer.VertexCount < instances.Count)
                    {
                        cachedRenderData.VertexBuffer?.Dispose();
                        cachedRenderData.VertexBuffer = new DynamicVertexBuffer(graphicsDevice, InstanceData.InstanceDeclaration, instances.Count, BufferUsage.WriteOnly);
                    }

                    cachedRenderData.VertexBuffer.SetDataEXT(instanceDataArraySpan, SetDataOptions.NoOverwrite);
                    cachedRenderData.HashCode = currentHashCode;
                }
                yield return (cachedRenderData.VertexBuffer, instances.Count, renderElement);
            }
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
    
    private void RenderInternal(bool isCreateShadowMap = false, int numCascade = -1)
    {
        var lighting = new Lighting(_lightCameras, Program.shadowRenderTargets, isCreateShadowMap, numCascade);

        _renderDataCache.Clear();
        foreach (var obj in Objects)
        {
            obj.Render(_camera, lighting);
            
            foreach (var renderData in obj.GetRenderData(lighting))
            {
                _renderDataCache.Add(renderData);
            }
        }

        foreach (var (buffer, instanceCount, element) in _renderDataCache)
        {
            element.Render(_camera, lighting, buffer, instanceCount);
        }
    }
}