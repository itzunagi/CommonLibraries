namespace Common.Results.Models
{
    internal class Result<T> : Result, IResult<T>
    {
        public T Value { get; init; }
    }
}
