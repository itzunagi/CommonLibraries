using System.Collections.Generic;
using System.Net;

namespace Common.Results.Models
{
    internal class Result : IResult
    {
        public bool IsSuccessful()
        {
            return (int)StatusCode >= 200 && (int)StatusCode < 400;
        }

        public HttpStatusCode StatusCode { get; init; }
        public IEnumerable<string> Errors { get; init; }
    }
}
