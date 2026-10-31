using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.GeneratorTests;

public class ScalarMappingTests
{
    private const string Prelude = "public enum Color { Red, Green } public enum Shape { Round }";

    private static RunResult Map(string sourceType, string destinationType) => Harness.Run(Harness.InMapper(
        "[Map(MemberList.Both)] public static partial B ToB(A source);",
        $"{Prelude} public class A {{ public {sourceType} P {{ get; set; }} = default!; }} public class B {{ public {destinationType} P {{ get; set; }} = default!; }}"));

    [Theory]
    [InlineData("bool", "bool")]
    [InlineData("char", "char")]
    [InlineData("byte", "byte")]
    [InlineData("sbyte", "sbyte")]
    [InlineData("short", "short")]
    [InlineData("ushort", "ushort")]
    [InlineData("int", "int")]
    [InlineData("uint", "uint")]
    [InlineData("long", "long")]
    [InlineData("ulong", "ulong")]
    [InlineData("float", "float")]
    [InlineData("double", "double")]
    [InlineData("decimal", "decimal")]
    [InlineData("string", "string")]
    [InlineData("Color", "Color")]
    [InlineData("System.Guid", "System.Guid")]
    [InlineData("System.DateTime", "System.DateTime")]
    [InlineData("System.DateTimeOffset", "System.DateTimeOffset")]
    [InlineData("System.TimeSpan", "System.TimeSpan")]
    [InlineData("System.DateOnly", "System.DateOnly")]
    [InlineData("System.TimeOnly", "System.TimeOnly")]
    [InlineData("nint", "nint")]
    public void Every_scalar_category_maps_to_itself(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertGenerated(1);

    [Theory]
    [InlineData("int", "long")]
    [InlineData("int", "double")]
    [InlineData("int", "decimal")]
    [InlineData("byte", "short")]
    [InlineData("byte", "int")]
    [InlineData("char", "int")]
    [InlineData("float", "double")]
    [InlineData("long", "float")]
    [InlineData("uint", "long")]
    public void Implicit_widening_conversions_are_allowed(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertGenerated(1);

    [Theory]
    [InlineData("int", "int?")]
    [InlineData("int?", "int?")]
    [InlineData("int?", "long?")]
    [InlineData("int", "long?")]
    [InlineData("Color", "Color?")]
    [InlineData("System.Guid", "System.Guid?")]
    [InlineData("string", "string?")]
    [InlineData("string?", "string?")]
    public void Compatible_nullable_forms_are_allowed(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertGenerated(1);

    [Theory]
    [InlineData("long", "int")]
    [InlineData("double", "float")]
    [InlineData("double", "decimal")]
    [InlineData("decimal", "double")]
    [InlineData("int", "byte")]
    [InlineData("int", "char")]
    [InlineData("int", "uint")]
    [InlineData("int", "bool")]
    [InlineData("string", "int")]
    [InlineData("int", "string")]
    [InlineData("string", "System.Guid")]
    [InlineData("System.Guid", "string")]
    [InlineData("Color", "int")]
    [InlineData("int", "Color")]
    [InlineData("Color", "Shape")]
    [InlineData("System.DateTime", "System.DateTimeOffset")]
    [InlineData("System.DateTime", "System.DateOnly")]
    [InlineData("long?", "int?")]
    public void Narrowing_parsing_formatting_and_user_defined_conversions_are_rejected(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertRejected("SSM008");

    [Theory]
    [InlineData("int?", "int")]
    [InlineData("int?", "long")]
    [InlineData("string?", "string")]
    [InlineData("Color?", "Color")]
    public void Nullable_source_to_non_nullable_destination_is_unsafe(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertRejected("SSM011");

    [Theory]
    [InlineData("object", "object")]
    [InlineData("int", "object")]
    [InlineData("object", "int")]
    [InlineData("object", "string")]
    public void Object_is_not_an_automatic_escape_hatch(string sourceType, string destinationType) =>
        Map(sourceType, destinationType).AssertRejected("SSM008");

    [Fact]
    public void Scalar_to_complex_is_rejected()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class Inner { } public class A { public int P { get; set; } } public class B { public Inner P { get; set; } = new(); }"));
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Custom_struct_is_not_scalar()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public struct Money { public decimal Amount { get; set; } } public class A { public Money P { get; set; } } public class B { public Money P { get; set; } }"));
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Framework_struct_outside_the_allowlist_is_not_scalar()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public System.Version P { get; set; } = new(); } public class B { public System.Version P { get; set; } = new(); }"));
        result.AssertRejected("SSM008");
    }
}
