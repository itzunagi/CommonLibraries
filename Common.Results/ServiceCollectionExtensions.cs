using Common.Results.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Results
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddResults(this IServiceCollection services)
        {
            services.TryAddSingleton(typeof(IResultFactory<>), typeof(ResultFactory<>));
            return services;
        }
    }
}
