using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Common.FileProvider.Abstractions;
using Common.Results;
using Common.Results.Models;

namespace Common.FileProvider.FileSystem.Internal;

/// <summary>
/// Provides file access using the local filesystem as the backing store
/// </summary>
internal class FileSystemNamedFileProvider : IFileProvider
{
    #region Private fields

    private readonly FileSystemOptions _options;
    private readonly IResultFactory _resultFactory;

    #endregion

    public FileSystemNamedFileProvider(
        IResultFactory resultFactory,
        FileSystemOptions options)
    {
        _resultFactory = resultFactory ?? throw new ArgumentNullException(nameof(resultFactory));
        _options = options;
        if (string.IsNullOrWhiteSpace(options.BasePath))
        {
            throw new ArgumentException("Base path is required to use file system provider.");
        }
    }

    #region IFileProvider public methods

    /// <inheritdoc />
    public async Task<IResult<IEnumerable<byte>>> GetFileAsync(string relativePath, string filename, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return _resultFactory.BadRequest<IEnumerable<byte>>("Must specify relative path.");
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            return _resultFactory.BadRequest<IEnumerable<byte>>("Must specify filename.");
        }

        var fullPath = Path.Combine(_options.BasePath, relativePath, filename);
        if (!File.Exists(fullPath))
        {
            return _resultFactory.NotFound<IEnumerable<byte>>();
        }

        var fileBytes = await File.ReadAllBytesAsync(fullPath, cancellationToken);

        return _resultFactory.Success(fileBytes.AsEnumerable());
    }

    /// <inheritdoc />
    public async Task<IResult> SaveFileAsync(string relativePath, string filename, Stream inputStream, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return _resultFactory.BadRequest("Must specify relative path.");
        }

        if (string.IsNullOrWhiteSpace(filename))
        {
            return _resultFactory.BadRequest("Must specify filename.");
        }

        var fullPath = Path.Combine(_options.BasePath, relativePath, filename);
        var fileBytes = new byte[inputStream.Length];
        await using Stream fileByteStream = new MemoryStream(fileBytes, true);
        await inputStream.CopyToAsync(fileByteStream, cancellationToken);
        await File.WriteAllBytesAsync(fullPath, fileBytes, cancellationToken);
        return _resultFactory.Success();
    }

    #endregion
}
