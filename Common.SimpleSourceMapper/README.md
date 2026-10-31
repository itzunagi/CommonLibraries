# Common.SimpleSourceMapper

Compile-time object mapping for C#. One package gives you:

- attributes to declare mappings (`[Map]`, `[ValidateMapping]`, `[ForMember]`),
- an analyzer that validates handwritten mappings,
- a source generator that implements declared `partial` mapping methods,
- no runtime reflection and no runtime dependencies beyond the small attribute assembly.

## Installation

```xml
<PackageReference Include="Common.SimpleSourceMapper" Version="0.1.0" />
```

The analyzer and generator ship inside the same package (`analyzers/dotnet/cs`); nothing else needs to be referenced.

**Requirements:** .NET SDK 7.0.200 or newer (Roslyn 4.5) and C# 9 or newer (partial methods with accessibility modifiers).
Mappings that use `required` members need C# 11. The attribute assembly targets `netstandard2.0`.

## Generated mappings

```csharp
using Common.SimpleSourceMapper;

public static partial class CustomerMappings
{
    [Map(MemberList.Both)]
    [ForMember(nameof(CustomerDto.DisplayName), nameof(Customer.Name))]
    public static partial CustomerDto ToDto(Customer source);
}
```

generates

```csharp
partial class CustomerMappings
{
    public static partial CustomerDto ToDto(Customer source)
    {
        if (source is null) throw new ArgumentNullException(nameof(source));

        return new CustomerDto
        {
            DisplayName = source.Name,
            Id = source.Id,
        };
    }
}
```

A `[Map]` method must be `public static partial`, declared without a body, take exactly one ordinary parameter and return a
non-void destination. Its containing types must be non-generic `partial` classes (nesting is allowed). Invalid mappings are
reported as errors and never produce a stub.

## Handwritten mappings

`[ValidateMapping]` keeps your implementation and checks it at compile time:

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

Supported shapes: `return new Destination { ... }`, expression-bodied creation, a local that is created, assigned and
returned, multiple returns, conditional branches, throw-only paths, simple source aliases and simple destination aliases.

### Analysis limitations

- **Source validation checks property-read coverage, not semantic contribution to the mapped result.** Reading a source
  property in a logging statement counts as a read.
- `source.Address.City` counts as reading `Address`. `nameof(source.Property)` and write-only access do not count.
  Reads inside lambdas and local functions do not count merely because they are declared.
- Passing the whole source to a helper does not imply that any property was used.
- Destination validation requires the returned object to be created in the method. Returning the result of a helper, passing
  the destination to a helper, `return null`, and `try/catch` are reported as `SSM012` ("cannot be proven") instead of
  being silently accepted. General interprocedural analysis is out of scope.
- Assignments are definite only when made on every normal return path (branch sets are intersected, loops may run zero times).

## `MemberList`

| Value | Meaning |
|---|---|
| `None` | No completeness checks. Signature and mapping-correctness checks still apply. |
| `Source` | Every eligible source property must be used. |
| `Destination` | Every eligible destination property must be assigned. **Default.** |
| `Both` | Source and destination checks. |

Undefined values are an error (`SSM015`).

## `ForMember`

`[ForMember(destination, source)]` — **destination first, source second** — maps a destination property from a differently
named source property. It is valid only together with `[Map]`.

```csharp
[Map(MemberList.Both)]
[ForMember(nameof(CustomerDto.DisplayName), nameof(Customer.Name))]
[ForMember(nameof(CustomerDto.Label), nameof(Customer.Name))] // one source may feed several destinations
public static partial CustomerDto ToDto(Customer source);
```

Both names must be eligible properties, matched case-sensitively; dotted paths are not supported. Two overrides for the same
destination are an error. An invalid override never falls back to name matching.

## Matching rules

For each destination property, an explicit `ForMember` wins; otherwise the source property with the exact, case-sensitive
same name is used.

**Eligible source properties:** public, instance, non-indexer properties with a public getter, declared or inherited.

**Eligible destination properties:** public, instance, non-indexer properties with a public `set` or `init`, declared or
inherited. Read-only destination properties are ignored by completeness checks. Overridden properties count once; hidden
(`new`) properties are ambiguous and reported (`SSM013`). Fields, static properties and explicit interface implementations
are ignored.

## Nested mappings

A complex property (anything that is not a scalar) is mapped by calling another `[Map]` method for that exact
source/destination type pair, found anywhere in the **current compilation** (other files and other mapper classes included,
in any declaration order). Referenced assemblies are not searched, `[ValidateMapping]` methods are not registered, and
nothing is synthesized: a same-type complex property still needs a declared mapper, otherwise you get `SSM008`. Declaring the
same pair twice is `SSM009`. Generated calls use fully qualified names.

Collections are not mapped automatically (`SSM008`). Cyclic mapping graphs are rejected (`SSM014`) because they would need
an identity/context strategy that version one does not have.

## Supported conversions

Scalars are an explicit allowlist: C# numeric primitives (including `nint`/`nuint`, `Half`, `Int128`/`UInt128` where
available), `bool`, `char`, `string`, `decimal`, enums, `Guid`, `DateTime`, `DateTimeOffset`, `TimeSpan`, `DateOnly`,
`TimeOnly` and their nullable forms. A scalar pair is mapped when C# has an identity or implicit, non-user-defined conversion
(for example `int` to `long`, `int` to `int?`, `int?` to `long?`). Narrowing, parsing, formatting and user-defined conversions
(for example `DateTime` to `DateTimeOffset`) are rejected. `object` is not an escape hatch.

## Nullability

| Situation | Behavior |
|---|---|
| Null source reference passed to a mapper | `ArgumentNullException` |
| Non-null scalar to nullable scalar | Allowed |
| Nullable scalar to nullable scalar | Allowed |
| Nullable source property to non-nullable destination | `SSM011` |
| Nullable complex property to nullable complex destination | Null guard (source read once into a local) |
| Non-null complex property to nullable destination | Nested mapper is called |
| Nullable-oblivious declarations | Compiler semantics are preserved with reduced guarantees: oblivious values may be null, so a guard is generated when the destination can accept null, and `SSM011` is only reported when both sides are explicitly annotated |

## Construction and required members

The destination needs an accessible parameterless constructor (or is a struct), and is created with an object initializer, so
`init` accessors work. Abstract destinations, inaccessible constructors and constructor-only models (including positional
records) are rejected with `SSM010`. `required` members must be assigned regardless of `MemberList`, unless the parameterless
constructor is marked `[SetsRequiredMembers]`. `required` fields cannot be satisfied.

## Not supported in version one

Generic mapping methods or generic containing types; `ref`/`in`/`out`, extension, async and iterator mapping methods; fields,
indexers, static and explicit-interface properties; constructor-parameter and positional-record mapping; automatic collection
mapping; flattening/unflattening and dotted paths; runtime reflection or mapper discovery; automatic mapping of undeclared
complex type pairs; object identity preservation; cyclic mapping graphs.

## Diagnostics

All diagnostics are errors.

| ID | Condition |
|---|---|
| `SSM001` | Invalid mapping signature or accessibility (not public/static, wrong parameter count, void, generic, ref/in/out, extension, async, ...) |
| `SSM002` | Invalid partial declaration or containing type (missing `partial`, has a body, generic/struct/file-local container) |
| `SSM003` | Conflicting or misplaced attributes (`[Map]` with `[ValidateMapping]`, `[ForMember]` without `[Map]`, `[ValidateMapping]` without a body) |
| `SSM004` | Unused source property |
| `SSM005` | Unassigned destination property |
| `SSM006` | Invalid `ForMember` property reference |
| `SSM007` | Duplicate destination override |
| `SSM008` | Unsupported conversion, collection, or missing/ambiguous nested mapper |
| `SSM009` | Duplicate registered mapping pair |
| `SSM010` | Invalid construction or unsatisfied required member |
| `SSM011` | Unsafe nullable mapping |
| `SSM012` | Handwritten coverage cannot be proven |
| `SSM013` | Unsupported member or type shape (generic/interface/scalar/collection mapping type, ambiguous hidden property) |
| `SSM014` | Cyclic mapping dependency |
| `SSM015` | Invalid `MemberList` value |

The analyzer reports handwritten coverage and declaration/attribute misuse; the generator reports mapping resolution and
generation failures. When a mapping is rejected the partial method has no implementation, so the compiler also reports
`CS8795`; fix the `SSM` diagnostic first.
