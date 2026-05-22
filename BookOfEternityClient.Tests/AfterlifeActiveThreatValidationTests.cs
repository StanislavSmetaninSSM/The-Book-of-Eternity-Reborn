using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class AfterlifeActiveThreatValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public AfterlifeActiveThreatValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-afterlife-threat-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ValidAfterlifeThreat_PassesThreatValidation()
    {
        await WriteThreatStateAsync(BuildValidThreatJson());

        var issues = await _validator.ValidateGameStateAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("afterlife_threat_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ThreatUpdateNullsCurrentActivity_ReportsCommandIssue()
    {
        await WriteThreatStateAsync("""
        {
          "schemaVersion": 1,
          "afterlifeThreatsToUpdate": [
            {
              "threatId": "chaos_soul_hunter_pack",
              "currentActivity": null
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_threat_update_null_current_activity_forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_ThreatUpdateCompletesCurrentActivity_ReportsCommandIssue()
    {
        await WriteThreatStateAsync("""
        {
          "schemaVersion": 1,
          "afterlifeThreatsToUpdate": [
            {
              "threatId": "chaos_soul_hunter_pack",
              "currentActivity": {
                "activeState": "Completed"
              }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_threat_update_terminal_activity_state_forbidden", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateGameStateAsync_InvalidAfterlifeThreatRealm_ReportsContractIssue()
    {
        await WriteThreatStateAsync(BuildValidThreatJson()
            .Replace("\"realm\": \"Chaos Sea\"", "\"realm\": \"Mortal World\"", StringComparison.Ordinal));

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_threat_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    private Task WriteThreatStateAsync(string json) =>
        _fs.WriteFileAtomicAsync(AfterlifeActiveThreatState.StatePath, json);

    private static string BuildValidThreatJson() =>
        """
        {
          "schemaVersion": 1,
          "threats": [
            {
              "threatId": "chaos_soul_hunter_pack",
              "realm": "Chaos Sea",
              "scopeId": "black_tide_shore",
              "displayName": "Стая охотников за душами",
              "threatArchetype": {
                "motivation": "Consumption",
                "method": "Overt"
              },
              "intensity": 4,
              "currentActivity": {
                "activityId": "hunt_001",
                "activityName": "Идут по следу души",
                "description": "Охотники ищут след игрока у Черного Прилива.",
                "activeState": "Active"
              },
              "impactProfile": {
                "primaryTargetType": "Location",
                "primaryTargetId": "black_tide_shore",
                "primaryTargetName": "Берег Черного Прилива",
                "primaryImpact": "Covert",
                "baseImpactValue": 4
              },
              "visibleToPlayer": true,
              "linkedFactionId": null,
              "linkedGuardianId": null,
              "sarefLink": null,
              "ledger": [
                {
                  "turnNumber": 9,
                  "summary": "Охотники впервые заметили след."
                }
              ]
            }
          ]
        }
        """;

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
