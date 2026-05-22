using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ChaosSeaGachaValidationTests : IDisposable
{
    private readonly string _rootPath;
    private readonly FileSystemManager _fs;
    private readonly ValidationService _validator;

    public ChaosSeaGachaValidationTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-chaos-gacha-validation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
        _fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        _fs.EnsureDirectoryStructure();
        _validator = new ValidationService(_fs, NullLogger<ValidationService>.Instance);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithoutNewRelic_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest());
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_missing_new_relic_materialization", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaOutsideExactChaosSea_Fails()
    {
        var preTurnSoul = CreateSoulRoot(currentRealm: "Shining Abode", inkFeathers: 10);
        var currentSoul = CreateSoulRoot(currentRealm: "Shining Abode", inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest());
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_invalid_realm", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithoutFeatherDeduction_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 10);
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest());
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_feather_balance_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithClientSpentSnapshot_Passes()
    {
        var rollbackSoul = CreateSoulRoot(inkFeathers: 10);
        var preTurnSoul = CreateSoulRoot(inkFeathers: 5);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Rare");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest(baseRarity: "Rare"));
        await WritePendingTurnSnapshotAsync(preTurnSoul, rollbackSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code?.StartsWith("direct_chaos_gacha_", StringComparison.OrdinalIgnoreCase) == true);
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AfterlifeClientPrepaidInkActionWithExtraSpend_Fails()
    {
        var rollbackSoul = CreateSoulRoot(inkFeathers: 10, enlightenmentExperience: 0);
        var preTurnSoul = CreateSoulRoot(inkFeathers: 5, enlightenmentExperience: 0);
        var currentSoul = CreateSoulRoot(inkFeathers: 0, enlightenmentExperience: 20);
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateCultivateEnlightenmentTurnRequest());
        await WriteCultivateEnlightenmentReceiptAsync();
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            rollbackSoul,
            "[INK_FEATHER_ACTION: CULTIVATE_ENLIGHTENMENT] Игрок вкладывает 5 Чернильных Перьев в просветление.");

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "afterlife_ink_feather_client_prepaid_double_spend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_CultivateEnlightenmentUsesAcceleratedFormula_Passes()
    {
        var rollbackSoul = CreateSoulRoot(inkFeathers: 10, enlightenmentExperience: 0);
        var preTurnSoul = CreateSoulRoot(inkFeathers: 5, enlightenmentExperience: 0);
        var currentSoul = CreateSoulRoot(inkFeathers: 5, enlightenmentExperience: 20);
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateCultivateEnlightenmentTurnRequest());
        await WriteCultivateEnlightenmentReceiptAsync();
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            rollbackSoul,
            "[INK_FEATHER_ACTION: CULTIVATE_ENLIGHTENMENT] Игрок вкладывает 5 Чернильных Перьев в просветление.");

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_enlightenment_gain_mismatch", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "ink_feather_enlightenment_growth_too_small", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "afterlife_ink_feather_client_prepaid_double_spend", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaBelowBaseRarity_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Common");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest(baseRarity: "Rare"));
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_result_rarity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithoutLiveTurnRequest_UsesSnapshotContext()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Common");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WritePendingTurnSnapshotAsync(preTurnSoul, gachaBaseRarity: "Rare");
        await WriteNodeAsync("ready/turn_complete.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7
        });

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_result_rarity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_MalformedLiveTurnRequest_FailsClosed()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Common");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await _fs.WriteFileAtomicAsync("input/turn_request.json", "{ malformed turn request");
        await WritePendingTurnSnapshotAsync(preTurnSoul, gachaBaseRarity: "Rare");

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "accepted_turn_special_action_request_parse_failed", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_result_rarity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaAboveBaseRarity_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Legendary");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest(baseRarity: "Rare"));
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_result_rarity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaWithUnknownRarity_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        AddStoredSoulRelic(currentSoul, "relic_new", "Mythic");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest(baseRarity: "Rare"));
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_result_rarity_mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaMutatesExistingRelic_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        currentSoul["soulRelics"]!["stored"]!.AsArray()[0]!.AsObject()["name"] = "Подменённая реликвия";
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest());
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_DirectChaosGachaMutatesUnrelatedSoulField_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10, enlightenmentExperience: 0);
        var currentSoul = CreateSoulRoot(inkFeathers: 5, enlightenmentExperience: 10);
        AddStoredSoulRelic(currentSoul, "relic_new");
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", CreateDirectGachaTurnRequest());
        await WritePendingTurnSnapshotAsync(preTurnSoul);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "direct_chaos_gacha_unexpected_soul_state_diff", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentRelicGrantWithoutCompanionEcho_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        await WriteNodeAsync("game_state/meta/soul_state.json", CreateSoulRoot());
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", CreateResidentRoot("resident_liora"));
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(
            "[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia)."));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: "[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).",
            preTurnResidentRoot: preTurnResidents);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_relic_grant_missing_companion_echo_relic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithoutSoulQuest_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var preTurnQuests = CreateSoulQuestRoot();
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", CreateResidentRoot("resident_liora", includeInteractionLog: true));
        await WriteNodeAsync("game_state/quests/soul_quests.json", new JsonObject { ["quests"] = new JsonArray() });
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(
            "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia)."));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).",
            preTurnResidentRoot: preTurnResidents,
            preTurnSoulQuestsRoot: preTurnQuests);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_quest_request_missing_linked_soul_quest", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentRelicGrantWithOnlyPreExistingReward_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        AddCompanionEchoRelic(preTurnSoul, "relic_liora_echo");
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        var preTurnResidents = CreateResidentRoot("resident_liora", includeInteractionLog: true);
        SetResidentRewardGranted(preTurnResidents, "relic_liora_echo");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        var playerAction = "[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(preTurnSoul, playerAction: playerAction, preTurnResidentRoot: preTurnResidents);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_relic_grant_no_new_companion_echo_relic", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentRelicGrantWithNewReward_Passes()
    {
        var preTurnSoul = CreateSoulRoot();
        var currentSoul = preTurnSoul.DeepClone().AsObject();
        AddCompanionEchoRelic(currentSoul, "relic_liora_echo");
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        SetResidentRewardGranted(currentResidents, "relic_liora_echo");
        AddResidentInteractionLog(currentResidents, "resident_liora", "log_liora_relic");
        var playerAction = "[ABODE_RESIDENT_RELIC_GRANT] Игрок принимает реликвию связи от afterlife resident 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(preTurnSoul, playerAction: playerAction, preTurnResidentRoot: preTurnResidents);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code!.StartsWith("abode_resident_relic_grant_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithOnlyPreExistingQuest_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora", includeInteractionLog: true);
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        var preTurnQuests = CreateSoulQuestRoot("quest_liora", "resident_liora");
        var currentQuests = preTurnQuests.DeepClone().AsObject();
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents,
            preTurnSoulQuestsRoot: preTurnQuests);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_quest_request_no_current_turn_quest_change", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithOmittedExistingQuestSnapshot_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora", includeInteractionLog: true);
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        var currentQuests = CreateSoulQuestRoot("quest_liora_old", "resident_liora");
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents,
            markSoulQuestsAsExistingWithoutSnapshot: true);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_quest_request_missing_pre_turn_soul_quests_snapshot", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestForUnknownResident_Fails()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        AddResidentInteractionLog(currentResidents, "resident_missing", "log_missing_resident_quest");
        var preTurnQuests = CreateSoulQuestRoot();
        var currentQuests = CreateSoulQuestRoot("quest_missing_resident", "resident_missing");
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Неизвестный' (residentId=resident_missing, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents,
            preTurnSoulQuestsRoot: preTurnQuests);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "abode_resident_quest_request_unknown_resident", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithNewQuest_Passes()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        AddResidentInteractionLog(currentResidents, "resident_liora", "log_liora_quest_new");
        var preTurnQuests = CreateSoulQuestRoot();
        var currentQuests = CreateSoulQuestRoot("quest_liora", "resident_liora");
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents,
            preTurnSoulQuestsRoot: preTurnQuests);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code!.StartsWith("abode_resident_quest_request_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithFirstQuestFile_Passes()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        AddResidentInteractionLog(currentResidents, "resident_liora", "log_liora_first_quest_file");
        var currentQuests = CreateSoulQuestRoot("quest_liora_first", "resident_liora");
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code!.StartsWith("abode_resident_quest_request_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_AbodeResidentQuestWithOldAndNewQuest_Passes()
    {
        var preTurnSoul = CreateSoulRoot();
        var preTurnResidents = CreateResidentRoot("resident_liora");
        var currentResidents = preTurnResidents.DeepClone().AsObject();
        AddResidentInteractionLog(currentResidents, "resident_liora", "log_liora_quest_second");
        var preTurnQuests = CreateSoulQuestRoot("quest_liora_old", "resident_liora");
        var currentQuests = preTurnQuests.DeepClone().AsObject();
        AddSoulQuest(currentQuests, "quest_liora_new", "resident_liora");
        var playerAction = "[ABODE_RESIDENT_QUEST_REQUEST] Игрок помогает обитателю загробья 'Лиора' (residentId=resident_liora, guardianId=guardian_azalia, abodeId=abode_azalia).";
        await WriteNodeAsync("game_state/meta/guardian_abode_residents.json", currentResidents);
        await WriteNodeAsync("game_state/quests/soul_quests.json", currentQuests);
        await WriteNodeAsync("input/turn_request.json", CreateResidentActionTurnRequest(playerAction));
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: playerAction,
            preTurnResidentRoot: preTurnResidents,
            preTurnSoulQuestsRoot: preTurnQuests);

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.DoesNotContain(issues, issue =>
            issue.Code!.StartsWith("abode_resident_quest_request_", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidateAcceptedTurnSpecialActionOutcomesAsync_SoulImprintWithoutSourceProvenance_Fails()
    {
        var preTurnSoul = CreateSoulRoot(inkFeathers: 10);
        var currentSoul = CreateSoulRoot(inkFeathers: 5);
        currentSoul["soulImprint"] = new JsonObject
        {
            ["imprintId"] = "imprint_liora",
            ["companionName"] = "Лиора",
            ["summary"] = "Слепок памяти Лиоры.",
            ["personalityTraits"] = new JsonArray("верность")
        };
        await WriteNodeAsync("game_state/meta/soul_state.json", currentSoul);
        await WriteNodeAsync("input/turn_request.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["playerAction"] = "[INK_FEATHER_ACTION: SOUL_IMPRINT] Игрок тратит 5 Чернильных Перьев на Создание Слепка Души текущего компаньона."
        });
        await WriteSoulImprintReceiptAsync(includeSourceProvenance: false);
        await WritePendingTurnSnapshotAsync(
            preTurnSoul,
            playerAction: "[INK_FEATHER_ACTION: SOUL_IMPRINT] Игрок тратит 5 Чернильных Перьев на Создание Слепка Души текущего компаньона.");

        var issues = await _validator.ValidateAcceptedTurnSpecialActionOutcomesAsync();

        Assert.Contains(issues, issue =>
            string.Equals(issue.Code, "ink_feather_soul_imprint_missing_source_provenance", StringComparison.OrdinalIgnoreCase));
    }

    private async Task WritePendingTurnSnapshotAsync(
        JsonObject preTurnSoulRoot,
        JsonObject? rollbackSoulRoot = null,
        string? playerAction = null,
        JsonObject? preTurnResidentRoot = null,
        JsonObject? preTurnSoulQuestsRoot = null,
        bool markSoulQuestsAsExistingWithoutSnapshot = false,
        string gachaBaseRarity = "Common")
    {
        const string soulSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/soul_state.json";
        await WriteNodeAsync(soulSnapshotPath, preTurnSoulRoot);

        const string soulRollbackPath = "game_state/meta/soul_state.json.explorer.rollback.test";
        if (rollbackSoulRoot != null)
            await WriteNodeAsync(soulRollbackPath, rollbackSoulRoot);

        var soulSnapshotJson = await _fs.ReadFileAsync(soulSnapshotPath) ?? string.Empty;
        var files = new JsonObject
        {
            ["game_state/meta/soul_state.json"] = soulSnapshotPath
        };
        var snapshotHashes = new JsonObject
        {
            ["game_state/meta/soul_state.json"] = PendingTurnSnapshotAuthority.ComputeSha256(soulSnapshotJson)
        };
        if (preTurnResidentRoot != null)
        {
            const string residentSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/meta/guardian_abode_residents.json";
            await WriteNodeAsync(residentSnapshotPath, preTurnResidentRoot);
            files[GuardianAbodeResidentState.StatePath] = residentSnapshotPath;
            snapshotHashes[GuardianAbodeResidentState.StatePath] =
                PendingTurnSnapshotAuthority.ComputeSha256(await _fs.ReadFileAsync(residentSnapshotPath) ?? string.Empty);
        }

        if (preTurnSoulQuestsRoot != null)
        {
            const string questSnapshotPath = "game_state/control/pending_turn_snapshot/game_state/quests/soul_quests.json";
            await WriteNodeAsync(questSnapshotPath, preTurnSoulQuestsRoot);
            files["game_state/quests/soul_quests.json"] = questSnapshotPath;
            snapshotHashes["game_state/quests/soul_quests.json"] =
                PendingTurnSnapshotAuthority.ComputeSha256(await _fs.ReadFileAsync(questSnapshotPath) ?? string.Empty);
        }

        var rollbackBaselineFiles = new JsonArray();
        if (rollbackSoulRoot != null)
            rollbackBaselineFiles.Add("game_state/meta/soul_state.json");
        if (markSoulQuestsAsExistingWithoutSnapshot)
            rollbackBaselineFiles.Add("game_state/quests/soul_quests.json");

        var manifest = new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["requestTimestamp"] = "2026-04-24T00:00:00Z",
            ["playerAction"] = playerAction ?? "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев.",
            ["gachaBaseResult"] = new JsonObject
            {
                ["baseRarity"] = gachaBaseRarity
            },
            ["files"] = files,
            ["snapshotFileHashes"] = snapshotHashes,
            ["clientOwnedValidationHashes"] = new JsonObject(),
            ["rollbackBackups"] = rollbackSoulRoot == null
                ? new JsonObject()
                : new JsonObject
                {
                    ["game_state/meta/soul_state.json"] = soulRollbackPath
                },
            ["rollbackBaselineFiles"] = rollbackBaselineFiles,
            ["sourceLabel"] = "обычный ход игрока"
        };
        manifest["manifestPayloadHash"] = PendingTurnSnapshotTestAuthority.ComputeManifestPayloadHash(manifest);
        await WriteNodeAsync("game_state/control/pending_turn_snapshot.json", manifest);
        await PendingTurnSnapshotTestAuthority.SyncAuthorityForCurrentManifestAsync(_fs);
    }

    private async Task WriteNodeAsync(string path, JsonNode node)
    {
        await _fs.WriteFileAtomicAsync(path, node.ToJsonString());
    }

    private static JsonObject CreateDirectGachaTurnRequest(string baseRarity = "Common") => new()
    {
        ["sessionId"] = "session",
        ["requestId"] = "request",
        ["turnNumber"] = 7,
        ["playerAction"] = "[CHAOS_SEA_DIRECT_GACHA] Игрок напрямую тянет Реликвию Души из Моря Хаоса и тратит 5 Чернильных Перьев.",
        ["gachaBaseResult"] = new JsonObject
        {
            ["baseRarity"] = baseRarity
        }
    };

    private static JsonObject CreateCultivateEnlightenmentTurnRequest() => new()
    {
        ["sessionId"] = "session",
        ["requestId"] = "request",
        ["turnNumber"] = 7,
        ["playerAction"] = "[INK_FEATHER_ACTION: CULTIVATE_ENLIGHTENMENT] Игрок вкладывает 5 Чернильных Перьев в просветление."
    };

    private static JsonObject CreateResidentActionTurnRequest(string playerAction) => new()
    {
        ["sessionId"] = "session",
        ["requestId"] = "request",
        ["turnNumber"] = 7,
        ["playerAction"] = playerAction
    };

    private static JsonObject CreateResidentRoot(string residentId, bool includeInteractionLog = false)
    {
        var root = new JsonObject
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["residentId"] = residentId,
                    ["guardianId"] = "guardian_azalia",
                    ["abodeId"] = "abode_azalia",
                    ["displayName"] = "Лиора",
                    ["bondRewardState"] = "eligible",
                    ["grantedRelicId"] = ""
                }
            },
            ["interactionLog"] = new JsonArray()
        };

        if (includeInteractionLog)
        {
            root["interactionLog"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = "log_liora_quest",
                ["residentId"] = residentId,
                ["title"] = "Просьба Лиоры",
                ["summary"] = "Лиора попросила помощи.",
                ["turn"] = 7,
                ["timestamp"] = "2026-04-24T00:00:00Z"
            });
        }

        return root;
    }

    private static void SetResidentRewardGranted(JsonObject residentRoot, string relicId)
    {
        var resident = residentRoot["entries"]?.AsArray().OfType<JsonObject>().First()
                       ?? throw new InvalidOperationException("Expected resident test entry.");
        resident["bondRewardState"] = "granted";
        resident["grantedRelicId"] = relicId;
    }

    private static void AddResidentInteractionLog(JsonObject residentRoot, string residentId, string entryId)
    {
        var log = residentRoot["interactionLog"]?.AsArray()
                  ?? throw new InvalidOperationException("Expected interactionLog test array.");
        log.Add(new JsonObject
        {
            ["entryId"] = entryId,
            ["residentId"] = residentId,
            ["title"] = "Память Лиоры",
            ["summary"] = "Лиора оставила новую память текущего хода.",
            ["turn"] = 7,
            ["timestamp"] = "2026-04-24T00:00:00Z"
        });
    }

    private static JsonObject CreateSoulQuestRoot(string? questId = null, string residentId = "resident_liora")
    {
        var root = new JsonObject
        {
            ["quests"] = new JsonArray()
        };
        if (!string.IsNullOrWhiteSpace(questId))
        {
            AddSoulQuest(root, questId, residentId);
        }

        return root;
    }

    private static void AddSoulQuest(JsonObject root, string questId, string residentId)
    {
        root["quests"]!.AsArray().Add(new JsonObject
        {
            ["questId"] = questId,
            ["title"] = "Просьба Лиоры",
            ["description"] = "Помочь Лиоре удержать память Обители.",
            ["status"] = "active",
            ["relatedAfterlifeResidentId"] = residentId,
            ["objectives"] = new JsonArray("Выслушать Лиору")
        });
    }

    private async Task WriteCultivateEnlightenmentReceiptAsync()
    {
        await WriteNodeAsync("output/ink_feather_action_result.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["actionTag"] = "CULTIVATE_ENLIGHTENMENT",
            ["resolved"] = true,
            ["costInFeathers"] = 5,
            ["resolutionType"] = "enlightenmentProgress",
            ["summary"] = "Просветление продвинулось.",
            ["stateEvidence"] = new JsonObject
            {
                ["experienceGain"] = 20,
                ["affectedFiles"] = new JsonArray("game_state/meta/soul_state.json")
            }
        });
    }

    private async Task WriteSoulImprintReceiptAsync(bool includeSourceProvenance)
    {
        var stateEvidence = new JsonObject
        {
            ["imprintId"] = "imprint_liora",
            ["companionName"] = "Лиора",
            ["affectedFiles"] = new JsonArray("game_state/meta/soul_state.json")
        };
        if (includeSourceProvenance)
            stateEvidence["sourceCompanionId"] = "companion_liora";

        await WriteNodeAsync("output/ink_feather_action_result.json", new JsonObject
        {
            ["sessionId"] = "session",
            ["requestId"] = "request",
            ["turnNumber"] = 7,
            ["actionTag"] = "SOUL_IMPRINT",
            ["resolved"] = true,
            ["costInFeathers"] = 5,
            ["resolutionType"] = "soulImprint",
            ["summary"] = "Слепок создан.",
            ["stateEvidence"] = stateEvidence
        });
    }

    private static JsonObject CreateSoulRoot(string currentRealm = "Chaos Sea", int inkFeathers = 10, int enlightenmentExperience = 0) => new()
    {
        ["currentRealm"] = currentRealm,
        ["currentIncarnation"] = 2,
        ["inkFeathers"] = new JsonObject
        {
            ["current"] = inkFeathers,
            ["total"] = 10
        },
        ["enlightenment"] = new JsonObject
        {
            ["experience"] = enlightenmentExperience,
            ["level"] = 1,
            ["currentTier"] = "Ур. 1"
        },
        ["soulRelics"] = new JsonObject
        {
            ["equipped"] = new JsonArray(),
            ["stored"] = new JsonArray
            {
                new JsonObject
                {
                    ["relicId"] = "relic_existing",
                    ["name"] = "Старая реликвия",
                    ["rarity"] = "Common",
                    ["quality"] = "Common"
                }
            }
        }
    };

    private static void AddStoredSoulRelic(JsonObject soulRoot, string relicId, string rarity = "Common")
    {
        var stored = soulRoot["soulRelics"]?["stored"]?.AsArray()
            ?? throw new InvalidOperationException("Expected soulRelics.stored test array.");
        stored.Add(new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = "Новая реликвия",
            ["rarity"] = rarity,
            ["quality"] = rarity
        });
    }

    private static void AddCompanionEchoRelic(JsonObject soulRoot, string relicId)
    {
        var stored = soulRoot["soulRelics"]?["stored"]?.AsArray()
            ?? throw new InvalidOperationException("Expected soulRelics.stored test array.");
        stored.Add(new JsonObject
        {
            ["relicId"] = relicId,
            ["name"] = "Эхо Лиоры",
            ["rarity"] = "Rare",
            ["quality"] = "Rare",
            ["relicType"] = GuardianAbodeResidentState.RelicTypeCompanionEcho,
            ["companionSeed"] = new JsonObject
            {
                ["sourceResidentId"] = "resident_liora",
                ["sourceGuardianId"] = "guardian_azalia",
                ["companionNameHint"] = "Лиора",
                ["originWorldSummary"] = "Память Обители Азалии.",
                ["futureCompanionPrompt"] = "Лиора может вернуться спутницей в следующей жизни."
            }
        });
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
            // ignored
        }
    }
}
