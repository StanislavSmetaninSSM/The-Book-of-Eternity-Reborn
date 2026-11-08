using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Proves private read-error transport with real small files, never joint publication authority.</summary>
/// <param name="output">Complete local diagnostic and owned-root cleanup receiver.</param>
public sealed class JointQteReadDiagnosticTests(ITestOutputHelper output)
{
    private const string QteMessage = "The fresh original/request no longer reproduces the joint QTE owner.";

    /// <summary>Retains the actual failed read boundary without changing its public refusal.</summary>
    /// <param name="input">Missing pending, malformed pending, or missing official original.</param>
    [Theory]
    [InlineData("missing")]
    [InlineData("malformed")]
    [InlineData("snapshot")]
    public async Task RealReadRefusalsKeepTheirOriginalBoundary(string input)
    {
        using var fixture = new Fixture(output);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        fixture.Files.GetOrCreateSessionGeneration(lease);
        if (input != "missing") await fixture.Files.WriteFileAtomicAsync(lease,
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath, input == "malformed" ? "{" : "{}");
        var read = await MortalJointRepairCheckpoint.ReconstructAsync(fixture.Files, lease, "session", "request", "snapshot", 44);
        Assert.False(read.IsValid);
        Assert.Null(read.Checkpoint);
        Assert.Null(read.Capture);
        var issue = Assert.Single(read.Issues);
        Assert.Equal("mortal_joint_checkpoint_" + (input == "missing" ? "pending_missing" : input == "malformed" ? "pending_invalid" : "input_invalid"), issue.Code);
        Assert.Equal("Client", issue.Actor);
        Assert.Equal(MortalWoundOpportunityAdapter.PrivateSourceIssueSection, issue.Section);
        var publicJson = JsonSerializer.Serialize(read);
        var failure = read.CreateFailure(QteMessage);
        Assert.Equal(QteMessage, failure.Message);
        Assert.Null(failure.InnerException);
        var rows = new List<string>();
        Assert.Same(failure, await Record.ExceptionAsync(() => JointQteDiagnosticCapture.RunAsync<int>(
            () => Task.FromException<int>(failure), rows.Add)));
        var diagnostic = Assert.IsType<MortalJointRepairReadDiagnostic>(MortalJointRepairReadDiagnostic.From(failure));
        Assert.Equal(input == "snapshot" ? "CaptureCoreAsync/ReadSnapshot" : "ReconstructAsync/" + (input == "missing" ? "pending_missing" : "pending_invalid"), diagnostic.Origin);
        Assert.Equal(JsonSerializer.Serialize(read.Issues), JsonSerializer.Serialize(diagnostic.Issues));
        Assert.Equal(publicJson, JsonSerializer.Serialize(read));
        if (input == "missing") Assert.Null(diagnostic.CaughtException);
        else
        {
            Assert.NotNull(diagnostic.CaughtException);
            Assert.NotEmpty(diagnostic.CaughtException.StackTrace!);
            Assert.Equal(issue.Message, diagnostic.CaughtException.Message);
            Assert.Contains(input == "snapshot" ? "ReadSnapshot" : "Root", diagnostic.CaughtException.StackTrace);
        }
        Assert.Contains(rows, row => row.Contains(diagnostic.Origin, StringComparison.Ordinal));
        foreach (var row in rows) output.WriteLine(row);
    }

    /// <summary>Preserves the real parser exception while leaving the serialized ReadResult unchanged.</summary>
    [Fact]
    public void ParseFailureRetainsCaughtExceptionAndOriginalIssue()
    {
        using var json = JsonDocument.Parse("{\"schemaVersion\":\"invalid\"}");
        var read = MortalJointRepairCheckpoint.Parse(json.RootElement);
        Assert.False(read.IsValid);
        var issue = Assert.Single(read.Issues);
        Assert.Equal("mortal_joint_checkpoint_shape", issue.Code);
        var diagnostic = Assert.IsType<MortalJointRepairReadDiagnostic>(read.Diagnostic);
        Assert.Equal("Parse/shape", diagnostic.Origin);
        Assert.IsType<InvalidOperationException>(diagnostic.CaughtException);
        Assert.Contains("Parse", diagnostic.CaughtException.StackTrace);
        Assert.Equal(issue.Message, diagnostic.CaughtException.Message);
        Assert.DoesNotContain("CaughtException", JsonSerializer.Serialize(read));
    }

    /// <summary>Preserves ordered complete issues and independently detached mutable repair DATA.</summary>
    [Fact]
    public async Task ForwardedIssuesKeepOrderFieldsAndDetachedRepairData()
    {
        var sourcePath = new[] { "source" };
        var destinationPath = new[] { "destination" };
        var first = new ValidationIssue("private/first", IssueSeverity.Error, "first message", "first_code",
            "Client", "first section", "expected first", "actual first", "repair first", IssueCategory.StateConsistency,
            ["repair/a", "repair/b"])
        {
            MortalItemRepairContext = new("coordinate", "consume", "player_inventory",
                new("player_inventory", "player", "source_container", sourcePath),
                new("player_inventory", "player", "destination_container", destinationPath),
                "item expected", "item actual", ["item companion"]),
            EffectRepairContext = new("Client", "route", "raw", new JsonObject { ["value"] = 1 },
                new JsonObject { ["target"] = 2 }, "definition", new JsonObject { ["event"] = 3 }, "value")
        };
        var second = new ValidationIssue("private/second", IssueSeverity.Warning, "second message", "second_code");
        var upstream = new[] { first, second };
        var mapped = new ValidationIssue("private/mapped", IssueSeverity.Error, "original refusal", "mapped_code");
        var read = new MortalJointRepairCheckpoint.ReadResult(null, [mapped]);
        var unchanged = JsonSerializer.Serialize(read);
        var printed = read.ToString();
        Assert.Same(read, read.WithDiagnostics("CaptureCoreAsync/ComposeRejectedQteResponse", upstream));
        var diagnostic = Assert.IsType<MortalJointRepairReadDiagnostic>(read.Diagnostic);
        Assert.Same(mapped, Assert.Single(read.Issues));
        Assert.Equal(unchanged, JsonSerializer.Serialize(read));
        Assert.Equal(printed, read.ToString());
        Assert.Equal(new[] { "first_code", "second_code" }, diagnostic.SourceIssues.Select(issue => issue.Code));
        Assert.Equal(JsonSerializer.Serialize(upstream), JsonSerializer.Serialize(diagnostic.SourceIssues));
        Assert.Equal(first.RepairTargetFiles, diagnostic.SourceIssues[0].RepairTargetFiles);
        var lines = new List<string>();
        var failure = read.CreateFailure(QteMessage);
        Assert.Same(failure, await Record.ExceptionAsync(() => JointQteDiagnosticCapture.RunAsync<int>(
            () => Task.FromException<int>(failure), lines.Add)));
        using var captured = JsonDocument.Parse(Assert.Single(lines));
        var rendered = captured.RootElement;
        Assert.Equal("JointQteReadDiagnostic", rendered.GetProperty("Kind").GetString());
        Assert.Equal(diagnostic.Origin, rendered.GetProperty("Origin").GetString());
        var sourceRows = rendered.GetProperty("SourceIssues");
        Assert.Equal(new[] { "first_code", "second_code" }, sourceRows.EnumerateArray().Select(row => row.GetProperty("Code").GetString()));
        for (var index = 0; index < upstream.Length; index++)
            foreach (var property in JsonSerializer.SerializeToElement(upstream[index]).EnumerateObject())
                Assert.Equal(property.Value.GetRawText(), sourceRows[index].GetProperty(property.Name).GetRawText());
        Assert.Equal(JsonSerializer.Serialize(first.RepairTargetFiles), sourceRows[0].GetProperty("RepairTargetFiles").GetRawText());
        Assert.Equal(JsonSerializer.Serialize(first.MortalItemRepairContext), sourceRows[0].GetProperty("MortalItemRepairContext").GetRawText());
        Assert.Equal(JsonSerializer.Serialize(first.EffectRepairContext), sourceRows[0].GetProperty("EffectRepairContext").GetRawText());
        Assert.Equal("mapped_code", rendered.GetProperty("Issues")[0].GetProperty("Code").GetString());
        foreach (var line in lines) output.WriteLine(line);
        sourcePath[0] = "changed original source";
        destinationPath[0] = "changed original destination";
        var exportedItem = diagnostic.SourceIssues[0].MortalItemRepairContext!;
        Assert.Equal("source", Assert.Single(exportedItem.SourceCarrier!.ContainerPath));
        Assert.Equal("destination", Assert.Single(exportedItem.DestinationCarrier!.ContainerPath));
        Assert.IsType<string[]>(exportedItem.SourceCarrier.ContainerPath)[0] = "changed returned source";
        Assert.IsType<string[]>(exportedItem.DestinationCarrier.ContainerPath)[0] = "changed returned destination";
        Assert.Equal("source", Assert.Single(diagnostic.SourceIssues[0].MortalItemRepairContext!.SourceCarrier!.ContainerPath));
        Assert.Equal("destination", Assert.Single(diagnostic.SourceIssues[0].MortalItemRepairContext!.DestinationCarrier!.ContainerPath));
        first.EffectRepairContext.ExpectedSource["value"] = 9;
        diagnostic.SourceIssues[0].EffectRepairContext!.ExpectedSource["value"] = 8;
        Assert.Equal(1, diagnostic.SourceIssues[0].EffectRepairContext!.ExpectedSource["value"]!.GetValue<int>());
        Assert.Equal(9, first.EffectRepairContext.ExpectedSource["value"]!.GetValue<int>());
        Assert.Null(diagnostic.CaughtException);
        Assert.Null(diagnostic.CaptureFailure);
    }

    /// <summary>Exercises the actual DATA failure, source retirement, common aggregate and fixture capture chain.</summary>
    [Fact]
    public async Task ActualQteFailureRetainsReadDiagnosticThroughCommonAggregate()
    {
        using var fixture = new Fixture(output);
        var files = fixture.Files;
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        files.GetOrCreateSessionGeneration(lease);
        await files.WriteFileAtomicAsync(lease, WoundAcceptedTurnSnapshotContract.PendingResolutionPath, "{");
        using var source = files.BeginSourceDataOperationIfNeeded();
        Assert.NotNull(source);
        using var recovery = files.BeginRecoveryValidation(lease);
        var rows = new List<string>();
        Exception? primary = null;
        Exception? secondary = null;
        var observed = await Record.ExceptionAsync(() => JointQteDiagnosticCapture.RunAsync(async () =>
        {
            try
            {
                await JointQteContributionData.RecomputeAsync(files, lease, new object(), () => null, () => false,
                    () => Task.CompletedTask, async () =>
                    {
                        var read = await MortalJointRepairCheckpoint.ReconstructAsync(files, lease, "session", "request", "snapshot", 44);
                        throw read.CreateFailure(QteMessage);
                    });
                throw new Xunit.Sdk.XunitException("Invalid real pending unexpectedly produced QTE DATA.");
            }
            catch (InvalidDataException error)
            {
                primary = error;
                return WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(error, () =>
                {
                    try { files.GetCurrentSemanticDataOperation(); }
                    catch (Exception abort) { secondary = abort; throw; }
                });
            }
        }, rows.Add));
        var aggregate = Assert.IsType<AggregateException>(observed);
        Assert.Same(primary, aggregate.InnerExceptions[0]);
        Assert.Same(secondary, aggregate.InnerExceptions[1]);
        Assert.IsType<InvalidOperationException>(secondary);
        Assert.Equal("The source operation is no longer available.", secondary!.Message);
        Assert.Equal(0, recovery.CaptureState().Entries);
        Assert.Throws<InvalidOperationException>(() => source.GetSemanticDataOperation());
        Assert.True(lease.IsActive);
        var diagnostic = Assert.IsType<MortalJointRepairReadDiagnostic>(MortalJointRepairReadDiagnostic.From(primary!));
        Assert.Equal("ReconstructAsync/pending_invalid", diagnostic.Origin);
        Assert.NotNull(diagnostic.CaughtException);
        Assert.Contains(rows, row => row.Contains("mortal_joint_checkpoint_pending_invalid", StringComparison.Ordinal));
        Assert.Contains(rows, row => row.Contains("CaughtException", StringComparison.Ordinal));
        foreach (var row in rows) output.WriteLine(row);
    }

    /// <summary>Prevents diagnostic owning errors from joining the ordinary exception-routing graph.</summary>
    [Fact]
    public void DiagnosticDoesNotAlterMessageExceptionGraphOrAutomaticSerialization()
    {
        Exception caught;
        try { throw new SessionReplacedException("private owning error", "expected", "actual"); }
        catch (Exception error) { caught = error; }
        var read = new MortalJointRepairCheckpoint.ReadResult(null,
            [new ValidationIssue("private/path", IssueSeverity.Error, "private issue", "private_code")]);
        var beforeJson = JsonSerializer.Serialize(read);
        var beforeText = read.ToString();
        read.WithDiagnostics("typed owning-error control", caughtException: caught);
        var failure = read.CreateFailure(QteMessage);
        var ordinary = new InvalidDataException(QteMessage);
        Assert.IsType<InvalidDataException>(failure);
        Assert.Equal(ordinary.Message, failure.Message);
        Assert.Equal(ordinary.ToString(), failure.ToString());
        Assert.Null(failure.InnerException);
        Assert.Empty(failure.Data.Values.OfType<Exception>());
        var diagnostic = Assert.IsType<MortalJointRepairReadDiagnostic>(MortalJointRepairReadDiagnostic.From(failure));
        Assert.Same(caught, diagnostic.CaughtException);
        Assert.NotEmpty(caught.StackTrace!);
        Assert.Equal("{}", JsonSerializer.Serialize(diagnostic));
        Assert.Equal(beforeJson, JsonSerializer.Serialize(read));
        Assert.Equal(beforeText, read.ToString());
        var refused = WoundAcceptedTurnPlanner.HandleJointRepairCommonPlanFailure(failure, null);
        Assert.False(refused.Success);
        Assert.Equal(QteMessage, Assert.Single(refused.Issues).Message);
        Assert.DoesNotContain("private owning error", JsonSerializer.Serialize(refused));
        Assert.DoesNotContain("private issue", JsonSerializer.Serialize(refused));
    }

    /// <summary>Reports a failed diagnostic copy and preserves the primary when its local output also fails.</summary>
    [Fact]
    public async Task DiagnosticCloneAndWriterFailuresCannotReplacePrimary()
    {
        var issue = new ValidationIssue("path", IssueSeverity.Error, "ordinary issue", "ordinary_code");
        var read = new MortalJointRepairCheckpoint.ReadResult(null, [issue]);
        var original = read.Issues;
        read.WithDiagnostics("copy failure", [null!]);
        Assert.Same(original, read.Issues);
        Assert.NotNull(read.Diagnostic!.CaptureFailure);
        var failure = read.CreateFailure(QteMessage);
        var writes = 0;
        var observed = await Record.ExceptionAsync(() => JointQteDiagnosticCapture.RunAsync<int>(
            () => Task.FromException<int>(failure), _ => { writes++; throw new IOException("local writer failed"); }));
        Assert.Same(failure, observed);
        Assert.True(writes > 0);
        Assert.Equal(QteMessage, observed!.Message);
    }

    /// <summary>Guards producer-issue getter failure through the same diagnostic seam used by mapped adapter refusals.</summary>
    [Fact]
    public async Task ThrowingSourceIssueGetterCannotReplaceOriginalRefusal()
    {
        var issue = new ValidationIssue("private/mapped", IssueSeverity.Error, "unchanged mapped refusal", "mapped_code");
        var read = new MortalJointRepairCheckpoint.ReadResult(null, [issue]);
        var before = JsonSerializer.Serialize(read);
        var getterFailure = new IOException("diagnostic source issue getter failed");
        var calls = 0;
        Assert.Same(read, read.WithDiagnosticsFromSource("mapped refusal", () => { calls++; throw getterFailure; }));
        Assert.Equal(1, calls);
        Assert.False(read.IsValid);
        Assert.Same(issue, Assert.Single(read.Issues));
        Assert.Equal(before, JsonSerializer.Serialize(read));
        Assert.Same(getterFailure, read.Diagnostic!.CaptureFailure);
        var failure = read.CreateFailure(QteMessage);
        Assert.Equal(QteMessage, failure.Message);
        Assert.Null(failure.InnerException);
        var rows = new List<string>();
        Assert.Same(failure, await Record.ExceptionAsync(() => JointQteDiagnosticCapture.RunAsync<int>(
            () => Task.FromException<int>(failure), rows.Add)));
        using var captured = JsonDocument.Parse(Assert.Single(rows));
        Assert.Equal("mapped refusal", captured.RootElement.GetProperty("Origin").GetString());
        Assert.Equal(getterFailure.ToString(), captured.RootElement.GetProperty("CaptureFailure").GetString());
        output.WriteLine(rows[0]);
    }

    /// <summary>Leaves successful operations untouched and produces no failure diagnostics.</summary>
    [Fact]
    public async Task CaptureWrapperLeavesSuccessUntouched()
    {
        var value = new object();
        var rows = new List<string>();
        Assert.Same(value, await JointQteDiagnosticCapture.RunAsync(() => Task.FromResult(value), rows.Add));
        Assert.Empty(rows);
    }

    /// <summary>Owns an ordinary small directory and real file manager without hooks or game preparation.</summary>
    private sealed class Fixture : IDisposable
    {
        private readonly string _nonce = Guid.NewGuid().ToString("N");
        private readonly ITestOutputHelper _output;
        private readonly string _root;

        /// <summary>Creates one independent root with no observer and no fabricated source authority.</summary>
        /// <param name="output">Owned cleanup receiver.</param>
        internal Fixture(ITestOutputHelper output)
        {
            _output = output;
            _root = Path.Combine(Path.GetTempPath(), "boe-qte-read-diagnostic-" + _nonce);
            Directory.CreateDirectory(Path.Combine(_root, "game_session"));
            File.WriteAllText(Path.Combine(_root, ".fixture-owner"), _nonce);
            Files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance);
        }

        /// <summary>Gets the actual manager issuing current leases and reading the invalid inputs.</summary>
        internal FileSystemManager Files { get; }

        /// <summary>Closes only the exact nonce-owned root after its source and lease have been disposed.</summary>
        public void Dispose()
        {
            Assert.Equal("boe-qte-read-diagnostic-" + _nonce, Path.GetFileName(_root));
            Assert.Equal(_nonce, File.ReadAllText(Path.Combine(_root, ".fixture-owner")));
            new TrustedLocalFileScope([Path.GetTempPath()]).DeleteOwnedTree(_root);
            Assert.False(Directory.Exists(_root));
            _output.WriteLine(JsonSerializer.Serialize(new { Kind = "QteReadDiagnosticCleanup", Root = _root, Removed = true }));
        }
    }
}
