using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.GeneratorTests;

public class NullabilityAndConstructionTests
{
    private const string Inner = "public class In { public int V { get; set; } } public class InDto { public int V { get; set; } }";

    private static RunResult Complex(string sourceType, string destinationType) => Harness.Run(Harness.InMapper(
        "[Map(MemberList.Both)] public static partial B ToB(A source); [Map(MemberList.Both)] public static partial InDto ToDto(In source);",
        $"{Inner} public class A {{ public {sourceType} P {{ get; set; }} = default!; }} public class B {{ public {destinationType} P {{ get; set; }} = default!; }}"));

    [Fact]
    public void Nullable_complex_to_nullable_complex_generates_a_null_guard()
    {
        var result = Complex("In?", "InDto?");
        result.AssertGenerated(2);
        Assert.Contains("is null ? default(global::T.InDto) : global::T.M.ToDto(", result.GeneratedText);
    }

    [Fact]
    public void Non_null_complex_to_nullable_complex_calls_the_mapper_directly()
    {
        var result = Complex("In", "InDto?");
        result.AssertGenerated(2);
        Assert.DoesNotContain("is null ?", result.GeneratedText.Replace("source is null", string.Empty));
        Assert.Contains("P = global::T.M.ToDto(source.P)", result.GeneratedText);
    }

    [Fact]
    public void Non_null_complex_to_non_null_complex_calls_the_mapper_directly()
    {
        Complex("In", "InDto").AssertGenerated(2);
    }

    [Fact]
    public void Nullable_complex_to_non_nullable_complex_is_unsafe()
    {
        Complex("In?", "InDto").AssertRejected("SSM011");
    }

    [Fact]
    public void Nullable_value_type_complex_to_nullable_destination_uses_HasValue()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source); [Map(MemberList.Both)] public static partial PointDto ToDto(Point source);",
            "public struct Point { public int X { get; set; } } public struct PointDto { public int X { get; set; } } " +
            "public class A { public Point? P { get; set; } } public class B { public PointDto? P { get; set; } }"));
        result.AssertGenerated(2);
        Assert.Contains(".HasValue ?", result.GeneratedText);
    }

    [Fact]
    public void Nullable_value_type_complex_to_non_nullable_is_unsafe()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source); [Map(MemberList.Both)] public static partial PointDto ToDto(Point source);",
            "public struct Point { public int X { get; set; } } public struct PointDto { public int X { get; set; } } " +
            "public class A { public Point? P { get; set; } } public class B { public PointDto P { get; set; } }"));
        result.AssertRejected("SSM011");
    }

    [Fact]
    public void Nullable_source_is_read_once_into_a_local()
    {
        var result = Complex("In?", "InDto?");
        Assert.Equal(1, result.GeneratedText.Split("source.P").Length - 1);
    }

    [Fact]
    public void Null_source_reference_throws_ArgumentNullException_in_generated_code()
    {
        var result = Harness.Run(Harness.InMapper("[Map(MemberList.Both)] public static partial Dst ToDst(Src source);"));
        Assert.Contains("throw new global::System.ArgumentNullException(nameof(source))", result.GeneratedText);
    }

    [Fact]
    public void Struct_source_has_no_null_check()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public struct A { public int V { get; set; } } public class B { public int V { get; set; } }"));
        result.AssertGenerated(1);
        Assert.DoesNotContain("ArgumentNullException", result.GeneratedText);
    }

    [Fact]
    public void Nullable_oblivious_declarations_generate_without_nullable_warnings()
    {
        var result = Harness.Run(
            "using Common.SimpleSourceMapper;\n#nullable disable\npublic class OA { public string P { get; set; } public OIn In { get; set; } } public class OIn { public int V { get; set; } }" +
            " public class OB { public string P { get; set; } public OInDto In { get; set; } } public class OInDto { public int V { get; set; } }" +
            " public static partial class OM { [Map(MemberList.Both)] public static partial OB ToB(OA source); [Map(MemberList.Both)] public static partial OInDto ToDto(OIn source); }");
        result.AssertGenerated(2);
        Assert.Contains("#nullable disable", result.GeneratedText);
        Assert.DoesNotContain(result.Compilation.GetDiagnostics(TestContext.Current.CancellationToken), d => d.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void Init_only_properties_are_assigned_by_the_initializer()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; init; } }"));
        result.AssertGenerated(1);
    }

    [Fact]
    public void Required_members_are_satisfied_by_matching_properties()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public required int Id { get; init; } }"));
        result.AssertGenerated(1);
    }

    [Fact]
    public void Unmatched_required_member_reports_SSM010_even_without_destination_validation()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.None)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public required string Name { get; set; } }"));
        result.AssertRejected("SSM010");
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Required_member_is_satisfied_by_a_ForMember()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] [ForMember(\"Name\", \"Title\")] public static partial B ToB(A source);",
            "public class A { public string Title { get; set; } = \"\"; } public class B { public required string Name { get; set; } }"));
        result.AssertGenerated(1);
    }

    [Fact]
    public void Constructor_marked_SetsRequiredMembers_satisfies_required_rules()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.None)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public required string Name { get; set; } " +
            "[System.Diagnostics.CodeAnalysis.SetsRequiredMembers] public B() { Name = \"\"; } }"));
        result.AssertGenerated(1);
    }

    [Fact]
    public void Required_field_cannot_be_satisfied()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.None)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public required int Field; }"));
        result.AssertRejected("SSM010");
    }

    [Theory]
    [InlineData("public class B { public int Id { get; set; } private B() { } }")]
    [InlineData("public class B { public int Id { get; set; } public B(int x) { } }")]
    [InlineData("public class B { public int Id { get; set; } protected B() { } }")]
    [InlineData("public abstract class B { public int Id { get; set; } }")]
    [InlineData("public record B(int Id);")]
    [InlineData("public record B(int Id) { public int Extra { get; set; } }")]
    public void Unsupported_construction_reports_SSM010(string destination)
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.None)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } " + destination));
        result.AssertRejected("SSM010");
        Assert.Empty(result.Generated);
    }

    [Theory]
    [InlineData("public class B { public int Id { get; set; } internal B() { } }")]
    [InlineData("public record B { public int Id { get; set; } }")]
    [InlineData("public struct B { public int Id { get; set; } }")]
    [InlineData("public record struct B { public int Id { get; set; } }")]
    public void Supported_construction_generates(string destination)
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } " + destination));
        result.AssertGenerated(1);
    }
}
