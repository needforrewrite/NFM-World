using GraphicsDevice = nfm_world.compat.GraphicsDeviceCompat;
using nfm_world.compat;
using System.Buffers;
using System.Collections;
using System.Runtime.InteropServices;
using MoonWorks.Graphics;
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
        if (cmd == null || backbuffer == null || mainDepth == null) return;

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

        // Upload instance data that changed via CopyPass
        var copyPass = cmd.BeginCopyPass();
        _renderDataCache.PrepareAndUpload(copyPass);

        // Gather and flush all per-frame buffer uploads in one CopyPass
        foreach (var obj in Objects)
        {
            obj.UploadBuffers(copyPass);
        }
        cmd.EndCopyPass(copyPass);

        // ── Shadow map passes ───────────────────────────────────────
        if (useShadowMapping)
        {
            for (var cascade = 0; cascade < Math.Min(_lightCameras.Length, Program.shadowRenderTargets.Length); cascade++)
            {
                var shadowColor = new ColorTargetInfo
                {
                    Texture = Program.shadowRenderTargets[cascade].Texture,
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.Store,
                    ClearColor = new MoonWorks.Graphics.Color(255, 255, 255, 255)
                };
                var shadowDepth = new DepthStencilTargetInfo
                {
                    Texture = Program.shadowDepthTargets[cascade],
                    LoadOp = LoadOp.Clear,
                    StoreOp = StoreOp.DontCare,
                    StencilLoadOp = LoadOp.DontCare,
                    StencilStoreOp = StoreOp.DontCare,
                    ClearDepth = 1.0f
                };

                var pass = cmd.BeginRenderPass(shadowDepth, shadowColor);
                RenderState.Pass = pass;

                var lighting = new Lighting(_lightCameras, Program.shadowRenderTargets, true, cascade);
                RenderInternal(lighting);

                cmd.EndRenderPass(pass);
            }
        }

        // ── Main pass ───────────────────────────────────────────────
        {
            var mainColor = new ColorTargetInfo
            {
                Texture = backbuffer,
                LoadOp = clearRenderBuffer ? LoadOp.Clear : LoadOp.Load,
                StoreOp = StoreOp.Store,
                ClearColor = new MoonWorks.Graphics.Color(
                    (byte)Math.Clamp((int)World.Sky.R, 0, 255),
                    (byte)Math.Clamp((int)World.Sky.G, 0, 255),
                    (byte)Math.Clamp((int)World.Sky.B, 0, 255), (byte)255)
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

            var lighting = new Lighting(_lightCameras, Program.shadowRenderTargets);
            RenderInternal(lighting);

            cmd.EndRenderPass(pass);
        }

        RenderState.Pass = null;
        RenderState.SceneRenderedThisFrame = true;
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
            public TransferBuffer? TransferBuffer = null;
            public int BufferCapacity = 0;
            public int HashCode = 0;
            public bool NeedsUpload = false;
        }

        private SortedDictionary<int, Dictionary<IInstancedRenderElement, CachedRenderData>> _cache = new();
        private List<CachedRenderData> _entriesToUpload = new();

        ~RenderDataCache()
        {
            foreach (var (_, innerCache) in _cache)
            foreach (var (_, data) in innerCache)
            {
                data.GpuBuffer?.Dispose();
                data.TransferBuffer?.Dispose();
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
                        data.TransferBuffer?.Dispose();
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
        /// map and fill transfer buffers, then upload all changed data via a single CopyPass.
        /// </summary>
        public void PrepareAndUpload(CopyPass copyPass)
        {
            _entriesToUpload.Clear();

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
                    if (cachedRenderData.GpuBuffer == null || cachedRenderData.BufferCapacity < instances.Count)
                    {
                        cachedRenderData.GpuBuffer?.Dispose();
                        cachedRenderData.TransferBuffer?.Dispose();
                        cachedRenderData.BufferCapacity = instances.Count;
                        cachedRenderData.GpuBuffer = GpuBuffer.Create<InstanceData>(
                            graphicsDevice, BufferUsageFlags.Vertex, (uint)instances.Count);
                        cachedRenderData.TransferBuffer = TransferBuffer.Create<InstanceData>(
                            graphicsDevice, TransferBufferUsage.Upload, (uint)instances.Count);
                    }

                    // Fill transfer buffer
                    var span = cachedRenderData.TransferBuffer.Map<InstanceData>(false);
                    for (var i = 0; i < instances.Count; i++)
                    {
                        span[i] = instances[i].ToInstanceData();
                    }
                    cachedRenderData.TransferBuffer.Unmap();

                    cachedRenderData.NeedsUpload = true;
                    cachedRenderData.HashCode = currentHashCode;
                    
                    CollectionsMarshal.SetCount(oldInstances, instances.Count);
                    CollectionsMarshal.AsSpan(instances).CopyTo(CollectionsMarshal.AsSpan(oldInstances));

                    _entriesToUpload.Add(cachedRenderData);
                }
            }

            // Upload all changed instance data in one CopyPass
            if (_entriesToUpload.Count > 0)
            {
                foreach (var entry in _entriesToUpload)
                {
                    copyPass.UploadToBuffer<InstanceData>(
                        entry.TransferBuffer,
                        entry.GpuBuffer,
                        0, 0, (uint)entry.RenderData.Count,
                        true);
                    entry.NeedsUpload = false;
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