using System.Reflection;
using System.Runtime.Loader;
using System.Text.Json;

namespace NFMWorld.LuaSourceGenerator;

/// <summary>
/// Custom AssemblyLoadContext that resolves dependencies from .deps.json file.
/// </summary>
internal class DependencyLoadContext : AssemblyLoadContext
{
    private readonly AssemblyDependencyResolver _resolver;
    private readonly Dictionary<string, string> _assemblyPaths = new();

    public DependencyLoadContext(string mainAssemblyPath)
    {
        _resolver = new AssemblyDependencyResolver(mainAssemblyPath);

        // Try to load and parse .deps.json file
        var depsJsonPath = Path.ChangeExtension(mainAssemblyPath, ".deps.json");
        if (File.Exists(depsJsonPath))
        {
            try
            {
                LoadDependenciesFromDepsJson(depsJsonPath, Path.GetDirectoryName(mainAssemblyPath)!);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Failed to parse {depsJsonPath}: {ex.Message}");
            }
        }
    }

    private void LoadDependenciesFromDepsJson(string depsJsonPath, string assemblyDirectory)
    {
        var json = File.ReadAllText(depsJsonPath);
        using var doc = JsonDocument.Parse(json);

        if (!doc.RootElement.TryGetProperty("targets", out var targets))
            return;

        // Get the first target (usually the runtime target)
        foreach (var target in targets.EnumerateObject())
        {
            foreach (var library in target.Value.EnumerateObject())
            {
                if (!library.Value.TryGetProperty("runtime", out var runtime))
                    continue;

                foreach (var runtimeAssembly in runtime.EnumerateObject())
                {
                    var assemblyName = Path.GetFileNameWithoutExtension(runtimeAssembly.Name);
                    var assemblyPath = Path.Combine(assemblyDirectory, runtimeAssembly.Name.Replace('/', Path.DirectorySeparatorChar));

                    if (File.Exists(assemblyPath))
                    {
                        _assemblyPaths[assemblyName] = assemblyPath;
                    }
                    else
                    {
                        assemblyPath = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                            ".nuget", "packages",
                            library.Name.ToLowerInvariant().Replace('/', Path.DirectorySeparatorChar),
                            runtimeAssembly.Name.Replace('/', Path.DirectorySeparatorChar)
                        );
                        if (File.Exists(assemblyPath))
                        {
                            _assemblyPaths[assemblyName] = assemblyPath;
                        }
                    }
                }
            }
            break; // Only process first target
        }
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Try using the resolver first
        var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        // Fall back to our parsed deps.json paths
        if (_assemblyPaths.TryGetValue(assemblyName.Name ?? "", out var path))
        {
            return LoadFromAssemblyPath(path);
        }

        // Let the default context handle it
        return null;
    }
}