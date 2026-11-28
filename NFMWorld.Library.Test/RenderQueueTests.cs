using System.Runtime.InteropServices;
using NFMWorld.Graphics;
using BoundingSphere = Maxine.Extensions.Mathematics.BoundingSphere;

namespace NFMWorld.Library.Test
{
    [TestClass]
    public class RenderQueueTests
    {
        private static readonly SortKey Key = SortKey.Create(RenderBucket.StagePieces);

        [TestMethod]
        public void MainOnlyAndShadowOnlyGroupsSurviveMultiplePasses()
        {
            using var device = new RecordingDevice();
            using var queue = new RenderQueue(device);
            var main = new RecordingElement();
            var shadow = new RecordingElement();
            queue.Begin(new TestCamera(), null);

            for (var frame = 0; frame < 5; frame++)
            {
                queue.Clear();
                Add(queue, shadow, 2, 10);
                queue.Flush(device.Commands);
                queue.Clear(beginFrame: false);
                queue.Flush(device.Commands);
                queue.Clear(beginFrame: false);
                Add(queue, main, 3, 20);
                queue.Flush(device.Commands);
            }

            Assert.HasCount(2, device.Buffers);
            Assert.HasCount(5, main.Counts);
            Assert.HasCount(5, shadow.Counts);
            CollectionAssert.AreEqual(new[] { 3, 3, 3, 3, 3 }, main.Counts);
            CollectionAssert.AreEqual(new[] { 2, 2, 2, 2, 2 }, shadow.Counts);
            Assert.IsTrue(device.Buffers.All(buffer => !buffer.Disposed));
        }

        [TestMethod]
        public void CapacityGrowsGeometricallyButDrawAndUploadUseActiveCount()
        {
            using var device = new RecordingDevice();
            using var queue = new RenderQueue(device);
            var element = new RecordingElement();
            queue.Begin(new TestCamera(), null);

            foreach (var count in new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9, 3, 0, 2 })
            {
                queue.Clear();
                Add(queue, element, count, count * 10);
                queue.Flush(device.Commands);
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4, 5, 6, 8, 9, 3, 2 }, element.Counts);
            Assert.IsTrue(device.Buffers.Count <= 5, "Incremental count changes should not recreate the buffer every frame.");
            Assert.IsTrue(device.Buffers.Take(device.Buffers.Count - 1).All(buffer => buffer.Disposed));
            Assert.IsFalse(device.Buffers[^1].Disposed);
            CollectionAssert.AreEqual(new[] { 20f, 21f }, element.LastData.Select(i => i.World.M41).ToArray());
            Assert.AreEqual(2 * InstanceData.Stride, device.Commands.LastUploadLength);
        }

        [TestMethod]
        public void SameGroupReuploadsDifferentPassDataAndShrinkingCounts()
        {
            using var device = new RecordingDevice();
            using var queue = new RenderQueue(device);
            var element = new RecordingElement();
            queue.Begin(new TestCamera(), null);
            queue.Clear();
            Add(queue, element, 4, 10);
            queue.Flush(device.Commands);
            var buffer = element.LastBuffer;
            queue.Clear(beginFrame: false);
            Add(queue, element, 1, 99);
            queue.Flush(device.Commands);

            Assert.AreSame(buffer, element.LastBuffer);
            CollectionAssert.AreEqual(new[] { 4, 1 }, element.Counts);
            Assert.AreEqual(99f, element.LastData[0].World.M41);
            Assert.AreEqual(InstanceData.Stride, device.Commands.LastUploadLength);
            Assert.HasCount(1, device.Buffers);
        }

        [TestMethod]
        public void UnusedGroupsExpireByFramesNotPassesAndDisposeIsImmediate()
        {
            using var device = new RecordingDevice();
            using var queue = new RenderQueue(device);
            var element = new RecordingElement();
            queue.Begin(new TestCamera(), null);
            queue.Clear();
            Add(queue, element, 1, 0);
            queue.Flush(device.Commands);
            var first = device.Buffers[0];

            for (var frame = 0; frame < 2; frame++)
            {
                queue.Clear();
                for (var pass = 0; pass < 4; pass++)
                    queue.Clear(beginFrame: false);
                Assert.IsFalse(first.Disposed);
            }

            queue.Clear();
            Assert.IsTrue(first.Disposed);
            Add(queue, element, 1, 0);
            queue.Flush(device.Commands);
            queue.Dispose();
            Assert.IsTrue(device.Buffers.All(buffer => buffer.Disposed));
            Assert.IsTrue(device.Buffers.All(buffer => buffer.DisposeCount == 1));
        }

        [TestMethod]
        public void CulledInstancesDoNotChangeCountsOrKeepExpiredResourcesAlive()
        {
            using var device = new RecordingDevice();
            using var queue = new RenderQueue(device);
            var element = new RecordingElement();
            queue.Begin(new TestCamera(), null);
            queue.Clear();
            Add(queue, element, 1, 0);
            queue.Flush(device.Commands);

            for (var frame = 0; frame < 3; frame++)
            {
                queue.Clear();
                queue.AddInstanced(
                    element,
                    new InstanceData(Matrix.Identity),
                    Key,
                    new BoundingSphere(new Vector3(100, 100, 100), 1)
                );
                queue.Flush(device.Commands);
            }

            CollectionAssert.AreEqual(new[] { 1 }, element.Counts);
            Assert.IsTrue(device.Buffers[0].Disposed);
        }

        private static void Add(RenderQueue queue, RecordingElement element, int count, int offset)
        {
            for (var i = 0; i < count; i++)
                queue.AddInstanced(element, new InstanceData(Matrix.CreateTranslation(offset + i, 0, 0)), Key);
        }

        private sealed class TestCamera : Camera
        {
            public TestCamera() => ViewProjectionMatrix = Matrix.Identity;
            public override void OnBeforeRender(float alpha) => ViewProjectionMatrix = Matrix.Identity;
        }

        private sealed class RecordingElement : IInstancedRenderElement
        {
            public readonly List<int> Counts = [];
            public IBuffer? LastBuffer;
            public InstanceData[] LastData = [];

            public void Render(ICommandBuffer cb, Camera camera, Lighting? lighting, IBuffer instanceBuffer, int instanceCount)
            {
                var buffer = (RecordingBuffer)instanceBuffer;
                Assert.IsFalse(buffer.Disposed);
                Assert.IsTrue(instanceCount * InstanceData.Stride <= buffer.SizeInBytes);
                Counts.Add(instanceCount);
                LastBuffer = buffer;
                LastData = MemoryMarshal.Cast<byte, InstanceData>(buffer.Data.AsSpan(0, instanceCount * InstanceData.Stride))
                    .ToArray();
            }
        }

        private sealed class RecordingBuffer(BufferDesc desc) : IBuffer
        {
            public BufferKind Kind => desc.Kind;
            public BufferUsage Usage => desc.Usage;
            public int SizeInBytes => desc.SizeInBytes;
            public readonly byte[] Data = new byte[desc.SizeInBytes];
            public bool Disposed;
            public int DisposeCount;

            public void Dispose()
            {
                Disposed = true;
                DisposeCount++;
            }
        }

        private sealed class RecordingDevice : IGraphicsDevice
        {
            public readonly List<RecordingBuffer> Buffers = [];
            public readonly RecordingCommands Commands = new();
            public ISwapchain Swapchain => throw new NotSupportedException();
            public bool HasBottomLeftFramebufferOrigin => false;
            public bool SupportsBaseVertexDraw => true;
            public ICommandBuffer AcquireCommandBuffer() => throw new NotSupportedException();
            public void Submit(ICommandBuffer cb) => throw new NotSupportedException();

            public IBuffer CreateBuffer(BufferDesc desc, ReadOnlySpan<byte> initialData = default)
            {
                var buffer = new RecordingBuffer(desc);
                initialData.CopyTo(buffer.Data);
                Buffers.Add(buffer);

                return buffer;
            }

            public ITexture CreateTexture(TextureDesc desc, ReadOnlySpan<byte> data = default) => throw new NotSupportedException(
            );

            public IRenderTarget CreateRenderTarget(RenderTargetDesc desc) => throw new NotSupportedException();
            public ISampler CreateSampler(SamplerDesc desc) => throw new NotSupportedException();
            public IPipelineState CreatePipeline(PipelineDesc desc) => throw new NotSupportedException();
            public void ReadTexture(ITexture texture, int x, int y, int width, int height, Span<byte> destination, int level = 0)
                => throw new NotSupportedException();

            public void Dispose()
            {
            }
        }

        private sealed class RecordingCommands : ICommandBuffer
        {
            public int LastUploadLength;

            public void UpdateBuffer(IBuffer buffer, ReadOnlySpan<byte> data, int offsetBytes = 0)
            {
                var recording = (RecordingBuffer)buffer;
                Assert.IsFalse(recording.Disposed);
                data.CopyTo(recording.Data.AsSpan(offsetBytes));
                LastUploadLength = data.Length;
            }

            public void SetPipeline(IPipelineState pipeline) => throw new NotSupportedException();
            public void SetVertexBuffer(int slot, IBuffer buffer, int strideBytes, int offsetBytes = 0) => throw new NotSupportedException(
            );

            public void SetIndexBuffer(IBuffer buffer, int offsetBytes = 0) => throw new NotSupportedException();
            public void SetShaderResource(int slot, ITexture texture, ISampler sampler) => throw new NotSupportedException();
            public void SetUniform(int slot, ReadOnlySpan<byte> value) => throw new NotSupportedException();
            public void SetRenderTarget(IRenderTarget? target) => throw new NotSupportedException();
            public void SetViewport(Viewport viewport) => throw new NotSupportedException();
            public void SetScissorRect(ScissorRect rect) => throw new NotSupportedException();
            public void UpdateTexture(ITexture texture, int x, int y, int width, int height, ReadOnlySpan<byte> data) => throw new NotSupportedException(
            );

            public void Clear(ClearOptions options, ColorRgba color, float depth = 1, int stencil = 0) => throw new NotSupportedException(
            );

            public void Draw(int startVertex, int primitiveCount) => throw new NotSupportedException();
            public void DrawIndexed(int baseVertex, int startIndex, int primitiveCount) => throw new NotSupportedException();
            public void DrawIndexedInstanced(int baseVertex, int startIndex, int primitiveCount, int instanceCount) => throw new NotSupportedException(
            );
        }
    }
}

namespace NFMWorld
{
    // Only these unused signatures/configuration are replaced; the queue, camera and instance data are real.
    public sealed class Lighting
    {
    }

    public static class CameraSettings
    {
        public const float RenderDistanceSqr = float.MaxValue;
    }
}
