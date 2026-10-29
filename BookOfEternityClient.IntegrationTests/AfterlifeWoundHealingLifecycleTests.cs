using System.Text.Json.Nodes;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

/// <summary>Real signed request -> existing validation -> common normalizer -> wound/world/History.</summary>
public sealed class AfterlifeWoundHealingLifecycleTests
{
    [Fact]
    public async Task FailedActiveCheckStillAdvancesOneCycleAndRetainsNaturalProgress()
    {
        await using var f = await Fixture.CreateAsync(rank: 2, tier: 2, die: 1);
        await f.PublishAsync();
        var wound = Assert.Single((await f.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(2, wound.Severity.Rank);
        Assert.Equal(3, wound.Recovery.CurrentStepProgress);
        Assert.False(string.IsNullOrWhiteSpace(wound.Care.LastAttemptId));
        await f.AssertOneCycleAndRetainedAttemptAsync();
        Assert.Empty((await f.ReadHistoryAsync()).Transitions.Where(t => t.Terminal));
        f.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task FailedActiveCheckOnRankOneHealsThroughItsOneNaturalTick()
    {
        await using var f = await Fixture.CreateAsync(rank: 1, tier: 1, die: 1);
        await f.PublishAsync();
        Assert.Empty((await f.ReadWoundsAsync()).Occurrences);
        var terminal = Assert.Single((await f.ReadHistoryAsync()).Transitions.Where(t => t.Terminal));
        Assert.Equal("heal", terminal.Kind);
        await f.AssertOneCycleAndRetainedAttemptAsync();
        var visible = await WoundPlayerReadService.LoadAsync(f.Files);
        Assert.True(visible.IsAvailable, visible.StatusMessage);
        Assert.Empty(visible.Active);
        Assert.Single(visible.History);
        f.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task SuccessfulActiveCheckHealsWithoutASecondNaturalHeal()
    {
        await using var f = await Fixture.CreateAsync(rank: 1, tier: 1, die: 20);
        await f.PublishAsync();
        var history = await f.ReadHistoryAsync();
        var self = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single(history.Transitions,
            row => row.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.Equal(2, self.SchemaVersion); Assert.Equal("self", self.Mode);
        foreach (var version in new[] { 5, 6, 99 })
        {
            var unsupported = self.ToCanonicalJson(); unsupported["schemaVersion"] = version;
            unsupported["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(unsupported);
            var versionIssues = new List<ValidationIssue>();
            Assert.Null(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(unsupported), "unsupported.self.family", versionIssues));
            Assert.Contains(versionIssues, issue => issue.Code == "spiritual_healing_history_invalid" && issue.Actual == "Spiritual result kind/schema/fingerprint differs.");
        }
        var originalInterface = await f.Files.ReadFileBytesAsync("output/interface_updates.json");
        var unboundMemory = new JsonObject
        {
            ["dialogueOptions"] = new JsonArray(), ["timestamp"] = DateTime.UtcNow.ToString("o"),
            [SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(new JsonObject
            {
                ["entryId"] = "memory_wrong_family", ["residentId"] = "resident_unbound", ["turn"] = 43,
                ["timestamp"] = DateTime.UtcNow.ToString("o"), ["title"] = "Посторонняя память", ["summary"] = "Я не являюсь участником этого самолечения."
            })
        };
        await f.Files.WriteFileAtomicAsync("output/interface_updates.json", unboundMemory.ToJsonString());
        var beforeInterface = f.CaptureMutationRoots();
        var wrongFamily = await new ValidationService(f.Files, NullLogger<ValidationService>.Instance).ValidateAcceptedTurnInterfacePayloadAsync();
        Assert.Contains(wrongFamily, issue => issue.Code == "spiritual_healing_interface_unbound" &&
            issue.Actual!.Contains("exact published family5 response", StringComparison.Ordinal));
        f.AssertMutationRootsUnchanged(beforeInterface);
        await using (var restore = await f.Files.AcquireCanonicalWriteLeaseAsync())
        {
            if (originalInterface is null) f.Files.DeleteFile(restore, "output/interface_updates.json");
            else await f.Files.WriteFileAtomicBytesAsync(restore, "output/interface_updates.json", originalInterface);
        }
        Assert.Empty((await f.ReadWoundsAsync()).Occurrences);
        Assert.Single((await f.ReadHistoryAsync()).Transitions.Where(t => t.Terminal));
        await f.AssertOneCycleAndRetainedAttemptAsync();
        f.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task SignedSelfRequestCannotBeReplacedByAcceptedV1Commands()
    {
        await using var f = await Fixture.CreateAsync(rank: 2, tier: 2, die: 1);
        await f.AssertV1ReplacementRejectedAsync();
    }

    [Fact]
    public async Task CanonicalResourceMutationAfterSignedRequestCannotBecomeItsBaseline()
    {
        await using var f = await Fixture.CreateAsync(rank: 2, tier: 2, die: 1);
        await f.CommitExternalResourceSpendAsync();
        await f.AssertChangedResourceSourceRejectedAsync();
    }

    internal sealed class Fixture : IAsyncDisposable
    {
        private const string WoundId = "wound_spiritual_session";
        private const string SessionId = "session_spiritual_healing";
        private const string RequestId = "request_spiritual_healing";
        private const int Turn = 43;
        private readonly string _root;
        private readonly int _die;
        private readonly Dictionary<string, byte[]> _resourceBytes;

        private Fixture(string root, FileSystemManager files, int die, Dictionary<string, byte[]> resourceBytes)
        { _root = root; Files = files; _die = die; _resourceBytes = resourceBytes; }
        internal FileSystemManager Files { get; }

        internal static async Task<Fixture> CreateAsync(int rank, int tier, int die, bool stageRequest = true,
            FileSystemManagerHooks? hooks = null, long initialProgress = 0,
            Func<FileSystemManager, JsonObject, Task>? beforeResourceBootstrap = null, string realm = "Chaos Sea")
        {
            Assert.True(AfterlifeEntityProfileState.TryNormalizeEffectRealm(realm, out var realmSlug));
            Assert.Contains(realmSlug, new[] { "chaos_sea", "shining_abode" });
            var root = Path.Combine(Path.GetTempPath(), "boe-spiritual-healing-lifecycle-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try
            {
                var files = hooks is null
                    ? new FileSystemManager(root, NullLogger<FileSystemManager>.Instance)
                    : new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
                        PhysicalLoadTransactionOperations.Instance, hooks);
                var rawWound = WoundContractTestData.CreateSpiritualActiveWound(woundId: WoundId, realm: realmSlug,
                    ownerId: "player_soul", carrierPath: WoundCarrierCatalog.AfterlifeProfilesPath);
                if (rank == 1)
                {
                    rawWound["severity"]!["rank"] = 1;
                    rawWound["severity"]!["value"] = "I";
                    rawWound["severity"]!["maximumAtCreation"] = "I";
                    rawWound["recovery"]!["currentStepThreshold"] = 2;
                    rawWound["consequences"]!["slotBudget"] = 1;
                    rawWound["consequences"]!["slotsUsed"] = 1;
                    rawWound["consequences"]!["entries"]!.AsArray().RemoveAt(1);
                    rawWound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().RemoveAt(1);
                    rawWound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().RemoveAt(1);
                }
                else if (rank is 3 or 4)
                {
                    rawWound["severity"]!["rank"] = rank;
                    rawWound["severity"]!["value"] = rank == 3 ? "III" : "IV";
                    rawWound["severity"]!["maximumAtCreation"] = rank == 3 ? "III" : "IV";
                    rawWound["recovery"]!["currentStepThreshold"] = 2 * rank;
                    rawWound["consequences"]!["slotBudget"] = rank;
                    rawWound["consequences"]!["slotsUsed"] = rank;
                    rawWound["consequences"]!["ownedEffectSources"]!["definitions"]![1]!["components"]![0]!["payload"]!["magnitude"] = rank - 1;
                    var strain = WoundContractTestData.CreateOwnedEffectDefinition(WoundId, realmSlug,
                        "definition_spiritual_strain", "spiritual_strain_burden");
                    strain["components"]![0]!["componentId"] = "component_spiritual_strain";
                    strain["components"]![0]!["payload"]!["magnitude"] = 1;
                    strain["triggers"]![0]!["componentIds"] = new JsonArray("component_spiritual_strain");
                    rawWound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().Add(strain);
                    rawWound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Add(new JsonObject
                    { ["effectId"] = "effect_spiritual_strain", ["definitionKey"] = "definition_spiritual_strain" });
                    rawWound["consequences"]!["entries"]!.AsArray().Add(new JsonObject
                    { ["slot"] = 3, ["profileKey"] = "spiritual_strain_burden", ["effectId"] = "effect_spiritual_strain",
                        ["readableSummary"] = "Духовное напряжение усиливает неудачи." });
                    if (rank == 4)
                    {
                        var position = WoundContractTestData.CreateOwnedEffectDefinition(WoundId, realmSlug,
                            "definition_spiritual_position", "spiritual_position_burden");
                        position["components"]![0]!["componentId"] = "component_spiritual_position";
                        position["components"]![0]!["payload"]!["magnitude"] = 2;
                        position["triggers"]![0]!["componentIds"] = new JsonArray("component_spiritual_position");
                        rawWound["consequences"]!["ownedEffectSources"]!["definitions"]!.AsArray().Add(position);
                        rawWound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray().Add(new JsonObject
                        { ["effectId"] = "effect_spiritual_position", ["definitionKey"] = "definition_spiritual_position" });
                        rawWound["consequences"]!["entries"]!.AsArray().Add(new JsonObject
                        { ["slot"] = 4, ["profileKey"] = "spiritual_position_burden", ["effectId"] = "effect_spiritual_position",
                            ["readableSummary"] = "Повреждение нарушает духовную позицию." });
                    }
                }
                rawWound["recovery"]!["currentStepProgress"] = initialProgress;
                var seed = WoundPlayerProjectionTestData.InputInRealm(realmSlug, rawWound);
                var profile = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("player_soul", "player_soul", realm, 42);
                profile.Remove(ActorMaterializationContract.PropertyName);
                profile["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
                var arts = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
                arts["spiritual_healing"] = tier;
                profile["standardArts"] = arts.DeepClone();
                var profiles = AfterlifeEntityProfileState.CreateDefaultRoot();
                profiles["profiles"] = new JsonArray(profile);
                var carriers = seed.Carriers with { AfterlifeProfiles = profiles };
                var catalog = WoundCarrierCatalog.Build(carriers);
                Assert.Empty(catalog.Issues);
                var wound = Assert.Single(catalog.Occurrences).Wound;
                var effects = new List<JsonObject>();
                foreach (var binding in wound.Consequences.OwnedEffectSources.RootBindings)
                {
                    var definition = JsonNode.Parse(wound.Consequences.OwnedEffectSources.Definitions.Single(d =>
                        d.GetProperty("definitionKey").GetString() == binding.DefinitionKey).GetRawText())!.AsObject();
                    var key = wound.Consequences.Entries.Single(e => e.EffectId == binding.EffectId).ProfileKey;
                    var effect = EffectMaterializationTestFixture.CreateSpiritualWoundCanonicalEffect(key,
                        targetKind: "player", realm: realmSlug, woundId: WoundId);
                    effect["effectId"] = binding.EffectId;
                    effect["target"]!["targetId"] = "player_soul";
                    effect["source"]!["definitionKey"] = binding.DefinitionKey;
                    foreach (var part in new[] { "display", "components", "triggers", "removal", "links" })
                        effect[part] = definition[part]!.DeepClone();
                    effect["display"]!["sourceLabel"] = wound.Display.Name;
                    effect["stacking"]!["stackKey"] = definition["stacking"]!["stackKey"]!.DeepClone();
                    effect["chronology"]!["lastTransitionId"] = "seed_transition_" + binding.EffectId;
                    effect["chronology"]!["createdEventRef"] = "seed_event_" + binding.EffectId;
                    effects.Add(effect);
                }
                profile["activeEffects"] = new JsonArray(effects.Select(e => (JsonNode)e.DeepClone()).ToArray());
                var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.ToArray());
                foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
                {
                    var effect = effects.Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
                    entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
                    entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
                    entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
                }
                var soulCombat = AfterlifeSpiritualConflictState.CreateDefaultCombatProfile();
                soulCombat["artTiers"] = arts.DeepClone();
                var soul = new JsonObject { ["soulName"] = "Лечащаяся душа", ["currentRealm"] = realm,
                    ["afterlifeCombatProfile"] = soulCombat };
                AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles, soul, null);
                var roots = new Dictionary<string, JsonObject>(StringComparer.Ordinal)
                {
                    ["game_state/meta/soul_state.json"] = soul,
                    [WoundCarrierCatalog.PlayerPath] = carriers.PlayerWounds!,
                    [WoundCarrierCatalog.NpcPath] = carriers.NpcWounds!,
                    [WoundCarrierCatalog.EnemiesPath] = carriers.EnemyCombatants!,
                    [WoundCarrierCatalog.AlliesPath] = carriers.AllyCombatants!,
                    [WoundCarrierCatalog.AfterlifeProfilesPath] = profiles,
                    [WoundIdentityState.StatePath] = JsonNode.Parse(WoundIdentityState.SerializeCanonical(seed.Identities))!.AsObject(),
                    [WoundHistoryState.HistoryPath] = JsonNode.Parse(WoundHistoryState.SerializeCanonical(seed.History))!.AsObject(),
                    [EffectIdentityState.StatePath] = index
                };
                await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                {
                    foreach (var pair in roots) await files.WriteFileAtomicAsync(lease, pair.Key, pair.Value.ToJsonString());
                    if (!stageRequest)
                        await files.WriteFileAtomicAsync(lease, MortalItemIdentityState.StatePath,
                            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
                }
                if (beforeResourceBootstrap is not null)
                    await beforeResourceBootstrap(files, profiles);
                await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(files,
                    new(Profiles: profiles, SoulState: soul));
                if (stageRequest)
                {
                    var control = await new ProgressionScheduleService(files, NullLogger<ProgressionScheduleService>.Instance)
                        .BuildControlForNextTurnAsync();
                    await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
                    {
                        var staged = await AfterlifeWoundTreatmentAuthority.StageSelfTreatmentAsync(files, lease,
                            new(SessionId, RequestId, Turn, control),
                            new(WoundId, WoundIdentityState.ComputeSemanticFingerprint(wound)), _ => new[] { die });
                        Assert.True(staged.IsStaged, staged.IssueCode);
                    }
                    await new LiveTurnPreparationService(files).PrepareAsync(new LiveTurnPreparationOptions
                    {
                        SessionId = SessionId, RequestId = RequestId, TurnNumber = Turn, CurrentRealm = realm,
                        PlayerAction = "Лечить собственную духовную рану", ProgressionControl = control,
                        PreGeneratedDices1d20 = new[] { 7, 11, 16 }
                    });
                }
                var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
                foreach (var path in ResourcePaths) resources.Add(path, File.ReadAllBytes(files.ResolvePath(path)));
                return new(root, files, die, resources);
            }
            catch { DeleteRoot(root); throw; }
        }

        internal async Task<AcceptedMechanicsPlan> PublishAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            await WriteReportAsync(lease);
            var issues = await new ValidationService(Files, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            var errors = issues.Where(i => i.Severity == IssueSeverity.Error).ToArray();
            Assert.True(errors.Length == 0, string.Join("\n", errors.Select(i => i.Code + "@" + i.FilePath + ":" + i.Message + ":" + i.Actual)));
            var plan = await new CanonicalStateNormalizer(Files, NullLogger<CanonicalStateNormalizer>.Instance)
                .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null);
            Assert.NotNull(plan);
            return plan!;
        }

        internal async Task WriteReportAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            await Files.WriteFileAtomicAsync(lease, ProgressionScheduleService.ReportPath, new JsonObject
            {
                ["progressionProcessingReport"] = new JsonObject
                {
                    ["sessionId"] = SessionId, ["requestId"] = RequestId, ["turnNumber"] = Turn,
                    ["worldCyclesProcessed"] = 0, ["factionCyclesProcessed"] = 0,
                    ["chaosSeaCyclesProcessed"] = 1, ["guardianProjectCyclesProcessed"] = 1, ["residentAgencyCyclesProcessed"] = 1,
                    ["newLastChaosSeaSimulationOrdinal"] = 1, ["newLastGuardianProjectCycleOrdinal"] = 1,
                    ["newLastResidentAgencyCycleOrdinal"] = 1
                }
            }.ToJsonString());
        }

        internal async Task AssertV1ReplacementRejectedAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            await WriteReportAsync(lease);
            var manifest = await ReadManifestAsync(lease);
            await Files.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, new JsonObject
            {
                ["schemaVersion"] = 1, ["sessionId"] = SessionId, ["requestId"] = RequestId,
                ["snapshotToken"] = manifest.ManifestPayloadHash, ["commands"] = new JsonArray()
            }.ToJsonString());
            var before = CaptureMutationRoots();
            var issues = await new ValidationService(Files, NullLogger<ValidationService>.Instance)
                .ValidateAcceptedTurnRawResourceMaterializationAsync(lease);
            Assert.Contains(issues, i => i.Code == "spiritual_self_treatment_admission_invalid" &&
                i.Actual?.Contains("retained spiritual treatment request changed", StringComparison.Ordinal) == true);
            AssertMutationRootsUnchanged(before);
        }

        internal async Task AssertChangedResourceSourceRejectedAsync()
        {
            await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
            await WriteReportAsync(lease);
            var before = CaptureMutationRoots();
            var manifest = await ReadManifestAsync(lease);
            var failure = await Assert.ThrowsAsync<InvalidDataException>(() =>
                AfterlifeWoundHealingPlanner.Continuation.AdmitAsync(Files, lease, manifest));
            Assert.Contains("Spiritual treatment source changed: " + ResourceMaterializationContract.StatePath, failure.Message);
            AssertMutationRootsUnchanged(before);
        }

        internal async Task<LiveTurnPendingSnapshotManifest> ReadManifestAsync(FileSystemManager.CanonicalWriteLease lease) =>
            JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await Files.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;

        internal Dictionary<string, byte[]?> CaptureMutationRoots(IEnumerable<string>? additionalPaths = null) => ResourcePaths.Concat(new[]
        {
            WoundCarrierCatalog.AfterlifeProfilesPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
            EffectIdentityState.StatePath, ProgressionScheduleService.SchedulePath, ProgressionScheduleService.ReportPath,
            AcceptedMechanicsPlan.WoundCommandPath
        }).Concat(additionalPaths ?? Array.Empty<string>()).Distinct(StringComparer.Ordinal)
            .ToDictionary(p => p, p => File.Exists(Files.ResolvePath(p)) ? File.ReadAllBytes(Files.ResolvePath(p)) : null,
            StringComparer.Ordinal);
        internal void AssertMutationRootsUnchanged(Dictionary<string, byte[]?> before)
        {
            var after = CaptureMutationRoots(before.Keys);
            foreach (var pair in before)
                Assert.True(pair.Value is null ? after[pair.Key] is null : after[pair.Key] is not null &&
                    pair.Value.AsSpan().SequenceEqual(after[pair.Key]), "Rejected treatment mutated " + pair.Key);
        }

        internal async Task CommitExternalResourceSpendAsync()
        {
            var definitions = ResourceDefinitionCatalog.ParseCanonical(await Files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
                allowMissingPristine: false).Catalog!;
            var ledger = ResourceStateContract.ParseCanonical(await Files.ReadFileAsync(ResourceMaterializationContract.StatePath),
                definitions, allowMissingPristine: false).Ledger!;
            var history = ResourceHistoryState.ParseCanonical(await Files.ReadFileAsync(ResourceMaterializationContract.HistoryPath),
                definitions, allowMissingPristine: false).History!;
            var before = Assert.Single(ledger.Entries, e => e.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeActor &&
                e.Coordinate.ResourceOwnerId == "player_soul" && e.Coordinate.ResourceKey == "spiritual_action_points");
            Assert.True(before.Current > 0);
            var prior = history.Transitions.Last(t => ResourceCoordinateComparer.Instance.Equals(t.Coordinate, before.Coordinate));
            var sequence = history.Transitions.Where(t => t.Turn == Turn).Select(t => t.ExecutionSequence).DefaultIfEmpty(-1).Max() + 1;
            var transition = new ResourceTransition("resource_transition_external_self_test", "resource_operation_external_self_test",
                "event_external_self_test", "registered_system_outcome", "external_self_test", ResourceMutationPhase.RegisteredSystemOutcome,
                60, sequence, before.Coordinate, ResourceTransitionOperation.Spend, 1m, 1m, ResourceTransitionOutcome.Applied, null,
                before.Snapshot, before.Snapshot with { Current = before.Current - 1m },
                new("registered_system_outcome", "external_self_test", prior.PolicyFingerprint), prior.PolicyFingerprint, null, Turn);
            var updatedHistory = ResourceHistoryState.CreateValidated(history.Transitions.Append(transition), definitions);
            Assert.True(updatedHistory.IsValid, string.Join(";", updatedHistory.Issues));
            var updatedState = new ResourceStateLedger(ledger.Entries.Select(e => ResourceCoordinateComparer.Instance.Equals(e.Coordinate, before.Coordinate)
                ? e with { Current = before.Current - 1m, Chronology = e.Chronology with { LastTransitionId = transition.TransitionId,
                    LastEventRef = transition.EventRef, LastTransitionTurn = Turn } } : e));
            Assert.Empty(updatedHistory.History!.ValidateStateAgreement(updatedState));
            await CanonicalResourceQuartetTestFixture.CommitExistingStateAsync(Files, definitions, ledger, history,
                updatedState, updatedHistory.History);
        }

        internal async Task<WoundCarrierCatalog> ReadWoundsAsync()
        {
            var profiles = await ReadRootAsync(WoundCarrierCatalog.AfterlifeProfilesPath);
            var catalog = WoundCarrierCatalog.Build(new(null, null, null, null, profiles));
            Assert.Empty(catalog.Issues);
            return catalog;
        }
        internal async Task<WoundHistoryState> ReadHistoryAsync()
        {
            var result = WoundHistoryState.Parse(await Files.ReadFileAsync(WoundHistoryState.HistoryPath), WoundHistoryState.HistoryPath);
            Assert.True(result.IsValid, string.Join(";", result.Issues.Select(i => i.Code + ":" + i.Message)));
            return result.State!;
        }
        internal async Task AssertOneCycleAndRetainedAttemptAsync()
        {
            var schedule = await ReadRootAsync(ProgressionScheduleService.SchedulePath);
            Assert.Equal(1, schedule["currentChaosSeaTurnOrdinal"]!.GetValue<int>());
            Assert.Equal(1, schedule["lastChaosSeaSimulationOrdinal"]!.GetValue<int>());
            Assert.Null(await Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
            Assert.Null(await Files.ReadFileBytesAsync(ProgressionScheduleService.ReportPath));
            var history = await ReadRootAsync(WoundHistoryState.HistoryPath);
            var primary = Assert.Single(history["transitions"]!.AsArray().OfType<JsonObject>()
                .Where(t => t["attemptId"] is not null));
            Assert.Equal("spiritual_healing", primary["transitionResult"]!["kind"]!.GetValue<string>());
            Assert.Equal(_die, primary["transitionResult"]!["request"]!["attempt"]!["roll"]!["values"]![0]!.GetValue<int>());
        }
        internal void AssertResourcesUnchanged()
        {
            foreach (var pair in _resourceBytes)
                Assert.True(JsonNode.DeepEquals(
                    JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(pair.Value)),
                    JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(File.ReadAllBytes(Files.ResolvePath(pair.Key))))),
                    "Self treatment changed canonical resource values or history: " + pair.Key);
        }
        private async Task<JsonObject> ReadRootAsync(string path) => JsonNode.Parse((await Files.ReadFileAsync(path))!)!.AsObject();
        private static readonly string[] ResourcePaths = [ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath];
        public ValueTask DisposeAsync() { DeleteRoot(_root); return ValueTask.CompletedTask; }
        private static void DeleteRoot(string root)
        {
            if (!Path.GetFileName(root).StartsWith("boe-spiritual-healing-lifecycle-", StringComparison.Ordinal) ||
                Path.GetDirectoryName(root) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar))
                throw new InvalidOperationException("Lifecycle fixture cannot remove a foreign root.");
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
