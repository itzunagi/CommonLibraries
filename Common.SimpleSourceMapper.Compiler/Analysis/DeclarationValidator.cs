using System.Collections.Immutable;
using System.Linq;
using Common.SimpleSourceMapper.Compiler.Diagnostics;
using Common.SimpleSourceMapper.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Common.SimpleSourceMapper.Compiler.Analysis;

internal sealed record DeclarationProblem(DiagnosticDescriptor Descriptor, Location? Location, ImmutableArray<object> Arguments)
{
    public Diagnostic ToDiagnostic(Location fallback) => Diagnostic.Create(Descriptor, Location ?? fallback, Arguments.ToArray());
}

/// <summary>
/// Validates mapping declarations (attributes, signature, partial structure). Shared by the analyzer, which
/// reports the problems, and the generator, which silently skips invalid declarations.
/// </summary>
internal static class DeclarationValidator
{
    private const string GeneratedMarker = "Common.SimpleSourceMapper.Compiler";

    public static ImmutableArray<DeclarationProblem> Validate(IMethodSymbol method)
    {
        var facts = AttributeFacts.Read(method);
        if (!facts.HasAny)
        {
            return ImmutableArray<DeclarationProblem>.Empty;
        }

        var problems = ImmutableArray.CreateBuilder<DeclarationProblem>();
        var name = method.Name;
        var methodLocation = method.Locations.FirstOrDefault();

        if (facts.Map is not null && facts.Validate is not null)
        {
            problems.Add(Problem(
                DiagnosticDescriptors.ConflictingAttributes,
                AttributeFacts.GetLocation(facts.Validate) ?? methodLocation,
                name,
                "[Map] and [ValidateMapping] cannot be combined; use [Map] for generated mappings and [ValidateMapping] for handwritten ones"));
            return problems.ToImmutable();
        }

        if (facts.Map is null)
        {
            foreach (var forMember in facts.ForMembers)
            {
                problems.Add(Problem(
                    DiagnosticDescriptors.ConflictingAttributes,
                    AttributeFacts.GetLocation(forMember) ?? methodLocation,
                    name,
                    "[ForMember] is only valid together with [Map]"));
            }
        }

        var attribute = facts.Map ?? facts.Validate;
        if (attribute is null)
        {
            return problems.ToImmutable();
        }

        var isMap = facts.Map is not null;
        var memberList = AttributeFacts.ReadMemberList(attribute);
        if (!AttributeFacts.IsValidMemberList(memberList))
        {
            problems.Add(Problem(
                DiagnosticDescriptors.InvalidMemberList,
                AttributeFacts.GetLocation(attribute) ?? methodLocation,
                name,
                memberList));
        }

        ValidateSignature(method, isMap, problems);
        if (isMap)
        {
            ValidatePartialStructure(method, problems);
        }
        else
        {
            ValidateHandwrittenBody(method, problems);
        }

        return problems.ToImmutable();
    }

    private static void ValidateSignature(IMethodSymbol method, bool isMap, ImmutableArray<DeclarationProblem>.Builder problems)
    {
        var name = method.Name;
        var location = method.Locations.FirstOrDefault();

        void Signature(string reason) =>
            problems.Add(Problem(DiagnosticDescriptors.InvalidSignature, location, name, reason));

        if (!method.IsStatic)
        {
            Signature("mapping methods must be static");
        }

        if (method.DeclaredAccessibility != Accessibility.Public)
        {
            Signature("mapping methods must be public");
        }

        if (method.IsGenericMethod)
        {
            Signature("generic mapping methods are not supported");
        }

        if (method.IsExtensionMethod)
        {
            Signature("extension mapping methods are not supported");
        }

        if (method.IsAsync)
        {
            Signature("asynchronous mapping methods are not supported");
        }

        if (method.ReturnsByRef || method.ReturnsByRefReadonly)
        {
            Signature("ref-returning mapping methods are not supported");
        }

        if (method.ReturnsVoid)
        {
            Signature("a mapping method must return the destination type, not void");
        }

        if (method.Parameters.Length != 1)
        {
            Signature("a mapping method must declare exactly one source parameter");
        }
        else
        {
            var parameter = method.Parameters[0];
            if (parameter.RefKind != RefKind.None)
            {
                Signature("ref, in and out parameters are not supported");
            }

            if (parameter.IsParams)
            {
                Signature("params parameters are not supported");
            }

            if (parameter.HasExplicitDefaultValue)
            {
                Signature("the source parameter cannot have a default value");
            }
        }

        if (method.ContainingType is { } container && IsGenericContainer(container))
        {
            var descriptor = isMap ? DiagnosticDescriptors.InvalidPartialDeclaration : DiagnosticDescriptors.InvalidSignature;
            problems.Add(Problem(descriptor, location, name, "generic containing types are not supported"));
        }

        if (!isMap && !method.ReturnsVoid && method.Parameters.Length == 1 && !HasSignatureProblem(problems))
        {
            // Iterator methods cannot return the destination, but the check keeps the contract explicit.
            if (ContainsYield(method))
            {
                Signature("iterator mapping methods are not supported");
            }
        }

        if (!HasSignatureProblem(problems) && method.Parameters.Length == 1 && !method.ReturnsVoid)
        {
            var sourceReason = TypeFacts.DescribeUnsupportedMappingType(method.Parameters[0].Type);
            var destinationReason = TypeFacts.DescribeUnsupportedMappingType(method.ReturnType);
            foreach (var reason in new[] { sourceReason, destinationReason }.Where(r => r is not null))
            {
                problems.Add(Problem(
                    DiagnosticDescriptors.UnsupportedShape,
                    location,
                    name,
                    method.Parameters[0].Type.ToDisplayString(TypeFacts.Friendly),
                    method.ReturnType.ToDisplayString(TypeFacts.Friendly),
                    reason!));
            }
        }
    }

    private static bool HasSignatureProblem(ImmutableArray<DeclarationProblem>.Builder problems) =>
        problems.Any(p => p.Descriptor == DiagnosticDescriptors.InvalidSignature);

    private static bool IsGenericContainer(INamedTypeSymbol type)
    {
        for (var t = type; t is not null; t = t.ContainingType)
        {
            if (t.IsGenericType)
            {
                return true;
            }
        }

        return false;
    }

    private static bool ContainsYield(IMethodSymbol method)
    {
        foreach (var reference in method.DeclaringSyntaxReferences)
        {
            if (reference.GetSyntax() is MethodDeclarationSyntax syntax &&
                syntax.DescendantNodes().OfType<YieldStatementSyntax>().Any())
            {
                return true;
            }
        }

        return false;
    }

    private static void ValidatePartialStructure(IMethodSymbol method, ImmutableArray<DeclarationProblem>.Builder problems)
    {
        var name = method.Name;
        var location = method.Locations.FirstOrDefault();

        void Partial(string reason) =>
            problems.Add(Problem(DiagnosticDescriptors.InvalidPartialDeclaration, location, name, reason));

        if (!method.IsPartialDefinition)
        {
            Partial("a [Map] method must be declared 'partial' without a body; use [ValidateMapping] to validate a handwritten mapping");
        }
        else if (method.PartialImplementationPart is { } implementation && !IsGenerated(implementation))
        {
            Partial("a [Map] method must not have a handwritten implementation; remove the body or use [ValidateMapping]");
        }

        var syntax = method.DeclaringSyntaxReferences.Select(r => r.GetSyntax()).OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (syntax is null)
        {
            return;
        }

        if (method.IsPartialDefinition && (syntax.Body is not null || syntax.ExpressionBody is not null))
        {
            Partial("a [Map] method must be declared without a body");
        }

        foreach (var ancestor in syntax.Ancestors().OfType<TypeDeclarationSyntax>())
        {
            if (ancestor is not ClassDeclarationSyntax)
            {
                Partial($"containing type '{ancestor.Identifier.Text}' must be a class");
            }
            else if (!ancestor.Modifiers.Any(m => m.IsKind(SyntaxKind.PartialKeyword)))
            {
                Partial($"containing type '{ancestor.Identifier.Text}' must be declared 'partial'");
            }

            if (ancestor.Modifiers.Any(m => m.Text == "file"))
            {
                Partial($"file-local containing type '{ancestor.Identifier.Text}' is not supported");
            }
        }
    }

    private static void ValidateHandwrittenBody(IMethodSymbol method, ImmutableArray<DeclarationProblem>.Builder problems)
    {
        if (!HasBody(method))
        {
            problems.Add(Problem(
                DiagnosticDescriptors.ConflictingAttributes,
                method.Locations.FirstOrDefault(),
                method.Name,
                "[ValidateMapping] requires a handwritten implementation; use [Map] for generated mappings"));
        }
    }

    public static bool HasBody(IMethodSymbol method) =>
        method.DeclaringSyntaxReferences
            .Select(r => r.GetSyntax())
            .OfType<MethodDeclarationSyntax>()
            .Any(s => s.Body is not null || s.ExpressionBody is not null);

    /// <summary>True when the implementing declaration was produced by this generator.</summary>
    public static bool IsGenerated(IMethodSymbol implementation)
    {
        foreach (var reference in implementation.DeclaringSyntaxReferences)
        {
            var text = reference.SyntaxTree.GetText();
            var length = System.Math.Min(text.Length, 400);
            if (text.ToString(new TextSpan(0, length)).Contains(GeneratedMarker))
            {
                return true;
            }
        }

        return false;
    }

    private static DeclarationProblem Problem(DiagnosticDescriptor descriptor, Location? location, params object[] arguments) =>
        new(descriptor, location, arguments.ToImmutableArray());
}
