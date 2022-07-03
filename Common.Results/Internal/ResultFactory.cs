using System;
using Common.Results.Models;
using System.Collections.Generic;
using System.Net;

namespace Common.Results.Internal;

internal class ResultFactory : IResultFactory
{
    protected readonly IReadOnlyCollection<string> EmptyErrors = Array.Empty<string>();

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
            Errors = EmptyErrors
        };
    }

    public IResult<T> Error<T>(HttpStatusCode statusCode, string errorMessage)
    {
        return new Result<T>
        {
            StatusCode = statusCode,
            Value = default,
            Errors = new[] { errorMessage }
        };
    }

    public IResult<T> Success<T>(T value)
    {
        return new Result<T>
        {
            StatusCode = HttpStatusCode.OK,
            Value = value,
            Errors = EmptyErrors
        };
    }
}
