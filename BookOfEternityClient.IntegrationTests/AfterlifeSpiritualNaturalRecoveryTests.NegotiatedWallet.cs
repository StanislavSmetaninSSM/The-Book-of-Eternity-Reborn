using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    /// <summary>Real grant/stage/owner controls; actual Engine cleanup is qualified by the separate UI journeys.</summary>
    [Theory]
    [InlineData("zero")][InlineData("null")][InlineData("proposed")]
    public async Task NegotiatedWalletRefusesCompetingCommandsAndProposedChangeWithoutLosingTheFailedAttempt(string conflict)
    {
        await using var fixture = await HealingFixture.CreateAsync(3, 0, 1, stageRequest: false,
            beforeResourceBootstrap: ExplorerWoundSelfTreatmentTests.SeedEngineReceivingResidentAsync,
            configureSoul: soul => soul["inkFeathers"] = new JsonObject { ["current"] = 500, ["total"] = 900 });
        var files = fixture.Files;
        var compensation = new JsonObject { ["kind"] = "free_aid", ["agreementRef"] = "agreement_wallet_guard",
            ["readableTerms"] = "Добровольная помощь без платы и нового обязательства после отдельного подтверждения." };
        _ = await CommitAidGrantAsync(fixture, receiveAid: true, resident: true, compensation: compensation);
        var grant = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        var grantJson = grant.ToCanonicalJson().ToJsonString();
        var original = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(1, original.Recovery.CurrentStepProgress);
        var grantJournal = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!
            [GuardianAbodeResidentState.ThoughtJournalProperty]!.DeepClone();
        var wallet = SpiritualTreatmentWalletInvariant.Capture(JsonNode.Parse((await files.ReadFileAsync(
            AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
        var scheduler = new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance);
        var opened = (await WoundApplicationService.OpenAsync(files, "/лечить", treatment: true)).WoundCommandState!;
        SpiritualSelfTreatmentSelection selection;
        ProgressionControl control;
        JsonObject request;
        JsonObject projection;
        var draws = 0;
        var protectedPaths = new[] { AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath,
            GuardianAbodeResidentState.StatePath, GuardianThoughtJournalState.StatePath,
            LiveTurnPreparationService.TurnRequestPath, LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath, AcceptedMechanicsPlan.WoundCommandPath,
            PendingPlayerActionService.PendingPath, ProgressionScheduleService.ReportPath, "output/interface_updates.json" };
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            var self = Assert.Single(opened.Targets, t => t.Owner.OwnerKind == "player_soul");
            var choices = (await WoundApplicationService.SelectTargetAsync(files, lease, opened, self.Value, "/лечить")).WoundCommandState!;
            var choice = Assert.Single(choices.Choices, c => c.Available && c.Selection.Aid?.Compensation is not null);
            var before = fixture.CaptureMutationRoots(protectedPaths);
            selection = await WoundApplicationService.ConfirmAsync(files, lease, choices, choice.Value);
            Assert.True(selection.Aid!.PlayerConfirmed); fixture.AssertMutationRootsUnchanged(before);
            int[] Draw(int count) { draws++; return Enumerable.Repeat(1, count).ToArray(); }
            var stage = await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw);
            Assert.True(stage.Stage.IsStaged, stage.Stage.IssueCode);
            control = Assert.IsType<ProgressionControl>(stage.Control);
            projection = Assert.IsType<JsonObject>(stage.Stage.GmContext);
            request = JsonNode.Parse((await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            Assert.Equal(1, draws); Assert.Equal(6, request["schemaVersion"]!.GetValue<int>());
            var retainedDice = request["attempt"]!["roll"]!["values"]!.AsArray();
            Assert.NotEmpty(retainedDice); Assert.All(retainedDice, value => Assert.Equal(1, value!.GetValue<int>()));
            Assert.Equal(3, request["activeSource"]!["healingTier"]!.GetValue<int>());
            Assert.Equal(0, projection["targetNaturalHealingTier"]!.GetValue<int>());
            Assert.Equal(0L, request["activeSource"]!["complicationModifier"]!.GetValue<long>());
            Assert.True(JsonNode.DeepEquals(compensation, request["participation"]!["record"]!["compensation"]));
            before = fixture.CaptureMutationRoots(protectedPaths);
            Assert.False((await AfterlifeWoundTreatmentAuthority.StagePlayerAidTurnAsync(files, lease, scheduler,
                "session_spiritual_healing", "request_aid", 44, selection, Draw)).Stage.IsStaged);
            Assert.Equal(1, draws); fixture.AssertMutationRootsUnchanged(before);
        }
        await new LiveTurnPreparationService(files).PrepareAsync(new()
        {
            SessionId = "session_spiritual_healing", RequestId = "request_aid", TurnNumber = 44,
            CurrentRealm = "Chaos Sea", PlayerAction = WoundApplicationService.ActionFor(selection), ProgressionControl = control
        });
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            await WriteAidReportAsync(files, lease, control);
            var response = new JsonObject { ["timestamp"] = DateTime.UtcNow.ToString("o"), ["dialogueOptions"] = new JsonArray(),
                ["spiritualHealingGraphs"] = AuthorNaturalGraphs(original, projection["destinationGraphs"]!.AsArray()) };
            if (conflict != "proposed") response["metaStateUpdates"] = new JsonObject
                { ["inkFeatherChanges"] = conflict == "zero" ? new JsonObject { ["spend"] = 0 } : null };
            await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
            await WriteResidentAidMemoryAsync(files, lease, "request_aid", 44, residentProvides: true);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            var before = fixture.CaptureMutationRoots(protectedPaths);
            if (conflict != "proposed")
            {
                var issues = await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
                Assert.Contains(issues, i => i.Code == "spiritual_self_treatment_admission_invalid" &&
                    i.Actual?.Contains("competing GM currency changes must be removed", StringComparison.Ordinal) == true);
                Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(files, lease));
                fixture.AssertMutationRootsUnchanged(before); fixture.AssertResourcesUnchanged();
                wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(lease,
                    AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                Assert.DoesNotContain((await fixture.ReadHistoryAsync()).Transitions, t => t.AttemptId is not null);
                return;
            }
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(), i => i.Severity == IssueSeverity.Error);
            Assert.DoesNotContain(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease), i => i.Severity == IssueSeverity.Error);
            var admission = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            Assert.True(!admission.Any(i => i.Severity == IssueSeverity.Error), string.Join(";", admission.Select(i => i.Code + ":" + i.Actual)));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(files, lease, out _, out var ready));
            var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(ready.Plan!.SpiritualHealingContinuation);
            try
            {
                owner.RequireCurrent(files, lease);
                var soul = JsonNode.Parse((await files.ReadFileAsync(lease, AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject();
                var changed = soul.DeepClone().AsObject(); changed["inkFeathers"]!["current"] = 499;
                var refusal = Assert.Throws<InvalidDataException>(() => owner.ComposeServiceSoulAfterImage(changed));
                Assert.Contains("cannot change or create the original Ink Feather wallet", refusal.Message);
                owner.RequireCurrent(files, lease); fixture.AssertMutationRootsUnchanged(before);
                var manifest = await fixture.ReadManifestAsync(lease);
                Assert.Same(ready.Plan, await new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance)
                    .BindTo(lease).NormalizeAccumulatedStateWithPlanAsync(manifest.Files));
                var history = await fixture.ReadHistoryAsync();
                var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
                    t => t.AttemptId is not null).TransitionResult);
                Assert.True(result.AidGrantAgrees(grant)); Assert.Equal("aid", result.Mode);
                Assert.True(JsonNode.DeepEquals(request, result.ToCanonicalJson()["request"]));
                Assert.Equal(grantJson, history.Transitions.Select(t => t.TransitionResult).OfType<SpiritualHealingPersistedResult>()
                    .Single(r => r.IsNatural).ToCanonicalJson().ToJsonString());
                var calculation = SpiritualHealingOutcomeMath.Calculate(new(3, original.Severity.Rank, 1, 0, 0))!;
                Assert.Equal(SpiritualHealingResultBand.NoImprovement, calculation.ResultBand);
                var expected = SpiritualWoundRecoveryPlanner.Build(original, 0, request["attempt"]!["attemptId"]!.GetValue<string>(),
                    request["attempt"]!["cycleKey"]!.GetValue<string>(), calculation);
                Assert.Equal(expected.Select(s => (s.Kind, s.SeverityRank, s.Progress)),
                    result.Stages.Select(s => (s.Kind, s.After.Severity.Rank, s.After.Recovery.CurrentStepProgress)));
                Assert.Equal(WoundMaterializationContract.SerializeCanonical(result.Stages[^1].After),
                    WoundMaterializationContract.SerializeCanonical(Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound));
                wallet.RequirePreserved(JsonNode.Parse((await files.ReadFileAsync(lease,
                    AfterlifeWoundHealingPlanner.Continuation.ServiceSoulPath))!)!.AsObject());
                var journal = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!
                    [GuardianAbodeResidentState.ThoughtJournalProperty]!.AsArray();
                Assert.Equal(2, journal.Count); Assert.True(JsonNode.DeepEquals(grantJournal[0], journal[0]));
                Assert.Equal(44, journal[1]!["turn"]!.GetValue<int>());
                var schedule = JsonNode.Parse((await files.ReadFileAsync(lease, ProgressionScheduleService.SchedulePath))!)!;
                Assert.Equal(control.NextChaosSeaTurnOrdinal, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
                var settled = fixture.CaptureMutationRoots(protectedPaths);
                Assert.NotNull(await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control,
                    "session_spiritual_healing", "request_aid", 44, p => files.ReadFileBytesAsync(lease, p), manifest.ManifestPayloadHash, request));
                fixture.AssertMutationRootsUnchanged(settled); fixture.AssertResourcesUnchanged(); Assert.Equal(1, draws);
            }
            finally { owner.Close(); }
        }
        Assert.DoesNotContain(await new ValidationService(files, NullLogger<ValidationService>.Instance)
            .ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
    }
}
