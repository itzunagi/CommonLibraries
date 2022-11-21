namespace Common.FileProvider.FileSystem;

/// <summary>
/// Options for using a local filesystem for storage
/// </summary>
public struct FileSystemOptions
{
    /// <summary>
    /// Base path for the folder the files should be stored under
    /// </summary>
    public string BasePath { get; set; }
}
