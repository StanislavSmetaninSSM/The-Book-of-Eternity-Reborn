using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class WoundMaterializationLifecycleTests
{
    /// <summary>
    /// Verifies that one published signed occurrence retains its receipt through one hundred identical responses.
    /// </summary>
    /// <returns>
    /// A task completing after all replays preserve the receipt and leave the published wound, effect, and history unchanged.
    /// </returns>
    [Fact]
    public async Task WoundMaterializationReplayTests_ConsumedOpportunityReceiptSurvivesOneHundredExactResponseReplays()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2);
        var decision = Decision("materialize", CreateRepairRoundtripProposal("treatment"));
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        var accepted = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            response.WoundDecisions,
            response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(accepted.Success, Describe(accepted.Issues));
        await PublishAsync(
            context,
            Assert.IsType<JsonObject>(accepted.CommandRoot));
        var before = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath);
        var acceptedReceipt = Assert.Single(accepted.DecisionReceipts);

        for (var replayIndex = 0; replayIndex < 100; replayIndex++)
        {
            var replay = WoundResponseInputComposer.Compose(
                authority.Binding,
                new[] { authority.Opportunity },
                response.WoundDecisions,
                response.Response,
                new[] { acceptedReceipt });

            Assert.True(replay.Success, Describe(replay.Issues));
            Assert.Empty(replay.Transitions);
            Assert.Empty(replay.Notifications);
            Assert.Equal(acceptedReceipt, Assert.Single(replay.DecisionReceipts));
            Assert.Empty(Assert.IsType<JsonObject>(replay.CommandRoot)
                ["commands"]!.AsArray());
        }

        await context.AssertUnchangedAsync(before);
        Assert.False(context.FileSystem.FileExists(
            AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath))["activeWounds"]!.AsArray());
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath))["transitions"]!.AsArray());
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath))["activeEffects"]!.AsArray());
    }

    /// <summary>
    /// Verifies that an accepted severity repair publishes once from a signed occurrence and remains stable through one hundred replays.
    /// </summary>
    /// <returns>
    /// A task completing after the repair wave is consumed and all replays preserve the receipt and published canonical bytes.
    /// </returns>
    [Fact]
    public async Task WoundMaterializationReplayTests_CorrectedRepairSurvivesOneHundredExactResponseReplays()
    {
        await using var context = await CreatePlayerContextAsync();
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2);
        var validProposal = CreateRepairRoundtripProposal("severity");
        var rejectedProposal = validProposal.DeepClone().AsObject();
        ApplyRejectedRepairMutation("severity", rejectedProposal);
        var rejectedDecision = Decision("materialize", rejectedProposal);
        rejectedDecision["opportunityRef"] = authority.Opportunity.PublicRef;
        var issue = new ValidationIssue(
            "woundDecisions[0].proposal.severity",
            IssueSeverity.Error,
            "The rejected wound severity exceeds its sealed maximum.",
            code: "wound_severity_above_opportunity",
            section: "wound_materialization",
            expected: "validator-internal range",
            actual: "III");
        var packet = Assert.Single(WoundRepairPacketBuilder.Build(
            new WoundRepairBuildRequest(
                authority.Binding.SessionId,
                authority.Binding.RequestId,
                authority.Binding.SnapshotToken,
                new[]
                {
                    new WoundRepairCandidateInput(
                        "repair_wound",
                        "candidate_replay_repair",
                        Fingerprint("replay-repair"),
                        authority.Opportunity.PublicRef,
                        new JsonObject
                        {
                            ["event"] = "острый край во время обвала",
                            ["target"] = "игрок",
                            ["realm"] = "Смертный мир"
                        },
                        new[] { "none", "materialize" },
                        "I",
                        "II",
                        rejectedDecision,
                        new[] { issue })
                })));
        var repairAuthority = new WoundRepairPacketAuthority(
            authority.Binding.SessionId,
            authority.Binding.RequestId,
            authority.Binding.SnapshotToken,
            "generation_replay_repair",
            authority.Opportunity.InputEvidenceFingerprint,
            Fingerprint("replay-repair-target"),
            Fingerprint("replay-repair-roll"));
        var repairCache = new AcceptedMechanicsPlanCache(
            AcceptedMechanicsPlanner.BuildAcceptedPlan);
        Assert.True(repairCache.TryRegisterWoundRepairWave(
            repairAuthority,
            new[] { packet }));
        Assert.True(repairCache.TryTakeWoundRepairPacket(
            repairAuthority,
            packet.CreateReceipt(),
            out var acceptedPacket));
        var correctedProposal = acceptedPacket.PreservedProposal.DeepClone().AsObject();
        ApplyRepairCorrection("severity", correctedProposal, validProposal);
        var correctedDecision = Decision("materialize", correctedProposal);
        correctedDecision["opportunityRef"] = authority.Opportunity.PublicRef;
        var correctedResponse = Response(correctedDecision);
        var corrected = WoundResponseInputComposer.Compose(
            authority.Binding,
            new[] { authority.Opportunity },
            correctedResponse.WoundDecisions,
            correctedResponse.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(corrected.Success, Describe(corrected.Issues));
        await PublishAsync(
            context,
            Assert.IsType<JsonObject>(corrected.CommandRoot));
        var before = await context.CaptureAsync(
            WoundCarrierCatalog.PlayerPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            EffectCarrierCatalog.PlayerPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath);
        var decisionReceipt = Assert.Single(corrected.DecisionReceipts);

        for (var replayIndex = 0; replayIndex < 100; replayIndex++)
        {
            var replay = WoundResponseInputComposer.Compose(
                authority.Binding,
                new[] { authority.Opportunity },
                correctedResponse.WoundDecisions,
                correctedResponse.Response,
                new[] { decisionReceipt });

            Assert.True(replay.Success, Describe(replay.Issues));
            Assert.Empty(replay.Transitions);
            Assert.Empty(replay.Notifications);
            Assert.Equal(decisionReceipt, Assert.Single(replay.DecisionReceipts));
            Assert.Empty(Assert.IsType<JsonObject>(replay.CommandRoot)
                ["commands"]!.AsArray());
        }

        await context.AssertUnchangedAsync(before);
        Assert.False(repairCache.HasWoundRepairWave);
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundCarrierCatalog.PlayerPath))["activeWounds"]!.AsArray());
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            WoundHistoryState.HistoryPath))["transitions"]!.AsArray());
        Assert.Single(Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            EffectCarrierCatalog.PlayerPath))["activeEffects"]!.AsArray());
    }
}

public sealed class WoundMaterializationReplayTests
{
    /// <summary>
    /// Retains only immutable accepted course-history JSON for detached replay fixtures.
    /// </summary>
    private static readonly Lazy<string> PreparedTreatmentHistory = new(
        MortalWoundTreatmentResolverTests.CreatePublishedCourseReplayHistory,
        LazyThreadSafetyMode.ExecutionAndPublication);

    private static readonly string[] ReplayTrackedPaths =
    [
        WoundMaterializationTestContext.PlayerWoundsPath,
        WoundMaterializationTestContext.HistoryPath,
        WoundMaterializationTestContext.CommandsPath,
        WoundMaterializationTestContext.PendingResolutionsPath,
        WoundMaterializationTestContext.ProgressionSchedulePath,
        WoundMaterializationTestContext.ProgressionReportPath,
        WoundMaterializationTestContext.NarrativeOutputPath,
        WoundMaterializationTestContext.InterfaceUpdatesOutputPath,
        WoundMaterializationTestContext.DebugLogsOutputPath,
        EffectCarrierCatalog.PlayerPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath
    ];

    public static TheoryData<string, bool> AcceptedReplayContours => new()
    {
        { "success", false },
        { "repair", false },
        { "crash_recovery", true },
        { "consumed_command", false }
    };

    public static TheoryData<string> ChangedReplayCoordinates => new()
    {
        "accepted_event",
        "treatment_attempt",
        "course",
        "course_milestone",
        "cycle",
        "payment",
        "output"
    };

    /// <summary>
    /// Preserves one accepted receipt and exact stored bytes across one hundred identical treatment replays.
    /// </summary>
    /// <param name="contour">
    /// Existing success, repair, crash-recovery or consumed-command replay contour.
    /// </param>
    /// <param name="retainExactCrashCommand">
    /// Whether the exact already-accepted command remains on disk for crash-recovery assertions.
    /// </param>
    /// <returns>
    /// A task completing after all one hundred receipt, sealed-result, duplicate-state and byte-preservation checks.
    /// </returns>
    [Theory]
    [MemberData(nameof(AcceptedReplayContours))]
    public async Task ExactAcceptedTreatmentReplay_OneHundredRepeatsReturnOneReceiptAndZeroDuplicateState(
        string contour,
        bool retainExactCrashCommand)
    {
        await using var context = await WoundMaterializationTestContext.CreateAsync();
        var scenario = CreateReplayScenario(contour);
        await SeedReplayStateAsync(context, scenario, retainExactCrashCommand);
        var before = await context.CaptureBeforeImagesAsync(ReplayTrackedPaths);
        var parsed = WoundHistoryState.Parse(
            scenario.History.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var state = Assert.IsType<WoundHistoryState>(parsed.State);
        var transition = state.Transitions.Single(value => string.Equals(
            value.OperationKey,
            scenario.OperationKey,
            StringComparison.Ordinal));
        var probe = WoundHistoryState.CreateReplayProbe(transition);
        WoundAlreadyAcceptedReceipt? firstReceipt = null;

        for (var replayIndex = 0; replayIndex < 100; replayIndex++)
        {
            var replay = WoundAcceptedTurnPlanner.ResolveAcceptedReplay(state, probe);

            Assert.Equal(WoundHistoryReplayDisposition.Exact, replay.Disposition);
            Assert.Empty(replay.Issues);
            var receipt = Assert.IsType<WoundAlreadyAcceptedReceipt>(
                replay.AlreadyAcceptedReceipt);
            firstReceipt ??= receipt;
            Assert.Equal(firstReceipt, receipt);
            Assert.Equal(scenario.OperationKey, receipt.OperationKey);
            Assert.Equal(scenario.EventRef, receipt.EventRef);
            Assert.Equal(scenario.AttemptId, receipt.AttemptId);
            Assert.Equal(scenario.CourseId, receipt.CourseId);
            Assert.Equal(scenario.CourseMilestoneOrdinal, receipt.CourseMilestoneOrdinal);
            Assert.Equal(scenario.CycleKey, receipt.CycleKey);
            Assert.Equal(scenario.PaymentFingerprint, receipt.PaymentFingerprint);
            Assert.Equal(scenario.OutputFingerprint, receipt.OutputFingerprint);
            Assert.Equal("Рана очищена и стабилизирована.", receipt.ReadableSummary);
            var treatmentResult = Assert.IsType<MortalWoundTreatmentPersistedResult>(
                receipt.TransitionResult);
            Assert.True(JsonNode.DeepEquals(
                scenario.History["transitions"]![2]!["transitionResult"],
                treatmentResult.ToCanonicalJson()));
        }

        await context.AssertBeforeImagesUnchangedAsync(before);
        AssertReplayStateHasNoDuplicates(context, scenario, retainExactCrashCommand);
    }

    [Theory]
    [MemberData(nameof(ChangedReplayCoordinates))]
    public async Task ChangedReplayCoordinate_OneHundredAttemptsRemainConflictAndByteExact(
        string changedCoordinate)
    {
        await using var context = await WoundMaterializationTestContext.CreateAsync();
        var scenario = CreateReplayScenario("changed_" + changedCoordinate);
        await SeedReplayStateAsync(context, scenario, retainExactCrashCommand: false);
        var before = await context.CaptureBeforeImagesAsync(ReplayTrackedPaths);
        var parsed = WoundHistoryState.Parse(
            scenario.History.ToJsonString(),
            WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var state = Assert.IsType<WoundHistoryState>(parsed.State);
        var transition = state.Transitions.Single(value => string.Equals(
            value.OperationKey,
            scenario.OperationKey,
            StringComparison.Ordinal));
        var exact = WoundHistoryState.CreateReplayProbe(transition);
        var changed = changedCoordinate switch
        {
            "accepted_event" => exact with { EventRef = scenario.EventRef + "_changed" },
            "treatment_attempt" => exact with { AttemptId = scenario.AttemptId + "_changed" },
            "course" => exact with { CourseId = scenario.CourseId + "_changed" },
            "course_milestone" => exact with
            {
                CourseMilestoneOrdinal = scenario.CourseMilestoneOrdinal + 1
            },
            "cycle" => exact with { CycleKey = scenario.CycleKey + "_changed" },
            "payment" => exact with
            {
                PaymentFingerprint = Fingerprint('e')
            },
            "output" => exact with { OutputFingerprint = Fingerprint('f') },
            _ => throw new ArgumentOutOfRangeException(
                nameof(changedCoordinate),
                changedCoordinate,
                "Unknown replay coordinate.")
        };

        for (var replayIndex = 0; replayIndex < 100; replayIndex++)
        {
            var replay = WoundAcceptedTurnPlanner.ResolveAcceptedReplay(state, changed);

            Assert.Equal(WoundHistoryReplayDisposition.Conflict, replay.Disposition);
            Assert.Null(replay.AlreadyAcceptedReceipt);
            Assert.Contains(replay.Issues, issue =>
                issue.Code == "wound_history_conflicting_replay");
        }

        await context.AssertBeforeImagesUnchangedAsync(before);
        AssertReplayStateHasNoDuplicates(
            context,
            scenario,
            retainExactCrashCommand: false);
    }

    /// <summary>
    /// Builds the existing outer replay sentinels around a detached genuine course-history prefix.
    /// </summary>
    /// <param name="contour">
    /// Name retained by the independent replay-state and correlation sentinels.
    /// </param>
    /// <returns>
    /// A fresh three-row course history retaining both actual predecessors and the complete second-milestone result.
    /// </returns>
    private static ReplayScenario CreateReplayScenario(string contour)
    {
        var suffix = contour.ToLowerInvariant();
        var history = JsonNode.Parse(PreparedTreatmentHistory.Value)!.AsObject();
        var parsed = WoundHistoryState.Parse(history.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var state = Assert.IsType<WoundHistoryState>(parsed.State);
        Assert.Equal(3, state.Transitions.Count);
        var accepted = state.Transitions.Last();
        var treatmentResult = Assert.IsType<MortalWoundTreatmentPersistedResult>(accepted.TransitionResult);
        var coordinates = treatmentResult.Request.Coordinates;
        var woundId = coordinates.WoundId;
        var operationKey = coordinates.OperationKey;
        var eventRef = coordinates.EventRef;
        var attemptId = coordinates.AttemptId;
        var courseId = Assert.IsType<string>(treatmentResult.Receipt.CourseId);
        var courseMilestoneOrdinal = Assert.IsType<int>(
            treatmentResult.Receipt.CourseMilestoneOrdinal);
        Assert.Equal(2, courseMilestoneOrdinal);
        var cycleKey = "cycle_replay_treatment_" + suffix;
        var paymentFingerprint = Fingerprint('9');
        var outputFingerprint = Fingerprint('d');
        var target = history["transitions"]![2]!.AsObject();
        target["cycleKey"] = cycleKey;
        target["paymentFingerprint"] = paymentFingerprint;
        target["outputFingerprint"] = outputFingerprint;
        target["readableSummary"] = "Рана очищена и стабилизирована.";
        return new ReplayScenario(
            suffix,
            woundId,
            operationKey,
            eventRef,
            attemptId,
            courseId,
            courseMilestoneOrdinal,
            cycleKey,
            paymentFingerprint,
            outputFingerprint,
            history);
    }

    private static async Task SeedReplayStateAsync(
        WoundMaterializationTestContext context,
        ReplayScenario scenario,
        bool retainExactCrashCommand)
    {
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.PlayerWoundsPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeWounds"] = new JsonArray(new JsonObject
                {
                    ["woundId"] = scenario.WoundId,
                    ["state"] = "stabilized"
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundHistoryState.HistoryPath,
            scenario.History.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            EffectCarrierCatalog.PlayerPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["activeEffects"] = new JsonArray(new JsonObject
                {
                    ["effectId"] = "effect_" + scenario.WoundId,
                    ["sourceWoundId"] = scenario.WoundId
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.StatePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["operationKey"] = scenario.OperationKey,
                    ["chargeCount"] = 1,
                    ["paymentFingerprint"] = scenario.PaymentFingerprint
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            ResourceMaterializationContract.HistoryPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["operationKey"] = scenario.OperationKey,
                    ["acceptedCharges"] = 1
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.ProgressionSchedulePath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray(new JsonObject
                {
                    ["cycleKey"] = scenario.CycleKey,
                    ["acceptedCount"] = 1
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.ProgressionReportPath,
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["acceptedCycleKeys"] = new JsonArray(scenario.CycleKey)
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.NarrativeOutputPath,
            new JsonObject
            {
                ["response"] = "Рана очищена и стабилизирована.",
                ["outputFingerprint"] = scenario.OutputFingerprint
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.InterfaceUpdatesOutputPath,
            new JsonObject
            {
                ["notifications"] = new JsonArray(new JsonObject
                {
                    ["outputFingerprint"] = scenario.OutputFingerprint,
                    ["publishedCount"] = 1
                })
            }.ToJsonString());
        await context.FileSystem.WriteFileAtomicAsync(
            WoundMaterializationTestContext.DebugLogsOutputPath,
            new JsonObject
            {
                ["replayContour"] = scenario.Contour,
                ["operationKey"] = scenario.OperationKey
            }.ToJsonString());
        if (retainExactCrashCommand)
        {
            await context.FileSystem.WriteFileAtomicAsync(
                WoundMaterializationTestContext.CommandsPath,
                new JsonObject
                {
                    ["schemaVersion"] = 1,
                    ["commands"] = new JsonArray(new JsonObject
                    {
                        ["operationKey"] = scenario.OperationKey,
                        ["eventRef"] = scenario.EventRef
                    })
                }.ToJsonString());
        }
        else if (context.FileSystem.FileExists(
                     WoundMaterializationTestContext.CommandsPath))
        {
            context.FileSystem.DeleteFile(
                WoundMaterializationTestContext.CommandsPath);
        }
    }

    private static void AssertReplayStateHasNoDuplicates(
        WoundMaterializationTestContext context,
        ReplayScenario scenario,
        bool retainExactCrashCommand)
    {
        var wounds = JsonNode.Parse(File.ReadAllText(context.FileSystem.ResolvePath(
            WoundMaterializationTestContext.PlayerWoundsPath)))!.AsObject();
        Assert.Single(wounds["activeWounds"]!.AsArray());
        var effects = JsonNode.Parse(File.ReadAllText(context.FileSystem.ResolvePath(
            EffectCarrierCatalog.PlayerPath)))!.AsObject();
        Assert.Single(effects["activeEffects"]!.AsArray());
        var resources = JsonNode.Parse(File.ReadAllText(context.FileSystem.ResolvePath(
            ResourceMaterializationContract.StatePath)))!.AsObject();
        Assert.Single(resources["entries"]!.AsArray());
        var schedule = JsonNode.Parse(File.ReadAllText(context.FileSystem.ResolvePath(
            WoundMaterializationTestContext.ProgressionSchedulePath)))!.AsObject();
        Assert.Single(schedule["entries"]!.AsArray());
        var output = JsonNode.Parse(File.ReadAllText(context.FileSystem.ResolvePath(
            WoundMaterializationTestContext.InterfaceUpdatesOutputPath)))!.AsObject();
        Assert.Single(output["notifications"]!.AsArray());
        var history = WoundHistoryState.Parse(
            File.ReadAllText(context.FileSystem.ResolvePath(
                WoundHistoryState.HistoryPath)),
            WoundHistoryState.HistoryPath);
        Assert.True(history.IsValid, Describe(history.Issues));
        Assert.Equal(3, history.State!.Transitions.Count);
        Assert.Single(history.State.Transitions, value => string.Equals(
            value.OperationKey,
            scenario.OperationKey,
            StringComparison.Ordinal));
        Assert.Equal(
            retainExactCrashCommand,
            context.FileSystem.FileExists(
                WoundMaterializationTestContext.CommandsPath));
    }

    private static string Describe(IEnumerable<ValidationIssue> issues) =>
        string.Join(Environment.NewLine, issues.Select(static issue =>
            $"{issue} code={issue.Code}; expected={issue.Expected}; actual={issue.Actual}"));

    private static string Fingerprint(char value) =>
        "sha256:" + new string(value, 64);

    private sealed record ReplayScenario(
        string Contour,
        string WoundId,
        string OperationKey,
        string EventRef,
        string AttemptId,
        string CourseId,
        int CourseMilestoneOrdinal,
        string CycleKey,
        string PaymentFingerprint,
        string OutputFingerprint,
        JsonObject History);
}
