using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.AnalyzerTests;

public class AttributeAndSignatureTests
{
    private static RunResult Run(string members, string types = "") => Harness.Run(Harness.InMapper(members, types));

    [Fact]
    public void Valid_generated_declaration_has_no_diagnostics()
    {
        var result = Run("[Map(MemberList.Both)] public static partial Dst ToDst(Src source);");
        Assert.True(result.Ids.Count == 0 && result.CompilationErrors.Count == 0, result.Describe());
    }

    [Fact]
    public void Valid_handwritten_declaration_has_no_diagnostics()
    {
        var result = Run("[ValidateMapping(MemberList.Both)] public static Dst ToDst(Src source) => new Dst { Id = source.Id, Name = source.Name };");
        Assert.True(result.Ids.Count == 0 && result.CompilationErrors.Count == 0, result.Describe());
    }

    [Theory]
    [InlineData("[Map] public partial Dst ToDst(Src source);")]
    [InlineData("[Map] internal static partial Dst ToDst(Src source);")]
    [InlineData("[Map] private static partial Dst ToDst(Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(Src a, Src b);")]
    [InlineData("[Map] public static partial Dst ToDst();")]
    [InlineData("[Map] public static partial Dst ToDst<TX>(Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(ref Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(in Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(out Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(params Src[] source);")]
    [InlineData("[Map] public static partial ref Dst ToDst(Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(this Src source);")]
    [InlineData("[Map] public static partial Dst ToDst(Src source = null);")]
    [InlineData("[ValidateMapping] public Dst ToDst(Src source) => new Dst();")]
    [InlineData("[ValidateMapping] internal static Dst ToDst(Src source) => new Dst();")]
    [InlineData("[ValidateMapping] public static Dst ToDst(Src a, Src b) => new Dst();")]
    [InlineData("[ValidateMapping] public static Dst ToDst() => new Dst();")]
    [InlineData("[ValidateMapping] public static void ToDst(Src source) { }")]
    [InlineData("[ValidateMapping] public static Dst ToDst<TX>(Src source) => new Dst();")]
    [InlineData("[ValidateMapping] public static Dst ToDst(ref Src source) => new Dst();")]
    [InlineData("[ValidateMapping] public static Dst ToDst(in Src source) => new Dst();")]
    [InlineData("[ValidateMapping] public static Dst ToDst(out Src source) { source = null!; return new Dst(); }")]
    [InlineData("[ValidateMapping] public static async System.Threading.Tasks.Task<Dst> ToDst(Src source) => await System.Threading.Tasks.Task.FromResult(new Dst());")]
    public void Invalid_signature_reports_SSM001(string members)
    {
        var result = Run(members);
        Assert.Contains("SSM001", result.Ids);
    }

    [Fact]
    public void Generic_containing_type_is_rejected()
    {
        var result = Harness.Run(
            """
            using Common.SimpleSourceMapper;
            using Domain;
            public static partial class G<TX>
            {
                [Map] public static partial Dst ToDst(Src source);
            }
            """);
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void Missing_partial_modifier_on_method_reports_SSM002()
    {
        var result = Run("[Map] public static Dst ToDst(Src source) => new Dst();");
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void Generated_declaration_with_body_reports_SSM002()
    {
        var result = Run("[Map] public static partial Dst ToDst(Src source); public static partial Dst ToDst(Src source) => new Dst();");
        Assert.Contains("SSM002", result.Ids);
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Containing_class_that_is_not_partial_reports_SSM002()
    {
        var result = Harness.Run(
            """
            using Common.SimpleSourceMapper;
            using Domain;
            public static class NotPartial
            {
                [Map] public static partial Dst ToDst(Src source);
            }
            """);
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void Nested_containing_type_that_is_not_partial_reports_SSM002()
    {
        var result = Harness.Run(
            """
            using Common.SimpleSourceMapper;
            using Domain;
            public static partial class Outer
            {
                public static class Inner
                {
                    [Map] public static partial Dst ToDst(Src source);
                }
            }
            """);
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void Struct_containing_type_reports_SSM002()
    {
        var result = Harness.Run(
            """
            using Common.SimpleSourceMapper;
            using Domain;
            public partial struct S
            {
                [Map] public static partial Dst ToDst(Src source);
            }
            """);
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void File_local_containing_type_reports_SSM002()
    {
        var result = Harness.Run(
            """
            using Common.SimpleSourceMapper;
            using Domain;
            file static partial class Hidden
            {
                [Map] public static partial Dst ToDst(Src source);
            }
            """);
        Assert.Contains("SSM002", result.Ids);
    }

    [Fact]
    public void Map_and_ValidateMapping_together_report_SSM003()
    {
        var result = Run("[Map] [ValidateMapping] public static partial Dst ToDst(Src source);");
        Assert.Contains("SSM003", result.Ids);
    }

    [Fact]
    public void ForMember_without_Map_reports_SSM003()
    {
        var result = Run("[ForMember(\"Name\", \"Name\")] public static Dst ToDst(Src source) => new Dst();");
        Assert.Contains("SSM003", result.Ids);
    }

    [Fact]
    public void ForMember_with_ValidateMapping_reports_SSM003()
    {
        var result = Run("[ValidateMapping(MemberList.None)] [ForMember(\"Name\", \"Name\")] public static Dst ToDst(Src source) => new Dst();");
        Assert.Contains("SSM003", result.Ids);
    }

    [Fact]
    public void ValidateMapping_without_implementation_reports_SSM003()
    {
        var result = Run("[ValidateMapping] public static partial Dst ToDst(Src source);");
        Assert.Contains("SSM003", result.Ids);
    }

    [Theory]
    [InlineData("[Map((MemberList)8)] public static partial Dst ToDst(Src source);")]
    [InlineData("[Map((MemberList)(-1))] public static partial Dst ToDst(Src source);")]
    [InlineData("[ValidateMapping((MemberList)4)] public static Dst ToDst(Src source) => new Dst();")]
    public void Undefined_member_list_value_reports_SSM015(string members)
    {
        var result = Run(members);
        Assert.Contains("SSM015", result.Ids);
    }

    [Theory]
    [InlineData("MemberList.None")]
    [InlineData("MemberList.Source")]
    [InlineData("MemberList.Destination")]
    [InlineData("MemberList.Both")]
    [InlineData("MemberList.Source | MemberList.Destination")]
    public void Defined_member_list_values_are_accepted(string value)
    {
        var result = Run($"[Map({value})] public static partial Dst ToDst(Src source);");
        Assert.DoesNotContain("SSM015", result.Ids);
    }

    [Theory]
    [InlineData("public static partial Dst ToDst(System.Collections.Generic.List<int> source);")]
    [InlineData("public static partial Dst ToDst(string source);")]
    [InlineData("public static partial Dst ToDst(int source);")]
    [InlineData("public static partial Dst ToDst(object source);")]
    [InlineData("public static partial Dst ToDst(System.IDisposable source);")]
    [InlineData("public static partial Dst ToDst(Src[] source);")]
    [InlineData("public static partial Dst ToDst(int? source);")]
    [InlineData("public static partial string ToDst(Src source);")]
    [InlineData("public static partial System.Collections.Generic.List<Dst> ToDst(Src source);")]
    [InlineData("public static partial System.IDisposable ToDst(Src source);")]
    public void Unsupported_source_or_destination_shape_reports_SSM013(string declaration)
    {
        var result = Run("[Map] " + declaration);
        Assert.Contains("SSM013", result.Ids);
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Hidden_destination_property_reports_SSM013()
    {
        var result = Run(
            "[Map(MemberList.None)] public static partial Hiding ToHiding(Src source);",
            "public class BaseDst { public int Id { get; set; } } public class Hiding : BaseDst { public new string Id { get; set; } = \"\"; }");
        Assert.Contains("SSM013", result.Ids);
    }
}
