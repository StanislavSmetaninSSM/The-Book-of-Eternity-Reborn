using System.Text.Json;
using BookOfEternityClient.Core;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>
/// Checks genuine Linux replacement leases without Windows legacy recovery's independent DATA retirement.
/// </summary>
/// <param name="output">
/// Receives uncapped existing named observations, physical source counts and owned cleanup evidence.
/// </param>
public sealed class SourceDataOperationLinuxTests(ITestOutputHelper output)
{
    /// <summary>
    /// Keeps an unsupported fresh reader outside the source adapter while retaining ordinary provisional DATA.
    /// </summary>
    /// <param name="reader">The original implicit sync/async or explicit unsupported reader route.</param>
    /// <returns>Completion after ordinary replacement reuse and its full physical success fence.</returns>
    [Theory]
    [InlineData("implicit-async")]
    [InlineData("implicit-sync")]
    [InlineData("explicit")]
    public async Task UnsupportedAbsentReaderPreservesOrdinarySourceLifetime(string reader)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires genuine Linux lease admission.");
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            Assert.Null(await files.ReadFileBytesAsync(first, "missing.txt"));
        }

        var absentSources = 0;
        fixture.OnRead = path => { if (path == "missing.txt") absentSources++; };
        fixture.Recording = true;
        await ReadAbsentWithUnsupportedAdapterAsync(files, reader);
        var firstAbsentSources = absentSources;
        Assert.True(firstAbsentSources > 0, "The first unsupported reader must physically observe absence.");
        await ReadAbsentWithUnsupportedAdapterAsync(files, reader);
        Assert.True(absentSources > firstAbsentSources, "The second unsupported reader must independently observe absence.");
        await using var replacement = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal("original", await files.ReadFileAsync(replacement, "present.txt"));
        Assert.Equal("original", await files.ReadFileAsync(replacement, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        await operation.CompleteAsync();
        Assert.Equal(1, fixture.PresentSources);
        output.WriteLine(JsonSerializer.Serialize(new { reader, absentSources,
            FunctionalPresentSources = 0, FreshFencePresentSources = fixture.PresentSources }));
    }

    /// <summary>
    /// Retains the original byte and absence dependencies across an unsupported fresh reader.
    /// </summary>
    /// <param name="change">The external untracked byte or absence drift after the unsupported read.</param>
    /// <returns>Completion after no effect or owning success escapes the mandatory fresh fence.</returns>
    [Theory]
    [InlineData("bytes")]
    [InlineData("appearance")]
    public async Task UnsupportedAbsentReaderCannotRetirePendingDriftProof(string change)
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires genuine Linux lease admission.");
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            Assert.Null(await files.ReadFileBytesAsync(first, "missing.txt"));
        }

        await ReadAbsentWithUnsupportedAdapterAsync(files, "implicit-async");
        if (change == "bytes") File.WriteAllText(fixture.PresentPath, "modified");
        else File.WriteAllText(Path.Combine(files.GameSessionPath, "missing.txt"), "appeared");
        fixture.Recording = true;
        await using var replacement = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal("original", await files.ReadFileAsync(replacement, "present.txt"));
        Assert.Null(await files.ReadFileBytesAsync(replacement, "missing.txt"));
        Assert.Equal(0, fixture.PresentSources);
        await Assert.ThrowsAsync<InvalidDataException>(() => operation.VerifyAsync(replacement));
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(replacement, "effect.txt", "forbidden"));
        await Assert.ThrowsAsync<InvalidOperationException>(operation.CompleteAsync);
        Assert.False(File.Exists(Path.Combine(files.GameSessionPath, "effect.txt")));
    }

    /// <summary>
    /// Uses the original ordinary missing-file path or an explicitly unsupported physical adapter.
    /// </summary>
    /// <param name="files">The actual manager retaining the whole-operation source owner.</param>
    /// <param name="reader">The original reader route selected by this case.</param>
    /// <returns>Completion after a fresh absence observation and normal owned adapter closing.</returns>
    private static async Task ReadAbsentWithUnsupportedAdapterAsync(FileSystemManager files, string reader)
    {
        switch (reader)
        {
            case "implicit-async": Assert.Null(await files.ReadFileBytesAsync("missing.txt")); break;
            case "implicit-sync": Assert.Null(files.ReadFileBytesSync("missing.txt")); break;
            case "explicit":
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync(CanonicalWritePurpose.PublicationReadQuiescence))
                    Assert.Null(await files.ReadFileBytesAsync(lease, "missing.txt"));
                break;
            default: throw new ArgumentOutOfRangeException(nameof(reader));
        }
    }

    /// <summary>
    /// Retains provisional source reuse after fresh adapter admission while final verification reads physically.
    /// </summary>
    /// <returns>
    /// Completion after zero functional source reads across the replacement and two explicit fresh fences.
    /// </returns>
    [Fact]
    public async Task ReplacementLeaseReusesDataAfterFreshAdmission()
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires genuine Linux lease admission.");
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            await operation.VerifyAsync(first);
        }

        fixture.Recording = true;
        await using var second = await files.AcquireCanonicalWriteLeaseAsync();
        var guards = fixture.CompletedOperations("MainAdmission.Validate.GuardCheck");
        var leaseValidations = fixture.CompletedOperations("Lease.Validate.Total");
        Assert.Equal("original", await files.ReadFileAsync(second, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        Assert.True(fixture.CompletedOperations("MainAdmission.Validate.GuardCheck") > guards);
        Assert.True(fixture.CompletedOperations("Lease.Validate.Total") > leaseValidations);
        Assert.Equal("original", await files.ReadFileAsync(second, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        await operation.VerifyAsync(second);
        Assert.Equal(1, fixture.PresentSources);
        await operation.CompleteAsync();
        Assert.Equal(2, fixture.PresentSources);
        output.WriteLine(JsonSerializer.Serialize(new { FunctionalSources = 0, FreshFenceSources = fixture.PresentSources }));
    }

    /// <summary>
    /// Allows only provisional old DATA after raw drift, then refuses verification, effect and owning success.
    /// </summary>
    /// <returns>
    /// Completion after the expected fence error is captured before physical disposal can mask assertions.
    /// </returns>
    [Fact]
    public async Task ReplacementLeaseRawDriftCannotAuthorizeEffectOrSuccess()
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires genuine Linux lease admission.");
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            await operation.VerifyAsync(first);
        }

        File.WriteAllText(fixture.PresentPath, "modified");
        fixture.Recording = true;
        await using var second = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Equal("original", await files.ReadFileAsync(second, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        await Assert.ThrowsAsync<InvalidDataException>(() => operation.VerifyAsync(second));
        Assert.Equal(1, fixture.PresentSources);
        await Assert.ThrowsAsync<InvalidOperationException>(() => files.WriteFileAtomicAsync(second, "effect.txt", "forbidden"));
        await Assert.ThrowsAsync<InvalidOperationException>(operation.CompleteAsync);
        Assert.False(File.Exists(Path.Combine(files.GameSessionPath, "effect.txt")));
        Assert.Equal("modified", File.ReadAllText(fixture.PresentPath));
    }

    /// <summary>
    /// Requires fresh generation bytes on the new adapter even when the parsed generation identity is unchanged.
    /// </summary>
    /// <returns>
    /// Completion after replacement binding refuses before returning cached file DATA.
    /// </returns>
    [Fact]
    public async Task ReplacementLeaseChecksGenerationBeforeReturningData()
    {
        Assert.True(OperatingSystem.IsLinux(), "This category requires genuine Linux lease admission.");
        using var fixture = new SourceDataOperationTests.SourceOperationFixture(output);
        var files = fixture.Files;
        using var operation = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(operation);
        await using (var first = await files.AcquireCanonicalWriteLeaseAsync())
        {
            files.GetOrCreateSessionGeneration(first);
            Assert.Equal("original", await files.ReadFileAsync(first, "present.txt"));
            await operation.VerifyAsync(first);
        }

        File.AppendAllText(files.SessionGenerationPath, "\n");
        fixture.Recording = true;
        await using var second = await files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => files.ReadFileAsync(second, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        await Assert.ThrowsAsync<InvalidOperationException>(operation.CompleteAsync);
    }
}
