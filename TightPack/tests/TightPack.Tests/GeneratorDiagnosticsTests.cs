extern alias TightPackGenerator;

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using TightPackGenerator::TightPack.Generator;

namespace TightPack.Tests;

/// <summary>Runs the generator over source text so diagnostics can be asserted.</summary>
[TestClass]
public sealed class GeneratorDiagnosticsTests
{
    private const string Prelude = """
                                   using System;
                                   using System.Collections.Generic;
                                   using TightPack;

                                   namespace Probe;
                                   """;

    [TestMethod]
    public void MissingPartial_IsReported()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(
            """
            [TightPackable]
            public class NotPartial
            {
                public int Value { get; set; }
            }
            """
        );

        Assert.IsTrue(diagnostics.Any(d => d.Id == "TPK0001"), Describe(diagnostics));
    }

    [TestMethod]
    public void UnsupportedMemberType_IsReported()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(
            """
            [TightPackable]
            public partial class BadMember
            {
                public object? Anything { get; set; }
            }
            """
        );

        Assert.IsTrue(diagnostics.Any(d => d.Id == "TPK0004"), Describe(diagnostics));
    }

    [TestMethod]
    public void DuplicateUnionTag_IsReported()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(
            """
            [TightPackable(GenerateType.Union)]
            [TightPackUnion(1, typeof(A))]
            [TightPackUnion(1, typeof(B))]
            public partial interface IBase
            {
            }

            [TightPackable]
            public partial class A : IBase
            {
                public int X { get; set; }
            }

            [TightPackable]
            public partial class B : IBase
            {
                public int Y { get; set; }
            }
            """
        );

        Assert.IsTrue(diagnostics.Any(d => d.Id == "TPK0005"), Describe(diagnostics));
    }

    [TestMethod]
    public void UnionCaseThatIsNotPackable_IsReported()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(
            """
            [TightPackable(GenerateType.Union)]
            [TightPackUnion(1, typeof(Plain))]
            public partial interface IBase
            {
            }

            public class Plain
            {
            }
            """
        );

        Assert.IsTrue(diagnostics.Any(d => d.Id == "TPK0006"), Describe(diagnostics));
    }

    [TestMethod]
    public void ImmutableTypeWithoutMatchingConstructor_IsReported()
    {
        ImmutableArray<Diagnostic> diagnostics = Run(
            """
            [TightPackable]
            public partial class Frozen
            {
                public Frozen(string mismatched)
                {
                    Name = mismatched;
                }

                public string Name { get; }
                public int Other { get; }
            }
            """
        );

        Assert.IsTrue(diagnostics.Any(d => d.Id is "TPK0002"), Describe(diagnostics));
    }

    [TestMethod]
    public void PaddingFreeStruct_ProducesBlitCode()
    {
        string source = RunAndGetGenerated(
            """
            [TightPackable]
            public partial struct Point
            {
                public int X;
                public int Y;
            }
            """
        );

        StringAssert.Contains(source, "WriteBlittable");
        StringAssert.Contains(source, "VerifySize");
    }

    [TestMethod]
    public void PaddedStruct_UsesMemberWrites()
    {
        string source = RunAndGetGenerated(
            """
            [TightPackable]
            public partial struct Padded
            {
                public byte A;
                public long B;
            }
            """
        );

        Assert.DoesNotContain("WriteBlittable", source);
        StringAssert.Contains(source, "WriteByte");
    }

    [TestMethod]
    public void ExplicitLayoutStruct_IsNeverBlitted()
    {
        string source = RunAndGetGenerated(
            """
            using System.Runtime.InteropServices;

            [TightPackable]
            [StructLayout(LayoutKind.Explicit)]
            public partial struct Overlapped
            {
                [FieldOffset(0)] public int A;
                [FieldOffset(0)] public float B;
            }
            """
        );

        Assert.DoesNotContain("WriteBlittable", source);
    }

    [TestMethod]
    public void PackedStruct_IsBlittedWhenPackRemovesThePadding()
    {
        string source = RunAndGetGenerated(
            """
            using System.Runtime.InteropServices;

            [TightPackable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public partial struct Packed
            {
                public byte A;
                public long B;
            }
            """
        );

        StringAssert.Contains(source, "WriteBlittable(in value, 9)");
        StringAssert.Contains(source, "VerifySize");
    }

    [TestMethod]
    public void PackedStruct_IsNotBlittedWhenANestedTypeIsPadded()
    {
        // Pack applies to the struct it is declared on; the tuple inside it keeps its own layout, so
        // the outer image still contains the tuple's padding.
        string source = RunAndGetGenerated(
            """
            using System.Runtime.InteropServices;

            [TightPackable]
            [StructLayout(LayoutKind.Sequential, Pack = 1)]
            public partial struct Frame
            {
                public (sbyte Kind, long A, long B, long C) Stunt;
            }
            """
        );

        Assert.DoesNotContain("WriteBlittable", source);
    }

    [TestMethod]
    public void InlineArrayField_IsCountedAsRepeatedElements()
    {
        string source = RunAndGetGenerated(
            """
            using System.Runtime.CompilerServices;

            [TightPackable]
            public partial struct Quad
            {
                public InlineArray4<long> Values;
            }
            """
        );

        StringAssert.Contains(source, "WriteBlittable(in value, 32)");
    }

    [TestMethod]
    public void StructHoldingADateTime_IsNotBlitted()
    {
        // WriteDateTime goes through ToBinary, which is not the raw image of a local time.
        string source = RunAndGetGenerated(
            """
            [TightPackable]
            public partial struct Stamped
            {
                public long Ticks;
                public DateTime When;
            }
            """
        );

        Assert.DoesNotContain("WriteBlittable", source);
    }

    [TestMethod]
    public void SizedStruct_IsNotBlitted()
    {
        // Size overrides the computed layout, so the tail is padded.
        string source = RunAndGetGenerated(
            """
            using System.Runtime.InteropServices;

            [TightPackable]
            [StructLayout(LayoutKind.Sequential, Size = 16)]
            public partial struct FixedSize
            {
                public int A;
            }
            """
        );

        Assert.DoesNotContain("WriteBlittable", source);
    }

    private static ImmutableArray<Diagnostic> Run(string body)
    {
        (ImmutableArray<Diagnostic> diagnostics, _) = Compile(body);
        return diagnostics;
    }

    internal static string RunAndGetGenerated(string body)
    {
        (_, string source) = Compile(body);
        return source;
    }

    /// <summary>Generated sources keyed by hint name, so a test can assert on one type's output.</summary>
    internal static ImmutableDictionary<string, string> RunAndGetSources(string body)
    {
        (_, _, ImmutableDictionary<string, string> sources) = CompileAll(body);
        return sources;
    }

    internal static (ImmutableArray<Diagnostic> Diagnostics, string Generated) Compile(string body)
    {
        (ImmutableArray<Diagnostic> diagnostics, string generated, _) = CompileAll(body);
        return (diagnostics, generated);
    }

    private static (
        ImmutableArray<Diagnostic> Diagnostics,
        string Generated,
        ImmutableDictionary<string, string> Sources) CompileAll(string body)
    {
        var parseOptions = new CSharpParseOptions(LanguageVersion.Preview);
        var syntaxTree = CSharpSyntaxTree.ParseText(Prelude + "\n" + body, parseOptions);

        // The probe compiles for the running runtime, so it needs the whole trusted platform set.
        var references = new List<MetadataReference>();
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string platform)
        {
            foreach (string path in platform.Split(Path.PathSeparator))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        references.Add(MetadataReference.CreateFromFile(typeof(TightPackableAttribute).Assembly.Location));

        var compilation = CSharpCompilation.Create(
            "GeneratorProbe",
            [syntaxTree],
            references,
            new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                nullableContextOptions: NullableContextOptions.Enable
            )
        );

        // Generated sources are parsed with the driver's options, so they must match the input.
        var driver = CSharpGeneratorDriver.Create(
            [new TightPackGenerator::TightPack.Generator.TightPackGenerator().AsSourceGenerator()],
            parseOptions: parseOptions
        );
        GeneratorDriver result = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation updated,
            out ImmutableArray<Diagnostic> generatorDiagnostics
        );

        var diagnostics = ImmutableArray.CreateBuilder<Diagnostic>();
        diagnostics.AddRange(generatorDiagnostics);

        // Surface compile errors so a test that expects a diagnostic also catches a broken emit.
        diagnostics.AddRange(updated.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error));

        GeneratorRunResult[] runs = result.GetRunResult().Results.ToArray();

        string generated = string.Join(
            "\n",
            runs.SelectMany(r => r.GeneratedSources).Select(s => s.SourceText.ToString())
        );

        var sources = ImmutableDictionary.CreateBuilder<string, string>();
        foreach (GeneratedSourceResult source in runs.SelectMany(r => r.GeneratedSources))
        {
            sources[source.HintName] = source.SourceText.ToString();
        }

        return (diagnostics.ToImmutable(), generated, sources.ToImmutable());
    }

    private static string Describe(ImmutableArray<Diagnostic> diagnostics) =>
        string.Join("\n", diagnostics.Select(d => d.ToString()));
}
