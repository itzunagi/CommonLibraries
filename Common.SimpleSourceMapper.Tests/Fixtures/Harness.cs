using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using Common.SimpleSourceMapper.Compiler.Analysis;
using Common.SimpleSourceMapper.Compiler.Generation;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Common.SimpleSourceMapper.Tests.Fixtures;

/// <summary>Result of running the generator and the analyzer over a set of sources.</summary>
public sealed class RunResult
{
    public required Compilation Compilation { get; init; }

    public required ImmutableArray<Diagnostic> GeneratorDiagnostics { get; init; }

    public required ImmutableArray<Diagnostic> AnalyzerDiagnostics { get; init; }

    public required ImmutableArray<(string HintName, string Text)> Generated { get; init; }

    /// <summary>Mapper diagnostics (SSM*) from both components, sorted and distinct.</summary>
    public IReadOnlyList<string> Ids => GeneratorDiagnostics.Concat(AnalyzerDiagnostics)
        .Where(d => d.Id.StartsWith("SSM", StringComparison.Ordinal))
        .Select(d => d.Id)
        .OrderBy(i => i, StringComparer.Ordinal)
        .ToList();

    public IReadOnlyList<string> DistinctIds => Ids.Distinct().ToList();

    public IReadOnlyList<Diagnostic> All => GeneratorDiagnostics.Concat(AnalyzerDiagnostics)
        .Where(d => d.Id.StartsWith("SSM", StringComparison.Ordinal)).ToList();

    public string GeneratedText => string.Join("\n", Generated.Select(g => g.Text));

    public IReadOnlyList<Diagnostic> CompilationErrors => Compilation.GetDiagnostics()
        .Where(d => d.Severity == DiagnosticSeverity.Error).ToList();

    public string Describe() =>
        string.Join("\n", All.Select(d => d.ToString())) + "\n--\n" +
        string.Join("\n", CompilationErrors.Select(d => d.ToString())) + "\n--\n" + GeneratedText;
}

public static class Harness
{
    private static readonly ImmutableArray<MetadataReference> References = BuildReferences();

    /// <summary>Common domain types used by most tests.</summary>
    public const string Prelude = """
        namespace Domain
        {
            public class Customer { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class CustomerDto { public int Id { get; set; } public string DisplayName { get; set; } = ""; }
            public class Src { public int Id { get; set; } public string Name { get; set; } = ""; }
            public class Dst { public int Id { get; set; } public string Name { get; set; } = ""; }
        }
        """;

    /// <summary>Wraps members in a public static partial mapper class with the usual usings.</summary>
    public static string InMapper(string members, string types = "", string className = "M") => $$"""
        using System;
        using Common.SimpleSourceMapper;
        using Domain;
        namespace T
        {
            {{types}}
            public static partial class {{className}}
            {
                {{members}}
            }
        }
        """;

    private static ImmutableArray<MetadataReference> BuildReferences()
    {
        var runtimeDirectory = Path.GetDirectoryName(typeof(object).Assembly.Location)!;
        var references = Directory.GetFiles(runtimeDirectory, "*.dll")
            .Where(IsManaged)
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path))
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(MapAttribute).Assembly.Location));
        return references.ToImmutableArray();
    }

    private static bool IsManaged(string path)
    {
        try
        {
            AssemblyName.GetAssemblyName(path);
            return true;
        }
        catch (BadImageFormatException)
        {
            return false;
        }
    }

    public static CSharpCompilation CreateCompilation(IEnumerable<string> sources, OutputKind kind = OutputKind.DynamicallyLinkedLibrary, string? name = null)
    {
        var options = new CSharpParseOptions(LanguageVersion.Latest);
        var trees = sources.Select((s, i) => CSharpSyntaxTree.ParseText(s, options, path: $"/test/File{i}.cs"));
        return CSharpCompilation.Create(
            name ?? "TestAssembly" + Guid.NewGuid().ToString("N"),
            trees,
            References,
            new CSharpCompilationOptions(kind, nullableContextOptions: NullableContextOptions.Enable));
    }

    public static RunResult Run(params string[] sources) => Run((IEnumerable<string>)sources);

    public static RunResult Run(IEnumerable<string> sources, bool includePrelude = true)
    {
        var all = includePrelude ? sources.Prepend(Prelude) : sources;
        var compilation = CreateCompilation(all);

        var driver = CSharpGeneratorDriver.Create(new MappingGenerator())
            .RunGeneratorsAndUpdateCompilation(compilation, out var updated, out var generatorDiagnostics);
        var generated = driver.GetRunResult().Results.SelectMany(r => r.GeneratedSources)
            .Select(s => (s.HintName, s.SourceText.ToString()))
            .ToImmutableArray();

        var analyzerDiagnostics = updated
            .WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new MappingAnalyzer()))
            .GetAnalyzerDiagnosticsAsync().GetAwaiter().GetResult();

        return new RunResult
        {
            Compilation = updated,
            GeneratorDiagnostics = generatorDiagnostics,
            AnalyzerDiagnostics = analyzerDiagnostics,
            Generated = generated
        };
    }

    /// <summary>Compiles the sources with the generator, emits the assembly and loads it for execution.</summary>
    public static LoadedAssembly Load(params string[] sources)
    {
        var result = Run(sources);
        if (result.CompilationErrors.Count > 0 || result.GeneratorDiagnostics.Any(d => d.Severity == DiagnosticSeverity.Error))
        {
            throw new InvalidOperationException("Compilation failed:\n" + result.Describe());
        }

        using var stream = new MemoryStream();
        var emit = result.Compilation.Emit(stream);
        if (!emit.Success)
        {
            throw new InvalidOperationException(string.Join("\n", emit.Diagnostics));
        }

        stream.Position = 0;
        var context = new AssemblyLoadContext("test", isCollectible: true);
        context.Resolving += (_, name) => name.Name == typeof(MapAttribute).Assembly.GetName().Name
            ? typeof(MapAttribute).Assembly
            : null;
        return new LoadedAssembly(context.LoadFromStream(stream));
    }
}

public sealed class LoadedAssembly
{
    public LoadedAssembly(Assembly assembly)
    {
        Assembly = assembly;
    }

    public Assembly Assembly { get; }

    public object New(string typeName, params (string Property, object? Value)[] values)
    {
        var type = Assembly.GetType(typeName) ?? throw new InvalidOperationException($"Type {typeName} not found");
        var instance = Activator.CreateInstance(type)!;
        foreach (var (property, value) in values)
        {
            type.GetProperty(property)!.SetValue(instance, value);
        }

        return instance;
    }

    /// <summary>Invokes a static mapper; overloads are selected by the runtime argument type or <paramref name="parameterType"/>.</summary>
    public object? Call(string typeName, string method, object? argument, string? parameterType = null)
    {
        var type = Assembly.GetType(typeName) ?? throw new InvalidOperationException($"Type {typeName} not found");
        var wanted = parameterType ?? argument?.GetType().FullName;
        var target = type.GetMethods().Single(m => m.Name == method && (wanted is null || m.GetParameters()[0].ParameterType.FullName == wanted));
        try
        {
            return target.Invoke(null, new[] { argument });
        }
        catch (TargetInvocationException ex)
        {
            throw ex.InnerException!;
        }
    }

    public static object? Get(object instance, string property) => instance.GetType().GetProperty(property)!.GetValue(instance);
}
