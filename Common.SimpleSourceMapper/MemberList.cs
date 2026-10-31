namespace Common.SimpleSourceMapper;

/// <summary>
/// Selects which member-completeness checks are performed for a mapping method.
/// </summary>
[Flags]
public enum MemberList
{
    /// <summary>Disable completeness checks; signature and mapping-correctness checks still apply.</summary>
    None = 0,

    /// <summary>Verify that every eligible source property is used.</summary>
    Source = 1,

    /// <summary>Verify that every eligible destination property is assigned.</summary>
    Destination = 2,

    /// <summary>Perform both the source and the destination checks.</summary>
    Both = Source | Destination
}
