using Common.SimpleSourceMapper.Tests.Fixtures;
using Xunit;

namespace Common.SimpleSourceMapper.Tests.AnalyzerTests;

public class HandwrittenAnalyzerTests
{
    private const string Types = """
        public class BaseSrc { public int Id { get; set; } }
        public class DerivedSrc : BaseSrc { public string Name { get; set; } = ""; }
        public class BaseDst { public int Id { get; set; } }
        public class DerivedDst : BaseDst { public string Name { get; set; } = ""; }
        public class VirtualSrc { public virtual int Id { get; set; } }
        public class OverrideSrc : VirtualSrc { public override int Id { get; set; } }
        public class VirtualDst { public virtual int Id { get; set; } }
        public class OverrideDst : VirtualDst { public override int Id { get; set; } }
        public class Nested { public string City { get; set; } = ""; }
        public class WithNested { public Nested Address { get; set; } = new(); public int Id { get; set; } }
        public class ReadOnlyDst { public int Id { get; set; } public string Computed => "x"; }
        public class RSrc { public int Id { get; set; } }
        """;

    private static RunResult Run(string members) => Harness.Run(Harness.InMapper(members, Types));

    private static void AssertIds(RunResult result, params string[] expected) =>
        Assert.True(
            result.Ids.SequenceEqual(expected.OrderBy(i => i, StringComparer.Ordinal)),
            $"Expected [{string.Join(", ", expected)}] but got [{string.Join(", ", result.Ids)}]\n{result.Describe()}");

    [Fact]
    public void Both_is_satisfied_by_expression_bodied_initializer()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Both)] public static Dst Map(Src s) => new Dst { Id = s.Id, Name = s.Name };"));
    }

    [Fact]
    public void Both_is_satisfied_by_block_bodied_initializer()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Both)] public static Dst Map(Src s) { return new Dst { Id = s.Id, Name = s.Name }; }"));
    }

    [Fact]
    public void Default_member_list_validates_destination()
    {
        AssertIds(Run("[ValidateMapping] public static Dst Map(Src s) => new Dst { Id = s.Id };"), "SSM005");
    }

    [Fact]
    public void Source_validation_reports_unread_source_property()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src s) => new Dst { Id = s.Id };"), "SSM004");
    }

    [Fact]
    public void Source_validation_does_not_require_destination_coverage()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src s) => new Dst { Id = s.Id, Name = s.Name };"));
    }

    [Fact]
    public void Destination_validation_does_not_require_source_coverage()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => new Dst { Id = 1, Name = \"x\" };"));
    }

    [Fact]
    public void Destination_validation_reports_missing_assignment()
    {
        var result = Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => new Dst { Id = s.Id };");
        AssertIds(result, "SSM005");
        Assert.Contains("'Name'", result.All[0].GetMessage());
    }

    [Fact]
    public void Both_reports_each_kind_of_gap()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Both)] public static Dst Map(Src s) => new Dst { Id = 1 };"),
            "SSM004", "SSM004", "SSM005");
    }

    [Fact]
    public void None_skips_completeness_checks()
    {
        AssertIds(Run("[ValidateMapping(MemberList.None)] public static Dst Map(Src s) => Helper(s); static Dst Helper(Src s) => new Dst();"));
    }

    [Fact]
    public void Local_variable_construction_is_tracked()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                var d = new Dst();
                d.Id = s.Id;
                d.Name = s.Name;
                return d;
            }
            """));
    }

    [Fact]
    public void Local_variable_construction_reports_missing_assignment()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var d = new Dst();
                    d.Id = s.Id;
                    return d;
                }
                """),
            "SSM005");
    }

    [Fact]
    public void Initializer_and_later_assignments_combine()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                var d = new Dst { Id = s.Id };
                d.Name = s.Name;
                return d;
            }
            """));
    }

    [Fact]
    public void Every_return_path_must_assign()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                if (s.Id > 0)
                {
                    return new Dst { Id = s.Id, Name = s.Name };
                }

                return new Dst { Id = s.Id, Name = s.Name };
            }
            """));
    }

    [Fact]
    public void A_single_incomplete_return_path_is_reported()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    if (s.Id > 0)
                    {
                        return new Dst { Id = s.Id };
                    }

                    return new Dst { Id = s.Id, Name = s.Name };
                }
                """),
            "SSM005");
    }

    [Fact]
    public void Assignment_on_only_one_branch_is_not_definite()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var d = new Dst { Id = s.Id };
                    if (s.Id > 0)
                    {
                        d.Name = s.Name;
                    }

                    return d;
                }
                """),
            "SSM005");
    }

    [Fact]
    public void Assignment_on_every_branch_is_definite()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                var d = new Dst { Id = s.Id };
                if (s.Id > 0)
                {
                    d.Name = s.Name;
                }
                else
                {
                    d.Name = "none" + s.Name;
                }

                return d;
            }
            """));
    }

    [Fact]
    public void Branch_assignments_are_intersected_not_unioned()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var d = new Dst();
                    if (s.Id > 0)
                    {
                        d.Id = 1;
                    }
                    else
                    {
                        d.Name = "x";
                    }

                    return d;
                }
                """),
            "SSM005", "SSM005");
    }

    [Fact]
    public void Conditional_expression_returns_are_checked_per_branch()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => s.Id > 0 ? new Dst { Id = 1, Name = \"a\" } : new Dst { Id = 2 };"),
            "SSM005");
    }

    [Fact]
    public void Assignments_to_a_non_returned_object_do_not_count()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var returned = new Dst { Id = s.Id };
                    var other = new Dst { Name = s.Name };
                    return returned;
                }
                """),
            "SSM005");
    }

    [Fact]
    public void Assignments_in_a_loop_are_not_definite()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var d = new Dst { Id = s.Id };
                    foreach (var x in new[] { 1 })
                    {
                        d.Name = s.Name;
                    }

                    return d;
                }
                """),
            "SSM005");
    }

    [Fact]
    public void Inherited_properties_are_part_of_the_inventory()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Both)] public static DerivedDst Map(DerivedSrc s) => new DerivedDst { Id = s.Id, Name = s.Name };"));
        AssertIds(
            Run("[ValidateMapping(MemberList.Both)] public static DerivedDst Map(DerivedSrc s) => new DerivedDst { Name = s.Name };"),
            "SSM004", "SSM005");
    }

    [Fact]
    public void Overridden_property_is_one_effective_property()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Both)] public static OverrideDst Map(OverrideSrc s) => new OverrideDst { Id = s.Id };"));
    }

    [Fact]
    public void Read_only_destination_properties_are_not_required()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Both)] public static ReadOnlyDst Map(RSrc s) => new ReadOnlyDst { Id = s.Id };"));
    }

    [Fact]
    public void Simple_source_alias_counts_as_reading_the_source()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                var alias = s;
                return new Dst { Id = alias.Id, Name = alias.Name };
            }
            """));
    }

    [Fact]
    public void Simple_destination_alias_is_tracked()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                var d = new Dst();
                var alias = d;
                alias.Id = s.Id;
                alias.Name = s.Name;
                return d;
            }
            """));
    }

    [Fact]
    public void Nameof_is_not_a_read()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src s) => new Dst { Id = s.Id, Name = nameof(s.Name) };"),
            "SSM004");
    }

    [Fact]
    public void Write_only_access_is_not_a_read()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Source)]
                public static Dst Map(Src s)
                {
                    s.Name = "changed";
                    return new Dst { Id = s.Id };
                }
                """),
            "SSM004");
    }

    [Fact]
    public void Nested_access_counts_as_reading_the_top_level_property()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Source)] public static Dst Map(WithNested s) => new Dst { Id = s.Id, Name = s.Address.City };"));
    }

    [Fact]
    public void Deferred_lambda_reads_do_not_count()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Source)]
                public static Dst Map(Src s)
                {
                    Func<string> deferred = () => s.Name;
                    return new Dst { Id = s.Id };
                }
                """),
            "SSM004");
    }

    [Fact]
    public void Deferred_local_function_reads_do_not_count()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Source)]
                public static Dst Map(Src s)
                {
                    string Deferred() => s.Name;
                    return new Dst { Id = s.Id };
                }
                """),
            "SSM004");
    }

    [Fact]
    public void Opaque_helper_returning_destination_cannot_be_proven()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => Helper(s); static Dst Helper(Src s) => new Dst();"),
            "SSM012");
    }

    [Fact]
    public void Passing_the_whole_source_to_a_helper_does_not_imply_reads()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src s) => new Dst { Id = Describe(s) }; static int Describe(Src s) => 0;"),
            "SSM004", "SSM004");
    }

    [Fact]
    public void Destination_passed_to_a_helper_cannot_be_proven()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Destination)]
                public static Dst Map(Src s)
                {
                    var d = new Dst();
                    Fill(d, s);
                    return d;
                }

                static void Fill(Dst d, Src s) { d.Id = s.Id; d.Name = s.Name; }
                """),
            "SSM012");
    }

    [Fact]
    public void Returning_null_cannot_be_proven()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => null!;"),
            "SSM012");
    }

    [Fact]
    public void Try_catch_is_unsupported_control_flow()
    {
        AssertIds(
            Run("""
                [ValidateMapping(MemberList.Both)]
                public static Dst Map(Src s)
                {
                    try
                    {
                        return new Dst { Id = s.Id, Name = s.Name };
                    }
                    catch (Exception)
                    {
                        throw;
                    }
                }
                """),
            "SSM012");
    }

    [Fact]
    public void Try_finally_and_using_are_supported()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                using var scope = new System.IO.MemoryStream();
                var d = new Dst { Id = s.Id };
                try
                {
                    d.Name = s.Name;
                }
                finally
                {
                    scope.Dispose();
                }

                return d;
            }
            """));
    }

    [Fact]
    public void Throw_only_path_satisfies_destination_validation()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Destination)] public static Dst Map(Src s) => throw new NotImplementedException();"));
    }

    [Fact]
    public void Throw_only_path_does_not_read_the_source()
    {
        AssertIds(
            Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src s) => throw new NotImplementedException();"),
            "SSM004", "SSM004");
    }

    [Fact]
    public void Throwing_branch_does_not_weaken_the_other_return_path()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s)
            {
                if (s.Id < 0)
                {
                    throw new ArgumentException("negative");
                }

                return new Dst { Id = s.Id, Name = s.Name };
            }
            """));
    }

    [Fact]
    public void Switch_expression_value_flows_into_assignments()
    {
        AssertIds(Run("""
            [ValidateMapping(MemberList.Both)]
            public static Dst Map(Src s) => new Dst
            {
                Id = s.Id,
                Name = s.Id switch { 0 => "zero", _ => s.Name }
            };
            """));
    }

    [Fact]
    public void Null_conditional_reads_count()
    {
        AssertIds(Run("[ValidateMapping(MemberList.Source)] public static Dst Map(Src? s) => new Dst { Id = s?.Id ?? 0, Name = s?.Name ?? \"\" };"));
    }

    [Fact]
    public void Handwritten_mapping_with_unsupported_signature_is_not_analyzed_further()
    {
        var result = Run("[ValidateMapping(MemberList.Both)] public Dst Map(Src s) => new Dst();");
        AssertIds(result, "SSM001");
    }
}
