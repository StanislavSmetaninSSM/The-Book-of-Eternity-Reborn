using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

public sealed class WoundQuestPublicationLifecycleTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task ExistingRegularQuestAppend_WithSignedWound_UsesCommonPublicationOrExactCompensation(
        bool failAfterWoundHistory) =>
        WoundMaterializationLifecycleTests.ExerciseRegularQuestPublicationAsync(
            failAfterWoundHistory, output.WriteLine);
}

public sealed partial class WoundMaterializationLifecycleTests
{
    /// <summary>
    /// Exercises one real signed acquisition and an existing quest's ordinary append owner.
    /// The failed row restores staged inputs, not the earlier Engine turn snapshot.
    /// </summary>
    internal static async Task ExerciseRegularQuestPublicationAsync(
        bool failAfterWoundHistory, Action<string> output)
    {
        const string questPath = "game_state/quests/regular_quests.json";
        const string questId = "quest_cross_the_pass";
        const string previousEntry = "#[41]. Путник начал переход через перевал.";
        const string appendedEntry = "#[42]. Обвал ранил левое предплечье; путник отступил от осыпи.";
        KnownRollbackPublicationCut? cut = null;
        ResourceMaterializationTestContext? observedContext = null;
        Dictionary<string, byte[]?>? observedAtCut = null;
        var observedPaths = new[]
        {
            questPath, WoundCarrierCatalog.PlayerPath, EffectCarrierCatalog.PlayerPath,
            WoundHistoryState.HistoryPath
        };
        var hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = (phase, index) =>
            {
                if (cut is null) return;
                try { cut.Hooks.LocalPublicationObserver?.Invoke(phase, index); }
                catch (Exception error) when (ReferenceEquals(error, cut.Failure))
                {
                    // The existing cut already verified MemberPublished and its actual after hash.
                    // Read raw bytes now, before the publisher or outer refresh compensates.
                    observedAtCut = observedPaths.ToDictionary(path => path,
                        path => CleanupPublicationCut.ReadOptional(observedContext!.FileSystem.ResolvePath(path)),
                        StringComparer.Ordinal);
                    throw;
                }
            }
        };
        await using var context = await CreatePlayerContextAsync(hooks);
        using var owned = new CleanupOwnedFixture(context.RootPath, output);
        observedContext = context;
        await context.WriteExactJsonAsync("game_state/meta/soul_state.json",
            new JsonObject { ["currentRealm"] = "Mortal World" }.ToJsonString());
        await context.WriteExactJsonAsync(MortalItemIdentityState.StatePath,
            MortalItemIdentityState.CreateEmptyRoot().ToJsonString());
        var originalQuest = new JsonObject
        {
            ["questId"] = questId,
            ["questName"] = "Перейти перевал",
            ["status"] = "Active",
            ["questGiver"] = "Путник",
            ["questBackground"] = "Путь к поселению проходит через перевал.",
            ["description"] = "Найти безопасный путь через горы.",
            ["objectives"] = new JsonArray(new JsonObject
            {
                ["objectiveId"] = "objective_cross_the_pass",
                ["description"] = "Добраться до противоположного склона.",
                ["status"] = "Active"
            }),
            ["detailsLog"] = new JsonArray(previousEntry)
        };
        var originalRoot = new JsonObject { ["quests"] = new JsonArray(originalQuest) };
        AssertNoQuestPublicationErrors(context.Validator.ValidateResponse(ToElement(originalRoot)));
        await context.WriteExactJsonAsync(questPath, originalRoot.ToJsonString());
        var originalQuestBytes = await context.FileSystem.ReadFileBytesAsync(questPath);
        var authority = await CreateSignedAuthorityAsync(context, maximumSeverityRank: 2,
            extraSnapshotPaths: new[] { questPath });
        var manifest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(
            "game_state/control/pending_turn_snapshot.json"));
        var snapshotQuestPath = manifest["files"]![questPath]!.GetValue<string>();
        Assert.Equal(originalQuestBytes, await context.FileSystem.ReadFileBytesAsync(snapshotQuestPath));

        var decision = Decision("materialize", CreateRepairRoundtripProposal("treatment"));
        decision["opportunityRef"] = authority.Opportunity.PublicRef;
        var response = Response(decision);
        response.UpdateQuests = new[] { ToElement(new JsonObject
        {
            ["questId"] = questId, ["newDetailsLogEntry"] = appendedEntry
        }) };
        var wire = JsonSerializer.SerializeToElement(response, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
        AssertNoQuestPublicationErrors(context.Validator.ValidateResponse(wire));
        var foreignQuest = JsonNode.Parse(wire.GetRawText())!.AsObject();
        foreignQuest["UpdateQuests"]![0]!["questId"] = "quest_not_in_signed_original";
        Assert.Contains(context.Validator.ValidateResponse(ToElement(foreignQuest)), issue =>
            issue.Code == "quest_update_unknown_existing_quest" && issue.Severity == IssueSeverity.Error);

        var composition = WoundResponseInputComposer.Compose(authority.Binding,
            new[] { authority.Opportunity }, response.WoundDecisions, response.Response,
            Array.Empty<WoundOpportunityDecisionReceipt>());
        Assert.True(composition.Success, Describe(composition.Issues));
        await new StateDistributor(context.FileSystem, NullLogger<StateDistributor>.Instance)
            .DistributeAsync(response, composition);
        var stagedQuest = Assert.IsType<JsonObject>(await context.ReadJsonAsync(questPath));
        Assert.True(JsonNode.DeepEquals(originalQuest, Assert.Single(stagedQuest["quests"]!.AsArray())));
        Assert.Equal(appendedEntry, Assert.Single(stagedQuest["UpdateQuests"]!.AsArray())!["newDetailsLogEntry"]!.GetValue<string>());
        AssertNoQuestPublicationErrors(await context.Validator.ValidateAcceptedTurnRawMortalItemMaterializationAsync());
        AssertNoQuestPublicationErrors(await context.Validator.ValidateAcceptedTurnRawResourceMaterializationAsync());

        var captured = await context.CaptureAsync(CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
            .Concat(new[] { "input/turn_request.json", "game_state/control/pending_turn_snapshot.json", snapshotQuestPath }));
        var before = captured.ToDictionary(pair => pair.Key, pair => pair.Value.Bytes, StringComparer.Ordinal);
        Assert.Contains(before, pair => pair.Value is null);
        Assert.Contains(before, pair => pair.Value is not null);
        var generationBefore = CleanupPublicationCut.ReadOptional(context.FileSystem.SessionGenerationPath);
        var backups = manifest["files"]!.AsObject().ToDictionary(pair => pair.Key,
            pair => pair.Value!.GetValue<string>(), StringComparer.Ordinal);
        if (failAfterWoundHistory)
        {
            cut = new KnownRollbackPublicationCut(context.FileSystem.ResolvePath(WoundHistoryState.HistoryPath));
            cut.Attach(context.FileSystem);
        }
        AcceptedTurnCanonicalStateRefresh.Result? refreshed = null;
        var failure = await Record.ExceptionAsync(async () => refreshed =
            await AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(
                context.FileSystem, context.Normalizer, context.Validator, backups));

        if (failAfterWoundHistory)
        {
            output(JsonSerializer.Serialize(new { kind = "wound-regular-quest-owner-at-cut", observedAtCut }));
            // A signed formal occurrence is not a QTE consume/receipt owner. This
            // genuine ordinary publication keeps the selected single-write wrapper.
            var writeFailure = Assert.IsType<CanonicalStateWriteException>(failure);
            Assert.Equal(WoundHistoryState.HistoryPath, writeFailure.RelativePath);
            Assert.Same(cut!.Failure, writeFailure.InnerException);
            cut.AssertRestored(before, generationBefore, failure, output);
            Assert.NotNull(observedAtCut);
            AssertQuestAndWoundPublished(observedAtCut!, originalRoot, appendedEntry, authority.Opportunity);
            Assert.Null(refreshed);
        }
        else
        {
            Assert.Null(failure);
            var result = Assert.IsType<AcceptedTurnCanonicalStateRefresh.Result>(refreshed);
            AssertNoQuestPublicationErrors(result.Issues);
            Assert.Null(result.TreatmentResourcePublicationTransaction);
            Assert.NotNull(result.MechanicsPlan?.WoundStageBundle);
            var after = observedPaths.Concat(new[]
            {
                WoundIdentityState.StatePath, MortalWoundOccurrenceState.StatePath,
                MortalWoundOpportunityReceiptState.StatePath
            }).ToDictionary(path => path,
                path => CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(path)), StringComparer.Ordinal);
            output(JsonSerializer.Serialize(new { kind = "wound-regular-quest-owner-published", after }));
            var published = AssertQuestAndWoundPublished(after, originalRoot, appendedEntry, authority.Opportunity);
            var identity = WoundIdentityState.Parse(
                QuestPublicationText(after[WoundIdentityState.StatePath]), WoundIdentityState.StatePath);
            Assert.True(identity.IsValid, Describe(identity.Issues));
            var entry = Assert.Single(identity.State!.Entries);
            Assert.Equal(published.WoundId, entry.WoundId);
            Assert.Equal("active", entry.Status);
            Assert.Equal(published.Ordinal, entry.LastTransitionOrdinal);
            var receipts = MortalWoundOpportunityReceiptState.Parse(
                QuestPublicationText(after[MortalWoundOpportunityReceiptState.StatePath]),
                MortalWoundOpportunityReceiptState.StatePath);
            Assert.True(receipts.IsValid, Describe(receipts.Issues));
            Assert.Empty(receipts.State!.Receipts);
            foreach (var path in new[] { MortalWoundOccurrenceState.StatePath, MortalWoundOpportunityReceiptState.StatePath })
            {
                Assert.DoesNotContain(path, result.MechanicsPlan!.OwnerCompanionAfterImages.Keys);
                Assert.Equal(before[path], after[path]);
            }
            Assert.Null(CleanupPublicationCut.ReadOptional(context.FileSystem.ResolvePath(AcceptedMechanicsPlan.WoundCommandPath)));
        }
    }

    private static void AssertNoQuestPublicationErrors(IEnumerable<ValidationIssue> issues) =>
        Assert.True(issues.All(issue => issue.Severity != IssueSeverity.Error), Describe(issues));

    private static string QuestPublicationText(byte[]? captured)
    {
        var bytes = Assert.IsType<byte[]>(captured);
        var preamble = System.Text.Encoding.UTF8.GetPreamble();
        return System.Text.Encoding.UTF8.GetString(
            bytes.AsSpan(bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0));
    }

    private static (string WoundId, string TransitionId, int Ordinal) AssertQuestAndWoundPublished(
        IReadOnlyDictionary<string, byte[]?> actual, JsonObject originalQuestRoot, string appendedEntry,
        WoundOpportunityAuthority opportunity)
    {
        JsonObject Read(string path) => JsonNode.Parse(QuestPublicationText(actual[path]))!.AsObject();
        var expectedQuest = originalQuestRoot.DeepClone().AsObject();
        expectedQuest["quests"]![0]!["detailsLog"]!.AsArray().Add(appendedEntry);
        Assert.True(JsonNode.DeepEquals(expectedQuest, Read("game_state/quests/regular_quests.json")),
            "The real quest owner must append once and preserve every other canonical field.");
        var wound = Assert.IsType<JsonObject>(Assert.Single(Read(WoundCarrierCatalog.PlayerPath)["activeWounds"]!.AsArray()));
        Assert.Equal("II", wound["severity"]!["value"]!.GetValue<string>());
        Assert.Equal(opportunity.OpportunityId, wound["origin"]!["opportunityId"]!.GetValue<string>());
        Assert.Equal(opportunity.EventRef, wound["origin"]!["eventRef"]!.GetValue<string>());
        var woundId = wound["woundId"]!.GetValue<string>();
        var transition = Assert.IsType<JsonObject>(Assert.Single(Read(WoundHistoryState.HistoryPath)["transitions"]!.AsArray()));
        Assert.Equal("create", transition["kind"]!.GetValue<string>());
        Assert.Equal(woundId, transition["woundId"]!.GetValue<string>());
        Assert.Equal(opportunity.EventRef, transition["eventRef"]!.GetValue<string>());
        var binding = Assert.Single(wound["consequences"]!["ownedEffectSources"]!["rootBindings"]!.AsArray());
        var effect = Assert.Single(Read(EffectCarrierCatalog.PlayerPath)["activeEffects"]!.AsArray().OfType<JsonObject>(),
            candidate => candidate["effectId"]?.GetValue<string>() == binding!["effectId"]!.GetValue<string>());
        Assert.Equal("wound", effect["source"]!["kind"]!.GetValue<string>());
        Assert.Equal(woundId, effect["source"]!["sourceId"]!.GetValue<string>());
        return (woundId, transition["transitionId"]!.GetValue<string>(), transition["woundTransitionOrdinal"]!.GetValue<int>());
    }
}
