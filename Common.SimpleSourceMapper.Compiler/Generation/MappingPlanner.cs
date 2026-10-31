using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Common.SimpleSourceMapper.Compiler.Analysis;
using Common.SimpleSourceMapper.Compiler.Diagnostics;
using Common.SimpleSourceMapper.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Common.SimpleSourceMapper.Compiler.Generation;

/// <summary>
/// Plans a single <c>[Map]</c> declaration: member matching, <c>ForMember</c> overrides, scalar conversions,
/// nullability, construction and completeness. Nested mappers are resolved later against the registry.
/// </summary>
internal sealed class MappingPlanner
{
    private readonly IMethodSymbol _method;
    private readonly Compilation _compilation;
    private readonly ImmutableArray<DiagnosticInfo>.Builder _diagnostics = ImmutableArray.CreateBuilder<DiagnosticInfo>();
    private readonly string _sourceDisplay;
    private readonly string _destinationDisplay;
    private readonly LocationInfo? _methodLocation;

    private MappingPlanner(IMethodSymbol method, Compilation compilation)
    {
        _method = method;
        _compilation = compilation;
        _sourceDisplay = method.Parameters[0].Type.ToDisplayString(TypeFacts.Friendly);
        _destinationDisplay = method.ReturnType.ToDisplayString(TypeFacts.Friendly);
        _methodLocation = LocationInfo.From(method.Locations.FirstOrDefault());
    }

    /// <summary>Returns null when the declaration is invalid; the analyzer reports those problems.</summary>
    public static MappingModel? Plan(IMethodSymbol method, SemanticModel semanticModel, CancellationToken cancellationToken)
    {
        if (!DeclarationValidator.Validate(method).IsEmpty)
        {
            return null;
        }

        var facts = AttributeFacts.Read(method);
        if (facts.Map is null)
        {
            return null;
        }

        var planner = new MappingPlanner(method, semanticModel.Compilation);
        var memberList = AttributeFacts.ReadMemberList(facts.Map);
        var position = method.Locations.FirstOrDefault()?.SourceSpan.Start ?? 0;
        var nullableEnabled = semanticModel.SyntaxTree == method.Locations.FirstOrDefault()?.SourceTree &&
                              semanticModel.GetNullableContext(position).AnnotationsEnabled();

        var assignments = planner.PlanAssignments(facts, memberList, cancellationToken);
        return planner.Build(assignments, nullableEnabled);
    }

    private MappingModel Build(ImmutableArray<AssignmentModel> assignments, bool nullableEnabled)
    {
        var method = _method;
        var source = method.Parameters[0].Type;
        var destination = method.ReturnType;
        var containers = new List<string>();
        var externallyAccessible = method.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;
        for (var t = method.ContainingType; t is not null; t = t.ContainingType)
        {
            containers.Insert(0, Identifier.Escape(t.Name));
            externallyAccessible &= t.DeclaredAccessibility is Accessibility.Public or Accessibility.Internal;
        }

        var ns = Identifier.FormatNamespace(method.ContainingNamespace);
        var containerFullName = method.ContainingType.ToDisplayString(TypeFacts.Qualified);
        var sourceQualified = source.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified);

        return new MappingModel(
            ns,
            containers.ToImmutableArray(),
            containerFullName,
            externallyAccessible,
            Identifier.Escape(method.Name),
            $"{containerFullName}.{method.Name}({sourceQualified})",
            $"{containerFullName}.{Identifier.Escape(method.Name)}",
            Identifier.Escape(method.Parameters[0].Name),
            sourceQualified,
            source.ToDisplayString(TypeFacts.QualifiedAnnotated),
            sourceQualified,
            _sourceDisplay,
            !source.IsValueType,
            destination.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified),
            destination.ToDisplayString(TypeFacts.QualifiedAnnotated),
            destination.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified),
            _destinationDisplay,
            nullableEnabled,
            _methodLocation,
            assignments.ToImmutableArray(),
            _diagnostics.ToImmutable());
    }

    private void Report(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] arguments) =>
        _diagnostics.Add(new DiagnosticInfo(descriptor.Id, location ?? _methodLocation, arguments.ToImmutableArray()));

    private void ReportMapping(DiagnosticDescriptor descriptor, LocationInfo? location, params string[] extra) =>
        Report(descriptor, location, new[] { _method.Name, _sourceDisplay, _destinationDisplay }.Concat(extra).ToArray());

    private ImmutableArray<AssignmentModel> PlanAssignments(AttributeFacts facts, int memberList, CancellationToken cancellationToken)
    {
        var source = _method.Parameters[0].Type;
        var destination = (INamedTypeSymbol)_method.ReturnType;
        var sourceMembers = MemberInventory.ForSource(source);
        var destinationMembers = MemberInventory.ForDestination(destination);
        var validateSource = (memberList & MemberListFlags.Source) != 0;
        var validateDestination = (memberList & MemberListFlags.Destination) != 0;

        var ambiguous = sourceMembers.AmbiguousNames.Concat(destinationMembers.AmbiguousNames).Distinct().ToList();
        if (ambiguous.Count > 0)
        {
            ReportMapping(
                DiagnosticDescriptors.UnsupportedShape,
                null,
                $"property '{ambiguous[0]}' is declared more than once through hiding and is ambiguous");
            return ImmutableArray<AssignmentModel>.Empty;
        }

        var setsRequiredMembers = CheckConstruction(destination);

        var overrides = new Dictionary<string, string>(StringComparer.Ordinal);
        var settled = new HashSet<string>(StringComparer.Ordinal);
        var used = new HashSet<string>(StringComparer.Ordinal);
        ParseForMembers(facts, sourceMembers, destinationMembers, overrides, settled, used);

        var assignments = ImmutableArray.CreateBuilder<AssignmentModel>();

        foreach (var property in destinationMembers.Properties)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (settled.Contains(property.Name))
            {
                continue;
            }

            string? sourceName = null;
            if (overrides.TryGetValue(property.Name, out var overridden))
            {
                sourceName = overridden;
            }
            else if (sourceMembers.Contains(property.Name))
            {
                sourceName = property.Name;
            }

            if (sourceName is null)
            {
                if (!setsRequiredMembers && MemberInventory.IsRequired(property))
                {
                    ReportMapping(
                        DiagnosticDescriptors.InvalidConstruction,
                        null,
                        $"required member '{property.Name}' has no matching source property; add a [ForMember] or a source property named '{property.Name}'");
                }
                else if (validateDestination)
                {
                    ReportMapping(DiagnosticDescriptors.UnassignedDestinationProperty, null, property.Name);
                }

                continue;
            }

            var sourceProperty = sourceMembers.Find(sourceName)!;
            used.Add(sourceName);

            var assignment = PlanAssignment(sourceProperty, property);
            if (assignment is not null)
            {
                assignments.Add(assignment);
            }
        }

        if (validateSource)
        {
            foreach (var property in sourceMembers.Properties.Where(p => !used.Contains(p.Name)))
            {
                ReportMapping(DiagnosticDescriptors.UnusedSourceProperty, null, property.Name);
            }
        }

        return assignments.ToImmutable();
    }

    private void ParseForMembers(
        AttributeFacts facts,
        MemberInventory sourceMembers,
        MemberInventory destinationMembers,
        Dictionary<string, string> overrides,
        HashSet<string> settled,
        HashSet<string> used)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var attribute in facts.ForMembers)
        {
            var location = LocationInfo.From(AttributeFacts.GetLocation(attribute));
            var arguments = attribute.ConstructorArguments;

            if (arguments.Length != 2 ||
                arguments[0].Value is not string destinationName ||
                arguments[1].Value is not string sourceName ||
                destinationName.Length == 0 ||
                sourceName.Length == 0)
            {
                ReportMapping(
                    DiagnosticDescriptors.InvalidForMember,
                    location,
                    "both arguments must be non-empty string constants, for example [ForMember(nameof(Dto.Name), nameof(Entity.Title))]");
                continue;
            }

            if (!seen.Add(destinationName))
            {
                ReportMapping(DiagnosticDescriptors.DuplicateOverride, location, destinationName);
                settled.Add(destinationName);
                continue;
            }

            string? problem = null;
            if (destinationName.Contains('.') || sourceName.Contains('.'))
            {
                problem = "dotted property paths are not supported in version one";
            }
            else if (!destinationMembers.Contains(destinationName))
            {
                problem = $"'{destinationName}' is not a public writable instance property of {_destinationDisplay}" +
                          Suggest(destinationName, destinationMembers);
            }
            else if (!sourceMembers.Contains(sourceName))
            {
                problem = $"'{sourceName}' is not a public readable instance property of {_sourceDisplay}" +
                          Suggest(sourceName, sourceMembers);
            }

            if (problem is not null)
            {
                ReportMapping(DiagnosticDescriptors.InvalidForMember, location, problem);

                // An invalid override never falls back to convention, and must not cascade into completeness errors.
                settled.Add(destinationName);
                if (sourceMembers.Contains(sourceName))
                {
                    used.Add(sourceName);
                }

                continue;
            }

            overrides[destinationName] = sourceName;
        }
    }

    private static string Suggest(string name, MemberInventory members)
    {
        var match = members.Properties.FirstOrDefault(p => string.Equals(p.Name, name, StringComparison.OrdinalIgnoreCase));
        return match is null ? string.Empty : $"; property names are case-sensitive, did you mean '{match.Name}'?";
    }

    /// <summary>Validates construction and returns whether the constructor satisfies required members.</summary>
    private bool CheckConstruction(INamedTypeSymbol destination)
    {
        var caller = _method.ContainingType;

        if (destination.IsAbstract)
        {
            ReportMapping(DiagnosticDescriptors.InvalidConstruction, null, $"'{_destinationDisplay}' is abstract and cannot be instantiated");
            return false;
        }

        var constructors = destination.InstanceConstructors;
        IMethodSymbol? parameterless = constructors.FirstOrDefault(c => c.Parameters.Length == 0);

        if (destination.IsValueType)
        {
            if (parameterless is { IsImplicitlyDeclared: false } && !_compilation.IsSymbolAccessibleWithin(parameterless, caller))
            {
                ReportMapping(DiagnosticDescriptors.InvalidConstruction, null, $"the parameterless constructor of '{_destinationDisplay}' is not accessible");
            }
        }
        else if (parameterless is null || !_compilation.IsSymbolAccessibleWithin(parameterless, caller))
        {
            var reason = destination.IsRecord
                ? $"'{_destinationDisplay}' is a record without an accessible parameterless constructor; positional-record construction is not supported in version one"
                : $"'{_destinationDisplay}' needs an accessible parameterless constructor; constructor-parameter mapping is not supported in version one";
            ReportMapping(DiagnosticDescriptors.InvalidConstruction, null, reason);
        }

        var setsRequiredMembers = parameterless is not null && parameterless.GetAttributes().Any(a =>
            a.AttributeClass is { Name: "SetsRequiredMembersAttribute" });

        if (!setsRequiredMembers)
        {
            foreach (var name in MemberInventory.ForDestination(destination).UnsatisfiableRequired)
            {
                ReportMapping(
                    DiagnosticDescriptors.InvalidConstruction,
                    null,
                    $"required member '{name}' cannot be set by the generated object initializer; version one supports public properties with set or init accessors");
            }
        }

        return setsRequiredMembers;
    }

    private AssignmentModel? PlanAssignment(IPropertySymbol sourceProperty, IPropertySymbol destinationProperty)
    {
        var sourceType = sourceProperty.Type;
        var destinationType = destinationProperty.Type;
        var sourceScalar = TypeFacts.IsScalar(sourceType);
        var destinationScalar = TypeFacts.IsScalar(destinationType);
        var propertyLabel = $"{destinationProperty.Name}";

        if (sourceScalar && destinationScalar)
        {
            var from = TypeFacts.Unwrap(sourceType);
            var to = TypeFacts.Unwrap(destinationType);
            var conversion = _compilation.ClassifyConversion(from, to);
            var allowed = conversion.Exists && (conversion.IsIdentity || (conversion.IsImplicit && !conversion.IsUserDefined));

            if (!allowed)
            {
                ReportMapping(
                    DiagnosticDescriptors.UnsupportedConversion,
                    null,
                    propertyLabel,
                    $"no implicit scalar conversion from '{sourceType.ToDisplayString(TypeFacts.Friendly)}' to '{destinationType.ToDisplayString(TypeFacts.Friendly)}'; narrowing, parsing, formatting and user-defined conversions are not supported");
                return null;
            }

            if (TypeFacts.IsExplicitlyNullable(sourceType) && TypeFacts.IsExplicitlyNonNull(destinationType))
            {
                ReportUnsafeNullable(sourceProperty, destinationProperty);
                return null;
            }

            return CreateAssignment(sourceProperty, destinationProperty, AssignmentKind.Direct, string.Empty, string.Empty, guard: false);
        }

        if (sourceScalar != destinationScalar)
        {
            ReportMapping(
                DiagnosticDescriptors.UnsupportedConversion,
                null,
                propertyLabel,
                $"cannot map '{sourceType.ToDisplayString(TypeFacts.Friendly)}' to '{destinationType.ToDisplayString(TypeFacts.Friendly)}'; only scalar-to-scalar and registered complex-to-complex mappings are supported");
            return null;
        }

        var unwrappedSource = TypeFacts.Unwrap(sourceType);
        var unwrappedDestination = TypeFacts.Unwrap(destinationType);

        if (TypeFacts.IsCollection(unwrappedSource) || TypeFacts.IsCollection(unwrappedDestination))
        {
            ReportMapping(
                DiagnosticDescriptors.UnsupportedConversion,
                null,
                propertyLabel,
                $"collection mapping ('{sourceType.ToDisplayString(TypeFacts.Friendly)}' to '{destinationType.ToDisplayString(TypeFacts.Friendly)}') is not supported in version one; map the elements in a handwritten method");
            return null;
        }

        if (TypeFacts.IsExplicitlyNullable(sourceType) && TypeFacts.IsExplicitlyNonNull(destinationType))
        {
            ReportUnsafeNullable(sourceProperty, destinationProperty);
            return null;
        }

        var guard = TypeFacts.MayBeNull(sourceType) && TypeFacts.MayBeNull(destinationType);
        return CreateAssignment(
            sourceProperty,
            destinationProperty,
            AssignmentKind.Nested,
            unwrappedSource.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified),
            unwrappedDestination.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified),
            guard);
    }

    private void ReportUnsafeNullable(IPropertySymbol sourceProperty, IPropertySymbol destinationProperty) =>
        ReportMapping(DiagnosticDescriptors.UnsafeNullable, null, sourceProperty.Name, destinationProperty.Name);

    private static AssignmentModel CreateAssignment(
        IPropertySymbol sourceProperty,
        IPropertySymbol destinationProperty,
        AssignmentKind kind,
        string nestedSource,
        string nestedDestination,
        bool guard) =>
        new(
            Identifier.Escape(destinationProperty.Name),
            Identifier.Escape(sourceProperty.Name),
            sourceProperty.Name,
            destinationProperty.Name,
            kind,
            destinationProperty.Type.WithNullableAnnotation(NullableAnnotation.None).ToDisplayString(TypeFacts.Qualified),
            nestedSource,
            nestedDestination,
            guard,
            TypeFacts.IsNullableValueType(sourceProperty.Type));
}
