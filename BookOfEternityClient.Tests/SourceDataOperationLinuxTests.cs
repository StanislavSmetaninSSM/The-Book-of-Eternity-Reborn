using System.Text.Json;
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
        var handles = fixture.CompletedOperations("PhysicalFileAuthority.ValidateHandlePath");
        Assert.Equal("original", await files.ReadFileAsync(second, "present.txt"));
        Assert.Equal(0, fixture.PresentSources);
        Assert.True(fixture.CompletedOperations("MainAdmission.Validate.GuardCheck") > guards);
        Assert.True(fixture.CompletedOperations("PhysicalFileAuthority.ValidateHandlePath") > handles);
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
