using System.Collections.Generic;
using Common.FileProvider.Abstractions;
using Common.FileProvider.FileSystem.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Common.FileProvider.FileSystem;

/// <summary>
/// This class provides extension methods for IServiceCollection to add file system file providers
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds a singleton instance of the FileSystemFileProviderFactory for providing named file providers
    /// </summary>
    /// <param name="services">the service collection</param>
    /// <param name="fileSystemOptionsMap">a dictionary mapping names to file system options</param>
    /// <returns>the service collection</returns>
    public static IServiceCollection AddFileSystemFileProviders(
        this IServiceCollection services,
        Dictionary<string, FileSystemOptions> fileSystemOptionsMap)
    {
        services.TryAddSingleton<INamedFileProviderFactory>(serviceProvider =>
            new FileSystemNamedFileProviderFactory(fileSystemOptionsMap));

        return services;
    }
}
