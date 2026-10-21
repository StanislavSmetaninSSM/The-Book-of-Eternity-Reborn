using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class JointBundleComponentTests
{
    private sealed partial class Fixture
    {
        /// <summary>
        /// Produces and persists one physical source occurrence through the real ordinary reduction and QTE producer.
        /// This prepares canonical inputs for a later turn without entering QTE runtime, story or turn publication.
        /// Coordinated publication uncertainty retains this fixture's owned container.
        /// </summary>
        /// <param name="sourceOriginal">
        /// The genuinely prepared current ordinary original for turn 41. Its data coordinates are checked
        /// against the actual source and generation; they do not grant producer or publication authority.
        /// </param>
        /// <returns>
        /// A task completing after exact guarded persistence and validation of the actual source occurrence.
        /// The caller retains ownership of the existing canonical lease.
        /// </returns>
        private async Task PreparePhysicalOccurrenceAsync(GameEngine.JointComponentOriginal sourceOriginal)
        {
            using var total = Files.MeasureOperation("JointBundleFixture.PreparePhysicalOccurrence");
            try
            {
                ArgumentNullException.ThrowIfNull(sourceOriginal);
                Files.EnsureCanonicalWriteLeaseActive(Lease);
                Assert.Equal(41, sourceOriginal.Turn);
                Assert.True(Files.IsCurrentSessionGeneration(Lease, sourceOriginal.Generation));
                Assert.False(Files.FileExists(Lease, MortalWoundOccurrenceState.StatePath));

                QtePhysicalInjurySource source;
                using (Files.MeasureOperation("JointBundleFixture.CapturePhysicalSource"))
                    source = await QtePhysicalInjurySource.CaptureCurrentAsync(Files, Lease,
                        sourceOriginal.SessionId, sourceOriginal.RequestId, sourceOriginal.SnapshotToken, sourceOriginal.Turn);
                Assert.Equal(sourceOriginal.Generation, source.Generation);

                const string eventRef = "turn_41:qte_terminal:1:resource:1";
                const string cause = "A falling stone strikes the player.";
                var selection = new QteTerminalResourceSelection(41, "qte_component_impact", "impact",
                    "brace", "success", 1, "health_damage", new JsonObject
                    {
                        ["resourceChanges"] = new JsonArray(new JsonObject
                        {
                            ["operation"] = "damage",
                            ["target"] = new JsonObject { ["kind"] = "player", ["targetId"] = "player_current" },
                            ["resourceKey"] = "health", ["amount"] = 5,
                            ["source"] = new JsonObject { ["kind"] = "narrative_outcome" },
                            ["eventRef"] = eventRef, ["reason"] = cause
                        })
                    }, new QteSceneService.QtePhysicalInjury
                    {
                        SchemaVersion = 1, ResourceCommandOrdinal = 1, MaximumSeverityRank = 2,
                        Cause = cause, AllowedLocationKinds = ["anatomical", "other"]
                    });

                var roots = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                using (Files.MeasureOperation("JointBundleFixture.CapturePhysicalRoots"))
                    foreach (var path in QteDeferredEffectContinuation.SealedRootPaths
                        .Concat(QtePhysicalInjurySource.WoundRootPaths).Distinct(StringComparer.Ordinal))
                    {
                        var bytes = await Files.ReadFileBytesAsync(Lease, path);
                        roots.Add(path, new CanonicalBeforeImage(bytes is not null, bytes));
                    }

                Func<string, string?> read = path => roots.TryGetValue(path, out var image)
                    ? image.Bytes is { } bytes ? CanonicalJsonUtf8.DecodeOneOptionalBom(bytes) : null
                    : throw new InvalidDataException("An uncaptured physical planning root was requested: " + path);
                Func<string, JsonNode?> readNode = path => read(path) is { } json
                    ? StrictJsonAuthority.Deserialize<JsonNode>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, path)
                    : null;
                Func<string, JsonObject?> readObject = path => readNode(path)?.AsObject();
                Func<string, string, string> hash = (domain, value) =>
                    "sha256:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(domain + "\0" + value)))
                        .ToLowerInvariant();

                using var planning = Files.MeasureOperation("JointBundleFixture.BuildPhysicalPlan");
                var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
                    read(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
                Assert.True(definitionsResult.IsValid, JsonSerializer.Serialize(definitionsResult.Issues));
                var definitions = definitionsResult.Catalog!;
                var stateResult = ResourceStateContract.ParseCanonical(
                    read(ResourceMaterializationContract.StatePath), definitions, allowMissingPristine: false);
                Assert.True(stateResult.IsValid, JsonSerializer.Serialize(stateResult.Issues));
                var historyResult = ResourceHistoryState.ParseCanonical(
                    read(ResourceMaterializationContract.HistoryPath), definitions, allowMissingPristine: false);
                Assert.True(historyResult.IsValid, JsonSerializer.Serialize(historyResult.Issues));
                var state = stateResult.Ledger!;
                var history = historyResult.History!;
                Assert.Empty(history.ValidateStateAgreement(state));
                var owners = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
                    path => Task.FromResult(read(path)), state, history,
                    CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
                Assert.True(owners.IsValid, JsonSerializer.Serialize(owners.Issues));
                var draft = QteTerminalResourceOutcome.BuildPlanningDraft(selection, definitions, state, history, owners.Authority!);
                Assert.True(draft.IsValid, JsonSerializer.Serialize(draft.Issues));
                Assert.NotNull(draft.RegisteredOutcome);
                Assert.NotNull(draft.Sources);
                var pending = ResourcePendingResolutionState.ParseCanonical(
                    read(ResourcePendingResolutionState.PendingPath), definitions, allowMissingPristine: true);
                Assert.Empty(pending.Issues);
                Assert.True(pending.State is null || pending.State.Requests.Count == 0);

                var carriers = new EffectCarrierCatalogInput(readObject(EffectCarrierCatalog.PlayerPath),
                    readObject(EffectCarrierCatalog.NpcPath), readObject(EffectCarrierCatalog.EnemiesPath),
                    readObject(EffectCarrierCatalog.AlliesPath), readObject(EffectCarrierCatalog.AfterlifeProfilesPath),
                    readObject(EffectCarrierCatalog.SpiritualConflictPath));
                Assert.Empty(EffectCarrierCatalog.Build(carriers).Issues);
                var identity = readObject(EffectAcceptedTurnPlan.IdentityIndexPath);
                Assert.NotNull(identity);
                var effectCommands = EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
                var effectInput = EffectAcceptedTurnInputComposer.Compose(source.SessionId, source.RequestId, source.Turn,
                    effectCommands, carriers, carriers, identity,
                    EffectAcceptedTurnInputComposer.SourceAuthorityPaths.Distinct(StringComparer.Ordinal)
                        .ToDictionary(path => path, path => readNode(path), StringComparer.Ordinal),
                    currentWorldTime: EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                        read(EffectAcceptedTurnInputComposer.WorldTimePath)),
                    publicationCarrierBaselines: carriers, realm: "mortal_world");
                var effectResult = new EffectAcceptedTurnPlanCache().GetOrBuild(effectInput);
                Assert.True(effectResult.Success, JsonSerializer.Serialize(effectResult.Issues));
                var effects = effectResult.Plan!;
                var commands = ResourceAcceptedTurnInputComposer.Parse(null);
                var events = new JsonObject { ["acceptedEvents"] = new JsonArray() };
                var pendingRoot = pending.State?.ToCanonicalRoot() ?? new JsonObject();
                var internalInputs = new JsonObject();
                var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
                    Definitions: hash("resource-definitions-v1", read(ResourceMaterializationContract.DefinitionsPath)!),
                    Owners: owners.Authority!.Fingerprint, ResourceState: state.Fingerprint, ResourceHistory: history.Fingerprint,
                    EffectSources: hash("accepted-mechanics-effect-sources-v1", effects.SourceAuthorityFingerprint),
                    EffectTargets: hash("accepted-mechanics-effect-targets-v1", effects.TargetAuthorityFingerprint),
                    EffectCarriers: hash("accepted-mechanics-effect-carriers-v1", effects.CarrierAuthorityFingerprint),
                    EffectIdentityIndex: hash("effect-index-v1", read(EffectAcceptedTurnPlan.IdentityIndexPath)!),
                    AcceptedEvents: hash("resource-events-v1", events.ToJsonString()),
                    Commands: hash("accepted-mechanics-commands-v1", commands.Root.ToJsonString() + "\n" + effectCommands.ToJsonString()),
                    Pending: hash("accepted-mechanics-pending-v1", pendingRoot.ToJsonString()),
                    InternalInputs: hash("accepted-mechanics-internal-v1", internalInputs.ToJsonString()),
                    WoundCarriers: hash("accepted-mechanics-wound-carriers-v1",
                        string.Join("\n", QtePhysicalInjurySource.WoundRootPaths.OrderBy(path => path, StringComparer.Ordinal)
                            .Select(path => path + "=" + roots[path].Fingerprint))),
                    WoundIdentityIndex: hash("accepted-mechanics-wound-index-v1", read(WoundIdentityState.StatePath) ?? "<missing>"),
                    WoundHistory: hash("accepted-mechanics-wound-history-v1", read(WoundHistoryState.HistoryPath) ?? "<missing>"));
                var context = new AcceptedMechanicsPlanningContext(definitions.ToCanonicalRoot(), definitions, state, history,
                    owners.Authority!, draft.Sources!, commands, identity!, effects,
                    registeredSystemOutcomes: [draft.RegisteredOutcome!], pendingResolutionState: pending.State,
                    executionSequenceOffset: draft.ExecutionSequenceOffset);
                var input = new AcceptedMechanicsInput(source.SessionId, source.RequestId, source.SnapshotToken,
                    "mortal_world", source.Turn, events, commands.Root, effectCommands, pendingRoot, internalInputs,
                    fingerprints, roots, [], context);
                var reduction = AcceptedMechanicsPlanner.ReduceAcceptedPlan(
                    input, AcceptedMechanicsPlanFingerprints.ComputePlanningInput(input));
                Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction, reduction.Kind);
                var ordinary = reduction.Completed!;
                var mutation = Assert.Single(ordinary.Resources.EffectBoundaryTranscript!.ResourceMutations
                    .Where(row => row.Transition.EventRef == eventRef));
                Assert.Equal(ResourceMutationExecutionKind.Applied, mutation.ExecutionKind);
                Assert.Equal(5m, mutation.Transition.AppliedAmount);
                Assert.Equal(new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health"),
                    mutation.Transition.Coordinate);
                var producer = QtePhysicalWoundOccurrenceProducer.Create(source, selection, ordinary);
                Assert.True(producer.FinalizedProjection.Success, JsonSerializer.Serialize(producer.FinalizedProjection.Issues));
                Assert.Single(producer.FinalizedProjection.Harms);
                var completed = AcceptedMechanicsPlanner.CompleteQtePhysicalWoundOccurrenceReduction(reduction, producer, roots);
                Assert.Equal(AcceptedMechanicsPlanner.AcceptedMechanicsReductionKind.CompletedOrdinaryReduction, completed.Kind);
                Assert.Same(ordinary.Resources, completed.Completed!.Resources);
                var assembled = AcceptedMechanicsPlanner.CompleteAcceptedReduction(completed);
                Assert.True(assembled.Success, JsonSerializer.Serialize(assembled.Issues));
                var validated = new AcceptedMechanicsPlanCache((_, _) => assembled)
                    .GetOrBuildValidated(completed.Completed.Input);
                Assert.True(validated.Success, JsonSerializer.Serialize(validated.Issues));
                var plan = validated.Plan!;
                Assert.False(plan.AwaitsPendingResolution);
                Assert.Empty(plan.OwnerTransitions);
                Assert.Empty(plan.WoundCarrierAfterImages);
                Assert.True(plan.OwnerCompanionAfterImages.ContainsKey(MortalWoundOccurrenceState.StatePath));
                planning?.Dispose();

                var writes = QteDeferredAcceptedMechanicsPlanner.CreatePublicationWrites(plan);
                var writtenPaths = writes.Select(write => write.Path).ToHashSet(StringComparer.Ordinal);
                writes = writes.Concat(roots.Where(row => !writtenPaths.Contains(row.Key))
                    .Select(row => CoordinatedStateWriteHelper.CreateExactGuardWrite(row.Key, row.Value))).ToArray();
                Assert.All(writes, write =>
                {
                    Assert.True(write.RequireCurrentBaseline);
                    Assert.NotNull(write.ExactPrevious);
                });
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.PhysicalPrepared", Source = sourceOriginal,
                    Input = completed.Completed.Input.InternalInputs, Events = completed.Completed.Input.AcceptedEvents,
                    plan.InputFingerprint, plan.PreparedPlanFingerprint, ResourceTranscript = ordinary.Resources.EffectBoundaryTranscript.Fingerprint,
                    Mutation = mutation, Writes = writes
                }));
                Assert.True(Files.IsCurrentSessionGeneration(Lease, sourceOriginal.Generation));
                _ = await QtePhysicalInjurySource.CaptureCurrentAsync(Files, Lease,
                    source.SessionId, source.RequestId, source.SnapshotToken, source.Turn);
                using (Files.MeasureOperation("JointBundleFixture.CommitPhysicalOccurrence"))
                    Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(Files, Lease, writes));

                using var verify = Files.MeasureOperation("JointBundleFixture.ValidatePhysicalAfterImages");
                foreach (var write in writes)
                {
                    if (!write.GuardOnly)
                        Assert.Equal(write.NextJson, await Files.ReadFileAsync(Lease, write.Path));
                    else
                        Assert.Equal(write.ExactPrevious!.Bytes, await Files.ReadFileBytesAsync(Lease, write.Path));
                }
                var persistedRoots = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
                foreach (var path in QtePhysicalInjurySource.WoundRootPaths)
                {
                    var bytes = await Files.ReadFileBytesAsync(Lease, path);
                    persistedRoots.Add(path, new CanonicalBeforeImage(bytes is not null, bytes));
                }
                var captured = QtePhysicalWoundOccurrenceProducer.ValidateCapturedWounds(persistedRoots);
                var occurrence = Assert.Single(captured.Pending.Occurrences);
                Assert.Equal(source.SessionId, occurrence.SourceSessionId);
                Assert.Equal(source.RequestId, occurrence.SourceRequestId);
                Assert.Equal(source.SnapshotToken, occurrence.SourceSnapshotToken);
                Assert.Equal(41, occurrence.SourceTurn);
                Assert.Equal("qte", occurrence.AdapterKind);
                Assert.Equal("physical", occurrence.Domain);
                Assert.Equal("mortal_qte_terminal_health_damage_v1", occurrence.ProfileKey);
                Assert.Equal("harmful", occurrence.Outcome.Kind);
                Assert.Equal(2, occurrence.Outcome.MaximumSeverityRank);
                Assert.Equal(cause, occurrence.Outcome.ReadableCause);
                Assert.Contains(occurrence.AcceptedEvents, row => row.EventRef == eventRef);
                Assert.True(Files.IsCurrentSessionGeneration(Lease, sourceOriginal.Generation));
                _output.WriteLine(JsonSerializer.Serialize(new
                {
                    Name = "JointBundleFixture.PhysicalPersisted", Source = sourceOriginal, Occurrence = occurrence,
                    CanonicalOccurrence = await Files.ReadFileAsync(Lease, MortalWoundOccurrenceState.StatePath),
                    CanonicalResourceState = await Files.ReadFileAsync(Lease, ResourceMaterializationContract.StatePath),
                    CanonicalResourceHistory = await Files.ReadFileAsync(Lease, ResourceMaterializationContract.HistoryPath)
                }));
            }
            catch (CoordinatedStatePublicationUncertainException)
            {
                _retained = true;
                throw;
            }
        }
    }
}
