using Common.Results.Models;
using System.Collections.Generic;
using System.Net;

namespace Common.Results.Internal
{
    internal class ResultFactory<T> : IResultFactory<T>
    {
        private readonly IReadOnlyCollection<string> _emptyErrors = new string[0];

        public IResult<T1> Error<T1>(HttpStatusCode statusCode, string errorMessage)
        {
            return new Result<T1>
            {
                StatusCode = statusCode,
                Value = default,
                Errors = new[] { errorMessage }
            };
        }

        public IResult Error(HttpStatusCode statusCode, string errorMessage)
        {
            return new Result
            {
                StatusCode = statusCode,
                Errors = new[] { errorMessage }
            };
        }

        public IResult Success()
        {
            return new Result
            {
                StatusCode = HttpStatusCode.OK,
                Errors = _emptyErrors
            };
        }

        public IResult<T1> Success<T1>(T1 value)
        {
            return new Result<T1>
            {
                StatusCode = HttpStatusCode.OK,
                Value = value,
                Errors = _emptyErrors
            };
        }
    }
}
