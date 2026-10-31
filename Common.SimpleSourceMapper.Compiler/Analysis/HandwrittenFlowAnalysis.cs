using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Common.SimpleSourceMapper.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Common.SimpleSourceMapper.Compiler.Analysis;

internal sealed class HandwrittenFlowResult
{
    public HandwrittenFlowResult(
        ImmutableArray<string> unreadSourceProperties,
        ImmutableArray<string> unassignedDestinationProperties,
        ImmutableArray<string> unprovableReasons)
    {
        UnreadSourceProperties = unreadSourceProperties;
        UnassignedDestinationProperties = unassignedDestinationProperties;
        UnprovableReasons = unprovableReasons;
    }

    public ImmutableArray<string> UnreadSourceProperties { get; }

    public ImmutableArray<string> UnassignedDestinationProperties { get; }

    /// <summary>Reasons the requested coverage could not be established.</summary>
    public ImmutableArray<string> UnprovableReasons { get; }
}

/// <summary>
/// Control-flow based analysis of a handwritten mapping body. Tracks source-property reads and, per returned
/// object identity, the destination properties that are definitely assigned on every normal return path.
/// </summary>
internal sealed class HandwrittenFlowAnalysis
{
    private readonly IParameterSymbol _source;
    private readonly ITypeSymbol _destinationType;
    private readonly MemberInventory _sourceMembers;
    private readonly MemberInventory _destinationMembers;
    private readonly HashSet<string> _reads = new(StringComparer.Ordinal);
    private readonly Stack<AbstractValue> _initializerStack = new();

    private HandwrittenFlowAnalysis(
        IParameterSymbol source,
        ITypeSymbol destinationType,
        MemberInventory sourceMembers,
        MemberInventory destinationMembers)
    {
        _source = source;
        _destinationType = destinationType;
        _sourceMembers = sourceMembers;
        _destinationMembers = destinationMembers;
    }

    public static HandwrittenFlowResult Analyze(
        IMethodBodyOperation body,
        IParameterSymbol source,
        ITypeSymbol destinationType,
        MemberInventory sourceMembers,
        MemberInventory destinationMembers,
        int memberList)
    {
        var analysis = new HandwrittenFlowAnalysis(source, destinationType, sourceMembers, destinationMembers);
        return analysis.Run(body, memberList);
    }

    private HandwrittenFlowResult Run(IMethodBodyOperation body, int memberList)
    {
        var checkSource = (memberList & MemberListFlags.Source) != 0;
        var checkDestination = (memberList & MemberListFlags.Destination) != 0;
        var reasons = ImmutableArray.CreateBuilder<string>();

        ControlFlowGraph cfg;
        try
        {
            cfg = ControlFlowGraph.Create(body);
        }
        catch (InvalidOperationException)
        {
            return Unprovable("the method body could not be analyzed");
        }

        var unsupported = FindUnsupportedRegion(cfg.Root);
        if (unsupported is not null)
        {
            return Unprovable($"{unsupported} is not supported by the handwritten-mapping analysis");
        }

        var blocks = cfg.Blocks;
        var entryStates = new FlowState?[blocks.Length];
        var initial = new FlowState();
        initial.Vars[_source] = AbstractValue.Source;
        entryStates[0] = initial;

        var work = new Queue<int>();
        work.Enqueue(0);
        var hasErrorFlow = false;

        while (work.Count > 0)
        {
            var ordinal = work.Dequeue();
            var state = entryStates[ordinal]!.Clone();
            var block = blocks[ordinal];

            Transfer(block, state);

            foreach (var branch in new[] { block.ConditionalSuccessor, block.FallThroughSuccessor })
            {
                if (branch?.Destination is not { } destination)
                {
                    continue;
                }

                var target = destination.Ordinal;
                var outgoing = state.Clone();
                RunFinallyRegions(blocks, branch, outgoing);
                if (entryStates[target] is null)
                {
                    entryStates[target] = outgoing;
                    work.Enqueue(target);
                }
                else if (entryStates[target]!.MergeFrom(outgoing))
                {
                    work.Enqueue(target);
                }
            }
        }

        // Second pass: collect the normal return points from the converged states.
        var returns = new List<(AbstractValue Value, FlowState State)>();
        for (var i = 0; i < blocks.Length; i++)
        {
            if (entryStates[i] is not { } entry)
            {
                continue;
            }

            var block = blocks[i];
            var state = entry.Clone();
            var value = Transfer(block, state);

            if (block.ConditionalSuccessor is null && block.FallThroughSuccessor is { } fall)
            {
                if (fall.Semantics == ControlFlowBranchSemantics.Return)
                {
                    RunFinallyRegions(blocks, fall, state);
                    returns.Add((value, state));
                }
                else if (fall.Semantics == ControlFlowBranchSemantics.Error)
                {
                    hasErrorFlow = true;
                }
            }
        }

        if (hasErrorFlow)
        {
            return Unprovable("the method body contains invalid code");
        }

        var unread = checkSource
            ? _sourceMembers.Properties.Select(p => p.Name).Where(n => !_reads.Contains(n)).ToImmutableArray()
            : ImmutableArray<string>.Empty;

        var unassigned = ImmutableArray<string>.Empty;
        if (checkDestination)
        {
            var provable = new List<HashSet<string>>();

            foreach (var (value, state) in returns)
            {
                if (!value.IsObjects)
                {
                    reasons.Add("a returned value is not an object created in the mapping method (helper or opaque call)");
                    continue;
                }

                if (value.Objects.Any(state.Escaped.Contains))
                {
                    reasons.Add("the returned object is passed to code the analysis cannot see into");
                    continue;
                }

                var assigned = new HashSet<string>(state.Assigned[value.Objects[0]], StringComparer.Ordinal);
                foreach (var id in value.Objects.Skip(1))
                {
                    assigned.IntersectWith(state.Assigned[id]);
                }

                provable.Add(assigned);
            }

            if (provable.Count > 0)
            {
                unassigned = _destinationMembers.Properties
                    .Select(p => p.Name)
                    .Where(n => provable.Any(set => !set.Contains(n)))
                    .ToImmutableArray();
            }
        }

        return new HandwrittenFlowResult(unread, unassigned, reasons.Distinct().ToImmutableArray());

        HandwrittenFlowResult Unprovable(string reason) =>
            new(ImmutableArray<string>.Empty, ImmutableArray<string>.Empty, ImmutableArray.Create(reason));
    }

    /// <summary>Applies the finally blocks that run when control leaves their try regions along a branch.</summary>
    private void RunFinallyRegions(ImmutableArray<BasicBlock> blocks, ControlFlowBranch branch, FlowState state)
    {
        foreach (var region in branch.FinallyRegions)
        {
            for (var i = region.FirstBlockOrdinal; i <= region.LastBlockOrdinal; i++)
            {
                Transfer(blocks[i], state);
            }
        }
    }

    private static string? FindUnsupportedRegion(ControlFlowRegion region)
    {
        switch (region.Kind)
        {
            // try/finally (also produced by foreach, using and lock) is supported: finally blocks run on every exit.
            case ControlFlowRegionKind.TryAndCatch:
            case ControlFlowRegionKind.Catch:
            case ControlFlowRegionKind.Filter:
            case ControlFlowRegionKind.FilterAndHandler:
                return "try/catch";
            case ControlFlowRegionKind.ErroneousBody:
                return "an erroneous method body";
        }

        foreach (var nested in region.NestedRegions)
        {
            if (FindUnsupportedRegion(nested) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Evaluates a block and returns the value of its branch value (the return value for return branches).</summary>
    private AbstractValue Transfer(BasicBlock block, FlowState state)
    {
        foreach (var operation in block.Operations)
        {
            Eval(operation, state);
        }

        return block.BranchValue is { } branchValue ? Eval(branchValue, state) : AbstractValue.Other;
    }

    private bool IsDestinationType(ITypeSymbol? type)
    {
        for (var t = type; t is not null; t = t.BaseType)
        {
            if (SymbolEqualityComparer.Default.Equals(t, _destinationType))
            {
                return true;
            }
        }

        return false;
    }

    private static void Escape(AbstractValue value, FlowState state)
    {
        foreach (var id in value.Objects)
        {
            state.Escaped.Add(id);
        }
    }

    private AbstractValue Eval(IOperation? operation, FlowState state)
    {
        switch (operation)
        {
            case null:
                return AbstractValue.Other;

            case IParameterReferenceOperation parameter:
                return state.Vars.TryGetValue(parameter.Parameter, out var parameterValue) ? parameterValue : AbstractValue.Other;

            case ILocalReferenceOperation local:
                return state.Vars.TryGetValue(local.Local, out var localValue) ? localValue : AbstractValue.Other;

            case IFlowCaptureReferenceOperation captureReference:
                return state.Captures.TryGetValue(captureReference.Id, out var captured) ? captured : AbstractValue.Other;

            case IFlowCaptureOperation capture:
            {
                var value = Eval(capture.Value, state);
                state.Captures[capture.Id] = value;
                return value;
            }

            case IInstanceReferenceOperation instance:
                // ImplicitReceiver is the object being initialized by an object/collection initializer.
                return instance.ReferenceKind == InstanceReferenceKind.ImplicitReceiver && _initializerStack.Count > 0
                    ? _initializerStack.Peek()
                    : AbstractValue.Other;

            case IConversionOperation conversion:
            {
                var value = Eval(conversion.Operand, state);
                return conversion.Conversion.IsUserDefined ? AbstractValue.Other : value;
            }

            case IParenthesizedOperation parenthesized:
                return Eval(parenthesized.Operand, state);

            case INameOfOperation:
            case ITypeOfOperation:
            case ILiteralOperation:
                return AbstractValue.Other;

            case IAnonymousFunctionOperation:
            case ILocalFunctionOperation:
                MarkCapturedDestinations(operation, state);
                return AbstractValue.Other;

            case IObjectCreationOperation creation:
                return EvalCreation(creation, state);

            case IMemberInitializerOperation memberInitializer:
                Eval(memberInitializer.InitializedMember, state);
                _initializerStack.Push(AbstractValue.Other);
                Eval(memberInitializer.Initializer, state);
                _initializerStack.Pop();
                return AbstractValue.Other;

            case ISimpleAssignmentOperation assignment:
                return EvalAssignment(assignment.Target, assignment.Value, state, compound: false);

            case ICompoundAssignmentOperation compound:
                return EvalAssignment(compound.Target, compound.Value, state, compound: true);

            case IIncrementOrDecrementOperation increment:
                return EvalAssignment(increment.Target, null, state, compound: true);

            case IPropertyReferenceOperation propertyReference:
                return EvalPropertyRead(propertyReference, state);

            case IInvocationOperation invocation:
            {
                Escape(Eval(invocation.Instance, state), state);
                foreach (var argument in invocation.Arguments)
                {
                    Escape(Eval(argument.Value, state), state);
                }

                return AbstractValue.Other;
            }

            case IVariableDeclaratorOperation declarator:
                if (declarator.Initializer is { } initializer)
                {
                    state.Vars[declarator.Symbol] = Eval(initializer.Value, state);
                }

                return AbstractValue.Other;

            case IIsPatternOperation isPattern:
            {
                var value = Eval(isPattern.Value, state);
                if (value.IsSource)
                {
                    CollectPatternReads(isPattern.Pattern);
                }

                return AbstractValue.Other;
            }

            default:
                foreach (var child in operation.ChildOperations)
                {
                    Eval(child, state);
                }

                return AbstractValue.Other;
        }
    }

    private AbstractValue EvalCreation(IObjectCreationOperation creation, FlowState state)
    {
        foreach (var argument in creation.Arguments)
        {
            Escape(Eval(argument.Value, state), state);
        }

        var value = AbstractValue.Other;
        if (IsDestinationType(creation.Type))
        {
            var id = creation.Syntax.SpanStart;
            state.Assigned[id] = new HashSet<string>(StringComparer.Ordinal);
            state.Escaped.Remove(id);
            value = AbstractValue.ForObject(id);
        }

        if (creation.Initializer is { } initializer)
        {
            _initializerStack.Push(value);
            foreach (var element in initializer.Initializers)
            {
                Eval(element, state);
            }

            _initializerStack.Pop();
        }

        return value;
    }

    private AbstractValue EvalAssignment(IOperation target, IOperation? valueOperation, FlowState state, bool compound)
    {
        switch (target)
        {
            case IPropertyReferenceOperation property when !property.Property.IsIndexer:
            {
                var instance = Eval(property.Instance, state);
                if (compound)
                {
                    // Compound assignments read the member before writing it.
                    CountRead(instance, property.Property);
                }

                var value = valueOperation is null ? AbstractValue.Other : Eval(valueOperation, state);
                if (instance.Objects.Length == 1 && state.Assigned.TryGetValue(instance.Objects[0], out var assigned))
                {
                    assigned.Add(property.Property.Name);
                }

                Escape(value, state);
                return value;
            }

            case ILocalReferenceOperation local:
            {
                var value = valueOperation is null ? AbstractValue.Other : Eval(valueOperation, state);
                state.Vars[local.Local] = value;
                return value;
            }

            case IParameterReferenceOperation parameter:
            {
                var value = valueOperation is null ? AbstractValue.Other : Eval(valueOperation, state);
                state.Vars[parameter.Parameter] = value;
                return value;
            }

            default:
            {
                Eval(target, state);
                var value = valueOperation is null ? AbstractValue.Other : Eval(valueOperation, state);
                Escape(value, state);
                return value;
            }
        }
    }

    private AbstractValue EvalPropertyRead(IPropertyReferenceOperation propertyReference, FlowState state)
    {
        var instance = Eval(propertyReference.Instance, state);
        foreach (var argument in propertyReference.Arguments)
        {
            Escape(Eval(argument.Value, state), state);
        }

        CountRead(instance, propertyReference.Property);
        return AbstractValue.Other;
    }

    private void CountRead(AbstractValue instance, IPropertySymbol property)
    {
        if (instance.IsSource && !property.IsStatic && !property.IsIndexer)
        {
            _reads.Add(property.Name);
        }
    }

    private void CollectPatternReads(IPatternOperation pattern)
    {
        if (pattern is IRecursivePatternOperation recursive)
        {
            foreach (var subpattern in recursive.PropertySubpatterns)
            {
                if (subpattern.Member is IPropertyReferenceOperation member)
                {
                    _reads.Add(member.Property.Name);
                }
            }
        }
    }

    /// <summary>
    /// Deferred bodies do not count as source reads, but destination objects they capture can be mutated later.
    /// </summary>
    private static void MarkCapturedDestinations(IOperation function, FlowState state)
    {
        foreach (var descendant in function.Descendants())
        {
            AbstractValue? value = descendant switch
            {
                ILocalReferenceOperation local when state.Vars.TryGetValue(local.Local, out var v) => v,
                IParameterReferenceOperation parameter when state.Vars.TryGetValue(parameter.Parameter, out var v) => v,
                _ => null
            };

            if (value is not null)
            {
                Escape(value, state);
            }
        }
    }
}
