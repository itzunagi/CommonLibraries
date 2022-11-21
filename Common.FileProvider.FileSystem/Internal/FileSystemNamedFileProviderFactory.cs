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
    private readonly IResultFactory _resultFactory;

    #endregion

    public FileSystemNamedFileProviderFactory(
        IResultFactory resultFactory,
        IDictionary<string, FileSystemOptions> namesToOptionsMap)
    {
        _optionsMap = namesToOptionsMap.ToImmutableDictionary() ??
                      throw new ArgumentNullException(nameof(namesToOptionsMap));
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
    }

    /// <inheritdoc />
    public IResult<IFileProvider> CreateNamedFileProvider(string name)
    {
        if (!_optionsMap.TryGetValue(name, out var options))
        {
            return _resultFactory.NotFound<IFileProvider>();
        }

        return _resultFactory.Success((IFileProvider)new FileSystemNamedFileProvider(
            _resultFactory,
            options));
    }
}
