// LLM maintained.
//
// Small helpers the WebGPU backend's types share: unmanaged UTF-8 strings, pointer reads and the
// binding-order table the shader compiler and this backend have to agree on.

using System.Runtime.InteropServices;
using Wg = Silk.NET.WebGPU;

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

        internal Utf8(string? text) =>
            _memory = text is null ? 0 : Marshal.StringToCoTaskMemUTF8(text);

        internal byte* Pointer => (byte*)_memory;

        public void Dispose()
        {
            if (_memory != 0)
                Marshal.FreeCoTaskMem(_memory);
        }
    }

    internal static string? Read(byte* text) => text is null ? null : Marshal.PtrToStringUTF8((nint)text);

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
    /// The binding numbers the shader compiler assigns, restated for the backend: the single uniform
    /// block is 0, every texture follows it in reflection order, then every sampler.
    /// </summary>
    internal static int TextureBinding(int index) => 1 + index;

    internal static int SamplerBinding(int textureCount, int index) => 1 + textureCount + index;

    internal static Wg.ShaderStage None => Wg.ShaderStage.None;
}
