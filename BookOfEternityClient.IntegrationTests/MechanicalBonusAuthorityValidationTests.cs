using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

[Trait("Category", "FullValidation")]
public sealed class MechanicalBonusAuthorityValidationTests : IDisposable
{
    private const string MissingAuthorityIssueCode = "inventory_mechanical_summary_missing_structured_authority";
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public MechanicalBonusAuthorityValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-mechanical-bonus-authority-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);

        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateGameStateAsync_StatBonusSummaryWithoutStructuredAuthority_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_belt_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +2"],
            "structuredBonuses": [],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_belt_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +2", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithoutStructuredAuthority_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "stiletto_stealth_1",
            "Серебряный стилет",
            """
            "bonuses": ["Скрытность +1"],
            "structuredBonuses": [],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("stiletto_stealth_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Скрытность +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithUnrelatedStructuredBonus_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "stiletto_unrelated_authority_1",
            "Серебряный стилет",
            """
            "bonuses": ["Скрытность +1"],
            "structuredBonuses": [
              {
                "description": "Сила +2",
                "bonusType": "Characteristic",
                "target": "strength",
                "valueType": "Flat",
                "value": 2,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("stiletto_unrelated_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Скрытность +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithDescriptionOnlyStructuredBonus_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_description_only_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +1"
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_description_only_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithMatchingDescriptionButContradictoryMetadata_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_contradictory_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +1",
                "bonusType": "Characteristic",
                "target": "dexterity",
                "valueType": "Flat",
                "value": 2,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_contradictory_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithNearNumericStructuredText_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_near_numeric_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +10",
                "bonusType": "Characteristic",
                "target": "Сила",
                "valueType": "Flat",
                "value": 10,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_near_numeric_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithPercentStructuredValue_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_percent_value_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +1%",
                "bonusType": "Characteristic",
                "target": "Сила",
                "valueType": "Percentage",
                "value": 1,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_percent_value_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithMissingStructuredValueType_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_missing_value_type_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +1",
                "bonusType": "Characteristic",
                "target": "Сила",
                "value": 1,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_missing_value_type_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithUnsupportedStructuredValueType_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_unsupported_value_type_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "description": "Сила +1",
                "bonusType": "Characteristic",
                "target": "Сила",
                "valueType": "NotFlat",
                "value": 1,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_unsupported_value_type_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithMissingCustomPropertyValueType_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_missing_custom_property_value_type_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [],
            "customProperties": [
              {
                "targetStateName": "strength",
                "changeValue": 1
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_missing_custom_property_value_type_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithStructuredBonusCombatStyleMissingValueType_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_structured_target_type_missing_value_type_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1%"],
            "structuredBonuses": [
              {
                "targetType": "strength",
                "value": "1%"
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_structured_target_type_missing_value_type_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1%", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithCustomPropertyCombatStyleMissingValueType_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_custom_target_type_missing_value_type_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1%"],
            "structuredBonuses": [],
            "customProperties": [
              {
                "targetType": "strength",
                "value": "1%"
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_custom_target_type_missing_value_type_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1%", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithBonusTypeOnlyTarget_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_bonus_type_only_target_authority_1",
            "Пояс силы",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [
              {
                "bonusType": "Сила",
                "valueType": "Flat",
                "value": 1
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_bonus_type_only_target_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithCombatEffectSplitTargetAndValue_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_split_combat_effect_authority_1",
            "Боевой браслет",
            """
            "bonuses": ["Сила +1"],
            "structuredBonuses": [],
            "combatEffect": [
              {
                "actionName": "Боевой импульс",
                "isActivatedEffect": true,
                "effects": [
                  {
                    "effectType": "Buff",
                    "targetType": "strength",
                    "targetTypeDisplayName": "Сила",
                    "effectDescription": "Усиливает силу на 2%",
                    "value": "2%",
                    "duration": 5
                  },
                  {
                    "effectType": "Buff",
                    "targetType": "dexterity",
                    "targetTypeDisplayName": "Ловкость",
                    "effectDescription": "Усиливает ловкость на 1%",
                    "value": "1%",
                    "duration": 5
                  }
                ]
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_split_combat_effect_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithCombatEffectDurationValue_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "strength_duration_combat_effect_authority_1",
            "Боевой браслет",
            """
            "bonuses": ["Сила +5"],
            "structuredBonuses": [],
            "combatEffect": [
              {
                "actionName": "Боевой импульс",
                "isActivatedEffect": true,
                "effects": [
                  {
                    "effectType": "Buff",
                    "targetType": "strength",
                    "targetTypeDisplayName": "Сила",
                    "effectDescription": "Усиливает силу на 2% на 5 ходов",
                    "value": "2%",
                    "duration": 5
                  }
                ]
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("strength_duration_combat_effect_authority_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Сила +5", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithEmptyCustomProperty_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "stiletto_empty_custom_property_1",
            "Серебряный стилет",
            """
            "bonuses": ["Скрытность +1"],
            "customProperties": [{}],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("stiletto_empty_custom_property_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Скрытность +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_SkillBonusSummaryWithoutStructuredAuthority_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "runic_glove_arcana_1",
            "Руническая перчатка",
            """
            "bonuses": ["Чувство магических потоков +2", "Аркановедение +1"],
            "structuredBonuses": [],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("runic_glove_arcana_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Аркановедение +1", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_ReputationBonusSummaryWithoutStructuredAuthority_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "valmont_ring_1",
            "Перстень дома Вальмонт",
            """
            "bonuses": ["Репутация среди аристократов +3"],
            "structuredBonuses": [],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("valmont_ring_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Репутация среди аристократов +3", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_HealingEffectSummaryWithoutCanonicalAuthority_ReportsIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "field_healer_tincture_1",
            "Настойка полевого лекаря",
            """
            "effects": ["Восстанавливает 15% здоровья"],
            "structuredBonuses": [],
            """,
            isConsumption: true));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.Contains(issues, issue =>
            IsMissingAuthorityIssue(issue) &&
            issue.FilePath.Contains("field_healer_tincture_1", StringComparison.OrdinalIgnoreCase) &&
            issue.Actual?.Contains("Восстанавливает 15% здоровья", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateGameStateAsync_MechanicalBonusSummaryWithStructuredAuthority_DoesNotReportIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "structured_strength_ring_1",
            "Кольцо силы",
            """
            "bonuses": ["Сила +2"],
            "structuredBonuses": [
              {
                "bonusType": "Characteristic",
                "target": "Сила",
                "valueType": "Flat",
                "value": 2,
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.DoesNotContain(issues, IsMissingAuthorityIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UtilityBonusSummaryWithBooleanStructuredAuthority_DoesNotReportIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "utility_stealth_vision_1",
            "Капюшон ночного охотника",
            """
            "bonuses": ["Скрытность в темноте"],
            "structuredBonuses": [
              {
                "description": "Скрытность в темноте",
                "bonusType": "Utility",
                "target": "Скрытность в темноте",
                "valueType": "Boolean",
                "value": true,
                "application": "Conditional",
                "condition": "в темноте"
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.DoesNotContain(issues, IsMissingAuthorityIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_StateSummaryWithStringStructuredAuthority_DoesNotReportIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "utility_state_vision_1",
            "Амулет ночного зрения",
            """
            "bonuses": ["Бонус состояния ночного зрения"],
            "structuredBonuses": [
              {
                "description": "Состояние ночного зрения активно",
                "bonusType": "Utility",
                "target": "Бонус состояния ночного зрения",
                "valueType": "String",
                "value": "active",
                "application": "Permanent",
                "condition": null
              }
            ],
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.DoesNotContain(issues, IsMissingAuthorityIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_NarrativeOnlyBonusSummaryWithExplicitClassification_DoesNotReportIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "engraved_knife_1",
            "Нож с древней руной",
            """
            "bonuses": ["На рукояти выгравирован древний символ."],
            "structuredBonuses": [],
            "mechanicalSummaryAuthority": "NarrativeOnly",
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.DoesNotContain(issues, IsMissingAuthorityIssue);
    }

    [Fact]
    public async Task ValidateGameStateAsync_UnresolvedMechanicalBonusSummaryWithPlayerFacingReason_DoesNotReportIssue()
    {
        await WriteInventoryAsync(CreateItem(
            "sealed_glove_1",
            "Запечатанная перчатка",
            """
            "bonuses": ["Аркановедение +1"],
            "structuredBonuses": [],
            "mechanicalSummaryAuthority": "Unresolved",
            "mechanicalSummaryUnresolvedReason": "Руны запечатаны, эффект станет ясен после ритуала распознавания.",
            """));

        var issues = await _validator.ValidateGameStateAsync(
            IntegrationValidationProfiles.MechanicalBonus);

        Assert.DoesNotContain(issues, IsMissingAuthorityIssue);
    }

    private async Task WriteInventoryAsync(params string[] items)
    {
        await _fs.WriteFileAtomicAsync("game_state/inventory/items.json", $$"""
        {
          "items": [
        {{string.Join(",\n", items)}}
          ]
        }
        """);
    }

    private static string CreateItem(string id, string name, string authorityFields, bool isConsumption = false)
    {
        return $$"""
            {
              "existedId": "{{id}}",
              "itemId": "{{id}}",
              "name": "{{name}}",
              "description": "{{name}}.",
              "image_prompt": "small fantasy inventory item",
              "quality": "Common",
              "price": 0,
              "count": 1,
              "weight": 0.1,
              "volume": 0.01,
              "contentsPath": null,
              "isContainer": false,
              "isConsumption": {{isConsumption.ToString().ToLowerInvariant()}},
              "requiresTwoHands": false,
              "durability": "100%",
              "type": "Инструмент",
              "group": "Снаряжение",
              {{authorityFields}}
              "equipmentSlot": null,
              "accessoryForSlot": null
            }
        """;
    }

    private static bool IsMissingAuthorityIssue(ValidationIssue issue) =>
        string.Equals(issue.Code, MissingAuthorityIssueCode, StringComparison.OrdinalIgnoreCase);

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }
}
