using System.Text;

namespace BookOfEternityClient.Core;

internal interface ILoadTransactionOperations
{
    bool DirectoryExists(string path);
    void CreateDirectory(string path);
    void MoveDirectory(string sourcePath, string destinationPath);
    void DeleteDirectory(string path, bool recursive);
    bool FileExists(string path);
    string ReadAllText(string path);
    void WriteAllTextAtomic(string path, string content);
    void DeleteFile(string path);
}

internal sealed class PhysicalLoadTransactionOperations : ILoadTransactionOperations
{
    internal static PhysicalLoadTransactionOperations Instance { get; } = new();

    private PhysicalLoadTransactionOperations()
    {
    }

    public bool DirectoryExists(string path) => Directory.Exists(path);
    public void CreateDirectory(string path) => Directory.CreateDirectory(path);
    public void MoveDirectory(string sourcePath, string destinationPath) =>
        Directory.Move(sourcePath, destinationPath);
    public void DeleteDirectory(string path, bool recursive) => Directory.Delete(path, recursive);
    public bool FileExists(string path) => File.Exists(path);
    public string ReadAllText(string path) => File.ReadAllText(path, Encoding.UTF8);
    public void DeleteFile(string path) => File.Delete(path);

    public void WriteAllTextAtomic(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];

        try
        {
            using (var stream = new FileStream(
                       tempPath,
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None,
                       bufferSize: 4096,
                       FileOptions.WriteThrough))
            {
                var bytes = Encoding.UTF8.GetBytes(content);
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }

            File.Move(tempPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
        }
    }
}
