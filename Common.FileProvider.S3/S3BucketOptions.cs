namespace Common.FileProvider.S3;

/// <summary>
/// Options for using an S3 bucket for storage
/// </summary>
public struct S3BucketOptions
{
    /// <summary>
    /// Name of the S3 bucket to use
    /// </summary>
    public string BucketName { get; set; }
}
