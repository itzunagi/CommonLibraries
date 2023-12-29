using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.Services.Mediator;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddMediator(this IServiceCollection services, ServiceLifetime mediatorLifetime)
    {
        switch (mediatorLifetime)
        {
            case ServiceLifetime.Scoped:
                services.TryAddScoped<IMediator, Mediator>();
                break;
            case ServiceLifetime.Transient:
                services.TryAddTransient<IMediator, Mediator>();
                break;
            case ServiceLifetime.Singleton:
                services.TryAddSingleton<IMediator, Mediator>();
                break;
            default:
                throw new NotSupportedException($"Service lifetime {mediatorLifetime} not supported.");
        }

        return services;
    }
}
