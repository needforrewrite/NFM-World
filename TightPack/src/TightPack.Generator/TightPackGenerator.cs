using System.Collections.Generic;
using System.Collections.Immutable;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace TightPack.Generator;

/// <summary>
/// Emits <c>ITightPackable&lt;TSelf&gt;</c> implementations for <c>[TightPackable]</c> types.
/// </summary>
[Generator(LanguageNames.CSharp)]
public sealed class TightPackGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        IncrementalValuesProvider<Result> results = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                "TightPack.TightPackableAttribute",
                static (node, _) => node is TypeDeclarationSyntax,
                static (ctx, cancellationToken) => Parse(ctx, cancellationToken)
            );

        context.RegisterSourceOutput(results, static (production, result) =>
            {
                foreach (Diagnostic diagnostic in result.Diagnostics)
                {
                    production.ReportDiagnostic(diagnostic);
                }

                if (result.Source is not null)
                {
                    production.AddSource(result.HintName, SourceText.From(result.Source, Encoding.UTF8));
                }
            }
        );
    }

    private static Result Parse(GeneratorAttributeSyntaxContext ctx, System.Threading.CancellationToken cancellationToken)
    {
        if (ctx.TargetSymbol is not INamedTypeSymbol symbol)
        {
            return Result.Empty;
        }

        var sink = new DiagnosticSink();
        Compilation compilation = ctx.SemanticModel.Compilation;
        TypeModel? model = TypeParser.Parse(symbol, compilation, sink, cancellationToken);

        if (model is null)
        {
            return new Result(null, "", sink.ToImmutable());
        }

        return new Result(Emitter.Emit(model), model.HintName + ".TightPack.g.cs", sink.ToImmutable());
    }

    private readonly record struct Result(string? Source, string HintName, ImmutableArray<Diagnostic> Diagnostics)
    {
        public static Result Empty => new(null, "", ImmutableArray<Diagnostic>.Empty);
    }
}

/// <summary>Collects diagnostics so the parser does not need a Roslyn context threaded through it.</summary>
internal sealed class DiagnosticSink
{
    private readonly ImmutableArray<Diagnostic>.Builder _diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();

    public void ReportDiagnostic(Diagnostic diagnostic) => _diagnostics.Add(diagnostic);

    public ImmutableArray<Diagnostic> ToImmutable() => _diagnostics.ToImmutable();
}
