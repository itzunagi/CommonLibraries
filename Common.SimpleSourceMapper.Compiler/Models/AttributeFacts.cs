using System.Collections.Immutable;
using Microsoft.CodeAnalysis;

namespace Common.SimpleSourceMapper.Compiler.Models;

/// <summary>
/// Locates the public attributes by metadata name so the compiler assembly never loads the attribute assembly.
/// </summary>
internal sealed class AttributeFacts
{
    public const string Namespace = "Common.SimpleSourceMapper";
    public const string MapMetadataName = Namespace + ".MapAttribute";
    public const string ValidateMetadataName = Namespace + ".ValidateMappingAttribute";
    public const string ForMemberMetadataName = Namespace + ".ForMemberAttribute";

    /// <summary>Default member list when the attribute argument is omitted.</summary>
    public const int DefaultMemberList = MemberListFlags.Destination;

    private AttributeFacts(
        AttributeData? map,
        AttributeData? validate,
        ImmutableArray<AttributeData> forMembers)
    {
        Map = map;
        Validate = validate;
        ForMembers = forMembers;
    }

    public AttributeData? Map { get; }

    public AttributeData? Validate { get; }

    public ImmutableArray<AttributeData> ForMembers { get; }

    public bool HasAny => Map is not null || Validate is not null || !ForMembers.IsEmpty;

    public static AttributeFacts Read(ISymbol method)
    {
        AttributeData? map = null;
        AttributeData? validate = null;
        var forMembers = ImmutableArray.CreateBuilder<AttributeData>();

        foreach (var attribute in method.GetAttributes())
        {
            var cls = attribute.AttributeClass;
            if (cls is null || cls.ContainingNamespace is not { } ns || ns.ToDisplayString() != Namespace)
            {
                continue;
            }

            switch (cls.Name)
            {
                case "MapAttribute":
                    map ??= attribute;
                    break;
                case "ValidateMappingAttribute":
                    validate ??= attribute;
                    break;
                case "ForMemberAttribute":
                    forMembers.Add(attribute);
                    break;
            }
        }

        return new AttributeFacts(map, validate, forMembers.ToImmutable());
    }

    public static int ReadMemberList(AttributeData attribute)
    {
        if (attribute.ConstructorArguments.Length > 0 && attribute.ConstructorArguments[0].Value is { } value)
        {
            return System.Convert.ToInt32(value);
        }

        return DefaultMemberList;
    }

    public static bool IsValidMemberList(int value) => value >= 0 && (value & ~MemberListFlags.Both) == 0;

    public static Location? GetLocation(AttributeData attribute) =>
        attribute.ApplicationSyntaxReference?.GetSyntax().GetLocation();
}

/// <summary>Mirror of the public <c>MemberList</c> enum used inside the compiler assembly.</summary>
internal static class MemberListFlags
{
    public const int None = 0;
    public const int Source = 1;
    public const int Destination = 2;
    public const int Both = 3;
}
