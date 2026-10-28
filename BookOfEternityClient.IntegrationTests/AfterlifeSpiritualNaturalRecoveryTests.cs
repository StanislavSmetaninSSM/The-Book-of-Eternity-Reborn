using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeSpiritualNaturalRecoveryTests
{
    [Fact]
    public async Task NaturalEffectExecutionKeepsTheOrdinaryAcceptedSourceReplacement()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        var oldDefinition = EffectMaterializationTestFixture.CreateDefinition();
        oldDefinition["definitionKey"] = "definition_old_ordinary_source";
        oldDefinition["allowedRealms"] = new JsonArray("chaos_sea");
        var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        profiles["profiles"]![0]!["specialArts"] = new JsonArray(new JsonObject
        {
            ["artId"] = "art_ordinary_companion", ["activeEffectDefinitions"] = new JsonArray(oldDefinition)
        });
        await fixture.Files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await StageAsync(fixture);
        var acceptedDefinition = oldDefinition.DeepClone().AsObject();
        acceptedDefinition["definitionKey"] = "definition_new_ordinary_source";
        acceptedDefinition["display"]!["name"] = "Принятый новый источник обычного действия";
        profiles["profiles"]![0]!["specialArts"]![0]!["activeEffectDefinitions"] = new JsonArray(acceptedDefinition);
        await fixture.Files.WriteFileAtomicAsync(WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var admission = await AfterlifeWoundHealingPlanner.Continuation.AdmitNaturalAsync(fixture.Files, lease,
            await fixture.ReadManifestAsync(lease));
        var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(admission.Owner);
        try
        {
            var prepared = owner.Prepare();
            var original = owner.Input;
            var roots = owner.SourceRoots.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);
            var acceptedExport = new EffectSourceExport("chaos_sea", "spiritual_art", "art_ordinary_companion",
                new JsonArray(acceptedDefinition.DeepClone()), true, true, true, "ordinary_art_ref");
            var acceptedCarriers = original.PreTurnEffectCarriers! with { AfterlifeProfiles = profiles };
            var input = EffectAcceptedTurnInputComposer.Compose(original.Binding.SessionId, original.Binding.SnapshotToken,
                original.Binding.Turn, new JsonObject(), original.PreTurnEffectCarriers!, acceptedCarriers,
                original.PreTurnEffectIdentityIndex, roots, acceptedPlanSourceExports: new[] { acceptedExport },
                replacedSourceOwners: new HashSet<EffectSourceOwnerKey> { new("chaos_sea", "spiritual_art", "art_ordinary_companion") },
                publicationCarrierBaselines: acceptedCarriers, realm: "chaos_sea", preparedWoundPlan: prepared);
            Assert.Empty(input.SourceAuthority.Issues);
            Assert.True(input.SourceAuthority.ResolveCanonicalBinding(
                new("chaos_sea", "spiritual_art", "art_ordinary_companion", "definition_new_ordinary_source"), "player").Success);
            owner.BindOrdinaryEffectInput(prepared, input);
            var executed = WoundEffectBatchPlanner.Build(prepared, input, new EffectIdentityFactory());
            Assert.True(executed.Success, string.Join(";", executed.Issues.Select(i => i.Actual)));
            var authority = executed.Plan!.SourceAuthority;
            var current = authority.ResolveCanonicalBinding(new("chaos_sea", "spiritual_art", "art_ordinary_companion",
                "definition_new_ordinary_source"), "player");
            Assert.True(current.Success);
            Assert.Equal("Принятый новый источник обычного действия", current.Source!.Definition["display"]!["name"]!.GetValue<string>());
            Assert.False(authority.ResolveCanonicalBinding(new("chaos_sea", "spiritual_art", "art_ordinary_companion",
                "definition_old_ordinary_source"), "player").Success);
            Assert.True(authority.ResolveCanonicalBinding(new("chaos_sea", "wound", "wound_spiritual_session",
                owner.Wounds[0].Consequences.OwnedEffectSources.RootBindings[0].DefinitionKey), "player").Success);
            fixture.AssertResourcesUnchanged();
        }
        finally { owner.Close(); }
    }

    [Fact]
    public async Task OrdinaryCostAndArtUpgradeSurviveWhileRecoveryUsesSignedStartTier()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        var (_, projection) = await StageAsync(fixture);
        Assert.Equal(0, Assert.Single(projection["cohort"]!.AsArray())!["healingTier"]!.GetValue<int>());
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await fixture.Files.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
        var before = ResourceStateContract.ParseCanonical(await fixture.Files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions, false).Ledger!;
        var reserve = Assert.Single(before.Entries, e => e.Coordinate.ResourceKey == "spiritual_action_points" &&
            e.Coordinate.ResourceOwnerId == "player_soul");
        Assert.True(reserve.Current >= 1m);
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            var soul = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, "game_state/meta/soul_state.json"))!)!.AsObject();
            soul["afterlifeCombatProfile"]!["artTiers"]!["spiritual_healing"] = 1;
            var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles, soul, null);
            await fixture.Files.WriteFileAtomicAsync(lease, "game_state/meta/soul_state.json", soul.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
            await fixture.Files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.CommandPath, new JsonObject
            {
                ["resourceChanges"] = new JsonArray(new JsonObject
                {
                    ["operation"] = "spend", ["target"] = new JsonObject { ["kind"] = "afterlife_actor", ["targetId"] = "player_soul" },
                    ["resourceKey"] = "spiritual_action_points", ["amount"] = 1,
                    ["source"] = new JsonObject { ["kind"] = "action_cost" },
                    ["eventRef"] = "turn_43:resource:1", ["reason"] = "Обычное безопасное действие расходует одно усилие."
                })
            }.ToJsonString());
        }
        await PublishAsync(fixture);
        // Using the upgraded H1 would have healed this I wound. Signed H0 must leave one point.
        Assert.Equal(1, Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound.Recovery.CurrentStepProgress);
        var acceptedProfiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
        Assert.Equal(1, acceptedProfiles["profiles"]![0]!["standardArts"]!["spiritual_healing"]!.GetValue<int>());
        var after = ResourceStateContract.ParseCanonical(await fixture.Files.ReadFileAsync(ResourceMaterializationContract.StatePath), definitions, false).Ledger!;
        Assert.Equal(reserve.Current - 1m, Assert.Single(after.Entries, e => e.Coordinate == reserve.Coordinate).Current);
        var result = Assert.IsType<SpiritualHealingPersistedResult>(Assert.Single((await fixture.ReadHistoryAsync()).Transitions,
            t => t.TransitionResult is SpiritualHealingPersistedResult).TransitionResult);
        Assert.Equal(0, result.ToCanonicalJson()["source"]!["ownerTiers"]![0]!["healingTier"]!.GetValue<int>());
    }

    [Fact]
    public async Task PublishedNaturalCohortCannotOmitAnActiveOrThisCycleHealedWound()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        await AddSecondWoundAsync(fixture);
        var (control, projection) = await StageAsync(fixture);
        var plan = await PublishAsync(fixture);
        var history = await fixture.ReadHistoryAsync();
        var primary = Assert.Single(history.Transitions, t => t.TransitionResult is SpiritualHealingPersistedResult);
        var accepted = Assert.IsType<SpiritualHealingPersistedResult>(primary.TransitionResult);
        Assert.True(accepted.IsNatural);
        Assert.Null(accepted.ToCanonicalJson()["request"]);
        var active = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal(1, active.Recovery.CurrentStepProgress);
        var healed = Assert.Single(history.Transitions, t => t.Terminal);
        Assert.NotEqual(active.WoundId, healed.WoundId);
        Assert.DoesNotContain(history.Transitions, t => t.AttemptId is not null || t.Kind == "treat");
        var before = fixture.CaptureMutationRoots(plan.TouchedPaths.Concat(plan.ConsumedPaths));
        var reopened = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
        await using (var lease = await reopened.AcquireCanonicalWriteLeaseAsync())
        {
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await reopened.ReadFileAsync(lease, LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
            async Task<SpiritualHealingPersistedResult?> Read(JsonObject? changedHistory = null) =>
                await ProgressionScheduleService.ReadPublishedSpiritualCycleCoreAsync(control,
                    manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
                    path => path == WoundHistoryState.HistoryPath && changedHistory is not null
                        ? Task.FromResult<byte[]?>(System.Text.Encoding.UTF8.GetBytes(changedHistory.ToJsonString()))
                        : reopened.ReadFileBytesAsync(lease, path));
            Assert.NotNull(await Read()); // Fresh services, same PID; no second tick.
            foreach (var path in new[] { ResourceMaterializationContract.CommandPath, EffectAcceptedTurnPlan.CommandPath })
            {
                // Lawful initial neighboring work is covered separately. Once the
                // cycle has published, even a valid new empty draft cannot become
                // permission to reuse its completed natural result and skip work.
                var command = path == ResourceMaterializationContract.CommandPath
                    ? new JsonObject { ["resourceChanges"] = new JsonArray() }
                    : EffectMaterializationTestFixture.CreateCommandRoot();
                await reopened.WriteFileAtomicAsync(lease, path, command.ToJsonString());
                var replaced = fixture.CaptureMutationRoots(new[] { path });
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Read());
                Assert.Contains("replaced consumed input", failure.Message);
                var issues = await new ValidationService(reopened, NullLogger<ValidationService>.Instance)
                    .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
                Assert.Contains(issues, i => i.Code == "spiritual_natural_recovery_admission_invalid" &&
                    i.Actual!.Contains("replaced consumed input", StringComparison.Ordinal));
                fixture.AssertMutationRootsUnchanged(replaced);
                reopened.DeleteFile(lease, path);
            }
            foreach (var omitted in new[] { active.WoundId, healed.WoundId })
            {
                var truncated = accepted.ToCanonicalJson();
                var retained = accepted.Stages.Where(s => s.Before.WoundId != omitted).ToArray();
                truncated["stages"] = new JsonArray(truncated["stages"]!.AsArray().OfType<JsonObject>()
                    .Where(s => s["before"]!["woundId"]!.GetValue<string>() != omitted).Select(s => s.DeepClone()).ToArray());
                var original = retained.GroupBy(s => s.Before.WoundId).Select(g => g.First().Before).ToArray();
                // Both wounds share one owner/tier, so no owner-tier is accidentally dropped.
                truncated["source"]!["projection"] = SpiritualHealingRequestProjection.CreateNatural("chaos_sea",
                    control.NextChaosSeaTurnOrdinal, truncated["source"]!["sourceFingerprint"]!.GetValue<string>(),
                    original.Select(w => new SpiritualWoundRecoveryMember(w, 0)).ToArray());
                truncated["resultFingerprint"] = WoundHistoryState.ComputeTransitionResultFingerprint(truncated);
                var parseIssues = new List<ValidationIssue>();
                Assert.NotNull(SpiritualHealingPersistedResult.Parse(JsonSerializer.SerializeToElement(truncated), "trimmed", parseIssues));
                Assert.Empty(parseIssues); // A self-consistent rehashed result reaches the independent witness.
                var changed = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!.AsObject();
                foreach (var row in changed["transitions"]!.AsArray().OfType<JsonObject>()) row.Remove("transitionResult");
                changed["transitions"]!.AsArray().OfType<JsonObject>()
                    .Single(r => r["transitionId"]!.GetValue<string>() == retained[0].TransitionId)["transitionResult"] = truncated;
                var parsedHistory = WoundHistoryState.Parse(changed.ToJsonString(), "trimmed.history");
                Assert.True(parsedHistory.IsValid, string.Join(";", parsedHistory.Issues.Select(i => i.Actual)));
                var failure = await Assert.ThrowsAsync<InvalidDataException>(() => Read(changed));
                Assert.Contains("omitted an independently eligible original wound cohort", failure.Message);
            }
            Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control,
                manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
                path => reopened.ReadFileBytesAsync(lease, path), manifest.ManifestPayloadHash,
                expectedNaturalProjection: projection));
        }
        fixture.AssertMutationRootsUnchanged(before);
        fixture.AssertResourcesUnchanged();
    }

    [Fact]
    public async Task NaturalPublicationRefusesConcurrentValidWoundCommandsAndSourceDriftBeforeWrites()
    {
        await using var fixture = await HealingFixture.CreateAsync(1, 0, 20, stageRequest: false);
        await StageAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var manifest = await fixture.ReadManifestAsync(lease);
        var originalProfiles = (await fixture.Files.ReadFileBytesAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
        // A closed, valid ordinary v1 no-op command is still another wound-stage owner.
        await fixture.Files.WriteFileAtomicAsync(lease, AcceptedMechanicsPlan.WoundCommandPath, new JsonObject
        {
            ["schemaVersion"] = 1, ["sessionId"] = manifest.SessionId, ["requestId"] = manifest.RequestId,
            ["snapshotToken"] = manifest.ManifestPayloadHash, ["commands"] = new JsonArray()
        }.ToJsonString());
        var before = fixture.CaptureMutationRoots();
        var refusal = await Assert.ThrowsAsync<InvalidDataException>(() =>
            AfterlifeWoundHealingPlanner.Continuation.AdmitNaturalAsync(fixture.Files, lease, manifest));
        Assert.Contains("cannot overwrite concurrent wound commands or opportunity stages", refusal.Message);
        fixture.AssertMutationRootsUnchanged(before);
        fixture.Files.DeleteFile(lease, AcceptedMechanicsPlan.WoundCommandPath);
        var profiles = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(originalProfiles))!.AsObject();
        profiles["profiles"]![0]!["activeWounds"]![0]!["recovery"]!["currentStepProgress"] = 1;
        await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        before = fixture.CaptureMutationRoots();
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        Assert.Contains(issues, i => i.Code == "spiritual_natural_recovery_admission_invalid");
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, lease));
        fixture.AssertMutationRootsUnchanged(before);
    }

    private static async Task<(ProgressionControl Control, JsonObject Projection)> StageAsync(HealingFixture fixture)
    {
        await fixture.Files.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot().ToJsonString());
        var control = await new ProgressionScheduleService(fixture.Files, NullLogger<ProgressionScheduleService>.Instance)
            .BuildControlForNextTurnAsync();
        JsonObject projection;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
            projection = (await AfterlifeWoundTreatmentAuthority.ProjectNaturalRecoveryAsync(fixture.Files, lease,
                new("session_spiritual_healing", "request_spiritual_healing", 43, control)))!;
        Assert.NotNull(projection);
        Assert.Null(await fixture.Files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        await new LiveTurnPreparationService(fixture.Files).PrepareAsync(new LiveTurnPreparationOptions
        {
            SessionId = "session_spiritual_healing", RequestId = "request_spiritual_healing", TurnNumber = 43,
            CurrentRealm = "Chaos Sea", PlayerAction = "Спокойно наблюдать за Морем Хаоса.", ProgressionControl = control
        });
        return (control, projection);
    }

    private static async Task<AcceptedMechanicsPlan> PublishAsync(HealingFixture fixture)
    {
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
        var plan = await new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance)
            .BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null);
        return Assert.IsType<AcceptedMechanicsPlan>(plan);
    }

    private static async Task AddSecondWoundAsync(HealingFixture fixture)
    {
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        var profiles = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
        var profile = profiles["profiles"]![0]!.AsObject();
        var first = profile["activeWounds"]![0]!.AsObject();
        var second = JsonNode.Parse(first.ToJsonString().Replace("wound_spiritual_session", "wound_terminal_session", StringComparison.Ordinal)
            .Replace("effect_", "effect_terminal_", StringComparison.Ordinal).Replace("definition_", "definition_terminal_", StringComparison.Ordinal))!.AsObject();
        second["recovery"]!["currentStepProgress"] = 1;
        var seed = WoundPlayerProjectionTestData.InputInRealm("chaos_sea", first, second);
        profile["activeWounds"] = seed.Carriers.AfterlifeProfiles!["profiles"]![0]!["activeWounds"]!.DeepClone();
        var effects = profile["activeEffects"]!.AsArray();
        var additional = effects.OfType<JsonObject>().Select(e => JsonNode.Parse(e.ToJsonString()
            .Replace("wound_spiritual_session", "wound_terminal_session", StringComparison.Ordinal)
            .Replace("effect_", "effect_terminal_", StringComparison.Ordinal).Replace("definition_", "definition_terminal_", StringComparison.Ordinal))!.AsObject()).ToArray();
        foreach (var effect in additional) effects.Add(effect);
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(effects.OfType<JsonObject>().ToArray());
        foreach (var entry in index["entries"]!.AsArray().OfType<JsonObject>())
        {
            var effect = effects.OfType<JsonObject>().Single(e => e["effectId"]!.GetValue<string>() == entry["effectId"]!.GetValue<string>());
            entry["owner"]!["carrierPath"] = EffectCarrierCatalog.AfterlifeProfilesPath;
            entry["transitions"]![0]!["transitionId"] = effect["chronology"]!["lastTransitionId"]!.DeepClone();
            entry["transitions"]![0]!["eventRef"] = effect["chronology"]!["createdEventRef"]!.DeepClone();
        }
        await fixture.Files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        await fixture.Files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, WoundIdentityState.SerializeCanonical(seed.Identities));
        await fixture.Files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, WoundHistoryState.SerializeCanonical(seed.History));
        await fixture.Files.WriteFileAtomicAsync(lease, EffectIdentityState.StatePath, index.ToJsonString());
    }
}
