using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;

namespace Common.SimpleSourceMapper.Compiler.Models;

/// <summary>
/// The one member inventory shared by the analyzer and the generator.
/// Contains the eligible properties of a type, one effective property per name.
/// </summary>
internal sealed class MemberInventory
{
    private readonly Dictionary<string, IPropertySymbol> _byName;

    private MemberInventory(
        ImmutableArray<IPropertySymbol> properties,
        ImmutableArray<string> ambiguousNames,
        ImmutableArray<string> unsatisfiableRequired)
    {
        Properties = properties;
        AmbiguousNames = ambiguousNames;
        UnsatisfiableRequired = unsatisfiableRequired;
        _byName = properties.ToDictionary(p => p.Name, StringComparer.Ordinal);
    }

    /// <summary>Eligible properties ordered by name.</summary>
    public ImmutableArray<IPropertySymbol> Properties { get; }

    /// <summary>Names declared more than once through hiding (or hidden by a field).</summary>
    public ImmutableArray<string> AmbiguousNames { get; }

    /// <summary>Required members of the destination that cannot be set by an object initializer.</summary>
    public ImmutableArray<string> UnsatisfiableRequired { get; }

    public IPropertySymbol? Find(string name) => _byName.TryGetValue(name, out var p) ? p : null;

    public bool Contains(string name) => _byName.ContainsKey(name);

    public static MemberInventory ForSource(ITypeSymbol type) => Build(type, destination: false);

    public static MemberInventory ForDestination(ITypeSymbol type) => Build(type, destination: true);

    public static IMethodSymbol? EffectiveGetter(IPropertySymbol property)
    {
        for (var p = property; p is not null; p = p.OverriddenProperty)
        {
            if (p.GetMethod is { } getter)
            {
                return getter;
            }
        }

        return null;
    }

    public static IMethodSymbol? EffectiveSetter(IPropertySymbol property)
    {
        for (var p = property; p is not null; p = p.OverriddenProperty)
        {
            if (p.SetMethod is { } setter)
            {
                return setter;
            }
        }

        return null;
    }

    public static bool IsRequired(IPropertySymbol property)
    {
        for (var p = property; p is not null; p = p.OverriddenProperty)
        {
            if (p.IsRequired)
            {
                return true;
            }
        }

        return false;
    }

    private static MemberInventory Build(ITypeSymbol type, bool destination)
    {
        var covered = new HashSet<IPropertySymbol>(SymbolEqualityComparer.Default);
        var groups = new Dictionary<string, List<IPropertySymbol>>(StringComparer.Ordinal);
        var fieldNames = new HashSet<string>(StringComparer.Ordinal);
        var requiredFields = new List<string>();

        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            foreach (var member in t.GetMembers())
            {
                if (member is IFieldSymbol { IsStatic: false, DeclaredAccessibility: Accessibility.Public, IsImplicitlyDeclared: false } field)
                {
                    fieldNames.Add(field.Name);
                    if (field.IsRequired)
                    {
                        requiredFields.Add(field.Name);
                    }

                    continue;
                }

                if (member is not IPropertySymbol property ||
                    property.IsStatic ||
                    property.IsIndexer ||
                    property.ExplicitInterfaceImplementations.Length > 0)
                {
                    continue;
                }

                if (covered.Contains(property))
                {
                    if (property.OverriddenProperty is { } next)
                    {
                        covered.Add(next);
                    }

                    continue;
                }

                if (property.OverriddenProperty is { } overridden)
                {
                    covered.Add(overridden);
                }

                if (property.DeclaredAccessibility != Accessibility.Public)
                {
                    continue;
                }

                if (!groups.TryGetValue(property.Name, out var list))
                {
                    groups.Add(property.Name, list = new List<IPropertySymbol>());
                }

                list.Add(property);
            }
        }

        var eligible = new List<IPropertySymbol>();
        var ambiguous = new List<string>();
        var unsatisfiable = new List<string>(requiredFields);

        foreach (var pair in groups.OrderBy(g => g.Key, StringComparer.Ordinal))
        {
            // The first entry is the most derived declaration, which is what name lookup binds to.
            var effective = pair.Value[0];

            if (pair.Value.Count > 1 || fieldNames.Contains(pair.Key))
            {
                ambiguous.Add(pair.Key);
            }

            var accessor = destination ? EffectiveSetter(effective) : EffectiveGetter(effective);
            var isEligible = accessor is { DeclaredAccessibility: Accessibility.Public };

            if (isEligible)
            {
                eligible.Add(effective);
            }
            else if (destination && IsRequired(effective))
            {
                unsatisfiable.Add(effective.Name);
            }
        }

        return new MemberInventory(
            eligible.ToImmutableArray(),
            ambiguous.ToImmutableArray(),
            unsatisfiable.Distinct(StringComparer.Ordinal).OrderBy(n => n, StringComparer.Ordinal).ToImmutableArray());
    }
}
