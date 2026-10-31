using Xunit;

namespace Common.SimpleSourceMapper.Tests.Fixtures;

public static class Assertions
{
    /// <summary>Asserts exactly these mapper diagnostics were reported (order-insensitive).</summary>
    public static void AssertIds(this RunResult result, params string[] expected) =>
        Assert.True(
            result.Ids.SequenceEqual(expected.OrderBy(i => i, StringComparer.Ordinal)),
            $"Expected [{string.Join(", ", expected)}] but got [{string.Join(", ", result.Ids)}]\n{result.Describe()}");

    /// <summary>
    /// Successful generation: some source was generated, no generator/analyzer errors, and the updated
    /// compilation has no errors.
    /// </summary>
    public static void AssertGenerated(this RunResult result, int expectedFiles = -1)
    {
        Assert.True(result.Ids.Count == 0, "Unexpected diagnostics:\n" + result.Describe());
        Assert.True(result.CompilationErrors.Count == 0, "Compilation errors:\n" + result.Describe());
        Assert.NotEmpty(result.Generated);
        if (expectedFiles >= 0)
        {
            Assert.Equal(expectedFiles, result.Generated.Length);
        }
    }

    /// <summary>
    /// Failed generation: the expected diagnostics were reported, nothing was generated for the failing mapping,
    /// and no throwing stub was emitted.
    /// </summary>
    public static void AssertRejected(this RunResult result, params string[] expected)
    {
        result.AssertIds(expected);
        Assert.DoesNotContain("NotImplementedException", result.GeneratedText);
    }

    /// <summary>The compilation errors other than the missing partial implementation (CS8795) that a rejected mapping causes.</summary>
    public static IEnumerable<string> UnexpectedCompilationErrors(this RunResult result) =>
        result.CompilationErrors.Where(d => d.Id != "CS8795").Select(d => d.ToString());
}
