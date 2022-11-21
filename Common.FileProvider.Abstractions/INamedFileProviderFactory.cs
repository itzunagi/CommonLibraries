using Common.Results.Models;

namespace Common.FileProvider.Abstractions;

/// <summary>
/// Creates new instance of named file providers
/// </summary>
public interface INamedFileProviderFactory
{
    /// <summary>
    /// Creates a named file provider using the given name
    /// </summary>
    /// <param name="name">the name of the file provider</param>
    /// <returns>A new instance of a file provider using the given name</returns>
    IResult<IFileProvider> CreateNamedFileProvider(string name);
}
