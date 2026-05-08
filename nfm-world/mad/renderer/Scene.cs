using GraphicsDevice = nfm_world.compat.GraphicsDeviceCompat;
using nfm_world.compat;
using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
using nfm_world_library;
using nfm_world_library.mad;
using nfm_world.camera;
using nfm_world.gameobject;
using nfm_world.renderable.mesh.render_elements;
using GpuBuffer = MoonWorks.Graphics.Buffer;

namespace nfm_world;

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
    
    public void Render(bool useShadowMapping, bool clearRenderBuffer = true)
    {
        var cmd = RenderState.Cmd;
        var backbuffer = RenderState.Backbuffer;
        var mainDepth = RenderState.MainDepthTexture;

        _camera.OnBeforeRender();
        foreach (var lightCamera in _lightCameras)
        {
            lightCamera.OnBeforeRender();
        }
        
        foreach (var renderable in Objects)
        {
            renderable.OnBeforeRender();
        }

        // Gather render data and prepare instance buffers
        _renderDataCache.Clear();
        foreach (var obj in Objects)
        {
            foreach (var renderData in obj.GetRenderData(null))
            {
                _renderDataCache.Add(renderData);
            }
        }

        // Upload changed instance data
        _renderDataCache.PrepareAndUpload();

        // ── Shadow map passes ───────────────────────────────────────
        if (useShadowMapping)
        {
            for (var cascade = 0; cascade < Math.Min(_lightCameras.Length, WorldGame.shadowRenderTargets.Length); cascade++)
            {
                var shadowColor = new ColorTargetInfo
                {
                    Texture = WorldGame.shadowRenderTargets[cascade].Texture,
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.Store,
                    ClearColor = new MoonWorks.Graphics.Color(255, 255, 255, 255)
                };
                var shadowDepth = new DepthStencilTargetInfo
                {
                    Texture = WorldGame.shadowDepthTargets[cascade],
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.DontCare,
                    StencilLoadOp = LoadOp.DontCare,
                    StencilStoreOp = StoreOp.DontCare,
                    ClearDepth = 1.0f
                };

                var pass = cmd.BeginRenderPass(shadowDepth, shadowColor);
                RenderState.Pass = pass;

                var lighting = new Lighting(_lightCameras, WorldGame.shadowRenderTargets, true, cascade);
                RenderInternal(lighting);

                cmd.EndRenderPass(pass);
            }
        }

        // ── Main pass ───────────────────────────────────────────────
        {
            Vector3 col = World.Sky.Snap(World.Snap);
            for (var i = 1; i < 20; ++i) {
                col = new Vector3(0.991f, 0.991f, 0.998f) * col;
            }
            var mainColor = new ColorTargetInfo
            {
                Texture = backbuffer,
                LoadOp = clearRenderBuffer ? LoadOp.Clear : LoadOp.Load,
                StoreOp = StoreOp.Store,
                ClearColor = new MoonWorks.Graphics.Color(
                    (byte)Math.Clamp((int)(col.X * 255), 0, 255),
                    (byte)Math.Clamp((int)(col.Y * 255), 0, 255),
                    (byte)Math.Clamp((int)(col.Z * 255), 0, 255)
                )
            };
            var mainDepthInfo = new DepthStencilTargetInfo
            {
                Texture = mainDepth,
                LoadOp = LoadOp.Clear,
                StoreOp = StoreOp.DontCare,
                StencilLoadOp = LoadOp.DontCare,
                StencilStoreOp = StoreOp.DontCare,
                ClearDepth = 1.0f
            };

            var pass = cmd.BeginRenderPass(mainDepthInfo, mainColor);
            RenderState.Pass = pass;

            var lighting = new Lighting(_lightCameras, WorldGame.shadowRenderTargets);
            RenderInternal(lighting);

            cmd.EndRenderPass(pass);
        }

        RenderState.Pass = null;
    }
    
    private class RenderDataCache(GraphicsDevice graphicsDevice)
    {
        private class CachedRenderData(
            List<RenderData> renderData
        )
        {
            public List<RenderData> RenderData = renderData;
            public List<RenderData> OldRenderData = [];
            public GpuBuffer? GpuBuffer = null;
            public int BufferCapacity = 0;
            public int HashCode = 0;
        }

        private SortedDictionary<int, Dictionary<IInstancedRenderElement, CachedRenderData>> _cache = new();

        ~RenderDataCache()
        {
            foreach (var (_, innerCache) in _cache)
            foreach (var (_, data) in innerCache)
            {
                data.GpuBuffer?.Dispose();
            }
        }

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
            if (aHashCode != bHashCode) return false;
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
            {
                if (!a[i].Equals(b[i])) return false;
            }
            return true;
        }

        private readonly List<IInstancedRenderElement> _elementsToPrune = new();
        public void Clear()
        {
            foreach (var (renderOrder, innerCache) in _cache)
            {
                _elementsToPrune.Clear();

                foreach (var (element, data) in innerCache)
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
                    if (innerCache.TryGetValue(element, out var data))
                    {
                        data.GpuBuffer?.Dispose();
                        innerCache.Remove(element);
                    }
                }
            }
        }

        public void Add(RenderData renderData)
        {
            if (!_cache.TryGetValue(renderData.RenderOrder, out var innerCache))
            {
                _cache[renderData.RenderOrder] = innerCache = new Dictionary<IInstancedRenderElement, CachedRenderData>();
            }
            
            ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(innerCache, renderData.RenderElement, out var exists);
            if (!exists)
            {
                entry = new CachedRenderData([renderData]);
            }
            else
            {
                entry!.RenderData.Add(renderData);
            }
        }

        /// <summary>
        /// Prepare instance data for upload: check for changes, create/resize GPU buffers,
        /// map and fill transfer buffers, then upload via ResourceUploader.
        /// </summary>
        public void PrepareAndUpload()
        {
            foreach (var (renderOrder, innerCache) in _cache)
            foreach (var (renderElement, cachedRenderData) in innerCache)
            {
                var instances = cachedRenderData.RenderData;
                if (instances.Count == 0) continue;
                
                var oldInstances = cachedRenderData.OldRenderData;
                var currentHashCode = GetHashCode(CollectionsMarshal.AsSpan(instances));
                var oldHashCode = cachedRenderData.HashCode;
                
                if (cachedRenderData.GpuBuffer == null ||
                    !AreRenderDataListsEqual(
                        CollectionsMarshal.AsSpan(instances),
                        CollectionsMarshal.AsSpan(oldInstances),
                        currentHashCode,
                        oldHashCode))
                {
                    // Resize buffers if needed
                    Span<InstanceData> data;
                    if (cachedRenderData.GpuBuffer == null || cachedRenderData.BufferCapacity < instances.Count)
                    {
                        cachedRenderData.GpuBuffer?.Dispose();
                        cachedRenderData.BufferCapacity = instances.Count;
                        cachedRenderData.GpuBuffer = WorldGame.ResourceUploader.CreateBufferAndMap((uint)instances.Count, BufferUsageFlags.Vertex, out data);
                    }
                    else
                    {
                        data = WorldGame.ResourceUploader.MapBufferData<InstanceData>(cachedRenderData.GpuBuffer, 0, (uint)instances.Count);
                    }
                    
                    // Fill transfer buffer
                    for (var i = 0; i < instances.Count; i++)
                    {
                        data[i] = instances[i].ToInstanceData();
                    }

                    cachedRenderData.HashCode = currentHashCode;
                    
                    CollectionsMarshal.SetCount(oldInstances, instances.Count);
                    CollectionsMarshal.AsSpan(instances).CopyTo(CollectionsMarshal.AsSpan(oldInstances));
                }
            }
        }

        /// <summary>
        /// Iterate over all render entries with their GPU instance buffers.
        /// </summary>
        public IEnumerable<(GpuBuffer Buffer, int InstanceCount, IInstancedRenderElement Element)> GetEntries()
        {
            foreach (var (renderOrder, innerCache) in _cache)
            foreach (var (renderElement, cachedRenderData) in innerCache)
            {
                if (cachedRenderData.RenderData.Count == 0 || cachedRenderData.GpuBuffer == null) continue;
                yield return (cachedRenderData.GpuBuffer, cachedRenderData.RenderData.Count, renderElement);
            }
        }
    }
    
    private void RenderInternal(Lighting lighting)
    {
        // Render immediate objects (Sky, Ground, Mountains, etc.)
        foreach (var obj in Objects)
        {
            obj.Render(_camera, lighting);
        }

        // Render instanced objects (Submesh, LineMesh)
        foreach (var (buffer, instanceCount, element) in _renderDataCache.GetEntries())
        {
            element.Render(_camera, lighting, buffer, instanceCount);
        }
    }

    public void GameTick(IStage currentStage)
    {
        foreach (var obj in Objects)
        {
            obj.GameTick(currentStage);
        }
    }
}