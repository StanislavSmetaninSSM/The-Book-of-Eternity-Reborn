using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class EffectAfterlifeAdapterTests
{
    [Theory]
    [InlineData("player_soul", "player_soul", "player")]
    [InlineData("guardian", "guardian_effect_target", "guardian")]
    [InlineData("resident", "resident_effect_target", "resident")]
    [InlineData("shining_faction_head", "faction_head_effect_target", "afterlife_actor")]
    [InlineData("radiant_actor", "radiant_effect_target", "radiant_actor")]
    public async Task PersistentAfterlifeApply_PublishesOnlyToExactProfileCarrier(
        string actorType,
        string actorId,
        string targetKind)
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        var artId = $"art_{actorId}_exact_seal";
        var definition = CreateAfterlifeProfileDefinition(targetKind);
        await context.MaterializeAfterlifeActorAsync(
            actorType,
            actorId,
            "Shining Abode",
            artId,
            definition);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");

        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["target"] = new JsonObject
        {
            ["kind"] = targetKind,
            ["targetId"] = actorId
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = artId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject { ["value"] = -2 };
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        var plan = await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);
        Assert.NotNull(plan);

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var profile = root[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["actorId"]?.GetValue<string>(),
                actorId,
                StringComparison.Ordinal));
        var effect = Assert.IsType<JsonObject>(
            Assert.Single(profile["activeEffects"]!.AsArray()));
        Assert.Equal(targetKind, effect["target"]!["kind"]!.GetValue<string>());
        Assert.Equal(actorId, effect["target"]!["targetId"]!.GetValue<string>());
        Assert.DoesNotContain(
            root,
            property => property.Key.EndsWith("Effects", StringComparison.Ordinal) &&
                        !string.Equals(property.Key, "activeEffects", StringComparison.Ordinal));

        var index = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.IdentityIndexPath))!.AsObject();
        var entry = Assert.IsType<JsonObject>(Assert.Single(index["entries"]!.AsArray()));
        Assert.Equal(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            entry["owner"]!["carrierPath"]!.GetValue<string>());
        Assert.Equal("activeEffects", entry["owner"]!["collection"]!.GetValue<string>());
    }

    [Fact]
    public async Task UnrelatedProfileUpdate_PreservesClientOwnedActiveEffects()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string actorId = "guardian_effect_update";
        const string artId = "art_guardian_preserved_seal";
        await context.MaterializeAfterlifeActorAsync(
            "guardian",
            actorId,
            "Shining Abode",
            artId,
            CreateAfterlifeProfileDefinition("guardian"));
        await ApplyPersistentProfileEffectAsync(
            context,
            turn: 43,
            actorId,
            artId,
            targetKind: "guardian");

        var root = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var profile = root[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["actorId"]?.GetValue<string>(),
                actorId,
                StringComparison.Ordinal));
        var expectedEffectId = Assert.IsType<JsonObject>(
            Assert.Single(profile["activeEffects"]!.AsArray()))["effectId"]!
            .GetValue<string>();
        var update = profile.DeepClone().AsObject();
        update.Remove("activeEffects");
        update["displayName"] = "Хранитель сохранённого следа";
        root[AfterlifeEntityProfileState.UpdateProperty] = new JsonArray(update);

        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 44,
            currentRealm: "Shining Abode");
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath,
            root);

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups);

        var canonical = (await context.ReadJsonAsync(
            EffectMaterializationTestContext.AfterlifeProfilesPath))!.AsObject();
        var updated = canonical[AfterlifeEntityProfileState.ProfilesProperty]!
            .AsArray()
            .OfType<JsonObject>()
            .Single(candidate => string.Equals(
                candidate["actorId"]?.GetValue<string>(),
                actorId,
                StringComparison.Ordinal));
        Assert.Equal(
            "Хранитель сохранённого следа",
            updated["displayName"]!.GetValue<string>());
        var preserved = Assert.IsType<JsonObject>(
            Assert.Single(updated["activeEffects"]!.AsArray()));
        Assert.Equal(expectedEffectId, preserved["effectId"]!.GetValue<string>());
    }

    [Fact]
    public async Task ShiningBlessingEntitlement_CannotBecomeEffectSourceAuthority()
    {
        await using var context = await EffectMaterializationTestContext.CreateAsync();
        const string actorId = "guardian_blessing_exclusion";
        await context.MaterializeAfterlifeActorAsync(
            "guardian",
            actorId,
            "Shining Abode",
            "art_real_source",
            CreateAfterlifeProfileDefinition("guardian"));

        var soul = (await context.ReadJsonAsync(
            "game_state/meta/soul_state.json"))!.AsObject();
        soul[ShiningBlessingEffectState.SoulStateProperty] = new JsonObject
        {
            ["cardId"] = "blessing_not_an_effect_source",
            ["activeEffectDefinitions"] = new JsonArray(
                CreateAfterlifeProfileDefinition("guardian"))
        };
        await context.WriteJsonAsync("game_state/meta/soul_state.json", soul);
        await context.CaptureValidatedPendingSnapshotAsync(
            turn: 43,
            currentRealm: "Shining Abode");

        var command = EffectMaterializationTestFixture.CreateApplyCommand("guardian");
        command["target"] = new JsonObject
        {
            ["kind"] = "guardian",
            ["targetId"] = actorId
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "fate_card",
            ["sourceId"] = "blessing_not_an_effect_source",
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject { ["value"] = -2 };
        command["eventRef"]!["authorityId"] = "turn_43";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();

        Assert.Contains(
            issues,
            issue => issue.Severity == IssueSeverity.Error &&
                     string.Equals(
                         issue.Code,
                         "effect_source_selector_unresolved",
                         StringComparison.Ordinal));
    }

    private static async Task ApplyPersistentProfileEffectAsync(
        EffectMaterializationTestContext context,
        int turn,
        string actorId,
        string artId,
        string targetKind)
    {
        await context.CaptureValidatedPendingSnapshotAsync(
            turn,
            currentRealm: "Shining Abode");
        var command = EffectMaterializationTestFixture.CreateApplyCommand(targetKind);
        command["target"] = new JsonObject
        {
            ["kind"] = targetKind,
            ["targetId"] = actorId
        };
        command["source"] = new JsonObject
        {
            ["kind"] = "spiritual_art",
            ["sourceId"] = artId,
            ["definitionKey"] = EffectMaterializationTestFixture.DefinitionKey
        };
        command["parameters"] = new JsonObject { ["value"] = -2 };
        command["eventRef"]!["authorityId"] = $"turn_{turn}";
        await context.WriteJsonAsync(
            EffectMaterializationTestContext.CommandPath,
            EffectMaterializationTestFixture.CreateCommandRoot(command));

        var issues = await context.ValidateAcceptedTurnRawMechanicsAsync();
        Assert.True(
            issues.All(issue => issue.Severity != IssueSeverity.Error),
            DescribeIssues(issues));
        var backups = await context.ReadPendingSnapshotBackupsAsync();
        Assert.NotNull(await context.NormalizeAccumulatedStateWithAcceptedMechanicsAsync(backups));
    }

    private static JsonObject CreateAfterlifeProfileDefinition(string targetKind)
    {
        var definition = EffectMaterializationTestFixture.CreateDefinition(
            "characteristic_modifier");
        definition["allowedRealms"] = new JsonArray("shining_abode");
        definition["allowedTargetKinds"] = new JsonArray(targetKind);
        return definition;
    }

    private static string DescribeIssues(IEnumerable<ValidationIssue> issues) =>
        string.Join(
            Environment.NewLine,
            issues.Select(issue =>
                $"{issue.Code} {issue.FilePath}: expected={issue.Expected}; actual={issue.Actual}"));
}
