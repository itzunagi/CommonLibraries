using System.Diagnostics;
using System.Text;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.PackagingTests;

/// <summary>
/// Packs the public project once into a temporary local feed. Consumers restore from that feed only,
/// using an isolated package cache, so they see exactly what a real consumer of the package would.
/// </summary>
public sealed class PackageFixture : IAsyncLifetime
{
    public const string PackageId = "Common.SimpleSourceMapper";

    private string _root = string.Empty;

    public string RepositoryRoot { get; private set; } = string.Empty;

    public string Version { get; } = "0.0.0-test." + Guid.NewGuid().ToString("N")[..8];

    public string FeedDirectory => Path.Combine(_root, "feed");

    public string PackagePath => Path.Combine(FeedDirectory, $"{PackageId}.{Version}.nupkg");

    public async ValueTask InitializeAsync()
    {
        RepositoryRoot = FindRepositoryRoot();
        _root = Path.Combine(Path.GetTempPath(), "ssm-package-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(FeedDirectory);

        var project = Path.Combine(RepositoryRoot, PackageId, PackageId + ".csproj");
        var result = await RunDotnetAsync(
            RepositoryRoot,
            "pack", project, "-c", "Release", "-o", FeedDirectory, $"-p:Version={Version}", "--nologo");

        Assert.True(result.ExitCode == 0, "dotnet pack failed:\n" + result.Output);
        Assert.True(File.Exists(PackagePath), "Package was not created:\n" + result.Output);
    }

    public ValueTask DisposeAsync()
    {
        try
        {
            if (_root.Length > 0)
            {
                Directory.Delete(_root, recursive: true);
            }
        }
        catch (IOException)
        {
            // Best-effort cleanup of a temp directory.
        }

        return ValueTask.CompletedTask;
    }

    /// <summary>Creates an isolated consumer project that references only the package and builds it.</summary>
    public async Task<ConsumerResult> BuildConsumerAsync(string name, string code)
    {
        var directory = Path.Combine(_root, "consumers", name);
        Directory.CreateDirectory(directory);

        File.WriteAllText(Path.Combine(directory, "nuget.config"), $"""
            <?xml version="1.0" encoding="utf-8"?>
            <configuration>
              <packageSources>
                <clear />
                <add key="local" value="{FeedDirectory}" />
              </packageSources>
              <packageSourceMapping>
                <clear />
                <packageSource key="local">
                  <package pattern="*" />
                </packageSource>
              </packageSourceMapping>
            </configuration>
            """);
        File.WriteAllText(Path.Combine(directory, "Consumer.csproj"), $"""
            <Project Sdk="Microsoft.NET.Sdk">
              <PropertyGroup>
                <OutputType>Exe</OutputType>
                <TargetFramework>net10.0</TargetFramework>
                <Nullable>enable</Nullable>
                <ImplicitUsings>enable</ImplicitUsings>
                <UseAppHost>false</UseAppHost>
              </PropertyGroup>
              <ItemGroup>
                <PackageReference Include="{PackageId}" Version="{Version}" />
              </ItemGroup>
            </Project>
            """);
        File.WriteAllText(Path.Combine(directory, "Program.cs"), code);

        var environment = new Dictionary<string, string> { ["NUGET_PACKAGES"] = Path.Combine(_root, "packages") };
        var build = await RunDotnetAsync(directory, environment, "build", "-c", "Release", "--nologo");
        return new ConsumerResult(directory, Path.Combine(directory, "bin", "Release", "net10.0"), build, environment);
    }

    public static Task<ProcessResult> RunDotnetAsync(string workingDirectory, params string[] arguments) =>
        RunDotnetAsync(workingDirectory, new Dictionary<string, string>(), arguments);

    public static async Task<ProcessResult> RunDotnetAsync(
        string workingDirectory,
        IReadOnlyDictionary<string, string> environment,
        params string[] arguments)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = workingDirectory,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false
        };

        foreach (var argument in arguments)
        {
            info.ArgumentList.Add(argument);
        }

        // Do not leak the outer MSBuild/test-host state into the nested build.
        foreach (var key in info.Environment.Keys.Where(k => k.StartsWith("MSBUILD", StringComparison.OrdinalIgnoreCase)).ToList())
        {
            info.Environment.Remove(key);
        }

        info.Environment["DOTNET_CLI_TELEMETRY_OPTOUT"] = "1";
        info.Environment["DOTNET_NOLOGO"] = "1";
        info.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        info.Environment["DOTNET_CLI_USE_MSBUILD_SERVER"] = "0";
        foreach (var pair in environment)
        {
            info.Environment[pair.Key] = pair.Value;
        }

        using var process = Process.Start(info)!;
        var output = new StringBuilder();
        process.OutputDataReceived += (_, e) => { lock (output) { output.AppendLine(e.Data); } };
        process.ErrorDataReceived += (_, e) => { lock (output) { output.AppendLine(e.Data); } };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        try
        {
            await process.WaitForExitAsync(timeout.Token);
        }
        catch (OperationCanceledException)
        {
            process.Kill(entireProcessTree: true);
            throw new TimeoutException("dotnet " + string.Join(' ', arguments) + " timed out:\n" + output);
        }

        process.WaitForExit();
        return new ProcessResult(process.ExitCode, output.ToString());
    }

    private static string FindRepositoryRoot()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "CommonLibraries.sln")))
            {
                return directory.FullName;
            }
        }

        throw new InvalidOperationException("Could not locate the repository root from " + AppContext.BaseDirectory);
    }
}

public sealed record ProcessResult(int ExitCode, string Output);

public sealed record ConsumerResult(
    string Directory,
    string OutputDirectory,
    ProcessResult Build,
    IReadOnlyDictionary<string, string> Environment)
{
    public Task<ProcessResult> RunAsync() =>
        PackageFixture.RunDotnetAsync(Directory, Environment, Path.Combine(OutputDirectory, "Consumer.dll"));
}
