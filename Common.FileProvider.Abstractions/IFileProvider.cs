using Common.Results.Models;

namespace Common.FileProvider.Abstractions;

/// <summary>
/// Provides an interface for storing and retrieving binary files from a storage location
/// </summary>
public interface IFileProvider
{
    /// <summary>
    /// Gets a file from the storage provider
    /// </summary>
    /// <param name="relativePath">relative path to the file from the provider's base path</param>
    /// <param name="filename">name of the file to retrieve</param>
    /// <param name="cancellationToken">synchronized cancellation token for this task</param>
    /// <returns>Result object indicating success or failure, and a stream containing the file data if successful</returns>
    Task<IResult<IEnumerable<byte>>> GetFileAsync(string relativePath, string filename, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stores a file in the storage provider
    /// </summary>
    /// <param name="relativePath">relative path to the file from the provider's base path</param>
    /// <param name="filename">name of the file to store</param>
    /// <param name="inputStream">stream containing the file bytes to store</param>
    /// <param name="cancellationToken">synchronized cancellation token for this task</param>
    /// <returns>Result object indicating success or failure</returns>
    Task<IResult> SaveFileAsync(string relativePath, string filename, Stream inputStream, CancellationToken cancellationToken = default);
}
