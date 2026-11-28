// LLM maintained.
//
// Small helpers the WebGPU backend's types share: unmanaged UTF-8 strings, pointer reads and the
// binding-order table the shader compiler and this backend have to agree on.

using System.Runtime.InteropServices;
using Wg = Maxine.WebGPU;

namespace NFMWorld.Graphics.WebGPU;

internal static unsafe class WebGpuInterop
{
    /// <summary>
    /// A NUL-terminated UTF-8 string in unmanaged memory.
    ///
    /// WebGPU takes labels and entry points as <c>char*</c> and copies them during the call, so the
    /// buffer only has to outlive the statement that creates the descriptor - which is why this is a
    /// disposable value rather than something the device pins.
    /// </summary>
    internal readonly ref struct Utf8
    {
        private readonly nint _memory;

        internal Utf8(string? text)
        {
            _memory = text is null ? 0 : Marshal.StringToCoTaskMemUTF8(text);
            Length = text is null ? 0 : System.Text.Encoding.UTF8.GetByteCount(text);
        }

        internal byte* Pointer => (byte*)_memory;

        /// <summary>The string as the API takes one: a pointer and a byte count, no terminator.</summary>
        internal Wg.StringView View => new() { Data = Pointer, Length = (nuint)Length };

        internal int Length { get; }

        public void Dispose()
        {
            if (_memory != 0)
                Marshal.FreeCoTaskMem(_memory);
        }
    }

    internal static string? Read(byte* text) => text is null ? null : Marshal.PtrToStringUTF8((nint)text);

    /// <summary>
    /// Reads one of the API's borrowed string slices - a label, an adapter description, a callback's
    /// error message. wgpu copies these itself when it takes one, so the slice is only ever read
    /// inside the call that handed it over.
    /// </summary>
    internal static string? Read(Wg.StringView text) =>
        text.Data is null || text.Length == 0 ? null : Marshal.PtrToStringUTF8((nint)text.Data, (int)text.Length);

    /// <summary>
    /// Rounds <paramref name="value"/> up to a multiple of <paramref name="alignment"/>.
    ///
    /// WebGPU's uniform dynamic offset and every texture-copy row pitch are alignment
    /// requirements rather than hints: a value that is merely close is rejected at submission.
    /// </summary>
    internal static ulong Align(ulong value, ulong alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    internal static int Align(int value, int alignment) =>
        alignment <= 1 ? value : (value + alignment - 1) / alignment * alignment;

    /// <summary>
    /// Blocks until <paramref name="completed"/> is set by a callback, or <paramref name="timeout"/>
    /// passes, and returns whether it was.
    ///
    /// WebGPU's async entry points are answered through a future, and the callback attached to one
    /// runs from a wait. The only wait this wgpu-native build implements is
    /// <c>wgpuInstanceProcessEvents</c>: <c>wgpuInstanceWaitAny</c> is a stub that aborts the
    /// process, and so is every instance-feature query, so the future itself is never waited on.
    /// Callbacks registered with <see cref="Wg.CallbackMode.AllowProcessEvents"/> run on this thread
    /// inside that call, which is what keeps the captured locals safe without a lock.
    /// </summary>
    internal static bool WaitFor(Wg.WebGPU api, Wg.Instance* instance, Func<bool> completed, TimeSpan timeout)
    {
        var deadline = DateTime.UtcNow + timeout;

        while (!completed())
        {
            if (DateTime.UtcNow >= deadline)

                return false;

            api.InstanceProcessEvents(instance);
            Thread.Sleep(1);
        }

        return true;
    }

    /// <summary>
    /// Creates the instance.
    ///
    /// It asks for nothing: wgpu-native's instance-level features (<c>TimedWaitAny</c>,
    /// <c>ShaderSourceSPIRV</c>) are not implemented by the build this ships with - requesting one
    /// panics inside <c>wgpuCreateInstance</c>, and so does both ways of asking whether one exists.
    /// </summary>
    internal static Wg.Instance* CreateInstance(Wg.WebGPU api)
    {
        var descriptor = default(Wg.InstanceDescriptor);

        return api.CreateInstance(&descriptor);
    }

    /// <summary>
    /// The binding numbers the shader compiler assigns, restated for the backend: the single uniform
    /// block is 0, every texture follows it in reflection order, then every sampler.
    /// </summary>
    internal static int TextureBinding(int index) => 1 + index;

    internal static int SamplerBinding(int textureCount, int index) => 1 + textureCount + index;

    internal static Wg.ShaderStage None => Wg.ShaderStage.None;
}
