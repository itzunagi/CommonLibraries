using System.Collections.Generic;
using Amazon.S3;
using Common.FileProvider.Abstractions;
using Common.FileProvider.S3.Internal;
using Common.Results;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.FileProvider.S3;

/// <summary>
/// This class provides extension methods for IServiceCollection to add S3 file providers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a singleton instance of the AmazonS3FileProviderFactory for providing named file providers
    /// </summary>
    /// <param name="services">the service collection</param>
    /// <param name="bucketOptionsMap">a dictionary mapping names to S3 bucket options</param>
    /// <returns>the service collection</returns>
    public static IServiceCollection AddS3FileProviders(
        this IServiceCollection services,
        Dictionary<string, S3BucketOptions> bucketOptionsMap)
    {
        services.AddResults();
        services.TryAddSingleton<INamedFileProviderFactory>(serviceProvider =>
            new AmazonS3NamedFileProviderFactory(
                serviceProvider.GetRequiredService<IAmazonS3>(),
                serviceProvider.GetRequiredService<IResultFactory>(),
                bucketOptionsMap));

        return services;
    }
}
