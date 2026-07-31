using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class ChaosSeaGuardianPoliticsStateTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaGuardianPoliticsStateTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-guardian-politics-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task NormalizeAccumulatedStateAsync_AppliesGuardianPoliticsUpdateSurfaces()
    {
        await SeedGuardianStateAsync();
        await _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "guardianPoliticalRelationUpdates": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Обе Обители хотят защитить архивы от охотников памяти.",
              "lastChangedTurn": 44,
              "effects": [ "training_discount" ]
            }
          ],
          "guardianPoliticalProjectUpdates": [
            {
              "projectId": "project_archive_pact",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "projectType": "alliance",
              "status": "active",
              "summary": "Азалия укрепляет пакт архивных Обителей.",
              "currentProgress": 2,
              "requiredProgress": 5,
              "lastUpdatedTurn": 44,
              "visibility": "known"
            }
          ],
          "guardianPoliticalInfluenceUpdates": [
            {
              "zoneId": "zone_silk_archive",
              "guardianId": "guardian_azalia",
              "scopeType": "abode",
              "scopeId": "abode_azalia",
              "displayName": "Шёлковый Архив",
              "influenceValue": 73,
              "controlLevel": 58,
              "visibility": "known",
              "updatedAtTurn": 44
            }
          ],
          "guardianPoliticalChronicleUpdates": [
            {
              "entryId": "chronicle_archive_pact_44",
              "turnNumber": 44,
              "eventType": "alliance",
              "summary": "Азалия и Серет признали общий долг перед архивами.",
              "visibility": "known",
              "guardianIds": [ "guardian_azalia", "guardian_seret" ],
              "consequences": [ "Обе Обители получают повод защищать архивные маршруты." ]
            }
          ]
        }
        """);

        var normalizer = new CanonicalStateNormalizer(_fs, NullLogger<CanonicalStateNormalizer>.Instance);
        await normalizer.NormalizeAccumulatedStateAsync();

        using var doc = JsonDocument.Parse((await _fs.ReadFileAsync(ChaosSeaGuardianPoliticsState.StatePath))!);
        var root = doc.RootElement;
        Assert.Equal("azalia_seret_alliance", root.GetProperty("relations")[0].GetProperty("relationId").GetString());
        Assert.Equal("project_archive_pact", root.GetProperty("projects")[0].GetProperty("projectId").GetString());
        Assert.Equal("zone_silk_archive", root.GetProperty("influenceZones")[0].GetProperty("zoneId").GetString());
        Assert.Equal("chronicle_archive_pact_44", root.GetProperty("chronicle")[0].GetProperty("entryId").GetString());
        Assert.False(root.TryGetProperty(ChaosSeaGuardianPoliticsState.RelationUpdatesProperty, out _));
        Assert.False(root.TryGetProperty(ChaosSeaGuardianPoliticsState.ProjectUpdatesProperty, out _));
        Assert.False(root.TryGetProperty(ChaosSeaGuardianPoliticsState.InfluenceUpdatesProperty, out _));
        Assert.False(root.TryGetProperty(ChaosSeaGuardianPoliticsState.ChronicleUpdatesProperty, out _));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidGuardianPolitics_RaisesContractIssues()
    {
        await SeedGuardianStateAsync();
        await _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "bad_relation",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "missing_guardian",
              "relationType": "alliance",
              "attitudeScore": 150,
              "visibility": "hidden",
              "isPlayerVisible": true,
              "reason": "Сломанная связь.",
              "lastChangedTurn": 44,
              "effects": []
            }
          ],
          "projects": [
            {
              "projectId": "bad_project",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "missing_guardian",
              "projectType": "rivalry",
              "status": "active",
              "summary": "Сломанный проект.",
              "currentProgress": 5,
              "requiredProgress": 3,
              "lastUpdatedTurn": 44,
              "visibility": "known"
            }
          ],
          "influenceZones": [
            {
              "zoneId": "bad_zone",
              "guardianId": "guardian_azalia",
              "scopeType": "abode",
              "scopeId": "abode_azalia",
              "displayName": "Сломанная зона",
              "influenceValue": 120,
              "controlLevel": -1,
              "visibility": "known",
              "updatedAtTurn": 44
            }
          ],
          "chronicle": []
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_unknown_guardian", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_attitude_score_out_of_range", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_hidden_relation_player_visible", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_project_progress_invalid", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_influence_value_out_of_range", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(issues, issue => string.Equals(issue.Code, "chaos_guardian_politics_control_level_out_of_range", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidGuardianPolitics_PassesContractValidation()
    {
        await SeedGuardianStateAsync();
        await WriteValidPoliticsStateAsync();

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("chaos_guardian_politics_", StringComparison.OrdinalIgnoreCase) == true);
    }

    private async Task SeedGuardianStateAsync()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", """
        {
          "guardians": [
            {
              "guardianId": "guardian_azalia",
              "canonicalName": "Азалия",
              "domain": "Memory",
              "relationshipData": { "currentReputation": 15 },
              "abode": { "abodeId": "abode_azalia", "abodeName": "Шёлковый Архив" }
            },
            {
              "guardianId": "guardian_seret",
              "canonicalName": "Серет",
              "domain": "Oaths",
              "relationshipData": { "currentReputation": 4 },
              "abode": { "abodeId": "abode_seret", "abodeName": "Зал Нерушимых Клятв" }
            }
          ],
          "activeGuardian": {
            "guardianId": "guardian_azalia",
            "canonicalName": "Азалия"
          },
          "chaosSeaNavigation": {
            "currentAbodeId": "abode_azalia",
            "knownAbodes": [
              { "abodeId": "abode_azalia", "name": "Шёлковый Архив", "guardianId": "guardian_azalia" },
              { "abodeId": "abode_seret", "name": "Зал Нерушимых Клятв", "guardianId": "guardian_seret" }
            ]
          }
        }
        """);
    }

    private Task WriteValidPoliticsStateAsync() =>
        _fs.WriteFileAtomicAsync(ChaosSeaGuardianPoliticsState.StatePath, """
        {
          "schemaVersion": 1,
          "relations": [
            {
              "relationId": "azalia_seret_alliance",
              "sourceGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "relationType": "alliance",
              "attitudeScore": 62,
              "visibility": "known",
              "reason": "Обе Обители хотят защитить архивы от охотников памяти.",
              "lastChangedTurn": 44,
              "effects": [ "training_discount" ]
            }
          ],
          "projects": [
            {
              "projectId": "project_archive_pact",
              "ownerGuardianId": "guardian_azalia",
              "targetGuardianId": "guardian_seret",
              "projectType": "alliance",
              "status": "active",
              "summary": "Азалия укрепляет пакт архивных Обителей.",
              "currentProgress": 2,
              "requiredProgress": 5,
              "lastUpdatedTurn": 44,
              "visibility": "known"
            }
          ],
          "influenceZones": [
            {
              "zoneId": "zone_silk_archive",
              "guardianId": "guardian_azalia",
              "scopeType": "abode",
              "scopeId": "abode_azalia",
              "displayName": "Шёлковый Архив",
              "influenceValue": 73,
              "controlLevel": 58,
              "visibility": "known",
              "updatedAtTurn": 44
            }
          ],
          "chronicle": [
            {
              "entryId": "chronicle_archive_pact_44",
              "turnNumber": 44,
              "eventType": "alliance",
              "summary": "Азалия и Серет признали общий долг перед архивами.",
              "visibility": "known",
              "guardianIds": [ "guardian_azalia", "guardian_seret" ],
              "consequences": [ "Обе Обители получают повод защищать архивные маршруты." ]
            }
          ],
          "playerRole": {
            "role": "mediator",
            "summary": "Игрок выступил посредником между двумя Обителями.",
            "lastUpdatedTurn": 44
          },
          "sarefLinks": [],
          "openConflicts": []
        }
        """);

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
