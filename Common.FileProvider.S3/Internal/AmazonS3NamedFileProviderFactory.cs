using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Amazon.S3;
using Common.FileProvider.Abstractions;
using Common.Results;
using Common.Results.Models;

namespace Common.FileProvider.S3.Internal;

/// <summary>
/// Creates new instance of named file providers using Amazon S3 for storage
/// </summary>
internal class AmazonS3NamedFileProviderFactory : INamedFileProviderFactory
{
    #region Private fields

    private readonly IAmazonS3 _amazonS3Client;
    private readonly IImmutableDictionary<string, S3BucketOptions> _optionsMap;

    #endregion

    public AmazonS3NamedFileProviderFactory(
        IAmazonS3 amazonS3Client,
        IDictionary<string, S3BucketOptions> namesToBucketOptionsMap)
    {
        _amazonS3Client = amazonS3Client ?? throw new ArgumentNullException(nameof(amazonS3Client));
        _optionsMap = namesToBucketOptionsMap.ToImmutableDictionary() ?? throw new ArgumentNullException(nameof(namesToBucketOptionsMap));
    }

    /// <inheritdoc />
    public IResult<IFileProvider> CreateNamedFileProvider(string name)
    {
        if (!_optionsMap.TryGetValue(name, out var bucketOptions))
        {
            return ResultFactory.NotFound<IFileProvider>();
        }

        return ResultFactory.Success<IFileProvider>(new AmazonS3NamedFileProvider(_amazonS3Client, bucketOptions));
    }
}
