using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeResourceCutoverTests
{
    /// <summary>
    /// Commits an actual saved dependent choice after physical narrative and correction files use supported UTF-8 encodings.
    /// </summary>
    /// <param name="narrativePreamble">
    /// Whether the initial materialization's real narrative file includes one UTF-8 preamble.
    /// </param>
    /// <param name="correctionPreamble">
    /// Whether the later allowlisted conflict correction includes one UTF-8 preamble.
    /// </param>
    /// <returns>
    /// A task completing after actual resume reaches completed-unpublished with the same command and decision fingerprint.
    /// </returns>
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task OriginalSpiritualDependentCorrection_Utf8PhysicalFilesResumeSavedChoice(
        bool narrativePreamble, bool correctionPreamble)
    {
        await using var context = await CreateSpiritualContinuationTransportContextAsync(commitFirst: true, dependentCost: true);
        await using var lease = await context.FileSystem.AcquireCanonicalWriteLeaseAsync();
        var narrative = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, ProjectionNarrativePath))!)!;
        narrative["response"] = "Чужое давление надломило волю хранителя.";
        byte[] narrativeBytes = [.. narrativePreamble ? Encoding.UTF8.GetPreamble() : [], .. Encoding.UTF8.GetBytes(narrative.ToJsonString())];
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, ProjectionNarrativePath, narrativeBytes);
        var opened = await context.Validator.OpenC2PrivateSessionAsync(lease);
        Assert.Equal("offer", opened.Disposition);
        using (var session = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(opened.Session))
        {
            var selected = await session.SubmitDecisionAsync(lease,
                OriginalSpiritualWoundDecision(session.Offer!.OpportunityRef, "spiritual_action_cost_burden", "guard"),
                "Чужое давление надломило волю хранителя.");
            Assert.True(selected.Disposition == "dependent_continuation", FormatC2SubmissionIssues(selected));
            selected.Session?.Dispose();
        }
        var command = await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath);
        Assert.NotNull(command);
        var checkpoint = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!)!;
        var fingerprint = checkpoint["checkpoint"]!["pendingSubmission"]!["stagedDecision"]!["decisionFingerprint"]!.GetValue<string>();
        var issued = await context.Validator.ReadSpiritualWoundContinuationAsync(lease);
        Assert.Equal("dependent_draft", Assert.IsType<SpiritualWoundContinuationRequest>(issued.Request).Phase);
        var raw = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
        raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["effectiveCost"] = 3;
        raw["activeConflict"]!["exchangeLog"]![1]!["actionCostAudit"]!["opposition"]!["after"] = 0;
        byte[] bytes = [.. correctionPreamble ? Encoding.UTF8.GetPreamble() : [], .. Encoding.UTF8.GetBytes(raw.ToJsonString())];
        await context.FileSystem.WriteFileAtomicBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath, bytes);
        var issues = await context.Validator.ValidateSpiritualWoundContinuationDraftAsync(lease, issued.Request!,
            new SpiritualWoundContinuationResponse { ContinuationId = issued.Request!.ContinuationId, WoundDecisions = [] },
            new Dictionary<string, byte[]?>(), requireResolvedDraft: true);
        Assert.Empty(issues);

        var current = await context.Validator.OpenC2PrivateSessionAsync(lease);
        Assert.True(current.Disposition == "dependent_continuation", FormatC2SubmissionIssues(current));
        using var resumeSession = Assert.IsType<ValidationService.SpiritualC2PrivateSession>(current.Session);
        var resumed = await resumeSession.ResumeDependentContinuationAsync(lease);
        using var completed = resumed.Session;
        Assert.True(resumed.Disposition == "completed_unpublished", FormatC2SubmissionIssues(resumed));
        Assert.Equal(command, await context.FileSystem.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(narrativeBytes, await context.FileSystem.ReadFileBytesAsync(lease, ProjectionNarrativePath));
        Assert.Equal(bytes, await context.FileSystem.ReadFileBytesAsync(lease, AfterlifeSpiritualConflictState.StatePath));
        var final = JsonNode.Parse((await context.FileSystem.ReadFileAsync(lease, SpiritualWoundCaptureCheckpointState.StatePath))!)!["checkpoint"]!;
        Assert.Null(final["pendingSubmission"]);
        Assert.Equal(1, final["committedAdvance"]!.GetValue<int>());
        Assert.Equal(fingerprint, final["advances"]![0]!["newDecisionFingerprints"]![0]!.GetValue<string>());
    }
}
