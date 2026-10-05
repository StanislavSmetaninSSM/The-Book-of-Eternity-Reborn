using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text.Json;

namespace BookOfEternityClient.Services.GmWorkers;

internal static class GmWorkerNativePackage
{
    internal static string Validate(string? directory)
    {
        if (!OperatingSystem.IsLinux() || RuntimeInformation.ProcessArchitecture != Architecture.X64)
            throw new PlatformNotSupportedException("Native lineage host is qualified only for linux-x64/glibc.");
        directory ??= Path.Combine(AppContext.BaseDirectory, "runtimes", "linux-x64", "native");
        directory = Path.GetFullPath(directory);
        var manifestPath = Path.Combine(directory, "package-manifest.json");
        if (new FileInfo(manifestPath).Length > 16384) throw Invalid();
        using var document = JsonDocument.Parse(File.ReadAllBytes(manifestPath));
        var root = document.RootElement;
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject()) if (!names.Add(property.Name)) throw Invalid();
        if (root.GetProperty("schemaVersion").GetInt32() != 1 ||
            root.GetProperty("maximumProtocolVersion").GetInt32() != 2 ||
            root.GetProperty("runtimeIdentifier").GetString() != "linux-x64" ||
            root.GetProperty("guarantee").GetString() != GmWorkerBackendSelector.NativeGuarantee ||
            root.GetProperty("binary").GetString() != "boe-lineage-supervisor") throw Invalid();
        foreach (var key in new[] { "sourceCommit", "sourceSha256", "binarySha256", "compilerSha256" })
        {
            var hash = root.GetProperty(key).GetString();
            if (hash == null || hash.Length != (key == "sourceCommit" ? 40 : 64) || hash.Any(c => !char.IsAsciiHexDigit(c) || char.IsUpper(c))) throw Invalid();
        }
        var libc = Marshal.PtrToStringAnsi(GetLibcVersion());
        if (!Version.TryParse(libc, out var current) ||
            !Version.TryParse(root.GetProperty("minimumGlibc").GetString(), out var minimum) || current < minimum)
            throw new PlatformNotSupportedException("Native lineage package requires a newer measured glibc ABI.");
        var executable = Path.Combine(directory, "boe-lineage-supervisor");
        using var binary = File.OpenRead(executable);
        Span<byte> header = stackalloc byte[20]; binary.ReadExactly(header);
        if (!header[..6].SequenceEqual(new byte[] { 127, 69, 76, 70, 2, 1 }) || header[18] != 62 || header[19] != 0) throw Invalid();
        binary.Position = 0;
        var digest = Convert.ToHexString(SHA256.HashData(binary)).ToLowerInvariant();
        if (digest != root.GetProperty("binarySha256").GetString() ||
            (File.GetUnixFileMode(executable) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) == 0) throw Invalid();
        return executable;
    }

    private static InvalidDataException Invalid() => new("Native lineage package metadata or executable is incompatible.");
    [DllImport("libc", EntryPoint = "gnu_get_libc_version")]
    private static extern IntPtr GetLibcVersion();
}
