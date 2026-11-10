using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Globalization;
using System.Text;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using Microsoft.Extensions.Logging;

namespace BookOfEternityClient.Services;

public sealed class GuardianCorrectionService
{
    public const string StatePath = "game_state/world/guardian_corrections.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const int CurrentReceiptSchemaVersion = 1;

    private static readonly string[] ReceiptRootScalarProperties =
    [
        "schemaVersion",
        "receiptFingerprint",
        "transactionAfterImageFingerprint",
        "lifeIncarnation",
        "appliedAt",
        "guardianId",
        "guardianName",
        "intent",
        "reputationAtApplication",
        "powerBefore",
        "powerAfter",
        "baseBudgetPoints",
        "remainingBudgetPoints",
        "totalAbodePowerSpent",
        "summary"
    ];

    private static readonly string[] ReceiptRootNestedProperties =
    [
        "transactionAfterImagePaths",
        "scenarioCoreSnapshot",
        "claimants",
        "contestedSlots",
        "resolutionOrder",
        "corrections"
    ];

    private static readonly string[] ReceiptScenarioProperties =
        ["scenarioCoreAssertions", "openCorrectionSlots"];
    private static readonly string[] ReceiptScenarioAssertionProperties =
        ["assertionId", "category", "value", "explicit", "source"];
    private static readonly string[] ReceiptScenarioSlotProperties =
        ["slotId", "slotType", "maxSeverity", "allowsFriendly", "allowsHostile", "sourceAssertionId"];
    private static readonly string[] ReceiptClaimantProperties =
    [
        "guardianId", "guardianName", "intent", "isActivePatron", "currentPower",
        "powerAfter", "baseBudgetPoints", "preparationBudgetPoints", "remainingBudgetPoints",
        "claimStrengthBase", "eligible", "sourceSummary"
    ];
    private static readonly string[] ReceiptContestProperties =
        ["slotId", "slotType", "winnerGuardianId", "winnerGuardianName", "winnerCorrectionId", "candidates"];
    private static readonly string[] ReceiptCandidateProperties =
    [
        "candidateCorrectionId", "sourceGuardianId", "sourceGuardianName", "intent", "severity",
        "budgetCostPoints", "abodePowerCost", "claimStrength", "title"
    ];
    private static readonly string[] ReceiptCorrectionProperties =
    [
        "correctionId", "sourceGuardianId", "sourceGuardianName", "intent", "slotId", "slotType",
        "severity", "budgetCostPoints", "abodePowerCost", "claimStrength", "title", "summary",
        "reason", "affectsStartAs"
    ];

    private static readonly string[] RequiredReceiptTransactionPaths =
    [
        GuardianProjectState.TrackerPath,
        GuardianPowerEventState.JournalPath,
        GuardiansPath,
        ResourceMaterializationContract.StatePath,
        ResourceMaterializationContract.HistoryPath,
        CanonicalResourceOwnerAuthorityComposer.AuthorityPath
    ];

    private static readonly HashSet<string> AllowedReceiptTransactionPaths = new(
        RequiredReceiptTransactionPaths.Concat(
        [
            AfterlifeEntityProfileState.StatePath,
            AfterlifeSpiritualConflictState.StatePath,
            "game_state/meta/soul_state.json",
            ShiningAbodeState.StatePath
        ]),
        StringComparer.Ordinal);

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
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; set; } = CurrentReceiptSchemaVersion;

        [JsonPropertyName("receiptFingerprint")]
        public string ReceiptFingerprint { get; set; } = "";

        [JsonPropertyName("transactionAfterImageFingerprint")]
        public string TransactionAfterImageFingerprint { get; set; } = "";

        [JsonPropertyName("transactionAfterImagePaths")]
        public List<string> TransactionAfterImagePaths { get; set; } = new();

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

    public async Task ApplyForNewLifeAsync(int lifeIncarnation, int turnNumber)
    {
        if (turnNumber <= 0)
            throw new ArgumentOutOfRangeException(nameof(turnNumber));
        var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? owningPublicationUncertainty = null;
        try
        {
            await ApplyForNewLifeAsync(lifeIncarnation, turnNumber, writeLease);
        }
        catch (CoordinatedStatePublicationUncertainException failure) { owningPublicationUncertainty = failure; throw; }
        finally { await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(_fs, writeLease, false, owningPublicationUncertainty); }
    }

    private async Task ApplyForNewLifeAsync(
        int lifeIncarnation,
        int turnNumber,
        FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var correctionBefore = await _fs.ReadFileAsync(writeLease, StatePath);
        var replay = ResolveCorrectionReplayStatus(
            correctionBefore,
            lifeIncarnation);
        if (replay.Status == CorrectionReplayStatus.Malformed)
        {
            throw new InvalidDataException(
                $"{replay.FailureCode}: {StatePath} cannot authorize Guardian correction replay.");
        }

        var guardiansRaw = await _fs.ReadFileAsync(
            writeLease,
            GuardiansPath);
        if (replay.Status == CorrectionReplayStatus.SameLife)
        {
            var replayReceipt = replay.Receipt ?? throw new InvalidDataException(
                "guardian_correction_receipt_root_invalid: same-life replay requires a parsed receipt.");
            if (string.IsNullOrWhiteSpace(guardiansRaw) ||
                JsonNode.Parse(guardiansRaw) is not JsonObject replayGuardians ||
                replayGuardians["activeGuardian"] is not JsonObject replayActiveGuardian)
            {
                throw new InvalidDataException(
                    "Previously applied Guardian correction requires its exact Guardian owner state.");
            }
            var replayGuardianId = GetString(replayActiveGuardian["guardianId"]);
            if (!string.Equals(
                    replayReceipt.GuardianId,
                    replayGuardianId,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "guardian_correction_receipt_guardian_mismatch: receipt guardianId does not match activeGuardian.guardianId.");
            }
            var replayPlan = await BuildExistingGuardianQuartetPlanAsync(
                writeLease,
                replayGuardians,
                turnNumber);
            var sealedPaths = replayReceipt.TransactionAfterImagePaths
                .ToHashSet(StringComparer.Ordinal);
            if (replayPlan.OwnerAfterImages.Keys.Any(path =>
                    !sealedPaths.Contains(path)))
            {
                throw new InvalidDataException(
                    "guardian_correction_receipt_after_image_mismatch: the current Guardian resource plan publishes an owner companion outside the sealed transaction manifest.");
            }
            var currentAfterImageFingerprint =
                await ComputeCurrentTransactionAfterImageFingerprintAsync(
                    writeLease,
                    replayReceipt.TransactionAfterImagePaths);
            if (!string.Equals(
                    replayReceipt.TransactionAfterImageFingerprint,
                    currentAfterImageFingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "guardian_correction_receipt_after_image_mismatch: receipt does not authorize the current Guardian correction transaction after-image.");
            }

            return;
        }

        var scenario = await _scenarioCoreService.ReadAsync(writeLease);
        if (scenario == null)
        {
            _fs.DeleteFile(writeLease, StatePath);
            return;
        }

        if (string.IsNullOrWhiteSpace(guardiansRaw))
        {
            _fs.DeleteFile(writeLease, StatePath);
            return;
        }

        var guardiansRoot = JsonNode.Parse(guardiansRaw) as JsonObject;
        var activeGuardian = guardiansRoot?["activeGuardian"] as JsonObject;
        if (guardiansRoot == null || activeGuardian == null)
        {
            _fs.DeleteFile(writeLease, StatePath);
            return;
        }

        var guardianId = GetString(activeGuardian["guardianId"]);
        if (string.IsNullOrWhiteSpace(guardianId))
        {
            _fs.DeleteFile(writeLease, StatePath);
            return;
        }

        var guardianName = GuardianManifestation.GetDisplayName(ToJsonElement(activeGuardian));
        var reputation = GuardianGachaChargeRules.ResolveGuardianReputation(activeGuardian);
        var trackerBefore = await _fs.ReadFileAsync(
            writeLease,
            GuardianProjectState.TrackerPath);
        var journalBefore = await _fs.ReadFileAsync(
            writeLease,
            GuardianPowerEventState.JournalPath);
        var trackerRoot = ParseTrackerRoot(trackerBefore);
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
            await CommitCorrectionAsync(
                writeLease,
                turnNumber,
                guardiansRoot,
                correctionBefore,
                state,
                trackerBefore,
                trackerAfter: null,
                journalBefore,
                journalAfter: null);
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
        ApplyCorrectionRelationshipEffects(
            guardiansRoot,
            claimants,
            corrections,
            contestedSlots);

        var powerJournalEntries = new List<JsonObject>();
        if (state.TotalAbodePowerSpent > 0)
        {
            GuardianPowerEventState.ApplyEvents(
                guardiansRoot,
                corrections.Select(correction =>
                    BuildPowerSpendEvent(correction, lifeIncarnation)),
                turnNumber,
                powerJournalEntries);
        }

        var journalAfter = powerJournalEntries.Count == 0
            ? null
            : await GuardianPowerEventState.BuildJournalUpdateAsync(
                _fs,
                writeLease,
                powerJournalEntries,
                GuardianPowerJournalMutationMode.RepairAndAppend);

        await CommitCorrectionAsync(
            writeLease,
            turnNumber,
            guardiansRoot,
            correctionBefore,
            state,
            trackerBefore,
            trackerChanged && trackerRoot != null
                ? trackerRoot.ToJsonString(JsonOpts)
                : null,
            journalBefore,
            journalAfter);
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

    private JsonObject? ParseTrackerRoot(string? trackerRaw)
    {
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

    private async Task CommitCorrectionAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        int turnNumber,
        JsonObject guardiansAfter,
        string? correctionBefore,
        GuardianCorrectionsState correctionAfter,
        string? trackerBefore,
        string? trackerAfter,
        string? journalBefore,
        string? journalAfter)
    {
        correctionAfter.SchemaVersion = CurrentReceiptSchemaVersion;
        var resourcePlan = await AfterlifeOwnerResourceStateService.BuildAsync(
            _fs,
            writeLease,
            new AfterlifeOwnerResourceAcceptedState(Guardians: guardiansAfter),
            turnNumber);
        if (!resourcePlan.IsValid)
        {
            throw new InvalidDataException(
                "Guardian correction cannot publish against the canonical resource quartet: " +
                string.Join(
                    "; ",
                    resourcePlan.Issues.Select(static issue =>
                        issue.Code ?? issue.Message)));
        }

        var transactionAuthority =
            await BuildExpectedTransactionAfterImageAuthorityAsync(
                writeLease,
                resourcePlan,
                trackerBefore,
                trackerAfter,
                journalBefore,
                journalAfter);
        correctionAfter.TransactionAfterImagePaths =
            transactionAuthority.Paths.ToList();
        correctionAfter.TransactionAfterImageFingerprint =
            ComputeTransactionAfterImageFingerprint(
                transactionAuthority.Paths,
                transactionAuthority.AfterImages);
        correctionAfter.ReceiptFingerprint =
            ComputeCorrectionReceiptFingerprint(correctionAfter);
        if (!IsSemanticallyValidCorrectionReceipt(correctionAfter))
        {
            throw new InvalidDataException(
                "guardian_correction_receipt_semantic_invalid: generated Guardian correction receipt is not internally coherent.");
        }

        var additionalWrites = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                StatePath,
                correctionBefore,
                JsonSerializer.Serialize(correctionAfter, JsonOpts),
                RequireCurrentBaseline: true)
        };
        additionalWrites.AddRange(transactionAuthority.CoordinatedWrites);

        if (!await AfterlifeOwnerResourceStateService.TryCommitAsync(
                _fs,
                writeLease,
                resourcePlan,
                additionalWrites.ToArray()))
        {
            throw new IOException(
                "Guardian correction owner state or canonical resource quartet changed before atomic publication.");
        }
    }

    private async Task<AfterlifeOwnerResourceStateFilePlan>
        BuildExistingGuardianQuartetPlanAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject guardians,
        int turnNumber)
    {
        var plan = await AfterlifeOwnerResourceStateService.BuildAsync(
            _fs,
            writeLease,
            new AfterlifeOwnerResourceAcceptedState(Guardians: guardians),
            turnNumber);
        if (plan.IsValid)
            return plan;

        throw new InvalidDataException(
            "Previously applied Guardian correction has invalid canonical resource authority: " +
            string.Join(
                "; ",
                plan.Issues.Select(static issue => issue.Code ?? issue.Message)));
    }

    private async Task<CorrectionTransactionAfterImageAuthority>
        BuildExpectedTransactionAfterImageAuthorityAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        AfterlifeOwnerResourceStateFilePlan resourcePlan,
        string? trackerBefore,
        string? trackerAfter,
        string? journalBefore,
        string? journalAfter)
    {
        if (resourcePlan.StateAfterImage == null ||
            resourcePlan.HistoryAfterImage == null ||
            resourcePlan.QuartetProjection == null ||
            !resourcePlan.OwnerAfterImages.ContainsKey(GuardiansPath))
        {
            throw new InvalidDataException(
                "Guardian correction cannot fingerprint an incomplete transaction after-image.");
        }

        var afterImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal)
        {
            [GuardianProjectState.TrackerPath] = trackerAfter == null
                ? await _fs.ReadFileBytesAsync(
                    writeLease,
                    GuardianProjectState.TrackerPath)
                : EncodeUtf8WithPreamble(trackerAfter),
            [GuardianPowerEventState.JournalPath] = journalAfter == null
                ? await _fs.ReadFileBytesAsync(
                    writeLease,
                    GuardianPowerEventState.JournalPath)
                : EncodeUtf8WithPreamble(journalAfter)
        };
        var coordinatedWrites = new[]
        {
            trackerAfter == null
                ? CoordinatedStateWriteHelper.CreateGuardWrite(
                    GuardianProjectState.TrackerPath,
                    trackerBefore)
                : new CoordinatedStateWriteHelper.PlannedWrite(
                    GuardianProjectState.TrackerPath,
                    trackerBefore,
                    trackerAfter,
                    RequireCurrentBaseline: true),
            journalAfter == null
                ? CoordinatedStateWriteHelper.CreateGuardWrite(
                    GuardianPowerEventState.JournalPath,
                    journalBefore)
                : new CoordinatedStateWriteHelper.PlannedWrite(
                    GuardianPowerEventState.JournalPath,
                    journalBefore,
                    journalAfter,
                    RequireCurrentBaseline: true)
        };

        foreach (var (path, ownerAfterImage) in resourcePlan.OwnerAfterImages)
        {
            if (!AllowedReceiptTransactionPaths.Contains(path) ||
                !afterImages.TryAdd(
                    path,
                    EncodeUtf8WithPreamble(
                        ownerAfterImage.ToJsonString(JsonOpts))))
            {
                throw new InvalidDataException(
                    $"Guardian correction cannot seal invalid or duplicate owner transaction path '{path}'.");
            }
        }

        afterImages[ResourceMaterializationContract.StatePath] =
            EncodeUtf8WithPreamble(
                resourcePlan.StateAfterImage.ToCanonicalJson());
        afterImages[ResourceMaterializationContract.HistoryPath] =
            EncodeUtf8WithPreamble(
                resourcePlan.HistoryAfterImage.ToCanonicalJson());

        var authorityAfter = resourcePlan.QuartetProjection.AuthorityAfterImage;
        var authorityBefore = resourcePlan.BeforeImages[
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath];
        afterImages[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
            JsonEquivalent(authorityBefore, authorityAfter)
                ? await _fs.ReadFileBytesAsync(
                    writeLease,
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath)
                : EncodeUtf8WithPreamble(authorityAfter);

        var paths = afterImages.Keys
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        if (!IsValidTransactionAfterImageManifest(paths))
        {
            throw new InvalidDataException(
                "Guardian correction generated an invalid transaction after-image manifest.");
        }

        return new CorrectionTransactionAfterImageAuthority(
            paths,
            afterImages,
            coordinatedWrites);
    }

    private async Task<string> ComputeCurrentTransactionAfterImageFingerprintAsync(
        FileSystemManager.CanonicalWriteLease writeLease,
        IReadOnlyList<string> paths)
    {
        var afterImages = new Dictionary<string, byte[]?>(StringComparer.Ordinal);
        foreach (var path in paths)
            afterImages[path] = await _fs.ReadFileBytesAsync(writeLease, path);
        return ComputeTransactionAfterImageFingerprint(paths, afterImages);
    }

    private static string ComputeTransactionAfterImageFingerprint(
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, byte[]?> afterImages)
    {
        using var fingerprint = new ResourceFingerprintBuilder(
            "guardian-correction-transaction-after-images-v1");
        foreach (var path in paths)
        {
            fingerprint.Append(path);
            var bytes = afterImages[path];
            fingerprint.Append(bytes != null);
            if (bytes != null)
                fingerprint.Append(Convert.ToBase64String(bytes));
        }
        return fingerprint.Build();
    }

    private static byte[] EncodeUtf8WithPreamble(string content)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var body = Encoding.UTF8.GetBytes(content);
        var result = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, result, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, result, preamble.Length, body.Length);
        return result;
    }

    private static bool JsonEquivalent(string? left, string right)
    {
        if (left == null)
            return false;
        try
        {
            return JsonNode.DeepEquals(JsonNode.Parse(left), JsonNode.Parse(right));
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static CorrectionReplayResolution ResolveCorrectionReplayStatus(
        string? correctionJson,
        int lifeIncarnation)
    {
        if (string.IsNullOrWhiteSpace(correctionJson))
        {
            return new CorrectionReplayResolution(
                CorrectionReplayStatus.NotApplied,
                Receipt: null,
                FailureCode: null);
        }
        if (!TryParseStrictCorrectionReceipt(
                correctionJson,
                out var existing,
                out var failureCode))
        {
            return new CorrectionReplayResolution(
                CorrectionReplayStatus.Malformed,
                Receipt: null,
                failureCode);
        }

        return new CorrectionReplayResolution(
            existing!.LifeIncarnation == lifeIncarnation
                ? CorrectionReplayStatus.SameLife
                : CorrectionReplayStatus.NotApplied,
            existing,
            FailureCode: null);
    }

    private static bool TryParseStrictCorrectionReceipt(
        string correctionJson,
        out GuardianCorrectionsState? receipt,
        out string failureCode)
    {
        receipt = null;
        failureCode = "guardian_correction_receipt_root_invalid";
        try
        {
            using var document = JsonDocument.Parse(correctionJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var duplicateIssues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                StatePath,
                duplicateIssues,
                "guardian_correction_receipt_duplicate_property");
            if (duplicateIssues.Count != 0)
            {
                failureCode = "guardian_correction_receipt_duplicate_property";
                return false;
            }

            var root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            if (!HasEveryProperty(root, ReceiptRootScalarProperties))
            {
                failureCode = "guardian_correction_receipt_root_incomplete";
                return false;
            }
            if (!HasEveryProperty(root, ReceiptRootNestedProperties))
            {
                failureCode = "guardian_correction_receipt_nested_incomplete";
                return false;
            }
            if (root.Count != ReceiptRootScalarProperties.Length +
                              ReceiptRootNestedProperties.Length)
            {
                failureCode = "guardian_correction_receipt_root_incomplete";
                return false;
            }

            failureCode = ValidateCorrectionReceiptNestedShape(root);
            if (!string.IsNullOrEmpty(failureCode))
                return false;

            receipt = JsonSerializer.Deserialize<GuardianCorrectionsState>(
                document.RootElement.GetRawText(),
                JsonOpts);
            if (receipt == null)
            {
                failureCode = "guardian_correction_receipt_nested_invalid";
                return false;
            }
            if (!IsValidTransactionAfterImageManifest(
                    receipt.TransactionAfterImagePaths))
            {
                receipt = null;
                failureCode = "guardian_correction_receipt_manifest_invalid";
                return false;
            }
            if (!IsSemanticallyValidCorrectionReceipt(receipt))
            {
                receipt = null;
                failureCode = "guardian_correction_receipt_semantic_invalid";
                return false;
            }
            if (receipt.SchemaVersion != CurrentReceiptSchemaVersion)
            {
                receipt = null;
                failureCode = "guardian_correction_receipt_schema_unsupported";
                return false;
            }
            if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                    receipt.ReceiptFingerprint) ||
                !string.Equals(
                    receipt.ReceiptFingerprint,
                    ComputeCorrectionReceiptFingerprint(receipt),
                    StringComparison.Ordinal))
            {
                receipt = null;
                failureCode = "guardian_correction_receipt_fingerprint_invalid";
                return false;
            }
            failureCode = string.Empty;
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or InvalidOperationException)
        {
            receipt = null;
            if (failureCode == "guardian_correction_receipt_root_invalid")
                return false;
            failureCode = "guardian_correction_receipt_nested_invalid";
            return false;
        }
    }

    private static string ValidateCorrectionReceiptNestedShape(JsonObject root)
    {
        var issue = ValidateStringArray(root["transactionAfterImagePaths"]);
        if (issue != null)
            return issue;
        if (root["scenarioCoreSnapshot"] is not JsonObject scenario)
            return "guardian_correction_receipt_nested_invalid";
        if (!HasExactProperties(scenario, ReceiptScenarioProperties))
            return "guardian_correction_receipt_nested_incomplete";
        issue = ValidateObjectArray(
            scenario["scenarioCoreAssertions"],
            ReceiptScenarioAssertionProperties,
            optionalProperty: "candidateId");
        if (issue != null)
            return issue;
        issue = ValidateObjectArray(
            scenario["openCorrectionSlots"],
            ReceiptScenarioSlotProperties);
        if (issue != null)
            return issue;
        issue = ValidateObjectArray(root["claimants"], ReceiptClaimantProperties);
        if (issue != null)
            return issue;
        issue = ValidateObjectArray(
            root["contestedSlots"],
            ReceiptContestProperties,
            nestedArrayProperty: "candidates",
            nestedElementProperties: ReceiptCandidateProperties);
        if (issue != null)
            return issue;
        issue = ValidateStringArray(root["resolutionOrder"]);
        if (issue != null)
            return issue;
        return ValidateObjectArray(root["corrections"], ReceiptCorrectionProperties)
               ?? string.Empty;
    }

    private static string? ValidateObjectArray(
        JsonNode? node,
        IReadOnlyCollection<string> requiredProperties,
        string? optionalProperty = null,
        string? nestedArrayProperty = null,
        IReadOnlyCollection<string>? nestedElementProperties = null)
    {
        if (node is not JsonArray array)
            return "guardian_correction_receipt_nested_invalid";
        foreach (var item in array)
        {
            if (item is not JsonObject itemObject)
                return "guardian_correction_receipt_nested_invalid";
            if (!HasExactProperties(
                    itemObject,
                    requiredProperties,
                    optionalProperty))
            {
                return "guardian_correction_receipt_nested_incomplete";
            }
            if (nestedArrayProperty != null)
            {
                var nestedIssue = ValidateObjectArray(
                    itemObject[nestedArrayProperty],
                    nestedElementProperties!);
                if (nestedIssue != null)
                    return nestedIssue;
            }
        }
        return null;
    }

    private static string? ValidateStringArray(JsonNode? node)
    {
        if (node is not JsonArray array ||
            array.Any(item =>
                item is not JsonValue value ||
                !value.TryGetValue<string>(out var text) ||
                string.IsNullOrWhiteSpace(text)))
        {
            return "guardian_correction_receipt_nested_invalid";
        }
        return null;
    }

    private static bool HasEveryProperty(
        JsonObject root,
        IEnumerable<string> properties) =>
        properties.All(root.ContainsKey);

    private static bool HasExactProperties(
        JsonObject root,
        IReadOnlyCollection<string> requiredProperties,
        string? optionalProperty = null)
    {
        if (!requiredProperties.All(root.ContainsKey))
            return false;
        var expectedCount = requiredProperties.Count +
                            (optionalProperty != null && root.ContainsKey(optionalProperty)
                                ? 1
                                : 0);
        return root.Count == expectedCount;
    }

    private static bool IsSemanticallyValidCorrectionReceipt(
        GuardianCorrectionsState receipt)
    {
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(
                receipt.TransactionAfterImageFingerprint) ||
            !IsValidTransactionAfterImageManifest(
                receipt.TransactionAfterImagePaths) ||
            receipt.LifeIncarnation <= 0 ||
            !DateTimeOffset.TryParseExact(
                receipt.AppliedAt,
                "O",
                CultureInfo.InvariantCulture,
                DateTimeStyles.RoundtripKind,
                out _) ||
            !ResourceMaterializationContract.IsExactIdentifier(receipt.GuardianId) ||
            string.IsNullOrWhiteSpace(receipt.GuardianName) ||
            receipt.Intent is not ("friendly" or "hostile" or "none") ||
            receipt.ReputationAtApplication is < -100 or > 300 ||
            !string.Equals(
                receipt.Intent,
                ResolveIntent(receipt.ReputationAtApplication),
                StringComparison.Ordinal) ||
            receipt.PowerBefore is < AbodePowerRules.MinPower or > AbodePowerRules.MaxPower ||
            receipt.PowerAfter is < AbodePowerRules.MinPower or > AbodePowerRules.MaxPower ||
            receipt.BaseBudgetPoints < 0 ||
            receipt.RemainingBudgetPoints < 0 ||
            receipt.TotalAbodePowerSpent < 0 ||
            string.IsNullOrWhiteSpace(receipt.Summary) ||
            receipt.ScenarioCoreSnapshot == null ||
            receipt.Claimants == null ||
            receipt.ContestedSlots == null ||
            receipt.ResolutionOrder == null ||
            receipt.Corrections == null)
        {
            return false;
        }

        var assertions = receipt.ScenarioCoreSnapshot.ScenarioCoreAssertions;
        var slots = receipt.ScenarioCoreSnapshot.OpenCorrectionSlots;
        if (assertions == null ||
            slots == null ||
            assertions.Any(static assertion => !IsValidScenarioAssertion(assertion)) ||
            slots.Any(static slot => !IsValidScenarioSlot(slot)) ||
            HasDuplicateValues(
                assertions.Select(static assertion => assertion.AssertionId)) ||
            HasDuplicateValues(slots.Select(static slot => slot.SlotId)))
        {
            return false;
        }

        var assertionIds = assertions
            .Select(static assertion => assertion.AssertionId)
            .ToHashSet(StringComparer.Ordinal);
        if (slots.Any(slot => !assertionIds.Contains(slot.SourceAssertionId)) ||
            receipt.Claimants.Any(static claimant => !IsValidClaimant(claimant)) ||
            receipt.ContestedSlots.Any(static contest => !IsValidContest(contest)) ||
            receipt.Corrections.Any(static correction => !IsValidCorrection(correction)) ||
            HasDuplicateValues(
                receipt.Claimants.Select(static claimant => claimant.GuardianId)) ||
            HasDuplicateValues(
                receipt.ContestedSlots.Select(static contest => contest.SlotId)) ||
            HasDuplicateValues(
                receipt.Corrections.Select(static correction => correction.CorrectionId)) ||
            receipt.ResolutionOrder.Any(static step => !IsCanonicalText(step)))
        {
            return false;
        }

        var slotById = slots.ToDictionary(
            static slot => slot.SlotId,
            StringComparer.Ordinal);
        var claimantById = receipt.Claimants.ToDictionary(
            static claimant => claimant.GuardianId,
            StringComparer.Ordinal);
        var allCandidates = receipt.ContestedSlots
            .SelectMany(static contest => contest.Candidates)
            .ToList();
        if (HasDuplicateValues(allCandidates.Select(
                static candidate => candidate.CandidateCorrectionId)))
        {
            return false;
        }

        var selectionCandidates = new List<CorrectionSelectionCandidate>();
        foreach (var contest in receipt.ContestedSlots)
        {
            if (!slotById.TryGetValue(contest.SlotId, out var slot) ||
                !string.Equals(
                    contest.SlotType,
                    slot.SlotType,
                    StringComparison.Ordinal))
            {
                return false;
            }

            foreach (var candidate in contest.Candidates)
            {
                if (!claimantById.TryGetValue(
                        candidate.SourceGuardianId,
                        out var claimant) ||
                    !string.Equals(
                        candidate.SourceGuardianName,
                        claimant.GuardianName,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.Intent,
                        claimant.Intent,
                        StringComparison.Ordinal) ||
                    !string.Equals(
                        candidate.CandidateCorrectionId,
                        BuildCorrectionId(
                            candidate.SourceGuardianId,
                            contest.SlotId,
                            candidate.Intent,
                            candidate.Severity),
                        StringComparison.Ordinal) ||
                    !SlotAllowsIntent(slot, candidate.Intent) ||
                    !SeverityFitsSlot(
                        candidate.Severity,
                        slot.MaxSeverity) ||
                    (long)candidate.ClaimStrength !=
                        (long)claimant.ClaimStrengthBase +
                        candidate.BudgetCostPoints ||
                    !TryBuildCanonicalSelectionCandidate(
                        contest,
                        candidate,
                        out var selectionCandidate))
                {
                    return false;
                }

                selectionCandidates.Add(selectionCandidate);
            }
        }

        var selection = SelectDeterministicCorrections(
            receipt.Claimants.Select(static claimant =>
                new CorrectionSelectionClaimant(
                    claimant.GuardianId,
                    claimant.IsActivePatron,
                    claimant.CurrentPower,
                    claimant.BaseBudgetPoints +
                        claimant.PreparationBudgetPoints))
                .ToArray(),
            selectionCandidates);
        if (selection.Contests.Count != receipt.ContestedSlots.Count ||
            !selection.ResolutionOrder.SequenceEqual(
                receipt.ResolutionOrder,
                StringComparer.Ordinal) ||
            selection.Winners.Count != receipt.Corrections.Count ||
            receipt.TotalAbodePowerSpent !=
                selection.Winners.Sum(static winner =>
                    (long)winner.AbodePowerCost))
        {
            return false;
        }

        for (var index = 0; index < selection.Contests.Count; index++)
        {
            if (!SelectionContestMatchesReceipt(
                    selection.Contests[index],
                    receipt.ContestedSlots[index]))
            {
                return false;
            }
        }

        for (var index = 0; index < selection.Winners.Count; index++)
        {
            if (!SelectionCandidateMatchesCorrection(
                    selection.Winners[index],
                    receipt.Corrections[index]))
            {
                return false;
            }
        }

        foreach (var claimant in receipt.Claimants)
        {
            var finalBalance = selection.FinalBalances[claimant.GuardianId];
            if (claimant.RemainingBudgetPoints !=
                    finalBalance.RemainingBudget ||
                claimant.PowerAfter != finalBalance.RemainingPower)
            {
                return false;
            }
        }

        var activeClaimants = receipt.Claimants
            .Where(static claimant => claimant.IsActivePatron)
            .ToList();
        return activeClaimants.Count <= 1 &&
               (activeClaimants.Count == 0
                   ? receipt.PowerAfter == receipt.PowerBefore &&
                     receipt.RemainingBudgetPoints == receipt.BaseBudgetPoints
                   :
                (string.Equals(
                     activeClaimants[0].GuardianId,
                     receipt.GuardianId,
                     StringComparison.Ordinal) &&
                 string.Equals(
                     activeClaimants[0].GuardianName,
                     receipt.GuardianName,
                     StringComparison.Ordinal) &&
                 string.Equals(
                     activeClaimants[0].Intent,
                     receipt.Intent,
                     StringComparison.Ordinal) &&
                 activeClaimants[0].CurrentPower == receipt.PowerBefore &&
                 activeClaimants[0].PowerAfter == receipt.PowerAfter &&
                 activeClaimants[0].BaseBudgetPoints == receipt.BaseBudgetPoints &&
                 activeClaimants[0].RemainingBudgetPoints == receipt.RemainingBudgetPoints));
    }

    private static bool IsValidScenarioAssertion(
        ScenarioCoreService.ScenarioCoreAssertion assertion) =>
        assertion != null &&
        ResourceMaterializationContract.IsExactIdentifier(assertion.AssertionId) &&
        ResourceMaterializationContract.IsExactIdentifier(assertion.Category) &&
        IsCanonicalText(assertion.Value) &&
        assertion.Explicit &&
        ResourceMaterializationContract.IsExactIdentifier(assertion.Source) &&
        (assertion.CandidateId == null ||
         ResourceMaterializationContract.IsExactIdentifier(assertion.CandidateId));

    private static bool IsValidScenarioSlot(
        ScenarioCoreService.ScenarioCorrectionSlot slot) =>
        slot != null &&
        ResourceMaterializationContract.IsExactIdentifier(slot.SlotId) &&
        ResourceMaterializationContract.IsExactIdentifier(slot.SlotType) &&
        IsAllowedSeverity(slot.MaxSeverity) &&
        (slot.AllowsFriendly || slot.AllowsHostile) &&
        ResourceMaterializationContract.IsExactIdentifier(slot.SourceAssertionId);

    private static bool IsValidClaimant(GuardianCorrectionClaimant claimant) =>
        claimant != null &&
        ResourceMaterializationContract.IsExactIdentifier(claimant.GuardianId) &&
        IsCanonicalText(claimant.GuardianName) &&
        IsCorrectionIntent(claimant.Intent) &&
        claimant.CurrentPower is >= AbodePowerRules.MinPower and <= AbodePowerRules.MaxPower &&
        claimant.PowerAfter is >= AbodePowerRules.MinPower and <= AbodePowerRules.MaxPower &&
        claimant.PowerAfter <= claimant.CurrentPower &&
        claimant.BaseBudgetPoints >= 0 &&
        claimant.PreparationBudgetPoints >= 0 &&
        (long)claimant.BaseBudgetPoints + claimant.PreparationBudgetPoints <=
            int.MaxValue &&
        claimant.RemainingBudgetPoints >= 0 &&
        claimant.RemainingBudgetPoints <=
            claimant.BaseBudgetPoints + claimant.PreparationBudgetPoints &&
        claimant.ClaimStrengthBase >= 0 &&
        claimant.Eligible &&
        IsCanonicalText(claimant.SourceSummary);

    private static bool IsValidContest(GuardianCorrectionContest contest)
    {
        if (contest == null ||
            !ResourceMaterializationContract.IsExactIdentifier(contest.SlotId) ||
            !ResourceMaterializationContract.IsExactIdentifier(contest.SlotType) ||
            contest.WinnerGuardianId == null ||
            contest.WinnerGuardianName == null ||
            contest.WinnerCorrectionId == null ||
            contest.Candidates == null ||
            contest.Candidates.Count == 0 ||
            contest.Candidates.Any(static candidate => !IsValidCandidate(candidate)) ||
            HasDuplicateValues(contest.Candidates.Select(
                static candidate => candidate.CandidateCorrectionId)))
        {
            return false;
        }

        var hasWinner = contest.WinnerCorrectionId.Length != 0;
        if (!hasWinner)
        {
            return contest.WinnerGuardianId.Length == 0 &&
                   contest.WinnerGuardianName.Length == 0;
        }

        if (!ResourceMaterializationContract.IsExactIdentifier(
                contest.WinnerCorrectionId) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                contest.WinnerGuardianId) ||
            !IsCanonicalText(contest.WinnerGuardianName))
        {
            return false;
        }

        return contest.Candidates.Any(candidate =>
            string.Equals(
                candidate.CandidateCorrectionId,
                contest.WinnerCorrectionId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.SourceGuardianId,
                contest.WinnerGuardianId,
                StringComparison.Ordinal) &&
            string.Equals(
                candidate.SourceGuardianName,
                contest.WinnerGuardianName,
                StringComparison.Ordinal));
    }

    private static bool IsValidCandidate(GuardianCorrectionCandidate candidate) =>
        candidate != null &&
        ResourceMaterializationContract.IsExactIdentifier(
            candidate.CandidateCorrectionId) &&
        ResourceMaterializationContract.IsExactIdentifier(candidate.SourceGuardianId) &&
        IsCanonicalText(candidate.SourceGuardianName) &&
        IsCorrectionIntent(candidate.Intent) &&
        IsAllowedSeverity(candidate.Severity) &&
        HasCanonicalCorrectionCosts(
            candidate.Severity,
            candidate.BudgetCostPoints,
            candidate.AbodePowerCost) &&
        candidate.ClaimStrength >= 0 &&
        IsCanonicalText(candidate.Title);

    private static bool IsValidCorrection(GuardianCorrectionEntry correction) =>
        correction != null &&
        ResourceMaterializationContract.IsExactIdentifier(correction.CorrectionId) &&
        ResourceMaterializationContract.IsExactIdentifier(correction.SourceGuardianId) &&
        IsCanonicalText(correction.SourceGuardianName) &&
        IsCorrectionIntent(correction.Intent) &&
        ResourceMaterializationContract.IsExactIdentifier(correction.SlotId) &&
        ResourceMaterializationContract.IsExactIdentifier(correction.SlotType) &&
        IsAllowedSeverity(correction.Severity) &&
        HasCanonicalCorrectionCosts(
            correction.Severity,
            correction.BudgetCostPoints,
            correction.AbodePowerCost) &&
        correction.ClaimStrength >= 0 &&
        IsCanonicalText(correction.Title) &&
        IsCanonicalText(correction.Summary) &&
        IsCanonicalText(correction.Reason) &&
        ResourceMaterializationContract.IsExactIdentifier(correction.AffectsStartAs);

    private static bool TryBuildCanonicalSelectionCandidate(
        GuardianCorrectionContest contest,
        GuardianCorrectionCandidate candidate,
        out CorrectionSelectionCandidate selectionCandidate)
    {
        selectionCandidate = null!;
        var matchingTemplates = GetTemplates(candidate.Intent)
            .Where(template => string.Equals(
                template.SlotType,
                contest.SlotType,
                StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (matchingTemplates.Count != 1)
            return false;
        var template = matchingTemplates[0];
        if (!string.Equals(
                candidate.Title,
                template.GetTitle(candidate.Severity),
                StringComparison.Ordinal))
        {
            return false;
        }

        selectionCandidate = new CorrectionSelectionCandidate(
            candidate.CandidateCorrectionId,
            candidate.SourceGuardianId,
            candidate.SourceGuardianName,
            candidate.Intent,
            contest.SlotId,
            contest.SlotType,
            candidate.Severity,
            candidate.BudgetCostPoints,
            candidate.AbodePowerCost,
            candidate.ClaimStrength,
            candidate.Title,
            template.GetSummary(
                candidate.Severity,
                candidate.SourceGuardianName),
            template.GetReason(
                candidate.Intent,
                candidate.SourceGuardianName),
            template.AffectsStartAs);
        return true;
    }

    private static bool SelectionContestMatchesReceipt(
        CorrectionSelectionContest expected,
        GuardianCorrectionContest actual)
    {
        if (!string.Equals(expected.SlotId, actual.SlotId, StringComparison.Ordinal) ||
            !string.Equals(expected.SlotType, actual.SlotType, StringComparison.Ordinal) ||
            !string.Equals(
                expected.WinnerGuardianId,
                actual.WinnerGuardianId,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.WinnerGuardianName,
                actual.WinnerGuardianName,
                StringComparison.Ordinal) ||
            !string.Equals(
                expected.WinnerCorrectionId,
                actual.WinnerCorrectionId,
                StringComparison.Ordinal) ||
            expected.Candidates.Count != actual.Candidates.Count)
        {
            return false;
        }

        for (var index = 0; index < expected.Candidates.Count; index++)
        {
            if (!SelectionCandidateMatchesReceiptCandidate(
                    expected.Candidates[index],
                    actual.Candidates[index]))
            {
                return false;
            }
        }

        return true;
    }

    private static bool SelectionCandidateMatchesReceiptCandidate(
        CorrectionSelectionCandidate expected,
        GuardianCorrectionCandidate actual) =>
        string.Equals(
            expected.CorrectionId,
            actual.CandidateCorrectionId,
            StringComparison.Ordinal) &&
        string.Equals(
            expected.SourceGuardianId,
            actual.SourceGuardianId,
            StringComparison.Ordinal) &&
        string.Equals(
            expected.SourceGuardianName,
            actual.SourceGuardianName,
            StringComparison.Ordinal) &&
        string.Equals(expected.Intent, actual.Intent, StringComparison.Ordinal) &&
        string.Equals(
            expected.Severity,
            actual.Severity,
            StringComparison.Ordinal) &&
        expected.BudgetCostPoints == actual.BudgetCostPoints &&
        expected.AbodePowerCost == actual.AbodePowerCost &&
        expected.ClaimStrength == actual.ClaimStrength &&
        string.Equals(expected.Title, actual.Title, StringComparison.Ordinal);

    private static bool SelectionCandidateMatchesCorrection(
        CorrectionSelectionCandidate expected,
        GuardianCorrectionEntry actual) =>
        string.Equals(expected.CorrectionId, actual.CorrectionId, StringComparison.Ordinal) &&
        string.Equals(expected.SourceGuardianId, actual.SourceGuardianId, StringComparison.Ordinal) &&
        string.Equals(expected.SourceGuardianName, actual.SourceGuardianName, StringComparison.Ordinal) &&
        string.Equals(expected.Intent, actual.Intent, StringComparison.Ordinal) &&
        string.Equals(expected.SlotId, actual.SlotId, StringComparison.Ordinal) &&
        string.Equals(expected.SlotType, actual.SlotType, StringComparison.Ordinal) &&
        string.Equals(expected.Severity, actual.Severity, StringComparison.Ordinal) &&
        expected.BudgetCostPoints == actual.BudgetCostPoints &&
        expected.AbodePowerCost == actual.AbodePowerCost &&
        expected.ClaimStrength == actual.ClaimStrength &&
        string.Equals(expected.Title, actual.Title, StringComparison.Ordinal) &&
        string.Equals(expected.Summary, actual.Summary, StringComparison.Ordinal) &&
        string.Equals(expected.Reason, actual.Reason, StringComparison.Ordinal) &&
        string.Equals(
            expected.AffectsStartAs,
            actual.AffectsStartAs,
            StringComparison.Ordinal);

    private static bool SlotAllowsIntent(
        ScenarioCoreService.ScenarioCorrectionSlot slot,
        string intent) =>
        intent switch
        {
            "friendly" => slot.AllowsFriendly,
            "hostile" => slot.AllowsHostile,
            _ => false
        };

    private static bool SeverityFitsSlot(string severity, string maxSeverity) =>
        GetSeverityRank(severity) <= GetSeverityRank(maxSeverity);

    private static int GetSeverityRank(string severity) => severity switch
    {
        "minor" => 1,
        "medium" => 2,
        "strong" => 3,
        _ => int.MaxValue
    };

    private static bool IsCorrectionIntent(string? intent) =>
        intent is "friendly" or "hostile";

    private static bool IsAllowedSeverity(string? severity) =>
        severity is "minor" or "medium" or "strong";

    private static bool HasCanonicalCorrectionCosts(
        string severity,
        int budgetCostPoints,
        int abodePowerCost) =>
        budgetCostPoints == AbodePowerRules.GetCorrectionSeverityBudgetCost(severity) &&
        abodePowerCost == AbodePowerRules.GetCorrectionSeverityAbodePowerCost(severity);

    private static bool IsCanonicalText(string? value) =>
        ResourceMaterializationContract.IsExactIdentifier(value);

    private static bool IsValidTransactionAfterImageManifest(
        IReadOnlyList<string>? paths)
    {
        if (paths == null || paths.Count == 0)
            return false;
        if (!paths.SequenceEqual(
                paths.OrderBy(static path => path, StringComparer.Ordinal),
                StringComparer.Ordinal) ||
            paths.Distinct(StringComparer.Ordinal).Count() != paths.Count ||
            paths.Any(path =>
                string.IsNullOrWhiteSpace(path) ||
                !string.Equals(path, path.Trim(), StringComparison.Ordinal) ||
                path.Contains('\\') ||
                !AllowedReceiptTransactionPaths.Contains(path)))
        {
            return false;
        }

        var sealedPaths = paths.ToHashSet(StringComparer.Ordinal);
        return RequiredReceiptTransactionPaths.All(sealedPaths.Contains);
    }

    private static bool HasDuplicateValues(IEnumerable<string> values)
    {
        var unique = new HashSet<string>(StringComparer.Ordinal);
        foreach (var value in values)
        {
            if (!unique.Add(value))
                return true;
        }
        return false;
    }

    private static string ComputeCorrectionReceiptFingerprint(
        GuardianCorrectionsState receipt)
    {
        var canonicalRoot = JsonSerializer.SerializeToNode(receipt, JsonOpts)!.AsObject();
        canonicalRoot.Remove("receiptFingerprint");
        using var fingerprint = new ResourceFingerprintBuilder(
            "guardian-correction-receipt-v1");
        fingerprint.Append(canonicalRoot.ToJsonString(JsonOpts));
        return fingerprint.Build();
    }

    private sealed record CorrectionReplayResolution(
        CorrectionReplayStatus Status,
        GuardianCorrectionsState? Receipt,
        string? FailureCode);

    private sealed record CorrectionTransactionAfterImageAuthority(
        IReadOnlyList<string> Paths,
        IReadOnlyDictionary<string, byte[]?> AfterImages,
        IReadOnlyList<CoordinatedStateWriteHelper.PlannedWrite> CoordinatedWrites);

    private enum CorrectionReplayStatus
    {
        NotApplied,
        SameLife,
        Malformed
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
        var allCandidates = new List<CorrectionSelectionCandidate>();
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

                allCandidates.Add(new CorrectionSelectionCandidate(
                    CorrectionId: BuildCorrectionId(
                        claimant.GuardianId,
                        slot.SlotId,
                        claimant.Intent,
                        severity),
                    SourceGuardianId: claimant.GuardianId,
                    SourceGuardianName: claimant.GuardianName,
                    Intent: claimant.Intent,
                    SlotId: slot.SlotId,
                    SlotType: slot.SlotType,
                    Severity: severity,
                    BudgetCostPoints: budgetCost,
                    AbodePowerCost: abodePowerCost,
                    ClaimStrength: claimant.ClaimStrengthBase +
                        AbodePowerRules.GetCorrectionSeverityBudgetCost(severity),
                    Title: template.GetTitle(severity),
                    Summary: template.GetSummary(
                        severity,
                        claimant.GuardianName),
                    Reason: template.GetReason(
                        claimant.Intent,
                        claimant.GuardianName),
                    AffectsStartAs: template.AffectsStartAs));
                usedSlots.Add(slot.SlotId);
            }
        }

        var transcript = SelectDeterministicCorrections(
            claimants.Select(static claimant =>
                new CorrectionSelectionClaimant(
                    claimant.GuardianId,
                    claimant.IsActivePatron,
                    claimant.CurrentPower,
                    claimant.BaseBudget + claimant.PreparationBudget))
                .ToArray(),
            allCandidates);
        foreach (var claimant in claimants)
        {
            var balance = transcript.FinalBalances[claimant.GuardianId];
            claimant.RemainingBudget = balance.RemainingBudget;
            claimant.RemainingPower = balance.RemainingPower;
            claimant.PowerAfter = balance.RemainingPower;
        }

        return (
            transcript.Winners
                .Select(ToGuardianCorrectionEntry)
                .ToList(),
            transcript.Contests
                .Select(ToGuardianCorrectionContest)
                .ToList(),
            transcript.ResolutionOrder.ToList());
    }

    private static CorrectionSelectionTranscript SelectDeterministicCorrections(
        IReadOnlyList<CorrectionSelectionClaimant> claimants,
        IReadOnlyList<CorrectionSelectionCandidate> candidates)
    {
        var claimantById = claimants.ToDictionary(
            static claimant => claimant.GuardianId,
            StringComparer.Ordinal);
        var balances = claimants.ToDictionary(
            static claimant => claimant.GuardianId,
            static claimant => new CorrectionSelectionBalance(
                claimant.InitialBudget,
                claimant.CurrentPower),
            StringComparer.Ordinal);
        var winners = new List<CorrectionSelectionCandidate>();
        var contests = new List<CorrectionSelectionContest>();
        var resolutionOrder = new List<string>();
        var hostileStrongUsed = false;

        foreach (var slotGroup in candidates
                     .GroupBy(
                         static candidate => candidate.SlotId,
                         StringComparer.OrdinalIgnoreCase)
                     .OrderBy(
                         static group => group.Key,
                         StringComparer.OrdinalIgnoreCase)
                     .ThenBy(
                         static group => group.Key,
                         StringComparer.Ordinal))
        {
            var orderedCandidates = slotGroup
                .OrderByDescending(static candidate => candidate.ClaimStrength)
                .ThenByDescending(candidate =>
                    claimantById[candidate.SourceGuardianId].IsActivePatron)
                .ThenByDescending(candidate =>
                    claimantById[candidate.SourceGuardianId].CurrentPower)
                .ThenBy(
                    static candidate => candidate.SourceGuardianId,
                    StringComparer.OrdinalIgnoreCase)
                .ThenBy(
                    static candidate => candidate.SourceGuardianId,
                    StringComparer.Ordinal)
                .ThenBy(
                    static candidate => candidate.CorrectionId,
                    StringComparer.Ordinal)
                .ToList();

            var winner = orderedCandidates.FirstOrDefault(candidate =>
                balances[candidate.SourceGuardianId].RemainingBudget >=
                    candidate.BudgetCostPoints &&
                balances[candidate.SourceGuardianId].RemainingPower >=
                    candidate.AbodePowerCost &&
                !(hostileStrongUsed &&
                  string.Equals(
                      candidate.Intent,
                      "hostile",
                      StringComparison.OrdinalIgnoreCase) &&
                  string.Equals(
                      candidate.Severity,
                      "strong",
                      StringComparison.OrdinalIgnoreCase)));

            if (winner == null)
            {
                contests.Add(new CorrectionSelectionContest(
                    slotGroup.Key,
                    orderedCandidates[0].SlotType,
                    WinnerGuardianId: "",
                    WinnerGuardianName: "",
                    WinnerCorrectionId: "",
                    Candidates: orderedCandidates.ToArray()));
                resolutionOrder.Add($"{slotGroup.Key}: no winner");
                continue;
            }

            var winnerBalance = balances[winner.SourceGuardianId];
            balances[winner.SourceGuardianId] = new CorrectionSelectionBalance(
                winnerBalance.RemainingBudget - winner.BudgetCostPoints,
                winnerBalance.RemainingPower - winner.AbodePowerCost);
            if (string.Equals(
                    winner.Intent,
                    "hostile",
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(
                    winner.Severity,
                    "strong",
                    StringComparison.OrdinalIgnoreCase))
            {
                hostileStrongUsed = true;
            }

            contests.Add(new CorrectionSelectionContest(
                slotGroup.Key,
                winner.SlotType,
                winner.SourceGuardianId,
                winner.SourceGuardianName,
                winner.CorrectionId,
                orderedCandidates.ToArray()));
            resolutionOrder.Add(
                $"{slotGroup.Key}: {winner.SourceGuardianName} [{winner.Severity}]");
            winners.Add(winner);
        }

        return new CorrectionSelectionTranscript(
            winners.ToArray(),
            contests.ToArray(),
            resolutionOrder.ToArray(),
            balances);
    }

    private static GuardianCorrectionEntry ToGuardianCorrectionEntry(
        CorrectionSelectionCandidate candidate) =>
        new()
        {
            CorrectionId = candidate.CorrectionId,
            SourceGuardianId = candidate.SourceGuardianId,
            SourceGuardianName = candidate.SourceGuardianName,
            Intent = candidate.Intent,
            SlotId = candidate.SlotId,
            SlotType = candidate.SlotType,
            Severity = candidate.Severity,
            BudgetCostPoints = candidate.BudgetCostPoints,
            AbodePowerCost = candidate.AbodePowerCost,
            ClaimStrength = candidate.ClaimStrength,
            Title = candidate.Title,
            Summary = candidate.Summary,
            Reason = candidate.Reason,
            AffectsStartAs = candidate.AffectsStartAs
        };

    private static GuardianCorrectionContest ToGuardianCorrectionContest(
        CorrectionSelectionContest contest) =>
        new()
        {
            SlotId = contest.SlotId,
            SlotType = contest.SlotType,
            WinnerGuardianId = contest.WinnerGuardianId,
            WinnerGuardianName = contest.WinnerGuardianName,
            WinnerCorrectionId = contest.WinnerCorrectionId,
            Candidates = contest.Candidates
                .Select(static candidate => new GuardianCorrectionCandidate
                {
                    CandidateCorrectionId = candidate.CorrectionId,
                    SourceGuardianId = candidate.SourceGuardianId,
                    SourceGuardianName = candidate.SourceGuardianName,
                    Intent = candidate.Intent,
                    Severity = candidate.Severity,
                    BudgetCostPoints = candidate.BudgetCostPoints,
                    AbodePowerCost = candidate.AbodePowerCost,
                    ClaimStrength = candidate.ClaimStrength,
                    Title = candidate.Title
                })
                .ToList()
        };

    private static string BuildCorrectionId(
        string guardianId,
        string slotId,
        string intent,
        string severity)
    {
        using var fingerprint = new ResourceFingerprintBuilder(
            "guardian-correction-identity-v1");
        fingerprint.Append(guardianId);
        fingerprint.Append(slotId);
        fingerprint.Append(intent);
        fingerprint.Append(severity);
        var authorityFingerprint = fingerprint.Build();
        return "gcor_" + authorityFingerprint["sha256:".Length..];
    }

    private static JsonObject BuildPowerSpendEvent(
        GuardianCorrectionEntry correction,
        int lifeIncarnation)
    {
        var audit = new JsonObject
        {
            ["lifeIncarnation"] = lifeIncarnation,
            ["correctionId"] = correction.CorrectionId,
            ["slotId"] = correction.SlotId,
            ["slotType"] = correction.SlotType,
            ["severity"] = correction.Severity,
            ["claimStrength"] = correction.ClaimStrength,
            ["intent"] = correction.Intent
        };

        return GuardianPowerEventState.BuildEvent(
            $"gce_life_{lifeIncarnation}_{correction.CorrectionId}",
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

    private sealed record CorrectionSelectionClaimant(
        string GuardianId,
        bool IsActivePatron,
        int CurrentPower,
        int InitialBudget);

    private sealed record CorrectionSelectionCandidate(
        string CorrectionId,
        string SourceGuardianId,
        string SourceGuardianName,
        string Intent,
        string SlotId,
        string SlotType,
        string Severity,
        int BudgetCostPoints,
        int AbodePowerCost,
        int ClaimStrength,
        string Title,
        string Summary,
        string Reason,
        string AffectsStartAs);

    private sealed record CorrectionSelectionBalance(
        int RemainingBudget,
        int RemainingPower);

    private sealed record CorrectionSelectionContest(
        string SlotId,
        string SlotType,
        string WinnerGuardianId,
        string WinnerGuardianName,
        string WinnerCorrectionId,
        IReadOnlyList<CorrectionSelectionCandidate> Candidates);

    private sealed record CorrectionSelectionTranscript(
        IReadOnlyList<CorrectionSelectionCandidate> Winners,
        IReadOnlyList<CorrectionSelectionContest> Contests,
        IReadOnlyList<string> ResolutionOrder,
        IReadOnlyDictionary<string, CorrectionSelectionBalance> FinalBalances);

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
