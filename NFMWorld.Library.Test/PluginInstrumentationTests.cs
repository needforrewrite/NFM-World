using System.Reflection;
using System.Security;
using DouglasDwyer.CasCore;
using Mono.Cecil;
using NFMWorldLibrary.Plugins;

namespace NFMWorld.Library.Test;

/// <summary>
/// Instrumentation must rewrite a plugin from its own metadata alone, never asking Cecil to
/// resolve a host assembly.
///
/// A <c>PublishSingleFile=true</c> build keeps every host assembly inside the apphost and the
/// browser has no assembly directory at all, so a resolution during the rewrite makes those
/// builds unable to load any plugin (<c>Failed to resolve assembly: 'NFMWorld.Library'</c>).
/// Desktop hides it: the runtime's own directory is on Cecil's search path, so resolving
/// succeeds there and only the bundled builds fail. Hence a resolver that throws.
///
/// The regression this guards is one <c>Resolve()</c> in <c>PatchMethodCall</c>, which asked
/// whether a <c>call</c> target's declaring type was a value type.
/// </summary>
[TestClass]
public class PluginInstrumentationTests
{
    private const string PluginPath = "data/gamemodes/sandbox/hostile/gamemode.dll";

    private sealed class ThrowingResolver : IAssemblyResolver
    {
        public List<string> Requested { get; } = [];

        public AssemblyDefinition? Resolve(AssemblyNameReference name)
        {
            Requested.Add(name.FullName);
            throw new AssemblyResolutionException(name);
        }

        public AssemblyDefinition? Resolve(AssemblyNameReference name, ReaderParameters parameters) => Resolve(name);

        public void Dispose()
        {
        }
    }

    private sealed class Instrumenter()
        : CasAssemblyLoader(PluginPolicy.Instance, "plugin-instrumentation-test", isCollectible: true)
    {
        public void Instrument(AssemblyDefinition assembly) => InstrumentAssembly(assembly);
    }


    [TestMethod]
    public void InstrumentingAPlugin_ResolvesNoAssemblies()
    {
        NFMWorldLibrary.TheVFS.VFS.MountDirectory(AppContext.BaseDirectory);
        var bytes = NFMWorldLibrary.TheVFS.VFS.ReadAllBytes(PluginPath);

        // Read the plugin the way a bundled host can: with nothing to resolve against. Any
        // Resolve() the rewriter still performs surfaces here as an exception.
        var resolver = new ThrowingResolver();
        using var definition = AssemblyDefinition.ReadAssembly(
            new MemoryStream(bytes, writable: false),
            new ReaderParameters { AssemblyResolver = resolver }
        );

        new Instrumenter().Instrument(definition);
        Assert.AreEqual(0, resolver.Requested.Count);

        // Instrumentation really happened: the guard fields the rewrite adds are present.
        Assert.IsTrue(
            definition.MainModule.Types.Any(x => x.Namespace == "DouglasDwyer.CasCore.Guard"),
            "the plugin was read but not rewritten"
        );
    }
}
