using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.RuntimeTests;

public class RuntimeMappingTests
{
    private const string Source = """
        using System;
        using Common.SimpleSourceMapper;
        namespace Rt
        {
            public enum Level { Low, High }
            public class Address { public string City { get; set; } = ""; public int Zip { get; set; } }
            public class AddressDto { public string City { get; set; } = ""; public long Zip { get; set; } }
            public class Customer
            {
                public int Id { get; set; }
                public string Name { get; set; } = "";
                public int? Age { get; set; }
                public Level Level { get; set; }
                public Guid Key { get; set; }
                public DateTime Created { get; set; }
                public decimal Balance { get; set; }
                public Address Home { get; set; } = new();
                public Address? Work { get; set; }
            }
            public class CustomerDto
            {
                public long Id { get; init; }
                public string DisplayName { get; set; } = "";
                public long? Age { get; set; }
                public Level Level { get; set; }
                public Guid Key { get; set; }
                public DateTime Created { get; set; }
                public decimal Balance { get; set; }
                public AddressDto Home { get; set; } = new();
                public AddressDto? Work { get; set; }
            }
            public static partial class Mappings
            {
                [Map(MemberList.Both)]
                [ForMember(nameof(CustomerDto.DisplayName), nameof(Customer.Name))]
                public static partial CustomerDto ToDto(Customer source);

                [Map(MemberList.Both)]
                public static partial AddressDto ToDto(Address source);
            }
        }
        """;

    private static readonly LoadedAssembly Assembly = Harness.Load(Source);

    private object NewCustomer(object? work = null) => Assembly.New(
        "Rt.Customer",
        ("Id", 7),
        ("Name", "Ada"),
        ("Age", 36),
        ("Level", Enum.ToObject(Assembly.Assembly.GetType("Rt.Level")!, 1)),
        ("Key", Guid.Parse("11111111-2222-3333-4444-555555555555")),
        ("Created", new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc)),
        ("Balance", 12.34m),
        ("Home", Assembly.New("Rt.Address", ("City", "London"), ("Zip", 1234))),
        ("Work", work));

    [Fact]
    public void Scalar_values_are_copied_and_widened()
    {
        var dto = Assembly.Call("Rt.Mappings", "ToDto", NewCustomer())!;

        Assert.Equal(7L, LoadedAssembly.Get(dto, "Id"));
        Assert.Equal(36L, LoadedAssembly.Get(dto, "Age"));
        Assert.Equal(1, Convert.ToInt32(LoadedAssembly.Get(dto, "Level")));
        Assert.Equal(Guid.Parse("11111111-2222-3333-4444-555555555555"), LoadedAssembly.Get(dto, "Key"));
        Assert.Equal(new DateTime(2024, 5, 6, 7, 8, 9, DateTimeKind.Utc), LoadedAssembly.Get(dto, "Created"));
        Assert.Equal(12.34m, LoadedAssembly.Get(dto, "Balance"));
    }

    [Fact]
    public void Renamed_properties_are_mapped()
    {
        var dto = Assembly.Call("Rt.Mappings", "ToDto", NewCustomer())!;
        Assert.Equal("Ada", LoadedAssembly.Get(dto, "DisplayName"));
    }

    [Fact]
    public void Nested_results_are_produced_by_the_nested_mapper()
    {
        var customer = NewCustomer();
        var dto = Assembly.Call("Rt.Mappings", "ToDto", customer)!;
        var home = LoadedAssembly.Get(dto, "Home")!;

        Assert.Equal("Rt.AddressDto", home.GetType().FullName);
        Assert.Equal("London", LoadedAssembly.Get(home, "City"));
        Assert.Equal(1234L, LoadedAssembly.Get(home, "Zip"));
    }

    [Fact]
    public void Nested_mapper_constructs_a_new_object_instead_of_sharing_references()
    {
        var customer = NewCustomer(Assembly.New("Rt.Address", ("City", "Paris"), ("Zip", 75)));
        var first = Assembly.Call("Rt.Mappings", "ToDto", customer)!;
        var second = Assembly.Call("Rt.Mappings", "ToDto", customer)!;

        Assert.NotSame(LoadedAssembly.Get(first, "Home"), LoadedAssembly.Get(second, "Home"));
        Assert.NotSame(LoadedAssembly.Get(first, "Work"), LoadedAssembly.Get(second, "Work"));
        Assert.NotSame(LoadedAssembly.Get(customer, "Home"), LoadedAssembly.Get(first, "Home"));

        // Mutating the result does not write through to the source.
        var home = LoadedAssembly.Get(first, "Home")!;
        home.GetType().GetProperty("City")!.SetValue(home, "changed");
        Assert.Equal("London", LoadedAssembly.Get(LoadedAssembly.Get(customer, "Home")!, "City"));
    }

    [Fact]
    public void Null_nested_source_maps_to_null()
    {
        var dto = Assembly.Call("Rt.Mappings", "ToDto", NewCustomer(work: null))!;
        Assert.Null(LoadedAssembly.Get(dto, "Work"));
    }

    [Fact]
    public void Non_null_nullable_nested_source_is_mapped()
    {
        var dto = Assembly.Call("Rt.Mappings", "ToDto", NewCustomer(Assembly.New("Rt.Address", ("City", "Rome"))))!;
        Assert.Equal("Rome", LoadedAssembly.Get(LoadedAssembly.Get(dto, "Work")!, "City"));
    }

    [Fact]
    public void Null_nullable_scalar_maps_to_null()
    {
        var customer = NewCustomer();
        customer.GetType().GetProperty("Age")!.SetValue(customer, null);
        var dto = Assembly.Call("Rt.Mappings", "ToDto", customer)!;
        Assert.Null(LoadedAssembly.Get(dto, "Age"));
    }

    [Fact]
    public void Null_source_throws_ArgumentNullException()
    {
        var ex = Assert.Throws<ArgumentNullException>(() => Assembly.Call("Rt.Mappings", "ToDto", null, "Rt.Customer"));
        Assert.Equal("source", ex.ParamName);
    }

    [Fact]
    public void Struct_destination_and_record_destination_work_at_runtime()
    {
        var assembly = Harness.Load("""
            using Common.SimpleSourceMapper;
            namespace Rt2
            {
                public class A { public int X { get; set; } public string S { get; set; } = ""; }
                public struct PointDto { public int X { get; set; } public string S { get; set; } }
                public record RecDto { public int X { get; init; } public string S { get; init; } = ""; }
                public static partial class M
                {
                    [Map(MemberList.Both)] public static partial PointDto ToPoint(A source);
                    [Map(MemberList.Both)] public static partial RecDto ToRec(A source);
                }
            }
            """);

        var a = assembly.New("Rt2.A", ("X", 5), ("S", "five"));
        var point = assembly.Call("Rt2.M", "ToPoint", a)!;
        var record = assembly.Call("Rt2.M", "ToRec", a)!;

        Assert.Equal(5, LoadedAssembly.Get(point, "X"));
        Assert.Equal("five", LoadedAssembly.Get(record, "S"));
    }

    [Fact]
    public void Required_and_init_members_are_populated()
    {
        var assembly = Harness.Load("""
            using Common.SimpleSourceMapper;
            namespace Rt3
            {
                public class A { public int Id { get; set; } public string Title { get; set; } = ""; }
                public class B { public required int Id { get; init; } public required string Name { get; init; } }
                public static partial class M
                {
                    [Map(MemberList.Both)] [ForMember("Name", "Title")] public static partial B ToB(A source);
                }
            }
            """);

        var b = assembly.Call("Rt3.M", "ToB", assembly.New("Rt3.A", ("Id", 3), ("Title", "t")))!;
        Assert.Equal(3, LoadedAssembly.Get(b, "Id"));
        Assert.Equal("t", LoadedAssembly.Get(b, "Name"));
    }

    [Fact]
    public void Nullable_nested_struct_sources_map_at_runtime()
    {
        var assembly = Harness.Load("""
            using Common.SimpleSourceMapper;
            namespace Rt4
            {
                public struct P { public int X { get; set; } }
                public struct PDto { public int X { get; set; } }
                public class A { public P? Pt { get; set; } }
                public class B { public PDto? Pt { get; set; } }
                public static partial class M
                {
                    [Map(MemberList.Both)] public static partial B ToB(A source);
                    [Map(MemberList.Both)] public static partial PDto ToDto(P source);
                }
            }
            """);

        var present = assembly.Call("Rt4.M", "ToB", assembly.New("Rt4.A", ("Pt", Activator.CreateInstance(assembly.Assembly.GetType("Rt4.P")!))))!;
        var absent = assembly.Call("Rt4.M", "ToB", assembly.New("Rt4.A"))!;

        Assert.NotNull(LoadedAssembly.Get(present, "Pt"));
        Assert.Null(LoadedAssembly.Get(absent, "Pt"));
    }
}
