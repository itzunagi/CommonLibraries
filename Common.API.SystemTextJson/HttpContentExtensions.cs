using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using NodaTime;
using NodaTime.Serialization.SystemTextJson;

namespace Common.API.SystemTextJson;

public static class HttpContentExtensions
{
    public static async Task<T> ReadAsAsync<T>(this HttpContent content) =>
        JsonSerializer.Deserialize<T>(await content.ReadAsStringAsync(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            }
            .ConfigureForNodaTime(DateTimeZoneProviders.Tzdb));
}
