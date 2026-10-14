using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("pending_stats")]
    [InlineData("unprocessed_level")]
    [InlineData("new_qte_offer")]
    public Task GuaranteedTreatment_ActiveTailRefusesBeforeJointCommonTakeAndRetainsHeldUnion(string scenario) =>
        RunJointCompletionControlAsync(nonzeroProgression: false, activeTailScenario: scenario);

    private async Task<Dictionary<string, byte[]?>> AuthorJointActiveTailBeforeOriginalAsync(string scenario,
        IReadOnlyDictionary<string, byte[]?> actualPrior42)
    {
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        Assert.Null(await _fs.ReadFileBytesAsync(lease, "input/turn_request.json"));
        Assert.Null(await _fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        foreach (var image in actualPrior42)
            Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(lease, image.Key));
        string path;
        JsonObject submitted;
        switch (scenario)
        {
            case "pending_stats":
                path = "game_state/player/status_changes.json";
                submitted = actualPrior42[path] is { } status ? ParseJsonObjectBytes(status) : new JsonObject();
                submitted["statsIncreased"] = new JsonArray("strength");
                break;
            case "unprocessed_level":
                path = "game_state/player/experience.json";
                submitted = ParseJsonObjectBytes(Assert.IsType<byte[]>(actualPrior42[path]));
                // Existing canonical level-up input shape; the real prior42
                // processed-level1 acknowledgment is deliberately unchanged.
                submitted["level"] = 2; submitted["playerLevel"] = 2;
                submitted["experience"] = 49; submitted["currentExperience"] = 49;
                submitted["totalExperience"] = 149; submitted["experienceForNextLevel"] = 150;
                break;
            case "new_qte_offer":
                path = QteSceneService.QteOfferPath;
                submitted = BuildPhysicalInjuryOffer(declared: false, maximum: 2);
                submitted["qteId"] = "qte_joint_active_tail_turn43";
                foreach (var terminal in submitted["terminalOutcomes"]!.AsArray())
                    foreach (var command in terminal!["responseFragment"]!["resourceChanges"]!.AsArray())
                        command!["eventRef"] = command["eventRef"]!.GetValue<string>().Replace("turn_42:", "turn_43:", StringComparison.Ordinal);
                break;
            default: throw new InvalidDataException("Unknown bounded active-tail input: " + scenario);
        }
        // Fixture input precedes the real original43. No acknowledgment, source,
        // request, claim, checkpoint or authority object is manufactured here.
        await _fs.WriteFileAtomicAsync(lease, path, submitted.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
        var images = actualPrior42.ToDictionary(row => row.Key, row => row.Value?.ToArray(), StringComparer.Ordinal);
        images[path] = Assert.IsType<byte[]>(await _fs.ReadFileBytesAsync(lease, path));
        Assert.False(actualPrior42[path] is { } previous && previous.AsSpan().SequenceEqual(images[path]));
        foreach (var image in images)
            Assert.Equal(image.Value, await _fs.ReadFileBytesAsync(lease, image.Key));
        Assert.Equal(actualPrior42["game_state/player/stat_points.json"], images["game_state/player/stat_points.json"]);
        WritePhysicalInjuryDiagnostic("fr063_joint_active_tail_before_original43", new
        { scenario, ActiveInputPath = path, ActualPrior42 = actualPrior42, Original43Inputs = images,
            ProcessedLevelAcknowledgmentAuthored = false, Original43Minted = false });
        return images;
    }

    private async Task AssertJointActiveTailRefusalAsync(string scenario, GameEngine engine,
        PendingTurnSnapshotReadAuthority original, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResourceFinalization finalization, IReadOnlyDictionary<string, byte[]?> before,
        JsonObject heldCommand, IReadOnlyList<(string Path, string Raw, Dictionary<string, byte[]?> Images)> milestones,
        int repairReplies, bool entered, bool handled, Exception? failure, Exception? observerFailure)
    {
        // Capture physical evidence before assertions. This supported reader
        // inspects the existing confirmed agreement; it creates no publication
        // lineage, request/claim or accepted treatment authority.
        await using var lease = await _fs.AcquireCanonicalWriteLeaseAsync();
        var after = await CaptureConfirmedClaimReadImagesAsync(_fs, lease);
        var confirmed = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(_fs, lease, request, finalization);
        var outcome = typeof(GameEngine).GetField("_mortalJointRepairCanonicalRefresh",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)?.GetValue(engine);
        var disposition = outcome?.GetType().GetProperty("Disposition")?.GetValue(outcome)?.ToString();
        var issues = outcome?.GetType().GetProperty("Issues")?.GetValue(outcome) as IReadOnlyList<ValidationIssue>;
        var hasCommon = AcceptedMechanicsPlanAuthority.HasValidated(_fs, lease);
        WritePhysicalInjuryDiagnostic("fr063_joint_active_tail_actual_refusal", new
        {
            scenario, ActualCaller = "ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync", entered, handled,
            Failure = failure?.ToString(), ObserverFailure = observerFailure?.ToString(), disposition, Issues = issues,
            ActualTurn = GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber, hasCommon, repairReplies,
            ConfirmedValid = confirmed.IsValid, ConfirmedState = confirmed.State, ConfirmedIssues = confirmed.Issues,
            Before = before, After = after, Milestones = milestones.Select(row => new { row.Path, row.Raw, row.Images })
        });
        Assert.Null(observerFailure); Assert.Null(failure); Assert.True(entered); Assert.True(handled);
        Assert.Equal("PublicationUncertain", disposition);
        Assert.NotNull(issues);
        var expected = scenario switch
        {
            "pending_stats" => "The first joint contour cannot own pending statsIncreased work.",
            "unprocessed_level" => "The first joint contour cannot own an unprocessed level-up or stat-point initialization/award.",
            "new_qte_offer" => "The first joint contour cannot own a new QTE offer for its decision turn.",
            _ => throw new InvalidDataException("Unknown bounded active-tail assertion.")
        };
        Assert.Contains(expected, DescribeValidationIssues(issues!), StringComparison.Ordinal);
        Assert.False(hasCommon);
        Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
        Assert.True(confirmed.IsValid, DescribeValidationIssues(confirmed.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, confirmed.State);
        Assert.Equal(request.RequestFingerprint, confirmed.RequestFingerprint); Assert.Null(confirmed.GenerationFingerprint);
        Assert.Equal(1, repairReplies);
        var dispatch = Assert.Single(milestones.Where(row => row.Path == "game_state/control/validation_repair_request.json"));
        using var repair = JsonDocument.Parse(dispatch.Raw.TrimStart('\uFEFF'));
        Assert.True(repair.RootElement.GetProperty("fullTurnResubmissionRequired").GetBoolean());
        Assert.Contains(repair.RootElement.GetProperty("errors").EnumerateArray(), row => row.GetProperty("code").GetString() == "wound_severity_above_opportunity");
        Assert.Equal(original.SnapshotToken, Assert.Single(repair.RootElement.GetProperty("harnessRepairPackets").EnumerateArray()
            .Where(row => row.TryGetProperty("candidateKind", out _))).GetProperty("snapshotToken").GetString());
        var pending = ParseJsonObjectBytes(Assert.IsType<byte[]>(after[WoundAcceptedTurnSnapshotContract.PendingResolutionPath]));
        Assert.True(WoundRepairPacketBuilder.IsValidPersistedRepairWave(JsonSerializer.SerializeToElement(pending["repairPackets"]),
            JsonSerializer.SerializeToElement(pending["repairReceipts"]), original.SessionId, original.RequestId, original.SnapshotToken));
        var submitted = Assert.IsType<JsonObject>(Assert.Single(pending["submittedTreatmentRequests"]!.AsArray()));
        Assert.Equal(request.RequestFingerprint, submitted["requestFingerprint"]!.GetValue<string>());
        Assert.True(JsonNode.DeepEquals(WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request), submitted["request"]));
        Assert.True(JsonNode.DeepEquals(heldCommand, ParseJsonObjectBytes(Assert.IsType<byte[]>(after[AcceptedMechanicsPlan.WoundCommandPath]))));
        Assert.Equal("II", ParseJsonObjectBytes(Assert.IsType<byte[]>(after[MortalWoundOpportunityAdapter.DecisionPath]))["proposal"]!["severity"]!.GetValue<string>());
        // Only actual repair transport and the authorized severity correction
        // differ from the complete original-bound pre-entry namespace.
        var repairOwned = new HashSet<string>(StringComparer.Ordinal)
        {
            WoundAcceptedTurnSnapshotContract.PendingResolutionPath, MortalWoundOpportunityAdapter.DecisionPath,
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            "game_state/control/validation_diagnostic_failure_report.json"
        };
        Assert.Equal(before.Keys.Where(path => !repairOwned.Contains(path)).OrderBy(path => path, StringComparer.Ordinal),
            after.Keys.Where(path => !repairOwned.Contains(path)).OrderBy(path => path, StringComparer.Ordinal));
        foreach (var image in before.Where(row => !repairOwned.Contains(row.Key)))
            Assert.Equal(image.Value, after[image.Key]);
        Assert.False(milestones.Any(row => row.Path == MortalQteDecisionPublication.StatePath));
        Assert.Equal("untreated", ReadCurrentTreatmentWound(_fs).Care.State);
        Assert.Single(ReadCurrentTreatmentHistory(_fs).State!.Transitions);
    }
}
