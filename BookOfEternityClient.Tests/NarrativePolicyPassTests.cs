using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Cheap actual validator/file controls. A pass alone never grants engine publication authority.</summary>
public sealed class NarrativePolicyPassTests(ITestOutputHelper output) : IDisposable
{
    private const string Narrative = "output/narrative_response.json";
    private const string Achievements = "game_state/meta/achievements.json";
    private const string Interface = "output/interface_updates.json";
    private readonly OrdinaryOutputFixture _fixture = new(output);

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task UnchangedPass_RechecksWithoutAnotherProjectorOrWriteAndCopiesDiagnostics()
    {
        await _fixture.SeedValidAsync();
        var before = File.ReadAllBytes(_fixture.Physical(Narrative));
        var mtime = _fixture.SetOldTime(Narrative);
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        pass.RequireIssuer(_fixture.Validator);
        Assert.Empty(pass.Issues);
        var captured = pass.CaptureDiagnostic();
        Assert.Null(captured.Fault); Assert.True(captured.Completed);
        Assert.Equal(1, captured.ProjectorCalls); Assert.Equal(0, captured.WritesStarted);
        var exported = captured.Occurrences.First(value => value.Bytes is { Length: > 0 });
        exported.Bytes![0] ^= 1;
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        var after = pass.CaptureDiagnostic();
        Assert.Equal(captured.ProjectorCalls, after.ProjectorCalls);
        Assert.Equal(captured.Occurrences.Length, after.Occurrences.Length);
        Assert.Equal(0, after.WritesReturned);
        Assert.Equal(before, File.ReadAllBytes(_fixture.Physical(Narrative)));
        Assert.Equal(mtime, File.GetLastWriteTimeUtc(_fixture.Physical(Narrative)));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ActualNormalization_RetainsActualPreimagePublicationAndPublicOwningReturn()
    {
        await _fixture.SeedValidAsync();
        var before = SeedNormalization();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Empty(pass.Issues);
        var capture = pass.CaptureDiagnostic();
        Assert.Null(capture.Fault); Assert.True(capture.ProjectionChanged);
        Assert.Equal(1, capture.ProjectorCalls); Assert.Equal(1, capture.WritesStarted); Assert.Equal(1, capture.WritesReturned);
        Assert.True(string.Equals(Convert.ToHexString(SHA256.HashData(before)), capture.WriterPreimageSha256, StringComparison.OrdinalIgnoreCase));
        if (capture.WriterRoute == "Canonical") Assert.NotNull(capture.WriterPreimageIdentity);
        else { Assert.Equal("Trusted", capture.WriterRoute); Assert.StartsWith("Committed:", capture.WriterOutcome); }
        Assert.Equal("Один\nдва.", JsonDocument.Parse(FileSystemManager.DecodeFileBytes(File.ReadAllBytes(_fixture.Physical(Narrative)))).RootElement.GetProperty("response").GetString());
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Equal(1, pass.CaptureDiagnostic().ProjectorCalls);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ChangedDependency_RefusalStaysLatchedAfterBytesAreRestored()
    {
        await _fixture.SeedValidAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var before = File.ReadAllBytes(_fixture.Physical(Achievements));
        File.WriteAllBytes(_fixture.Physical(Achievements), before.Concat(Encoding.UTF8.GetBytes("\n ")).ToArray());
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var failure = await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.Contains(Achievements, failure.Message);
        File.WriteAllBytes(_fixture.Physical(Achievements), before);
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.Contains("source_changed", pass.CaptureDiagnostic().Fault);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task FollowupRead_RecordsBothPhysicalAbsencesWithoutInventingAnOriginal()
    {
        await _fixture.SeedValidAsync();
        File.Delete(_fixture.Physical(Interface));
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Equal(2, pass.CaptureDiagnostic().Occurrences.Count(value => value.Path == Interface && value.State == "Absent"));
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, value => value.Route == "original-lookup" && value.State == "Missing");
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Throws<InvalidDataException>(() => pass.RequireOriginalContext("invented", "request", "token", 1));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task FollowupObservedChange_RemainsUnavailableWhenFinalAbsenceMatchesFirstRead()
    {
        await _fixture.SeedValidAsync();
        File.Delete(_fixture.Physical(Interface));
        var calls = 0;
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            AfterCanonicalReadAttemptAsync = path =>
            {
                if (path == Interface && ++calls == 1) File.WriteAllText(_fixture.Physical(Interface), OrdinaryOutputFixture.ValidInterface);
                return Task.CompletedTask;
            }
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        File.Delete(_fixture.Physical(Interface));
        var capture = pass.CaptureDiagnostic();
        Assert.Contains(capture.Occurrences, value => value.Path == Interface && value.State == "Absent");
        Assert.Contains(capture.Occurrences, value => value.Path == Interface && value.State == "Present");
        Assert.Contains("observed_source_changed", capture.Fault);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(files, lease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task SwallowedAchievementOriginalFault_PreservesOrdinaryIssuesAndRefusesContribution()
    {
        await _fixture.SeedValidAsync();
        var manifest = await PrepareOriginalAsync();
        var originalPath = manifest.Files[Achievements];
        var faults = 0;
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (path != originalPath) return Task.CompletedTask;
                faults++;
                return Task.FromException(new IOException("actual achievement original read sentinel"));
            }
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var original = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        var ordinary = await validator.ValidateAcceptedTurnNarrativePayloadAsync();
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Equal(2, faults);
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(pass.Issues));
        Assert.Contains("actual achievement original read sentinel", pass.CaptureDiagnostic().Fault);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(files, lease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task GenuineSuppliedOriginal_RetainsContextAndSelectedPhysicalSources()
    {
        await _fixture.SeedValidAsync();
        var manifest = await PrepareOriginalAsync();
        using var scope = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        pass.RequireOriginalContext(manifest.SessionId, manifest.RequestId, manifest.ManifestPayloadHash, manifest.TurnNumber);
        var capture = pass.CaptureDiagnostic();
        Assert.Contains(capture.Occurrences, value => value.State == "SuppliedPrevalidatedData");
        Assert.DoesNotContain(capture.Occurrences, value => value.Path == LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        Assert.Contains(capture.Occurrences, value => value.Path == manifest.Files[Achievements] && value.State == "Present");
        Assert.Contains(capture.Occurrences, value => value.Path == PendingTurnSnapshotAuthority.AuthorityPath && value.State == "Present");
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Throws<InvalidDataException>(() => pass.RequireOriginalContext(manifest.SessionId, "foreign", manifest.ManifestPayloadHash, manifest.TurnNumber));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task GenuineUncoveredOriginal_PreservesNullablePolicyWithoutReadingInventedSnapshot()
    {
        await _fixture.SeedValidAsync();
        File.Delete(_fixture.Physical(Achievements));
        var manifest = await PrepareOriginalAsync();
        await _fixture.Files.WriteFileAtomicAsync(Achievements, "{}");
        using var scope = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, value => value.Role == ValidationService.NarrativeSourceRole.Achievements && (value.State is "Uncovered" or "ProvedAbsent"));
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ForeignIssuerAndManager_RefuseEvenWhenPhysicalRootMatches()
    {
        await _fixture.SeedValidAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var otherFiles = _fixture.CreateFiles(new FileSystemManagerHooks());
        Assert.Throws<InvalidDataException>(() => pass.RequireIssuer(new ValidationService(otherFiles, NullLogger<ValidationService>.Instance)));
        await using var lease = await otherFiles.AcquireCanonicalWriteLeaseAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(otherFiles, lease));
        Assert.Contains("foreign_files", error.Message);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ChangedGeneration_RefusesRetainedPass()
    {
        await _fixture.SeedValidAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        _fixture.Files.RotateSessionGeneration(lease);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.Contains("generation_changed", error.Message);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task Dispose_ReleasesBuffersAndRefusesUse()
    {
        await _fixture.SeedValidAsync();
        var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.NotEmpty(pass.CaptureDiagnostic().Occurrences);
        pass.Dispose(); pass.Dispose();
        Assert.True(pass.CaptureDiagnostic().Disposed); Assert.Empty(pass.CaptureDiagnostic().Occurrences);
        Assert.Empty(pass.Issues);
        Assert.Throws<InvalidDataException>(() => pass.RequireIssuer(_fixture.Validator));
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ReusedSourceData_IsNotRelabeledAsActualPhysicalClosure()
    {
        await _fixture.SeedValidAsync();
        using var data = _fixture.Files.BeginSourceDataOperationIfNeeded();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Contains("partial_source_provenance", pass.CaptureDiagnostic().Fault);
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task OverwrittenRealWriterPreimage_CannotBeBlessedByMatchingFinalImage()
    {
        await _fixture.SeedValidAsync();
        SeedNormalization();
        var replaced = false;
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            AfterCanonicalReadAttemptAsync = path =>
            {
                if (path == Narrative && !replaced)
                {
                    replaced = true;
                    File.WriteAllText(_fixture.Physical(Narrative), OrdinaryOutputFixture.ValidNarrative);
                }
                return Task.CompletedTask;
            }
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.True(replaced); Assert.Equal(1, pass.CaptureDiagnostic().WritesStarted);
        Assert.Equal(0, pass.CaptureDiagnostic().WritesReturned);
        Assert.Contains("preimage_changed", pass.CaptureDiagnostic().Fault);
        Assert.Equal("Один\nдва.", JsonDocument.Parse(FileSystemManager.DecodeFileBytes(File.ReadAllBytes(_fixture.Physical(Narrative)))).RootElement.GetProperty("response").GetString());
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(files, lease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ActualPublicOwningCloseFailure_NeverReturnsSuccessfulPassAfterCommittedNormalization()
    {
        await _fixture.SeedValidAsync();
        SeedNormalization();
        var failure = new IOException("actual N writer owning close sentinel");
        using var closer = new ThrowingClose(failure);
        var files = _fixture.CreateFiles(new FileSystemManagerHooks { OrdinaryWriteLeaseCloseContextFactory = () => closer });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var error = await Record.ExceptionAsync(async () =>
        {
            using var unexpected = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        });
        Assert.Same(failure, error); Assert.Equal(1, closer.Calls);
        Assert.Equal("Один\nдва.", JsonDocument.Parse(FileSystemManager.DecodeFileBytes(File.ReadAllBytes(_fixture.Physical(Narrative)))).RootElement.GetProperty("response").GetString());
        // The original lease disposal releases all handles even when its context throws.
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        Assert.True(lease.IsActive);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task ExpiredLease_RefusesAndLatchesTheActualOwnerFailure()
    {
        await _fixture.SeedValidAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await lease.DisposeAsync();
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.Contains("not active", failure.Message);
        Assert.Contains("not active", pass.CaptureDiagnostic().Fault);
        await using var freshLease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, freshLease));
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Fact]
    public async Task GenuineConditionalDelta_PreservesRepeatedActualCurrentAndOriginalReads()
    {
        await _fixture.SeedValidAsync();
        const string passive = "game_state/player/skills_passive.json";
        await _fixture.Files.WriteFileAtomicAsync(passive,
            """{"passiveSkillChanges":[{"skillId":"skill_seal","name":"Чтение печатей"}]}""");
        await _fixture.Files.WriteFileAtomicAsync("game_state/player/skill_mastery.json", """{"skillMasteryChanges":[]}""");
        var manifest = await PrepareOriginalAsync();
        await _fixture.Files.WriteFileAtomicAsync(Narrative,
            """{"response":"Твоя подготовка в чтении печатей помогает заметить одну неправильную деталь: свидетельская зарубка смещена на волосок.","timestamp":"2026-01-02T00:00:00Z"}""");
        using var original = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var capture = pass.CaptureDiagnostic();
        Assert.Null(capture.Fault);
        Assert.Equal(2, capture.Occurrences.Count(value => value.Path == passive && value.State == "Present"));
        Assert.Contains(capture.Occurrences, value => value.Path == manifest.Files[passive] && value.State == "Present");
        Assert.Contains(pass.Issues, issue => issue.Code == "accepted_turn_skill_claim_missing_state_delta");
        Assert.Equal(capture.Occurrences.Select(value => value.Ordinal), capture.Occurrences.Select(value => value.Ordinal).Order());
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Equal(capture.ProjectorCalls, pass.CaptureDiagnostic().ProjectorCalls);
    }

    /// <summary>Checks actual invocation source integrity through an independent ordinary file fixture.</summary>
    [Theory]
    [InlineData("Missing")]
    [InlineData("Matching")]
    [InlineData("WrongBefore")]
    [InlineData("Prepared")]
    public async Task InterfaceTransitionData_OnlyMatchesExactPublishedBeforeAndAfter(string mode)
    {
        await _fixture.SeedValidAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var before = File.ReadAllBytes(_fixture.Physical(Interface));
        var after = Encoding.UTF8.GetBytes("""{"dialogueOptions":[],"timestamp":"2026-01-01T00:00:00Z"}""");
        File.WriteAllBytes(_fixture.Physical(Interface), after);
        // This exercises only DATA matching. The engine must independently
        // authenticate the provided record and never accepts these unit DATA as an owner.
        var suppliedBefore = mode == "WrongBefore" ? before.Concat(new byte[] { 32 }).ToArray() : before;
        string Fingerprint(string text) => WoundAcceptedTurnFingerprintWriter.Compute([text]);
        var record = MortalJointInterfacePublication.CreatePrepared(new JsonObject
        {
            ["sessionGeneration"] = "generation", ["sessionId"] = "session", ["requestId"] = "request",
            ["snapshotToken"] = "snapshot", ["turnNumber"] = 17, ["realm"] = "mortal_world",
            ["stageOrdinal"] = 1, ["readyOrdinal"] = 2, ["iterationOrdinal"] = 3,
            ["checkpointFingerprint"] = Fingerprint("checkpoint"), ["operationKey"] = "operation",
            ["attemptId"] = "attempt", ["requestFingerprint"] = Fingerprint("request")
        }, new JsonObject { ["fixture"] = "DATA-only" }, Fingerprint("source"),
            new MortalJointInterfacePublication.ComputedResult(suppliedBefore, after, "Changed", false, null, "Object", "ParseAccepted", []));
        if (mode != "Prepared") record = record.AsPublished();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        if (mode == "Matching") await pass.RequireCurrentAsync(_fixture.Files, lease, record);
        else await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease, mode == "Missing" ? null : record));
        Assert.Equal(1, pass.CaptureDiagnostic().ProjectorCalls);
        Assert.Equal(0, pass.CaptureDiagnostic().WritesStarted);
    }

    /// <summary>Preserves best-effort literal parse policy without inventing physical source failures.</summary>
    [Theory]
    [InlineData("{", "JsonException")]
    [InlineData("[]", "Array")]
    public async Task LiteralAchievementParseDisposition_RetainsOriginalIssuesWithoutPhysicalFault(string json, string disposition)
    {
        await _fixture.SeedValidAsync();
        _fixture.WriteRaw(Achievements, Encoding.UTF8.GetBytes(json));
        var ordinary = await _fixture.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(pass.Issues));
        var captured = pass.CaptureDiagnostic();
        Assert.Null(captured.Fault); Assert.Equal(1, captured.TimestampCalls); Assert.Empty(captured.TimestampIssues);
        Assert.Contains(captured.Occurrences, value => value.Role == ValidationService.NarrativeSourceRole.Achievements && value.State == disposition);
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
    }

    /// <summary>Distinguishes a real injected read JsonException from literal malformed achievement JSON on that same source path.</summary>
    [Fact]
    public async Task ReadJsonException_RemainsPhysicalFaultAfterOriginalNarrativeDomainCatch()
    {
        await _fixture.SeedValidAsync();
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path => path == Achievements
                ? Task.FromException(new JsonException("actual source read JsonException sentinel")) : Task.CompletedTask
        });
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        var ordinary = await validator.ValidateAcceptedTurnNarrativePayloadAsync();
        using var pass = await validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(pass.Issues));
        Assert.Equal("accepted_turn_invalid_narrative_json", Assert.Single(pass.Issues).Code);
        var captured = pass.CaptureDiagnostic();
        Assert.Contains("actual source read JsonException sentinel", captured.Fault);
        Assert.Contains(captured.Occurrences, value => value.Path == Achievements && value.State == "SourceFault:JsonException");
        Assert.Contains(captured.Occurrences, value => value.Route == "narrative-domain-catch" && value.State == "DownstreamJsonException");
        Assert.Equal(1, captured.TimestampCalls); Assert.Empty(captured.TimestampIssues);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(files, lease));
    }

    /// <summary>Retains the original malformed narrative verdict while recording that no timestamp call was reached.</summary>
    [Fact]
    public async Task LiteralMalformedNarrative_RecordsParseDispositionAndZeroTimestampCalls()
    {
        await _fixture.SeedValidAsync();
        _fixture.WriteRaw(Narrative, Encoding.UTF8.GetBytes("{"));
        var ordinary = await _fixture.Validator.ValidateAcceptedTurnNarrativePayloadAsync();
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(pass.Issues));
        Assert.Equal("accepted_turn_invalid_narrative_json", Assert.Single(pass.Issues).Code);
        var captured = pass.CaptureDiagnostic();
        Assert.Null(captured.Fault); Assert.Equal(0, captured.TimestampCalls); Assert.Empty(captured.TimestampIssues);
        Assert.Contains(captured.Occurrences, value => value.Route == "narrative-domain-catch" && value.State == "InvalidJson");
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
    }

    /// <summary>Retains exactly the issue delta from the single original timestamp policy call.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task TimestampPolicy_RetainsItsActualSingleCallAndIssueDelta(bool invalid)
    {
        await _fixture.SeedValidAsync();
        var document = JsonNode.Parse(OrdinaryOutputFixture.ValidNarrative)!.AsObject();
        if (invalid) document["timestamp"] = "invalid timestamp"; else document.Remove("timestamp");
        _fixture.WriteRaw(Narrative, Encoding.UTF8.GetBytes(document.ToJsonString()));
        using var pass = await _fixture.Validator.ValidateAcceptedTurnNarrativePolicyPassAsync();
        var captured = pass.CaptureDiagnostic();
        Assert.Null(captured.Fault); Assert.Equal(1, captured.TimestampCalls);
        Assert.Equal(invalid ? "narrative_response_invalid_timestamp" : "narrative_response_missing_timestamp", Assert.Single(captured.TimestampIssues).Code);
        Assert.Equal(JsonSerializer.Serialize(pass.Issues), JsonSerializer.Serialize(captured.TimestampIssues));
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Equal(1, pass.CaptureDiagnostic().TimestampCalls);
    }

    /// <summary>Exercises actual declared protected sync/async leaves and a real lower-level read fault without an engine.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeclaredProtectedReader_RetainsActualBytesAndLatchesReadFault(bool synchronous)
    {
        await _fixture.SeedValidAsync();
        var fail = false;
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            AfterCanonicalReadInitialValidationAsync = path => fail && path == Narrative
                ? Task.FromException(new JsonException("actual protected leaf sentinel")) : Task.CompletedTask
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var source = new ValidationService.NarrativeInterfaceSourceSession(files);
        await files.RunLegacyStorageRecoveryAsync(lease, async () =>
        {
            var bytes = synchronous ? source.ReadBytesSync(Narrative, Narrative, "declared-protected", lease) :
                await source.ReadBytesAsync(Narrative, Narrative, "declared-protected", lease);
            Assert.Equal(File.ReadAllBytes(_fixture.Physical(Narrative)), bytes);
            Assert.Contains(source.CaptureDiagnostic().Occurrences, value => value.Path == Narrative && value.State == "Present");
            fail = true;
            if (synchronous) Assert.Throws<JsonException>(() => source.ReadBytesSync(Narrative, Narrative, "declared-protected", lease));
            else await Assert.ThrowsAsync<JsonException>(() => source.ReadBytesAsync(Narrative, Narrative, "declared-protected", lease));
            Assert.Contains(source.CaptureDiagnostic().Occurrences, value => value.Path == Narrative && value.State == "SourceFault:JsonException");
            Assert.Contains("actual protected leaf sentinel", source.CaptureDiagnostic().Fault);
        });
    }

    /// <summary>Proves an arbitrary close factory disables reserved and source DATA reuse before it can execute.</summary>
    [Fact]
    public async Task CloseDependencyFactory_UsesFreshReadFallbackWithoutInvokingFactoryDuringReads()
    {
        await _fixture.SeedValidAsync();
        var calls = 0;
        var reads = new CanonicalReadActivityAccumulator();
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            PassiveCanonicalReads = reads,
            OrdinaryWriteLeaseCloseContextFactory = () => { calls++; return null; }
        });
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        await using var refusedReadSet = files.TryBeginReservedValidationReadSet(lease);
        Assert.Null(refusedReadSet);
        using var data = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(data);
        var first = await files.ReadFileAsync(lease, Achievements);
        var second = await files.ReadFileAsync(lease, Achievements);
        Assert.Equal(first, second); Assert.Equal(2, reads.ReadCount(Achievements));
        Assert.Equal(0, calls);
        await data.CompleteAsync();
    }

    /// <summary>Authors valid source text requiring the existing ordinary newline normalization.</summary>
    private byte[] SeedNormalization()
    {
        var bytes = Encoding.UTF8.GetBytes("""{"response":"Один`nдва.","timestamp":"2026-01-01T00:00:00Z"}""");
        _fixture.WriteRaw(Narrative, bytes);
        return bytes;
    }

    /// <summary>Uses the real preparation service before authoring a fresh current narrative.</summary>
    private async Task<LiveTurnPendingSnapshotManifest> PrepareOriginalAsync()
    {
        await new LiveTurnPreparationService(_fixture.Files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "n-pass-session", RequestId = "n-pass-request", TurnNumber = 17,
            CurrentRealm = "Mortal World", PlayerAction = "Осмотреть реку."
        });
        var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
            File.ReadAllText(_fixture.Physical(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)),
            LiveTurnPreparationService.ManifestJsonOptions)!;
        await _fixture.Files.WriteFileAtomicAsync(Narrative,
            """{"response":"Новая тихая сцена у реки.","timestamp":"2026-01-02T00:00:00Z"}""");
        return manifest;
    }

    /// <summary>Throws once from the actual owning lease disposal dependency.</summary>
    /// <param name="failure">The original close error retained by the actual writer.</param>
    private sealed class ThrowingClose(IOException failure) : IDisposable
    {
        internal int Calls { get; private set; }
        /// <summary>Fails the first actual close; subsequent test cleanup is inert.</summary>
        public void Dispose() { if (++Calls == 1) throw failure; }
    }

    /// <summary>Removes only this test's independently owned ordinary file fixture.</summary>
    public void Dispose() => _fixture.Dispose();
}
