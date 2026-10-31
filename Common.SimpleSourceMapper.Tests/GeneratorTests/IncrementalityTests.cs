using Common.SimpleSourceMapper.Compiler.Generation;
using Common.SimpleSourceMapper.Tests.Fixtures;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.GeneratorTests;

public class IncrementalityTests
{
    [Fact]
    public void Unrelated_edits_do_not_regenerate_mappings()
    {
        var compilation = Harness.CreateCompilation(new[]
        {
            Harness.Prelude,
            Harness.InMapper("[Map(MemberList.Both)] public static partial Dst ToDst(Src source);")
        });

        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            new[] { new MappingGenerator().AsSourceGenerator() },
            driverOptions: new GeneratorDriverOptions(IncrementalGeneratorOutputKind.None, trackIncrementalGeneratorSteps: true));

        driver = driver.RunGenerators(compilation, TestContext.Current.CancellationToken);

        var edited = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText("namespace Unrelated { public class Other { } }", cancellationToken: TestContext.Current.CancellationToken));
        driver = driver.RunGenerators(edited, TestContext.Current.CancellationToken);

        var run = driver.GetRunResult().Results.Single();
        var reasons = run.TrackedOutputSteps.SelectMany(s => s.Value).SelectMany(s => s.Outputs).Select(o => o.Reason).ToList();
        Assert.NotEmpty(reasons);
        Assert.All(reasons, reason => Assert.True(
            reason is IncrementalStepRunReason.Cached or IncrementalStepRunReason.Unchanged,
            $"Unexpected reason {reason}"));
    }
}
