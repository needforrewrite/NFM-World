using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Maxine.Extensions.Mathematics;
using NFMWorld.Graphics;
using NFMWorldLibrary;
using BoundingSphere = Microsoft.Xna.Framework.BoundingSphere;

namespace NFMWorld;

/// <summary>
/// Unified render queue that collects both instanced draws (batched by render element)
/// and immediate draws (deferred, sorted by <see cref="SortKey"/>). Replaces the
/// <c>RenderDataCache</c> inner class of <c>Scene</c>.
/// </summary>
public class RenderQueue(IGraphicsDevice graphicsDevice) : IDisposable
{
    private const int InstanceStrideBytes = InstanceData.Stride;

    // ── Instanced queue (migrated from RenderDataCache) ──

    private sealed class CachedInstancedGroup(List<InstanceData> instances)
    {
        public readonly List<InstanceData> Instances = instances;
        public readonly List<InstanceData> OldInstances = [];
        public IBuffer? VertexBuffer;
        public int HashCode;
    }

    private class DrawCallList
    {
        public List<IImmediateRenderElement>? ImmediateDraws;
        public Dictionary<IInstancedRenderElement, CachedInstancedGroup>? InstancedDraws;
    }

    private readonly Dictionary<SortKey, DrawCallList> _draws = new();
    private List<SortKey> _sortKeys = [];

    private readonly List<IInstancedRenderElement> _elementsToPrune = [];

    private Camera? _camera;
    private Lighting? _lighting;

    // ── Public API ──

    /// <summary>
    /// Reset per-frame state. Prunes instanced entries not seen for two consecutive frames.
    /// Must be called at the start of each render pass.
    /// </summary>
    public void Clear()
    {
        foreach (var (_, drawCall) in _draws)
        {
            if (drawCall.InstancedDraws is {} instancedDraws)
            {
                _elementsToPrune.Clear();
                foreach (var (element, group) in instancedDraws)
                {
                    if (group.Instances.Count == 0)
                    {
                        _elementsToPrune.Add(element);
                    }
                    else
                    {
                        CollectionsMarshal.SetCount(group.Instances, 0);
                    }
                }

                foreach (var element in _elementsToPrune)
                {
                    if (drawCall.InstancedDraws.TryGetValue(element, out var group))
                    {
                        group.VertexBuffer?.Dispose();
                        drawCall.InstancedDraws.Remove(element);
                    }
                }
            }

            drawCall.ImmediateDraws?.Clear();
        }
    }

    /// <summary>
    /// Queue an instanced draw. Draws with the same <paramref name="element"/> and
    /// <paramref name="key"/> are batched into a single GPU instanced draw call.
    /// </summary>
    public void AddInstanced(IInstancedRenderElement element, in InstanceData data, SortKey key, BoundingSphere? bsphere = null)
    {
        CheckBegin();

        if (bsphere is { } bsphereValue)
        {
            // Apply frustum culling
            if (!_camera.Frustum.Intersects(bsphereValue))
                return;

            // Apply distance culling
            if (Vector3.DistanceSquared(_camera.Position, bsphereValue.Center) > CameraSettings.RenderDistanceSqr + (bsphereValue.Radius * bsphereValue.Radius))
                return;
        }

        ref var innerCache = ref CollectionsMarshal.GetValueRefOrAddDefault(_draws, key, out var exists);
        if (!exists)
        {
            innerCache = new DrawCallList();
        }

        var instancedDraws = innerCache!.InstancedDraws ??= [];

        ref var entry = ref CollectionsMarshal.GetValueRefOrAddDefault(instancedDraws, element, out var exists1);
        if (!exists1)
        {
            entry = new CachedInstancedGroup([data]);
        }
        else
        {
            entry!.Instances.Add(data);
        }
    }

    /// <summary>
    /// Queue an immediate (non-instanced) draw. Deferred draws are sorted by
    /// <paramref name="sortKey"/> before execution in <see cref="Flush"/>.
    /// </summary>
    public void AddImmediate(SortKey sortKey, IImmediateRenderElement element)
    {
        ref var values = ref CollectionsMarshal.GetValueRefOrAddDefault(_draws, sortKey, out var exists);
        if (!exists)
        {
            values = new DrawCallList();
        }

        var immediateDraws = values!.ImmediateDraws ??= [];
        immediateDraws.Add(element);
    }

    /// <summary>
    /// Execute all queued draws in the correct order, recording into <paramref name="cb"/> - the
    /// single command buffer the caller acquired for this frame/pass (see
    /// <see cref="IGraphicsDevice.AcquireCommandBuffer"/>'s "one at a time" rule; this method
    /// itself never acquires or submits one).
    /// </summary>
    public void Flush(ICommandBuffer cb)
    {
        CheckBegin();

        _sortKeys.Clear();
        _sortKeys.AddRange(_draws.Keys);
        _sortKeys.Sort();

        foreach (var sortKey in _sortKeys)
        {
            var draw = _draws[sortKey];

            // draw immediates
            if (draw.ImmediateDraws is { } immediateDraws)
            {
                foreach (var element in immediateDraws)
                {
                    element.Render(cb, _camera, _lighting);
                }
            }

            // draw instanced batches
            if (draw.InstancedDraws is { } instancedDraws)
            {
                foreach (var (renderElement, cachedGroup) in instancedDraws)
                {
                    var instances = cachedGroup.Instances;
                    if (instances.Count == 0)
                        continue;

                    var oldInstances = cachedGroup.OldInstances;
                    var currentHashCode = GetInstanceDataHashCode(CollectionsMarshal.AsSpan(instances));

                    if (cachedGroup.VertexBuffer == null ||
                        currentHashCode != cachedGroup.HashCode ||
                        !AreInstanceDataListsEqual(
                            CollectionsMarshal.AsSpan(instances),
                            CollectionsMarshal.AsSpan(oldInstances)))
                    {
                        var instanceSpan = CollectionsMarshal.AsSpan(instances);
                        var instanceBytes = MemoryMarshal.AsBytes(instanceSpan);

                        if (cachedGroup.VertexBuffer == null ||
                            cachedGroup.VertexBuffer.SizeInBytes < instances.Count * InstanceStrideBytes)
                        {
                            cachedGroup.VertexBuffer?.Dispose();
                            cachedGroup.VertexBuffer = graphicsDevice.CreateBuffer(
                                new BufferDesc(BufferKind.Vertex, BufferUsage.Dynamic, instances.Count * InstanceStrideBytes),
                                instanceBytes);
                        }
                        else
                        {
                            cb.UpdateBuffer(cachedGroup.VertexBuffer, instanceBytes);
                        }

                        cachedGroup.HashCode = currentHashCode;

                        CollectionsMarshal.SetCount(oldInstances, instances.Count);
                        instanceSpan.CopyTo(CollectionsMarshal.AsSpan(oldInstances));
                    }

                    renderElement.Render(cb, _camera, _lighting, cachedGroup.VertexBuffer, instances.Count);
                }
            }
        }
    }

    [MemberNotNull(nameof(_camera))]
    private void CheckBegin()
    {
        if (_camera is null)
        {
            ThrowNotBeganYet();
        }
        return;

        [DoesNotReturn]
        static void ThrowNotBeganYet()
        {
            throw new InvalidOperationException($"Did not call {nameof(RenderQueue)}.{nameof(Begin)} yet");
        }
    }

    // ── Cleanup ──

    ~RenderQueue()
    {
        Dispose(false);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void Dispose(bool disposing)
    {
        foreach (var (_, innerCache) in _draws)
        {
            if (innerCache.InstancedDraws is { } instances)
            {
                foreach (var (_, group) in instances)
                {
                    group.VertexBuffer?.Dispose();
                }
            }
        }

        if (disposing)
        {
            _draws.Clear();
            _sortKeys.Clear();
        }
    }

    // ── Hash / equality helpers ──

    private static int GetInstanceDataHashCode<T>(ReadOnlySpan<T> data) where T : notnull
    {
        var hc = data.Length;
        foreach (ref readonly var val in data)
        {
            hc = unchecked(hc * 314159 + val.GetHashCode());
        }

        return hc;
    }

    private static bool AreInstanceDataListsEqual<T>(ReadOnlySpan<T> a, ReadOnlySpan<T> b) where T : notnull
    {
        if (a.Length != b.Length)
            return false;

        for (var i = 0; i < a.Length; i++)
        {
            if (!a[i].Equals(b[i]))
                return false;
        }

        return true;
    }

    public void Begin(Camera camera, Lighting? lighting)
    {
        _camera = camera;
        _lighting = lighting;
    }
}
