using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;

namespace Common.SimpleSourceMapper.Compiler.Diagnostics;

/// <summary>Stable diagnostic descriptors shared by the analyzer and the generator.</summary>
internal static class DiagnosticDescriptors
{
    private const string Category = "SimpleSourceMapper";

    private static readonly Dictionary<string, DiagnosticDescriptor> All = new(StringComparer.Ordinal);

    private static DiagnosticDescriptor Create(string id, string title, string message)
    {
        var descriptor = new DiagnosticDescriptor(
            id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
        All.Add(id, descriptor);
        return descriptor;
    }

    public static DiagnosticDescriptor ById(string id) => All[id];

    public static IEnumerable<DiagnosticDescriptor> Descriptors => All.Values;

    public static readonly DiagnosticDescriptor InvalidSignature = Create(
        "SSM001",
        "Invalid mapping signature or accessibility",
        "Mapping method '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor InvalidPartialDeclaration = Create(
        "SSM002",
        "Invalid partial declaration or containing type",
        "Mapping method '{0}' is invalid: {1}");

    public static readonly DiagnosticDescriptor ConflictingAttributes = Create(
        "SSM003",
        "Conflicting or misplaced attributes",
        "Mapping method '{0}' has misplaced attributes: {1}");

    public static readonly DiagnosticDescriptor UnusedSourceProperty = Create(
        "SSM004",
        "Unused source property",
        "Mapping '{0}' ({1} -> {2}) does not use source property '{3}'; map it, or use a MemberList that does not validate the source");

    public static readonly DiagnosticDescriptor UnassignedDestinationProperty = Create(
        "SSM005",
        "Unassigned destination property",
        "Mapping '{0}' ({1} -> {2}) does not assign destination property '{3}'; assign it, add a [ForMember], or use a MemberList that does not validate the destination");

    public static readonly DiagnosticDescriptor InvalidForMember = Create(
        "SSM006",
        "Invalid ForMember property reference",
        "Mapping '{0}' ({1} -> {2}) has an invalid [ForMember]: {3}");

    public static readonly DiagnosticDescriptor DuplicateOverride = Create(
        "SSM007",
        "Duplicate destination override",
        "Mapping '{0}' ({1} -> {2}) has more than one [ForMember] for destination property '{3}'; keep only one");

    public static readonly DiagnosticDescriptor UnsupportedConversion = Create(
        "SSM008",
        "Unsupported conversion or missing nested mapper",
        "Mapping '{0}' ({1} -> {2}) cannot map property '{3}': {4}");

    public static readonly DiagnosticDescriptor DuplicateMapping = Create(
        "SSM009",
        "Duplicate registered mapping pair",
        "Mapping '{0}' registers {1} -> {2}, which is also registered by {3}; keep a single [Map] per source/destination pair");

    public static readonly DiagnosticDescriptor InvalidConstruction = Create(
        "SSM010",
        "Invalid construction or unsatisfied required member",
        "Mapping '{0}' ({1} -> {2}) cannot construct the destination: {3}");

    public static readonly DiagnosticDescriptor UnsafeNullable = Create(
        "SSM011",
        "Unsafe nullable mapping",
        "Mapping '{0}' ({1} -> {2}) maps nullable source property '{3}' to non-nullable destination property '{4}'; make the destination nullable or the source non-nullable");

    public static readonly DiagnosticDescriptor CoverageNotProven = Create(
        "SSM012",
        "Handwritten coverage cannot be proven",
        "Mapping '{0}' ({1} -> {2}) cannot be validated: {3}");

    public static readonly DiagnosticDescriptor UnsupportedShape = Create(
        "SSM013",
        "Unsupported member or type shape",
        "Mapping '{0}' ({1} -> {2}) uses an unsupported shape: {3}");

    public static readonly DiagnosticDescriptor CyclicMapping = Create(
        "SSM014",
        "Cyclic mapping dependency",
        "Mapping '{0}' is part of a cyclic mapping dependency ({1}); cyclic graphs need an identity/context strategy that version one does not provide");

    public static readonly DiagnosticDescriptor InvalidMemberList = Create(
        "SSM015",
        "Invalid MemberList value",
        "Mapping method '{0}' uses the undefined MemberList value {1}; use None, Source, Destination or Both");
}
