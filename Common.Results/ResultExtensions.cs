using Common.Results.Models;

namespace Common.Results
{
    public static class ResultExtensions
    {
        public static IResult<T> AsType<T>(this IResult originalResult)
        {
            return new Result<T>
            {
                Errors = originalResult.Errors,
                StatusCode = originalResult.StatusCode,
                Value = default
            };
        }
    }
}
