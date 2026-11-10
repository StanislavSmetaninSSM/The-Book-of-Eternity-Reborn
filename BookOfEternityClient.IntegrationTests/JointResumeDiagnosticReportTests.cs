using System.Security.Cryptography;
using System.Text;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks exact diagnostic retention with independent small files and no game recovery or authority.
/// </summary>
public sealed class JointResumeDiagnosticReportTests
{
    /// <summary>
    /// Keeps identical UTF8 bytes/hash in two independently mutable files and reports only closed writers.
    /// </summary>
    [Fact]
    public void ExactCopiesAndHashesAreIndependent()
    {
        var root = NewRoot(); using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var bytes = Encoding.UTF8.GetBytes("{\"Logs\":\"Ж😀\",\"Line\":\"a\\nb\"}");
        Assert.Null(JointResumeDiagnosticReport.PublishConfigured(bytes, null, _ => throw new InvalidOperationException("No configured-root callback expected.")));
        var callbacks = 0;
        void Observe(JointResumeDiagnosticReport.Reference reference)
        {
            using var exclusive = new FileStream(reference.Path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
            callbacks++;
        }
        var first = JointResumeDiagnosticReport.Publish(bytes, Path.Combine(root, "one.json"), Observe)!;
        var second = JointResumeDiagnosticReport.Publish(bytes, Path.Combine(root, "two.json"), Observe)!;
        Assert.Equal(2, callbacks); Assert.Equal(bytes.LongLength, first.Bytes);
        Assert.Equal(Convert.ToHexString(SHA256.HashData(bytes)), first.SHA256);
        Assert.Equal(first.SHA256, second.SHA256);
        Assert.Equal(bytes, File.ReadAllBytes(first.Path)); Assert.Equal(bytes, File.ReadAllBytes(second.Path));
        File.WriteAllText(first.Path, "changed"); Assert.Equal(bytes, File.ReadAllBytes(second.Path));
    }

    /// <summary>
    /// Refuses an existing name without changing its bytes or announcing a stored report.
    /// </summary>
    [Fact]
    public void CreateNewCollisionDoesNotOverwrite()
    {
        var root = NewRoot(); using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var path = Path.Combine(root, "existing.json"); File.WriteAllText(path, "original");
        var announced = false;
        Assert.Throws<IOException>(() => JointResumeDiagnosticReport.Publish([1, 2], path, _ => announced = true));
        Assert.False(announced); Assert.Equal("original", File.ReadAllText(path));
    }

    /// <summary>
    /// Refuses nonabsolute and checkout destinations before any report can be created.
    /// </summary>
    /// <param name="kind">The invalid destination boundary.</param>
    [Theory]
    [InlineData("relative")]
    [InlineData("checkout")]
    public void InvalidDestinationRefusesBeforeWriting(string kind)
    {
        var leaf = "report-" + Guid.NewGuid().ToString("N") + ".json";
        var path = kind == "relative" ? leaf : Path.Combine(TestRepoPaths.RepoRoot, leaf);
        Assert.Throws<InvalidDataException>(() => JointResumeDiagnosticReport.Publish([], path, _ => throw new InvalidOperationException()));
        Assert.False(File.Exists(path));
    }

    /// <summary>
    /// Preserves the exact original exception while diagnostic I/O failure stays explicit without a primary.
    /// </summary>
    [Fact]
    public void IoFailurePreservesPrimaryAndIsExplicitWithoutOne()
    {
        var root = NewRoot(); using var cleanup = new CleanupOwnedFixture(root, Console.WriteLine);
        var path = Path.Combine(root, "existing.json"); File.WriteAllText(path, "original");
        var primary = new InvalidOperationException("original game failure");
        var observed = Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            try { throw primary; }
            finally { Assert.Null(JointResumeDiagnosticReport.Publish([1], path, _ => throw new Exception(), primary)); }
        }));
        Assert.Same(primary, observed);
        Assert.Throws<IOException>(() => JointResumeDiagnosticReport.Publish([1], path, _ => throw new Exception()));
        Assert.Equal("original", File.ReadAllText(path));
        var fresh = Path.Combine(root, "callback.json");
        var callbackFailure = Assert.Throws<InvalidOperationException>((Action)(() =>
        {
            try { throw primary; }
            finally { Assert.Null(JointResumeDiagnosticReport.Publish([7], fresh, _ => throw new IOException("reference pipe"), primary)); }
        }));
        Assert.Same(primary, callbackFailure); Assert.Equal(new byte[] { 7 }, File.ReadAllBytes(fresh));
    }

    private static string NewRoot()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-resume-report-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root); return root;
    }
}
