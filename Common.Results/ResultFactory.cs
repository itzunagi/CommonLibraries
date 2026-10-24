using System;
using Common.Results.Models;
using System.Collections.Generic;
using System.Net;

namespace Common.Results;

public static class ResultFactory
{
    private static readonly IReadOnlyCollection<string> EmptyErrors = Array.Empty<string>();

    public static IResult Error(HttpStatusCode statusCode, string errorMessage)
    {
        return new Result
        {
            StatusCode = statusCode,
            Errors = new[] { errorMessage }
        };
    }

    public static IResult Success()
    {
        return new Result
        {
            StatusCode = HttpStatusCode.OK,
            Errors = EmptyErrors
        };
    }

    public static IResult<T> Error<T>(HttpStatusCode statusCode, string errorMessage)
    {
        return new Result<T>
        {
            StatusCode = statusCode,
            Value = default,
            Errors = new[] { errorMessage }
        };
    }

    public static IResult<T> Success<T>(T value)
    {
        return new Result<T>
        {
            StatusCode = HttpStatusCode.OK,
            Value = value,
            Errors = EmptyErrors
        };
    }
    
    public static IResult BadRequest(string errorMessage = "Bad request.")
    {
        return Error(HttpStatusCode.BadRequest, errorMessage);
    }

    public static IResult<T> BadRequest<T>(string errorMessage = "Bad request.")
    {
        return Error<T>(HttpStatusCode.BadRequest, errorMessage);
    }

    public static IResult<T> NotFound<T>(string errorMessage = "Not found.")
    {
        return Error<T>(HttpStatusCode.NotFound, errorMessage);
    }
}
