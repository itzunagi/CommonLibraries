namespace Common.SimpleSourceMapper.Compiler.Models;

internal enum AssignmentKind
{
    /// <summary>Plain assignment relying on an implicit scalar conversion.</summary>
    Direct,

    /// <summary>Call to a nested mapping method resolved from the compilation-wide registry.</summary>
    Nested
}

/// <summary>One destination-property assignment of a generated mapping.</summary>
internal sealed record AssignmentModel(
    string DestinationProperty,
    string SourceProperty,
    string SourcePropertyName,
    string DestinationPropertyName,
    AssignmentKind Kind,
    string DestinationType,
    string NestedSourceKey,
    string NestedDestinationKey,
    bool Guard,
    bool SourceIsNullableValue);

/// <summary>
/// Symbol-free description of one <c>[Map]</c> declaration, produced per method so the incremental
/// pipeline can cache it. Cross-mapping concerns (registry, cycles) are resolved in a later stage.
/// </summary>
internal sealed record MappingModel(
    string Namespace,
    EquatableArray<string> Containers,
    string ContainerFullName,
    bool ExternallyAccessible,
    string MethodName,
    string MethodKey,
    string CallName,
    string ParameterName,
    string SourceType,
    string SourceTypeAnnotated,
    string SourceKey,
    string SourceDisplay,
    bool SourceIsReferenceType,
    string DestinationType,
    string DestinationTypeAnnotated,
    string DestinationKey,
    string DestinationDisplay,
    bool NullableEnabled,
    LocationInfo? Location,
    EquatableArray<AssignmentModel> Assignments,
    EquatableArray<DiagnosticInfo> Diagnostics);
