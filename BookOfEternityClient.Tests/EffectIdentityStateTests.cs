using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class EffectIdentityStateTests
{
    [Fact]
    public void Factory_AllocatesOpaquePrefixedDistinctIdentities()
    {
        var factory = new EffectIdentityFactory();

        var firstEffect = factory.CreateEffectId();
        var secondEffect = factory.CreateEffectId();
        var transition = factory.CreateTransitionId();
        var resolution = factory.CreateResolutionId();

        Assert.StartsWith("effect_", firstEffect, StringComparison.Ordinal);
        Assert.StartsWith("effect_", secondEffect, StringComparison.Ordinal);
        Assert.StartsWith("effect_transition_", transition, StringComparison.Ordinal);
        Assert.StartsWith("effect_resolution_", resolution, StringComparison.Ordinal);
        Assert.Equal(4, new[] { firstEffect, secondEffect, transition, resolution }.Distinct(StringComparer.Ordinal).Count());
        Assert.DoesNotContain(EffectMaterializationTestFixture.DefinitionKey, firstEffect, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Parse_CompleteActiveIndex_ReturnsValidatedState()
    {
        using var document = Parse(EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect()));

        var result = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath);

        Assert.Empty(result.Issues);
        var state = Assert.IsType<EffectIdentityState>(result.State);
        Assert.True(state.TryGetEntry(EffectMaterializationTestFixture.EffectId, out var entry));
        Assert.Equal("active", entry.State);
        Assert.Equal("player", entry.Owner.Kind);
        Assert.Equal("player_current", entry.Owner.OwnerId);
        Assert.Single(entry.Transitions);
        Assert.True(JsonNode.DeepEquals(
            EffectMaterializationTestFixture.CreateIdentityIndex(EffectMaterializationTestFixture.CreateCanonicalEffect()),
            state.ToJson()));
    }

    [Theory]
    [InlineData("schemaVersion")]
    [InlineData("entries")]
    public void Parse_MissingRequiredRootField_IsRejected(string field)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        index.Remove(field);
        using var document = Parse(index);

        var result = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath);

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue =>
            issue.Code == "effect_identity_missing_field" &&
            issue.FilePath == $"{EffectIdentityState.StatePath}.{field}");
    }

    [Fact]
    public void Parse_UnknownAndDuplicateProperties_AreRejectedBeforeNodeConversion()
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        index["repairHint"] = "forged";
        var json = index.ToJsonString().Replace(
            "\"state\":\"active\"",
            "\"state\":\"active\",\"state\":\"active\"",
            StringComparison.Ordinal);
        using var document = JsonDocument.Parse(json);

        var result = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath);

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_identity_unknown_field");
        Assert.Contains(result.Issues, issue => issue.Code == "effect_identity_duplicate_property");
    }

    [Theory]
    [InlineData("owner", "carrierPath")]
    [InlineData("stackCoordinate", "stackKey")]
    [InlineData("transitions[0]", "eventRef")]
    public void Parse_IncompleteClosedNestedEvidence_IsRejected(string section, string field)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        var entry = index["entries"]![0]!.AsObject();
        ResolveObject(entry, section).Remove(field);
        using var document = Parse(index);

        var result = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath);

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_identity_missing_field");
    }

    [Fact]
    public void Parse_TerminalStateRequiresMatchingImmutableTerminalTransition()
    {
        var index = CreateTerminalIndex("removed", "remove");
        using var validDocument = Parse(index);
        Assert.Empty(EffectIdentityState.Parse(validDocument.RootElement, EffectIdentityState.StatePath).Issues);

        index["entries"]![0]!["transitions"]![0]!["kind"] = "create";
        using var invalidDocument = Parse(index);
        var result = EffectIdentityState.Parse(invalidDocument.RootElement, EffectIdentityState.StatePath);

        Assert.Null(result.State);
        Assert.Contains(result.Issues, issue => issue.Code == "effect_identity_terminal_evidence_mismatch");
    }

    [Fact]
    public void Parse_ActiveEntryCannotEndWithTerminalTransition()
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        index["entries"]![0]!["transitions"]![0]!["kind"] = "expire";
        using var document = Parse(index);

        Assert.Contains(
            EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath).Issues,
            issue => issue.Code == "effect_identity_active_terminal_conflict");
    }

    [Theory]
    [InlineData("effect_test_bleeding")]
    [InlineData("EFFECT_TEST_BLEEDING")]
    [InlineData("effect_test_bleedіng")]
    public void Parse_DuplicateOrConfusableEffectIdentityAcrossEntries_IsRejected(string secondId)
    {
        var first = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var second = CreateDistinctEffect(secondId, "effect_transition_second", "turn_43:second");
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(first, second);
        using var document = Parse(index);

        Assert.Contains(
            EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath).Issues,
            issue => issue.Code is "effect_identity_duplicate_id" or "effect_identity_confusable_id");
    }

    [Theory]
    [InlineData("effect_transition_test_apply")]
    [InlineData("EFFECT_TRANSITION_TEST_APPLY")]
    [InlineData("effect_transіtion_test_apply")]
    public void Parse_DuplicateOrConfusableTransitionIdentityAcrossEntries_IsRejected(string secondTransitionId)
    {
        var first = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var second = CreateDistinctEffect("effect_test_second", secondTransitionId, "turn_43:second");
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(first, second);
        using var document = Parse(index);

        Assert.Contains(
            EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath).Issues,
            issue => issue.Code is "effect_identity_duplicate_transition" or "effect_identity_confusable_transition");
    }

    [Fact]
    public void Parse_NonPositiveTurnAndDuplicateReplayEvent_AreRejected()
    {
        var first = EffectMaterializationTestFixture.CreateCanonicalEffect();
        var second = CreateDistinctEffect("effect_test_second", "effect_transition_second", "turn_42:wound_opened");
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(first, second);
        index["entries"]![1]!["transitions"]![0]!["turn"] = 0;
        using var document = Parse(index);

        var issues = EffectIdentityState.Parse(document.RootElement, EffectIdentityState.StatePath).Issues;

        Assert.Contains(issues, issue => issue.Code == "effect_identity_invalid_transition");
        Assert.Contains(issues, issue => issue.Code == "effect_identity_replay_conflict");
    }

    [Fact]
    public void ValidateClientOwnedContinuity_RejectsGmAuthoredIndexMutation()
    {
        var before = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        var current = before.DeepClone().AsObject();
        current["entries"]![0]!["state"] = "suspended";
        using var beforeDocument = Parse(before);
        using var currentDocument = Parse(current);

        var issues = EffectIdentityState.ValidateClientOwnedContinuity(
            beforeDocument.RootElement,
            currentDocument.RootElement,
            EffectIdentityState.StatePath);

        Assert.Contains(issues, issue => issue.Code == "effect_identity_direct_mutation");
    }

    [Fact]
    public void ValidateTerminalContinuity_RejectsDeletionOrMutationOfTerminalHistory()
    {
        using var beforeDocument = Parse(CreateTerminalIndex("removed", "remove"));
        var before = Assert.IsType<EffectIdentityState>(
            EffectIdentityState.Parse(beforeDocument.RootElement, EffectIdentityState.StatePath).State);

        using var missingDocument = Parse(new JsonObject
        {
            ["schemaVersion"] = 1,
            ["entries"] = new JsonArray()
        });
        var missing = Assert.IsType<EffectIdentityState>(
            EffectIdentityState.Parse(missingDocument.RootElement, EffectIdentityState.StatePath).State);
        Assert.Contains(
            before.ValidateTerminalContinuity(missing, EffectIdentityState.StatePath),
            issue => issue.Code == "effect_identity_terminal_history_mutated");

        var changedIndex = CreateTerminalIndex("removed", "remove");
        changedIndex["entries"]![0]!["transitions"]![0]!["eventRef"] = "turn_99:forged";
        using var changedDocument = Parse(changedIndex);
        var changed = Assert.IsType<EffectIdentityState>(
            EffectIdentityState.Parse(changedDocument.RootElement, EffectIdentityState.StatePath).State);
        Assert.Contains(
            before.ValidateTerminalContinuity(changed, EffectIdentityState.StatePath),
            issue => issue.Code == "effect_identity_terminal_history_mutated");
    }

    private static JsonObject CreateDistinctEffect(string effectId, string transitionId, string eventRef)
    {
        var effect = EffectMaterializationTestFixture.CreateCanonicalEffect();
        effect["effectId"] = effectId;
        effect["chronology"]!["lastTransitionId"] = transitionId;
        effect["chronology"]!["createdEventRef"] = eventRef;
        return effect;
    }

    private static JsonObject CreateTerminalIndex(string state, string transitionKind)
    {
        var index = EffectMaterializationTestFixture.CreateIdentityIndex(
            EffectMaterializationTestFixture.CreateCanonicalEffect());
        var entry = index["entries"]![0]!.AsObject();
        var transition = entry["transitions"]![0]!.AsObject();
        entry["state"] = state;
        transition["kind"] = transitionKind;
        transition["sourceEffectIds"] = new JsonArray(EffectMaterializationTestFixture.EffectId);
        transition["resultEffectIds"] = new JsonArray();
        return index;
    }

    private static JsonObject ResolveObject(JsonObject entry, string section) =>
        section switch
        {
            "owner" => entry["owner"]!.AsObject(),
            "stackCoordinate" => entry["stackCoordinate"]!.AsObject(),
            "transitions[0]" => entry["transitions"]![0]!.AsObject(),
            _ => throw new ArgumentOutOfRangeException(nameof(section), section, null)
        };

    private static JsonDocument Parse(JsonNode node) => JsonDocument.Parse(node.ToJsonString());
}
