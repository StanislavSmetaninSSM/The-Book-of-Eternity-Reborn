using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Fact]
    public async Task PlainTerminalTreatmentProjectsHealedHistoryAtTerminalSeverity()
    {
        var root = Path.Combine(Path.GetTempPath(), "boe-physical-history-plain-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
        var timer = Stopwatch.StartNew();
        try
        {
            files.EnsureDirectoryStructure();
            var scenario = CreatePlainTerminalHistoryScenario();
            // This is the same initial-DATA seeder used by ComponentSeed, not a
            // joint fixture, accepted-state owner, fabricated receipt or snapshot.
            _ = AcceptedStateFixture.SeedCanonicalComponentData(files, scenario, "player", "player_current",
                WoundCarrierCatalog.PlayerPath, initial =>
                {
                    SeedMortalMenuDisplayCarriers(initial);
                    PreparePlainTerminalHistoryProvider(initial);
                });
            output.WriteLine($"PLAIN_HISTORY initial_data_ms={timer.ElapsedMilliseconds}");
            timer.Restart();
            await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
            {
                SessionId = scenario.AcceptedState["sessionId"]!.GetValue<string>(),
                RequestId = scenario.AcceptedState["requestId"]!.GetValue<string>(),
                TurnNumber = scenario.AcceptedState["turn"]!.GetValue<int>(),
                CurrentRealm = "Mortal World",
                PlayerAction = "Field medic последовательно уменьшает тяжесть раны IV до I и исцеляет её.",
                PreGeneratedDices1d20 = scenario.AcceptedState["acceptedDice"]!.AsArray()
                    .Select(value => value!.GetValue<int>()).ToArray()
            });
            output.WriteLine($"PLAIN_HISTORY ordinary_original_ms={timer.ElapsedMilliseconds}");
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
            }.ToJsonString(), "plainHistory.context");
            Assert.True(context.IsValid, DescribeIssues(context.Issues));
            var exported = MortalWoundTreatmentAcceptedStateAuthority.ExportCurrent(files, lease,
                context.Context!, scenario.Before["woundId"]!.GetValue<string>());
            Assert.True(exported.IsValid, DescribeIssues(exported.Issues));
            var owner = Assert.IsType<MortalWoundTreatmentAcceptedStateAuthority>(exported.Authority);
            Assert.True(owner.HasCurrentAdmissionAuthority());
            Assert.False(owner.HasOriginalGuidedTreatmentInput);
            Assert.False(owner.HasOriginalMortalPreparedInput);
            var before = owner.CurrentWound;
            Assert.Equal(4, before.Severity.Rank);
            Assert.Equal("active", before.Lifecycle);
            Assert.True(WoundPlayerProjection.Visible(before.Display.Visibility));
            Assert.Single(before.Consequences.OwnedEffectSources.RootBindings);
            var history = new WoundHistoryParseResult(owner.History, []);
            var preparedRequest = MortalWoundTreatmentPlanner.PrepareGuaranteedRequest(owner, history, before,
                "operation_plain_terminal_history", scenario.RouteId, Assert.Single(owner.Binding.AcceptedEvents).EventRef);
            Assert.True(preparedRequest.IsValid, DescribeIssues(preparedRequest.Issues));
            var request = Assert.IsType<MortalWoundTreatmentAttemptRequest>(preparedRequest.Request);
            Assert.Empty(request.ResourceAuthority.Claims);
            var attempted = MortalWoundTreatmentPlanner.CreateGuaranteedAttempt(request, history, before, owner);
            Assert.Equal("Resolved", attempted.Disposition);
            Assert.Empty(attempted.Issues);
            var resolution = Assert.IsType<MortalWoundTreatmentResolution>(attempted.Resolution);
            Assert.Equal("AcceptedTerminal", resolution.AttemptDisposition);
            Assert.Equal("success", resolution.ResultCategory);
            Assert.Equal(4, resolution.DeclaredResult.Count);
            Assert.IsType<MortalWoundStabilizeOperation>(resolution.DeclaredResult[0]);
            Assert.Equal(new[] { 1, 2 }, resolution.DeclaredResult.OfType<MortalWoundReduceSeverityOperation>().Select(row => row.Steps));
            Assert.Empty(Assert.IsType<MortalWoundHealOperation>(resolution.DeclaredResult[^1]).Legacies);
            var finalization = MortalWoundTreatmentResourceComposer.Finalize(resolution);
            Assert.True(finalization.IsValid, DescribeIssues(finalization.Issues));
            Assert.Equal("not_required", finalization.Finalization!.Disposition);
            var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(lease));
            Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
            output.WriteLine($"PLAIN_HISTORY ordinary_admission_ms={timer.ElapsedMilliseconds}");
            timer.Restart();
            AssertPhysicalHistoryFactoryControls(files, lease, owner, request, resolution);
            var composed = WoundAcceptedTurnPlanner.ComposeMortalWoundTreatmentPublication(files, lease,
                new GameResponse(), owner, request, resolution);
            Assert.True(composed.IsValid, "PLAIN_HISTORY publication premises: " + DescribeIssues(composed.Issues));
            Assert.Empty(composed.Issues);
            output.WriteLine($"PLAIN_HISTORY common_composition_ms={timer.ElapsedMilliseconds}");
            AssertPlainTerminalHistoryOutput(Assert.IsType<AcceptedMechanicsPlan>(composed.Plan), owner, request, resolution);
        }
        finally
        {
            // The declaration-scoped lease closes before this fixture is removed,
            // including the expected player-History assertion failure.
            Directory.Delete(root, recursive: true);
            output.WriteLine("PLAIN_HISTORY owned fixture removed: " + !Directory.Exists(root));
        }
    }

    private static ResolverScenario CreatePlainTerminalHistoryScenario()
    {
        var scenario = CreateScenario("guaranteed_current_capability_proof_stabilizes", "guaranteed");
        var wound = scenario.Before;
        wound["severity"]!["value"] = "IV";
        wound["severity"]!["rank"] = 4;
        wound["severity"]!["maximumAtCreation"] = "IV";
        wound["consequences"]!["slotBudget"] = 4;
        wound["treatment"]!["routes"]![0]!["outcomes"]![0]!["result"] = new JsonArray(
            new JsonObject { ["kind"] = "stabilize" },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 1 },
            new JsonObject { ["kind"] = "reduce_severity", ["steps"] = 2 },
            new JsonObject { ["kind"] = "heal", ["legacies"] = new JsonArray() });
        return scenario with { History = CreateCurrentWoundHistory(wound), SeedCanonicalWoundEffects = true };
    }

    private static void PreparePlainTerminalHistoryProvider(FileSystemManager files)
    {
        var path = files.ResolvePath(NpcCoreChangesContract.NpcCorePath);
        var root = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        var medic = Assert.Single(root["NPCsInScene"]!.AsArray().OfType<JsonObject>(),
            row => row["NPCId"]!.GetValue<string>() == "field_medic_01");
        var skill = Assert.Single(medic["activeSkills"]!.AsArray().OfType<JsonObject>(),
            row => row["skillId"]!.GetValue<string>() == "skill_guaranteed_care_01");
        var capability = Assert.Single(skill["mortalWoundTreatmentCapabilities"]!.AsArray().OfType<JsonObject>(),
            row => row["capabilityRef"]!.GetValue<string>() == "exact_materialized_healing_source");
        Assert.Equal(1, capability["operationLimits"]!["maximumSeverityReductionSteps"]!.GetValue<int>());
        capability["operationLimits"]!["maximumSeverityReductionSteps"] = 3;
        // Initial provider DATA is fixed before bootstrap and the real original.
        // The ordinary exporter/planner must still admit its actual capability.
        File.WriteAllText(path, root.ToJsonString());
    }

    private void AssertPlainTerminalHistoryOutput(AcceptedMechanicsPlan plan,
        MortalWoundTreatmentAcceptedStateAuthority owner, MortalWoundTreatmentAttemptRequest request,
        MortalWoundTreatmentResolution resolution,
        [System.Runtime.CompilerServices.CallerMemberName] string producer = "")
    {
        var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(plan.WoundStageBundle);
        var input = bundle.Input;
        var final = bundle.FinalPlan;
        var effects = bundle.EffectBatchPlan.EffectPlan;
        Assert.Null(bundle.PreparedPlan.JointRepairContinuationAuthority);
        Assert.True(WoundAcceptedTurnPlanner.TryReadTerminalTreatment(bundle.PreparedPlan, out var terminal));
        Assert.True(EffectAcceptedTurnPlanner.TryReadMortalTreatmentExecution(bundle.PreparedPlan, effects, out var stages));
        Assert.Equal(2, stages.Count);
        Assert.Empty(terminal.Legacies.EffectOperationBatches);
        Assert.Equal(request.RequestFingerprint, terminal.Request.RequestFingerprint);
        Assert.Equal(resolution.ResultFingerprint, terminal.Resolution.ResultFingerprint);
        Assert.Equal(owner.CurrentWound.Severity.Rank, stages[0].Before.Severity.Rank);
        Assert.Equal(1, stages[0].After.Severity.Rank);
        Assert.Equal(1, stages[1].Before.Severity.Rank);
        if (resolution.DeclaredResult.Any(operation => operation is MortalWoundStabilizeOperation))
        {
            Assert.Equal("stabilization", stages[1].Before.Recovery.RecoveryAnchor!.AnchorKind);
            Assert.Equal(owner.CurrentGameMinute, stages[1].Before.Recovery.RecoveryAnchor!.AnchorMinute);
            Assert.Equal(stages[0].After.LastTransition.TransitionId, stages[1].Before.Recovery.RecoveryAnchor!.AnchorTransitionId);
        }
        Assert.Equal("healed", stages[1].After.Lifecycle);
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(request.RouteSourceWound),
            WoundMaterializationContract.SerializeCanonical(stages[0].Before));
        Assert.Equal(WoundMaterializationContract.SerializeCanonical(stages[0].After),
            WoundMaterializationContract.SerializeCanonical(stages[1].Before));
        Assert.All(stages, stage =>
        {
            Assert.Equal(owner.CurrentWound.Owner, stage.Before.Owner);
            Assert.Equal(owner.CurrentWound.Owner, stage.After.Owner);
            Assert.Equal(request.Coordinates.WoundId, stage.Before.WoundId);
            Assert.Equal(request.Coordinates.WoundId, stage.After.WoundId);
        });
        var historyRoot = final.HistoryAfterImage;
        var parsedHistory = WoundHistoryState.Parse(historyRoot.ToJsonString(), WoundHistoryState.HistoryPath);
        Assert.True(parsedHistory.IsValid, DescribeIssues(parsedHistory.Issues));
        var history = parsedHistory.State!;
        var originalRows = input.PreTurnHistory["transitions"]!.AsArray();
        var finalRows = historyRoot["transitions"]!.AsArray();
        Assert.Equal(originalRows.Count + 2, finalRows.Count);
        for (var index = 0; index < originalRows.Count; index++)
            Assert.True(JsonNode.DeepEquals(originalRows[index], finalRows[index]));
        var rows = history.Transitions.Skip(originalRows.Count).ToArray();
        Assert.Equal(new[] { "treat", "heal" }, rows.Select(row => row.Kind));
        Assert.Equal(owner.History.NextOrdinal + 2, history.NextOrdinal);
        var heal = Assert.IsType<MortalWoundHealOutcomeIntent>(resolution.OutcomeIntents[^1]);
        for (var index = 0; index < rows.Length; index++)
        {
            var row = rows[index]; var stage = stages[index];
            Assert.Equal(owner.History.NextOrdinal + index, row.Ordinal);
            Assert.Equal(owner.CurrentWound.LastTransition.Ordinal + index + 1, row.WoundTransitionOrdinal);
            Assert.Equal(request.Coordinates.Turn, row.Turn);
            Assert.Equal(request.Coordinates.WoundId, row.WoundId);
            Assert.Equal(stage.After.LastTransition.TransitionId, row.TransitionId);
            Assert.Equal(stage.OperationKey, row.OperationKey);
            Assert.Equal(stage.EventRef, row.EventRef);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.Before), row.BeforeFingerprint);
            Assert.Equal(WoundIdentityState.ComputeSemanticFingerprint(stage.After), row.AfterFingerprint);
            Assert.Equal(request.RequestFingerprint, row.SourceFingerprint);
            Assert.Equal(index == 1, row.Terminal);
        }
        Assert.Equal(request.Coordinates.OperationKey, rows[0].OperationKey);
        Assert.Equal(request.Coordinates.EventRef, rows[0].EventRef);
        Assert.Equal(request.Coordinates.AttemptId, rows[0].AttemptId);
        Assert.Equal(heal.HealChildCoordinates.TransitionId, rows[1].TransitionId);
        Assert.Equal(heal.HealChildCoordinates.OperationKey, rows[1].OperationKey);
        Assert.Equal(heal.HealChildCoordinates.EventRef, rows[1].EventRef);
        Assert.Equal(rows[0].AfterFingerprint, rows[1].BeforeFingerprint);
        Assert.Null(rows[1].AttemptId);
        Assert.Null(rows[1].TransitionResult);
        var persisted = Assert.IsType<MortalWoundTreatmentPersistedResult>(rows[0].TransitionResult);
        Assert.False(persisted.HasGuidedPublicationWitness);
        var historicalEnvelope = persisted.ToCanonicalJson();
        Assert.Equal(3, historicalEnvelope["schemaVersion"]!.GetValue<int>());
        Assert.Equal(owner.CurrentGameMinute, historicalEnvelope["publicationMinute"]!.GetValue<long>());
        Assert.Equal(owner.BindingFingerprint, historicalEnvelope["bindingFingerprint"]!.GetValue<string>());
        Assert.Equal(owner.WoundSourcePath, historicalEnvelope["sourcePath"]!.GetValue<string>());
        Assert.Equal(terminal.Outcome.Fingerprint, historicalEnvelope["outcomePreparationFingerprint"]!.GetValue<string>());
        Assert.Equal(request.RequestFingerprint, persisted.Request.RequestFingerprint);
        Assert.Equal(MortalWoundTreatmentReceipt.Create(resolution).ReceiptFingerprint, persisted.Receipt.ReceiptFingerprint);
        Assert.True(JsonNode.DeepEquals(terminal.PersistedResult.ToCanonicalJson(), persisted.ToCanonicalJson()));

        var carriers = input.PreTurnCarriers;
        foreach (var contribution in final.CarrierContributions)
        {
            var collection = WoundCarrierCollectionAuthority.Resolve(carriers, contribution.Owner);
            foreach (var mutation in contribution.Mutations)
                Assert.Null(AcceptedMechanicsCarrierAssembler.ApplyMutation(collection, contribution.Owner, mutation));
        }
        Assert.NotNull(carriers.PlayerWounds); Assert.NotNull(carriers.NpcWounds);
        Assert.NotNull(carriers.EnemyCombatants); Assert.NotNull(carriers.AllyCombatants); Assert.NotNull(carriers.AfterlifeProfiles);
        var catalog = WoundCarrierCatalog.Build(carriers);
        Assert.Empty(catalog.Issues);
        Assert.DoesNotContain(catalog.Occurrences, row => row.WoundId == request.Coordinates.WoundId);
        var parsedIdentity = WoundIdentityState.Parse(final.IdentityIndexAfterImage.ToJsonString(), WoundIdentityState.StatePath);
        Assert.True(parsedIdentity.IsValid, DescribeIssues(parsedIdentity.Issues));
        var identities = parsedIdentity.State!;
        Assert.Empty(history.ValidateAgreement(identities, catalog));
        Assert.True(identities.TryGetEntry(request.Coordinates.WoundId, out var healed));
        Assert.Equal("healed", healed.Status);
        Assert.Equal(rows[1].TransitionId, healed.TerminalTransitionId);
        Assert.Equal(rows[1].AfterFingerprint, healed.SemanticFingerprint);
        Assert.Equal(rows[1].WoundTransitionOrdinal, healed.LastTransitionOrdinal);
        Assert.Equal("mortal_world", healed.Realm); Assert.Equal("player", healed.OwnerKind);
        Assert.Equal("player_current", healed.OwnerId); Assert.Equal(WoundCarrierCatalog.PlayerPath, healed.CarrierPath);

        var effectCarriers = effects.ResourceTriggerCarriers;
        var effectCatalog = EffectCarrierCatalog.Build(effectCarriers);
        var mechanics = EffectMechanicsSnapshot.Build(effectCatalog, effects.IdentityIndexAfterImage,
            effects.SkillScopeAuthority, out var effectIdentities);
        Assert.True(mechanics.IsAccepted, DescribeIssues(mechanics.Issues));
        Assert.NotNull(effectIdentities);
        var application = Assert.Single(stages[0].Applications);
        var root = Assert.Single(terminal.TreatmentBatch!.RootApplications);
        var prior = Assert.Single(owner.CurrentWound.Consequences.OwnedEffectSources.RootBindings);
        Assert.Equal("wound", application.SourceKey.Kind);
        Assert.Equal(owner.CurrentWound.WoundId, application.SourceKey.SourceId);
        Assert.Equal(root.ApplicationRef, application.ApplicationRef);
        Assert.Equal(root.ExpectedSourceKey, application.SourceKey);
        Assert.Equal(root.ExpectedTargetKey, application.TargetKey);
        Assert.Equal(root.ExpectedCarrierCoordinate, application.CarrierCoordinate);
        Assert.Equal(request.Coordinates.EventRef, application.CausalEventRef);
        Assert.Equal(prior.EffectId, root.PriorRootEffectId);
        Assert.NotEqual(prior.EffectId, application.EffectId);
        Assert.Equal(application.EffectId, Assert.Single(stages[1].Before.Consequences.OwnedEffectSources.RootBindings).EffectId);
        Assert.True(effectIdentities!.TryGetEntry(application.EffectId, out var allocated));
        Assert.Equal("expired", allocated.State);
        Assert.Equal(request.Coordinates.Turn, allocated.CreatedAtTurn);
        Assert.Equal(new[] { "create", "expire" }, allocated.Transitions.Select(row => row.Kind));
        var created = allocated.Transitions[0]; var expired = allocated.Transitions[1];
        Assert.Equal(application.CreateTransitionId, created.TransitionId);
        Assert.Equal(application.CreatedEventRef, created.EventRef);
        Assert.Equal(WoundEffectOperationEventRef.Create(request.Coordinates.EventRef, 1, 1, "apply"), created.EventRef);
        Assert.Equal(new[] { prior.EffectId }, created.SourceEffectIds);
        Assert.Equal(new[] { application.EffectId }, created.ResultEffectIds);
        Assert.Equal(WoundEffectOperationEventRef.Create(request.Coordinates.EventRef, 2, 1, "expire"), expired.EventRef);
        Assert.Equal(new[] { application.EffectId }, expired.SourceEffectIds);
        Assert.Empty(expired.ResultEffectIds);
        Assert.All(allocated.Transitions, row => { Assert.Equal(request.Coordinates.Turn, row.Turn); Assert.Null(row.ReceiptId); });
        Assert.Equal(application.SourceKey.Realm, allocated.Realm);
        Assert.Equal(application.SourceKey.Kind, allocated.Source["kind"]!.GetValue<string>());
        Assert.Equal(application.SourceKey.SourceId, allocated.Source["sourceId"]!.GetValue<string>());
        Assert.Equal(application.SourceKey.DefinitionKey, allocated.Source["definitionKey"]!.GetValue<string>());
        Assert.Equal(application.TargetKey.Kind, allocated.Target["kind"]!.GetValue<string>());
        Assert.Equal(application.TargetKey.TargetId, allocated.Target["targetId"]!.GetValue<string>());
        Assert.True(WoundEffectTerminalOperationPlanner.TryCreateExpectedIdentityOwner(application.TargetKey,
            application.CarrierCoordinate, out var expectedOwner));
        Assert.Equal(expectedOwner, allocated.Owner);
        var definition = Assert.Single(terminal.TreatmentBatch.SourceExport.Definitions,
            row => row.DefinitionKey == application.SourceKey.DefinitionKey).Definition;
        Assert.Equal(new EffectStackCoordinate(application.SourceKey.Realm, application.TargetKey.Kind,
            application.TargetKey.TargetId, application.SourceKey.Kind, application.SourceKey.SourceId,
            definition["stacking"]!["stackKey"]!.GetValue<string>()), allocated.StackCoordinate);
        Assert.DoesNotContain(effectCatalog.Occurrences, row => row.EffectId == application.EffectId || row.EffectId == prior.EffectId);
        Assert.True(effectIdentities.TryGetEntry(prior.EffectId, out var retired));
        Assert.Equal("expired", retired.State);
        Assert.Equal(WoundEffectOperationEventRef.Create(request.Coordinates.EventRef, 1, 2, "expire"), retired.Transitions[^1].EventRef);
        Assert.Equal(2, effectIdentities.ResolveSourceGroup(new(owner.CurrentWound.Owner.Realm, "wound", owner.CurrentWound.WoundId)).Count);

        var resources = ResourceDefinitionCatalog.ParseCanonical(plan.DefinitionAfterImage.ToJsonString(), allowMissingPristine: false);
        Assert.True(resources.IsValid, DescribeIssues(resources.Issues));
        CapturePlainTerminalHistoryOutput(owner, final, carriers, effectCarriers, effects, plan, stages[1].Before, stages[1].After, producer);
        var projectionInput = new WoundPlayerProjectionInput(carriers, identities, history, mechanics,
            "mortal_world", resources.Catalog!, new Dictionary<string, string>(StringComparer.Ordinal),
            HistoricalEffects: new(effectCatalog, effectIdentities!));
        var projected = WoundPlayerProjection.Build(projectionInput, catalog);
        Assert.True(projected.IsAvailable);
        Assert.Empty(projected.Active);
        // All real ordinary producer, linkage, identity and allocation premises
        // above must pass before the missing physical History entry is causal RED.
        var entry = Assert.Single(projected.History);
        Assert.Equal(stages[1].After.Display.Name, entry.Name);
        Assert.Equal("I", entry.Severity);
        Assert.Equal("Рана зажила", entry.Summary);
        if (resolution.DeclaredResult.Any(operation => operation is MortalWoundStabilizeOperation))
            AssertPlainTerminalHistoryRefusals(projectionInput, catalog, final.HistoryAfterImage,
                final.IdentityIndexAfterImage, effects.IdentityIndexAfterImage);
        AssertPlainTerminalHistoryAuditSuffix(projectionInput, catalog, entry, stages[1].Before);
    }

    private void CapturePlainTerminalHistoryOutput(MortalWoundTreatmentAcceptedStateAuthority owner,
        WoundAcceptedTurnPlan final, WoundCarrierCatalogInput carriers, EffectCarrierCatalogInput effectCarriers,
        EffectAcceptedTurnPlan effects, AcceptedMechanicsPlan plan,
        WoundMaterializationEnvelope terminalBefore, WoundMaterializationEnvelope terminalAfter, string producer)
    {
        var directory = Environment.GetEnvironmentVariable("BOE_TEST_DIAGNOSTIC_ROOT") is null
            ? Path.Combine(Path.GetTempPath(), "boe-physical-history-captures")
            : Path.GetFullPath(TestDiagnosticPaths.Root);
        var repository = Path.GetFullPath(TestRepoPaths.RepoRoot).TrimEnd(Path.DirectorySeparatorChar);
        var comparison = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        if (directory.Equals(repository, comparison) || directory.StartsWith(repository + Path.DirectorySeparatorChar, comparison))
            throw new InvalidDataException("Plain History output capture requires BOE_TEST_DIAGNOSTIC_ROOT outside the checkout.");
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "plain-terminal-history-composed-" + Guid.NewGuid().ToString("N") + ".json");
        var capture = new JsonObject
        {
            ["captureVersion"] = 1, ["state"] = "composed_not_published",
            ["producer"] = producer,
            ["bindingFingerprint"] = owner.BindingFingerprint, ["sourcePath"] = owner.WoundSourcePath,
            ["currentMinute"] = owner.CurrentGameMinute,
            ["sourceBefore"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(owner.CurrentWound)),
            ["terminalBefore"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(terminalBefore)),
            ["terminalAfter"] = JsonNode.Parse(WoundMaterializationContract.SerializeCanonical(terminalAfter)),
            ["history"] = final.HistoryAfterImage, ["identities"] = final.IdentityIndexAfterImage,
            ["woundCarriers"] = new JsonObject
            {
                ["player"] = carriers.PlayerWounds?.DeepClone(), ["npcs"] = carriers.NpcWounds?.DeepClone(),
                ["enemies"] = carriers.EnemyCombatants?.DeepClone(), ["allies"] = carriers.AllyCombatants?.DeepClone(),
                ["afterlife"] = carriers.AfterlifeProfiles?.DeepClone()
            },
            ["effectCarriers"] = new JsonObject
            {
                ["player"] = effectCarriers.PlayerEffects?.DeepClone(), ["npcs"] = effectCarriers.NpcEffects?.DeepClone(),
                ["enemies"] = effectCarriers.EnemyCombatants?.DeepClone(), ["allies"] = effectCarriers.AllyCombatants?.DeepClone(),
                ["afterlife"] = effectCarriers.AfterlifeProfiles?.DeepClone(), ["conflict"] = effectCarriers.SpiritualConflict?.DeepClone()
            },
            ["effectIdentities"] = effects.IdentityIndexAfterImage,
            ["resourceDefinitions"] = plan.DefinitionAfterImage
        };
        File.WriteAllText(path, capture.ToJsonString());
        output.WriteLine("PLAIN_HISTORY composed DATA capture (not published or completed): " + path);
    }
}
