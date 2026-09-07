using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "RegressionIntegration")]
public sealed class SpiritualHealingArtValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public SpiritualHealingArtValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-standard-wound-art-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Theory]
    [InlineData("spiritual_resilience", 0)]
    [InlineData("spiritual_resilience", 1)]
    [InlineData("spiritual_resilience", 2)]
    [InlineData("spiritual_resilience", 3)]
    [InlineData("spiritual_resilience", 4)]
    [InlineData("spiritual_resilience", 5)]
    [InlineData("spiritual_healing", 0)]
    [InlineData("spiritual_healing", 1)]
    [InlineData("spiritual_healing", 2)]
    [InlineData("spiritual_healing", 3)]
    [InlineData("spiritual_healing", 4)]
    [InlineData("spiritual_healing", 5)]
    public async Task StandardWoundArt_CurrentEntityProfileAcceptsEveryScalarTier(string id, int tier)
    {
        var root = BuildCompleteEntityProfileRoot();
        root["profiles"]![0]!["standardArts"]![id] = tier;
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.AfterlifeEntityProfile);

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains(".profiles[0]", StringComparison.Ordinal) &&
            issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("spiritual_resilience", "null")]
    [InlineData("spiritual_resilience", "\"1\"")]
    [InlineData("spiritual_resilience", "true")]
    [InlineData("spiritual_resilience", "1.5")]
    [InlineData("spiritual_resilience", "-1")]
    [InlineData("spiritual_resilience", "6")]
    [InlineData("spiritual_resilience", "{ \"tier\": 1, \"experience\": 0 }")]
    [InlineData("spiritual_healing", "null")]
    [InlineData("spiritual_healing", "\"1\"")]
    [InlineData("spiritual_healing", "true")]
    [InlineData("spiritual_healing", "1.5")]
    [InlineData("spiritual_healing", "-1")]
    [InlineData("spiritual_healing", "6")]
    [InlineData("spiritual_healing", "{ \"tier\": 1, \"experience\": 0 }")]
    public async Task StandardWoundArt_CurrentEntityProfileRejectsMalformedTier(string id, string rawValue)
    {
        var root = BuildCompleteEntityProfileRoot();
        root["profiles"]![0]!["standardArts"]![id] = JsonNode.Parse(rawValue);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.AfterlifeEntityProfile);

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_entity_profile_invalid_standard_art_tier" &&
            issue.FilePath.EndsWith($".standardArts.{id}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public async Task StandardWoundArt_CurrentEntityProfileRequiresExplicitTier(string id)
    {
        var root = BuildCompleteEntityProfileRoot();
        root["profiles"]![0]!["standardArts"]!.AsObject().Remove(id);
        await _fs.WriteFileAtomicAsync(AfterlifeEntityProfileState.StatePath, root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(IntegrationValidationProfiles.AfterlifeEntityProfile);

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_entity_profile_missing_standard_art_tier" &&
            issue.FilePath.EndsWith($".standardArts.{id}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("spiritual_resilience", 0)]
    [InlineData("spiritual_resilience", 1)]
    [InlineData("spiritual_resilience", 2)]
    [InlineData("spiritual_resilience", 3)]
    [InlineData("spiritual_resilience", 4)]
    [InlineData("spiritual_resilience", 5)]
    [InlineData("spiritual_healing", 0)]
    [InlineData("spiritual_healing", 1)]
    [InlineData("spiritual_healing", 2)]
    [InlineData("spiritual_healing", 3)]
    [InlineData("spiritual_healing", 4)]
    [InlineData("spiritual_healing", 5)]
    public async Task StandardWoundArt_CurrentPlayerProfileAcceptsEveryScalarTier(string id, int tier)
    {
        var root = BuildSoulRoot();
        root[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![id] = tier;
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { "game_state/meta/soul_state.json" }));

        Assert.DoesNotContain(issues, issue =>
            issue.FilePath.Contains(".afterlifeCombatProfile", StringComparison.Ordinal) &&
            issue.Severity == IssueSeverity.Error);
    }

    [Theory]
    [InlineData("spiritual_resilience", "null")]
    [InlineData("spiritual_resilience", "\"1\"")]
    [InlineData("spiritual_resilience", "true")]
    [InlineData("spiritual_resilience", "1.5")]
    [InlineData("spiritual_resilience", "-1")]
    [InlineData("spiritual_resilience", "6")]
    [InlineData("spiritual_resilience", "{ \"tier\": 1, \"experience\": 0 }")]
    [InlineData("spiritual_healing", "null")]
    [InlineData("spiritual_healing", "\"1\"")]
    [InlineData("spiritual_healing", "true")]
    [InlineData("spiritual_healing", "1.5")]
    [InlineData("spiritual_healing", "-1")]
    [InlineData("spiritual_healing", "6")]
    [InlineData("spiritual_healing", "{ \"tier\": 1, \"experience\": 0 }")]
    public async Task StandardWoundArt_CurrentPlayerProfileRejectsMalformedTier(string id, string rawValue)
    {
        var root = BuildSoulRoot();
        root[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]![id] = JsonNode.Parse(rawValue);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { "game_state/meta/soul_state.json" }));

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_combat_profile_invalid_art_tier" &&
            issue.FilePath.EndsWith($".artTiers.{id}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("spiritual_resilience")]
    [InlineData("spiritual_healing")]
    public async Task StandardWoundArt_CurrentPlayerProfileRequiresExplicitTier(string id)
    {
        var root = BuildSoulRoot();
        root[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!["artTiers"]!.AsObject().Remove(id);
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { "game_state/meta/soul_state.json" }));

        Assert.Contains(issues, issue =>
            issue.Code == "afterlife_combat_profile_missing_art_tier" &&
            issue.FilePath.EndsWith($".artTiers.{id}", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task StandardWoundArt_CurrentPlayerProfileRequiresArtTiersObject(bool malformed)
    {
        var root = BuildSoulRoot();
        var profile = root[AfterlifeSpiritualConflictState.SoulStateProfileProperty]!.AsObject();
        if (malformed)
            profile["artTiers"] = new JsonArray();
        else
            profile.Remove("artTiers");
        await _fs.WriteFileAtomicAsync("game_state/meta/soul_state.json", root.ToJsonString());

        var issues = await _validator.ValidateGameStateAsync(new GameStateValidationSelection(
            GameStateValidationPhase.MetaMiscStateFiles,
            new[] { "game_state/meta/soul_state.json" }));

        Assert.Contains(issues, issue =>
            issue.FilePath.EndsWith(".afterlifeCombatProfile.artTiers", StringComparison.Ordinal) &&
            issue.Code == (malformed ? "expected_object" : "afterlife_combat_profile_missing_art_tiers"));
    }

    private static JsonObject BuildCompleteEntityProfileRoot()
    {
        var root = JsonNode.Parse(AfterlifeEntityProfileValidationTests.BuildValidProfileJson())!.AsObject();
        var standardArts = root["profiles"]![0]!["standardArts"]!.AsObject();
        standardArts["spiritual_resilience"] = 0;
        standardArts["spiritual_healing"] = 0;
        return root;
    }

    private static JsonObject BuildSoulRoot() =>
        new()
        {
            ["soulName"] = "Асуран",
            ["currentRealm"] = "Chaos Sea",
            [AfterlifeSpiritualConflictState.SoulStateProfileProperty] = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["enlightenmentRank"] = 1,
                ["radianceRank"] = 0,
                ["retainedRadianceRank"] = 0,
                ["spiritFocusTier"] = 0,
                ["artTiers"] = new JsonObject
                {
                    ["spiritual_resilience"] = 0,
                    ["spiritual_healing"] = 0
                },
                ["capstones"] = new JsonObject(),
                ["lastRecoveryTurn"] = 0
            }
        };

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Ignore temp cleanup failures.
        }
    }
}
