using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class MathAssistantContractValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MathAssistantContractValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-math-assistant-contract-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public void ValidateResponse_ValidMathRequestAndAudit_DoesNotReportMathIssues()
    {
        using var doc = JsonDocument.Parse(BuildValidResponseJson());

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("math_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ValidateResponse_MalformedAudit_ReportsShapeIssue()
    {
        using var doc = JsonDocument.Parse("""
        {
          "response": "Расчёт не может быть принят.",
          "mathAudit": [
            {
              "requestId": "calc_reward_1",
              "expression": "baseReward + bonus",
              "variables": { "baseReward": 10, "bonus": 5 },
              "result": 15,
              "rounding": { "mode": "none" },
              "formulaVersion": "math_assistant_v1",
              "applicationState": "applied_to_state"
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_audit_missing_audit_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_MismatchedAuditResult_ReportsRepairBlockingMismatch()
    {
        using var doc = JsonDocument.Parse(BuildValidResponseJson()
            .Replace("\"result\": 38", "\"result\": 39", StringComparison.Ordinal));

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_audit_result_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_AuditWithMissingVariable_ReportsEvaluationFailure()
    {
        using var doc = JsonDocument.Parse(BuildValidResponseJson()
            .Replace("\"discountPercent\": 15", "\"otherPercent\": 15", StringComparison.Ordinal));

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_audit_evaluation_failed", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains(MathAssistantErrorCodes.MissingVariable, StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public void ValidateResponse_DuplicateRequestIds_ReportsDuplicate()
    {
        using var doc = JsonDocument.Parse("""
        {
          "response": "Нужно посчитать две формулы.",
          "mathRequests": [
            {
              "requestId": "calc_duplicate",
              "purpose": "first",
              "expression": "base + 1",
              "variables": { "base": 1 },
              "rounding": { "mode": "none" }
            },
            {
              "requestId": "calc_duplicate",
              "purpose": "second",
              "expression": "base + 2",
              "variables": { "base": 1 },
              "rounding": { "mode": "none" }
            }
          ]
        }
        """);

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_request_duplicate_id", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_MortalCombatMathAuditMatchingHealthDelta_DoesNotReportDeltaMismatch()
    {
        using var doc = JsonDocument.Parse(BuildMortalCombatMathAuditResponseJson(currentHealthChange: -13));

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "math_audit_applied_delta_mismatch", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(issue.Code, "math_audit_missing_referenced_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_MortalCombatMathAuditMismatchedHealthDelta_ReportsMismatch()
    {
        using var doc = JsonDocument.Parse(BuildMortalCombatMathAuditResponseJson(currentHealthChange: -12));

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_audit_applied_delta_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_MortalCombatMathAuditReferencesMissingDelta_ReportsMissingDelta()
    {
        using var doc = JsonDocument.Parse(BuildMortalCombatMathAuditResponseJson(includeHealthDelta: false));

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_audit_missing_referenced_delta", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void ValidateResponse_SimpleCombatWithoutMathAudit_RemainsAcceptedByMathAssistant()
    {
        using var doc = JsonDocument.Parse("""
        {
          "response": "Обычный удар без сложной арифметики.",
          "combat_log_markdown": "Враг задевает героя. Здоровье: -5.",
          "currentHealthChange": -5
        }
        """);

        var issues = _validator.ValidateResponse(doc.RootElement);

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("math_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MathAuditStateFile_IsValidated()
    {
        await _fs.WriteFileAtomicAsync("game_state/meta/math_audit.json", """
        {
          "mathRequests": [
            {
              "requestId": "calc_state_bad",
              "purpose": "invalid stored request",
              "expression": "base + missing",
              "variables": { "base": 1 },
              "rounding": { "mode": "none" }
            }
          ]
        }
        """);

        var issues = await _validator.ValidateGameStateAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "math_request_evaluation_failed", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void MathAssistantContractDocumentation_IsGmFacingAndReferencesStateFile()
    {
        var apiSpec = ReadRepoFile("CLI_API_Specification.md");
        var daemonSpec = ReadRepoFile("CLI_Agent_Daemon_Specification.md");

        foreach (var text in new[] { apiSpec, daemonSpec })
        {
            Assert.Contains("mathRequests", text, StringComparison.Ordinal);
            Assert.Contains("mathAudit", text, StringComparison.Ordinal);
            Assert.Contains("game_state/meta/math_audit.json", text, StringComparison.Ordinal);
            Assert.Contains("math_assistant_v1", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void MathAssistantContractFields_AreMappedToAuditStateFile()
    {
        Assert.Equal(MathAssistantContractState.StatePath, FileMapping.FieldToFile["mathRequests"]);
        Assert.Equal(MathAssistantContractState.StatePath, FileMapping.FieldToFile["mathAudit"]);
    }

    private static string BuildValidResponseJson() => """
    {
      "response": "Скидка посчитана через Математика.",
      "mathRequests": [
        {
          "requestId": "calc_discount_1",
          "purpose": "treasury exchange discount",
          "expression": "baseCost * discountPercent / 100",
          "variables": { "baseCost": 250, "discountPercent": 15 },
          "rounding": { "mode": "away_from_zero", "decimalPlaces": 0 },
          "expectedResult": 38
        }
      ],
      "mathAudit": [
        {
          "auditId": "calc_discount_1",
          "requestId": "calc_discount_1",
          "purpose": "treasury exchange discount",
          "expression": "baseCost * discountPercent / 100",
          "normalizedExpression": "baseCost*discountPercent/100",
          "variables": { "baseCost": 250, "discountPercent": 15 },
          "rawResult": 37.5,
          "result": 38,
          "rounding": { "mode": "away_from_zero", "decimalPlaces": 0 },
          "formulaVersion": "math_assistant_v1",
          "applicationState": "applied_to_state",
          "referencedBy": [ "treasuryReceipt:exchange_1" ],
          "warnings": []
        }
      ]
    }
    """;

    private static string BuildMortalCombatMathAuditResponseJson(int currentHealthChange = -13, bool includeHealthDelta = true)
    {
        var healthDeltaLine = includeHealthDelta
            ? $"""
              "currentHealthChange": {currentHealthChange},
            """
            : "";

        return $$"""
        {
          "response": "Удар пробил защиту после расчёта урона.",
          "combat_log_markdown": "Расчёт: 12 базового урона + 4 сила - 3 броня = 13 урона; currentHealthChange = -13.",
        {{healthDeltaLine}}  "mathAudit": [
            {
              "auditId": "calc_mortal_damage_1",
              "requestId": "calc_mortal_damage_1",
              "purpose": "mortal combat applied health delta",
              "expression": "armorReduction - (baseDamage + strengthBonus)",
              "normalizedExpression": "armorReduction-(baseDamage+strengthBonus)",
              "variables": { "baseDamage": 12, "strengthBonus": 4, "armorReduction": 3 },
              "rawResult": -13,
              "result": -13,
              "rounding": { "mode": "none" },
              "formulaVersion": "math_assistant_v1",
              "applicationState": "applied_to_state",
              "referencedBy": [ "currentHealthChange", "combat_log_markdown:mortal_damage_1" ],
              "warnings": []
            }
          ]
        }
        """;
    }

    private static string ReadRepoFile(params string[] segments)
    {
        var path = Path.Combine(TestRepoPaths.RepoRoot, Path.Combine(segments));
        return File.ReadAllText(path);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_rootPath))
                Directory.Delete(_rootPath, recursive: true);
        }
        catch
        {
            // Best-effort cleanup for temporary test folders.
        }
    }
}
