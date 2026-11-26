// LLM maintained.
//
// The window's native handles, as the video driver that created it exposes them.
//
// A graphics backend that makes its own device against the window needs more than one number, and
// which numbers it needs depends entirely on the windowing system: X11 wants a connection and a
// window id, Wayland a display and a surface, macOS a CAMetalLayer and nothing else. Reporting a
// single "native window handle" - which is what this project used to do - can only ever be enough for
// Win32.
//
// The values are raw and untyped on purpose: a backend should not have to alias this project's
// SDL3-CS references to interpret a pointer it will hand straight to its own API.

namespace NFMWorld.Platform.SDL3;

/// <summary>The windowing system the window was created through.</summary>
public enum NativeWindowSystem
{
    /// <summary>No native handles - the window was not created through a desktop video driver.</summary>
    None,

    /// <summary>The Win32 driver. <see cref="NativeWindowHandles.Window"/> is an <c>HWND</c>.</summary>
    Win32,

    /// <summary>The X11 driver. <see cref="NativeWindowHandles.Display"/> is a <c>Display*</c> and
    /// <see cref="NativeWindowHandles.Window"/> the window's XID.</summary>
    X11,

    /// <summary>The Wayland driver. <see cref="NativeWindowHandles.Display"/> is a <c>wl_display*</c>
    /// and <see cref="NativeWindowHandles.Window"/> a <c>wl_surface*</c>.</summary>
    Wayland,

    /// <summary>The Cocoa driver. <see cref="NativeWindowHandles.Window"/> is an <c>NSWindow*</c> and
    /// <see cref="NativeWindowHandles.MetalLayer"/> the <c>CAMetalLayer*</c> of the Metal view SDL
    /// placed over it.</summary>
    Cocoa,
}

/// <summary>
/// The native handles of one window. Fields that do not apply to <paramref name="System"/> are zero,
/// so a caller switches on the system rather than testing a pointer.
/// </summary>
/// <param name="Window">The window itself: an <c>HWND</c>, an X11 XID, a <c>wl_surface*</c> or an
/// <c>NSWindow*</c>. Null for <see cref="NativeWindowSystem.None"/>.</param>
/// <param name="Display">The display connection the window belongs to, for X11 and Wayland.</param>
/// <param name="MetalLayer">The window's <c>CAMetalLayer*</c>, for Cocoa.</param>
public readonly record struct NativeWindowHandles(
    NativeWindowSystem System,
    IntPtr Window,
    IntPtr Display = default,
    IntPtr MetalLayer = default
);
