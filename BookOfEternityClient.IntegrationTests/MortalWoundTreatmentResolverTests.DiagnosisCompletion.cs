using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class MortalWoundTreatmentResolverTests
{
    [Theory]
    [InlineData(20, 0, false)]
    [InlineData(1, 30, true)]
    public Task PublishedDiagnosisItemCompletionUsesActualClosedWitnessAndRefusesCurrentDrift(int natural, int duration, bool fate) =>
        AssertPublishedDiagnosisCompletionAsync(natural, duration, fate, resourceCost: false);

    [Fact]
    public Task PublishedDiagnosisResourceCompletionBindsResealedSemanticsToActualSpendIdentities() =>
        AssertPublishedDiagnosisCompletionAsync(20, 30, false, resourceCost: true);

    private static async Task AssertPublishedDiagnosisCompletionAsync(int natural, int duration, bool fate, bool resourceCost)
    {
        await using var fixture = await DiagnosisOwnershipFixture.CreateAsync(natural, signedInput: true,
            duration: duration, seedFate: fate, twoMembers: true, resourceCost: resourceCost);
        var fs = fixture.Files;
        var input = fixture.Authority.ReadDiagnosisOriginalInput();
        var selected = MortalWoundDiagnosisInputCodec.Read(input);
        var requestJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, LiveTurnPreparationService.TurnRequestPath));
        var original = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var manifestJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath));
        var manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(manifestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        var authorityJson = Assert.IsType<string>(await fs.ReadFileAsync(fixture.Lease, PendingTurnSnapshotAuthority.AuthorityPath));
        var token = fixture.Authority.Binding.SnapshotToken; var generation = fixture.Authority.SessionGeneration;
        Assert.Equal(token, manifest.ManifestPayloadHash);
        var observed = PendingTurnSnapshotReader.ReadCurrent(fs, fixture.Lease,
            new[] { AcceptedMechanicsPlan.WoundCommandPath, ResourceMaterializationContract.HistoryPath });
        Assert.True(observed.Success, JsonSerializer.Serialize(observed.Issues));
        var signedInputBytes = observed.Snapshot!.ReadRequiredBytes(AcceptedMechanicsPlan.WoundCommandPath);
        Assert.True(JsonNode.DeepEquals(input, JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(
            signedInputBytes))));
        Assert.True(MortalWoundDiagnosisInputCodec.IsBound(input,
            new(original.SessionId, original.RequestId, original.TurnNumber, original.ProgressionControl!)));
        var admission = await WoundApplicationService.ResolveMortalDiagnosisInputAsync(fs, fixture.Lease, selected.Frame);
        var request = admission.Request;
        var check = Assert.IsType<MortalWoundProcedureCheckAuthority>(request.ModeAuthority);
        Assert.Equal(fate, check.PreparedCriticalReaction is not null);
        var originalRequest = WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString();
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync(fixture.Lease));
        Assert.Empty(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(fixture.Lease));
        var response = resourceCost ? new GameResponse { RemoveActiveSkills = Array.Empty<string>(), NPCInventoryUpdates = Array.Empty<JsonElement>() } : new GameResponse();
        var common = await WoundAcceptedTurnPlanner.ComposeMortalDiagnosisPublicationAsync(fs, fixture.Lease, response, admission.Owner, request);
        Assert.True(common.IsValid, JsonSerializer.Serialize(common.Issues));
        var publicationAuthority = Assert.IsType<MortalWoundTreatmentResourcePublicationAuthority>(common.Plan!.TreatmentResourcePublicationAuthority);
        if (resourceCost)
        {
            var bundle = Assert.IsType<AcceptedMechanicsWoundStageBundle>(common.Plan.WoundStageBundle);
            var prepared = bundle.PreparedPlan;
            Assert.NotNull(prepared.MortalDiagnosisAuthority);
            Assert.Null(prepared.TreatmentContinuationAuthority);
            Assert.Same(prepared.MortalDiagnosisAuthority, bundle.MortalProcedureContinuation);
            Assert.Same(bundle.MortalProcedureContinuation, publicationAuthority.ContinuationAuthority);
            Assert.True(publicationAuthority.HasValidSeal());
            Assert.Equal(MortalItemNpcTradeTailDisposition.SkipUntouchedTreatmentContinuation,
                publicationAuthority.NpcTradeDisposition);

            // The sealed resource-only plan owns no NPC change. Its exact full
            // root must survive the same tail used by real normalization below.
            var baseline = publicationAuthority.ItemPublicationBaseline;
            var npcBaseline = Assert.IsType<JsonObject>(baseline.FinalCarrierRoots[NpcCoreChangesContract.NpcCorePath]);
            var npcBefore = publicationAuthority.NpcCoreBackupRoot;
            var npc = Assert.IsType<JsonObject>(Assert.Single(npcBefore["NPCsInScene"]!.AsArray()));
            Assert.False(npcBefore.ContainsKey("UpdateNPCs"));
            Assert.False(npcBefore.ContainsKey(NpcTradeRequestState.UpdateReceiptsProperty));
            Assert.False(npc.ContainsKey(NpcTradeRequestState.ReceiptsProperty));
            Assert.True(JsonNode.DeepEquals(npcBefore, npcBaseline));
            var skipped = MortalItemPublicationTailTransforms.NpcTrade(npcBefore, npcBefore,
                publicationAuthority.NpcTradeDisposition);
            Assert.True(JsonNode.DeepEquals(npcBefore, skipped));

            var expectedApplied = npcBefore.DeepClone().AsObject();
            expectedApplied["NPCsInScene"]![0]![NpcTradeRequestState.ReceiptsProperty] = new JsonArray();
            var applied = MortalItemPublicationTailTransforms.NpcTrade(npcBefore, npcBefore,
                MortalItemNpcTradeTailDisposition.Apply);
            Assert.True(JsonNode.DeepEquals(expectedApplied, applied));
            Assert.False(JsonNode.DeepEquals(npcBaseline, applied));
            Assert.True(JsonNode.DeepEquals(npcBefore, npcBaseline));
        }
        var expectedSemantic = publicationAuthority.SemanticFingerprint;
        var expectedSeed = publicationAuthority.IdentitySeed;
        await fixture.ReleaseLeaseAsync();
        var normalizer = new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance);
        var published = await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(fs, normalizer, validator,
            new Dictionary<string, string>(StringComparer.Ordinal));
        var transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
        try
        {
            Assert.DoesNotContain(published.Issues, issue => issue.Severity == IssueSeverity.Error);
            var advanced = await transaction.AdvancePublishedAgreementWithProgressionAsync(fs,
                new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance), selected.Frame.Progression);
            Assert.True(advanced.IsValid, JsonSerializer.Serialize(advanced.Issues));
            var completed = await transaction.CompleteAsync(fs);
            Assert.True(completed.IsValid, JsonSerializer.Serialize(completed.Issues));
            Assert.Empty(completed.Issues); Assert.Equal(1, completed.ChangedCount);
            Assert.Equal(MortalWoundTreatmentPublicationTransactionOutcome.Finalized, completed.Outcome);
        }
        finally { await ((IAsyncDisposable)transaction).DisposeAsync(); }
        await fixture.AcquireLeaseAsync();
        var before = fixture.CaptureJson();
        var current = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        async Task<byte[]?> Read(string path)
        {
            counts[path] = counts.GetValueOrDefault(path) + 1;
            var bytes = await fs.ReadFileBytesAsync(fixture.Lease, path);
            current[path] = bytes?.ToArray(); return bytes;
        }
        var exact = await MortalWoundDiagnosisCompletion.RequirePublishedAsync(original, token, generation, selected.Selection, input, Read);
        Assert.All(counts.Values, count => Assert.Equal(1, count));
        Assert.Equal(expectedSemantic, exact.ReconstructPublicationSemantic());
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(exact.Resolution).Finalization!;
        Assert.Equal(expectedSeed, MortalWoundTreatmentResourcePublicationAuthority.ComputeIdentitySeed(exact.Source.AcceptedStateFingerprint,
            exact.Request.RequestFingerprint, exact.Resolution.ResultFingerprint, finalization.FinalizationFingerprint, exact.ReconstructPublicationSemantic()));
        Assert.Equal(2, exact.Recoveries.Length);
        Assert.NotEqual(exact.Request.Coordinates.WoundId, exact.Recoveries[0].Original.WoundId);
        Assert.Equal(resourceCost ? 2 : 1, finalization.Consumptions.Count);
        Assert.All(finalization.Consumptions, consumption => Assert.Equal(resourceCost ? "resource_quantity" : "item_quantity", consumption.Kind));

        // Native original admission above is genuine. This is detached delivery
        // DATA for the same shared predicate, never a fabricated accepted receipt
        // or a claim that an Engine terminal/browser phase has executed.
        var binding = new PendingPlayerActionService.Binding(original.RequestId, generation, original.PlayerAction,
            "browser-wound-action", selected.Selection.ToJson().ToJsonString(), selected.Selection);
        var staged = new PendingPlayerActionService.Staged(binding, requestJson, manifestJson, authorityJson,
            string.Empty, string.Empty, Convert.ToBase64String(signedInputBytes));
        counts.Clear();
        await GameEngine.RequireAcceptedSpiritualResultAsync(staged, manifest, Read);
        Assert.All(counts.Values, count => Assert.Equal(1, count));
        fixture.AssertJson(before);

        async Task Refuse(string label, Action<Dictionary<string, byte[]?>> fault)
        {
            var changed = current.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
            fault(changed);
            var error = await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
                original, token, generation, selected.Selection, input, path => Task.FromResult(changed[path])));
            Assert.False(string.IsNullOrWhiteSpace(error.Message), label);
            fixture.AssertJson(before);
        }
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
            original, "foreign_snapshot", generation, selected.Selection, input, path => Task.FromResult(current[path])));
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
            original, token, "foreign_generation", selected.Selection, input, path => Task.FromResult(current[path])));
        var foreignControl = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        foreignControl.ProgressionControl!.CurrentWorldTimeInMinutes++;
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
            foreignControl, token, generation, selected.Selection, input, path => Task.FromResult(current[path])));
        var foreignInput = MortalWoundDiagnosisInputCodec.Create(new(original.SessionId, original.RequestId,
            original.TurnNumber, foreignControl.ProgressionControl), selected.Selection, selected.Context);
        Assert.True(MortalWoundDiagnosisInputCodec.IsBound(foreignInput, new(original.SessionId, original.RequestId,
            original.TurnNumber, foreignControl.ProgressionControl)));
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
            foreignControl, token, generation, selected.Selection, foreignInput, path => Task.FromResult(current[path])));
        var foreignDice = JsonSerializer.Deserialize<TurnRequest>(requestJson, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        foreignDice.PreGeneratedDices1d20[0] = foreignDice.PreGeneratedDices1d20[0] == 20 ? 19 : 20;
        await Assert.ThrowsAsync<InvalidDataException>(() => MortalWoundDiagnosisCompletion.RequirePublishedAsync(
            foreignDice, token, generation, selected.Selection, input, path => Task.FromResult(current[path])));
        await Refuse("missing result", map => map[WoundHistoryState.HistoryPath] = null);
        await Refuse("duplicate result", map => Change(map, WoundHistoryState.HistoryPath, root =>
        {
            var rows = root["transitions"]!.AsArray(); rows.Add(rows.Single(row => row!["kind"]!.GetValue<string>() == "diagnose")!.DeepClone());
        }));
        await Refuse("changed cohort", map => ChangeResult(map, result => result["cohort"]!.AsArray().RemoveAt(0)));
        await Refuse("changed cohort order", map => ChangeResult(map, result =>
        {
            var rows = result["cohort"]!.AsArray(); var first = rows[0]!.DeepClone(); rows.RemoveAt(0); rows.Add(first);
        }));
        await Refuse("v1 has no current publication witness", map =>
        {
            ChangeResult(map, result => { result.Remove("publicationInputs"); result["schemaVersion"] = 1; });
            var historical = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(map[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
            Assert.True(historical.IsValid, JsonSerializer.Serialize(historical.Issues));
        });
        await Refuse("missing v2 witness", map => ChangeResult(map, result => result.Remove("publicationInputs")));
        await Refuse("mixed version witness", map => ChangeResult(map, result => result["schemaVersion"] = 1));
        await Refuse("malformed witness", map => ChangeResult(map, result => result["publicationInputs"]!["itemCommands"]!["UpdateInventory"] = "invalid"));
        await Refuse("extra witness fields", map => ChangeResult(map, result => result["publicationInputs"]!["foreign"] = true));
        await Refuse("current clock drift", map => Change(map, EffectAcceptedTurnInputComposer.WorldTimePath, root => root["currentTimeInMinutes"] = 999));
        await Refuse("current schedule drift", map => Change(map, ProgressionScheduleService.SchedulePath, root => root["lastUpdatedUtc"] = DateTime.UnixEpoch.ToString("O")));
        await Refuse("current resource drift", map => Change(map, ResourceMaterializationContract.StatePath,
            root => root["entries"]!.AsArray()[0]!["current"] = 0));
        await Refuse("current carrier drift", map => Change(map, WoundCarrierCatalog.PlayerPath,
            root => root["activeWounds"]!.AsArray()[0]!["display"]!["description"] = "foreign"));
        await Refuse("current item drift", map => Change(map, NpcCoreChangesContract.NpcCorePath,
            root => root["NPCsInScene"]!.AsArray()[0]!["inventory"]!.AsArray()[0]!["count"] = 999));
        await Refuse("current effect drift", map => Change(map, EffectCarrierCatalog.PlayerPath, root =>
        {
            var effect = root["activeEffects"]!.AsArray().First(row => row!["components"]!.AsArray()
                .Any(component => component!["profile"]!.GetValue<string>() == "periodic_damage"));
            effect!["components"]!.AsArray().First(component => component!["profile"]!.GetValue<string>() == "periodic_damage")!["payload"]!["amount"] = 4;
        }));
        await Refuse("reintroduced command", map => map[AcceptedMechanicsPlan.WoundCommandPath] = Encoding.UTF8.GetBytes(input.ToJsonString()));
        await Refuse("reintroduced pending work", map => map[WoundAcceptedTurnSnapshotContract.PendingResolutionPath] = Encoding.UTF8.GetBytes("{}"));
        await Refuse("reintroduced report", map => map[ProgressionScheduleService.ReportPath] = Encoding.UTF8.GetBytes("{}"));
        await Refuse("reintroduced resource command", map => map[ResourceMaterializationContract.CommandPath] = Encoding.UTF8.GetBytes("{}"));
        await Refuse("reintroduced effect command", map => map[EffectAcceptedTurnPlan.CommandPath] = Encoding.UTF8.GetBytes("{}"));
        await Refuse("uninstantiated malformed definition", map =>
        {
            Change(map, "game_state/player/skills_active.json", root => root["activeSkillChanges"]!.AsArray().Add(new JsonObject
            {
                ["skillId"] = "skill_uninstantiated_bad_definition", ["skillName"] = "Unused definition",
                ["activeEffectDefinitions"] = new JsonObject()
            }));
            var roots = CompletionSourceNodes(map);
            Assert.Empty(EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots).Issues);
            Assert.Contains(ValidationService.ValidateMortalDiagnosisEffectSources(roots), issue =>
                issue.Code == "effect_source_definition_invalid_field" && issue.FilePath.Contains("activeEffectDefinitions", StringComparison.Ordinal));
        });
        await Refuse("uninstantiated unregistered owner", map =>
        {
            Change(map, "game_state/player/skills_active.json", root => root["activeSkillChanges"]!.AsArray().Add(new JsonObject
            {
                ["skillName"] = "Unregistered unused owner", ["activeEffectDefinitions"] = new JsonArray()
            }));
            var roots = CompletionSourceNodes(map);
            Assert.Empty(EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(roots).Issues);
            Assert.Contains(ValidationService.ValidateMortalDiagnosisEffectSources(roots), issue =>
                issue.Code == "effect_source_authority_invalid_owner_identity" && issue.FilePath.Contains("activeEffectDefinitions", StringComparison.Ordinal));
        });
        if (resourceCost)
        {
            var unresolvedOwner = await WithUnresolvedCompletionOwnerAsync(current);
            await Refuse("valid ledger and composed owner omit live owner agreement", map =>
            {
                foreach (var path in new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath }) map[path] = unresolvedOwner[path];
            });
            await Refuse("resealed changed semantic", map =>
            {
                ChangeResult(map, result => result["publicationInputs"]!["skillCommands"]!.AsObject().Remove("removeActiveSkills"));
                var parsed = WoundHistoryState.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(map[WoundHistoryState.HistoryPath]!), WoundHistoryState.HistoryPath);
                Assert.True(parsed.IsValid, JsonSerializer.Serialize(parsed.Issues));
                var changed = Assert.IsType<MortalWoundDiagnosisPersistedResult>(Assert.Single(parsed.State!.Transitions,
                    row => row.Kind == "diagnose").TransitionResult);
                Assert.NotEqual(expectedSemantic, changed.ReconstructPublicationSemantic());
            });
            await Refuse("missing spend", map => Change(map, ResourceMaterializationContract.HistoryPath, root =>
            {
                var rows = root["entries"]!.AsArray(); rows.Remove(rows.First(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome"));
            }));
            await Refuse("wrong spend", map => Change(map, ResourceMaterializationContract.HistoryPath, root =>
                root["entries"]!.AsArray().First(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome")!["requestedAmount"] = 2));
            await Refuse("duplicate spend", map => Change(map, ResourceMaterializationContract.HistoryPath, root =>
            {
                var rows = root["entries"]!.AsArray(); rows.Add(rows.First(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome")!.DeepClone());
            }));
            await Refuse("wrong spend execution order", map => Change(map, ResourceMaterializationContract.HistoryPath, root =>
            {
                var rows = root["entries"]!.AsArray().Where(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome").ToArray();
                Assert.Equal(2, rows.Length);
                var first = rows[0]!["executionSequence"]!.GetValue<int>();
                rows[0]!["executionSequence"] = rows[1]!["executionSequence"]!.DeepClone(); rows[1]!["executionSequence"] = first;
            }));
            await Refuse("valid canonical wrong operation identity", map =>
            {
                Change(map, ResourceMaterializationContract.HistoryPath, root =>
                    root["entries"]!.AsArray().First(row => row!["originKind"]!.GetValue<string>() == "registered_system_outcome")!["operationId"] = "resource_operation_" + Guid.NewGuid().ToString("N"));
                var definitions = ResourceDefinitionCatalog.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(map[ResourceMaterializationContract.DefinitionsPath]!), false);
                var history = ResourceHistoryState.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(map[ResourceMaterializationContract.HistoryPath]!), definitions.Catalog!, false);
                var state = ResourceStateContract.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(map[ResourceMaterializationContract.StatePath]!), definitions.Catalog!, false);
                Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues)); Assert.True(state.IsValid);
                Assert.Empty(history.History!.ValidateStateAgreement(state.Ledger!));
            });
        }
        else await Refuse("wrong item spend identity", map => Change(map, MortalItemIdentityState.StatePath, root =>
        {
            var entry = root["entries"]!.AsArray().Single(row => row!["itemId"]!.GetValue<string>() == "sterile_thread");
            entry!["transitions"]!.AsArray()[^1]!["authorityId"] = "foreign_item_consumption";
        }));
        fixture.AssertJson(before);
        Assert.Equal(originalRequest, WoundResponseInputComposer.SerializeMortalWoundTreatmentValue(request)!.ToJsonString());
    }

    private static void Change(Dictionary<string, byte[]?> files, string path, Action<JsonObject> change)
    {
        var root = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(files[path]!))!.AsObject();
        change(root); files[path] = Encoding.UTF8.GetBytes(root.ToJsonString());
    }

    private static Dictionary<string, JsonNode?> CompletionSourceNodes(Dictionary<string, byte[]?> files) =>
        EffectAcceptedTurnInputComposer.SourceAuthorityPaths.ToDictionary(path => path,
            path => files.GetValueOrDefault(path) is { } bytes ? JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(bytes)) : null,
            StringComparer.Ordinal);

    private static async Task<Dictionary<string, byte[]?>> WithUnresolvedCompletionOwnerAsync(Dictionary<string, byte[]?> original)
    {
        var files = original.ToDictionary(pair => pair.Key, pair => pair.Value?.ToArray(), StringComparer.Ordinal);
        var historyRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(files[ResourceMaterializationContract.HistoryPath]!))!.AsObject();
        var stateRoot = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(files[ResourceMaterializationContract.StatePath]!))!.AsObject();
        var rows = historyRoot["entries"]!.AsArray();
        var initialized = rows.First(row => row!["operation"]!.GetValue<string>() == "initialize" &&
            row["coordinate"]!["resourceKey"]!.GetValue<string>() == "health")!.DeepClone().AsObject();
        const string ownerId = "npc_unresolved_completion";
        var turn = initialized["turn"]!.GetValue<int>(); var eventRef = $"turn_{turn}:completion_owner_gap";
        var transitionId = "resource_transition_" + Guid.NewGuid().ToString("N");
        initialized["coordinate"]!["ownerKind"] = "npc"; initialized["coordinate"]!["resourceOwnerId"] = ownerId;
        initialized["transitionId"] = transitionId; initialized["operationId"] = "resource_operation_" + Guid.NewGuid().ToString("N");
        initialized["eventRef"] = eventRef;
        initialized["executionSequence"] = rows.Where(row => row!["turn"]!.GetValue<int>() == turn)
            .Max(row => row!["executionSequence"]!.GetValue<int>()) + 1;
        var after = initialized["afterState"]!.AsObject();
        var live = new JsonObject
        {
            ["realm"] = "mortal_world", ["ownerKind"] = "npc", ["resourceOwnerId"] = ownerId, ["resourceKey"] = "health",
            ["current"] = after["current"]!.DeepClone(), ["maximum"] = after["maximum"]!.DeepClone(),
            ["capacityBinding"] = after["capacityBinding"]!.DeepClone(), ["state"] = after["state"]!.DeepClone(),
            ["chronology"] = new JsonObject
            {
                ["createdAtTurn"] = turn, ["createdEventRef"] = eventRef, ["lastTransitionId"] = transitionId,
                ["lastEventRef"] = eventRef, ["lastTransitionTurn"] = turn
            }
        };
        rows.Add(initialized); stateRoot["entries"]!.AsArray().Add(live);
        files[ResourceMaterializationContract.HistoryPath] = Encoding.UTF8.GetBytes(historyRoot.ToJsonString());
        files[ResourceMaterializationContract.StatePath] = Encoding.UTF8.GetBytes(stateRoot.ToJsonString());
        var definitions = ResourceDefinitionCatalog.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(files[ResourceMaterializationContract.DefinitionsPath]!), false);
        Assert.True(definitions.IsValid);
        var catalog = Assert.IsType<ResourceDefinitionCatalog>(definitions.Catalog);
        var history = ResourceHistoryState.ParseCanonical(historyRoot.ToJsonString(), catalog, false);
        var state = ResourceStateContract.ParseCanonical(stateRoot.ToJsonString(), catalog, false);
        Assert.True(history.IsValid, JsonSerializer.Serialize(history.Issues)); Assert.True(state.IsValid, JsonSerializer.Serialize(state.Issues));
        var parsedHistory = Assert.IsType<ResourceHistoryState>(history.History); var ledger = Assert.IsType<ResourceStateLedger>(state.Ledger);
        Assert.Empty(parsedHistory.ValidateStateAgreement(ledger));
        Task<string?> Read(string path) => Task.FromResult(files.GetValueOrDefault(path) is { } bytes ? CanonicalJsonUtf8.DecodeOneOptionalBom(bytes) : null);
        var rebuilt = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(catalog, Read, ledger, parsedHistory,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Assert.True(rebuilt.IsValid, JsonSerializer.Serialize(rebuilt.Issues));
        files[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] = Encoding.UTF8.GetBytes(Assert.IsType<string>(rebuilt.CanonicalAuthorityJson));
        var existing = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(catalog, Read, ledger, parsedHistory,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        Assert.True(existing.IsValid, JsonSerializer.Serialize(existing.Issues));
        Assert.Contains(existing.Authority!.ValidateCanonicalAgreement(ledger, parsedHistory), issue =>
            issue.Code == "resource_owner_unresolved" && issue.Severity == IssueSeverity.Error &&
            issue.FilePath.StartsWith("resourceState.entries[", StringComparison.Ordinal));
        // This is the unresolved-owner branch, not a claim to cover terminal-owner cleanup.
        return files;
    }

    private static void ChangeResult(Dictionary<string, byte[]?> files, Action<JsonObject> change) =>
        Change(files, WoundHistoryState.HistoryPath, root =>
        {
            var row = root["transitions"]!.AsArray().Single(value => value!["kind"]!.GetValue<string>() == "diagnose")!;
            var result = row["transitionResult"]!.AsObject(); change(result);
            result["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(result);
        });
}
