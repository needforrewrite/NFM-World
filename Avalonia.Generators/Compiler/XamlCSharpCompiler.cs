using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Avalonia.Generators.NameGenerator;
using Microsoft.CodeAnalysis;
using XamlX;
using XamlX.Ast;
using XamlX.CSharp;
using XamlX.Emit;
using XamlX.IL;
using XamlX.Parsers;
using XamlX.Transform;
using XamlX.Transform.Transformers;
using XamlX.TypeSystem;

namespace Avalonia.Generators.Compiler;

/// <summary>
/// Compiles XAML documents to C# source code using the XamlX compiler pipeline
/// with CSharpEmitter/CSharpTypeBuilder instead of IL emission.
/// </summary>
internal sealed class XamlCSharpCompiler
{
    private readonly IXamlTypeSystem _typeSystem;
    private readonly TransformerConfiguration _configuration;
    private readonly XamlILCompiler _compiler;
    private readonly IXamlType _contextType;
    private readonly bool _didNotFindRegisterMethod;

    public XamlCSharpCompiler(IXamlTypeSystem typeSystem, bool supportHotReloading = false)
    {
        _typeSystem = typeSystem;

        var mappings = CreateTypeMappings(typeSystem);
        var diagnosticsHandler = new XamlDiagnosticsHandler();
        var assembly = typeSystem.Assemblies.First();

        _configuration = new TransformerConfiguration(
            typeSystem, assembly, mappings,
            diagnosticsHandler: diagnosticsHandler);

        var emitMappings = new XamlLanguageEmitMappings<IXamlILEmitter, XamlILNodeEmitResult>();
        if (supportHotReloading)
        {
            // Find the XamlHotReload.Register method for runtime support
            var registerMethod = assembly
                .FindType("nfm_world.ui.yoga.xaml.XamlHotReload")
                ?.FindMethod(method => method.Name == "Register");
            
            if (registerMethod == null)
            {
                _didNotFindRegisterMethod = true;
            }

            emitMappings.ContextFactoryCallback = (context, codeGen) =>
            {
                if (registerMethod != null)
                {
                    codeGen
                        .Ldarg(1) // load element parameter
                        .Ldstr(context.BaseUrl) // load base URI
                        .EmitCall(registerMethod); // Call XamlHotReload.Register(element, baseUri)
                }
            };
        }

        _compiler = new XamlILCompiler(_configuration, emitMappings, true)
        {
            EnableIlVerification = false
        };

        // Add directive removal transformer
        _compiler.Transformers.Add(new RemoveXamlDirectivesTransformer());

        // Generate context type eagerly (shared across all views)
        var contextBuilder = new CSharpTypeBuilder(typeSystem, "__XamlRuntime__", "XamlContext",
            typeSystem.GetType("System.Object"), XamlVisibility.Assembly);
        _contextType = _compiler.CreateContextType(contextBuilder);
        ContextSource = contextBuilder.GenerateSource();
    }

    /// <summary>
    /// Gets the generated C# source for the shared XamlContext type.
    /// </summary>
    public string ContextSource { get; }

    /// <summary>
    /// Compiles a XAML document to C# source code, returning the generated member declarations
    /// (Populate, Build methods + helpers) to be embedded in the partial class.
    /// </summary>
    /// <param name="xamlSource">The raw XAML source text.</param>
    /// <param name="filePath">The file path for diagnostics and base URI.</param>
    /// <param name="indent">The indentation to use for the generated members.</param>
    /// <returns>Generated C# member declarations, or null if compilation fails.</returns>
    public string CompileView(string xamlSource, string filePath, out IReadOnlyList<DiagnosticDescriptor> diagnostics, string indent = "        ")
    {
        var doc = XDocumentXamlParser.Parse(xamlSource, new Dictionary<string, string>
        {
            { XamlNamespaces.Blend2008, XamlNamespaces.Blend2008 }
        });

        // Transform AST (resolves types, properties, etc.)
        _compiler.Transform(doc);

        // Get root type info from the transformed AST
        var rootGrp = (XamlValueWithManipulationNode)doc.Root;
        var rootType = rootGrp.Type.GetClrType();

        // Create a CSharpTypeBuilder as the container for generated methods.
        // This is a "virtual" type builder - it collects the Populate/Build methods
        // and nested types, then we extract just the members.
        var typeBuilder = new CSharpTypeBuilder(
            _typeSystem,
            rootType.Namespace ?? "",
            rootType.Name,
            null,
            XamlVisibility.Public
        );

        // Compile - this defines Populate, Build, and XamlNamespaceInfo on the typeBuilder
        _compiler.Compile(
            doc,
            typeBuilder,
            _contextType,
            populateMethodName: "Populate",
            createMethodName: "Build",
            namespaceInfoClassName: "XamlNamespaceInfo",
            baseUri: filePath,
            fileSource: new SourceGenFileSource(filePath, xamlSource));

        // Extract just the member declarations
        var sb = new StringBuilder();
        if (_didNotFindRegisterMethod)
        {
            diagnostics = [NameGeneratorDiagnostics.XamlHotReloadNotFound];
            sb.Append($"{indent}// Warning: XamlHotReload.Register method not found. Hot reload support will be disabled.\n");
        }
        else
        {
            diagnostics = [];
        }
        
        typeBuilder.GenerateMembers(sb, indent);
        return sb.ToString();
    }

    private static XamlLanguageTypeMappings CreateTypeMappings(IXamlTypeSystem typeSystem)
    {
        var mappings = new XamlLanguageTypeMappings(typeSystem);

        TryAddType(typeSystem, "Avalonia.Metadata.XmlnsDefinitionAttribute", mappings.XmlnsAttributes);
        TryAddType(typeSystem, "Avalonia.Metadata.ContentAttribute", mappings.ContentAttributes);
        TryAddType(typeSystem, "Avalonia.Metadata.WhitespaceSignificantCollectionAttribute", mappings.WhitespaceSignificantCollectionAttributes);
        TryAddType(typeSystem, "Avalonia.Metadata.TrimSurroundingWhitespaceAttribute", mappings.TrimSurroundingWhitespaceAttributes);
        TryAddType(typeSystem, "Avalonia.Metadata.UsableDuringInitializationAttribute", mappings.UsableDuringInitializationAttributes);
        TryAddType(typeSystem, "Avalonia.Metadata.TemplateContentAttribute", mappings.DeferredContentPropertyAttributes);

        var rootObjectProvider = typeSystem.FindType("Avalonia.Markup.Xaml.IRootObjectProvider");
        if (rootObjectProvider != null)
        {
            mappings.RootObjectProvider = rootObjectProvider;
            mappings.RootObjectProviderIntermediateRootPropertyName = "IntermediateRootObject";
        }

        var uriContext = typeSystem.FindType("Avalonia.Markup.Xaml.IUriContext");
        if (uriContext != null)
            mappings.UriContextProvider = uriContext;

        var provideValueTarget = typeSystem.FindType("Avalonia.Markup.Xaml.IProvideValueTarget");
        if (provideValueTarget != null)
            mappings.ProvideValueTarget = provideValueTarget;

        var addChild = typeSystem.FindType("Avalonia.Metadata.IAddChild");
        if (addChild != null)
            mappings.IAddChild = addChild;

        var addChildOfT = typeSystem.FindType("Avalonia.Metadata.IAddChild`1");
        if (addChildOfT != null)
            mappings.IAddChildOfT = addChildOfT;

        var parentStackProvider = typeSystem.FindType("XamlX.Runtime.IXamlParentStackProviderV1");
        if (parentStackProvider != null)
            mappings.ParentStackProvider = parentStackProvider;

        var xmlNamespaceInfoProvider = typeSystem.FindType("XamlX.Runtime.IXamlXmlNamespaceInfoProviderV1");
        if (xmlNamespaceInfoProvider != null)
            mappings.XmlNamespaceInfoProvider = xmlNamespaceInfoProvider;

        return mappings;
    }

    private static void TryAddType(IXamlTypeSystem typeSystem, string typeName, List<IXamlType> list)
    {
        var type = typeSystem.FindType(typeName);
        if (type != null)
            list.Add(type);
    }

    /// <summary>
    /// Transformer that removes x:Class and other XAML directives before emit.
    /// </summary>
    private class RemoveXamlDirectivesTransformer : IXamlAstTransformer
    {
        public IXamlAstNode Transform(AstTransformationContext context, IXamlAstNode node)
        {
            if (node is XamlManipulationGroupNode group)
                group.Children.RemoveAll(n => n is XamlAstXmlDirective { Name: "Class" or "Key" or "Name" });
            if (node is XamlAstObjectNode objNode)
                objNode.Children.RemoveAll(n => n is XamlAstXmlDirective { Name: "Class" or "Key" or "Name" });
            if (node is XamlValueWithManipulationNode { Manipulation: XamlManipulationGroupNode manipGroup })
                manipGroup.Children.RemoveAll(n => n is XamlAstXmlDirective { Name: "Class" or "Key" or "Name" });
            return node;
        }
    }
}

internal class SourceGenFileSource : IFileSource
{
    public string FilePath { get; }
    public byte[] FileContents { get; }

    public SourceGenFileSource(string filePath, string content)
    {
        FilePath = filePath;
        FileContents = Encoding.UTF8.GetBytes(content);
    }
}
