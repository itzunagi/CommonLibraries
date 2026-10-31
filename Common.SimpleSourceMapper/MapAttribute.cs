namespace Common.SimpleSourceMapper;

/// <summary>
/// Declares a static partial mapping method whose implementation is generated at compile time.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class MapAttribute : Attribute
{
    public MapAttribute(MemberList memberList = MemberList.Destination)
    {
        MemberList = memberList;
    }

    public MemberList MemberList { get; }
}
