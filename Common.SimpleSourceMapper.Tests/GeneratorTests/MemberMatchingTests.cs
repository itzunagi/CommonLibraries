using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.GeneratorTests;

public class MemberMatchingTests
{
    private static RunResult Run(string members, string types = "") => Harness.Run(Harness.InMapper(members, types));

    [Fact]
    public void Exact_name_matches_are_mapped()
    {
        var result = Run("[Map(MemberList.Both)] public static partial Dst ToDst(Src source);");
        result.AssertGenerated(1);
        Assert.Contains("Id = source.Id,", result.GeneratedText);
        Assert.Contains("Name = source.Name,", result.GeneratedText);
    }

    [Fact]
    public void Matching_is_case_sensitive()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int id { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertRejected("SSM004", "SSM005");
    }

    [Fact]
    public void Unmatched_destination_property_is_allowed_when_destination_is_not_validated()
    {
        var result = Run(
            "[Map(MemberList.Source)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public int Extra { get; set; } }");
        result.AssertGenerated(1);
        Assert.DoesNotContain("Extra", result.GeneratedText);
    }

    [Fact]
    public void Unmatched_destination_property_is_reported_by_default()
    {
        var result = Run(
            "[Map] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public int Extra { get; set; } }");
        result.AssertRejected("SSM005");
    }

    [Fact]
    public void Unused_source_property_is_reported_when_source_is_validated()
    {
        var result = Run(
            "[Map(MemberList.Source)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } public int Extra { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertRejected("SSM004");
    }

    [Fact]
    public void None_skips_completeness_checks()
    {
        var result = Run(
            "[Map(MemberList.None)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } public int Extra { get; set; } } public class B { public int Id { get; set; } public int Other { get; set; } }");
        result.AssertGenerated(1);
    }

    [Fact]
    public void ForMember_renames_a_property_destination_first_source_second()
    {
        var result = Run("[Map(MemberList.Both)] [ForMember(nameof(CustomerDto.DisplayName), nameof(Customer.Name))] public static partial CustomerDto ToDto(Customer source);");
        result.AssertGenerated(1);
        Assert.Contains("DisplayName = source.Name,", result.GeneratedText);
    }

    [Fact]
    public void One_source_property_can_populate_several_destination_properties()
    {
        var result = Run(
            "[Map(MemberList.Both)] [ForMember(\"First\", \"Value\")] [ForMember(\"Second\", \"Value\")] public static partial B ToB(A source);",
            "public class A { public int Value { get; set; } } public class B { public int First { get; set; } public int Second { get; set; } }");
        result.AssertGenerated(1);
        Assert.Contains("First = source.Value,", result.GeneratedText);
        Assert.Contains("Second = source.Value,", result.GeneratedText);
    }

    [Fact]
    public void ForMember_overrides_a_convention_match()
    {
        var result = Run(
            "[Map(MemberList.None)] [ForMember(\"Id\", \"Other\")] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } public int Other { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertGenerated(1);
        Assert.Contains("Id = source.Other,", result.GeneratedText);
    }

    [Theory]
    [InlineData("[ForMember(\"Missing\", \"Name\")]")]
    [InlineData("[ForMember(\"DisplayName\", \"Missing\")]")]
    [InlineData("[ForMember(\"displayname\", \"Name\")]")]
    [InlineData("[ForMember(\"DisplayName\", \"name\")]")]
    [InlineData("[ForMember(\"DisplayName\", \"\")]")]
    [InlineData("[ForMember(\"\", \"Name\")]")]
    [InlineData("[ForMember(\"DisplayName\", \"Name.Length\")]")]
    [InlineData("[ForMember(\"Display.Name\", \"Name\")]")]
    public void Invalid_ForMember_reports_SSM006(string forMember)
    {
        var result = Run($"[Map(MemberList.None)] {forMember} public static partial CustomerDto ToDto(Customer source);");
        Assert.Contains("SSM006", result.Ids);
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Invalid_override_does_not_fall_back_to_convention()
    {
        var result = Run(
            "[Map(MemberList.None)] [ForMember(\"Id\", \"Missing\")] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertRejected("SSM006");
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Invalid_override_does_not_cascade_into_completeness_errors()
    {
        var result = Run(
            "[Map(MemberList.Both)] [ForMember(\"Missing\", \"Id\")] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertRejected("SSM006");
    }

    [Fact]
    public void Duplicate_destination_override_reports_SSM007()
    {
        var result = Run(
            "[Map(MemberList.None)] [ForMember(\"Id\", \"A1\")] [ForMember(\"Id\", \"A2\")] public static partial B ToB(A source);",
            "public class A { public int A1 { get; set; } public int A2 { get; set; } } public class B { public int Id { get; set; } }");
        result.AssertRejected("SSM007");
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void ForMember_targeting_a_read_only_destination_reports_SSM006()
    {
        var result = Run(
            "[Map(MemberList.None)] [ForMember(\"Computed\", \"Id\")] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public int Computed => 1; }");
        Assert.Contains("SSM006", result.Ids);
    }

    [Fact]
    public void Read_only_destination_properties_are_ignored_by_completeness()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } } public class B { public int Id { get; set; } public int Computed => 1; }");
        result.AssertGenerated(1);
    }

    [Fact]
    public void Write_only_source_properties_are_not_eligible()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } int _x; public int WriteOnly { set { _x = value; } } } public class B { public int Id { get; set; } }");
        result.AssertGenerated(1);
    }

    [Fact]
    public void Inherited_source_and_destination_properties_are_mapped()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial DerivedDst ToDst(DerivedSrc source);",
            "public class BaseSrc { public int Id { get; set; } } public class DerivedSrc : BaseSrc { public string Name { get; set; } = \"\"; } " +
            "public class BaseDst { public int Id { get; set; } } public class DerivedDst : BaseDst { public string Name { get; set; } = \"\"; }");
        result.AssertGenerated(1);
        Assert.Contains("Id = source.Id,", result.GeneratedText);
    }

    [Fact]
    public void Overridden_property_is_mapped_once()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial OverrideDst ToDst(OverrideSrc source);",
            "public class VirtualSrc { public virtual int Id { get; set; } } public class OverrideSrc : VirtualSrc { public override int Id { get; set; } } " +
            "public class VirtualDst { public virtual int Id { get; set; } } public class OverrideDst : VirtualDst { public override int Id { get; set; } }");
        result.AssertGenerated(1);
        Assert.Equal(1, result.GeneratedText.Split("Id = source.Id").Length - 1);
    }

    [Fact]
    public void Hidden_properties_are_diagnosed()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial HidingDst ToDst(Src source);",
            "public class BaseDst { public int Id { get; set; } } public class HidingDst : BaseDst { public new int Id { get; set; } public string Name { get; set; } = \"\"; }");
        result.AssertRejected("SSM013");
    }

    [Fact]
    public void Fields_indexers_and_static_properties_are_ignored()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } public int Field; public int this[int i] => i; public static int Static { get; set; } } " +
            "public class B { public int Id { get; set; } public int Field; public static int Static { get; set; } }");
        result.AssertGenerated(1);
        Assert.DoesNotContain("Field", result.GeneratedText);
        Assert.DoesNotContain("Static", result.GeneratedText);
    }

    [Fact]
    public void Explicit_interface_properties_are_ignored()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public interface IHasName { string Name { get; } } public class A : IHasName { public int Id { get; set; } string IHasName.Name => \"\"; } public class B { public int Id { get; set; } }");
        result.AssertGenerated(1);
    }

    [Fact]
    public void Non_public_properties_are_ignored()
    {
        var result = Run(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int Id { get; set; } internal int Hidden { get; set; } protected int Prot { get; set; } public int Inner { private get; set; } } " +
            "public class B { public int Id { get; set; } internal int Hidden { get; set; } public int Inner { get; private set; } }");
        result.AssertGenerated(1);
    }
}
