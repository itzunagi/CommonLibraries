using Microsoft.CodeAnalysis;

namespace Common.SimpleSourceMapper.Compiler.Models;

/// <summary>Type classification shared by the analyzer and the generator.</summary>
internal static class TypeFacts
{
    public static readonly SymbolDisplayFormat Qualified = SymbolDisplayFormat.FullyQualifiedFormat;

    public static readonly SymbolDisplayFormat QualifiedAnnotated = SymbolDisplayFormat.FullyQualifiedFormat
        .AddMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    public static readonly SymbolDisplayFormat Friendly = SymbolDisplayFormat.MinimallyQualifiedFormat;

    public static bool IsNullableValueType(ITypeSymbol type) =>
        type is INamedTypeSymbol { IsValueType: true, OriginalDefinition.SpecialType: SpecialType.System_Nullable_T };

    /// <summary>Strips <c>Nullable&lt;T&gt;</c>; reference-type annotations are ignored by symbol identity.</summary>
    public static ITypeSymbol Unwrap(ITypeSymbol type) =>
        IsNullableValueType(type) ? ((INamedTypeSymbol)type).TypeArguments[0] : type;

    /// <summary>True when a value of the type may be null (explicitly nullable or oblivious reference type).</summary>
    public static bool MayBeNull(ITypeSymbol type) =>
        IsNullableValueType(type) ||
        (!type.IsValueType && type.NullableAnnotation != NullableAnnotation.NotAnnotated);

    /// <summary>True when the type is explicitly declared nullable.</summary>
    public static bool IsExplicitlyNullable(ITypeSymbol type) =>
        IsNullableValueType(type) ||
        (!type.IsValueType && type.NullableAnnotation == NullableAnnotation.Annotated);

    /// <summary>True when the type is explicitly declared non-nullable.</summary>
    public static bool IsExplicitlyNonNull(ITypeSymbol type) =>
        type.IsValueType
            ? !IsNullableValueType(type)
            : type.NullableAnnotation == NullableAnnotation.NotAnnotated;

    public static bool IsScalar(ITypeSymbol type)
    {
        var t = Unwrap(type);

        switch (t.SpecialType)
        {
            case SpecialType.System_Boolean:
            case SpecialType.System_Char:
            case SpecialType.System_SByte:
            case SpecialType.System_Byte:
            case SpecialType.System_Int16:
            case SpecialType.System_UInt16:
            case SpecialType.System_Int32:
            case SpecialType.System_UInt32:
            case SpecialType.System_Int64:
            case SpecialType.System_UInt64:
            case SpecialType.System_IntPtr:
            case SpecialType.System_UIntPtr:
            case SpecialType.System_Single:
            case SpecialType.System_Double:
            case SpecialType.System_Decimal:
            case SpecialType.System_String:
            case SpecialType.System_DateTime:
                return true;
        }

        if (t.TypeKind == TypeKind.Enum)
        {
            return true;
        }

        if (t is { TypeKind: TypeKind.Struct, ContainingNamespace: { Name: "System", ContainingNamespace.IsGlobalNamespace: true } })
        {
            switch (t.Name)
            {
                case "Guid":
                case "DateTimeOffset":
                case "TimeSpan":
                case "DateOnly":
                case "TimeOnly":
                case "Half":
                case "Int128":
                case "UInt128":
                    return true;
            }
        }

        return false;
    }

    public static bool IsCollection(ITypeSymbol type)
    {
        var t = Unwrap(type);
        if (t.TypeKind == TypeKind.Array || t.SpecialType == SpecialType.System_String)
        {
            return t.TypeKind == TypeKind.Array;
        }

        foreach (var iface in t.AllInterfaces)
        {
            if (iface.SpecialType == SpecialType.System_Collections_IEnumerable)
            {
                return true;
            }
        }

        return t.SpecialType == SpecialType.System_Collections_IEnumerable;
    }

    /// <summary>
    /// Returns a reason when the type cannot be the source or destination of a version-one mapping, otherwise null.
    /// </summary>
    public static string? DescribeUnsupportedMappingType(ITypeSymbol type)
    {
        var display = type.ToDisplayString(Friendly);

        if (type.TypeKind is not (TypeKind.Class or TypeKind.Struct))
        {
            return $"'{display}' must be a concrete class or struct";
        }

        if (type is INamedTypeSymbol { IsGenericType: true })
        {
            return $"'{display}' is generic; only non-generic types are supported";
        }

        if (type.IsRefLikeType)
        {
            return $"'{display}' is a ref struct";
        }

        if (type.SpecialType != SpecialType.None || IsScalar(type))
        {
            return $"'{display}' is a scalar type; mapping methods work on complex types";
        }

        if (IsCollection(type))
        {
            return $"'{display}' is a collection; collection mapping is not supported";
        }

        return null;
    }
}
