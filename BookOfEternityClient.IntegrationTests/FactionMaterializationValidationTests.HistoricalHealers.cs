using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class FactionMaterializationValidationTests
{
    /// <summary>Exact signed actor history, rather than faction age or an authored turn, selects historical validation.</summary>
    [Theory]
    [InlineData("historical", true)] [InlineData("historical", false)]
    [InlineData("missing", false)] [InlineData("missing", true)]
    [InlineData("unusable", false)] [InlineData("unusable", true)]
    [InlineData("new_actor", true)] [InlineData("first_envelope", true)]
    [InlineData("changed_envelope", true)] [InlineData("duplicate_original", true)]
    [InlineData("changed_original_bytes", true)]
    public async Task FactionHealerHistoryUsesExactPreTurnActorAndPhase(string condition, bool raw)
    {
        var faction = BuildCompleteGenericStoryShiningFaction();
        var residents = BuildRouteResidentRoot(); var profiles = new List<JsonObject>();
        AuthorFixtureFactionHealer(faction, residents, profiles);
        var root = ShiningRoot(faction);
        root["halls"] = new JsonArray(BuildShiningHall("hall_dawn_archive", "Dawn Archive"));
        var initial = BuildAfterlifeProfileRoot(profiles);
        var original = initial.DeepClone().AsObject();
        var current = AfterlifeEntityProfileState.ProjectAcceptedRoot(initial, initial, new JsonObject
        {
            ["shiningAbodeCyclesProcessed"] = 1, ["newLastShiningAbodeCycleOrdinal"] = 1
        }, null, root);
        var actor = current["profiles"]![0]!.AsObject();
        Assert.Single(actor["progressionLedger"]!.AsArray());
        Assert.Empty(ActorMaterializationContract.ValidateHistoricalAfterlifeProfile(JsonSerializer.SerializeToElement(actor), "grown.healer"));
        if (condition == "new_actor") original["profiles"]!.AsArray().Clear();
        if (condition == "first_envelope") original["profiles"]![0]!.AsObject().Remove(ActorMaterializationContract.PropertyName);
        if (condition == "duplicate_original") original["profiles"]!.AsArray().Add(original["profiles"]![0]!.DeepClone());
        if (condition == "changed_envelope")
            actor["materialization"]!["sections"]!["progressionHistory"]!["reason"] = "A different authored creation reason.";

        await WriteCurrentAndSnapshotAsync((ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()),
            (AfterlifeProfilesPath, current.ToJsonString()),
            (SarefStoryPath, BuildSarefStoryRoot("revealed", "shine_faction_dawn_archive").ToJsonString()),
            (ShiningPath, root.ToJsonString()), (ResidentPath, residents.ToJsonString()), (AfterlifeProfilesPath, original.ToJsonString()));
        if (condition == "missing") _fs.DeleteFile(LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
        if (condition == "unusable")
            await _fs.WriteFileAtomicAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath, "{}");
        if (condition == "changed_original_bytes")
            await _fs.WriteFileAtomicAsync("game_state/control/pending_turn_snapshot/" + AfterlifeProfilesPath, "{}");
        var before = CaptureHealerAuthoredState();
        var issues = new List<ValidationIssue>();
        await _validator.ValidateAcceptedTurnShiningFactionMaterializationCompletenessAsync(raw, issues);
        AssertHealerAuthoredStateUnchanged(before);
        if (condition == "historical" || condition == "missing" && !raw)
        {
            Assert.DoesNotContain(issues, i => i.Severity == IssueSeverity.Error);
            Assert.True(JsonNode.DeepEquals(initial["profiles"]![0]!["materialization"], actor["materialization"]));
        }
        else if (condition is "new_actor" or "first_envelope")
            Assert.Contains(issues, i => i.Code == "actor_materialization_section_empty_surface_invalid");
        else if (condition == "changed_envelope")
            Assert.Contains(issues, i => i.Code == "actor_materialization_historical_envelope_changed");
        else
            Assert.Contains(issues, i => i.Code == "actor_materialization_pre_turn_authority_unusable");
    }
}
