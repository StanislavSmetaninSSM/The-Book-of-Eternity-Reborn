namespace BookOfEternityClient.Core;

internal interface ILoadTransactionOperations
{
    bool DirectoryExists(string path);
    bool FileExists(string path);

    void BeforeCreateDirectory(string path)
    {
    }

    void BeforeMoveDirectory(string sourcePath, string destinationPath)
    {
    }

    void BeforeDeleteDirectory(string path)
    {
    }

    void BeforeDeleteFile(string path)
    {
    }

    void BeforeWriteAllTextAtomic(string path, string content)
    {
    }
}

internal sealed class PhysicalLoadTransactionOperations : ILoadTransactionOperations
{
    internal static PhysicalLoadTransactionOperations Instance { get; } = new();

    private PhysicalLoadTransactionOperations()
    {
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public bool FileExists(string path) => File.Exists(path);
}
