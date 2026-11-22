using DouglasDwyer.CasCore;
using Mono.Cecil;

namespace NFMWorldLibrary.Plugins;

/// <summary>
/// The sandbox loader, able to read the host's assemblies whatever shape this build shipped in.
/// </summary>
/// <remarks>
/// CasCore's rewrites resolve every assembly the plugin references through Mono.Cecil, which looks
/// on disk and in the runtime's trusted-platform list - neither of which holds a
/// <c>PublishSingleFile=true</c> build, where every assembly lives inside the apphost. Without this
/// a sandboxed plugin fails to load with "Failed to resolve assembly: 'NFMWorld.Library'".
///
/// The app directory is added first so the same load works when the working directory is not the
/// one the game was installed in; the bundle is the fallback for the bundled build.
/// </remarks>
internal sealed class SandboxLoader(CasPolicy policy, string name)
    : CasAssemblyLoader(policy, name, isCollectible: true)
{
    protected override void InstrumentAssembly(AssemblyDefinition assembly)
    {
        if (assembly.MainModule.AssemblyResolver is BaseAssemblyResolver resolver)
        {
            resolver.AddSearchDirectory(AppContext.BaseDirectory);
            resolver.ResolveFailure += ReadFromBundle;
        }

        base.InstrumentAssembly(assembly);
    }

    private static AssemblyDefinition? ReadFromBundle(object? sender, AssemblyNameReference reference)
    {
        if (sender is not BaseAssemblyResolver resolver
            || SingleFileBundle.ForCurrentProcess is not { } bundle
            || !bundle.TryGetAssembly(reference.Name, out var image))
            return null;

        // Resolves against the same resolver, so a reference from the host assembly's own signature
        // is found the same way.
        return AssemblyDefinition.ReadAssembly(
            new MemoryStream(image, writable: false),
            new ReaderParameters { AssemblyResolver = resolver }
        );
    }
}
