// LLM maintained.
//
// Whether the Vulkan loader ANGLE needs is actually on disk.
//
// This exists because the failure it detects is reported as something else entirely, and everything
// about the message points away from the real cause. ANGLE does not link the Vulkan loader: it
// resolves it at run time in OpenLibVulkan (src/common/vulkan/libvulkan_loader.cpp), and on Windows
// angle_use_custom_libvulkan is true (src/common/vulkan/BUILD.gn), which picks SearchType::ModuleDir
// over SearchType::SystemDir. So the loader has to be beside libGLESv2.dll, and the system loader
// already installed in System32 is not consulted at all.
//
// When it is missing, that call returns null and ANGLE turns it into VK_ERROR_INITIALIZATION_FAILED
// at vk_renderer.cpp (rx::vk::Renderer::initialize), which reaches EGL and then the caller as
//
//   Internal Vulkan error (-3): Initialization of an object could not be completed for
//   implementation-specific reasons, in ..\..\src\libANGLE\renderer\vulkan\vk_renderer.cpp,
//   rx::vk::Renderer::initialize:2491.
//
// which is the ANGLE_VK_CHECK on that null. So the text blames the driver, names ANGLE's own Vulkan
// layer, and raises a "VkResult" that neither the Vulkan loader nor the driver produced. The only
// thing this path can actually mean is that the file is not there. Checking it up front replaces a
// misleading driver error with a statement of fact.
//
// Why the warning rather than a refusal: the check cannot be made exact. The search runs when
// libGLESv2 is loaded, against whatever is beside *it* - the app directory in every configuration
// this project builds, but SDL can be pointed elsewhere, and a profile-corrected guess would then
// reject a run that works. So the two outcomes are ordered by what a wrong answer costs. Guessing
// "missing" on a working setup would refuse a valid run, which is the worse mistake, so absence only
// warns and the attempt is still made; if it then fails, the reader has the explanation next to it.
namespace NFMWorld.Graphics.OpenGL;

/// <summary>Whether ANGLE's private Vulkan loader is present, for the one selection that needs it.</summary>
public static class AngleVulkanLoader
{
    /// <summary>
    /// The file <c>OpenLibVulkan</c> looks for, which is per platform.
    ///
    /// Not the platform's usual library extension: Linux emits this one as
    /// <c>libvulkan.so.1</c> because the libvulkan target sets
    /// <c>output_extension = "so.1"</c> (<c>third_party/vulkan-loader/src/BUILD.gn</c>), so a search
    /// for <c>libvulkan.so</c> finds nothing and reads as "there is no loader".
    /// </summary>
    private static readonly string LibraryName =
        OperatingSystem.IsWindows() ? "vulkan-1.dll"
        : OperatingSystem.IsLinux() ? "libvulkan.so.1"
        // macOS links the loader statically - angle_shared_libvulkan is !is_mac - so nothing is
        // loaded at run time and there is no file whose absence could matter.
        : "";

    /// <summary>Whether <paramref name="selection"/> needs a loader that is not beside the running executable.</summary>
    /// <remarks>
    /// <c>Gl</c> and <c>Gles</c> are pass-throughs to the driver's own GL and do not go through ANGLE's
    /// Vulkan layer at all, and <c>Null</c> renders nothing by construction; none of them can be
    /// affected by a missing loader. <c>D3d11</c> and <c>Default</c> resolve to angle_use_custom_libvulkan
    /// as well, but their Vulkan loader is never opened, so this holds the warning to the one selection
    /// that can actually miss it.
    /// </remarks>
    public static bool MayBeMissing(AngleSelection selection) =>
        selection.Platform == AnglePlatformType.Vulkan;

    /// <summary>
    /// The path the loader would have to be at for an <c>eglGetPlatformDisplay</c> request to find it,
    /// or <see langword="null"/> where nothing is loaded at run time.
    /// </summary>
    public static string? ExpectedPath =>
        LibraryName.Length == 0 ? null : Path.Combine(AppContext.BaseDirectory, LibraryName);

    /// <summary>Whether that path exists.</summary>
    public static bool IsPresent =>
        ExpectedPath is { } path && File.Exists(path);
}
