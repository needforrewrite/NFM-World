// LLM maintained.
//
// The shared D3D11/DXGI plumbing: the constants TerraFX does not declare, the COM error check, and
// the one piece of marshalling this backend needs that the bindings cannot do for it.
//
// Kept apart from the mapping table (D3D11Mapping.cs) and from every resource type because all three
// layers need these, and because the two things in here are each a trap worth having exactly one
// copy of.
using System.Runtime.InteropServices;
using System.Text;
using TerraFX.Interop.DirectX;
using TerraFX.Interop.Windows;

namespace NFMWorld.Graphics.D3D11;

/// <summary>
/// Helpers over the raw TerraFX bindings.
///
/// TerraFX gives the SDK's vtables as C# interfaces and nothing else: every call returns an
/// <see cref="HRESULT"/> that is the caller's to check, every out parameter is a bare pointer the
/// caller is the owner of, and <c>ComPtr&lt;T&gt;</c> - the one convenience the package does offer -
/// is a <c>ref struct</c> and therefore cannot be a field. So this backend holds raw pointers and
/// releases them by hand, which is exactly the shape FNA3D's C driver has, and the reason that
/// driver is a usable reference rather than a translation exercise.
/// </summary>
internal static class D3D11Interop
{
    /// <summary>
    /// <c>D3D11_SDK_VERSION</c>. The bindings emit no named constant for it (there is no
    /// <c>*SdkVersion</c> type anywhere in the assembly), and the value is fixed by the ABI rather
    /// than by the SDK installed: <c>D3D11CreateDevice</c>/<c>D3D11CreateDeviceAndSwapChain</c>
    /// expect 7 in this parameter and have since Windows 8. Anything else fails the call outright.
    /// </summary>
    public const uint SdkVersion = 7;

    /// <summary>
    /// <c>DXGI_USAGE_RENDER_TARGET_OUTPUT</c>. DXGI has no <c>DXGI_USAGE</c> enum in this assembly at
    /// all - <c>DXGI_SWAP_CHAIN_DESC.BufferUsage</c> is a plain <c>uint</c> - so the one value this
    /// backend needs is named here instead of written as a bare 32.
    /// </summary>
    public const uint UsageRenderTargetOutput = 32;

    /// <summary>
    /// <c>DXGI_SWAP_CHAIN_FLAG_ALLOW_TEARING</c>. Also absent as an enum member - the assembly's
    /// <c>DXGI_SWAP_CHAIN_FLAG</c> stops at <c>HW_PROTECTED</c> - and needed for the low-latency
    /// present that is the whole reason to run the flip model here.
    /// </summary>
    public const uint SwapChainFlagAllowTearing = 2048;

    /// <summary>
    /// <c>DXGI_PRESENT_ALLOW_TEARING</c>. No <c>DXGI_PRESENT</c> enum exists in this assembly, so the
    /// per-present flag is named here. Only legal alongside
    /// <see cref="SwapChainFlagAllowTearing"/> and a zero sync interval.
    /// </summary>
    public const uint PresentAllowTearing = 512;

    /// <summary>
    /// The window-association flags that stop DXGI's own hotkeys from hijacking the window. FNA3D
    /// passes <c>DXGI_MWA_NO_WINDOW_CHANGES</c> alone, which is the conservative choice: Alt+Enter and
    /// Print Screen stay DXGI's, but no resize/mode handling is registered behind the app's back.
    /// Its value comes from the SDK's <c>DXGI_MWA_FLAGS</c> enum, which this assembly does not have.
    /// </summary>
    public const uint MwaNoWindowChanges = 2;

    /// <summary>
    /// Throws with the HRESULT, the operation and a caller-supplied hint. Every failure in this
    /// backend is fatal to the frame or to the resource being built, and an HRESULT in an exception
    /// message is worth far more than a null pointer producing a black frame later.
    /// </summary>
    public static void Check(HRESULT hr, string operation, string? detail = null)
    {
        if (hr >= 0)
            return;

        var message = $"{operation} failed with 0x{hr.Value:X8}.";
        if (!string.IsNullOrEmpty(detail))
            message += " " + detail;

        throw new InvalidOperationException(message);
    }

    /// <summary>
    /// Releases a COM object through a local and nulls it, so a double release is impossible and the
    /// field cannot be left dangling.
    /// </summary>
    /// <remarks>
    /// A plain <c>obj->Release()</c> at each site would be one line shorter and is what the C driver
    /// does, but this backend has a dozen disposers that can each be reached twice (a caller's
    /// <see cref="IDisposable.Dispose"/> and the finalizer behind it) and a double <c>Release</c> on
    /// a raw interface pointer is a use-after-free rather than a no-op.
    /// </remarks>
    public static unsafe void Release<T>(ref T* pointer) where T : unmanaged
    {
        if (pointer == null)
            return;

        ((IUnknown*)pointer)->Release();
        pointer = null;
    }

    /// <summary>
    /// The IID of a TerraFX interface, as a real <see cref="Guid"/>.
    /// </summary>
    /// <remarks>
    /// <c>Windows.__uuidof&lt;T&gt;()</c> is the only way to name an IID in this assembly, but its result
    /// is a wrapper whose conversion to <see cref="Guid"/> is an operator on a temporary - so
    /// <c>&amp;__uuidof&lt;T&gt;()</c> does not compile, and the value has to be materialised into a local
    /// before its address can be taken. Routing every site through this method makes that a
    /// type-system fact rather than a thing to remember: the return type is already the <c>Guid</c>
    /// the COM methods want a pointer to.
    /// </remarks>
    public static Guid Iid<T>() where T : unmanaged, TerraFX.Interop.INativeGuid => Windows.__uuidof<T>();

    /// <summary>
    /// The bytes of an ASCII COM string, pinned for as long as the caller holds the handle.
    /// </summary>
    /// <remarks>
    /// This exists for exactly one field: <c>D3D11_INPUT_ELEMENT_DESC.SemanticName</c> is a bare
    /// <c>sbyte*</c>, and <c>CreateInputLayout</c> reads - and copies - the name during the call. A
    /// marshalled <see cref="string"/> parameter would only be guaranteed alive for the duration of a
    /// P/Invoke, and TerraFX's generated bindings are not P/Invokes: they are interface calls whose
    /// struct argument is passed by pointer. So the name is pinned explicitly, one handle per element,
    /// and freed once the layout exists. The same shape FNA3D gets for free from a string literal's
    /// static storage.
    /// </remarks>
    public static GCHandle PinAscii(string text)
    {
        var bytes = Encoding.ASCII.GetBytes(text + "\0");
        return GCHandle.Alloc(bytes, GCHandleType.Pinned);
    }

    /// <summary>The pinned bytes' address, as the <c>sbyte*</c> a D3D11 name field wants.</summary>
    public static unsafe sbyte* AddressOf(GCHandle handle) => (sbyte*)handle.AddrOfPinnedObject();
}
