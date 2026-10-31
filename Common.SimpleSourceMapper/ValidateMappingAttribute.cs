namespace Common.SimpleSourceMapper;

/// <summary>
/// Requests compile-time validation of a handwritten static mapping method.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public sealed class ValidateMappingAttribute : Attribute
{
    public ValidateMappingAttribute(MemberList memberList = MemberList.Destination)
    {
        MemberList = memberList;
    }

    public MemberList MemberList { get; }
}
