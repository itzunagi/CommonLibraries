using System.Collections.Generic;
using System.Net;

namespace Common.Results.Models
{
    public interface IResult
    {
        bool IsSuccessful();
        HttpStatusCode StatusCode { get; init; }
        IEnumerable<string> Errors { get; init; }
    }
}
