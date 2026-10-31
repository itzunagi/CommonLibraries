using System.IO.Compression;
using System.Xml.Linq;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.PackagingTests;

public class PackageConsumerTests : IClassFixture<PackageFixture>
{
    private readonly PackageFixture _package;

    public PackageConsumerTests(PackageFixture package)
    {
        _package = package;
    }

    private const string ValidConsumer = """
        using Common.SimpleSourceMapper;

        var customer = new Customer { Id = 1, Name = "Ada", Home = new Address { City = "London" } };
        var dto = CustomerMappings.ToDto(customer);
        var manual = ManualMappings.ToDto(customer);
        Console.WriteLine($"{dto.Id}|{dto.DisplayName}|{dto.Home.City}|{manual.DisplayName}");

        public class Address { public string City { get; set; } = ""; }
        public class AddressDto { public string City { get; set; } = ""; }
        public class Customer { public int Id { get; set; } public string Name { get; set; } = ""; public Address Home { get; set; } = new(); }
        public class CustomerDto { public int Id { get; set; } public string DisplayName { get; set; } = ""; public AddressDto Home { get; set; } = new(); }

        public static partial class CustomerMappings
        {
            [Map(MemberList.Both)]
            [ForMember(nameof(CustomerDto.DisplayName), nameof(Customer.Name))]
            public static partial CustomerDto ToDto(Customer source);

            [Map(MemberList.Both)]
            public static partial AddressDto ToDto(Address source);
        }

        public static class ManualMappings
        {
            [ValidateMapping(MemberList.Destination)]
            public static CustomerDto ToDto(Customer source) => new CustomerDto
            {
                Id = source.Id,
                DisplayName = source.Name.ToUpperInvariant(),
                Home = new AddressDto { City = source.Home.City }
            };
        }
        """;

    private const string InvalidConsumer = """
        using Common.SimpleSourceMapper;

        public class Source { public int Id { get; set; } }
        public class Target { public int Id { get; set; } public string Missing { get; set; } = ""; }

        public static partial class Mappings
        {
            [Map(MemberList.Both)]
            public static partial Target ToTarget(Source source);
        }

        public static class Manual
        {
            [ValidateMapping(MemberList.Destination)]
            public static Target ToTarget(Source source) => new Target { Id = source.Id };
        }

        public static class Program { public static void Main() { } }
        """;

    [Fact]
    public async Task Valid_consumer_builds_generates_and_runs()
    {
        var consumer = await _package.BuildConsumerAsync("valid", ValidConsumer);
        Assert.True(consumer.Build.ExitCode == 0, consumer.Build.Output);
        Assert.DoesNotContain("SSM", consumer.Build.Output);

        var run = await consumer.RunAsync();
        Assert.True(run.ExitCode == 0, run.Output);
        Assert.Contains("1|Ada|London|ADA", run.Output);
    }

    [Fact]
    public async Task Invalid_consumer_fails_with_the_expected_diagnostics()
    {
        var consumer = await _package.BuildConsumerAsync("invalid", InvalidConsumer);

        Assert.NotEqual(0, consumer.Build.ExitCode);
        Assert.Contains("SSM005", consumer.Build.Output);
        Assert.Contains("Missing", consumer.Build.Output);
        Assert.Contains("error SSM005", consumer.Build.Output);
    }

    [Fact]
    public async Task Compiler_and_roslyn_assemblies_do_not_become_runtime_dependencies()
    {
        var consumer = await _package.BuildConsumerAsync("runtime-deps", ValidConsumer);
        Assert.True(consumer.Build.ExitCode == 0, consumer.Build.Output);

        var files = Directory.GetFiles(consumer.OutputDirectory).Select(Path.GetFileName).ToList();
        Assert.Contains("Common.SimpleSourceMapper.dll", files);
        Assert.DoesNotContain(files, f => f!.Contains("Compiler", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(files, f => f!.StartsWith("Microsoft.CodeAnalysis", StringComparison.OrdinalIgnoreCase));

        var deps = File.ReadAllText(Path.Combine(consumer.OutputDirectory, "Consumer.deps.json"));
        Assert.DoesNotContain("Microsoft.CodeAnalysis", deps);
        Assert.DoesNotContain("SimpleSourceMapper.Compiler", deps);
    }

    [Fact]
    public void Package_layout_has_attributes_in_lib_and_the_compiler_as_an_analyzer()
    {
        using var archive = ZipFile.OpenRead(_package.PackagePath);
        var entries = archive.Entries.Select(e => e.FullName).ToList();

        Assert.Contains("lib/netstandard2.0/Common.SimpleSourceMapper.dll", entries);
        Assert.Contains("analyzers/dotnet/cs/Common.SimpleSourceMapper.Compiler.dll", entries);
        Assert.DoesNotContain(entries, e => e.StartsWith("lib/", StringComparison.Ordinal) && e.Contains("Compiler", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Contains("Microsoft.CodeAnalysis", StringComparison.Ordinal));
        Assert.DoesNotContain(entries, e => e.Contains("Tests", StringComparison.Ordinal));
        Assert.Contains("README.md", entries);
    }

    [Fact]
    public void Package_has_no_dependencies()
    {
        using var archive = ZipFile.OpenRead(_package.PackagePath);
        using var reader = new StreamReader(archive.GetEntry("Common.SimpleSourceMapper.nuspec")!.Open());
        var nuspec = XDocument.Parse(reader.ReadToEnd());

        Assert.DoesNotContain(nuspec.Descendants(), e => e.Name.LocalName == "dependency");
    }

    [Fact]
    public void Project_files_declare_packability_and_version()
    {
        var main = XDocument.Load(Path.Combine(_package.RepositoryRoot, "Common.SimpleSourceMapper", "Common.SimpleSourceMapper.csproj"));
        Assert.Equal("true", main.Descendants("IsPackable").Single().Value);
        Assert.False(string.IsNullOrWhiteSpace(main.Descendants("Version").Single().Value));
        Assert.Equal("Common.SimpleSourceMapper", main.Descendants("PackageId").Single().Value);

        var tests = XDocument.Load(Path.Combine(_package.RepositoryRoot, "Common.SimpleSourceMapper.Tests", "Common.SimpleSourceMapper.Tests.csproj"));
        Assert.Equal("false", tests.Descendants("IsPackable").Single().Value);
        Assert.Equal("true", tests.Descendants("IsTestProject").Single().Value);

        var compiler = XDocument.Load(Path.Combine(_package.RepositoryRoot, "Common.SimpleSourceMapper.Compiler", "Common.SimpleSourceMapper.Compiler.csproj"));
        Assert.Equal("false", compiler.Descendants("IsPackable").Single().Value);
    }
}
