namespace Common.Results.Models
{
    public interface IResult<T> : IResult
    {
        T Value { get; init; }
    }
}
