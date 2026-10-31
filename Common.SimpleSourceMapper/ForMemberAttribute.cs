namespace Common.SimpleSourceMapper;

/// <summary>
/// Maps a destination property from a differently named source property.
/// Valid only on methods that also carry <see cref="MapAttribute"/>.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = true, Inherited = false)]
public sealed class ForMemberAttribute : Attribute
{
    /// <param name="destination">Name of the destination property.</param>
    /// <param name="source">Name of the source property.</param>
    public ForMemberAttribute(string destination, string source)
    {
        Destination = destination;
        Source = source;
    }

    public string Destination { get; }

    public string Source { get; }
}
