using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using Avalonia.Markup.Xaml;
using Maxine.Extensions;
using NFMWorld.XamlX.Core;
using NFMWorldLibrary;
using XamlX;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.TypeSystem;

namespace NFMWorld.UI.Yoga.Xaml;

public class XamlHotReload
{
#if DEBUG
    private static FileSystemWatcher? _watcher;
    private static ConditionalWeakTable<Node, string> _trackedNodes = new();
    
    [RequiresUnreferencedCode("Uses XamlX Sre types which may not be compatible with trimming.")]
    [RequiresDynamicCode("Uses Reflection.Emit which may not be compatible with AOT.")]
    public static void Initialize(string? projectRoot = null)
    {
        _watcher = new FileSystemWatcher(projectRoot ?? ProjectUtils.TryGetProjectDirectory() ?? ".", "*.xaml");
        _watcher.IncludeSubdirectories = true;
        _watcher.Changed += OnXamlFileChanged;
        _watcher.EnableRaisingEvents = true;
    }
#endif
    
    [Conditional("DEBUG")]
    public static void Register(Node node, string xamlPath)
    {
#if DEBUG
        Logging.Debug($"[XamlHotReload] Registered for hot reload: {xamlPath}");
        var fullPath = Path.GetFullPath(Path.Combine(_watcher!.Path, xamlPath));
        _trackedNodes.AddOrUpdate(node, fullPath);
#endif
    }
    
#if DEBUG
#pragma warning disable IL2026
#pragma warning disable IL3050
    private static readonly Debounced<FileSystemEventArgs> Debounced = ((Action<FileSystemEventArgs>)((e) => Task.Run(() => ReloadXaml(e.FullPath)))).Debounce(1000);
#pragma warning restore IL3050
#pragma warning restore IL2026

    [RequiresUnreferencedCode("Uses XamlX Sre types which may not be compatible with trimming.")]
    [RequiresDynamicCode("Uses Reflection.Emit which may not be compatible with AOT.")]
    private static void OnXamlFileChanged(object sender, FileSystemEventArgs e)
    {
        // Debounce, compile, and swap
        Debounced(e);
    }

    [RequiresUnreferencedCode("Uses XamlX Sre types which may not be compatible with trimming.")]
    [RequiresDynamicCode("Uses Reflection.Emit which may not be compatible with AOT.")]
    private static async Task? ReloadXaml(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var nodesToUpdate = _trackedNodes.Where(e => e.Value == fullPath).ToArray();
        if (nodesToUpdate.Length > 0)
        {
            Logging.Debug($"[XamlHotReload] Reloading XAML: {fullPath}");

            // Reload the XAML and re-initialize the view
            try
            {
                var firstNode = nodesToUpdate[0].Key;
                var (create, populate) = CompileXaml(firstNode, path, await File.ReadAllTextAsync(path));
                Logging.Debug($"[XamlHotReload] Successfully compiled XAML: {fullPath}");
                
                foreach (var (node, _) in nodesToUpdate)
                {
                    // Remove old children
                    if (node is View view)
                        view.Children.Clear();

                    // Populate the view
                    populate(AvaloniaXamlLoader.CreateDefaultServiceProvider(node), node);
                }
                Logging.Debug($"[XamlHotReload] Successfully reloaded XAML: {fullPath}");
            }
            catch (Exception ex)
            {
                Logging.Warning($"[XamlHotReload] Failed to reload XAML: {fullPath}. Error: {ex}");
            }
        }
    }

    // Ensure that System.ComponentModel is available
    internal static readonly Assembly _unused = typeof(TypeConverterAttribute).Assembly;

    [RequiresUnreferencedCode("Uses XamlX Sre types which may not be compatible with trimming.")]
    [RequiresDynamicCode("Uses Reflection.Emit which may not be compatible with AOT.")]
    private static (Func<IServiceProvider?, object>? create, Action<IServiceProvider?, object?> populate) CompileXaml(Node intoNode, string xamlPath, string text)
    {
        var typeSystem = new SreTypeSystem();

        var assembly = typeSystem.FindAssembly(Assembly.GetExecutingAssembly().GetName().Name ?? throw new InvalidOperationException("Could not get executing assembly name"));

        // Create XamlX configuration with our type mappings
        var typeMappings = XamlHelpers.CreateTypeMappings(typeSystem);
        var diagnosticsHandler = new XamlDiagnosticsHandler
        {
            HandleDiagnostic = diagnostic =>
            {
                if (diagnostic.Severity == XamlDiagnosticSeverity.Error)
                    Logging.Debug($"XAML: {diagnostic.Code} - {diagnostic.Title}");
                else if (diagnostic.Severity == XamlDiagnosticSeverity.Warning)
                    Logging.Debug($"XAML: {diagnostic.Code} - {diagnostic.Title}");
                else
                    Logging.Debug($"XAML: {diagnostic.Code} - {diagnostic.Title}");
                return diagnostic.Severity;
            }
        };
        var config = new TransformerConfiguration(typeSystem, assembly, typeMappings, diagnosticsHandler: diagnosticsHandler);

        var emitMappings = new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>();
        var compiler = new XamlILCompiler(config, emitMappings, true)
        {
            EnableIlVerification = false // Disable for now, can enable for debugging
        };

        // Add our custom transformer to remove x:Class and other directives before emit
        compiler.Transformers.Add(new RemoveXamlDirectivesTransformer());

        var aName = new AssemblyName($"__XamlRuntimeHotReloadAssembly__{DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        var ab = AssemblyBuilder.DefineDynamicAssembly(aName, AssemblyBuilderAccess.Run);

        var mb = ab.DefineDynamicModule(aName.Name ?? "MainModule");

        var contextBuilder = typeSystem.CreateTypeBuilder(mb.DefineType("__XamlRuntimeHotReloadContext"));
        var contextTypeDef = compiler.CreateContextType(contextBuilder);

        try
        {
            var type = CompileXamlInner(intoNode, xamlPath, text, typeSystem, mb, compiler, contextTypeDef);
            var runtimeType = type.CreateType();
            var (create, populate) = GetCallbacks(runtimeType!);
            return (create, populate);
        }
        catch (Exception ex)
        {
            Logging.Debug($"Error compiling hot-reload Xaml for {intoNode.GetType().FullName}: {ex.Message}");
            throw;
        }
    }

    [RequiresUnreferencedCode("Uses XamlX Sre types which may not be compatible with trimming.")]
    [RequiresDynamicCode("Uses Reflection.Emit which may not be compatible with AOT.")]
    private static TypeBuilder CompileXamlInner(Node intoNode, string xamlPath, string xml, SreTypeSystem typeSystem, ModuleBuilder mb, XamlILCompiler compiler, IXamlType contextTypeDef)
    {
        var targetType = mb.DefineType($"__XamlRuntimeHotReloadType__{intoNode.GetType().FullName}__{Guid.NewGuid():N}",
            TypeAttributes.Public | TypeAttributes.Class,
            intoNode.GetType());
        
        var doc = XDocumentXamlParser.Parse(xml);

        // Transform the XAML AST
        compiler.Transform(doc);

        // Create a type builder for the target type
        var typeBuilder = typeSystem.CreateTypeBuilder(targetType);

        // Compile and emit Populate/Build methods
        compiler.Compile(
            doc,
            typeBuilder,
            contextTypeDef,
            populateMethodName: "Populate",
            createMethodName: "Build",
            namespaceInfoClassName: "XamlNamespaceInfo",
            baseUri: xamlPath,
            fileSource: new XamlFileSource(xamlPath, xml));

        return targetType;
    }

    private static (Func<IServiceProvider?, object>? create, Action<IServiceProvider?, object?> populate)
        GetCallbacks([DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.PublicMethods)] Type created)
    {
        var isp = Expression.Parameter(typeof(IServiceProvider));
        var createCb = created.GetMethod("Build") is { } buildMethod
            ? Expression.Lambda<Func<IServiceProvider?, object>>(
                Expression.Convert(Expression.Call(buildMethod, isp), typeof(object)), isp).Compile()
            : null;

        var epar = Expression.Parameter(typeof(object));
        var populate = created.GetMethod("Populate")!;
        isp = Expression.Parameter(typeof(IServiceProvider));
        var populateCb = Expression.Lambda<Action<IServiceProvider?, object?>>(
            Expression.Call(populate, isp, Expression.Convert(epar, populate.GetParameters()[1].ParameterType)),
            isp, epar).Compile();

        return (createCb, populateCb);
    }
#endif
}