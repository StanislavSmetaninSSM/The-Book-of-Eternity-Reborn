using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Keeps an opaque resumer report after disposable process containers close; it never supplies recovery input.
/// </summary>
internal static class JointResumeDiagnosticReport
{
    /// <summary>
    /// Identifies one closed diagnostic file without granting authority over its described game root.
    /// </summary>
    internal sealed record Reference(string Path, long Bytes, string SHA256);

    /// <summary>
    /// Writes exact caller-owned bytes once and publishes the reference only after the physical writer closes.
    /// </summary>
    /// <param name="payload">Already serialized immutable UTF8 report bytes.</param>
    /// <param name="path">A new absolute diagnostic file outside checkout.</param>
    /// <param name="publishReference">Synchronous receiver for the closed-file reference.</param>
    /// <param name="primaryFailure">An existing failure that secondary diagnostic I/O must not replace.</param>
    /// <returns>The stored reference, or null when a secondary failure accompanies an existing primary.</returns>
    internal static Reference? Publish(byte[] payload, string path, Action<Reference> publishReference,
        Exception? primaryFailure = null)
    {
        try
        {
            if (!Path.IsPathFullyQualified(path)) throw new InvalidDataException("A resumer report path must be absolute.");
            path = Path.GetFullPath(path);
            var relative = Path.GetRelativePath(TestRepoPaths.RepoRoot, path);
            if (!Path.IsPathFullyQualified(relative) && relative != ".." &&
                !relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
                throw new InvalidDataException("A resumer report must remain outside checkout.");
            var parent = Path.GetDirectoryName(path)!;
            var scope = new TrustedLocalFileScope([parent]);
            scope.EnsureDirectory(parent);
            scope.ValidateFile(path, allowMissing: true);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                stream.Write(payload);
                stream.Flush(flushToDisk: true);
            }
            var reference = new Reference(path, payload.LongLength, Convert.ToHexString(SHA256.HashData(payload)));
            publishReference(reference);
            return reference;
        }
        catch (Exception secondary) when (primaryFailure is not null)
        {
            try
            {
                Console.Error.WriteLine(JsonSerializer.Serialize(new { Boundary = "joint_resume_report_failure",
                    Primary = primaryFailure.ToString(), Secondary = secondary.ToString() }));
                Console.Error.Flush();
            }
            catch (Exception) { /* Preserve the already propagating primary even when both diagnostic streams fail. */ }
            return null;
        }
    }
}
