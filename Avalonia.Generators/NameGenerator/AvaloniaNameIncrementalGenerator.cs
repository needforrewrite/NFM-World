using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;
using Avalonia.Generators.Common;
using Avalonia.Generators.Common.Domain;
using Avalonia.Generators.Compiler;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using XamlX;
using XamlX.TypeSystem;

namespace Avalonia.Generators.NameGenerator;

[Generator(LanguageNames.CSharp)]
public class AvaloniaNameIncrementalGenerator : IIncrementalGenerator
{
    private const string SourceItemGroupMetadata = "build_metadata.AdditionalFiles.SourceItemGroup";
    private static readonly MiniCompiler s_noopCompiler = MiniCompiler.CreateNoop();

#if AVA_DEBUG
    public static List<string> Logs { get; } = [];
#endif

    [Conditional("AVA_DEBUG")]
    public static void Print(string msg)
#if AVA_DEBUG
        => Logs.Add("//\t" + msg);
#else
    {
    }
#endif

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        Print("hi from AvaloniaNameIncrementalGenerator");
        
#if AVA_DEBUG
        if (!Debugger.IsAttached) 
        { 
            //Debugger.Launch(); 
        }
#endif
        
        // Map MSBuild properties onto readonly GeneratorOptions.
        var options = context.AnalyzerConfigOptionsProvider
            .Select(static (options, _) => new GeneratorOptions(options.GlobalOptions))
            .WithTrackingName(TrackingNames.XamlGeneratorOptionsProvider);

        // Filter additional texts, we only need Avalonia XAML files.
        var xamlFiles = context.AdditionalTextsProvider
            .Combine(options.Combine(context.AnalyzerConfigOptionsProvider))
            .Where(static pair =>
            {
                var text = pair.Left;
                var (options, optionsProvider) = pair.Right;
                var filePath = text.Path;
                
                Print($"File path: {filePath}");

                if (!(filePath.EndsWith(".xaml", StringComparison.OrdinalIgnoreCase) ||
                      filePath.EndsWith(".paml", StringComparison.OrdinalIgnoreCase) ||
                      filePath.EndsWith(".axaml", StringComparison.OrdinalIgnoreCase)))
                {
                    Print("Not a XAML file.");

                    return false;
                }

                if (!options.AvaloniaNameGeneratorFilterByPath.Matches(filePath))
                {
                    Print("Filtered out by path.");
                    return false;
                }

                if (!optionsProvider.GetOptions(pair.Left).TryGetValue(SourceItemGroupMetadata, out var itemGroup)
                    || itemGroup != "AvaloniaXaml")
                {
                    Print("Not in AvaloniaXaml item group. Item group: " + itemGroup);
                    return false;
                }

                return true;
            })
            .Select(static (pair, _) => pair.Left)
            .WithTrackingName(TrackingNames.InputXamlFilesProvider);

        // Actual parsing step. We input XAML files one by one, but don't resolve any types.
        // That's why we use NoOp type system here, allowing parsing to run detached from C# compilation.
        // Otherwise we would need to re-parse XAML on any C# file changed.
        var parsedXamlClasses = xamlFiles
            .Select(static (file, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                var xaml = file.GetText(cancellationToken)?.ToString();
                if (xaml is null)
                {
                    Print("XAML text is null.");
                    return null;
                }

                ResolvedXmlView? resolvedXmlView;
                DiagnosticFactory? diagnosticFactory = null;
                var location =  new FileLinePositionSpan(file.Path, default);
                try
                {
                    var viewResolver = new XamlXViewResolver(s_noopCompiler);
                    var view = viewResolver.ResolveView(xaml, cancellationToken);
                    if (view is null)
                    {
                        Print("View is null after parsing.");
                        return null;
                    }

                    var xmlNames = EquatableList<ResolvedXmlName>.Empty;
                    var nameResolver = new XamlXNameResolver();
                    xmlNames = nameResolver.ResolveXmlNames(view.Xaml, cancellationToken);
                    
                    Print("Resolved XML names: " + string.Join(", ", xmlNames.Select(n => n.Name)));

                    resolvedXmlView = new ResolvedXmlView(view, xmlNames);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (XmlException ex)
                {
                    Print("Caught XmlException during XAML parsing: " + ex.Message);
                    diagnosticFactory = new(NameGeneratorDiagnostics.ParseFailed, new(file.Path, GetLinePositionSpan(ex)), new([ex.Message]));

                    resolvedXmlView = ex is XamlParseException ? TryExtractTypeFromXml(xaml) : null;
                }
                catch (XamlTypeSystemException ex)
                {
                    Print("Caught XamlTypeSystemException during XAML parsing: " + ex.Message);
                    diagnosticFactory = new(NameGeneratorDiagnostics.ParseFailed, location, new([ex.Message]));
                    resolvedXmlView = TryExtractTypeFromXml(xaml);
                }
                catch (Exception ex)
                {
                    Print("Caught general Exception during XAML parsing: " + ex.ToString());
                    diagnosticFactory = GetInternalErrorDiagnostic(location, ex);
                    resolvedXmlView = null;
                }

                return new XmlClassInfo(file.Path, xaml, resolvedXmlView, diagnosticFactory);
            })
            .Where(request => request is not null)
            .WithTrackingName(TrackingNames.ParsedXamlClasses);
        
        // IMPORTANT: we shouldn't cache CompilationProvider as a whole,
        // But we also should keep in mind that CompilationProvider can frequently re-trigger generator.
        var roslynTypeSystem = context.CompilationProvider
            .Select(static (compilation, _) => new RoslynTypeSystem(compilation))
            .WithTrackingName(TrackingNames.RoslynTypeSystem);

        var compiler = roslynTypeSystem
            .Select(static (roslynTypeSystem, _) => MiniCompiler.CreateRoslyn(roslynTypeSystem, MiniCompiler.AvaloniaXmlnsDefinitionAttribute))
            .WithTrackingName(TrackingNames.XamlTypeSystem);

        // Create C# XAML compiler for full XAML-to-C# compilation (WithXamlXCompilation behavior).
        var csharpCompiler = roslynTypeSystem
            .Combine(options)
            .Select(static (pair, _) =>
            {
                var (roslynTypeSystem, options) = pair;
                
                try
                {
                    return new XamlCSharpCompiler(roslynTypeSystem, options.NfmWorldAvaloniaNameGeneratorIsHotReloadingEnabled);
                }
                catch (Exception ex)
                {
                    Print($"Failed to create {nameof(XamlCSharpCompiler)}: {ex}");
                    
                    return null;
                }
            })
            .WithTrackingName(TrackingNames.XamlCSharpCompiler);
        
        // Generate the shared XamlContext type (used by all compiled XAML views).
        context.RegisterSourceOutput(csharpCompiler, static (ctx, compiler) =>
        {
            if (compiler != null)
                ctx.AddSource("__XamlContext.g.cs", compiler.ContextSource);
            ctx.AddSource($"logs2.g.cs", SourceText.From(string.Join("\n", Logs), Encoding.UTF8));
        });
        
        // Note: this step will be re-executed on any C# file changes.
        // As much as possible heavy tasks should be moved outside of this step, like XAML parsing.
        var resolvedNames = parsedXamlClasses
            .Combine(compiler)
            .Combine(csharpCompiler)
            .Select(static (pair, ct) =>
            {
                var ((classInfo, compiler), csharpCompiler) = pair;
                var hasDevToolsReference = compiler.TypeSystem.FindAssembly("Avalonia.Diagnostics") is not null;
                var nameResolver = new XamlXNameResolver();

                var diagnostics = new List<DiagnosticFactory>(2);
                if (classInfo?.Diagnostic != null)
                {
                    diagnostics.Add(classInfo.Diagnostic);
                }

                ResolvedView? view = null;
                string? compiledXamlSource = null;
                if (classInfo?.XmlView is { } xmlView)
                {
                    var type = compiler.TypeSystem.FindType(xmlView.FullName);

                    if (type is null)
                    {
                        diagnostics.Add(new(NameGeneratorDiagnostics.InvalidType, new(classInfo.FilePath, default), new([xmlView.FullName])));
                    }
                    else if (type.IsAvaloniaStyledElement())
                    {
                        var resolvedNames = new List<ResolvedName>();
                        foreach (var xmlName in xmlView.XmlNames)
                        {
                            ct.ThrowIfCancellationRequested();

                            try
                            {
                                var clrType = compiler.ResolveXamlType(xmlName.XmlType);
                                if (!clrType.IsAvaloniaStyledElement())
                                {
                                    Print($"Skipping name resolution for non-StyledElement type: {clrType.GetFqn()}");
                                    continue;
                                }

                                resolvedNames.Add(nameResolver
                                    .ResolveName(clrType, xmlName.Name, xmlName.FieldModifier));
                            }
                            catch (XmlException ex)
                            {
                                Print($"Caught XmlException during name resolution: {ex.Message}");
                                diagnostics.Add(new(NameGeneratorDiagnostics.NamedElementFailed,
                                    new(classInfo.FilePath, GetLinePositionSpan(ex)), new([xmlName.Name, ex.Message])));
                            }
                            catch (Exception ex)
                            {
                                Print($"Caught general Exception during name resolution: {ex}");
                                diagnostics.Add(GetInternalErrorDiagnostic(new(classInfo.FilePath, default), ex));
                            }
                        }

                        view = new ResolvedView(xmlView, type.IsAvaloniaWindow(), new(resolvedNames));

                        // Compile XAML to C# for WithXamlXCompilation behavior
                        if (csharpCompiler != null && classInfo.XamlSource != null)
                        {
                            try
                            {
                                compiledXamlSource = csharpCompiler.CompileView(classInfo.XamlSource, classInfo.FilePath, out var xamlDiagnostics);
                                foreach (var diag in xamlDiagnostics)
                                {
                                    diagnostics.Add(new(diag, new(classInfo.FilePath, default), new([xmlView.FullName])));
                                }
                            }
                            catch (Exception ex)
                            {
                                Print($"Caught general Exception during XAML compilation: {ex}");
                                diagnostics.Add(GetInternalXamlErrorDiagnostic(new(classInfo.FilePath, default), ex));
                            }
                        }
                    }
                }

                return new ResolvedClassInfo(view, hasDevToolsReference, new(diagnostics), compiledXamlSource);
            })
            .WithTrackingName(TrackingNames.ResolvedNamesProvider);

        context.RegisterSourceOutput(resolvedNames.Combine(options), static (context, pair) =>
        {
            var (info, options) = pair;

            foreach (var diagnostic in info.Diagnostics)
            {
                context.ReportDiagnostic(diagnostic.Create());
            }

            if (info.View is { } view && options.AvaloniaNameGeneratorFilterByNamespace.Matches(view.Namespace))
            {
                ICodeGenerator codeGenerator = options.AvaloniaNameGeneratorBehavior switch
                {
                    Behavior.OnlyProperties => new OnlyPropertiesCodeGenerator(
                        options.AvaloniaNameGeneratorClassFieldModifier),
                    Behavior.InitializeComponent => new InitializeComponentCodeGenerator(
                        options.AvaloniaNameGeneratorAttachDevTools && info.CanAttachDevTools && view.IsWindow,
                        options.AvaloniaNameGeneratorClassFieldModifier),
                    Behavior.WithXamlXCompilation => new XamlXCodeGenerator(
                        options.AvaloniaNameGeneratorAttachDevTools && info.CanAttachDevTools && view.IsWindow,
                        options.AvaloniaNameGeneratorClassFieldModifier,
                        info.CompiledXamlSource),
                    _ => throw new ArgumentOutOfRangeException()
                };
                var fileName = options.AvaloniaNameGeneratorViewFileNamingStrategy switch
                {
                    ViewFileNamingStrategy.ClassName => $"{view.ClassName}.g.cs",
                    ViewFileNamingStrategy.NamespaceAndClassName => $"{view.Namespace}.{view.ClassName}.g.cs",
                    _ => throw new ArgumentOutOfRangeException(
                        nameof(ViewFileNamingStrategy), options.AvaloniaNameGeneratorViewFileNamingStrategy,
                        "Unknown naming strategy!")
                };

                var generatedPartialClass = codeGenerator.GenerateCode(
                    info.View.ClassName,
                    info.View.Namespace,
                    info.View.Names);

                Print("Generating file: " + fileName);
                context.AddSource(fileName, generatedPartialClass);
            }

            context.AddSource($"logs1-{Guid.NewGuid()}.g.cs", SourceText.From(string.Join("\n", Logs), Encoding.UTF8));
        });
        
#if AVA_DEBUG
        context.RegisterPostInitializationOutput(
            static context => context.AddSource($"logs.g.cs", SourceText.From(string.Join("\n", Logs), Encoding.UTF8))
        );
#endif
    }

    private static DiagnosticFactory GetInternalErrorDiagnostic(FileLinePositionSpan location, Exception ex) =>
        new(NameGeneratorDiagnostics.InternalError, location, new([ex.ToString().Replace('\n', '*').Replace('\r', '*')]));

    private static DiagnosticFactory GetInternalXamlErrorDiagnostic(FileLinePositionSpan location, Exception ex) =>
        new(NameGeneratorDiagnostics.InternalErrorCompilingXaml, location, new([ex.ToString().Replace('\n', '*').Replace('\r', '*')]));

    /// <summary>
    /// Fallback in case XAML parsing fails. Extracts just the class name and namespace of the root element.
    /// </summary>
    private static ResolvedXmlView? TryExtractTypeFromXml(string xaml)
    {
        try
        {
            var document = XDocument.Parse(xaml);
            var classValue = document.Root.Attribute(XName.Get("Class", XamlNamespaces.Xaml2006))?.Value;
            if (classValue?.LastIndexOf('.') is { } lastDotIndex && lastDotIndex != -1)
            {
                return new(classValue.Substring(lastDotIndex + 1), classValue.Substring(0, lastDotIndex), EquatableList<ResolvedXmlName>.Empty);
            }
        }
        catch
        {
            // ignore
        }
        return null;
    }

    private static LinePositionSpan GetLinePositionSpan(XmlException ex)
    {
        var position = new LinePosition(Math.Max(0, ex.LineNumber - 1), Math.Max(0, ex.LinePosition - 1));
        return new(position, position);
    }

    internal record XmlClassInfo(
        string FilePath,
        string? XamlSource,
        ResolvedXmlView? XmlView,
        DiagnosticFactory? Diagnostic);

    internal record ResolvedClassInfo(
        ResolvedView? View,
        bool CanAttachDevTools,
        EquatableList<DiagnosticFactory> Diagnostics,
        string? CompiledXamlSource);

    /// <summary>
    /// Avoid holding references to <see cref="Diagnostic"/> because it can hold references to <see cref="ISymbol"/>, <see cref="SyntaxTree"/>, etc.
    /// </summary>
    internal record DiagnosticFactory(DiagnosticDescriptor Descriptor, FileLinePositionSpan LinePosition, EquatableList<string> FormatArguments)
    {
        public Diagnostic Create() => Diagnostic.Create(Descriptor, 
            Location.Create(LinePosition.Path, default, new(LinePosition.StartLinePosition, LinePosition.EndLinePosition)),
            messageArgs: [.. FormatArguments]);
    }
}