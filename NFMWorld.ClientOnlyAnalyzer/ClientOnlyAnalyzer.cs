using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace NFMWorld.ClientOnlyAnalyzer;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ClientOnlyAnalyzer : DiagnosticAnalyzer
{
    public const string DiagnosticId = "NFMW0001";

    private const string Category = "Usage";

    private const string ClientOnlyAttributeFullName = "NFMWorldLibrary.Backend.Gamemodes.ClientOnlyAttribute";
    private const string ClientServerTypeFullName = "NFMWorldLibrary.Backend.Gamemodes.ClientServer";
    private const string RunIfOnClientMethodName = "RunIfOnClient";

    /// <summary>
    /// The name of the NFMWorld executable project assembly.
    /// </summary>
    private const string NfmWorldAssemblyName = "NFMWorld";

    private static readonly DiagnosticDescriptor Rule = new(
        id: DiagnosticId,
        title: "Client-only member accessed outside NFMWorld or RunIfOnClient",
        messageFormat: "'{0}' is marked [ClientOnly] and can only be accessed from the NFMWorld assembly or via ClientServer.RunIfOnClient",
        category: Category,
        defaultSeverity: DiagnosticSeverity.Error,
        isEnabledByDefault: true,
        description: "Members annotated with [ClientOnly] (or belonging to a [ClientOnly] type) must only be accessed from within the NFMWorld project or passed as a delegate to ClientServer.RunIfOnClient.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();

        // Method / delegate invocations.
        context.RegisterSyntaxNodeAction(AnalyzeInvocation, SyntaxKind.InvocationExpression);

        // Property / field reads: obj.Member  or  Member  or  obj?.Member
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.SimpleMemberAccessExpression);
        context.RegisterSyntaxNodeAction(AnalyzeMemberAccess, SyntaxKind.IdentifierName);

        // Property / field writes: obj.Member = value  or  Member = value
        context.RegisterSyntaxNodeAction(AnalyzeAssignment, SyntaxKind.SimpleAssignmentExpression);
    }

    // ── shared reporting logic ────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if a diagnostic should be reported for access to
    /// <paramref name="symbol"/> at <paramref name="node"/>.
    /// </summary>
    private static bool ShouldReport(
        ISymbol symbol,
        SyntaxNode node,
        SyntaxNodeAnalysisContext context)
    {
        if (!IsClientOnly(symbol))
            return false;

        if (context.Compilation.Assembly.Name == NfmWorldAssemblyName)
            return false;

        if (IsInsideRunIfOnClientArgument(node, context.SemanticModel, context.CancellationToken))
            return false;

        if (IsInsideClientOnlyChain(node, context.SemanticModel, context.CancellationToken))
            return false;

        return true;
    }

    // ── invocation analysis ───────────────────────────────────────────

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;

        if (context.SemanticModel.GetSymbolInfo(invocation, context.CancellationToken).Symbol
            is not IMethodSymbol methodSymbol)
        {
            return;
        }

        if (ShouldReport(methodSymbol, invocation, context))
        {
            var diagnostic = Diagnostic.Create(Rule, invocation.GetLocation(), methodSymbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    // ── member access (property / field reads) ────────────────────────

    private static void AnalyzeMemberAccess(SyntaxNodeAnalysisContext context)
    {
        var node = context.Node;

        // ── skip nameof(…) — the member name is a string literal, not an access.
        if (IsInsideNameOf(node))
            return;

        // ── de-duplicate: skip the inner IdentifierName of a
        //    MemberAccessExpression — the outer node already reports.
        if (node is IdentifierNameSyntax identifier)
        {
            if (identifier.Parent is MemberAccessExpressionSyntax memberAccess
                && memberAccess.Name == identifier)
            {
                return;
            }
        }

        // ── skip the left-hand side of an assignment — AnalyzeAssignment
        //    already reports those.
        if (node.Parent is AssignmentExpressionSyntax assignment
            && assignment.Left == node)
        {
            return;
        }

        // ── skip when used as an argument — the member is being passed as a
        //    delegate or ref, not accessed for its value.
        if (node.Parent is ArgumentSyntax)
            return;

        var symbol = context.SemanticModel.GetSymbolInfo(node, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol and not IFieldSymbol)
            return;

        if (ShouldReport(symbol, node, context))
        {
            var diagnostic = Diagnostic.Create(Rule, node.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    // ── assignment (property / field writes) ──────────────────────────

    private static void AnalyzeAssignment(SyntaxNodeAnalysisContext context)
    {
        var assignment = (AssignmentExpressionSyntax)context.Node;

        var symbol = context.SemanticModel.GetSymbolInfo(assignment.Left, context.CancellationToken).Symbol;
        if (symbol is not IPropertySymbol and not IFieldSymbol)
            return;

        if (ShouldReport(symbol, assignment, context))
        {
            var diagnostic = Diagnostic.Create(Rule, assignment.Left.GetLocation(), symbol.Name);
            context.ReportDiagnostic(diagnostic);
        }
    }

    // ── [ClientOnly] detection ────────────────────────────────────────

    /// <summary>
    /// Returns <c>true</c> if <paramref name="symbol"/> is considered
    /// [ClientOnly], either directly or via a type-level annotation.
    /// </summary>
    private static bool IsClientOnly(ISymbol symbol)
    {
        switch (symbol)
        {
            case IMethodSymbol method:
                return IsClientOnlyMethod(method);
            case IPropertySymbol property:
                return IsClientOnlyProperty(property);
            case IFieldSymbol field:
                return IsClientOnlyField(field);
            default:
                return false;
        }
    }

    /// <summary>
    /// Checks whether the method symbol has the <c>[ClientOnly]</c> attribute.
    /// Checks the method itself, its <c>OriginalDefinition</c>, overridden
    /// methods, explicitly implemented interface methods, and — for methods
    /// resolved on a class that inherits a default interface method — the
    /// corresponding interface method declaration.  Also checks the declaring
    /// type for a type-level <c>[ClientOnly]</c>.
    /// </summary>
    private static bool IsClientOnlyMethod(IMethodSymbol methodSymbol)
    {
        // Check the method symbol itself.
        if (HasAttributeOnSymbol(methodSymbol))
            return true;

        // Check the original definition (e.g., for overrides or base. calls
        // Roslyn may resolve to a symbol that strips attributes).
        if (methodSymbol.OriginalDefinition is { } original
            && !SymbolEqualityComparer.Default.Equals(original, methodSymbol))
        {
            if (HasAttributeOnSymbol(original))
                return true;
        }

        // Check overridden method chain.
        for (var overridden = methodSymbol.OverriddenMethod;
             overridden is not null;
             overridden = overridden.OverriddenMethod)
        {
            if (HasAttributeOnSymbol(overridden))
                return true;
        }

        // Check explicitly implemented interface methods.
        foreach (var ifaceMethod in methodSymbol.ExplicitInterfaceImplementations)
        {
            if (HasAttributeOnSymbol(ifaceMethod))
                return true;
        }

        // When Roslyn resolves a base.X() call for a default interface method,
        // it synthesises a method on the base class that does not carry the
        // original [ClientOnly] attribute. Walk all interfaces of the
        // containing type and check matching methods.
        if (methodSymbol.ContainingType is { } containingType)
        {
            foreach (var iface in containingType.AllInterfaces)
            {
                foreach (var ifaceMember in iface.GetMembers(methodSymbol.Name))
                {
                    if (ifaceMember is not IMethodSymbol ifaceMethod)
                        continue;

                    if (ifaceMethod.Parameters.Length != methodSymbol.Parameters.Length)
                        continue;

                    if (HasAttributeOnSymbol(ifaceMethod))
                        return true;
                }
            }
        }

        // Check the associated symbol (e.g. property getter/setter).
        if (methodSymbol.AssociatedSymbol is { } associated
            && HasAttributeOnSymbol(associated))
        {
            return true;
        }

        // Check declaring type for type-level [ClientOnly].
        if (methodSymbol.ContainingType is { } ct
            && HasTypeLevelClientOnly(ct))
        {
            return true;
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the property itself, its getter, its setter,
    /// or its declaring type has <c>[ClientOnly]</c>.
    /// </summary>
    private static bool IsClientOnlyProperty(IPropertySymbol property)
    {
        if (HasAttributeOnSymbol(property))
            return true;

        if (property.GetMethod is { } getter && IsClientOnlyMethod(getter))
            return true;

        if (property.SetMethod is { } setter && IsClientOnlyMethod(setter))
            return true;

        if (property.ContainingType is { } ct && HasTypeLevelClientOnly(ct))
            return true;

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if the field itself or its declaring type has
    /// <c>[ClientOnly]</c>.
    /// </summary>
    private static bool IsClientOnlyField(IFieldSymbol field)
    {
        if (HasAttributeOnSymbol(field))
            return true;

        if (field.ContainingType is { } ct && HasTypeLevelClientOnly(ct))
            return true;

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="type"/> is annotated with
    /// <c>[ClientOnly]</c> (allowing the type itself to be the gate for
    /// all its members).
    /// </summary>
    private static bool HasTypeLevelClientOnly(INamedTypeSymbol type)
    {
        return HasAttributeOnSymbol(type);
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="symbol"/> directly bears a
    /// <c>[ClientOnly]</c> attribute.
    /// </summary>
    private static bool HasAttributeOnSymbol(ISymbol symbol)
    {
        foreach (var attr in symbol.GetAttributes())
        {
            if (attr.AttributeClass is { } attrClass
                && GetFullMetadataName(attrClass) == ClientOnlyAttributeFullName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Returns <c>true</c> if <paramref name="node"/> is inside a
    /// <c>nameof(...)</c> expression.
    /// </summary>
    private static bool IsInsideNameOf(SyntaxNode node)
    {
        for (var current = node.Parent; current is not null; current = current.Parent)
        {
            if (current is InvocationExpressionSyntax invocation
                && invocation.Expression is IdentifierNameSyntax name
                && name.Identifier.Text == "nameof")
            {
                return true;
            }

            // Stop at member or type declarations.
            if (current is MemberDeclarationSyntax or TypeDeclarationSyntax)
                break;
        }

        return false;
    }

    /// <summary>
    /// Walks up the syntax tree from <paramref name="node"/> to determine
    /// whether the access is nested inside a member that is itself in the
    /// "client-only chain" — i.e. the containing member (method, property,
    /// accessor, constructor, etc.) has <c>[ClientOnly]</c> or belongs to a
    /// <c>[ClientOnly]</c> type. Also checks local functions, walking up to
    /// the nearest enclosing member.
    /// </summary>
    private static bool IsInsideClientOnlyChain(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode? current = node.Parent;
        while (current is not null)
        {
            if (GetMemberSymbol(current, semanticModel, cancellationToken) is { } symbol
                && IsClientOnly(symbol))
            {
                return true;
            }

            if (current is AnonymousFunctionExpressionSyntax)
            {
                // Lambdas / delegate {} don't carry attributes — keep
                // walking up to the enclosing named member.
            }

            if (current is TypeDeclarationSyntax)
                break;

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Returns the <see cref="ISymbol"/> for a member declaration node,
    /// or <c>null</c> if the node does not declare a member.
    /// </summary>
    private static ISymbol? GetMemberSymbol(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        switch (node)
        {
            // Methods, constructors, operators, destructors.
            case BaseMethodDeclarationSyntax m:
                return semanticModel.GetDeclaredSymbol(m, cancellationToken);

            // Properties (including expression-bodied).
            case PropertyDeclarationSyntax p:
                return semanticModel.GetDeclaredSymbol(p, cancellationToken);

            // Property / event accessors (get, set, add, remove).
            case AccessorDeclarationSyntax a:
                return semanticModel.GetDeclaredSymbol(a, cancellationToken);

            // Events (including expression-bodied).
            case EventDeclarationSyntax e:
                return semanticModel.GetDeclaredSymbol(e, cancellationToken);

            // Local functions.
            case LocalFunctionStatementSyntax l:
                return semanticModel.GetDeclaredSymbol(l, cancellationToken);

            // Field initializers: walk up to the VariableDeclarator.
            case VariableDeclaratorSyntax v:
                return semanticModel.GetDeclaredSymbol(v, cancellationToken);

            default:
                return null;
        }
    }

    /// <summary>
    /// Walks up the syntax tree from <paramref name="node"/> to determine
    /// whether the access is nested inside a delegate (lambda or anonymous
    /// method) that is passed as an argument to
    /// <c>ClientServer.RunIfOnClient</c>.
    /// </summary>
    private static bool IsInsideRunIfOnClientArgument(
        SyntaxNode node,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        SyntaxNode? current = node.Parent;

        while (current is not null)
        {
            // Stop if we hit a member declaration — we've left the local scope.
            if (current is MemberDeclarationSyntax)
            {
                return false;
            }

            // Check if the current node is an argument to an invocation.
            if (current is ArgumentSyntax argument
                && argument.Parent is ArgumentListSyntax argList
                && argList.Parent is InvocationExpressionSyntax outerInvocation)
            {
                if (IsRunIfOnClientInvocation(outerInvocation, semanticModel, cancellationToken))
                {
                    return true;
                }
            }

            current = current.Parent;
        }

        return false;
    }

    /// <summary>
    /// Determines whether <paramref name="invocation"/> is a call to
    /// <c>ClientServer.RunIfOnClient</c> (either overload).
    /// </summary>
    private static bool IsRunIfOnClientInvocation(
        InvocationExpressionSyntax invocation,
        SemanticModel semanticModel,
        CancellationToken cancellationToken)
    {
        if (semanticModel.GetSymbolInfo(invocation, cancellationToken).Symbol
            is not IMethodSymbol methodSymbol)
        {
            return false;
        }

        return methodSymbol.Name == RunIfOnClientMethodName
               && methodSymbol.ContainingType is { } containingType
               && GetFullMetadataName(containingType) == ClientServerTypeFullName;
    }

    /// <summary>
    /// Builds the full metadata name (namespace + type name) for a type symbol,
    /// recursing through nested types.
    /// </summary>
    private static string GetFullMetadataName(INamedTypeSymbol type)
    {
        var parts = new List<string>();
        BuildName(type, parts);
        return string.Join(".", parts);

        static void BuildName(INamedTypeSymbol t, List<string> list)
        {
            if (t.ContainingType is { } outer)
            {
                BuildName(outer, list);
                list.Add(t.Name);
            }
            else
            {
                if (t.ContainingNamespace is { IsGlobalNamespace: false } ns)
                {
                    list.Add(ns.ToDisplayString());
                }
                list.Add(t.Name);
            }
        }
    }
}
