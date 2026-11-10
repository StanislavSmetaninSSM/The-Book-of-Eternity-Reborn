using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;
using HealingFixture = BookOfEternityClient.Tests.AfterlifeWoundHealingLifecycleTests.Fixture;

namespace BookOfEternityClient.Tests;

public sealed class WoundGuardianJournalPublicationLifecycleTests(ITestOutputHelper output)
{
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string GuardianId = "guardian_alpha";
    private const string ProjectId = "proj_active";
    private const string NextState = "Хранитель проверяет опоры внешнего кольца.";

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ExistingGuardianProjectUpdate_WithSignedSelfHealing_UsesCommonPublicationOrExactCompensation(
        bool failAfterWoundHistory)
    {
        KnownRollbackPublicationCut? cut = null;
        FileSystemManager? observedFiles = null;
        Dictionary<string, byte[]?>? observedAtCut = null;
        var observedPaths = new[] { GuardianProjectState.TrackerPath, GuardianProjectState.JournalPath,
            WoundCarrierCatalog.AfterlifeProfilesPath, EffectIdentityState.StatePath, WoundHistoryState.HistoryPath };
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (cut is null) return;
                try { cut.Hooks.LocalPublicationObserver?.Invoke(phase, index); }
                catch (Exception error) when (ReferenceEquals(error, cut.Failure))
                {
                    observedAtCut = ReadBytes(observedFiles!, observedPaths);
                    throw;
                }
            }
        };
        var tracker = CreateTracker();
        var journal = CreateJournal();
        string? ownedRoot = null;
        await using var fixture = await HealingFixture.CreateAsync(1, 1, 20, hooks: hooks,
            allocatedRootForDiagnostics: root => ownedRoot = root,
            beforeResourceBootstrap: async (files, _) =>
            {
                var guardian = PlayerGuardianFoundationValidationTests.CreateGuardian(GuardianId, "Азалия", "abode_alpha");
                var guardians = new JsonObject { ["guardians"] = new JsonArray(guardian),
                    ["activeGuardian"] = guardian.DeepClone(),
                    ["chaosSeaNavigation"] = new JsonObject { ["currentAbodeId"] = "abode_alpha",
                        ["discoveredAbodes"] = new JsonArray("abode_alpha") } };
                guardians = CanonicalStateNormalizer.BuildGuardianAuthorityRootForValidation(
                    guardians, guardians, null, null, null, currentTurn: 42);
                await files.WriteFileAtomicAsync(GuardiansPath, guardians.ToJsonString());
                await files.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, tracker.ToJsonString());
                await files.WriteFileAtomicAsync(GuardianProjectState.JournalPath, journal.ToJsonString());
                await files.WriteFileAtomicAsync(MortalItemIdentityState.StatePath,
                    MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
            });
        // This guard writes cleanup evidence even if a later oracle fails.
        using var owned = new CleanupOwnedFixture(Assert.IsType<string>(ownedRoot), output.WriteLine);
        var files = fixture.Files;
        observedFiles = files;
        var validator = new ValidationService(files, NullLogger<ValidationService>.Instance);
        LiveTurnPendingSnapshotManifest manifest;
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
        {
            manifest = await fixture.ReadManifestAsync(lease);
            await fixture.WriteReportAsync(lease);
        }
        Assert.Equal("Chaos Sea", JsonNode.Parse((await files.ReadFileAsync("game_state/meta/soul_state.json"))!)!["currentRealm"]!.GetValue<string>());
        Assert.NotNull(manifest.ProgressionControl);
        Assert.Equal(1, manifest.ProgressionControl!.GuardianProjectCyclesExpectedThisTurn);
        foreach (var path in new[] { GuardiansPath, GuardianProjectState.TrackerPath, GuardianProjectState.JournalPath })
        {
            Assert.True(manifest.Files.TryGetValue(path, out var backup), path);
            Assert.Equal(await files.ReadFileBytesAsync(path), await files.ReadFileBytesAsync(backup!));
        }
        AssertNoErrors(await validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.GuardianProjectStateFiles, new[] { GuardianProjectState.TrackerPath, GuardianProjectState.JournalPath })));
        var originalWound = Assert.Single((await fixture.ReadWoundsAsync()).Occurrences).Wound;
        var oldEffectIds = originalWound.Consequences.OwnedEffectSources.RootBindings.Select(row => row.EffectId).ToArray();
        Assert.NotEmpty(oldEffectIds);
        var retainedCommand = await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath);
        Assert.NotNull(retainedCommand);
        var response = new GameResponse { GuardianProjectUpdates = new[] { JsonSerializer.SerializeToElement(new
            { guardianId = GuardianId, projectId = ProjectId, activeState = NextState }) } };
        var wire = JsonSerializer.SerializeToElement(response, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        AssertNoErrors(validator.ValidateResponse(wire));
        var foreign = JsonNode.Parse(wire.GetRawText())!.AsObject();
        foreign["guardianProjectUpdates"]![0]!["projectId"] = "project_not_in_signed_original";
        Assert.Contains(validator.ValidateResponse(JsonSerializer.SerializeToElement(foreign)), issue =>
            issue.Code == "guardian_project_update_unknown_project_id" && issue.Severity == IssueSeverity.Error);
        await new StateDistributor(files, NullLogger<StateDistributor>.Instance).DistributeAsync(response);
        Assert.Equal(retainedCommand, await files.ReadFileBytesAsync(AcceptedMechanicsPlan.WoundCommandPath));
        var staged = JsonNode.Parse((await files.ReadFileAsync(GuardianProjectState.TrackerPath))!)!.AsObject();
        Assert.True(JsonNode.DeepEquals(tracker["activeProjects"], staged["activeProjects"]));
        Assert.Equal(NextState, Assert.Single(staged["guardianProjectUpdates"]!.AsArray())!["activeState"]!.GetValue<string>());
        AssertNoErrors(await validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        // Staged canonical arrays deliberately remain the signed before image. The public
        // command gate above validates intent; only the real normalizer derives its after image.
        await using (var lease = await files.AcquireCanonicalWriteLeaseAsync())
            AssertNoErrors(await validator.ValidateAcceptedTurnRawResourceMaterializationAsync(lease, deferSpiritualSelfTreatmentPublication: true));
        var before = fixture.CaptureMutationRoots(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(manifest.Files.Values).Concat(new[] { "input/turn_request.json", LiveTurnPreparationService.PendingTurnSnapshotManifestPath }));
        Assert.Contains(before, pair => pair.Value is null);
        Assert.Contains(before, pair => pair.Value is not null);
        var generationBefore = CleanupPublicationCut.ReadOptional(files.SessionGenerationPath);
        if (failAfterWoundHistory)
        {
            cut = new KnownRollbackPublicationCut(files.ResolvePath(WoundHistoryState.HistoryPath));
            cut.Attach(files);
        }
        AcceptedTurnCanonicalStateRefresh.Result? refreshed = null;
        var failure = await Record.ExceptionAsync(async () => refreshed =
            await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(files,
                new CanonicalStateNormalizer(files, NullLogger<CanonicalStateNormalizer>.Instance), validator, manifest.Files));
        if (failAfterWoundHistory)
        {
            output.WriteLine(JsonSerializer.Serialize(new { kind = "wound-guardian-journal-owner-at-cut", observedAtCut }));
            Assert.Same(cut!.Failure, failure); // Real self-treatment owns the multi-member batch.
            cut.AssertRestored(before, generationBefore, failure, output.WriteLine);
            Assert.NotNull(observedAtCut);
            AssertPublishedOwnerData(observedAtCut!, tracker, journal, originalWound.WoundId, oldEffectIds);
            Assert.Null(refreshed);
        }
        else
        {
            Assert.Null(failure);
            var result = Assert.IsType<AcceptedTurnCanonicalStateRefresh.Result>(refreshed);
            AssertNoErrors(result.Issues);
            Assert.Null(result.TreatmentResourcePublicationTransaction);
            var plan = Assert.IsType<AcceptedMechanicsPlan>(result.MechanicsPlan);
            Assert.IsType<AfterlifeWoundHealingPlanner.Continuation>(plan.SpiritualHealingContinuation);
            var after = ReadBytes(files, observedPaths.Concat(new[] { WoundIdentityState.StatePath, ProgressionScheduleService.SchedulePath }));
            output.WriteLine(JsonSerializer.Serialize(new { kind = "wound-guardian-journal-owner-published", before, after }));
            AssertPublishedOwnerData(after, tracker, journal, originalWound.WoundId, oldEffectIds);
            var identities = WoundIdentityState.Parse(Text(after[WoundIdentityState.StatePath]), WoundIdentityState.StatePath);
            Assert.True(identities.IsValid, Describe(identities.Issues));
            var identity = Assert.Single(identities.State!.Entries, row => row.WoundId == originalWound.WoundId);
            Assert.Equal("healed", identity.Status);
            await fixture.AssertOneCycleAndRetainedAttemptAsync();
            var schedule = JsonNode.Parse(Text(after[ProgressionScheduleService.SchedulePath]))!;
            Assert.Equal(1, schedule["lastGuardianProjectCycleOrdinal"]!.GetValue<int>());
            fixture.AssertResourcesUnchanged();
            AssertNoErrors(await validator.ValidateGameStateAsync(new GameStateValidationSelection(
                GameStateValidationPhase.GuardianProjectStateFiles, new[] { GuardianProjectState.JournalPath })));
        }
    }

    private static void AssertPublishedOwnerData(IReadOnlyDictionary<string, byte[]?> actual,
        JsonObject initialTracker, JsonObject initialJournal, string woundId, string[] oldEffectIds)
    {
        JsonObject Read(string path) => JsonNode.Parse(Text(actual[path]))!.AsObject();
        var expectedTracker = initialTracker.DeepClone().AsObject();
        expectedTracker["activeProjects"]![0]!["project"]!["activeState"] = NextState;
        Assert.True(JsonNode.DeepEquals(expectedTracker, Read(GuardianProjectState.TrackerPath)));
        var actualJournal = Read(GuardianProjectState.JournalPath);
        var entries = actualJournal["entries"]!.AsArray();
        Assert.Equal(2, entries.Count);
        Assert.True(JsonNode.DeepEquals(initialJournal["entries"]![0], entries[0]));
        var appended = Assert.IsType<JsonObject>(entries[1]);
        Assert.StartsWith($"gpj_{GuardianId}_{ProjectId}_43_progressed_", appended["entryId"]!.GetValue<string>());
        Assert.Equal(43, appended["turn"]!.GetValue<int>());
        Assert.Equal(GuardianId, appended["guardianId"]!.GetValue<string>());
        Assert.Equal(ProjectId, appended["projectId"]!.GetValue<string>());
        Assert.Equal("progressed", appended["eventType"]!.GetValue<string>());
        Assert.Equal("player_known", appended["visibility"]!.GetValue<string>());
        Assert.Contains(appended["details"]!.AsArray(), row => row!.GetValue<string>() == "Состояние: " + NextState);
        var player = Assert.Single(Read(WoundCarrierCatalog.AfterlifeProfilesPath)["profiles"]!.AsArray(),
            row => row!["actorId"]!.GetValue<string>() == "player_soul")!;
        Assert.Empty(player["activeWounds"]!.AsArray());
        Assert.DoesNotContain(player["activeEffects"]!.AsArray(), row => oldEffectIds.Contains(row!["effectId"]!.GetValue<string>()));
        using var effectJson = JsonDocument.Parse(Text(actual[EffectIdentityState.StatePath]));
        var effects = EffectIdentityState.Parse(effectJson.RootElement, EffectIdentityState.StatePath);
        Assert.Empty(effects.Issues);
        foreach (var id in oldEffectIds) Assert.Equal("expired", Assert.Single(effects.State!.Entries, row => row.EffectId == id).State);
        var parsed = WoundHistoryState.Parse(Text(actual[WoundHistoryState.HistoryPath]), WoundHistoryState.HistoryPath);
        Assert.True(parsed.IsValid, Describe(parsed.Issues));
        var terminal = Assert.Single(parsed.State!.Transitions, row => row.Terminal);
        Assert.Equal(woundId, terminal.WoundId);
        Assert.Equal("heal", terminal.Kind);
        var self = Assert.Single(parsed.State.Transitions.Select(row => row.TransitionResult).OfType<SpiritualHealingPersistedResult>());
        Assert.Equal("self", self.Mode);
        Assert.Equal(2, self.SchemaVersion);
    }

    private static JsonObject CreateTracker() => new()
    {
        ["activeProjects"] = new JsonArray(new JsonObject { ["guardianId"] = GuardianId, ["project"] = new JsonObject
        {
            ["projectId"] = ProjectId, ["projectType"] = "abode_expansion", ["projectTier"] = "major", ["projectMode"] = "internal",
            ["projectName"] = "Расширение Обители", ["name"] = "Расширение Обители", ["activeState"] = "Подготовка внешнего кольца.",
            ["totalWork"] = 18, ["workDone"] = 6, ["totalStages"] = 3, ["currentStage"] = 1, ["pressure"] = 4, ["stability"] = 72
        } }), ["completedProjects"] = new JsonArray(), ["temporaryProjectModifiers"] = new JsonArray()
    };
    private static JsonObject CreateJournal() => new() { ["entries"] = new JsonArray(new JsonObject
    {
        ["entryId"] = "gpj_existing", ["turn"] = 42, ["guardianId"] = GuardianId, ["projectId"] = ProjectId,
        ["eventType"] = "started", ["visibility"] = "player_known", ["title"] = "Проект начат",
        ["summary"] = "Хранитель начал расширять внешнее кольцо Обители.", ["details"] = new JsonArray("Сохранить прежнюю запись без изменений.")
    }) };
    private static Dictionary<string, byte[]?> ReadBytes(FileSystemManager files, IEnumerable<string> paths) =>
        paths.Distinct(StringComparer.Ordinal).ToDictionary(path => path,
            path => CleanupPublicationCut.ReadOptional(files.ResolvePath(path)), StringComparer.Ordinal);
    private static string Text(byte[]? bytes) => CanonicalJsonUtf8.DecodeOneOptionalBom(Assert.IsType<byte[]>(bytes));
    private static string Describe(IEnumerable<ValidationIssue> issues) => string.Join("\n", issues.Select(i => i.Code + "@" + i.FilePath + ":" + i.Message + ":" + i.Actual));
    private static void AssertNoErrors(IEnumerable<ValidationIssue> issues) => Assert.True(issues.All(i => i.Severity != IssueSeverity.Error), Describe(issues));
}
