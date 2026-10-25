using BookOfEternityClient.Core;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class SourceValidationBatchTests
{
    /// <summary>
    /// Allows serial thread hops while preserving exact ordinal API and Windows-role DATA identities.
    /// </summary>
    /// <returns>
    /// Completion after real classifiers, detached counters and service cleanup are checked.
    /// </returns>
    [Fact]
    public async Task SerialThreadHopKeepsDirectoryDataAndSeparateClassifierFamilies()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var owner = new BatchOwner();
        using var batch = new FileSystemManager.SourceValidationBatch(fixture.Files, lease, owner, owner.Work,
            owner.Version, lease.MainAdmission!, lease.AmbientRegistration!);
        var path = fixture.Files.GameSessionPath;
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, batch.ObserveTrusted(path));
        await Task.Run(() => Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, batch.ObserveTrusted(path)));
        Assert.Equal(1, batch.CaptureStatistics().SourceQueries);
        Assert.Equal(1, batch.CaptureStatistics().Hits);
        Assert.True(batch.ObserveDirectory(path, PathMetadataRole.Root).Exists);
        Assert.True(batch.ObserveDirectory(path, PathMetadataRole.Segment).Exists);
        batch.ObserveDirectory(path, PathMetadataRole.Root);
        Assert.Equal(3, batch.CaptureStatistics().SourceQueries);
        Assert.Equal(2, batch.CaptureStatistics().Hits);
        Assert.False(batch.CanShareAdmission(new FileSystemManager(fixture.Files.BasePath, Microsoft.Extensions.Logging.Abstractions.NullLogger<FileSystemManager>.Instance)));
        batch.Dispose();
        Assert.True(batch.CaptureState().Closed);
        Assert.Equal(0, batch.CaptureState().Entries);
        Assert.Null(batch.Active);
    }

    /// <summary>
    /// Never retains a file or absence as positive directory DATA.
    /// </summary>
    /// <returns>
    /// Completion after newly appearing and reclassified entries are observed freshly.
    /// </returns>
    [Fact]
    public async Task FilesAndAbsenceRemainFresh()
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var owner = new BatchOwner();
        using var batch = new FileSystemManager.SourceValidationBatch(fixture.Files, lease, owner, owner.Work,
            owner.Version, lease.MainAdmission!, lease.AmbientRegistration!);
        var path = Path.Combine(fixture.Files.GameSessionPath, "changing");
        Assert.Equal(TrustedLocalFileScope.EntryKind.Missing, batch.ObserveTrusted(path));
        File.WriteAllText(path, "file");
        Assert.Equal(TrustedLocalFileScope.EntryKind.RegularFile, batch.ObserveTrusted(path));
        File.Delete(path); Directory.CreateDirectory(path);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, batch.ObserveTrusted(path));
        Assert.Equal(3, batch.CaptureStatistics().SourceQueries);
        Assert.Equal(0, batch.CaptureStatistics().Hits);
    }

    /// <summary>
    /// Permanently clears positive DATA after the original eligibility proof is lost.
    /// </summary>
    /// <param name="loss">
    /// Root token, work identity, policy, suppression or explicit lifetime retirement.
    /// </param>
    /// <returns>
    /// Completion after fresh classification observes a changed former directory and service DATA is empty.
    /// </returns>
    [Theory]
    [InlineData("version")]
    [InlineData("work")]
    [InlineData("policy")]
    [InlineData("suppression")]
    [InlineData("close")]
    public async Task EligibilityLossPermanentlyRetiresData(string loss)
    {
        using var fixture = new LifecycleProducerReadForwarderTests.Fixture(output, false);
        await fixture.InitializeAsync();
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var owner = new BatchOwner();
        using var batch = new FileSystemManager.SourceValidationBatch(fixture.Files, lease, owner, owner.Work,
            owner.Version, lease.MainAdmission!, lease.AmbientRegistration!);
        var path = Path.Combine(fixture.Files.GameSessionPath, "changed-directory");
        Directory.CreateDirectory(path);
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, batch.ObserveTrusted(path));
        Assert.Equal(TrustedLocalFileScope.EntryKind.Directory, batch.ObserveTrusted(path));
        Assert.Equal(1, batch.CaptureStatistics().Hits);
        if (loss == "version") owner.Version = new();
        else if (loss == "work") owner.Work = new();
        else if (loss == "policy") fixture.Files.SourceValidationBatchReuseEnabled = false;
        else if (loss == "close") batch.Dispose();
        using (loss == "suppression" ? FileSystemManager.SuppressValidationReuse() : null) Assert.Null(batch.Active);
        fixture.Files.SourceValidationBatchReuseEnabled = true;
        Directory.Delete(path); File.WriteAllText(path, "now a file");
        Assert.Equal(TrustedLocalFileScope.EntryKind.RegularFile, batch.ObserveTrusted(path));
        Assert.Null(batch.Active);
        Assert.True(batch.CaptureState().Closed);
        Assert.Equal(0, batch.CaptureState().Entries);
    }

    /// <summary>
    /// Supplies only mutable in-memory work/version identities for isolated cache-lifetime fixtures.
    /// </summary>
    private sealed class BatchOwner : FileSystemManager.ISourceValidationBatchOwner
    {
        /// <summary>
        /// Gets or replaces the serial work identity to exercise retirement.
        /// </summary>
        internal object Work { get; set; } = new();

        /// <summary>
        /// Gets or replaces the opaque directory version to exercise retirement.
        /// </summary>
        internal object Version { get; set; } = new();

        /// <inheritdoc />
        public bool TryCaptureBatchVersion(object work, out object? version)
        {
            version = ReferenceEquals(work, Work) ? Version : null;
            return version != null;
        }
    }
}
