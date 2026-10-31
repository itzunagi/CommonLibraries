using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;

namespace Common.SimpleSourceMapper.Compiler.Analysis;

/// <summary>Abstract value of an expression: unknown, the source object, or one of the tracked destination objects.</summary>
internal sealed class AbstractValue : IEquatable<AbstractValue>
{
    public static readonly AbstractValue Other = new(Array.Empty<int>(), isSource: false);
    public static readonly AbstractValue Source = new(Array.Empty<int>(), isSource: true);

    private AbstractValue(int[] objects, bool isSource)
    {
        Objects = objects;
        IsSource = isSource;
    }

    public bool IsSource { get; }

    /// <summary>Creation-site ids of the destination objects this value may refer to.</summary>
    public int[] Objects { get; }

    public bool IsObjects => Objects.Length > 0;

    public static AbstractValue ForObject(int id) => new(new[] { id }, isSource: false);

    public static AbstractValue Join(AbstractValue a, AbstractValue b)
    {
        if (a.Equals(b))
        {
            return a;
        }

        if (a.IsObjects && b.IsObjects)
        {
            return new AbstractValue(a.Objects.Union(b.Objects).OrderBy(i => i).ToArray(), isSource: false);
        }

        return Other;
    }

    public bool Equals(AbstractValue? other) =>
        other is not null && IsSource == other.IsSource && Objects.SequenceEqual(other.Objects);

    public override bool Equals(object? obj) => Equals(obj as AbstractValue);

    public override int GetHashCode() => Objects.Aggregate(IsSource ? 1 : 0, (h, i) => unchecked(h * 31 + i));
}

/// <summary>
/// Dataflow state at a point of the control-flow graph. Assigned sets are definite (intersected at joins);
/// everything else is joined conservatively.
/// </summary>
internal sealed class FlowState
{
    public Dictionary<ISymbol, AbstractValue> Vars { get; private set; } = new(SymbolEqualityComparer.Default);

    public Dictionary<CaptureId, AbstractValue> Captures { get; private set; } = new();

    public Dictionary<int, HashSet<string>> Assigned { get; private set; } = new();

    public HashSet<int> Escaped { get; private set; } = new();

    public FlowState Clone() => new()
    {
        Vars = new Dictionary<ISymbol, AbstractValue>(Vars, SymbolEqualityComparer.Default),
        Captures = new Dictionary<CaptureId, AbstractValue>(Captures),
        Assigned = Assigned.ToDictionary(p => p.Key, p => new HashSet<string>(p.Value, StringComparer.Ordinal)),
        Escaped = new HashSet<int>(Escaped)
    };

    /// <summary>Joins <paramref name="other"/> into this state and reports whether anything changed.</summary>
    public bool MergeFrom(FlowState other)
    {
        var changed = false;

        foreach (var pair in other.Vars)
        {
            if (!Vars.TryGetValue(pair.Key, out var existing))
            {
                Vars[pair.Key] = pair.Value;
                changed = true;
                continue;
            }

            var joined = AbstractValue.Join(existing, pair.Value);
            if (!joined.Equals(existing))
            {
                Vars[pair.Key] = joined;
                changed = true;
            }
        }

        foreach (var pair in other.Captures)
        {
            if (!Captures.TryGetValue(pair.Key, out var existing))
            {
                Captures[pair.Key] = pair.Value;
                changed = true;
                continue;
            }

            var joined = AbstractValue.Join(existing, pair.Value);
            if (!joined.Equals(existing))
            {
                Captures[pair.Key] = joined;
                changed = true;
            }
        }

        foreach (var pair in other.Assigned)
        {
            if (!Assigned.TryGetValue(pair.Key, out var existing))
            {
                Assigned[pair.Key] = new HashSet<string>(pair.Value, StringComparer.Ordinal);
                changed = true;
                continue;
            }

            if (existing.RemoveWhere(name => !pair.Value.Contains(name)) > 0)
            {
                changed = true;
            }
        }

        foreach (var id in other.Escaped)
        {
            changed |= Escaped.Add(id);
        }

        return changed;
    }
}
