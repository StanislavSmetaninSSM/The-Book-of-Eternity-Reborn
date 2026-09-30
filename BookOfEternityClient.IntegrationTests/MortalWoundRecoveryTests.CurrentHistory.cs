using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundRecoveryTests
{
    [Theory]
    [InlineData(false, "malformed")]
    [InlineData(true, "malformed")]
    [InlineData(false, "missing")]
    [InlineData(true, "missing")]
    [InlineData(false, "empty_history")]
    [InlineData(true, "empty_history")]
    [InlineData(false, "changed_fingerprint")]
    [InlineData(true, "changed_fingerprint")]
    [InlineData(false, "directory")]
    [InlineData(true, "directory")]
    public void T069C_CurrentHistoryFailurePrecedesRecoveryArithmeticWithoutWriting(bool restart, string mutation)
    {
        var scenario = Scenario.CheckedOverflow() with { StartsStabilized = false };
        using var fixture = Fixture.Create(scenario);
        if (restart) fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        AssertPlannerResult(InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);

        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        WoundHistoryParseResult? parsed = null;
        switch (mutation)
        {
            case "malformed": File.WriteAllText(historyPath, "{"); break;
            case "missing": File.Delete(historyPath); break;
            case "empty_history": File.WriteAllText(historyPath, "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}"); break;
            case "changed_fingerprint": fixture.TamperPersistedRecoveryEvidence("history"); break;
            case "directory": File.Delete(historyPath); Directory.CreateDirectory(historyPath); break;
            default: throw new ArgumentOutOfRangeException(nameof(mutation), mutation, null);
        }
        if (mutation != "directory")
        {
            parsed = WoundHistoryState.Parse(File.Exists(historyPath) ? File.ReadAllText(historyPath) : null, WoundHistoryState.HistoryPath);
            Assert.Equal(mutation is "empty_history" or "changed_fingerprint", parsed.IsValid);
        }
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();
        var result = InvokeRecoveryWithoutAmbientLease(fixture);
        AssertInvalidHistory(result);
        var issues = Values(result, "Issues").Select(Assert.IsType<ValidationIssue>).ToArray();
        if (parsed is { IsValid: false })
            Assert.Equal(parsed.Issues.Select(static issue => (issue.Code, issue.FilePath, issue.Actual)), issues.Select(static issue => (issue.Code, issue.FilePath, issue.Actual)));
        else
        {
            var issue = Assert.Single(issues);
            Assert.Equal(mutation == "directory" ? "mortal_wound_recovery_history_read_failed" : "mortal_wound_recovery_history_mismatch", issue.Code);
            Assert.Equal(WoundHistoryState.HistoryPath, issue.FilePath);
        }
        Assert.DoesNotContain(issues, static issue => issue.Code == "mortal_wound_recovery_checked_time_overflow");
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
        Assert.Equal(mutation == "directory", Directory.Exists(historyPath));
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void T069C_CurrentHistoryAdmissionPreservesSemanticHistoryAndSealedClock(bool restart, bool reformatWithBom)
    {
        var scenario = Scenario.RequiresStabilization();
        using var fixture = Fixture.Create(scenario);
        if (restart) fixture.RestartForReplay();
        fixture.AssertCarrierIdentityHistoryAgreement();
        var historyPath = fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath);
        if (reformatWithBom)
        {
            var original = File.ReadAllBytes(historyPath);
            var history = Assert.IsType<JsonObject>(JsonNode.Parse(File.ReadAllText(historyPath)));
            var reordered = new JsonObject();
            foreach (var property in history.Reverse()) reordered.Add(property.Key, property.Value?.DeepClone());
            File.WriteAllText(historyPath, reordered.ToJsonString(new JsonSerializerOptions { WriteIndented = true }), new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
            var reformatted = File.ReadAllBytes(historyPath);
            Assert.False(original.AsSpan().SequenceEqual(reformatted));
            Assert.True(reformatted.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }));
            Assert.True(WoundHistoryState.Parse(File.ReadAllText(historyPath), WoundHistoryState.HistoryPath).IsValid);
        }
        fixture.CorruptLiveClock();
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();
        var first = AssertPlannerResult(InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);
        var repeated = AssertPlannerResult(InvokeRecoveryWithoutAmbientLease(fixture), scenario, fixture.WoundId);
        Assert.NotNull(first); Assert.NotNull(repeated);
        Assert.Equal(Required(first, "AuthorityFingerprint"), Required(repeated, "AuthorityFingerprint"));
        Assert.Equal(Required(first, "TickKey"), Required(repeated, "TickKey"));
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void T069C_CurrentHistoryDoesNotOverrideRejectedRegistryAuthority(bool staleBinding)
    {
        using var fixture = Fixture.Create(Scenario.RequiresStabilization());
        var binding = fixture.Binding;
        if (staleBinding) fixture.PrepareFreshContinuationTurn(45, "history_guard_stale_binding");
        File.WriteAllText(fixture.FileSystem.ResolvePath(WoundHistoryState.HistoryPath), "{");
        var governedBefore = Fixture.CaptureAllGovernedBytes(fixture.FileSystem);
        var treeBefore = fixture.CaptureCanonicalTreeBytes();
        var result = InvokeRecoveryWithoutAmbientLease(fixture, binding, staleBinding ? fixture.WoundId : "wound_other_current");
        AssertPlannerAuthorityRejected(result);
        AssertRecoveryAdmissionDidNotWrite(fixture, governedBefore, treeBefore);
    }

    private static object InvokeRecoveryWithoutAmbientLease(Fixture fixture, WoundAcceptedTurnBinding? binding = null, string? woundId = null)
    {
        Task<object> operation;
        using (ExecutionContext.SuppressFlow()) operation = Task.Run(() => InvokePlan(fixture, binding, woundId));
        return operation.GetAwaiter().GetResult();
    }

    private static void AssertRecoveryAdmissionDidNotWrite(Fixture fixture, IReadOnlyDictionary<string, byte[]?> governedBefore, IReadOnlyDictionary<string, byte[]> treeBefore)
    {
        Fixture.AssertAllGovernedBytesUnchanged(fixture.FileSystem, governedBefore);
        fixture.AssertCanonicalTreeBytesUnchanged(treeBefore);
        Assert.False(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.FileSystem, fixture.Lease, out _, out _));
    }
}
