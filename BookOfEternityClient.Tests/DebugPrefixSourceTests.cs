using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Real-file prefix DATA controls; none constructs an engine owner or publication authority.</summary>
public sealed class DebugPrefixSourceTests(ITestOutputHelper output) : IDisposable
{
    private readonly OrdinaryReasoningFixture _fixture = new(output);
    private const string Debug = OrdinaryReasoningFixture.DebugPath;

    [Fact]
    public async Task OrdinaryFull_ChangedThenRestoredReadsRetainExactUnchangedIssueVector()
    {
        using var control = new OrdinaryReasoningFixture(output);
        await control.SeedValidAsync(); await control.PrepareOriginalAsync();
        var expected = await control.Validator.ValidateAcceptedTurnReasoningAsync();
        Assert.Equal("accepted_turn_stale_debug_logs", Assert.Single(expected).Code);
        await _fixture.SeedValidAsync();
        var originalPath = await _fixture.PrepareOriginalAsync();
        var a = File.ReadAllBytes(_fixture.Physical(Debug));
        var b = a.Concat(Encoding.UTF8.GetBytes("\n ")).ToArray();
        var reads = 0; var returns = 0; var restores = 0;
        var order = new List<object>();
        var validator = _fixture.CreateValidator(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (path == Debug)
                {
                    reads++; if (reads == 2) _fixture.WriteDebugRaw(b);
                    order.Add(new { Path = path, Read = reads, Raw = Convert.ToBase64String(File.ReadAllBytes(_fixture.Physical(Debug))) });
                }
                if (path == "game_state/meta/guardians.json" && returns == 2 && restores == 0)
                {
                    Assert.Equal(b, File.ReadAllBytes(_fixture.Physical(Debug)));
                    _fixture.WriteDebugRaw(a); restores++; order.Add(new { RestoreOn = path });
                }
                return Task.CompletedTask;
            },
            AfterCanonicalReadAttemptAsync = path =>
            {
                if (path == Debug)
                {
                    returns++; Assert.Equal(returns == 1 ? a : b, File.ReadAllBytes(_fixture.Physical(Debug)));
                }
                return Task.CompletedTask;
            }
        });
        _fixture.CaptureSeed();
        var issues = await validator.ValidateAcceptedTurnReasoningAsync();
        output.WriteLine(JsonSerializer.Serialize(new { kind = "DFR_ACTUAL_ORDINARY_A_B_A", reads, returns, restores, order,
            A = Convert.ToBase64String(a), B = Convert.ToBase64String(b), Issues = issues }));
        Assert.Equal(2, reads); Assert.Equal(2, returns); Assert.Equal(1, restores);
        Assert.Equal(JsonSerializer.Serialize(expected), JsonSerializer.Serialize(issues));
        Assert.Equal(a, File.ReadAllBytes(_fixture.Physical(originalPath)));
        _fixture.AssertSeedBytesUnchanged();
    }

    [Fact]
    public async Task ActualProducer_RechecksCopiesAndRetainsOneTimestampWithoutReevaluation()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Equal("NeedsActorDependencies", pass.Disposition);
        Assert.Equal("accepted_turn_stale_debug_logs", Assert.Single(pass.Issues).Code);
        var captured = pass.CaptureDiagnostic();
        Assert.Null(captured.Fault); Assert.Equal(1, captured.TimestampCalls);
        Assert.Equal(2, captured.Occurrences.Count(row => row.Path == Debug && row.State == "Present"));
        captured.Occurrences.First(row => row.Bytes is { Length: > 0 }).Bytes![0] ^= 1;
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        await pass.RequireCurrentAsync(_fixture.Files, lease);
        Assert.Equal(1, pass.CaptureDiagnostic().TimestampCalls);
        Assert.Equal(captured.Occurrences.Length, pass.CaptureDiagnostic().Occurrences.Length);
        Assert.True(lease.IsActive);
    }

    [Fact]
    public async Task ActualProducer_ChangedThenRestoredPhysicalReadsStayUnavailable()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var a = File.ReadAllBytes(_fixture.Physical(Debug));
        var b = a.Concat(Encoding.UTF8.GetBytes("\n ")).ToArray();
        var reads = 0;
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path =>
            {
                if (path == Debug && ++reads == 2) _fixture.WriteDebugRaw(b);
                return Task.CompletedTask;
            }
        });
        var validator = new ValidationService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Equal(2, reads);
        Assert.Equal("accepted_turn_stale_debug_logs", Assert.Single(pass.Issues).Code);
        _fixture.WriteDebugRaw(a);
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(files, lease));
        Assert.Contains("observed_source_changed", pass.CaptureDiagnostic().Fault);
    }

    [Fact]
    public async Task CurrentDriftRefusalRemainsLatchedAfterRestoration()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var a = File.ReadAllBytes(_fixture.Physical(Debug));
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        _fixture.WriteDebugRaw(a.Concat(Encoding.UTF8.GetBytes(" ")).ToArray());
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        _fixture.WriteDebugRaw(a);
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
    }

    [Theory]
    [InlineData("malformed")]
    [InlineData("empty-thoughts")]
    [InlineData("scope-error")]
    public async Task PolicyErrorsPreserveOrdinaryTerminalVersusContinue(string scenario)
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var raw = scenario == "malformed" ? "{" : OrdinaryReasoningFixture.Debug(scenario == "empty-thoughts" ? "" :
            OrdinaryReasoningFixture.ValidThoughts.Replace("Scene-local", "opaque", StringComparison.Ordinal));
        _fixture.WriteDebugRaw(Encoding.UTF8.GetBytes(raw));
        var ordinary = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Equal(JsonSerializer.Serialize(ordinary), JsonSerializer.Serialize(pass.Issues));
        Assert.Equal(scenario == "scope-error" ? "NeedsActorDependencies" : "TerminalPrefix", pass.Disposition);
        Assert.Null(pass.CaptureDiagnostic().Fault);
        if (scenario == "malformed") Assert.Contains(pass.CaptureDiagnostic().Occurrences, row => row.Route == "domain-catch" && (row.State == nameof(JsonException) || row.State == "JsonReaderException"));
        await pass.RequireCurrentAsync(_fixture.Files, lease);
    }

    [Fact]
    public async Task ActualReadJsonFaultEscapesAndDoesNotBecomeAParseDisposition()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var failure = new JsonException("DFR actual source leaf fault");
        var files = _fixture.CreateFiles(new FileSystemManagerHooks
        {
            BeforeCanonicalReadOpenAsync = path => path == Debug ? Task.FromException(failure) : Task.CompletedTask
        });
        var validator = new ValidationService(files, Microsoft.Extensions.Logging.Abstractions.NullLogger<ValidationService>.Instance);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        Assert.Same(failure, await Record.ExceptionAsync(() => validator.EvaluateObservedDebugPrefixAsync(lease)));
        Assert.True(lease.IsActive);
    }

    [Fact]
    public async Task CorruptOriginalRetainsPolicyNullIssuesButCannotCloseSource()
    {
        await _fixture.SeedValidAsync(); var original = await _fixture.PrepareOriginalAsync();
        File.AppendAllText(_fixture.Physical(original), " ", new UTF8Encoding(false));
        Assert.Null(await _fixture.Validator.ReadAcceptedTurnReasoningOriginalDebugAsync());
        var ordinary = await _fixture.Validator.ValidateAcceptedTurnReasoningAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Empty(ordinary); Assert.Empty(pass.Issues);
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuppliedOriginalDataRequiresFullMatchingContextJoin(bool altered)
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var real = JsonNode.Parse(Assert.IsType<string>(await _fixture.Files.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
        var supplied = real.DeepClone().AsObject();
        if (altered) supplied["playerAction"] = "Same tuple, changed supplied action DATA.";
        using var scope = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(supplied);
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, row => row.Route == "original-context" && row.State == "SuppliedPrevalidatedData");
        Assert.DoesNotContain(pass.CaptureDiagnostic().Occurrences, row => row.Route == "lookup-presence");
        if (altered) Assert.Throws<InvalidDataException>(() => pass.RequireOriginalContext(real));
        else { pass.RequireOriginalContext(real); await pass.RequireCurrentAsync(_fixture.Files, lease); }
    }

    [Fact]
    public async Task SuppliedUnjoinedDataCannotClose()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var real = JsonNode.Parse(Assert.IsType<string>(await _fixture.Files.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
        using var scope = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(real);
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.Contains("join_required", error.Message);
    }

    [Fact]
    public async Task ForeignFilesAndExpiredPassRefuseWithoutClosingBorrowedLease()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        var foreign = _fixture.CreateFiles(new FileSystemManagerHooks());
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(foreign, lease));
        pass.Dispose();
        await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.True(lease.IsActive);
    }

    [Fact]
    public async Task SameTupleManifestMutationAfterCaptureCannotJoinRetainedData()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        var manifest = JsonNode.Parse(Assert.IsType<string>(await _fixture.Files.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath)))!.AsObject();
        using var scope = _fixture.Validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        var anchoredData = _fixture.Validator.CaptureAcceptedTurnReasoningOriginalData(manifest);
        manifest["playerAction"] = "Changed after the actual source invocation, same tuple.";
        Assert.NotEqual(anchoredData, _fixture.Validator.CaptureAcceptedTurnReasoningOriginalData(manifest));
        Assert.Throws<InvalidDataException>(() => pass.RequireOriginalContext(manifest));
    }

    [Fact]
    public async Task ClosedBorrowedLeaseAndForeignValidatorRefuse()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        var foreign = _fixture.CreateValidator(new FileSystemManagerHooks());
        Assert.Throws<InvalidDataException>(() => pass.RequireIssuer(foreign));
        await lease.DisposeAsync();
        await Assert.ThrowsAsync<InvalidOperationException>(() => pass.RequireCurrentAsync(_fixture.Files, lease));
        Assert.False(lease.IsActive);
    }

    [Fact]
    public async Task GenuineUncoveredOptionalDebugPreservesNullableFreshnessWithoutInventingSignedAbsence()
    {
        await _fixture.SeedValidAsync();
        var current = File.ReadAllBytes(_fixture.Physical(Debug));
        File.Delete(_fixture.Physical(Debug));
        var prepared = await new LiveTurnPreparationService(_fixture.Files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "dfr-uncovered-session", RequestId = "dfr-" + Guid.NewGuid().ToString("N"),
            TurnNumber = 2, CurrentRealm = "Mortal World", PlayerAction = "Осмотреть пустую комнату.",
            PreGeneratedDices1d20 = new[] { 14, 8, 17 }
        });
        var manifest = JsonNode.Parse(Assert.IsType<string>(await _fixture.Files.ReadFileAsync(prepared.ManifestPath)))!.AsObject();
        Assert.Null(manifest["files"]?[Debug]);
        Assert.DoesNotContain(Debug, PendingTurnSnapshotPathPresenceV1.LogicalPaths);
        _fixture.WriteDebugRaw(current);
        Assert.Null(await _fixture.Validator.ReadAcceptedTurnReasoningOriginalDebugAsync());
        Assert.Empty(await _fixture.Validator.ValidateAcceptedTurnReasoningAsync());
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        Assert.Empty(pass.Issues);
        Assert.Contains(pass.CaptureDiagnostic().Occurrences, row => row.Route == "original" && row.State == "Uncovered");
        Assert.DoesNotContain(pass.CaptureDiagnostic().Occurrences, row => row.State == "ProvedAbsent");
        pass.RequireOriginalContext(manifest);
        await pass.RequireCurrentAsync(_fixture.Files, lease);
    }

    [Fact]
    public async Task GenuineGenerationReplacementCannotReuseEarlierBorrowedLeaseData()
    {
        await _fixture.SeedValidAsync(); await _fixture.PrepareOriginalAsync();
        await using var lease = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        using var pass = await _fixture.Validator.EvaluateObservedDebugPrefixAsync(lease);
        await lease.DisposeAsync();
        await using (var lifecycle = await _fixture.Files.AcquireSessionLifecycleLeaseAsync())
        await using (var replacement = await _fixture.Files.AcquireSessionReplacementWriteLeaseAsync(lifecycle))
        {
            var before = _fixture.Files.ReadExistingSessionGeneration(replacement);
            Assert.NotEqual(before, _fixture.Files.RotateSessionGeneration(replacement));
        }
        await using var next = await _fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var error = await Assert.ThrowsAsync<InvalidDataException>(() => pass.RequireCurrentAsync(_fixture.Files, next));
        Assert.Contains("foreign_files_or_lease", error.Message);
    }

    public void Dispose() => _fixture.Dispose();
}
