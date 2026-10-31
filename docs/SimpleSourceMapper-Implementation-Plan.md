# Common.SimpleSourceMapper Implementation Plan

## 1. Objective

Implement `Common.SimpleSourceMapper`, a C# library distributed as a single NuGet package that provides:

1. Attributes for declaring and validating mapping methods.
2. A compile-time analyzer for validating handwritten mappings.
3. A source generator for implementing declared partial mapping methods.
4. Explicit property-name overrides.
5. Discovery of nested mapping methods within the current compilation.
6. Unit, runtime, and package-consumer tests.
7. Integration with the repository’s existing private-feed publishing workflow.

**Terminology:** Use `MemberList.Source` to validate source-property usage, `MemberList.Destination` to validate destination-property assignments, and `MemberList.Both` to perform both checks.

The original requirement repeated `MemberList.Source` for destination validation; this plan uses `MemberList.Destination` for that case.

## 2. Repository Inspection

Before making changes, inspect:

- Solution and project layout.
- Existing target frameworks and SDK configuration.
- Language version and nullable-reference-type settings.
- `Directory.Build.props` and `Directory.Build.targets`.
- Central package management configuration.
- Existing test framework and test conventions.
- Package metadata and versioning conventions.
- `NuGet.config`.
- GitHub Actions workflows.
- Existing private-feed publishing configuration.
- Existing authentication, secret names, and release triggers.

Follow repository conventions wherever possible.

Do not invent private-feed URLs, credentials, secret names, or release triggers. Discover and reuse the configuration already used by other projects.

## 3. Version-One Scope

### Supported

- Public static mapping methods.
- Exactly one ordinary source parameter.
- A non-void destination return type.
- Concrete, non-generic source and destination types.
- Public instance properties, including inherited properties.
- Exact, case-sensitive property-name matching.
- Explicit property renaming.
- Scalar assignments using supported implicit conversions.
- Nested complex mappings declared with the mapping-generation attribute.
- Nullable values under the policy defined below.
- Destination types with accessible parameterless construction.
- Public destination setters and `init` accessors.

### Not Supported Initially

- Generic mapping methods or generic containing mapper types.
- `ref`, `in`, `out`, extension, asynchronous, or iterator mapping methods.
- Fields, indexers, static properties, or explicit interface implementation properties.
- Constructor-parameter mapping.
- Positional-record construction.
- Automatic collection mapping.
- Flattening, unflattening, or dotted property paths.
- Runtime reflection or runtime mapper discovery.
- Automatic mapping of undeclared complex type pairs.
- Object identity preservation.
- Cyclic generated mapping graphs.

Unsupported cases must produce actionable diagnostics where applicable and be documented.

## 4. Public API

Use the namespace `Common.SimpleSourceMapper`.

### MemberList

```csharp
namespace Common.SimpleSourceMapper;

[Flags]
public enum MemberList
{
    None = 0,
    Source = 1,
    Destination = 2,
    Both = Source | Destination
}
```

Semantics:

- `None`: Disable completeness checks, but retain signature and mapping-correctness checks.
- `Source`: Verify that every eligible source property is used.
- `Destination`: Verify that every eligible destination property is assigned.
- `Both`: Perform source and destination checks.

Unknown flag values must produce a diagnostic.

### ValidateMappingAttribute

```csharp
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = false)]
public sealed class ValidateMappingAttribute : Attribute
{
    public ValidateMappingAttribute(
        MemberList memberList = MemberList.Destination)
    {
        MemberList = memberList;
    }

    public MemberList MemberList { get; }
}
```

Validates a handwritten mapping implementation.

Example:

```csharp
public static class CustomerMappings
{
    [ValidateMapping(MemberList.Both)]
    public static CustomerDto ToDto(Customer source)
    {
        return new CustomerDto
        {
            Id = source.Id,
            DisplayName = source.Name
        };
    }
}
```

### MapAttribute

```csharp
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = false,
    Inherited = false)]
public sealed class MapAttribute : Attribute
{
    public MapAttribute(
        MemberList memberList = MemberList.Destination)
    {
        MemberList = memberList;
    }

    public MemberList MemberList { get; }
}
```

Declares a mapping whose implementation is generated.

Example:

```csharp
public static partial class CustomerMappings
{
    [Map(MemberList.Both)]
    [ForMember(
        nameof(CustomerDto.DisplayName),
        nameof(Customer.Name))]
    public static partial CustomerDto ToDto(Customer source);
}
```

### ForMemberAttribute

```csharp
[AttributeUsage(
    AttributeTargets.Method,
    AllowMultiple = true,
    Inherited = false)]
public sealed class ForMemberAttribute : Attribute
{
    public ForMemberAttribute(string destination, string source)
    {
        Destination = destination;
        Source = source;
    }

    public string Destination { get; }

    public string Source { get; }
}
```

The constructor argument order is destination first, source second.

### Attribute Rules

- `ValidateMapping` requires a handwritten implementation.
- `Map` requires a partial method declaration without a body.
- `ValidateMapping` and `Map` cannot appear on the same method.
- `ForMember` is valid only with `Map`.
- `Map` performs its own member-list validation.
- Attributes may target methods, but the analyzer must enforce public/static/signature requirements.

## 5. Project Structure

Use one public package project, one internal compiler project, and one test project.

```text
src/
  Common.SimpleSourceMapper/
    Common.SimpleSourceMapper.csproj
    MemberList.cs
    ValidateMappingAttribute.cs
    MapAttribute.cs
    ForMemberAttribute.cs

  Common.SimpleSourceMapper.Compiler/
    Common.SimpleSourceMapper.Compiler.csproj
    Analysis/
    Generation/
    Models/
    Diagnostics/

tests/
  Common.SimpleSourceMapper.Tests/
    Common.SimpleSourceMapper.Tests.csproj
    AnalyzerTests/
    GeneratorTests/
    RuntimeTests/
    Fixtures/
    PackagingTests/
```

### Public Package Project

Explicitly include:

```xml
<PropertyGroup>
  <IsPackable>true</IsPackable>
  <PackageId>Common.SimpleSourceMapper</PackageId>
  <Version>0.1.0</Version>
</PropertyGroup>
```

Use `0.1.0` only if consistent with repository versioning policy.

Select a target framework consistent with the repository and intended consumers.

Keep this assembly free of runtime Roslyn dependencies.

### Compiler Project

Implement:

- A Roslyn `DiagnosticAnalyzer`.
- An `IIncrementalGenerator`.
- Shared symbol, member-selection, mapping-model, and diagnostic utilities.

Recommended settings:

- Target `netstandard2.0`, subject to compiler-host compatibility requirements.
- Set `IsPackable` to `false`.
- Reference Roslyn packages using `PrivateAssets="all"`.
- Enable analyzer development rules.
- Identify public attributes by metadata name and semantic symbols rather than loading the attribute assembly at runtime.

### Test Project

Explicitly include:

```xml
<PropertyGroup>
  <IsPackable>false</IsPackable>
  <IsTestProject>true</IsTestProject>
</PropertyGroup>
```

Use the repository’s existing test framework.

## 6. NuGet Package Layout

Produce one public package containing:

```text
lib/<target-framework>/Common.SimpleSourceMapper.dll
analyzers/dotnet/cs/Common.SimpleSourceMapper.Compiler.dll
```

Requirements:

- A single package reference enables attributes, analysis, and generation.
- The compiler assembly must not be exposed as a runtime library.
- Roslyn assemblies must not become runtime dependencies.
- Packing must build and include the compiler assembly reliably.
- Verify the actual package using an isolated consumer project.

## 7. Shared Member Selection

Implement one member inventory shared by the analyzer and generator.

### Eligible Source Properties

Include properties that are:

- Public instance properties.
- Non-indexers.
- Readable through a public getter.
- Declared on the source type or inherited.

### Eligible Destination Properties

Include properties that are:

- Public instance properties.
- Non-indexers.
- Writable through a public setter or public `init` accessor.
- Declared on the destination type or inherited.

Read-only destination properties are excluded from destination completeness checking.

For version one, “every destination member” means every eligible writable property, not fields or computed properties.

### Inheritance

- Treat an overridden property as one effective property.
- Diagnose ambiguous hidden properties.
- Use Roslyn symbols and semantic accessibility checks.
- Do not rely only on property-name strings.

## 8. Handwritten Mapping Analyzer

### Source Validation

For `MemberList.Source`, verify that every eligible source property is read within the supported mapping body.

This establishes property-read coverage, not proof that each read contributes to the returned object.

For example, reading a source property in a logging statement counts as a read under this contract.

### Destination Validation

For `MemberList.Destination`, verify that every eligible destination property is definitely assigned on every normal return path to the returned destination object.

Assignments to unrelated destination instances do not count.

### Supported Method Shapes

Support:

```csharp
return new Destination
{
    Id = source.Id
};
```

```csharp
var destination = new Destination();
destination.Id = source.Id;
return destination;
```

Also support:

- Expression-bodied object creation.
- Multiple return statements.
- Conditional branches.
- Simple source aliases.
- Simple destination aliases when identity can be tracked reliably.

### Analysis Implementation

Use Roslyn `IOperation` and control-flow analysis.

At control-flow joins:

- Intersect definitely assigned destination-property sets.
- Do not union assignments from mutually exclusive branches.

Track assignments by returned object identity.

### Source-Read Rules

- Getter reads rooted in the source parameter or tracked aliases count.
- Write-only access does not count.
- `nameof(source.Property)` does not count.
- `source.Address.City` counts as reading the top-level `Address` property.
- Deferred lambda and local-function bodies do not count merely because they are declared.
- Passing the complete source object to an arbitrary helper does not imply all properties were used.
- General interprocedural analysis is outside version one.

### Unsupported Analysis

If coverage cannot be established, report that it cannot be proven.

Example:

```csharp
[ValidateMapping(MemberList.Destination)]
public static Destination Map(Source source)
{
    return ArbitraryHelper(source);
}
```

Do not silently treat unsupported bodies as successfully validated.

Distinguish missing-member diagnostics from unsupported-analysis diagnostics.

## 9. Mapping Registry

Discover valid `Map` methods throughout the current compilation.

Build a registry keyed by the ordered source/destination type pair using Roslyn symbol equality.

Requirements:

- Register declarations before resolving nested mappings.
- Support mappings across different source files and mapper classes.
- Do not depend on syntax traversal order.
- Ignore nullable reference annotations for registry identity.
- Handle null behavior separately.
- Diagnose duplicate registrations for the same ordered pair.
- Do not search referenced assemblies in version one.
- Do not register `ValidateMapping` methods as nested mappers.

Generated calls must use fully qualified containing types and method names.

## 10. Source Generator

### Declaration Requirements

Generated mappings must be:

- `public static partial`.
- Declared without a body or expression body.
- Declared with exactly one ordinary parameter.
- Declared in supported non-generic partial mapper classes.
- Nested only within compatible partial containing declarations.
- Non-file-local.

Support nested, non-generic partial mapper classes.

### Generation Pipeline

For each mapping declaration:

1. Validate the declaration.
2. Build source and destination member inventories.
3. Parse and validate `ForMember` attributes.
4. Resolve destination-property mappings.
5. Select direct assignment or nested mapping.
6. Apply nullability rules.
7. Validate member-list completeness.
8. Validate destination construction and required members.
9. Detect generated mapping cycles.
10. Emit an implementation if the mapping plan is valid.

### Property Matching

For each destination property:

1. Use an explicit `ForMember` override if present.
2. Otherwise match the exact source-property name.
3. Do not fall back to convention after an invalid override.

Additional rules:

- One source property may populate multiple destination properties.
- Multiple overrides targeting the same destination property are invalid.
- Property matching is case-sensitive.
- Dotted property paths are unsupported.

## 11. Scalar Mapping Rules

Maintain an explicit scalar allowlist.

Include:

- C# numeric primitives.
- `bool` and `char`.
- `string`.
- `decimal`.
- Enum types.
- `Guid`.
- `DateTime`.
- `DateTimeOffset`.
- `TimeSpan`.
- `DateOnly` and `TimeOnly` when available.
- Compatible nullable forms.

Do not classify all structs or all types under `System` as scalar.

Do not use `object` as an automatic complex-mapping escape hatch.

Use Roslyn conversion classification:

- Allow identity conversions.
- Allow implicit, non-user-defined scalar conversions.
- Allow valid lifted nullable conversions.
- Reject narrowing conversions.
- Reject parsing and formatting conversions.
- Reject user-defined conversion operators in version one.

Recognize optional framework types by metadata name when available in the consumer compilation.

## 12. Complex Mapping Rules

For complex properties:

- Require a registered mapping for the appropriate source/destination type pair.
- Generate a call to that mapper.
- Do not automatically assign complex references, even when the property types match.
- Do not synthesize undeclared nested mappings.
- Diagnose missing or ambiguous mappings.

For collections:

- Require an explicitly registered supported mapper.
- Otherwise report unsupported mapping.
- Do not generate collection iteration automatically.

### Missing Members

- An unmatched destination property is an error when destination validation is enabled.
- If destination validation is disabled, unmatched properties may remain unassigned unless construction requires them.
- An explicitly configured or convention-matched property with an unsupported conversion is always an error.
- An unused source property is an error when source validation is enabled.
- Required-member construction rules apply regardless of `MemberList`.

## 13. Nullability and Construction

Use the following fixed version-one policy:

| Situation | Behavior |
|---|---|
| Null source reference passed to a mapper | Throw `ArgumentNullException` |
| Non-null scalar mapped to compatible nullable scalar | Allow |
| Nullable scalar mapped to compatible nullable scalar | Allow |
| Nullable source property mapped to non-nullable destination | Diagnostic |
| Nullable complex property mapped to nullable complex destination | Generate a null guard |
| Non-null complex property mapped to nullable destination | Call nested mapper |
| Nullable-oblivious reference declarations | Preserve compiler semantics and document reduced guarantees |

Example:

```csharp
Address = source.Address is null
    ? null
    : global::Example.AddressMappings.ToDto(source.Address)
```

Cache nullable nested source values in locals where necessary to avoid repeated getter evaluation.

### Destination Construction

Require accessible parameterless construction or supported struct default construction.

Generate object initializers:

```csharp
return new global::Example.CustomerDto
{
    Id = source.Id,
    DisplayName = source.Name
};
```

Validate required members.

Allow construction to satisfy required-member rules through an appropriately attributed constructor where applicable.

Reject:

- Abstract destination types.
- Inaccessible construction.
- Unsupported constructor-only models.

Generated code must be compatible with the supported consumer language and target framework.

## 14. Cycle Detection

Build a dependency graph between generated mapping methods.

Reject:

- Direct recursive mapping dependencies.
- Indirect mapping cycles.

Explain in diagnostics and documentation that cyclic graph support requires an identity/context strategy outside version one.

## 15. Generated-Code Requirements

Generated files must:

- Include an auto-generated header.
- Use stable, collision-resistant hint names.
- Use fully qualified type references.
- Escape identifiers correctly.
- Preserve method signature details.
- Reproduce containing partial declarations correctly.
- Use deterministic member ordering.
- Use a consistent nullable directive.
- Avoid runtime reflection and mapper lookup.
- Avoid copying mapping attributes onto implementing declarations.
- Produce identical output for identical relevant inputs.

For invalid mappings:

- Report actionable diagnostics.
- Do not emit an invalid implementation.
- Do not replace the mapping with a silently generated throwing stub.

Tests should prioritize the mapper’s diagnostic when missing partial implementations also cause compiler errors.

## 16. Diagnostics

Create stable diagnostic IDs.

| ID | Condition |
|---|---|
| `SSM001` | Invalid mapping signature or accessibility |
| `SSM002` | Invalid partial declaration or containing type |
| `SSM003` | Conflicting or misplaced attributes |
| `SSM004` | Unused source property |
| `SSM005` | Unassigned destination property |
| `SSM006` | Invalid `ForMember` property reference |
| `SSM007` | Duplicate destination override |
| `SSM008` | Unsupported conversion or missing nested mapper |
| `SSM009` | Duplicate registered mapping pair |
| `SSM010` | Invalid construction or unsatisfied required member |
| `SSM011` | Unsafe nullable mapping |
| `SSM012` | Handwritten coverage cannot be proven |
| `SSM013` | Unsupported member or type shape |
| `SSM014` | Cyclic mapping dependency |
| `SSM015` | Invalid `MemberList` value |

Use error severity by default when the requested mapping contract cannot be satisfied.

Messages should identify:

- The mapping method.
- The affected property.
- Source and destination types.
- A likely correction.

### Diagnostic Ownership

Avoid duplicate reporting:

- Analyzer: handwritten coverage and declaration/attribute misuse.
- Generator: mapping resolution and generation failures.
- Shared utilities: common validation logic called by the owning component.

## 17. Tests

### Attribute and Signature Tests

Cover:

- Valid attribute usage.
- Instance and non-public methods.
- Incorrect parameter counts.
- Void and unsupported return types.
- Generic methods.
- `ref`, `in`, and `out`.
- Missing partial modifiers.
- Generated declarations with bodies.
- Conflicting attributes.
- Invalid `MemberList` values.
- Misplaced `ForMember`.

### Handwritten Analyzer Tests

Cover:

- Successful source, destination, and both validation.
- Missing source reads.
- Missing destination assignments.
- Expression-bodied object initializers.
- Local-variable construction.
- Multiple return paths.
- Assignment on only one branch.
- Assignment on every branch.
- Assignment to a non-returned object.
- Inherited properties and overrides.
- Supported aliases.
- `nameof` and write-only accesses.
- Deferred lambda reads.
- Opaque helper calls.
- Unsupported control flow.
- Throw-only paths.

### Generator Tests

Cover:

- Every supported scalar category.
- Allowed widening conversions.
- Rejected narrowing conversions.
- Exact-name matching.
- Renamed properties.
- Source-property reuse.
- Invalid and duplicate overrides.
- Nested mappings across files and classes.
- Declaration-order independence.
- Missing and duplicate nested mappings.
- Same-type complex properties.
- Nullable scalar and complex mappings.
- `init` properties.
- Required members.
- Inaccessible constructors.
- Unsupported collections and record construction.
- Direct and indirect cycles.
- Escaped identifiers.
- Nested mapper classes.
- Deterministic generation.
- Hint-name collisions.

For successful generation tests, assert:

1. Expected generated source.
2. No generator error diagnostics.
3. No errors in the updated compilation.

### Runtime Tests

Execute compiled mappings and verify:

- Scalar values.
- Renamed properties.
- Nested results.
- Null behavior.
- Nested mapper invocation.
- No accidental complex-reference sharing when a nested mapper should construct a new object.

### Package-Consumer Tests

Pack the public project to a temporary local feed.

Build an isolated consumer using only:

```xml
<PackageReference
    Include="Common.SimpleSourceMapper"
    Version="<test-version>" />
```

Verify:

- Attributes resolve.
- Analyzer diagnostics appear.
- Generation runs.
- Valid consumers compile and execute.
- Invalid consumers fail with expected diagnostics.
- Compiler and Roslyn assemblies do not become runtime dependencies.
- The test project is not packaged.

## 18. GitHub Actions

Extend the existing workflow rather than creating a separate publishing mechanism.

Tasks:

1. Register projects in the solution and build configuration.
2. Include the tests in pull-request validation.
3. Add the public package project to existing packaging steps or matrices.
4. Reuse existing private-feed configuration and authentication.
5. Use the same trusted publishing triggers as sibling packages.
6. Run tests before publication.
7. Preserve existing package publications.
8. Upload package artifacts if existing workflows do so.
9. Verify package-selection globs include the new package.
10. Prevent untrusted pull requests from accessing publishing credentials.

Do not hard-code credentials or modify secret values.

## 19. Documentation

Add a README covering:

- Installation.
- Handwritten validation examples.
- Generated mapping examples.
- `MemberList` semantics.
- `ForMember` argument order and examples.
- Nested mapping discovery.
- Eligible source and destination properties.
- Nullability policy.
- Required-member behavior.
- Supported conversions.
- Unsupported version-one features.
- Handwritten-analysis limitations.
- Diagnostic descriptions.
- Supported consumer SDK and language requirements.

Explicitly document that source validation checks property-read coverage, not semantic contribution to the mapped result.

## 20. Implementation Sequence

Implement in independently testable stages:

1. Inspect repository conventions and record compatibility decisions.
2. Create project structure, attributes, and enum.
3. Add shared symbol/member inventory and diagnostics.
4. Implement handwritten mapping validation.
5. Implement scalar mapping generation.
6. Add `ForMember` and completeness validation.
7. Add compilation-wide registry and nested mappings.
8. Add nullability, construction, required-member, and cycle handling.
9. Add runtime and package-consumer tests.
10. Add documentation and GitHub Actions integration.

Do not mark stages complete using source snapshots alone.

Generated output must compile, and executable mappings must produce the expected results.

## 21. Definition of Done

- [ ] One public NuGet package contains the attributes, analyzer, and generator.
- [ ] Handwritten mappings receive compile-time completeness validation.
- [ ] Supported partial declarations receive generated implementations.
- [ ] `ForMember` supports explicit property renaming.
- [ ] Complex mappings resolve within the current compilation.
- [ ] Unsupported and ambiguous mappings produce actionable diagnostics.
- [ ] Generated mappings follow documented nullability and construction rules.
- [ ] Main project explicitly sets `IsPackable` to `true`.
- [ ] Main project includes a `Version` property.
- [ ] Test project explicitly sets `IsPackable` to `false`.
- [ ] Analyzer, generator, runtime, and package-consumer tests pass.
- [ ] README documents supported behavior and limitations.
- [ ] Existing GitHub Actions publish the package to the same private feed as sibling packages.
- [ ] Existing project builds, tests, and publishing behavior remain intact.
