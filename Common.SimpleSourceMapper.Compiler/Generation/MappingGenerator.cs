using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Common.SimpleSourceMapper.Compiler.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Common.SimpleSourceMapper.Compiler.Generation;

/// <summary>Implements the partial methods declared with <c>[Map]</c>.</summary>
[Generator(Microsoft.CodeAnalysis.LanguageNames.CSharp)]
public sealed class MappingGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var models = context.SyntaxProvider
            .ForAttributeWithMetadataName(
                AttributeFacts.MapMetadataName,
                static (node, _) => node is MethodDeclarationSyntax,
                static (ctx, ct) => ctx.TargetSymbol is IMethodSymbol method
                    ? MappingPlanner.Plan(method, ctx.SemanticModel, ct)
                    : null)
            .Where(static model => model is not null)
            .Select(static (model, _) => model!)
            .Collect();

        context.RegisterSourceOutput(models, static (spc, all) => Execute(spc, all));
    }

    private static void Execute(SourceProductionContext context, ImmutableArray<MappingModel> models)
    {
        var usedHints = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var mapping in MappingResolver.Resolve(models))
        {
            foreach (var diagnostic in mapping.Model.Diagnostics.Concat(mapping.Diagnostics))
            {
                context.ReportDiagnostic(diagnostic.ToDiagnostic());
            }

            if (mapping.HasErrors)
            {
                continue;
            }

            var hint = MappingEmitter.HintName(mapping.Model);
            for (var n = 1; !usedHints.Add(hint); n++)
            {
                hint = MappingEmitter.HintName(mapping.Model).Replace(".g.cs", $"_{n}.g.cs");
            }

            context.AddSource(hint, SourceText.From(MappingEmitter.Emit(mapping), Encoding.UTF8));
        }
    }
}
