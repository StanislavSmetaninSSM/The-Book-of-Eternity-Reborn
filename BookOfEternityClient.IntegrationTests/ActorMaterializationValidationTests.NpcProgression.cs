using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ActorMaterializationValidationTests
{
    [Theory]
    [InlineData("missing", false)]
    [InlineData("wrong_threshold", false)]
    [InlineData("negative", false)]
    [InlineData("fractional", false)]
    [InlineData("terminal_cap", false)]
    [InlineData("standard", true)]
    [InlineData("table", true)]
    [InlineData("none", true)]
    public async Task NewMortalNpc_PublicValidationRequiresCoherentExplicitProgression(string scenario, bool valid)
    {
        const string actorId = "npcref_explicit_progression";
        var root = JsonNode.Parse(BuildMortalActorStateJson(actorId, "NPCsInScene",
            canTeach: false, includeEnvelope: true))!.AsObject();
        var actor = root["NPCsInScene"]![0]!.AsObject();
        ConfigureSameTurnMortalActor(actor, actorId, "mat_explicit_progression");
        actor["level"] = 2;
        actor["experience"] = 17;
        actor["experienceForNextLevel"] = 565;
        actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"standard"}""");
        switch (scenario)
        {
            case "missing": actor.Remove("progressionProfile"); break;
            case "wrong_threshold": actor["experienceForNextLevel"] = 566; break;
            case "negative": actor["experience"] = -1; break;
            case "fractional": actor["level"] = 2.5m; break;
            case "terminal_cap":
                actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"standard","maxLevel":2,"capOverflow":"discard"}""");
                actor["experienceForNextLevel"] = 0; // terminal XP must also be zero, not the retained 17
                break;
            case "table":
                actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"table","thresholds":[100,200],"capOverflow":"discard"}""");
                actor["experienceForNextLevel"] = 200;
                break;
            case "none":
                actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"none"}""");
                actor["experience"] = 0;
                actor["experienceForNextLevel"] = 0;
                break;
        }
        var original = root.ToJsonString();
        using var document = JsonDocument.Parse(original);
        var responseIssues = _validator.ValidateResponse(document.RootElement);
        IReadOnlyList<ValidationIssue>? stateIssues = null;
        if (scenario == "missing")
        {
            await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", """{"currentRealm":"Mortal World","currentIncarnation":1}""");
            await WriteCurrentAndValidatedPreTurnAsync(NpcCoreChangesContract.NpcCorePath, original,
                """{"UpdateNPCs":[],"NPCsInScene":[]}""");
            stateIssues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.ActorMaterialization);
        }

        const string code = "npc_creation_progression_invalid";
        if (valid)
            Assert.DoesNotContain(responseIssues, issue => issue.Severity == IssueSeverity.Error);
        else
            Assert.Contains(responseIssues, issue => issue.Code == code &&
                issue.FilePath.Contains("NPCsInScene[0]", StringComparison.Ordinal));
        if (stateIssues is not null)
            Assert.Contains(stateIssues, issue => issue.Code == code);
        Assert.Equal(original, root.ToJsonString());

        if (scenario == "standard")
        {
            // No migration: a permanent actor with no profile remains outside this NEW-actor requirement.
            actor["NPCId"] = "npc_historical";
            actor.Remove("initialId");
            actor.Remove("progressionProfile");
            actor["materialization"]!["actorId"] = "npc_historical";
            using var historical = JsonDocument.Parse(root.ToJsonString());
            Assert.DoesNotContain(_validator.ValidateResponse(historical.RootElement), issue => issue.Code == code);
        }
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task NewPermanentMortalNpc_AuthenticatedOriginalAbsenceRequiresProfile(bool includeProfile)
    {
        const string actorId = "npc_new_permanent_progression";
        var root = JsonNode.Parse(BuildMortalActorStateJson(actorId, "UpdateNPCs",
            canTeach: true, includeEnvelope: true, includeInventory: true))!.AsObject();
        var actor = root["UpdateNPCs"]![0]!.AsObject();
        if (includeProfile)
            actor["progressionProfile"] = JsonNode.Parse("""{"schemaVersion":1,"mode":"table","thresholds":[100,150],"capOverflow":"discard"}""");
        else
            actor.Remove("progressionProfile");
        var current = root.ToJsonString();
        await WriteCurrentAndValidatedPreTurnAsync(NpcCoreChangesContract.NpcCorePath, current,
            """{"UpdateNPCs":[],"NPCsInScene":[]}""");
        using var document = JsonDocument.Parse(current);
        var issues = _validator.ValidateResponse(document.RootElement);
        if (includeProfile)
            Assert.DoesNotContain(issues, issue => issue.Severity == IssueSeverity.Error);
        else
            Assert.Contains(issues, issue => issue.Code == "npc_creation_progression_invalid" &&
                issue.Actor == $"mortal_npc:{actorId}");
        Assert.Equal(current, root.ToJsonString());
    }

    [Fact]
    public async Task LoadedHistoricalInitialId_WithoutProfileOrPendingTurn_DoesNotRequireCreationProfile()
    {
        const string actorId = "historical_initial_progression";
        var root = JsonNode.Parse(BuildMortalActorStateJson(actorId, "UpdateNPCs",
            canTeach: false, includeEnvelope: true))!.AsObject();
        var actor = root["UpdateNPCs"]![0]!.AsObject();
        ConfigureSameTurnMortalActor(actor, actorId, "mat_historical_initial_progression");
        actor.Remove("progressionProfile");
        // Loaded canonical history, not a response and not an actor proved absent from an authenticated original.
        await _fs.WriteFileAtomicAsync(NpcCoreChangesContract.NpcCorePath, root.ToJsonString());
        var issues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.ActorMaterialization);
        Assert.DoesNotContain(issues, issue => issue.Code == "npc_creation_progression_invalid");
    }

}
