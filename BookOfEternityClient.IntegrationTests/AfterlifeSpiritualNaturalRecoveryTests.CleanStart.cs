using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed partial class AfterlifeSpiritualNaturalRecoveryTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanStartPublishesOriginalRecoveryAndNewOwnerTogether(bool accumulated)
    {
        await using var fixture = await CreateCleanStartFixtureAsync();
        var (control, projection) = await StageAsync(fixture);
        var historyBefore = await fixture.ReadHistoryAsync();
        await WriteCleanStartAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        AssertNoCleanStartErrors(await validator.ValidateAcceptedTurnRawMortalLocationMaterializationAsync());
        AssertNoCleanStartErrors(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync(lease));
        var issues = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        AssertNoCleanStartErrors(issues);
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var result));
        var plan = Assert.IsType<AcceptedMechanicsPlan>(result.Plan);
        var before = fixture.CaptureMutationRoots([AfterlifeSpiritualConflictState.StatePath]);
        Assert.Equal(0, Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound.Recovery.CurrentStepProgress);
        var normalizer = new CanonicalStateNormalizer(fixture.Files, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(lease);
        Assert.Same(plan, accumulated
            ? await normalizer.NormalizeAccumulatedStateWithPlanAsync(new Dictionary<string, string>())
            : await normalizer.NormalizeAcceptedMechanicsAsync(backups: null));
        var wound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        Assert.Equal((3, 1), (wound.Severity.Rank, wound.Recovery.CurrentStepProgress));
        var history = await fixture.ReadHistoryAsync();
        var oldRows = JsonNode.Parse(WoundHistoryState.SerializeCanonical(historyBefore))!["transitions"]!.AsArray();
        var currentRows = JsonNode.Parse(WoundHistoryState.SerializeCanonical(history))!["transitions"]!.AsArray();
        for (var index = 0; index < oldRows.Count; index++) Assert.True(JsonNode.DeepEquals(oldRows[index], currentRows[index]));
        var tick = Assert.Single(history.Transitions.Skip(historyBefore.Transitions.Count));
        Assert.Equal("recover", tick.Kind);
        Assert.Null(tick.AttemptId);
        Assert.Equal("natural", Assert.IsType<SpiritualHealingPersistedResult>(tick.TransitionResult).Mode);
        var conflict = JsonNode.Parse((await fixture.Files.ReadFileAsync(lease, AfterlifeSpiritualConflictState.StatePath))!)!;
        Assert.Empty(conflict["activeConflict"]!["exchangeLog"]!.AsArray());
        Assert.False(conflict.AsObject().ContainsKey(AfterlifeSpiritualConflictState.ResponseField));
        Assert.True(JsonNode.DeepEquals(plan.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath], conflict));
        var definitions = ResourceDefinitionCatalog.ParseCanonical(await fixture.Files.ReadFileAsync(lease,
            ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
        var prior = ResourceStateContract.ParseCanonical(CanonicalJsonUtf8.DecodeOneOptionalBom(before[ResourceMaterializationContract.StatePath]!), definitions, false).Ledger!;
        var current = ResourceStateContract.ParseCanonical(await fixture.Files.ReadFileAsync(lease,
            ResourceMaterializationContract.StatePath), definitions, false).Ledger!;
        Assert.All(prior.Entries, entry => Assert.Contains(current.Entries, actual => actual == entry));
        var side = Assert.Single(current.Entries, entry => entry.Coordinate.OwnerKind == ResourceOwnerKind.AfterlifeConflictSide);
        Assert.Equal((6m, 6m), (side.Current, side.Maximum));
        var manifest = await fixture.ReadManifestAsync(lease);
        Assert.True(await ProgressionScheduleService.IsPublishedSpiritualCycleCoreAsync(control, manifest.SessionId,
            manifest.RequestId, manifest.TurnNumber, path => fixture.Files.ReadFileBytesAsync(lease, path),
            manifest.ManifestPayloadHash, expectedNaturalProjection: projection));
        var published = fixture.CaptureMutationRoots([AfterlifeSpiritualConflictState.StatePath]);
        AssertNoCleanStartErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
        fixture.AssertMutationRootsUnchanged(published);
    }

    [Theory]
    [InlineData("exchange")]
    [InlineData("recent")]
    [InlineData("canonical")]
    [InlineData("wrong_realm")]
    [InlineData("invalid_actor")]
    [InlineData("missing_log")]
    [InlineData("wrong_log_type")]
    [InlineData("multiple_seeds")]
    [InlineData("combat_condition")]
    [InlineData("pending_resolution")]
    [InlineData("extra_update")]
    [InlineData("wrapper_realm")]
    [InlineData("wrapper_realm_type")]
    public async Task InvalidNaturalStartCannotPublishAnyRecovery(string mutation)
    {
        await using var fixture = await CreateCleanStartFixtureAsync();
        await StageAsync(fixture);
        var root = await WriteCleanStartAsync(fixture);
        var seed = root[AfterlifeSpiritualConflictState.ResponseField]!["conflictState"]!;
        switch (mutation)
        {
            case "exchange": seed["exchangeLog"]!.AsArray().Add(new JsonObject { ["exchangeId"] = "not_a_clean_start" }); break;
            case "recent": root["recentConflicts"]!.AsArray().Add(new JsonObject { ["conflictId"] = "forged_history" }); break;
            case "canonical": root = AfterlifeSpiritualConflictState.ApplyUpdate(root, root[AfterlifeSpiritualConflictState.ResponseField]!.AsObject()); break;
            case "wrong_realm": seed["realm"] = "Shining Abode"; break;
            case "invalid_actor": seed["oppositionSide"]!["leadContestant"]!.AsObject().Remove("displayName"); break;
            case "missing_log": seed.AsObject().Remove("exchangeLog"); break;
            case "wrong_log_type": seed["exchangeLog"] = new JsonObject(); break;
            case "multiple_seeds": root[AfterlifeSpiritualConflictState.ResponseField]!["conflictSeed"] = seed.DeepClone(); break;
            case "combat_condition": seed["combatConditions"] = new JsonArray(); break;
            case "pending_resolution": seed["resolutionState"] = "ready_to_resolve"; break;
            case "wrapper_realm": root[AfterlifeSpiritualConflictState.ResponseField]!["realm"] = "Shining Abode"; break;
            case "wrapper_realm_type": root[AfterlifeSpiritualConflictState.ResponseField]!["realm"] = new JsonObject(); break;
            case "extra_update": root[AfterlifeSpiritualConflictState.ResponseField]!["exchange"] = new JsonObject(); break;
        }
        await fixture.Files.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var before = fixture.CaptureMutationRoots([AfterlifeSpiritualConflictState.StatePath]);
        var issues = await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease);
        Assert.Contains(issues, issue => issue.Severity == IssueSeverity.Error);
        Assert.False(AcceptedMechanicsPlanAuthority.HasValidated(fixture.Files, lease));
        fixture.AssertMutationRootsUnchanged(before);
    }

    [Fact]
    public async Task CleanStartRequiresItsOwnCompositionBeforeExecution()
    {
        await using var fixture = await CreateCleanStartFixtureAsync();
        await StageAsync(fixture);
        var raw = await WriteCleanStartAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var admission = await AfterlifeWoundHealingPlanner.Continuation.AdmitNaturalAsync(fixture.Files, lease,
            await fixture.ReadManifestAsync(lease));
        var owner = Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(admission.Owner);
        try
        {
            Assert.Throws<InvalidDataException>(() => owner.Prepare());
            var roots = owner.SourceRoots;
            var original = roots[AfterlifeSpiritualConflictState.StatePath]!.AsObject();
            var acceptedConflict = AfterlifeSpiritualConflictState.ApplyUpdate(original,
                raw[AfterlifeSpiritualConflictState.ResponseField]!.AsObject());
            var originalRoots = new AfterlifeResourceOwnerRoots(roots[WoundCarrierCatalog.AfterlifeProfilesPath]!.AsObject(), original,
                roots["game_state/meta/soul_state.json"]!.AsObject(), roots[ShiningAbodeState.StatePath]?.AsObject());
            var accepted = new AfterlifeResourceOwnerRoots(originalRoots.Profiles, acceptedConflict,
                originalRoots.SoulState, originalRoots.ShiningAbode);
            var definitions = ResourceDefinitionCatalog.ParseCanonical(await fixture.Files.ReadFileAsync(lease,
                ResourceMaterializationContract.DefinitionsPath), false).Catalog!;
            var actual = AfterlifeResourceOwnerComposer.Compose(new(definitions, originalRoots, accepted));
            AssertNoCleanStartErrors(actual.Issues);
            var differentConflict = actual.OwnerCompanionAfterImages[AfterlifeSpiritualConflictState.StatePath].DeepClone().AsObject();
            differentConflict["activeConflict"]!["conflictId"] = "foreign_clean_start";
            var foreign = actual with { OwnerCompanionAfterImages = new Dictionary<string, JsonObject>(actual.OwnerCompanionAfterImages)
                { [AfterlifeSpiritualConflictState.StatePath] = differentConflict } };
            var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
            var before = fixture.CaptureMutationRoots([AfterlifeSpiritualConflictState.StatePath]);
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.BindNaturalConflictCompositionAsync(validator, definitions, accepted, foreign));
            Assert.Throws<InvalidDataException>(() => owner.Prepare());
            var exports = actual.Authority!.ExportInput();
            var invalidAuthority = ResourceOwnerAuthority.Build(exports with
                { SameTurnOwners = exports.SameTurnOwners.Concat(exports.SameTurnOwners.Take(1)).ToArray() });
            Assert.NotEmpty(invalidAuthority.Issues);
            await Assert.ThrowsAsync<InvalidDataException>(() => owner.BindNaturalConflictCompositionAsync(validator,
                definitions, accepted, actual with { Authority = invalidAuthority }));
            AssertNoCleanStartErrors(await owner.BindNaturalConflictCompositionAsync(validator, definitions, accepted, actual));
            owner.RequireExecutionCurrent();
            Assert.NotNull(owner.Prepare());
            fixture.AssertMutationRootsUnchanged(before);
        }
        finally { owner.Close(); }
    }

    [Theory]
    [InlineData("settings")]
    [InlineData("request")]
    [InlineData("receipt")]
    [InlineData("conflict")]
    public async Task BoundCleanStartRefusesDependencyDriftBeforeWrites(string dependency)
    {
        await using var fixture = await CreateCleanStartFixtureAsync();
        await StageAsync(fixture);
        await WriteCleanStartAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        AssertNoCleanStartErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var result));
        var owner = result.Plan!.SpiritualHealingContinuation!;
        var path = dependency switch
        {
            "settings" => AfterlifeSpiritualConflictState.DifficultySettingsPath,
            "request" => "input/turn_request.json",
            "receipt" => SpiritualWoundOpportunityReceiptState.StatePath,
            _ => AfterlifeSpiritualConflictState.StatePath
        };
        var original = await fixture.Files.ReadFileBytesAsync(lease, path);
        await fixture.Files.WriteFileAtomicAsync(lease, path, original is null ? "{}" : CanonicalJsonUtf8.DecodeOneOptionalBom(original) + " ");
        var before = fixture.CaptureMutationRoots([AfterlifeSpiritualConflictState.StatePath, path]);
        Assert.Throws<InvalidDataException>(() => owner.RequireExecutionCurrent());
        Assert.Throws<InvalidDataException>(() => owner.RequireNaturalConflictPlan(result.Plan));
        fixture.AssertMutationRootsUnchanged(before);
    }

    [Fact]
    public async Task CleanStartCannotUseAClosedOrReacquiredPublicationLease()
    {
        await using var fixture = await CreateCleanStartFixtureAsync();
        await StageAsync(fixture);
        await WriteCleanStartAsync(fixture);
        var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
        AcceptedMechanicsPlan prior;
        await using (var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync())
        {
            await fixture.WriteReportAsync(lease);
            AssertNoCleanStartErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease));
            Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var result));
            prior = result.Plan!;
        }
        await using var current = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        Assert.Throws<InvalidOperationException>(() => prior.SpiritualHealingContinuation!.RequireExecutionCurrent());
        Assert.Throws<InvalidDataException>(() => prior.SpiritualHealingContinuation!.RequireCurrent(fixture.Files, current));
        AssertNoCleanStartErrors(await validator.PrepareSpiritualSelfTreatmentPublicationAsync(current));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, current, out _, out var fresh));
        Assert.NotSame(prior, fresh.Plan);
        fresh.Plan!.SpiritualHealingContinuation!.RequireExecutionCurrent();
    }

    [Fact]
    public async Task CleanStartPublicationCutRestoresRecoveryConflictAndResourcesTogether()
    {
        KnownRollbackPublicationCut? cut = null;
        var hooks = new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) => cut?.Hooks.LocalPublicationObserver?.Invoke(phase, index) };
        await using var fixture = await CreateCleanStartFixtureAsync(hooks);
        await StageAsync(fixture);
        await WriteCleanStartAsync(fixture);
        await using var lease = await fixture.Files.AcquireCanonicalWriteLeaseAsync();
        await fixture.WriteReportAsync(lease);
        AssertNoCleanStartErrors(await new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance)
            .PrepareSpiritualSelfTreatmentPublicationAsync(lease));
        Assert.True(AcceptedMechanicsPlanAuthority.TryPeekValidated(fixture.Files, lease, out _, out var result));
        var before = fixture.CaptureMutationRoots(result.Plan!.BeforeImages.Keys);
        var generation = CleanupPublicationCut.ReadOptional(fixture.Files.SessionGenerationPath);
        cut = new KnownRollbackPublicationCut(fixture.Files.ResolvePath(WoundHistoryState.HistoryPath));
        cut.Attach(fixture.Files);
        var failure = await Record.ExceptionAsync(() => new CanonicalStateNormalizer(fixture.Files,
            NullLogger<CanonicalStateNormalizer>.Instance).BindTo(lease).NormalizeAcceptedMechanicsAsync(backups: null));
        Assert.Same(cut.Failure, failure);
        cut.AssertRestored(before, generation, failure, _ => { });
        fixture.AssertMutationRootsUnchanged(before);
        Assert.Throws<InvalidDataException>(() => result.Plan.SpiritualHealingContinuation!.RequireExecutionCurrent());
    }

    private static Task<HealingFixture> CreateCleanStartFixtureAsync(FileSystemManagerHooks? hooks = null) => HealingFixture.CreateAsync(3, 0, 20,
        stageRequest: false, hooks: hooks, beforeResourceBootstrap: (_, profiles) =>
        {
            var guardian = AfterlifeActorMaterializationTestFixture.CreateCompleteProfile("guardian", "guardian_clean_start", "Chaos Sea", 42);
            guardian["standardArts"] = AfterlifeSpiritualConflictState.CreateDefaultArtTiers();
            guardian["specialArts"] = new JsonArray();
            profiles["profiles"]!.AsArray().Add(guardian);
            return Task.CompletedTask;
        });

    private static async Task<JsonObject> WriteCleanStartAsync(HealingFixture fixture)
    {
        var root = JsonNode.Parse((await fixture.Files.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath))!)!.AsObject();
        root[AfterlifeSpiritualConflictState.ResponseField] = new JsonObject
        {
            ["mode"] = "start", ["conflictState"] = new JsonObject
            {
                ["conflictId"] = "natural_clean_start", ["realm"] = "Chaos Sea", ["dangerMode"] = "hostile",
                ["status"] = "active", ["resolutionState"] = "active", ["sideModel"] = "direct_duel",
                ["playerSideStrain"] = "clear", ["oppositionSideStrain"] = "clear", ["conflictPosition"] = "contested",
                ["exchangeLog"] = new JsonArray(),
                ["playerSide"] = new JsonObject { ["leadContestant"] = new JsonObject
                    { ["actorType"] = "player", ["actorId"] = "player_soul", ["displayName"] = "Душа" }, ["supporters"] = new JsonArray() },
                ["oppositionSide"] = new JsonObject
                {
                    ["leadContestant"] = new JsonObject { ["actorType"] = "guardian", ["actorId"] = "guardian_clean_start",
                        ["displayName"] = "Хранитель", ["actorArtTierSnapshot"] = AfterlifeSpiritualConflictState.CreateDefaultArtTiers(),
                        ["artAuthoritySource"] = "afterlife_entity_profiles" }, ["supporters"] = new JsonArray(),
                    ["resourceMaterialization"] = new JsonObject { ["resources"] = new JsonArray(new JsonObject
                        { ["resourceKey"] = "spiritual_action_points", ["maximum"] = 6 }) }
                }
            }
        };
        await fixture.Files.WriteFileAtomicAsync(AfterlifeSpiritualConflictState.StatePath, root.ToJsonString());
        return root;
    }

    private static void AssertNoCleanStartErrors(IEnumerable<ValidationIssue> issues) =>
        Assert.True(!issues.Any(issue => issue.Severity == IssueSeverity.Error), string.Join("\n", issues.Select(issue => issue.Code + ": " + issue.Actual)));
}
