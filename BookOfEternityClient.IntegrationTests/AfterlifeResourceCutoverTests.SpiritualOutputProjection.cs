using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    private const string ProjectionNarrativePath = "output/narrative_response.json";
    private const string ProjectionInterfacePath = "output/interface_updates.json";

    /// <summary>
    /// Projects retained raw output under the signed named intake without writing any file or allocating on memo reads.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualOutputProjection_ProjectsRetainedBytesAndMemoizesFallbackTime()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var narrative = Encoding.UTF8.GetBytes("""{"response":"Первая`nвторая"}""");
        var interfaceOutput = Encoding.UTF8.GetBytes("""{"dialogueOptions":["[INK_FEATHER_ACTION: LEARN_SKILL] Выбор`nдва"]}""");
        await context.WriteExactBytesAsync(ProjectionNarrativePath, narrative);
        await context.WriteExactBytesAsync(ProjectionInterfacePath, interfaceOutput);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var paths = context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal).ToArray();
        var before = new Dictionary<string, byte[]?>();
        foreach (var path in paths) before[path] = await context.FileSystem.ReadFileBytesAsync(lease, path);

        var captured = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(captured.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(captured.Capture);
        var first = await capture.ReadOriginalOutputProjectionAsync(lease);
        Assert.Empty(first.Issues);
        var projection = Assert.IsType<ValidationService.SpiritualOriginalOutputProjection>(first.Projection);
        Assert.Equal("Первая\nвторая", JsonNode.Parse(projection.Narrative!.Json)!["response"]!.GetValue<string>());
        var options = JsonNode.Parse(projection.Interface!.Json)!["dialogueOptions"]!.AsArray();
        Assert.Equal("Выбор\nдва", options[0]!["text"]!.GetValue<string>());
        Assert.NotNull(JsonNode.Parse(projection.Interface.Json)!["timestamp"]);
        var rows = capture.ReadAllocationJournal(lease).ToJsonString();
        Assert.Contains(JsonNode.Parse(rows)!.AsArray(), row =>
            row!["kind"]!.GetValue<string>() == "utc_time" &&
            row["coordinate"]!.GetValue<string>().Length != 0);

        var second = await capture.ReadOriginalOutputProjectionAsync(lease);
        Assert.Equal(projection, second.Projection);
        Assert.Equal(rows, capture.ReadAllocationJournal(lease).ToJsonString());
        Assert.Equal(paths, context.FileSystem.EnumerateFiles(lease, "*").Order(StringComparer.Ordinal));
        foreach (var pair in before)
            Assert.Equal(pair.Value, await context.FileSystem.ReadFileBytesAsync(lease, pair.Key));
    }

    /// <summary>
    /// Replays the same original output clock row under a fresh signed owner and exact live-input comparison.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualOutputProjection_ReplaysExactClockCauseAndExport()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactBytesAsync(ProjectionInterfacePath,
            Encoding.UTF8.GetBytes("""{"image_prompt":"Врата"}"""));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var initial = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(initial.Issues);
        using var first = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(initial.Capture);
        var original = (await first.ReadOriginalOutputProjectionAsync(lease)).Projection!;
        var rows = first.ReadAllocationJournal(lease).ToJsonString();
        var expectedCoordinate = ExpectedInterfaceClockCoordinate(first);
        var outputRow = Assert.Single(JsonNode.Parse(rows)!.AsArray(), row =>
            row!["coordinate"]!.GetValue<string>() == expectedCoordinate);
        Assert.Equal("utc_time", outputRow!["kind"]!.GetValue<string>());
        Assert.Equal("projections", outputRow["owner"]!.GetValue<string>());
        Assert.Equal(JsonNode.Parse(original.Interface!.Json)!["timestamp"]!.GetValue<string>(),
            outputRow["value"]!.GetValue<string>());
        first.Dispose();

        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        var replayed = await fresh.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows, true);
        AssertNoConflictFrameErrors(replayed.Issues);
        using var second = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(replayed.Capture);
        Assert.Equal(original, (await second.ReadOriginalOutputProjectionAsync(lease)).Projection);
        Assert.Equal(rows, second.ReadAllocationJournal(lease).ToJsonString());
    }

    /// <summary>
    /// Rejects missing, reordered or altered timestamp rows before attaching a fresh original owner.
    /// </summary>
    /// <param name="mutation">
    /// Independent malformed replay-row operation to apply to the valid retained stream.
    /// </param>
    [Theory]
    [InlineData("missing")]
    [InlineData("reordered")]
    [InlineData("wrong-kind")]
    [InlineData("wrong-coordinate")]
    [InlineData("malformed-utc")]
    public async Task OriginalSpiritualOutputProjection_RejectsAlteredClockRows(string mutation)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await WriteOriginalIntakeDraftAsync(context);
        await context.WriteExactBytesAsync(ProjectionInterfacePath,
            Encoding.UTF8.GetBytes("""{"dialogueOptions":null}"""));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var coordinate = ExpectedInterfaceClockCoordinate(capture);
        var rows = capture.ReadAllocationJournal(lease).DeepClone().AsArray();
        capture.Dispose();
        var index = Enumerable.Range(0, rows.Count).Single(i =>
            rows[i]!["coordinate"]!.GetValue<string>() == coordinate);
        switch (mutation)
        {
            case "missing":
                rows.RemoveAt(index);
                break;
            case "reordered":
                Assert.True(index > 0);
                var previous = rows[index - 1]!.DeepClone();
                var output = rows[index]!.DeepClone();
                rows[index - 1] = output;
                rows[index] = previous;
                rows[index - 1]!["ordinal"] = index - 1;
                rows[index]!["ordinal"] = index;
                break;
            case "wrong-kind":
                rows[index]!["kind"] = "effect";
                rows[index]!["value"] = "effect_0123456789abcdef0123456789abcdef";
                break;
            case "wrong-coordinate":
                rows[index]!["coordinate"] = "wrong-original-output-cause";
                break;
            case "malformed-utc":
                rows[index]!["value"] = "not-a-utc-time";
                break;
        }
        var fresh = new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance);
        if (mutation == "malformed-utc")
            await Assert.ThrowsAsync<FormatException>(() =>
                fresh.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows.ToJsonString(), true));
        else
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                fresh.CaptureSpiritualOriginalTurnWithIntakeAsync(lease, rows.ToJsonString(), true));
        Assert.False(EffectAcceptedTurnPlanAuthority.TryPeekValidated(context.FileSystem, lease, out _, out _));
    }

    /// <summary>
    /// Leaves existing, malformed and absent interface images unchanged without an output timestamp request.
    /// </summary>
    /// <param name="json">
    /// Exact present interface text, or <see langword="null"/> for an absent file.
    /// </param>
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("{bad")]
    [InlineData("{\"image_prompt\":null,\"timestamp\":\"2026-01-01T00:00:00Z\"}")]
    [InlineData("{\"dialogueOptions\":null,\"timestamp\":42}")]
    public async Task OriginalSpiritualOutputProjection_NoFallbackDoesNotRequestOutputTime(string? json)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (json is not null)
            await context.WriteExactBytesAsync(ProjectionInterfacePath, Encoding.UTF8.GetBytes(json));
        if (json == string.Empty)
            await context.WriteExactBytesAsync(ProjectionNarrativePath, Array.Empty<byte>());
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);

        var projection = (await capture.ReadOriginalOutputProjectionAsync(lease)).Projection!;

        if (json == string.Empty)
        {
            Assert.NotNull(projection.Narrative);
            Assert.False(projection.Narrative.Changed);
            Assert.Equal(string.Empty, projection.Narrative.Json);
        }
        else Assert.Null(projection.Narrative);
        if (json is null) Assert.Null(projection.Interface);
        else
        {
            Assert.NotNull(projection.Interface);
            Assert.False(projection.Interface.Changed);
            Assert.Equal(json, projection.Interface.Json);
        }
        var outputCoordinate = ExpectedInterfaceClockCoordinate(capture);
        Assert.DoesNotContain(capture.ReadAllocationJournal(lease), row =>
            row!["coordinate"]!.GetValue<string>() == outputCoordinate);
    }

    /// <summary>
    /// Revokes retained output when present bytes change or a previously absent interface file appears.
    /// </summary>
    /// <param name="wasPresent">
    /// Whether the interface file existed in the signed original capture.
    /// </param>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OriginalSpiritualOutputProjection_RejectsChangedOrNewOutput(bool wasPresent)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        if (wasPresent)
            await context.WriteExactBytesAsync(ProjectionInterfacePath,
                Encoding.UTF8.GetBytes("""{"image_prompt":"Старая сцена","timestamp":"2026-01-01T00:00:00Z"}"""));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var original = (await capture.ReadOriginalOutputProjectionAsync(lease)).Projection!;
        Assert.Equal(wasPresent, original.Interface is not null);
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionInterfacePath,
            Encoding.UTF8.GetBytes("""{"image_prompt":"Новая сцена"}"""));

        var result = await capture.ReadOriginalOutputProjectionAsync(lease);

        Assert.Null(result.Projection);
        Assert.Contains(result.Issues, issue => issue.Code == "spiritual_original_input_changed" &&
            issue.FilePath == ProjectionInterfacePath);
        Assert.False(capture.IsCurrentOwner);
    }

    /// <summary>
    /// Preserves UTF-8 and UTF-16 byte-order marks while decoding both projected output files.
    /// </summary>
    /// <param name="utf16">
    /// Uses UTF-16 with its byte-order mark when <see langword="true"/>; otherwise uses UTF-8 with its mark.
    /// </param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OriginalSpiritualOutputProjection_DecodesBomWithoutChangingRawBytes(bool utf16)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        var encoding = utf16 ? Encoding.Unicode : new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);
        var narrative = encoding.GetPreamble().Concat(encoding.GetBytes("""{"response":"Тихо`nснова"}""")).ToArray();
        var interfaceOutput = encoding.GetPreamble().Concat(encoding.GetBytes(
            """{"image_prompt":"Луна","timestamp":"2026-01-01T00:00:00Z"}""")).ToArray();
        await context.WriteExactBytesAsync(ProjectionNarrativePath, narrative);
        await context.WriteExactBytesAsync(ProjectionInterfacePath, interfaceOutput);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();

        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var projection = (await capture.ReadOriginalOutputProjectionAsync(lease)).Projection!;
        Assert.Equal("Тихо\nснова", JsonNode.Parse(projection.Narrative!.Json)!["response"]!.GetValue<string>());
        Assert.Equal("Луна", JsonNode.Parse(projection.Interface!.Json)!["image_prompt"]!.GetValue<string>());
        Assert.Equal(narrative, await context.FileSystem.ReadFileBytesAsync(lease, ProjectionNarrativePath));
        Assert.Equal(interfaceOutput, await context.FileSystem.ReadFileBytesAsync(lease, ProjectionInterfacePath));
    }

    /// <summary>
    /// Keeps a legacy capture without the named projection and rejects the accessor after disposal.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualOutputProjection_RequiresNamedLiveCaptureAndLease()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var legacy = await ValidationService.SpiritualOriginalTurnCapture.CaptureAsync(context.Validator, lease);
        AssertNoConflictFrameErrors(legacy.Issues);
        using var legacyCapture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(legacy.Capture);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            legacyCapture.ReadOriginalOutputProjectionAsync(lease));
        var named = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(named.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(named.Capture);
        Assert.Null((await capture.ReadOriginalOutputProjectionAsync(lease)).Projection!.Interface);
        capture.Dispose();
        await Assert.ThrowsAsync<ObjectDisposedException>(() => capture.ReadOriginalOutputProjectionAsync(lease));
    }

    /// <summary>
    /// Rejects a lease from another filesystem and an expired real lease before serving the memo.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualOutputProjection_RejectsWrongAndExpiredLease()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        var otherRoot = Path.Combine(Path.GetTempPath(), "boe-output-projection-lease-" + Guid.NewGuid().ToString("N"));
        try
        {
            var other = new FileSystemManager(otherRoot, NullLogger<FileSystemManager>.Instance);
            other.EnsureDirectoryStructure();
            await using var wrongLease = await other.AcquireCanonicalWriteLeaseAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                capture.ReadOriginalOutputProjectionAsync(wrongLease));
            Assert.NotNull((await capture.ReadOriginalOutputProjectionAsync(lease)).Projection);
            await lease.DisposeAsync();
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                capture.ReadOriginalOutputProjectionAsync(lease));
        }
        finally
        {
            if (Directory.Exists(otherRoot)) Directory.Delete(otherRoot, recursive: true);
        }
    }

    /// <summary>
    /// Rejects a memo return when its capture is revoked during an awaited physical freshness read.
    /// </summary>
    [Fact]
    public async Task OriginalSpiritualOutputProjection_RevocationDuringReadCannotReturnMemo()
    {
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var armed = false;
        var hooks = new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = async path =>
            {
                if (!armed || path != ProjectionInterfacePath) return;
                entered.TrySetResult();
                await release.Task;
            }
        };
        await using var context = await CreateCompleteConflictFrameContextAsync(hooks,
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        await context.WriteExactBytesAsync(ProjectionInterfacePath,
            Encoding.UTF8.GetBytes("""{"image_prompt":"Луна","timestamp":"2026-01-01T00:00:00Z"}"""));
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var recorded = await context.Validator.CaptureSpiritualOriginalTurnWithIntakeAsync(lease);
        AssertNoConflictFrameErrors(recorded.Issues);
        using var capture = Assert.IsType<ValidationService.SpiritualOriginalTurnCapture>(recorded.Capture);
        armed = true;
        var pendingRead = capture.ReadOriginalOutputProjectionAsync(lease);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(30));
            capture.Dispose();
        }
        finally { release.TrySetResult(); }

        await Assert.ThrowsAsync<ObjectDisposedException>(() => pendingRead);
    }

    /// <summary>
    /// Keeps unchanged and malformed ordinary output bytes and write times, while changed writes equal the shared projection.
    /// </summary>
    [Fact]
    public async Task OrdinaryOutputProjection_WritesOnlyChangedSharedImages()
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(
            seedOriginalInputs: SeedOriginalIntakeBaselinesAsync);
        await WriteCompleteConflictFrameExchangeAsync(context);
        const string unchangedNarrative = """{"response":"Тихая сцена","timestamp":"2026-01-01T00:00:00Z"}""";
        const string malformedInterface = "{broken";
        await context.WriteExactBytesAsync(ProjectionNarrativePath, Encoding.UTF8.GetBytes(unchangedNarrative));
        await context.WriteExactBytesAsync(ProjectionInterfacePath, Encoding.UTF8.GetBytes(malformedInterface));
        var narrativePhysical = Path.Combine(context.FileSystem.GameSessionPath, ProjectionNarrativePath);
        var interfacePhysical = Path.Combine(context.FileSystem.GameSessionPath, ProjectionInterfacePath);
        var oldTime = new DateTime(2020, 1, 2, 3, 4, 5, DateTimeKind.Utc);
        File.SetLastWriteTimeUtc(narrativePhysical, oldTime);
        File.SetLastWriteTimeUtc(interfacePhysical, oldTime);

        await context.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync();

        Assert.Equal(Encoding.UTF8.GetBytes(unchangedNarrative), await File.ReadAllBytesAsync(narrativePhysical));
        Assert.Equal(Encoding.UTF8.GetBytes(malformedInterface), await File.ReadAllBytesAsync(interfacePhysical));
        Assert.Equal(oldTime, File.GetLastWriteTimeUtc(narrativePhysical));
        Assert.Equal(oldTime, File.GetLastWriteTimeUtc(interfacePhysical));

        const string changedNarrative = """{"response":"Первая`nвторая","timestamp":"2026-01-01T00:00:00Z"}""";
        const string changedInterface = """{"dialogueOptions":["Выбор`nдва"],"timestamp":"2026-01-01T00:00:00Z"}""";
        await context.WriteExactBytesAsync(ProjectionNarrativePath, Encoding.UTF8.GetBytes(changedNarrative));
        await context.WriteExactBytesAsync(ProjectionInterfacePath, Encoding.UTF8.GetBytes(changedInterface));
        await context.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync();
        Assert.Equal(AcceptedTurnOutputProjector.ProjectNarrative(changedNarrative).Json,
            await context.FileSystem.ReadFileAsync(ProjectionNarrativePath));
        Assert.Equal(AcceptedTurnOutputProjector.ProjectInterface(changedInterface, null).Json,
            await context.FileSystem.ReadFileAsync(ProjectionInterfacePath));
    }

    /// <summary>
    /// Preserves stale-output warnings before normalization and the existing invalid timestamp error.
    /// </summary>
    /// <param name="timestampJson">
    /// Raw timestamp JSON value to validate after the stale comparison.
    /// </param>
    /// <param name="expectedCode">
    /// Existing validator code for the selected timestamp error.
    /// </param>
    [Theory]
    [InlineData("42", "interface_updates_missing_timestamp")]
    [InlineData("\"not-a-time\"", "interface_updates_invalid_timestamp")]
    public async Task OrdinaryOutputProjection_PreservesStaleWarningAndTimestampSeverity(
        string timestampJson, string expectedCode)
    {
        await using var context = await CreateCompleteConflictFrameContextAsync(seedOriginalInputs: async frame =>
        {
            await SeedOriginalIntakeBaselinesAsync(frame);
            await frame.WriteExactBytesAsync(ProjectionNarrativePath,
                Encoding.UTF8.GetBytes("""{"response":"Тихо`nснова","timestamp":"2026-01-01T00:00:00Z"}"""));
            await frame.WriteExactBytesAsync(ProjectionInterfacePath,
                Encoding.UTF8.GetBytes("{\"dialogueOptions\":[\"Осмотреть дверь\"],\"timestamp\":" +
                    timestampJson + "}"));
        });
        await WriteCompleteConflictFrameExchangeAsync(context);

        var narrativeIssues = await context.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        var interfaceIssues = await context.Validator.ValidateAcceptedTurnInterfacePayloadAsync();

        Assert.Contains(narrativeIssues, issue => issue.Code == "accepted_turn_stale_narrative_response" &&
            issue.Severity == IssueSeverity.Warning);
        Assert.Contains(interfaceIssues, issue => issue.Code == "accepted_turn_stale_interface_updates" &&
            issue.Severity == IssueSeverity.Warning);
        Assert.Contains(interfaceIssues, issue => issue.Code == expectedCode &&
            issue.Severity == IssueSeverity.Error);
    }

    /// <summary>
    /// Derives the expected typed timestamp coordinate from the retained original identity and exact input image.
    /// </summary>
    /// <param name="capture">
    /// Signed original capture whose detached draft image supplies the causal fingerprint.
    /// </param>
    /// <returns>
    /// The role and cause-specific journal coordinate for the interface fallback timestamp.
    /// </returns>
    private static string ExpectedInterfaceClockCoordinate(ValidationService.SpiritualOriginalTurnCapture capture)
    {
        var draft = Assert.IsType<SpiritualOriginalDraftInputs>(OriginalCaptureField(capture, "_draftInputs"));
        var evidence = new JsonObject
        {
            ["sessionId"] = draft.SessionId,
            ["requestId"] = draft.RequestId,
            ["snapshotToken"] = draft.SnapshotToken,
            ["turn"] = draft.Turn,
            ["path"] = ProjectionInterfacePath,
            ["originalImageFingerprint"] = draft.ReadImage(ProjectionInterfacePath).Fingerprint
        };
        return SpiritualWoundStateJson.Hash(new JsonObject
        {
            ["role"] = AcceptedTurnProjectionTimeKind.InterfaceOutputTimestamp.ToString(),
            ["evidence"] = evidence
        }, "projection_clock");
    }
}
