using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class GuardianCorrectionService
{
    public const string StatePath = "game_state/world/guardian_corrections.json";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private readonly FileSystemManager _fs;
    private readonly ScenarioCoreService _scenarioCoreService;
    private readonly ILogger<GuardianCorrectionService> _logger;

    public GuardianCorrectionService(
        FileSystemManager fs,
        ScenarioCoreService scenarioCoreService,
        ILogger<GuardianCorrectionService> logger)
    {
        _fs = fs;
        _scenarioCoreService = scenarioCoreService;
        _logger = logger;
    }

    public sealed class GuardianCorrectionsState
    {
        [JsonPropertyName("lifeIncarnation")]
        public int LifeIncarnation { get; set; }

        [JsonPropertyName("appliedAt")]
        public string AppliedAt { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "none";

        [JsonPropertyName("reputationAtApplication")]
        public int ReputationAtApplication { get; set; }

        [JsonPropertyName("powerBefore")]
        public int PowerBefore { get; set; }

        [JsonPropertyName("powerAfter")]
        public int PowerAfter { get; set; }

        [JsonPropertyName("baseBudgetPoints")]
        public int BaseBudgetPoints { get; set; }

        [JsonPropertyName("remainingBudgetPoints")]
        public int RemainingBudgetPoints { get; set; }

        [JsonPropertyName("totalAbodePowerSpent")]
        public int TotalAbodePowerSpent { get; set; }

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("scenarioCoreSnapshot")]
        public GuardianCorrectionScenarioSnapshot ScenarioCoreSnapshot { get; set; } = new();

        [JsonPropertyName("claimants")]
        public List<GuardianCorrectionClaimant> Claimants { get; set; } = new();

        [JsonPropertyName("contestedSlots")]
        public List<GuardianCorrectionContest> ContestedSlots { get; set; } = new();

        [JsonPropertyName("resolutionOrder")]
        public List<string> ResolutionOrder { get; set; } = new();

        [JsonPropertyName("corrections")]
        public List<GuardianCorrectionEntry> Corrections { get; set; } = new();
    }

    public sealed class GuardianCorrectionScenarioSnapshot
    {
        [JsonPropertyName("scenarioCoreAssertions")]
        public List<ScenarioCoreService.ScenarioCoreAssertion> ScenarioCoreAssertions { get; set; } = new();

        [JsonPropertyName("openCorrectionSlots")]
        public List<ScenarioCoreService.ScenarioCorrectionSlot> OpenCorrectionSlots { get; set; } = new();
    }

    public sealed class GuardianCorrectionEntry
    {
        [JsonPropertyName("correctionId")]
        public string CorrectionId { get; set; } = "";

        [JsonPropertyName("sourceGuardianId")]
        public string SourceGuardianId { get; set; } = "";

        [JsonPropertyName("sourceGuardianName")]
        public string SourceGuardianName { get; set; } = "";

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "";

        [JsonPropertyName("slotId")]
        public string SlotId { get; set; } = "";

        [JsonPropertyName("slotType")]
        public string SlotType { get; set; } = "";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "minor";

        [JsonPropertyName("budgetCostPoints")]
        public int BudgetCostPoints { get; set; }

        [JsonPropertyName("abodePowerCost")]
        public int AbodePowerCost { get; set; }

        [JsonPropertyName("claimStrength")]
        public int ClaimStrength { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";

        [JsonPropertyName("summary")]
        public string Summary { get; set; } = "";

        [JsonPropertyName("reason")]
        public string Reason { get; set; } = "";

        [JsonPropertyName("affectsStartAs")]
        public string AffectsStartAs { get; set; } = "";
    }

    public sealed class GuardianCorrectionClaimant
    {
        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "none";

        [JsonPropertyName("isActivePatron")]
        public bool IsActivePatron { get; set; }

        [JsonPropertyName("currentPower")]
        public int CurrentPower { get; set; }

        [JsonPropertyName("powerAfter")]
        public int PowerAfter { get; set; }

        [JsonPropertyName("baseBudgetPoints")]
        public int BaseBudgetPoints { get; set; }

        [JsonPropertyName("preparationBudgetPoints")]
        public int PreparationBudgetPoints { get; set; }

        [JsonPropertyName("remainingBudgetPoints")]
        public int RemainingBudgetPoints { get; set; }

        [JsonPropertyName("claimStrengthBase")]
        public int ClaimStrengthBase { get; set; }

        [JsonPropertyName("eligible")]
        public bool Eligible { get; set; }

        [JsonPropertyName("sourceSummary")]
        public string SourceSummary { get; set; } = "";
    }

    public sealed class GuardianCorrectionContest
    {
        [JsonPropertyName("slotId")]
        public string SlotId { get; set; } = "";

        [JsonPropertyName("slotType")]
        public string SlotType { get; set; } = "";

        [JsonPropertyName("winnerGuardianId")]
        public string WinnerGuardianId { get; set; } = "";

        [JsonPropertyName("winnerGuardianName")]
        public string WinnerGuardianName { get; set; } = "";

        [JsonPropertyName("winnerCorrectionId")]
        public string WinnerCorrectionId { get; set; } = "";

        [JsonPropertyName("candidates")]
        public List<GuardianCorrectionCandidate> Candidates { get; set; } = new();
    }

    public sealed class GuardianCorrectionCandidate
    {
        [JsonPropertyName("candidateCorrectionId")]
        public string CandidateCorrectionId { get; set; } = "";

        [JsonPropertyName("sourceGuardianId")]
        public string SourceGuardianId { get; set; } = "";

        [JsonPropertyName("sourceGuardianName")]
        public string SourceGuardianName { get; set; } = "";

        [JsonPropertyName("intent")]
        public string Intent { get; set; } = "";

        [JsonPropertyName("severity")]
        public string Severity { get; set; } = "minor";

        [JsonPropertyName("budgetCostPoints")]
        public int BudgetCostPoints { get; set; }

        [JsonPropertyName("abodePowerCost")]
        public int AbodePowerCost { get; set; }

        [JsonPropertyName("claimStrength")]
        public int ClaimStrength { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }

    public async Task<GuardianCorrectionsState?> ReadAsync()
    {
        var raw = await _fs.ReadFileAsync(StatePath);
        if (string.IsNullOrWhiteSpace(raw))
            return null;

        try
        {
            return JsonSerializer.Deserialize<GuardianCorrectionsState>(raw, JsonOpts);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Не удалось прочитать guardian corrections state");
            return null;
        }
    }

    public async Task ResetForAfterlifeAsync()
    {
        _fs.DeleteFile(StatePath);
        await Task.CompletedTask;
    }

    public async Task ApplyForNewLifeAsync(int lifeIncarnation)
    {
        var scenario = await _scenarioCoreService.ReadAsync();
        if (scenario == null)
        {
            _fs.DeleteFile(StatePath);
            return;
        }

        var guardiansRaw = await _fs.ReadFileAsync("game_state/meta/guardians.json");
        if (string.IsNullOrWhiteSpace(guardiansRaw))
        {
            _fs.DeleteFile(StatePath);
            return;
        }

        var guardiansRoot = JsonNode.Parse(guardiansRaw) as JsonObject;
        var activeGuardian = guardiansRoot?["activeGuardian"] as JsonObject;
        if (guardiansRoot == null || activeGuardian == null)
        {
            _fs.DeleteFile(StatePath);
            return;
        }

        var guardianId = GetString(activeGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId))
        {
            _fs.DeleteFile(StatePath);
            return;
        }

        var guardianName = GuardianManifestation.GetDisplayName(ToJsonElement(activeGuardian));
        var reputation = GuardianGachaChargeRules.ResolveGuardianReputation(activeGuardian);
        var trackerRoot = await ReadTrackerRootAsync();
        var activeGuardianDerivedState = GuardianProjectState.ResolveGuardianDerivedState(activeGuardian, trackerRoot);
        var currentPower = activeGuardianDerivedState.CurrentPower;
        var budgetPoints = activeGuardianDerivedState.BaseNextLifeCorrectionBudgetPoints;
        var intent = ResolveIntent(reputation);
        var trackerChanged = GuardianProjectState.ExpireLifeBoundEffects(trackerRoot, lifeIncarnation);

        var state = new GuardianCorrectionsState
        {
            LifeIncarnation = lifeIncarnation,
            AppliedAt = DateTime.UtcNow.ToString("o"),
            GuardianId = guardianId!,
            GuardianName = guardianName,
            Intent = intent,
            ReputationAtApplication = reputation,
            PowerBefore = currentPower,
            PowerAfter = currentPower,
            BaseBudgetPoints = budgetPoints,
            RemainingBudgetPoints = budgetPoints,
            ScenarioCoreSnapshot = new GuardianCorrectionScenarioSnapshot
            {
                ScenarioCoreAssertions = scenario.ScenarioCoreAssertions.Select(CloneAssertion).ToList(),
                OpenCorrectionSlots = scenario.OpenCorrectionSlots.Select(CloneSlot).ToList()
            }
        };

        var claimants = BuildClaimants(
            guardiansRoot,
            guardianId!,
            trackerRoot,
            budgetPoints,
            currentPower,
            reputation,
            intent,
            activeGuardianDerivedState);
        state.Claimants = claimants.Select(BuildClaimantSnapshot).ToList();

        if (claimants.Count == 0)
        {
            state.Summary = budgetPoints <= 0
                ? "Сила Обители активного Хранителя недостаточна для явных корректив этой жизни."
                : "Ни один Хранитель не получил достаточно сильного claim на совместимую коррективу этой жизни.";
            await _fs.WriteFileAtomicAsync(StatePath, JsonSerializer.Serialize(state, JsonOpts));
            return;
        }

        var (corrections, contestedSlots, resolutionOrder) = ResolveCorrectionsMultiClaimant(claimants, scenario.OpenCorrectionSlots);

        state.Corrections = corrections;
        state.ContestedSlots = contestedSlots;
        state.ResolutionOrder = resolutionOrder;
        state.Claimants = claimants.Select(BuildClaimantSnapshot).ToList();
        state.RemainingBudgetPoints = claimants.Where(item => item.IsActivePatron).Select(item => item.RemainingBudget).DefaultIfEmpty(budgetPoints).First();
        state.TotalAbodePowerSpent = corrections.Sum(c => c.AbodePowerCost);
        state.PowerAfter = claimants.Where(item => item.IsActivePatron).Select(item => item.PowerAfter).DefaultIfEmpty(currentPower).First();
        state.Summary = corrections.Count == 0
            ? "Подходящих совместимых слотов для явных корректив в этом сценарии не нашлось."
            : string.Join(" ", corrections.Select(c => c.Summary));
        trackerChanged = GuardianProjectState.ConsumeSoulPreparationForLife(trackerRoot, lifeIncarnation) || trackerChanged;
        var relationshipChanged = ApplyCorrectionRelationshipEffects(guardiansRoot, claimants, corrections, contestedSlots);

        if (state.TotalAbodePowerSpent > 0)
        {
            var powerJournalEntries = new List<JsonObject>();
            var changed = GuardianPowerEventState.ApplyEvents(
                guardiansRoot,
                corrections.Select(BuildPowerSpendEvent),
                0,
                powerJournalEntries);
            if (changed || relationshipChanged)
                await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString(JsonOpts));
            if (powerJournalEntries.Count > 0)
                await GuardianPowerEventState.AppendJournalEntriesAsync(_fs, powerJournalEntries);
        }
        else if (relationshipChanged)
        {
            await _fs.WriteFileAtomicAsync("game_state/meta/guardians.json", guardiansRoot.ToJsonString(JsonOpts));
        }

        if (trackerChanged && trackerRoot != null)
            await _fs.WriteFileAtomicAsync(GuardianProjectState.TrackerPath, trackerRoot.ToJsonString(JsonOpts));

        await _fs.WriteFileAtomicAsync(StatePath, JsonSerializer.Serialize(state, JsonOpts));
    }

    public async Task<string?> BuildSystemReminderFragmentAsync(string? currentRealm)
    {
        if (!RealmSemantics.IsMortalRealm(currentRealm))
            return null;

        var state = await ReadAsync();
        if (state == null)
            return null;

        var parts = new List<string>
        {
            "GUARDIAN CORRECTIONS FOR THIS LIFE:",
            $"  - Client-authored applied correction state exists at {StatePath}.",
            "  - These are compatible additions around the confirmed Scenario Core, not permission to rewrite the player's start."
        };

        if (state.Corrections.Count == 0)
        {
            parts.Add($"  - No explicit corrections were applied. Summary: {state.Summary}");
            return string.Join(Environment.NewLine, parts);
        }

        parts.Add($"  - Source guardian: {state.GuardianName} ({state.Intent}, reputation {state.ReputationAtApplication}, power {state.PowerBefore}->{state.PowerAfter})");
        foreach (var claimant in state.Claimants)
            parts.Add($"  - Claimant: {claimant.GuardianName} [{claimant.Intent}] power {claimant.CurrentPower}->{claimant.PowerAfter}, budget {claimant.BaseBudgetPoints}+{claimant.PreparationBudgetPoints}->{claimant.RemainingBudgetPoints}");
        foreach (var contest in state.ContestedSlots.Where(item => item.Candidates.Count > 1))
            parts.Add($"  - Contested slot {contest.SlotType}: winner {contest.WinnerGuardianName}");
        foreach (var correction in state.Corrections)
            parts.Add($"  - [{correction.Severity}/{correction.SlotType}] {correction.Title}: {correction.Summary}");

        return string.Join(Environment.NewLine, parts);
    }

    private static string ResolveIntent(int reputation)
    {
        if (reputation <= -21)
            return "hostile";
        if (reputation >= 20)
            return "friendly";
        return "none";
    }

    private async Task<JsonObject?> ReadTrackerRootAsync()
    {
        var trackerRaw = await _fs.ReadFileAsync(GuardianProjectState.TrackerPath);
        if (string.IsNullOrWhiteSpace(trackerRaw))
            return null;

        try
        {
            return JsonNode.Parse(trackerRaw) as JsonObject;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Не удалось прочитать guardian project tracker для guardian corrections");
        }

        return null;
    }

    private List<ClaimantRuntime> BuildClaimants(
        JsonObject guardiansRoot,
        string activeGuardianId,
        JsonObject? trackerRoot,
        int activeBaseBudget,
        int activePower,
        int activeReputation,
        string activeIntent,
        GuardianProjectState.ResolvedGuardianDerivedState activeGuardianDerivedState)
    {
        var allGuardians = new List<JsonObject>();
        if (guardiansRoot["guardians"] is JsonArray guardians)
            allGuardians.AddRange(guardians.OfType<JsonObject>());
        if (guardiansRoot["activeGuardian"] is JsonObject activeGuardian &&
            allGuardians.All(item => !string.Equals(GetString(item["guardianId"]), activeGuardianId, StringComparison.OrdinalIgnoreCase)))
        {
            allGuardians.Add(activeGuardian);
        }

        var activeProjectEffects = activeGuardianDerivedState.ProjectEffects;
        var hostilePriorityBonus = activeProjectEffects.HostilePriorityTokensGranted;
        var rivalClaimants = new List<ClaimantRuntime>();
        foreach (var guardian in allGuardians)
        {
            var guardianId = GetString(guardian["guardianId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.Equals(guardianId, activeGuardianId, StringComparison.OrdinalIgnoreCase))
                continue;

            var offensiveBonus = GuardianProjectState.GetLatestCompletedOffensiveBonus(trackerRoot, guardianId, activeGuardianId);
            if (offensiveBonus <= 0)
                continue;

            var derivedState = GuardianProjectState.ResolveGuardianDerivedState(guardian, trackerRoot);
            var currentPower = derivedState.CurrentPower;
            var baseBudget = derivedState.BaseNextLifeCorrectionBudgetPoints;
            if (baseBudget <= 0)
                continue;

            var projectEffects = derivedState.ProjectEffects;
            var preparationBudget = projectEffects.PreparationBudgetPoints;
            var preparationClaimBonus = projectEffects.PreparationClaimPriorityBonus;
            var relationshipPressureBonus = GuardianRelationshipRules.ResolveCorrectionPressureBonus(guardian, activeGuardianId, allGuardians, trackerRoot);
            var claimant = new ClaimantRuntime
            {
                GuardianId = guardianId,
                GuardianName = GuardianManifestation.GetDisplayName(ToJsonElement(guardian)),
                Intent = "hostile",
                IsActivePatron = false,
                CurrentPower = currentPower,
                PowerAfter = currentPower,
                BaseBudget = baseBudget,
                PreparationBudget = preparationBudget,
                RemainingBudget = baseBudget + preparationBudget,
                RemainingPower = currentPower,
                Reputation = GuardianGachaChargeRules.ResolveGuardianReputation(guardian),
                PreparationClaimBonus = preparationClaimBonus + hostilePriorityBonus,
                ProjectClaimBonus = offensiveBonus + relationshipPressureBonus,
                ClaimStrengthBase = AbodePowerRules.GetCorrectionClaimPowerBand(currentPower) + offensiveBonus + relationshipPressureBonus + preparationClaimBonus + hostilePriorityBonus,
                Eligible = true,
                SourceSummary = $"Враждебный claim через completed offensive_intrigue против {activeGuardianId}. Political bonus +{offensiveBonus}, relation pressure +{relationshipPressureBonus}, preparation +{preparationClaimBonus}, hostile priority +{hostilePriorityBonus}."
            };
            rivalClaimants.Add(claimant);
        }

        rivalClaimants = rivalClaimants
            .OrderByDescending(item => item.ClaimStrengthBase)
            .ThenByDescending(item => item.CurrentPower)
            .ThenBy(item => item.GuardianId, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var selected = new List<ClaimantRuntime>();
        if (!string.Equals(activeIntent, "none", StringComparison.OrdinalIgnoreCase) && activeBaseBudget > 0)
        {
            var fortificationBonus = GuardianProjectState.GetLatestCompletedFortificationBonus(trackerRoot, activeGuardianId);
            var topRival = rivalClaimants.FirstOrDefault();
            var counterBonus = topRival != null
                ? GuardianProjectState.GetLatestCompletedCounterOperationBonus(trackerRoot, activeGuardianId, topRival.GuardianId)
                : 0;
            var coalitionDefenseBonus = topRival != null
                ? GuardianRelationshipRules.ResolveCorrectionDefenseSupportBonus(allGuardians, activeGuardianId, topRival.GuardianId, trackerRoot)
                : 0;
            var preparationBudget = activeProjectEffects.PreparationBudgetPoints;
            var preparationClaimBonus = activeProjectEffects.PreparationClaimPriorityBonus;
            selected.Add(new ClaimantRuntime
            {
                GuardianId = activeGuardianId,
                GuardianName = guardiansRoot["activeGuardian"] is JsonObject activeGuardianNode
                    ? GuardianManifestation.GetDisplayName(ToJsonElement(activeGuardianNode))
                    : activeGuardianId,
                Intent = activeIntent,
                IsActivePatron = true,
                CurrentPower = activePower,
                PowerAfter = activePower,
                BaseBudget = activeBaseBudget,
                PreparationBudget = preparationBudget,
                RemainingBudget = activeBaseBudget + preparationBudget,
                RemainingPower = activePower,
                Reputation = activeReputation,
                PreparationClaimBonus = preparationClaimBonus,
                ProjectClaimBonus = fortificationBonus + counterBonus + 1 + coalitionDefenseBonus,
                ClaimStrengthBase = AbodePowerRules.GetCorrectionClaimPowerBand(activePower) + preparationClaimBonus + fortificationBonus + counterBonus + 1 + coalitionDefenseBonus,
                Eligible = true,
                SourceSummary = $"Active patron claim. Fortification shield +{fortificationBonus}, counter-operation +{counterBonus}, coalition support +{coalitionDefenseBonus}, preparation +{preparationClaimBonus}."
            });
        }

        if (rivalClaimants.Count > 0)
            selected.Add(rivalClaimants[0]);

        return selected
            .Where(item => item.Eligible && item.RemainingBudget > 0 && item.RemainingPower > 0)
            .Take(2)
            .ToList();
    }

    private static GuardianCorrectionClaimant BuildClaimantSnapshot(ClaimantRuntime claimant)
    {
        return new GuardianCorrectionClaimant
        {
            GuardianId = claimant.GuardianId,
            GuardianName = claimant.GuardianName,
            Intent = claimant.Intent,
            IsActivePatron = claimant.IsActivePatron,
            CurrentPower = claimant.CurrentPower,
            PowerAfter = claimant.PowerAfter,
            BaseBudgetPoints = claimant.BaseBudget,
            PreparationBudgetPoints = claimant.PreparationBudget,
            RemainingBudgetPoints = claimant.RemainingBudget,
            ClaimStrengthBase = claimant.ClaimStrengthBase,
            Eligible = claimant.Eligible,
            SourceSummary = claimant.SourceSummary
        };
    }

    private static bool ApplyCorrectionRelationshipEffects(
        JsonObject guardiansRoot,
        IReadOnlyList<ClaimantRuntime> claimants,
        IReadOnlyList<GuardianCorrectionEntry> corrections,
        IReadOnlyList<GuardianCorrectionContest> contestedSlots)
    {
        var activeClaimant = claimants.FirstOrDefault(item => item.IsActivePatron);
        var hostileClaimant = claimants.FirstOrDefault(item =>
            !item.IsActivePatron &&
            string.Equals(item.Intent, "hostile", StringComparison.OrdinalIgnoreCase));
        if (activeClaimant == null || hostileClaimant == null)
            return false;

        var hostileWon = corrections.Any(item =>
            string.Equals(item.SourceGuardianId, hostileClaimant.GuardianId, StringComparison.OrdinalIgnoreCase));
        var directlyContested = contestedSlots.Any(contest =>
            contest.Candidates.Any(candidate => string.Equals(candidate.SourceGuardianId, activeClaimant.GuardianId, StringComparison.OrdinalIgnoreCase)) &&
            contest.Candidates.Any(candidate => string.Equals(candidate.SourceGuardianId, hostileClaimant.GuardianId, StringComparison.OrdinalIgnoreCase)));
        if (!hostileWon && !directlyContested)
            return false;

        return GuardianRelationshipRules.ApplyMutualDelta(
            guardiansRoot,
            hostileClaimant.GuardianId,
            activeClaimant.GuardianId,
            hostileWon ? -10 : -6,
            hostileWon ? -8 : -4,
            hostileWon
                ? $"Relations worsened after hostile correction claims from {hostileClaimant.GuardianId} prevailed against {activeClaimant.GuardianId}."
                : $"Relations worsened after hostile correction conflict against {activeClaimant.GuardianId}.",
            hostileWon
                ? $"Relations worsened after {hostileClaimant.GuardianId} pushed hostile life corrections against this Guardian."
                : $"Relations worsened after a contested hostile correction claim from {hostileClaimant.GuardianId}.");
    }

    private static (List<GuardianCorrectionEntry> Corrections, List<GuardianCorrectionContest> ContestedSlots, List<string> ResolutionOrder)
        ResolveCorrectionsMultiClaimant(
            List<ClaimantRuntime> claimants,
            IReadOnlyList<ScenarioCoreService.ScenarioCorrectionSlot> slots)
    {
        var allCandidates = new List<(ClaimantRuntime Claimant, GuardianCorrectionEntry Correction)>();
        foreach (var claimant in claimants)
        {
            var usedSlots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var template in GetTemplates(claimant.Intent))
            {
                var slot = slots.FirstOrDefault(candidate =>
                    string.Equals(candidate.SlotType, template.SlotType, StringComparison.OrdinalIgnoreCase) &&
                    !usedSlots.Contains(candidate.SlotId) &&
                    ((string.Equals(claimant.Intent, "friendly", StringComparison.OrdinalIgnoreCase) && candidate.AllowsFriendly) ||
                     (string.Equals(claimant.Intent, "hostile", StringComparison.OrdinalIgnoreCase) && candidate.AllowsHostile)));
                if (slot == null)
                    continue;

                var severity = ResolveSeverity(claimant, slot.MaxSeverity);
                if (severity == null)
                    continue;

                var budgetCost = AbodePowerRules.GetCorrectionSeverityBudgetCost(severity);
                var abodePowerCost = AbodePowerRules.GetCorrectionSeverityAbodePowerCost(severity);
                if (budgetCost > claimant.RemainingBudget || abodePowerCost > claimant.RemainingPower)
                    continue;

                var correction = new GuardianCorrectionEntry
                {
                    CorrectionId = $"{claimant.GuardianId}_{slot.SlotId}_{severity}",
                    SourceGuardianId = claimant.GuardianId,
                    SourceGuardianName = claimant.GuardianName,
                    Intent = claimant.Intent,
                    SlotId = slot.SlotId,
                    SlotType = slot.SlotType,
                    Severity = severity,
                    BudgetCostPoints = budgetCost,
                    AbodePowerCost = abodePowerCost,
                    ClaimStrength = claimant.ClaimStrengthBase + AbodePowerRules.GetCorrectionSeverityBudgetCost(severity),
                    Title = template.GetTitle(severity),
                    Summary = template.GetSummary(severity, claimant.GuardianName),
                    Reason = template.GetReason(claimant.Intent, claimant.GuardianName),
                    AffectsStartAs = template.AffectsStartAs
                };

                allCandidates.Add((claimant, correction));
                usedSlots.Add(slot.SlotId);
            }
        }

        var results = new List<GuardianCorrectionEntry>();
        var contests = new List<GuardianCorrectionContest>();
        var resolutionOrder = new List<string>();
        var hostileStrongUsed = false;

        foreach (var slotGroup in allCandidates
                     .GroupBy(item => item.Correction.SlotId, StringComparer.OrdinalIgnoreCase)
                     .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase))
        {
            var orderedCandidates = slotGroup
                .OrderByDescending(item => item.Correction.ClaimStrength)
                .ThenByDescending(item => item.Claimant.IsActivePatron)
                .ThenByDescending(item => item.Claimant.CurrentPower)
                .ThenBy(item => item.Claimant.GuardianId, StringComparer.OrdinalIgnoreCase)
                .ToList();

            var contest = new GuardianCorrectionContest
            {
                SlotId = slotGroup.Key,
                SlotType = orderedCandidates[0].Correction.SlotType,
                Candidates = orderedCandidates.Select(item => new GuardianCorrectionCandidate
                {
                    CandidateCorrectionId = item.Correction.CorrectionId,
                    SourceGuardianId = item.Correction.SourceGuardianId,
                    SourceGuardianName = item.Correction.SourceGuardianName,
                    Intent = item.Correction.Intent,
                    Severity = item.Correction.Severity,
                    BudgetCostPoints = item.Correction.BudgetCostPoints,
                    AbodePowerCost = item.Correction.AbodePowerCost,
                    ClaimStrength = item.Correction.ClaimStrength,
                    Title = item.Correction.Title
                }).ToList()
            };

            var winner = orderedCandidates.FirstOrDefault(item =>
                item.Claimant.RemainingBudget >= item.Correction.BudgetCostPoints &&
                item.Claimant.RemainingPower >= item.Correction.AbodePowerCost &&
                !(hostileStrongUsed &&
                  string.Equals(item.Correction.Intent, "hostile", StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(item.Correction.Severity, "strong", StringComparison.OrdinalIgnoreCase)));

            if (winner.Correction == null)
            {
                contests.Add(contest);
                resolutionOrder.Add($"{slotGroup.Key}: no winner");
                continue;
            }

            winner.Claimant.RemainingBudget -= winner.Correction.BudgetCostPoints;
            winner.Claimant.RemainingPower -= winner.Correction.AbodePowerCost;
            winner.Claimant.PowerAfter = winner.Claimant.RemainingPower;
            if (string.Equals(winner.Correction.Intent, "hostile", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(winner.Correction.Severity, "strong", StringComparison.OrdinalIgnoreCase))
            {
                hostileStrongUsed = true;
            }

            contest.WinnerGuardianId = winner.Correction.SourceGuardianId;
            contest.WinnerGuardianName = winner.Correction.SourceGuardianName;
            contest.WinnerCorrectionId = winner.Correction.CorrectionId;
            contests.Add(contest);
            resolutionOrder.Add($"{slotGroup.Key}: {winner.Correction.SourceGuardianName} [{winner.Correction.Severity}]");
            results.Add(winner.Correction);
        }

        return (results, contests, resolutionOrder);
    }

    private static JsonObject BuildPowerSpendEvent(GuardianCorrectionEntry correction)
    {
        var audit = new JsonObject
        {
            ["correctionId"] = correction.CorrectionId,
            ["slotId"] = correction.SlotId,
            ["slotType"] = correction.SlotType,
            ["severity"] = correction.Severity,
            ["claimStrength"] = correction.ClaimStrength,
            ["intent"] = correction.Intent
        };

        return GuardianPowerEventState.BuildEvent(
            $"gce_{correction.CorrectionId}",
            correction.SourceGuardianId,
            -correction.AbodePowerCost,
            "correction_spend",
            "guardian_corrections",
            correction.CorrectionId,
            $"Корректива Хранителя: {correction.Title}",
            correction.Reason,
            audit);
    }

    private static IReadOnlyList<CorrectionTemplate> GetTemplates(string intent)
    {
        if (string.Equals(intent, "friendly", StringComparison.OrdinalIgnoreCase))
        {
            return
            [
                new CorrectionTemplate(
                    "protection_or_omen",
                    "protective_omen",
                    "Старт получает незримую защиту или доброе предзнаменование",
                    "в старте уже действует защитный или благоприятный слой",
                    "добавляя совместимую благую поддержку вокруг исходного сценария"),
                new CorrectionTemplate(
                    "ally_thread",
                    "ally_thread",
                    "Рядом со стартом уже существует союзная нить судьбы",
                    "у игрока с самого начала появляется потенциальный союзник или покровитель",
                    "добавляя совместимую социальную опору в пределах сценарного ядра"),
                new CorrectionTemplate(
                    "resource_blessing",
                    "resource_blessing",
                    "Старт получает скрытую ресурсную подушку безопасности",
                    "старт снабжается мягким ресурсным преимуществом, не отменяющим исходные условия",
                    "вкладывая силу Обители в мягкое облегчение старта")
            ];
        }

        return
        [
            new CorrectionTemplate(
                "rival_thread",
                "rival_thread",
                "С самого начала формируется чужая враждебная нить судьбы",
                "в мире уже зреет параллельная враждебная линия, способная войти в конфликт с игроком",
                "навязывая совместимый конфликт вокруг исходного сценария"),
            new CorrectionTemplate(
                "debt_or_oath",
                "debt_or_oath",
                "Старт уже отягощён долгом, клятвой или обязательством",
                "в старте уже присутствует обязательство, которое создаёт давление, не отменяя базовые факты",
                "закладывая тяжёлое обязательство в свободный correction slot"),
            new CorrectionTemplate(
                "resource_complication",
                "resource_complication",
                "Старт получает скрытое ресурсное осложнение",
                "в исходной ситуации появляется дефицит, ограничение или перекос ресурсов",
                "усложняя старт, но не переписывая сценарное ядро"),
            new CorrectionTemplate(
                "occult_hidden_layer",
                "hidden_threat",
                "За стартом скрывается невидимая угроза",
                "в старте уже существует скрытый слой угрозы, заговора или метафизического давления",
                "встраивая скрытую угрозу в свободный слой сценария")
        ];
    }

    private static string? ResolveSeverity(ClaimantRuntime claimant, string maxSeverity)
    {
        var maxWeight = AbodePowerRules.GetCorrectionSeverityBudgetCost(maxSeverity);
        if (claimant.RemainingBudget >= 3 &&
            maxWeight >= 3 &&
            string.Equals(claimant.Intent, "hostile", StringComparison.OrdinalIgnoreCase) &&
            (claimant.Reputation <= -51 || claimant.ProjectClaimBonus >= 2))
        {
            return "strong";
        }

        if (claimant.RemainingBudget >= 2 && maxWeight >= 2)
            return "medium";
        if (claimant.RemainingBudget >= 1)
            return "minor";
        return null;
    }

    private static int GetInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var parsedInt))
                return parsedInt;
            if (value.TryGetValue<long>(out var parsedLong) &&
                parsedLong <= int.MaxValue &&
                parsedLong >= int.MinValue)
            {
                return (int)parsedLong;
            }
            if (value.TryGetValue<string>(out var parsedString) && int.TryParse(parsedString, out var parsedFromString))
                return parsedFromString;
        }

        return 0;
    }

    private static string GetString(JsonNode? node)
    {
        if (node is not JsonValue value)
            return string.Empty;

        try
        {
            return value.GetValue<string>() ?? string.Empty;
        }
        catch
        {
            return node.ToJsonString();
        }
    }

    private static JsonElement ToJsonElement(JsonObject node)
    {
        using var doc = JsonDocument.Parse(node.ToJsonString());
        return doc.RootElement.Clone();
    }

    private static ScenarioCoreService.ScenarioCoreAssertion CloneAssertion(ScenarioCoreService.ScenarioCoreAssertion source)
    {
        return new ScenarioCoreService.ScenarioCoreAssertion
        {
            AssertionId = source.AssertionId,
            Category = source.Category,
            Value = source.Value,
            Explicit = source.Explicit,
            Source = source.Source,
            CandidateId = source.CandidateId
        };
    }

    private static ScenarioCoreService.ScenarioCorrectionSlot CloneSlot(ScenarioCoreService.ScenarioCorrectionSlot source)
    {
        return new ScenarioCoreService.ScenarioCorrectionSlot
        {
            SlotId = source.SlotId,
            SlotType = source.SlotType,
            MaxSeverity = source.MaxSeverity,
            AllowsFriendly = source.AllowsFriendly,
            AllowsHostile = source.AllowsHostile,
            SourceAssertionId = source.SourceAssertionId
        };
    }

    private sealed record CorrectionTemplate(
        string SlotType,
        string AffectsStartAs,
        string BaseTitle,
        string BaseSummary,
        string ReasonTail)
    {
        public string GetTitle(string severity) => severity switch
        {
            "strong" => $"{BaseTitle} (сильная корректива)",
            "medium" => $"{BaseTitle} (средняя корректива)",
            _ => $"{BaseTitle} (малая корректива)"
        };

        public string GetSummary(string severity, string guardianName) => severity switch
        {
            "strong" => $"{guardianName} вносит сильную коррективу: {BaseSummary}.",
            "medium" => $"{guardianName} вносит заметную коррективу: {BaseSummary}.",
            _ => $"{guardianName} мягко корректирует старт: {BaseSummary}."
        };

        public string GetReason(string intent, string guardianName)
        {
            var prefix = string.Equals(intent, "friendly", StringComparison.OrdinalIgnoreCase)
                ? $"{guardianName} благожелательно тратит силу Обители"
                : $"{guardianName} враждебно тратит силу Обители";
            return $"{prefix}, {ReasonTail}.";
        }
    }

    private sealed class ClaimantRuntime
    {
        public string GuardianId { get; set; } = "";
        public string GuardianName { get; set; } = "";
        public string Intent { get; set; } = "none";
        public bool IsActivePatron { get; set; }
        public int CurrentPower { get; set; }
        public int PowerAfter { get; set; }
        public int BaseBudget { get; set; }
        public int PreparationBudget { get; set; }
        public int RemainingBudget { get; set; }
        public int RemainingPower { get; set; }
        public int Reputation { get; set; }
        public int PreparationClaimBonus { get; set; }
        public int ProjectClaimBonus { get; set; }
        public int ClaimStrengthBase { get; set; }
        public bool Eligible { get; set; }
        public string SourceSummary { get; set; } = "";
    }
}
