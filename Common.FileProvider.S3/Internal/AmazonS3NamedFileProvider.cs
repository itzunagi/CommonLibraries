using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Amazon.S3;
using Amazon.S3.Model;
using Common.FileProvider.Abstractions;
using Common.Results;
using Common.Results.Models;

namespace Common.FileProvider.S3.Internal;

/// <summary>
/// Provides file access using an S3 bucket in AWS as the backing store
/// </summary>
internal sealed class AmazonS3NamedFileProvider : IFileProvider
{
    #region Private fields

    private readonly IAmazonS3 _amazonS3Client;
    private readonly S3BucketOptions _bucketOptions;

    #endregion

    public AmazonS3NamedFileProvider(
        IAmazonS3 amazonS3Client,
        S3BucketOptions bucketOptions)
    {
        _amazonS3Client = amazonS3Client ?? throw new ArgumentNullException(nameof(amazonS3Client));
        _bucketOptions = bucketOptions;
    }

    #region IFileProvider public methods

    /// <inheritdoc />
    public async Task<IResult<IEnumerable<byte>>> GetFileAsync(string relativePath, string filename, CancellationToken cancellationToken = default)
    {
        try
        {
            var getResponse = await _amazonS3Client.GetObjectAsync(new GetObjectRequest
            {
                BucketName = _bucketOptions.BucketName,
                Key = $"{Path.Combine(relativePath, filename)}"
            }, cancellationToken).ConfigureAwait(false);
            var newByteArray = new byte[getResponse.ContentLength];
            await using Stream newByteStream = new MemoryStream(newByteArray, true);
            await getResponse.ResponseStream.CopyToAsync(newByteStream, cancellationToken).ConfigureAwait(false);
            return ResultFactory.Success(newByteArray.AsEnumerable());
        }
        catch (AmazonS3Exception e)
        {
            return ResultFactory.Error<IEnumerable<byte>>(HttpStatusCode.InternalServerError, e.Message);
        }
    }

    /// <inheritdoc />
    public async Task<IResult> SaveFileAsync(string relativePath, string filename, Stream inputStream, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _amazonS3Client.PutObjectAsync(new PutObjectRequest
            {
                BucketName = _bucketOptions.BucketName,
                Key = $"{Path.Combine(relativePath, filename)}",
                InputStream = inputStream
            }, cancellationToken);
            return ResultFactory.Success();
        }
        catch (AmazonS3Exception e)
        {
            return ResultFactory.Error(HttpStatusCode.InternalServerError, e.Message);
        }
    }

    #endregion
}
