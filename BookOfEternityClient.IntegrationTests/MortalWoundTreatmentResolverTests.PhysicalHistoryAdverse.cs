using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    /// <summary>
    /// Uses one ordinary procedure owner to compose a genuine adverse prefix
    /// and terminal heal. The shared plain oracle checks its actual after-images.
    /// </summary>
    [Fact]
    public async Task OrdinaryProcedureAdversePrefixProjectsHealedHistory()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-physical-history-adverse-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var timer = Stopwatch.StartNew();
        try
        {
            files.EnsureDirectoryStructure();
            var scenario = CreateAdverseTerminalHistoryScenario();
            _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current",
                WoundCarrierCatalog.PlayerPath, SeedMortalMenuDisplayCarriers);
            output.WriteLine($"ADVERSE_HISTORY initial_data_ms={timer.ElapsedMilliseconds}");
            timer.Restart();
            await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                CurrentRealm = "Mortal World",
                PlayerAction = "Field medic проводит процедуру: рана ухудшается, затем её тяжесть снижается до I и она исцеляется.",
                PreGeneratedDices1d20 = scenario.AcceptedState["acceptedDice"]!.AsArray()
                    .Select(value => value!.GetValue<int>()).ToArray()
            });
            output.WriteLine($"ADVERSE_HISTORY ordinary_original_ms={timer.ElapsedMilliseconds}");
            timer.Restart();
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var original = JsonSerializer.Deserialize<TurnRequest>(Assert.IsType<string>(
                await files.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath)),
                LiveTurnPreparationService.ManifestJsonOptions)!;
            Assert.Null(original.MortalTreatment);
            Assert.Null(original.MortalDiagnosis);
            Assert.Null(original.SpiritualHealing);
            Assert.Null(await files.ReadFileAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
            var context = MortalWoundTreatmentAuthority.ParseContext(new JsonObject
            {
                ["schemaVersion"] = 1, ["realm"] = "mortal_world", ["targetKind"] = "player",
                ["targetId"] = "player_current", ["providerKind"] = "npc", ["providerId"] = "field_medic_01",
                ["currentLocationId"] = "loc_field_clinic_001"
            }.ToJsonString(), "adverseHistory.context");
            Assert.True(context.IsValid, DescribeIssues(context.Issues));
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease,
                context.Context!, scenario.Before["woundId"]!.GetValue<string>());
            Assert.True(exported.IsValid, DescribeIssues(exported.Issues));
            var owner = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
            Assert.True(owner.HasCurrentAdmissionAuthority());
            Assert.False(owner.HasOriginalGuidedTreatmentInput);
            Assert.False(owner.HasOriginalMortalPreparedInput);
            var before = owner.CurrentWound;
            Assert.Equal(3, before.Severity.Rank);
            Assert.Equal("active", before.Lifecycle);
            Assert.True(WoundPlayerProjection.Visible(before.Display.Visibility));
            Assert.Single(before.Consequences.OwnedEffectSources.RootBindings);
            var history = new WoundHistoryParseResult(owner.History, []);
            var prepared = MortalWoundTreatmentPlanner.PrepareProcedureRequest(owner, history, before,
                scenario.OperationKey, scenario.RouteId, Assert.Single(owner.Binding.AcceptedEvents).EventRef);
            Assert.True(prepared.IsValid, DescribeIssues(prepared.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(prepared.Request);
            var procedure = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
            try
            {
                Assert.Equal(new[] { 13 }, procedure.SourceRolls);
                Assert.True(owner.HasLiveProcedureReservationAgreement(procedure));
                Assert.True(request.HasNewProvisionalClaimCleanup);
                Assert.Empty(request.ResourceAuthority.Claims);
                Assert.Equal("not_required", request.ResourceAuthority.ReservationDisposition);
                Assert.Null(request.ResourceAuthority.ReservationId);
                var attempted = MortalWoundTreatmentPlanner.CreateProcedureAttempt(request, history, before, owner);
                Assert.Equal("Resolved", attempted.Disposition);
                Assert.Empty(attempted.Issues);
                var resolution = Assert.IsType<MortalWoundTreatmentResolution>(attempted.Resolution);
                Assert.Equal("AcceptedTerminal", resolution.AttemptDisposition);
                Assert.Equal("partial_success", resolution.ResultCategory);
                Assert.Equal("None", resolution.RouteCompletion);
                Assert.Equal(4, resolution.DeclaredResult.Count);
                var policyOperation = Assert.IsType<MortalWoundApplyDeteriorationOperation>(resolution.DeclaredResult[0]);
                Assert.Equal("policy_preparation", policyOperation.PolicyRef);
                var policyIntent = Assert.IsType<MortalWoundApplyDeteriorationOutcomeIntent>(resolution.OutcomeIntents[0]);
                Assert.True(policyIntent.TryGetPreparedDeterioration(request, policyOperation, out var policy));
                Assert.NotNull(policy);
                Assert.Equal(owner.WoundSourcePath, policy.WoundSourcePath);
                Assert.Equal(0, policy.OperationOrdinal);
                Assert.Equal(request.RequestFingerprint, policy.RequestFingerprint);
                Assert.Equal(MortalWoundDeteriorationResultKind.IncreaseSeverity, policy.Policy.ResultKind);
                Assert.Equal(new[] { 1, 2 }, resolution.DeclaredResult.OfType<MortalWoundReduceSeverityOperation>().Select(row => row.Steps));
                Assert.Empty(Assert.IsType<MortalWoundHealOperation>(resolution.DeclaredResult[^1]).Legacies);
                var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
                Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
                Assert.Equal("not_required", finalization.Finalization!.Disposition);
                var response = await StageAndConfirmAdverseHistoryRequestAsync(files, lease, owner,
                    request, resolution, finalization.Finalization);
                Assert.True(owner.HasLiveProcedureReservationAgreement(procedure));
                var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
                Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
                Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
                output.WriteLine($"ADVERSE_HISTORY ordinary_admission_ms={timer.ElapsedMilliseconds}");
                timer.Restart();
                var composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(files, lease,
                    response, owner, request, resolution);
                Assert.True(composed.IsValid, "ADVERSE_HISTORY publication premises: " + DescribeIssues(composed.Issues));
                Assert.Empty(composed.Issues);
                output.WriteLine($"ADVERSE_HISTORY common_composition_ms={timer.ElapsedMilliseconds}");
                var plan = Assert.IsType<AcceptedMechanicsPlan>(composed.Plan);
                AssertPlainTerminalHistoryOutput(plan, owner, request, resolution);
                AssertAdverseHistoryMetadataRefusals(plan, request);
            }
            finally
            {
                // This case composes, but never publishes. Use the request's
                // genuine combined rollback for its procedure draw and resources.
                var rolledBack = request.RollbackNewProvisionalClaims(owner);
                if (!rolledBack)
                {
                    var resources = MortalWoundTreatmentResourceComposer.ReleaseTreatmentResources(files, lease,
                        [request], "cancelled");
                    var diceReleased = !owner.HasLiveProcedureReservationAgreement(procedure) ||
                        procedure.ReleaseProvisionalReservations(owner);
                    Assert.True(resources.IsValid, DescribeIssues(resources.Issues));
                    Assert.True(diceReleased, "The ordinary procedure draw must be released before fixture closing.");
                }
                Assert.False(owner.HasLiveProcedureReservationAgreement(procedure));
                output.WriteLine("ADVERSE_HISTORY procedure/resource claims closed; combined_rollback=" + rolledBack);
            }
        }
        finally
        {
            Directory.Delete(root, recursive: true);
            output.WriteLine("ADVERSE_HISTORY owned fixture removed: " + !Directory.Exists(root));
        }
    }

    /// <summary>
    /// Persists the actual ordinary command before confirming its existing
    /// reservation. Empty resource claims still require the procedure hold.
    /// </summary>
    private static async Task<GameResponse> StageAndConfirmAdverseHistoryRequestAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, MortalWoundTreatmentAcceptedStateAuthority owner,
        MortalWoundTreatmentAttemptRequest request, MortalWoundTreatmentResolution resolution,
        MortalWoundTreatmentResourceFinalization finalization)
    {
        const string scene = "Процедура сначала ухудшила рану, затем снизила её тяжесть до I и завершилась исцелением.";
        var response = new GameResponse { Response = scene };
        var command = WoundResponseInputComposer.ComposeMortalWoundTreatmentCommandRoot(owner.Binding, resolution, scene);
        var originalCommand = command.ToJsonString();
        var parsed = WoundResponseInputComposer.ParseCommandRoot(JsonSerializer.SerializeToElement(command));
        Assert.True(parsed.Success, DescribeIssues(parsed.Issues));
        var composition = WoundResponseInputComposer.RecomposeCommandRoot(owner.Binding, parsed,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composition.Success, DescribeIssues(composition.Issues));
        Assert.True(JsonNode.DeepEquals(command, composition.CommandRoot));
        var staged = Assert.IsType<byte[]>(await StateDistributor.StageAcceptedWoundCommandAsync(files, lease,
            response, composition));
        var readback = Assert.IsType<byte[]>(await files.ReadFileBytesAsync(lease, AcceptedMechanicsPlan.WoundCommandPath));
        Assert.Equal(staged, readback);
        // The normal text writer may emit a UTF-8 BOM. Decode the already
        // byte-compared DATA using the same BOM-aware text semantics, without IO.
        using var readbackStream = new MemoryStream(readback, writable: false);
        using var readbackText = new StreamReader(readbackStream, System.Text.Encoding.UTF8,
            detectEncodingFromByteOrderMarks: true);
        Assert.True(JsonNode.DeepEquals(command, JsonNode.Parse(readbackText.ReadToEnd())));
        Assert.Equal(originalCommand, command.ToJsonString());
        var confirmed = MortalWoundTreatmentResourceComposer.ConfirmPersistedTreatmentResources(files, lease, [request]);
        Assert.True(confirmed.IsValid, DescribeIssues(confirmed.Issues));
        var hold = AcceptedTurnAuthorityRegistry.InspectConfirmedMortalWoundTreatmentResourceClaim(files, lease,
            request, finalization);
        Assert.True(hold.IsValid, DescribeIssues(hold.Issues));
        Assert.Equal(MortalWoundTreatmentResourceReservationState.ConfirmedHeld, hold.State);
        Assert.True(owner.HasCurrentAdmissionAuthority());
        Assert.False(owner.HasOriginalGuidedTreatmentInput);
        // Narrative belongs to the exact staged command. The common publisher
        // accepts the mechanics-only envelope, as the ordinary production path does.
        var mechanics = new GameResponse();
        Assert.Empty(WoundAcceptedTurnPlanner.ValidateMortalTreatmentResponseShape(mechanics));
        return mechanics;
    }

    /// <summary>Creates only initial canonical DATA, before bootstrap and the ordinary original.</summary>
    private static ResolverScenario CreateAdverseTerminalHistoryScenario()
    {
        var scenario = CreateScenario("procedure_normal_uses_lowest_free_die", "procedure");
        scenario.AcceptedState["acceptedDice"] = new JsonArray(13, 7);
        var wound = scenario.Before;
        wound["severity"]!["value"] = "III";
        wound["severity"]!["rank"] = 3;
        wound["severity"]!["maximumAtCreation"] = "III";
        wound["consequences"]!["slotBudget"] = 3;
        wound["recovery"]!["deteriorationPolicy"] = new JsonObject
        {
            ["policyRef"] = "policy_preparation", ["unmetConditions"] = new JsonArray("not_stabilized"),
            ["graceMinutes"] = 30L, ["cadenceMinutes"] = 10L,
            ["result"] = new JsonObject { ["kind"] = "increase_severity" }
        };
        var route = wound["treatment"]!["routes"]![0]!.AsObject();
        var skillRequirement = route["requirements"]![1]!.DeepClone();
        route["requirements"] = new JsonArray(skillRequirement);
        route["resourcePolicy"] = Policy(new JsonArray(), new JsonArray());
        route["resolution"]!["modifierSource"]!["requirementIndex"] = 0;
        var partial = Assert.Single(route["outcomes"]!.AsArray().OfType<JsonObject>(),
            row => row["category"]!.GetValue<string>() == "partial_success");
        partial["result"] = new JsonArray(
            new JsonObject { ["kind"] = "apply_deterioration", ["policyRef"] = "policy_preparation" },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
            new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        return scenario with
        {
            OperationKey = "operation_plain_adverse_terminal_history", ExpectedCategory = "partial_success",
            ExpectedNaturalRoll = 13, ExpectedIntentCount = 4,
            History = CreateCurrentWoundHistory(wound), SeedCanonicalWoundEffects = true
        };
    }

    /// <summary>
    /// Re-seals only two negative outer metadata values after the genuine
    /// positive projection. The exact inner receipt and all positive DATA remain unchanged.
    /// </summary>
    private void AssertAdverseHistoryMetadataRefusals(AcceptedMechanicsPlan plan,
        MortalWoundTreatmentAttemptRequest request)
    {
        var final = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle).FinalPlan;
        var history = final.HistoryAfterImage;
        var historyBefore = history.ToJsonString();
        JsonObject Primary(JsonObject root) => Assert.Single(root["transitions"]!.AsArray().OfType<JsonObject>(),
            row => row["operationKey"]?.GetValue<string>() == request.Coordinates.OperationKey);
        var baseline = Primary(history)["transitionResult"]!.AsObject();
        Assert.Equal(3, baseline["schemaVersion"]!.GetValue<int>());
        var innerBefore = baseline["terminal"]!.ToJsonString();
        var originalPreparation = baseline["outcomePreparationFingerprint"]!.GetValue<string>();
        foreach (var field in new[] { "bindingFingerprint", "sourcePath" })
        {
            var timer = Stopwatch.StartNew();
            var changed = history.DeepClone().AsObject();
            var result = Primary(changed)["transitionResult"]!.AsObject();
            var replacement = field == "bindingFingerprint"
                ? WoundAcceptedTurnFingerprintWriter.Compute(new[] { "negative_adverse_history_binding", request.RequestFingerprint })
                : baseline[field]!.GetValue<string>() + ".foreign_history_source";
            Assert.NotEqual(baseline[field]!.GetValue<string>(), replacement);
            result[field] = replacement;
            result["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(result);
            Assert.Equal(innerBefore, result["terminal"]!.ToJsonString());
            Assert.Equal(originalPreparation, result["outcomePreparationFingerprint"]!.GetValue<string>());
            Assert.Equal(WoundHistoryState.ComputeTransitionResultFingerprint(result), result["resultFingerprint"]!.GetValue<string>());
            var parsed = WoundHistoryState.Parse(changed.ToJsonString(), WoundHistoryState.HistoryPath);
            Assert.False(parsed.IsValid, "Re-sealed adverse metadata must not match the retained genuine outcome preparation: " + field);
            Assert.Null(parsed.State);
            Assert.Contains(parsed.Issues, issue => issue.Code == "mortal_wound_treatment_history_result_invalid");
            output.WriteLine($"ADVERSE_HISTORY refusal={field} boundary=strict_parse historical_helper_reached=false elapsed_ms={timer.ElapsedMilliseconds}");
        }
        Assert.Equal(historyBefore, history.ToJsonString());
        Assert.Equal(innerBefore, Primary(history)["transitionResult"]!["terminal"]!.ToJsonString());
    }
}
