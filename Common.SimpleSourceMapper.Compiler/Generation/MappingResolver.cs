using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Common.SimpleSourceMapper.Compiler.Diagnostics;
using Common.SimpleSourceMapper.Compiler.Models;

namespace Common.SimpleSourceMapper.Compiler.Generation;

/// <summary>A mapping plus everything that depends on the other mappings in the compilation.</summary>
internal sealed class ResolvedMapping
{
    public ResolvedMapping(MappingModel model)
    {
        Model = model;
    }

    public MappingModel Model { get; }

    public List<DiagnosticInfo> Diagnostics { get; } = new();

    /// <summary>Nested mapper call for each nested assignment, keyed by destination property.</summary>
    public Dictionary<string, MappingModel> Targets { get; } = new(StringComparer.Ordinal);

    public bool HasErrors => Model.Diagnostics.Count > 0 || Diagnostics.Count > 0;
}

/// <summary>
/// Builds the compilation-wide registry of mapping pairs, resolves nested mappers and rejects cycles.
/// Registration is complete before any resolution, so declaration order never matters.
/// </summary>
internal static class MappingResolver
{
    public static List<ResolvedMapping> Resolve(IEnumerable<MappingModel> models)
    {
        var ordered = models
            .OrderBy(m => m.MethodKey, StringComparer.Ordinal)
            .Select(m => new ResolvedMapping(m))
            .ToList();

        var registry = new Dictionary<(string Source, string Destination), List<MappingModel>>();
        foreach (var mapping in ordered)
        {
            var key = (mapping.Model.SourceKey, mapping.Model.DestinationKey);
            if (!registry.TryGetValue(key, out var list))
            {
                registry.Add(key, list = new List<MappingModel>());
            }

            list.Add(mapping.Model);
        }

        foreach (var group in registry.Values.Where(g => g.Count > 1))
        {
            foreach (var member in group)
            {
                var others = string.Join(", ", group.Where(g => g != member).Select(g => g.MethodKey));
                var mapping = ordered.First(m => m.Model == member);
                mapping.Diagnostics.Add(Info(
                    DiagnosticDescriptors.DuplicateMapping,
                    member,
                    member.MethodName,
                    member.SourceDisplay,
                    member.DestinationDisplay,
                    others));
            }
        }

        foreach (var mapping in ordered)
        {
            ResolveNested(mapping, registry);
        }

        DetectCycles(ordered);
        return ordered;
    }

    private static void ResolveNested(
        ResolvedMapping mapping,
        Dictionary<(string Source, string Destination), List<MappingModel>> registry)
    {
        var model = mapping.Model;

        foreach (var assignment in model.Assignments.Where(a => a.Kind == AssignmentKind.Nested))
        {
            var pair = $"'{assignment.NestedSourceKey}' -> '{assignment.NestedDestinationKey}'";

            if (!registry.TryGetValue((assignment.NestedSourceKey, assignment.NestedDestinationKey), out var candidates))
            {
                mapping.Diagnostics.Add(Unsupported(
                    model,
                    assignment,
                    $"no [Map] method is declared for {pair} in this compilation; declare one so the nested mapping can be generated"));
                continue;
            }

            if (candidates.Count > 1)
            {
                mapping.Diagnostics.Add(Unsupported(
                    model,
                    assignment,
                    $"the mapping {pair} is ambiguous because it is declared {candidates.Count} times"));
                continue;
            }

            var target = candidates[0];
            if (!target.ExternallyAccessible && target.ContainerFullName != model.ContainerFullName)
            {
                mapping.Diagnostics.Add(Unsupported(
                    model,
                    assignment,
                    $"the mapping {pair} is declared in '{target.ContainerFullName}', which is not accessible from here"));
                continue;
            }

            mapping.Targets[assignment.DestinationProperty] = target;
        }
    }

    private static void DetectCycles(List<ResolvedMapping> mappings)
    {
        var index = new Dictionary<MappingModel, int>();
        for (var i = 0; i < mappings.Count; i++)
        {
            index[mappings[i].Model] = i;
        }

        var edges = mappings
            .Select(m => m.Targets.Values.Select(t => index[t]).Distinct().OrderBy(i => i).ToArray())
            .ToArray();

        // Tarjan's strongly connected components.
        var indices = new int[mappings.Count];
        var lowLinks = new int[mappings.Count];
        var onStack = new bool[mappings.Count];
        var stack = new Stack<int>();
        var counter = 1;
        var components = new List<List<int>>();

        void Visit(int node)
        {
            indices[node] = lowLinks[node] = counter++;
            stack.Push(node);
            onStack[node] = true;

            foreach (var next in edges[node])
            {
                if (indices[next] == 0)
                {
                    Visit(next);
                    lowLinks[node] = Math.Min(lowLinks[node], lowLinks[next]);
                }
                else if (onStack[next])
                {
                    lowLinks[node] = Math.Min(lowLinks[node], indices[next]);
                }
            }

            if (lowLinks[node] != indices[node])
            {
                return;
            }

            var component = new List<int>();
            int member;
            do
            {
                member = stack.Pop();
                onStack[member] = false;
                component.Add(member);
            }
            while (member != node);

            components.Add(component);
        }

        for (var i = 0; i < mappings.Count; i++)
        {
            if (indices[i] == 0)
            {
                Visit(i);
            }
        }

        foreach (var component in components)
        {
            if (component.Count == 1 && !edges[component[0]].Contains(component[0]))
            {
                continue;
            }

            var names = component.OrderBy(i => i).Select(i => mappings[i].Model.MethodName).ToArray();
            var path = names.Length == 1 ? $"{names[0]} -> {names[0]}" : string.Join(" -> ", names) + " -> " + names[0];

            foreach (var i in component)
            {
                var model = mappings[i].Model;
                mappings[i].Diagnostics.Add(Info(DiagnosticDescriptors.CyclicMapping, model, model.MethodName, path));
            }
        }
    }

    private static DiagnosticInfo Unsupported(MappingModel model, AssignmentModel assignment, string reason) =>
        Info(
            DiagnosticDescriptors.UnsupportedConversion,
            model,
            model.MethodName,
            model.SourceDisplay,
            model.DestinationDisplay,
            assignment.DestinationPropertyName,
            reason);

    private static DiagnosticInfo Info(Microsoft.CodeAnalysis.DiagnosticDescriptor descriptor, MappingModel model, params string[] arguments) =>
        new(descriptor.Id, model.Location, arguments.ToImmutableArray());
}
