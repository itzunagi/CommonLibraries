using System.Collections.Immutable;
using System.Linq;
using Common.SimpleSourceMapper.Compiler.Diagnostics;
using Common.SimpleSourceMapper.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Common.SimpleSourceMapper.Compiler.Analysis;

/// <summary>
/// Reports attribute and declaration misuse and validates handwritten <c>[ValidateMapping]</c> methods.
/// Mapping-resolution failures for <c>[Map]</c> methods are owned by the generator.
/// </summary>
[DiagnosticAnalyzer(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
public sealed class MappingAnalyzer : DiagnosticAnalyzer
{
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } =
        DiagnosticDescriptors.Descriptors.ToImmutableArray();

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeMethod, SyntaxKind.MethodDeclaration);
    }

    private static void AnalyzeMethod(SyntaxNodeAnalysisContext context)
    {
        var syntax = (MethodDeclarationSyntax)context.Node;
        if (syntax.AttributeLists.Count == 0 ||
            context.SemanticModel.GetDeclaredSymbol(syntax, context.CancellationToken) is not { } method)
        {
            return;
        }

        var facts = AttributeFacts.Read(method);
        if (!facts.HasAny)
        {
            return;
        }

        var fallback = method.Locations.FirstOrDefault() ?? Location.None;
        var problems = DeclarationValidator.Validate(method);
        foreach (var problem in problems)
        {
            context.ReportDiagnostic(problem.ToDiagnostic(fallback));
        }

        if (!problems.IsEmpty || facts.Validate is not { } attribute)
        {
            return;
        }

        var memberList = AttributeFacts.ReadMemberList(attribute);
        if (memberList == MemberListFlags.None)
        {
            return;
        }

        AnalyzeHandwritten(context, syntax, method, memberList, fallback);
    }

    private static void AnalyzeHandwritten(
        SyntaxNodeAnalysisContext context,
        MethodDeclarationSyntax syntax,
        IMethodSymbol method,
        int memberList,
        Location location)
    {
        var sourceType = method.Parameters[0].Type;
        var destinationType = method.ReturnType;
        var sourceName = sourceType.ToDisplayString(TypeFacts.Friendly);
        var destinationName = destinationType.ToDisplayString(TypeFacts.Friendly);

        var sourceMembers = MemberInventory.ForSource(sourceType);
        var destinationMembers = MemberInventory.ForDestination(destinationType);

        var checkSource = (memberList & MemberListFlags.Source) != 0;
        var checkDestination = (memberList & MemberListFlags.Destination) != 0;

        var hidden = (checkSource ? sourceMembers.AmbiguousNames : ImmutableArray<string>.Empty)
            .Concat(checkDestination ? destinationMembers.AmbiguousNames : ImmutableArray<string>.Empty)
            .Distinct()
            .ToList();
        if (hidden.Count > 0)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnsupportedShape,
                location,
                method.Name,
                sourceName,
                destinationName,
                $"property '{hidden[0]}' is declared more than once through hiding and is ambiguous"));
            return;
        }

        if (context.SemanticModel.GetOperation(syntax, context.CancellationToken) is not IMethodBodyOperation body)
        {
            ReportUnprovable(context, method, sourceName, destinationName, location, "the method body could not be analyzed");
            return;
        }

        var result = HandwrittenFlowAnalysis.Analyze(
            body, method.Parameters[0], destinationType, sourceMembers, destinationMembers, memberList);

        foreach (var reason in result.UnprovableReasons)
        {
            ReportUnprovable(context, method, sourceName, destinationName, location, reason);
        }

        foreach (var name in result.UnreadSourceProperties)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnusedSourceProperty, location, method.Name, sourceName, destinationName, name));
        }

        foreach (var name in result.UnassignedDestinationProperties)
        {
            context.ReportDiagnostic(Diagnostic.Create(
                DiagnosticDescriptors.UnassignedDestinationProperty, location, method.Name, sourceName, destinationName, name));
        }
    }

    private static void ReportUnprovable(
        SyntaxNodeAnalysisContext context,
        IMethodSymbol method,
        string sourceName,
        string destinationName,
        Location location,
        string reason)
    {
        context.ReportDiagnostic(Diagnostic.Create(
            DiagnosticDescriptors.CoverageNotProven, location, method.Name, sourceName, destinationName, reason));
    }
}
