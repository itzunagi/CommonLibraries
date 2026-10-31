using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.GeneratorTests;

public class NestedMappingTests
{
    private const string Models = """
        namespace Models
        {
            public class Address { public string City { get; set; } = ""; }
            public class AddressDto { public string City { get; set; } = ""; }
            public class Person { public int Id { get; set; } public Address Home { get; set; } = new(); }
            public class PersonDto { public int Id { get; set; } public AddressDto Home { get; set; } = new(); }
        }
        """;

    private static RunResult Run(params string[] files) => Harness.Run(files.Prepend(Models));

    private const string PersonMapper = """
        using Common.SimpleSourceMapper;
        using Models;
        public static partial class PersonMappings
        {
            [Map(MemberList.Both)] public static partial PersonDto ToDto(Person source);
        }
        """;

    private const string AddressMapper = """
        using Common.SimpleSourceMapper;
        using Models;
        public static partial class AddressMappings
        {
            [Map(MemberList.Both)] public static partial AddressDto ToDto(Address source);
        }
        """;

    [Fact]
    public void Nested_mapping_is_resolved_across_files_and_classes()
    {
        var result = Run(PersonMapper, AddressMapper);
        result.AssertGenerated(2);
        Assert.Contains("global::AddressMappings.ToDto(source.Home)", result.GeneratedText);
    }

    [Fact]
    public void Declaration_order_does_not_matter()
    {
        var forward = Run(PersonMapper, AddressMapper);
        var reverse = Run(AddressMapper, PersonMapper);
        forward.AssertGenerated(2);
        reverse.AssertGenerated(2);
        Assert.Equal(
            forward.Generated.OrderBy(g => g.HintName).Select(g => g.Text),
            reverse.Generated.OrderBy(g => g.HintName).Select(g => g.Text));
    }

    [Fact]
    public void Nested_mapping_in_the_same_class_is_resolved()
    {
        var result = Run("""
            using Common.SimpleSourceMapper;
            using Models;
            public static partial class Both
            {
                [Map(MemberList.Both)] public static partial PersonDto ToDto(Person source);
                [Map(MemberList.Both)] public static partial AddressDto ToDto(Address source);
            }
            """);
        result.AssertGenerated(2);
    }

    [Fact]
    public void Missing_nested_mapper_reports_SSM008_and_emits_nothing_for_the_mapping()
    {
        var result = Run(PersonMapper);
        result.AssertRejected("SSM008");
        Assert.Empty(result.Generated);
        Assert.Empty(result.UnexpectedCompilationErrors());
    }

    [Fact]
    public void Duplicate_mapping_pair_reports_SSM009_and_makes_dependents_ambiguous()
    {
        var result = Run(PersonMapper, AddressMapper, """
            using Common.SimpleSourceMapper;
            using Models;
            public static partial class OtherAddressMappings
            {
                [Map(MemberList.Both)] public static partial AddressDto Convert(Address source);
            }
            """);
        result.AssertRejected("SSM008", "SSM009", "SSM009");
    }

    [Fact]
    public void Same_type_complex_property_still_requires_a_mapper()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class Inner { public int V { get; set; } } public class A { public Inner P { get; set; } = new(); } public class B { public Inner P { get; set; } = new(); }"));
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Same_type_complex_property_uses_a_declared_mapper()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source); [Map(MemberList.Both)] public static partial Inner Copy(Inner source);",
            "public class Inner { public int V { get; set; } } public class A { public Inner P { get; set; } = new(); } public class B { public Inner P { get; set; } = new(); }"));
        result.AssertGenerated(2);
        Assert.Contains("global::T.M.Copy(source.P)", result.GeneratedText);
    }

    [Fact]
    public void Validate_mapping_methods_are_not_registered_as_nested_mappers()
    {
        var result = Run(PersonMapper, """
            using Common.SimpleSourceMapper;
            using Models;
            public static class Handwritten
            {
                [ValidateMapping(MemberList.Both)]
                public static AddressDto ToDto(Address source) => new AddressDto { City = source.City };
            }
            """);
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Unregistered_collections_are_unsupported()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source); [Map(MemberList.Both)] public static partial Item ToItem(Item source);",
            "public class Item { public int V { get; set; } } " +
            "public class A { public System.Collections.Generic.List<Item> Items { get; set; } = new(); } " +
            "public class B { public System.Collections.Generic.List<Item> Items { get; set; } = new(); }"));
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Array_properties_are_unsupported_collections()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial B ToB(A source);",
            "public class A { public int[] Values { get; set; } = new int[0]; } public class B { public int[] Values { get; set; } = new int[0]; }"));
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Mapper_in_a_different_namespace_is_called_fully_qualified()
    {
        var result = Run(PersonMapper, """
            using Common.SimpleSourceMapper;
            using Models;
            namespace Deep.Inner.Space
            {
                public static partial class AddressMappings
                {
                    [Map(MemberList.Both)] public static partial AddressDto ToDto(Address source);
                }
            }
            """);
        result.AssertGenerated(2);
        Assert.Contains("global::Deep.Inner.Space.AddressMappings.ToDto(source.Home)", result.GeneratedText);
    }

    [Fact]
    public void Nested_mapper_classes_are_supported()
    {
        var result = Run("""
            using Common.SimpleSourceMapper;
            using Models;
            namespace Outer.Space
            {
                public static partial class Mappers
                {
                    public static partial class People
                    {
                        [Map(MemberList.Both)] public static partial PersonDto ToDto(Person source);
                    }

                    public partial class Addresses
                    {
                        [Map(MemberList.Both)] public static partial AddressDto ToDto(Address source);
                    }
                }
            }
            """);
        result.AssertGenerated(2);
        Assert.Contains("global::Outer.Space.Mappers.Addresses.ToDto(source.Home)", result.GeneratedText);
    }

    [Fact]
    public void Private_nested_mapper_is_not_reachable_from_another_class()
    {
        var result = Run(PersonMapper, """
            using Common.SimpleSourceMapper;
            using Models;
            public static partial class Holder
            {
                private static partial class Hidden
                {
                    [Map(MemberList.Both)] public static partial AddressDto ToDto(Address source);
                }
            }
            """);
        result.AssertRejected("SSM008");
    }

    [Fact]
    public void Direct_cycle_is_rejected()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial NodeDto ToDto(Node source);",
            "public class Node { public Node? Next { get; set; } } public class NodeDto { public NodeDto? Next { get; set; } }"));
        result.AssertRejected("SSM014");
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Indirect_cycle_is_rejected_for_every_member_of_the_cycle()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial ADto ToDto(A source); [Map(MemberList.Both)] public static partial BDto ToDto(B source);",
            "public class A { public B? B { get; set; } } public class B { public A? A { get; set; } } " +
            "public class ADto { public BDto? B { get; set; } } public class BDto { public ADto? A { get; set; } }"));
        result.AssertRejected("SSM014", "SSM014");
        Assert.Empty(result.Generated);
    }

    [Fact]
    public void Mappings_that_depend_on_a_cycle_but_are_not_part_of_it_are_still_generated()
    {
        var result = Harness.Run(Harness.InMapper(
            "[Map(MemberList.Both)] public static partial NodeDto ToDto(Node source); [Map(MemberList.Both)] public static partial HolderDto ToDto(Holder source);",
            "public class Node { public Node? Next { get; set; } } public class NodeDto { public NodeDto? Next { get; set; } } " +
            "public class Holder { public Node Root { get; set; } = new(); } public class HolderDto { public NodeDto Root { get; set; } = new(); }"));
        result.AssertIds("SSM014");
        Assert.Single(result.Generated);
    }
}
