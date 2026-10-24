using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Common.FileProvider.Abstractions;
using Common.Results;
using Common.Results.Models;

namespace Common.FileProvider.FileSystem.Internal;

/// <summary>
/// Creates new instance of named file providers using local filesystem for storage
/// </summary>
internal class FileSystemNamedFileProviderFactory : INamedFileProviderFactory
{
    #region Private fields

    private readonly IImmutableDictionary<string, FileSystemOptions> _optionsMap;

    #endregion

    public FileSystemNamedFileProviderFactory(
        IDictionary<string, FileSystemOptions> namesToOptionsMap)
    {
        _optionsMap = namesToOptionsMap.ToImmutableDictionary() ??
                      throw new ArgumentNullException(nameof(namesToOptionsMap));
    }

    /// <inheritdoc />
    public IResult<IFileProvider> CreateNamedFileProvider(string name)
    {
        if (!_optionsMap.TryGetValue(name, out var options))
        {
            return ResultFactory.NotFound<IFileProvider>();
        }

        return ResultFactory.Success<IFileProvider>(new FileSystemNamedFileProvider(options));
    }
}
