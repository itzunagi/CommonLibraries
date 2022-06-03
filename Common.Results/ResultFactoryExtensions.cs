using System.Net;
using Common.Results.Models;

namespace Common.Results
{
    public static class ResultFactoryExtensions
    {
        public static IResult BadRequest(this IResultFactory resultFactory, string errorMessage = "Bad request.")
        {
            return resultFactory.Error(HttpStatusCode.BadRequest, errorMessage);
        }

        public static IResult<T> BadRequest<T>(this IResultFactory resultFactory, string errorMessage = "Bad request.")
        {
            return resultFactory.Error<T>(HttpStatusCode.BadRequest, errorMessage);
        }

        public static IResult<T1> NotFound<T1>(this IResultFactory resultFactory, string errorMessage = "Not found.")
        {
            return resultFactory.Error<T1>(HttpStatusCode.NotFound, errorMessage);
        }
    }
}
