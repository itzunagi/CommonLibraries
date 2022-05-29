using System.Net;
using Common.Results.Models;

namespace Common.Results
{
    public interface IResultFactory
    {
        IResult Error(HttpStatusCode statusCode, string errorMessage);
        IResult Success();
        IResult<T> Error<T>(HttpStatusCode statusCode, string errorMessage);
        IResult<T> Success<T>(T value);
    }
}
