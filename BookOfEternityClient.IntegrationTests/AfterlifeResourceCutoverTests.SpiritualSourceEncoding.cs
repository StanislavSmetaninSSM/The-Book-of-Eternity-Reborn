using System.Text;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Admits canonical UTF-8 preambles without changing the signed originals or physical candidate bytes.
    /// </summary>
    /// <param name="originalHasPreamble">
    /// Whether only the signed original has a preamble; otherwise only the current candidate has one.
    /// </param>
    /// <returns>
    /// A task completing after source admission and exact whole-session preservation checks.
    /// </returns>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SourceOwner_Utf8PreamblePreservesSignedAndCurrentImages(bool originalHasPreamble)
    {
        string[] paths = [AfterlifeEntityProfileState.StatePath, ValidationService.SpiritualWoundSourceSession.SoulPath];
        await using var context = await CreateCompleteConflictFrameContextAsync(
            captureOriginalSnapshot: async original =>
            {
                if (originalHasPreamble)
                    foreach (var path in paths)
                        await original.FileSystem.WriteFileAtomicAsync(path,
                            (await original.FileSystem.ReadFileAsync(path))!);
                await original.CaptureValidatedPendingSnapshotAsync(
                    turn: 42, currentRealm: "Chaos Sea", preGeneratedDices1d20: [15, 5, 12, 8]);
            });
        foreach (var path in paths)
        {
            var json = (await context.FileSystem.ReadFileAsync(path))!;
            if (originalHasPreamble)
                await context.WriteExactJsonAsync(path, json);
            else
                await context.FileSystem.WriteFileAtomicAsync(path, json);
        }
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var signed = PendingTurnSnapshotReader.ReadCurrent(context.FileSystem, lease, paths);
        Assert.True(signed.Success, string.Join(Environment.NewLine, signed.Issues));
        foreach (var path in paths)
        {
            var originalBytes = signed.Snapshot!.ReadRequiredBytes(path);
            var currentBytes = (await context.FileSystem.ReadFileBytesAsync(lease, path))!;
            Assert.Equal(originalHasPreamble, originalBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            Assert.Equal(!originalHasPreamble, currentBytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        }
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        Assert.True(prepared.Issues.Count == 0, string.Join(Environment.NewLine,
            prepared.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        var session = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        var source = Assert.Single(session.Sources);
        Assert.True(session.Owns(source));
        Assert.Equal("guardian:guardian_frame", source.AffectedActor);
        Assert.Equal(10L, source.Calculation.Input.HarmfulMargin);
        Assert.Equal(new[] { 0, 1 }, session.ClaimedDice);
    }

    /// <summary>
    /// Rejects malformed UTF-8 inside an otherwise valid JSON string instead of replacing its invalid byte.
    /// </summary>
    /// <returns>
    /// A task completing after the failed source admission leaves all signed and current files unchanged.
    /// </returns>
    [Fact]
    public async Task SourceOwner_Utf8PreambleDoesNotPermitMalformedCurrentEncoding()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        var json = (await context.FileSystem.ReadFileAsync(AfterlifeEntityProfileState.StatePath))!.TrimEnd();
        byte[] invalid = [.. Encoding.UTF8.GetPreamble(),
            .. Encoding.UTF8.GetBytes(json[..^1] + ",\"_encodingProbe\":\""),
            0xff, .. Encoding.UTF8.GetBytes("\"}")];
        await context.WriteExactBytesAsync(AfterlifeEntityProfileState.StatePath, invalid);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);

        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        Assert.Null(prepared.Session);
        Assert.Contains(prepared.Issues, issue => issue.Code == "spiritual_source_input_invalid");
    }

    /// <summary>
    /// Continues the same owned source after a canonical writer adds a preamble to unchanged conflict JSON.
    /// </summary>
    /// <returns>
    /// A task completing after continuation preserves the source reference, signed origin and physical bytes.
    /// </returns>
    [Fact]
    public async Task SourceOwner_Utf8PreambleContinuationRetainsOwnedSourceAndExactImages()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync();
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var prepared = await context.Validator.BeginSpiritualWoundSourceSessionAsync(lease);
        Assert.Empty(prepared.Issues);
        var owner = Assert.IsType<ValidationService.SpiritualWoundSourceSession>(prepared.Session);
        var source = Assert.Single(owner.Sources);
        var path = AfterlifeSpiritualConflictState.StatePath;
        var json = (await context.FileSystem.ReadFileAsync(lease, path))!;
        await context.FileSystem.WriteFileAtomicAsync(lease, path, json);
        Assert.True((await context.FileSystem.ReadFileBytesAsync(lease, path))!
            .AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
        var before = await ReadSpiritualContinuationTransportFilesAsync(context);

        var continued = await owner.ContinueAsync(lease);

        await AssertSpiritualContinuationTransportFilesUnchangedAsync(context, before);
        Assert.True(continued.Issues.Count == 0, string.Join(Environment.NewLine,
            continued.Issues.Select(issue => $"{issue.Code}: {issue.Actual}")));
        Assert.Same(owner, continued.Session);
        Assert.Same(source, Assert.Single(owner.Sources));
        Assert.True(owner.Owns(source));
        Assert.Equal(new[] { 0, 1 }, owner.ClaimedDice);
    }
}
