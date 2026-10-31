using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using WorldXaml.Generator.Common;

namespace NFMWorld.OpcodeSourceGen;

[Generator(LanguageNames.CSharp)]
public class OpcodeIncrementalGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor NoOpcodeDiagnostic = new(
        id: "NFMW0002",
        title: "Packet opcode must be an integral constant",
        messageFormat: "Packet '{0}' has a non-integral opcode; the packet dispatcher cannot be generated for it",
        category: "NFMWorld.Opcode",
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true);

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var c2sPackets = context.SyntaxProvider.ForAttributeWithMetadataName(
            "NFMWorldLibrary.Multiplayer.Packets.PacketClientToServerAttribute",
            (node, ct) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
            (ctx, ct) => ReadPacket(ctx)
        );
        var s2cPackets = context.SyntaxProvider.ForAttributeWithMetadataName(
            "NFMWorldLibrary.Multiplayer.Packets.PacketServerToClientAttribute",
            (node, ct) => node is ClassDeclarationSyntax or StructDeclarationSyntax,
            (ctx, ct) => ReadPacket(ctx)
        );
        
        context.RegisterSourceOutput(c2sPackets, GeneratePacket);
        context.RegisterSourceOutput(s2cPackets, GeneratePacket);
        
        context.RegisterSourceOutput(c2sPackets.Collect().Combine(s2cPackets.Collect()), (ctx, pair) =>
        {
            var (c2s, s2c) = pair;

            // Ordered, so the generated file is stable across incremental runs: the
            // Collect() order is not guaranteed.
            c2s = c2s.OrderBy(p => p.Opcode ?? int.MaxValue).ThenBy(p => p.FullName, StringComparer.Ordinal).ToImmutableArray();
            s2c = s2c.OrderBy(p => p.Opcode ?? int.MaxValue).ThenBy(p => p.FullName, StringComparer.Ordinal).ToImmutableArray();

            var sb = new IndentedStringBuilder();

            sb.AppendJoin(c2s.Concat(s2c)
                .Select(p => p.Namespace)
                .Where(ns => ns is not null)
                .Distinct(StringComparer.Ordinal)
                .Select(ns => $"using {ns};"), '\n');

            sb.AppendLine();

            sb.AppendLine(
                $$"""
                  #nullable enable
                  using MemoryPack;
                  using NFMWorldLibrary.Util;

                  namespace NFMWorldLibrary.Multiplayer;

                  public static class MultiplayerUtils
                  {
                  """);

            using (sb.Indent())
            {
                sb.AppendLine("private static T DeserializePacket<T>(ReadOnlyMemory<byte> data) where T : IReadableWritable<T>, IMemoryPackable<T> => T.Read(data);");
                sb.AppendLine("public static IPacketClientToServer? TryDeserializeC2SPacket(sbyte opcode, ReadOnlyMemory<byte> data)");
                sb.AppendLine("{");

                using (sb.Indent())
                {
                    sb.AppendLine("return opcode switch");
                    sb.AppendLine("{");

                    using (sb.Indent())
                    {
                        foreach (var (@namespace, name, _, opcode, isStruct, isRecord) in c2s)
                        {
                            if (@namespace is null || name is null || opcode is null) continue;

                            sb.AppendLine($"{opcode} => DeserializePacket<{name}>(data),");
                        }

                        sb.AppendLine("_ => null");
                    }

                    sb.AppendLine("};");
                }

                sb.AppendLine("}");
                
                sb.AppendLine("public static IPacketServerToClient? TryDeserializeS2CPacket(sbyte opcode, ReadOnlyMemory<byte> data)");
                sb.AppendLine("{");

                using (sb.Indent())
                {
                    sb.AppendLine("return opcode switch");
                    sb.AppendLine("{");
                    
                    using (sb.Indent())
                    {
                        foreach (var (@namespace, name, _, opcode, isStruct, isRecord) in s2c)
                        {
                            if (@namespace is null || name is null || opcode is null) continue;

                            sb.AppendLine($"{opcode} => DeserializePacket<{name}>(data),");
                        }

                        sb.AppendLine("_ => null");
                    }

                    sb.AppendLine("};");
                }

                sb.AppendLine("}");
            }
            
            sb.AppendLine("}");
            
            ctx.AddSource($"MultiplayerUtils.g.cs", sb.ToString());
        });
        return;

        static (string? Namespace, string? Name, string? FullName, sbyte? Opcode, bool IsStruct, bool IsRecord) ReadPacket(GeneratorAttributeSyntaxContext ctx)
        {
            var node = (TypeDeclarationSyntax)ctx.TargetNode;
            var typeSymbol = ctx.SemanticModel.GetDeclaredSymbol(node) as ITypeSymbol;
            var @namespace = typeSymbol?.ContainingNamespace.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat).Remove(0, "global::".Length);
            var name = typeSymbol?.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat);
            var fullName = typeSymbol?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);

            // The opcode is declared as an sbyte, but a literal reaches us as whatever the
            // constant's type is (enum member, int literal, const) - reading it strictly as
            // sbyte silently produced a null opcode and an uncompilable bundle. Convert
            // instead, and leave null only for a value that really is not an integral.
            sbyte? opcode = null;
            if (ctx.Attributes.FirstOrDefault()?.ConstructorArguments.FirstOrDefault() is { } argument &&
                argument.Value is not null)
            {
                try
                {
                    opcode = Convert.ToSByte(argument.Value, System.Globalization.CultureInfo.InvariantCulture);
                }
                catch (Exception e) when (e is OverflowException or InvalidCastException or FormatException)
                {
                    opcode = null;
                }
            }

            return (@namespace, name, fullName, opcode, node is StructDeclarationSyntax, node is RecordDeclarationSyntax);
        }

        static void GeneratePacket(SourceProductionContext ctx, (string? Namespace, string? Name, string? FullName, sbyte? Opcode, bool IsStruct, bool IsRecord) packetInfo)
        {
            // No opcode means the attribute argument was not an integral, which is a source
            // error the developer needs to see - emitting `Opcode => ` with a null would
            // fail later with a message about the generated file.
            if (packetInfo is { Namespace: not null, Name: not null, Opcode: null })
            {
                ctx.ReportDiagnostic(Diagnostic.Create(
                    NoOpcodeDiagnostic,
                    Location.None,
                    packetInfo.FullName ?? packetInfo.Name));
                return;
            }

            var sb = new IndentedStringBuilder();

            sb.AppendLine(
                $$"""
                  #nullable enable
                  using MemoryPack;
                  
                  namespace {{packetInfo.Namespace}};

                  partial {{(packetInfo.IsStruct ? "struct" : packetInfo.IsRecord ? "record" : "class")}} {{packetInfo.Name}}
                  {
                      [MemoryPackIgnore]
                      public {{(packetInfo.IsStruct ? "readonly " : "")}}sbyte Opcode => {{packetInfo.Opcode}};
                  }
                  """);
            
            // Keyed by full name: a minimally-qualified name collides when two packets in
            // different namespaces share a short name.
            var hintName = (packetInfo.FullName ?? packetInfo.Name!).Replace("global::", "").Replace('.', '_');
            ctx.AddSource($"{hintName}.g.cs", sb.ToString());
        }
    }
}