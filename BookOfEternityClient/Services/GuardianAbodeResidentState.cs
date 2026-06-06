using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianAbodeResidentState
{
    public const string StatePath = "game_state/meta/guardian_abode_residents.json";
    public const string UpdateProperty = "UpdateGuardianAbodeResidents";
    public const string UpdateRosterReceiptsProperty = "UpdateGuardianAbodeResidentRosterReceipts";
    public const string RosterReceiptsProperty = "rosterReceipts";
    public const string EntriesProperty = "entries";
    public const string UpdateInteractionReceiptsProperty = "UpdateGuardianAbodeResidentInteractionReceipts";
    public const string InteractionReceiptsProperty = "interactionReceipts";
    public const string UpdateTransferReceiptsProperty = "UpdateGuardianAbodeResidentTransferReceipts";
    public const string TransferReceiptsProperty = "transferReceipts";
    public const string UpdateHistoryLogProperty = "UpdateGuardianAbodeResidentHistoryLog";
    public const string HistoryLogProperty = "historyLog";
    public const string UpdateThoughtJournalProperty = "residentThoughtJournalUpdates";
    public const string ThoughtJournalProperty = "thoughtJournal";
    public const string UpdateInteractionLogProperty = "residentInteractionLogUpdates";
    public const string InteractionLogProperty = "interactionLog";
    public const string RelicTypeCompanionEcho = "companion_echo";

    public const string InteractionTypeTalk = "talk";
    public const string InteractionTypeHistory = "history";

    public const string InteractionStatusAccepted = "accepted";
    public const string InteractionStatusRejected = "rejected";
    public const string InteractionStatusCancelled = "cancelled";

    public const string TransferStatusAccepted = "accepted";
    public const string TransferStatusRefused = "refused";
    public const string TransferStatusDepartedOnly = "departed_only";

    public const string TransferModeDepartureOnly = "departure_only";
    public const string TransferModeAcceptedTransfer = "accepted_transfer";
    public const string TransferModeRefusedTransfer = "refused_transfer";
    public const string TransferCompetitionLabelStrongPull = "strong_pull";
    public const string TransferCompetitionLabelPlausiblePull = "plausible_pull";
    public const string TransferCompetitionLabelWeakPull = "weak_pull";

    public const string ResponseModeTalkScene = "talk_scene";
    public const string ResponseModeHistoryRevealed = "history_revealed";
    public const string ResponseModeHistoryRefused = "history_refused";
    public const string ResponseModeHistoryPartial = "history_partial";
    public const string ResponseModeBondShiftOnly = "bond_shift_only";

    public const string BondTierStranger = "stranger";
    public const string BondTierFamiliar = "familiar";
    public const string BondTierTrusted = "trusted";
    public const string BondTierBound = "bound";

    public const string AbodeDevotionTierAlienated = "alienated";
    public const string AbodeDevotionTierUncertain = "uncertain";
    public const string AbodeDevotionTierAttached = "attached";
    public const string AbodeDevotionTierDevoted = "devoted";
    public const string AbodeDevotionTierSteadfast = "steadfast";

    public const string MigrationStateSettled = "settled";
    public const string MigrationStateWavering = "wavering";
    public const string MigrationStateRestless = "restless";
    public const string MigrationStateConsideringDeparture = "considering_departure";
    public const string MigrationStateReadyToTransfer = "ready_to_transfer";

    public const string PowerSensitivityLow = "low";
    public const string PowerSensitivityMedium = "medium";
    public const string PowerSensitivityHigh = "high";

    public const string MigrationDispositionRooted = "rooted";
    public const string MigrationDispositionSelective = "selective";
    public const string MigrationDispositionOpportunistic = "opportunistic";
    public const string MigrationDispositionWandering = "wandering";

    public const string CommunalOrientationLow = "low";
    public const string CommunalOrientationMedium = "medium";
    public const string CommunalOrientationHigh = "high";

    public const string StabilityNeedLow = "low";
    public const string StabilityNeedMedium = "medium";
    public const string StabilityNeedHigh = "high";

    public const string RewardStateNone = "none";
    public const string RewardStateEligible = "eligible";
    public const string RewardStateGranted = "granted";
    public const string RewardStateConsumed = "consumed";

    private static readonly HashSet<string> AllowedResidentKinds = new(StringComparer.OrdinalIgnoreCase)
    {
        "junior_spirit",
        "attendant_spirit",
        "wayfaring_soul",
        "bound_soul"
    };

    private static readonly HashSet<string> AllowedOriginTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "native_spirit",
        "traveler_soul"
    };

    private static readonly HashSet<string> AllowedBondTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        BondTierStranger,
        BondTierFamiliar,
        BondTierTrusted,
        BondTierBound
    };

    private static readonly HashSet<string> AllowedRewardStates = new(StringComparer.OrdinalIgnoreCase)
    {
        RewardStateNone,
        RewardStateEligible,
        RewardStateGranted,
        RewardStateConsumed
    };

    private static readonly HashSet<string> AllowedTransferCompetitionLabels = new(StringComparer.OrdinalIgnoreCase)
    {
        TransferCompetitionLabelStrongPull,
        TransferCompetitionLabelPlausiblePull,
        TransferCompetitionLabelWeakPull
    };

    private static readonly HashSet<string> AllowedAbodeDevotionTiers = new(StringComparer.OrdinalIgnoreCase)
    {
        AbodeDevotionTierAlienated,
        AbodeDevotionTierUncertain,
        AbodeDevotionTierAttached,
        AbodeDevotionTierDevoted,
        AbodeDevotionTierSteadfast
    };

    private static readonly HashSet<string> AllowedMigrationStates = new(StringComparer.OrdinalIgnoreCase)
    {
        MigrationStateSettled,
        MigrationStateWavering,
        MigrationStateRestless,
        MigrationStateConsideringDeparture,
        MigrationStateReadyToTransfer
    };

    private static readonly HashSet<string> AllowedPowerSensitivityValues = new(StringComparer.OrdinalIgnoreCase)
    {
        PowerSensitivityLow,
        PowerSensitivityMedium,
        PowerSensitivityHigh
    };

    private static readonly HashSet<string> AllowedMigrationDispositionValues = new(StringComparer.OrdinalIgnoreCase)
    {
        MigrationDispositionRooted,
        MigrationDispositionSelective,
        MigrationDispositionOpportunistic,
        MigrationDispositionWandering
    };

    private static readonly HashSet<string> AllowedCommunalOrientationValues = new(StringComparer.OrdinalIgnoreCase)
    {
        CommunalOrientationLow,
        CommunalOrientationMedium,
        CommunalOrientationHigh
    };

    private static readonly HashSet<string> AllowedStabilityNeedValues = new(StringComparer.OrdinalIgnoreCase)
    {
        StabilityNeedLow,
        StabilityNeedMedium,
        StabilityNeedHigh
    };

    private static readonly HashSet<string> AllowedInteractionTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        InteractionTypeTalk,
        InteractionTypeHistory
    };

    private static readonly HashSet<string> AllowedInteractionStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        InteractionStatusAccepted,
        InteractionStatusRejected,
        InteractionStatusCancelled
    };

    private static readonly HashSet<string> AllowedTransferStatuses = new(StringComparer.OrdinalIgnoreCase)
    {
        TransferStatusAccepted,
        TransferStatusRefused,
        TransferStatusDepartedOnly
    };

    private static readonly HashSet<string> AllowedTransferModes = new(StringComparer.OrdinalIgnoreCase)
    {
        TransferModeDepartureOnly,
        TransferModeAcceptedTransfer,
        TransferModeRefusedTransfer
    };

    private static readonly HashSet<string> AllowedResponseModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ResponseModeTalkScene,
        ResponseModeHistoryRevealed,
        ResponseModeHistoryRefused,
        ResponseModeHistoryPartial,
        ResponseModeBondShiftOnly
    };

    private static readonly string[] MigrationDispositionOrder =
    {
        MigrationDispositionRooted,
        MigrationDispositionSelective,
        MigrationDispositionOpportunistic,
        MigrationDispositionWandering
    };

    private static readonly string[] ScaleOrder =
    {
        PowerSensitivityLow,
        PowerSensitivityMedium,
        PowerSensitivityHigh
    };

    public sealed class ResidentEntry
    {
        public string ResidentId { get; init; } = "";
        public string GuardianId { get; init; } = "";
        public string AbodeId { get; init; } = "";
        public string DisplayName { get; init; } = "";
        public string ResidentKind { get; init; } = "";
        public string OriginType { get; init; } = "";
        public string RoleLabel { get; init; } = "";
        public string Summary { get; init; } = "";
        public int BondLevel { get; init; }
        public string BondTier { get; init; } = BondTierStranger;
        public bool CanGrantCompanionRelic { get; init; }
        public string BondRewardState { get; init; } = RewardStateNone;
        public string LinkedSoulQuestId { get; init; } = "";
        public string GrantedRelicId { get; init; } = "";
        public bool HistoryRevealed { get; init; }
        public string OriginWorldSummary { get; init; } = "";
        public string FutureCompanionPrompt { get; init; } = "";
        public string BondReason { get; init; } = "";
        public IReadOnlyList<string> CoreTraits { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ArchetypeHints { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> AppearanceMotifs { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> AvailableInteractions { get; init; } = Array.Empty<string>();
        public ResidentPersonalityProfile PersonalityProfile { get; init; } = new();
        public ResidentAbodeDisposition AbodeDisposition { get; init; } = new();
        public int AbodeDevotionLevel { get; init; }
        public string AbodeDevotionTier { get; init; } = AbodeDevotionTierAttached;
        public int Restlessness { get; init; }
        public string MigrationState { get; init; } = MigrationStateSettled;
        public bool IsPresent { get; init; } = true;
    }

    public sealed class ResidentPersonalityProfile
    {
        public string Archetype { get; init; } = "";
        public string Worldview { get; init; } = "";
        public string CulturalLayer { get; init; } = "";
        public IReadOnlyList<string> CoreValues { get; init; } = Array.Empty<string>();
        public IReadOnlyList<ResidentPersonalityTrait> PersonalityTraits { get; init; } = Array.Empty<ResidentPersonalityTrait>();
    }

    public sealed class ResidentPersonalityTrait
    {
        public string TraitName { get; init; } = "";
        public int Value { get; init; }
        public string ValueDescription { get; init; } = "";
        public string Description { get; init; } = "";
    }

    public sealed class ResidentAbodeDisposition
    {
        public string PowerSensitivity { get; init; } = PowerSensitivityMedium;
        public string MigrationDisposition { get; init; } = MigrationDispositionSelective;
        public string CommunalOrientation { get; init; } = CommunalOrientationMedium;
        public string StabilityNeed { get; init; } = StabilityNeedMedium;
    }

    public sealed class ResidentTransferCompetitionCandidate
    {
        public string TargetGuardianId { get; init; } = "";
        public string TargetGuardianName { get; init; } = "";
        public string TargetAbodeId { get; init; } = "";
        public string TargetAbodeName { get; init; } = "";
        public int SourceAbodePower { get; init; }
        public int TargetAbodePower { get; init; }
        public int TargetResidentCount { get; init; }
        public int CompetitionScore { get; init; }
        public string CompetitionLabel { get; init; } = TransferCompetitionLabelWeakPull;
        public string CompetitionReason { get; init; } = "";
    }

    public sealed class ResidentAbodeDriftContext
    {
        public bool TouchesResidentTurnSurface { get; init; }
        public int PreviousAbodePower { get; init; }
        public int CurrentAbodePower { get; init; }
        public bool HasPowerTierRise { get; init; }
        public bool HasPowerTierDecline { get; init; }
        public bool HasAcceptedTalkScene { get; init; }
        public bool HasAcceptedHistoryReveal { get; init; }
        public bool HasRejectedResidentScene { get; init; }
        public bool HasQuestProgress { get; init; }
        public bool HasRewardFulfilled { get; init; }
        public bool HasExplicitMemoryScene { get; init; }
        public bool ExplicitSceneLeansPositive { get; init; }
        public bool ExplicitSceneLeansNegative { get; init; }

        public bool HasCanonicalTrigger =>
            HasPowerTierRise ||
            HasPowerTierDecline ||
            HasAcceptedTalkScene ||
            HasAcceptedHistoryReveal ||
            HasRejectedResidentScene ||
            HasQuestProgress ||
            HasRewardFulfilled ||
            HasExplicitMemoryScene;
    }

    public sealed class ResidentAbodeDriftProjection
    {
        public bool HasCanonicalTrigger { get; init; }
        public int AbodeDevotionLevel { get; init; }
        public string AbodeDevotionTier { get; init; } = AbodeDevotionTierAttached;
        public int Restlessness { get; init; }
        public string MigrationState { get; init; } = MigrationStateSettled;
        public string TriggerSummary { get; init; } = "none";
    }

    public sealed class InteractionReceiptEntry
    {
        public string RequestId { get; init; } = "";
        public string ResidentId { get; init; } = "";
        public string GuardianId { get; init; } = "";
        public string AbodeId { get; init; } = "";
        public string InteractionType { get; init; } = "";
        public string Status { get; init; } = "";
        public string ResponseMode { get; init; } = "";
        public string HistoryEntryId { get; init; } = "";
        public string Reason { get; init; } = "";
        public int ResolvedAtTurn { get; init; }
        public string ResolvedAtUtc { get; init; } = "";
    }

    public sealed class RosterReceiptEntry
    {
        public string RequestId { get; init; } = "";
        public string GuardianId { get; init; } = "";
        public string GuardianName { get; init; } = "";
        public string AbodeId { get; init; } = "";
        public string AbodeName { get; init; } = "";
        public int RosterCount { get; init; }
        public int ResolvedAtTurn { get; init; }
        public string ResolvedAtUtc { get; init; } = "";
    }

    public sealed class TransferReceiptEntry
    {
        public string RequestId { get; init; } = "";
        public string ResidentId { get; init; } = "";
        public string ResidentName { get; init; } = "";
        public string SourceGuardianId { get; init; } = "";
        public string SourceGuardianName { get; init; } = "";
        public string SourceAbodeId { get; init; } = "";
        public string SourceAbodeName { get; init; } = "";
        public string TargetGuardianId { get; init; } = "";
        public string TargetGuardianName { get; init; } = "";
        public string TargetAbodeId { get; init; } = "";
        public string TargetAbodeName { get; init; } = "";
        public string Status { get; init; } = "";
        public string TransferMode { get; init; } = "";
        public string DepartureHistoryEntryId { get; init; } = "";
        public string ArrivalHistoryEntryId { get; init; } = "";
        public string Reason { get; init; } = "";
        public int ResolvedAtTurn { get; init; }
        public string ResolvedAtUtc { get; init; } = "";
    }

    public sealed class HistoryLogEntry
    {
        public string EntryId { get; init; } = "";
        public string ResidentId { get; init; } = "";
        public string Title { get; init; } = "";
        public string Summary { get; init; } = "";
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
        public int RevealedAtTurn { get; init; }
        public string RevealedAtUtc { get; init; } = "";
    }

    public sealed class JournalEntry
    {
        public string EntryId { get; init; } = "";
        public string ResidentId { get; init; } = "";
        public string Title { get; init; } = "";
        public string Summary { get; init; } = "";
        public string EventType { get; init; } = "";
        public string Consequence { get; init; } = "";
        public string Attitude { get; init; } = "";
        public string Intent { get; init; } = "";
        public IReadOnlyList<string> Tags { get; init; } = Array.Empty<string>();
        public int Turn { get; init; }
        public string Timestamp { get; init; } = "";
    }

    public static void NormalizeShape(JsonObject root)
    {
        if (root[EntriesProperty] is not JsonArray entries)
        {
            if (root[UpdateProperty] is JsonArray updateEntries)
                root[EntriesProperty] = updateEntries.DeepClone();
            else
                root[EntriesProperty] = new JsonArray();
        }

        if (root[EntriesProperty] is JsonArray normalizedEntries)
        {
            for (var i = normalizedEntries.Count - 1; i >= 0; i--)
            {
                if (normalizedEntries[i] is not JsonObject resident)
                {
                    normalizedEntries.RemoveAt(i);
                    continue;
                }

                NormalizeResidentObject(resident);
            }
        }

        if (root[RosterReceiptsProperty] is not JsonArray rosterReceipts)
        {
            if (root[UpdateRosterReceiptsProperty] is JsonArray updateRosterReceipts)
                root[RosterReceiptsProperty] = updateRosterReceipts.DeepClone();
            else
                root[RosterReceiptsProperty] = new JsonArray();
        }

        if (root[RosterReceiptsProperty] is JsonArray normalizedRosterReceipts)
        {
            for (var i = normalizedRosterReceipts.Count - 1; i >= 0; i--)
            {
                if (normalizedRosterReceipts[i] is not JsonObject receipt)
                {
                    normalizedRosterReceipts.RemoveAt(i);
                    continue;
                }

                NormalizeRosterReceiptObject(receipt);
            }
        }

        if (root[InteractionReceiptsProperty] is not JsonArray interactionReceipts)
        {
            if (root[UpdateInteractionReceiptsProperty] is JsonArray updateInteractionReceipts)
                root[InteractionReceiptsProperty] = updateInteractionReceipts.DeepClone();
            else
                root[InteractionReceiptsProperty] = new JsonArray();
        }

        if (root[InteractionReceiptsProperty] is JsonArray normalizedReceipts)
        {
            for (var i = normalizedReceipts.Count - 1; i >= 0; i--)
            {
                if (normalizedReceipts[i] is not JsonObject receipt)
                {
                    normalizedReceipts.RemoveAt(i);
                    continue;
                }

                NormalizeInteractionReceiptObject(receipt);
            }
        }

        if (root[TransferReceiptsProperty] is not JsonArray transferReceipts)
        {
            if (root[UpdateTransferReceiptsProperty] is JsonArray updateTransferReceipts)
                root[TransferReceiptsProperty] = updateTransferReceipts.DeepClone();
            else
                root[TransferReceiptsProperty] = new JsonArray();
        }

        if (root[TransferReceiptsProperty] is JsonArray normalizedTransferReceipts)
        {
            for (var i = normalizedTransferReceipts.Count - 1; i >= 0; i--)
            {
                if (normalizedTransferReceipts[i] is not JsonObject receipt)
                {
                    normalizedTransferReceipts.RemoveAt(i);
                    continue;
                }

                NormalizeTransferReceiptObject(receipt);
            }
        }

        if (root[HistoryLogProperty] is not JsonArray historyLog)
        {
            if (root[UpdateHistoryLogProperty] is JsonArray updateHistoryLog)
                root[HistoryLogProperty] = updateHistoryLog.DeepClone();
            else
                root[HistoryLogProperty] = new JsonArray();
        }

        if (root[HistoryLogProperty] is JsonArray normalizedHistory)
        {
            for (var i = normalizedHistory.Count - 1; i >= 0; i--)
            {
                if (normalizedHistory[i] is not JsonObject historyEntry)
                {
                    normalizedHistory.RemoveAt(i);
                    continue;
                }

                NormalizeHistoryLogEntry(historyEntry);
            }
        }

        if (root[ThoughtJournalProperty] is not JsonArray thoughtJournal)
        {
            if (root[UpdateThoughtJournalProperty] is JsonArray updateThoughtJournal)
                root[ThoughtJournalProperty] = updateThoughtJournal.DeepClone();
            else
                root[ThoughtJournalProperty] = new JsonArray();
        }

        if (root[ThoughtJournalProperty] is JsonArray normalizedThoughtJournal)
        {
            for (var i = normalizedThoughtJournal.Count - 1; i >= 0; i--)
            {
                if (normalizedThoughtJournal[i] is not JsonObject entry)
                {
                    normalizedThoughtJournal.RemoveAt(i);
                    continue;
                }

                ActorJournalState.NormalizeEntryObject(entry, "residentId");
            }
        }

        if (root[InteractionLogProperty] is not JsonArray interactionLog)
        {
            if (root[UpdateInteractionLogProperty] is JsonArray updateInteractionLog)
                root[InteractionLogProperty] = updateInteractionLog.DeepClone();
            else
                root[InteractionLogProperty] = new JsonArray();
        }

        if (root[InteractionLogProperty] is JsonArray normalizedInteractionLog)
        {
            for (var i = normalizedInteractionLog.Count - 1; i >= 0; i--)
            {
                if (normalizedInteractionLog[i] is not JsonObject entry)
                {
                    normalizedInteractionLog.RemoveAt(i);
                    continue;
                }

                ActorJournalState.NormalizeEntryObject(entry, "residentId");
            }
        }
    }

    public static JsonArray EnsureEntriesArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[EntriesProperty]!.AsArray();
    }

    public static JsonArray EnsureInteractionReceiptsArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[InteractionReceiptsProperty]!.AsArray();
    }

    public static JsonArray EnsureTransferReceiptsArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[TransferReceiptsProperty]!.AsArray();
    }

    public static JsonArray EnsureRosterReceiptsArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[RosterReceiptsProperty]!.AsArray();
    }

    public static JsonArray EnsureHistoryLogArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[HistoryLogProperty]!.AsArray();
    }

    public static JsonArray EnsureThoughtJournalArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[ThoughtJournalProperty]!.AsArray();
    }

    public static JsonArray EnsureInteractionLogArray(JsonObject root)
    {
        NormalizeShape(root);
        return root[InteractionLogProperty]!.AsArray();
    }

    public static void ApplyUpdates(JsonObject root, JsonArray updates)
    {
        var entries = EnsureEntriesArray(root);
        foreach (var resident in updates.OfType<JsonObject>())
        {
            NormalizeResidentObject(resident);
            var residentId = GetNodeString(resident["residentId"]);
            if (string.IsNullOrWhiteSpace(residentId))
                continue;

            UpsertResident(entries, resident);
        }
    }

    public static void ApplyRosterReceiptUpdates(JsonObject root, JsonArray receipts)
    {
        var rosterReceipts = EnsureRosterReceiptsArray(root);
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            NormalizeRosterReceiptObject(receipt);
            UpsertRosterReceipt(rosterReceipts, receipt);
        }
    }

    public static void ApplyInteractionReceiptUpdates(JsonObject root, JsonArray receipts)
    {
        var interactionReceipts = EnsureInteractionReceiptsArray(root);
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            NormalizeInteractionReceiptObject(receipt);
            UpsertInteractionReceipt(interactionReceipts, receipt);
        }
    }

    public static void ApplyTransferReceiptUpdates(JsonObject root, JsonArray receipts)
    {
        var transferReceipts = EnsureTransferReceiptsArray(root);
        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            NormalizeTransferReceiptObject(receipt);
            UpsertTransferReceipt(transferReceipts, receipt);
        }
    }

    public static void ApplyHistoryLogUpdates(JsonObject root, JsonArray historyLog)
    {
        var entries = EnsureHistoryLogArray(root);
        foreach (var historyEntry in historyLog.OfType<JsonObject>())
        {
            NormalizeHistoryLogEntry(historyEntry);
            UpsertHistoryLogEntry(entries, historyEntry);
        }
    }

    public static void ApplyThoughtJournalUpdates(JsonObject root, JsonArray updates)
    {
        var entries = EnsureThoughtJournalArray(root);
        foreach (var entry in updates.OfType<JsonObject>())
        {
            ActorJournalState.NormalizeEntryObject(entry, "residentId");
            UpsertJournalEntry(entries, entry);
        }
    }

    public static void ApplyInteractionLogUpdates(JsonObject root, JsonArray updates)
    {
        var entries = EnsureInteractionLogArray(root);
        foreach (var entry in updates.OfType<JsonObject>())
        {
            ActorJournalState.NormalizeEntryObject(entry, "residentId");
            UpsertJournalEntry(entries, entry);
        }
    }

    public static void UpsertResident(JsonArray entries, JsonObject resident)
    {
        var residentId = GetNodeString(resident["residentId"]);
        if (string.IsNullOrWhiteSpace(residentId))
            return;

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            entries[i] = resident.DeepClone();
            return;
        }

        entries.Add(resident.DeepClone());
    }

    public static List<ResidentEntry> CollectEntries(JsonElement root, string guardianId, string abodeId, bool presentOnly = true) =>
        CollectEntries(root, guardianId, abodeId, currentAbodePower: null, presentOnly);

    public static List<ResidentEntry> CollectEntries(
        JsonElement root,
        string guardianId,
        string abodeId,
        int? currentAbodePower,
        bool presentOnly = true)
    {
        var result = new List<ResidentEntry>();
        foreach (var resident in EnumerateResidentObjects(root))
        {
            if (!string.Equals(GetString(resident, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetString(resident, "abodeId"), abodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (presentOnly && !IsPresent(resident))
                continue;

            var residentObject = JsonNode.Parse(resident.GetRawText()) as JsonObject;
            if (residentObject == null)
                continue;

            NormalizeResidentObject(residentObject, currentAbodePower);
            result.Add(BuildResidentEntry(residentObject));
        }

        return result
            .OrderByDescending(entry => entry.BondLevel)
            .ThenBy(entry => entry.DisplayName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool HasResidentsForAbode(JsonElement root, string guardianId, string abodeId) =>
        CollectEntries(root, guardianId, abodeId, presentOnly: true).Count > 0;

    public static JsonObject? FindResident(JsonObject root, string residentId)
    {
        var entries = EnsureEntriesArray(root);
        return entries.OfType<JsonObject>()
            .FirstOrDefault(entry => string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    public static List<InteractionReceiptEntry> CollectInteractionReceipts(JsonElement root, string residentId)
    {
        var result = new List<InteractionReceiptEntry>();
        foreach (var receipt in EnumerateInteractionReceiptObjects(root))
        {
            if (!string.Equals(GetString(receipt, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new InteractionReceiptEntry
            {
                RequestId = GetString(receipt, "requestId"),
                ResidentId = GetString(receipt, "residentId"),
                GuardianId = GetString(receipt, "guardianId"),
                AbodeId = GetString(receipt, "abodeId"),
                InteractionType = GetString(receipt, "interactionType"),
                Status = GetString(receipt, "status"),
                ResponseMode = GetString(receipt, "responseMode"),
                HistoryEntryId = GetString(receipt, "historyEntryId"),
                Reason = GetString(receipt, "reason"),
                ResolvedAtTurn = GetInt(receipt, "resolvedAtTurn", 0),
                ResolvedAtUtc = GetString(receipt, "resolvedAtUtc")
            });
        }

        return result
            .OrderByDescending(entry => entry.ResolvedAtTurn)
            .ThenByDescending(entry => entry.ResolvedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<RosterReceiptEntry> CollectRosterReceipts(JsonElement root, string guardianId, string abodeId)
    {
        var result = new List<RosterReceiptEntry>();
        foreach (var receipt in EnumerateRosterReceiptObjects(root))
        {
            if (!string.Equals(GetString(receipt, "guardianId"), guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(GetString(receipt, "abodeId"), abodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            result.Add(new RosterReceiptEntry
            {
                RequestId = GetString(receipt, "requestId"),
                GuardianId = GetString(receipt, "guardianId"),
                GuardianName = GetString(receipt, "guardianName"),
                AbodeId = GetString(receipt, "abodeId"),
                AbodeName = GetString(receipt, "abodeName"),
                RosterCount = GetInt(receipt, "rosterCount", 0),
                ResolvedAtTurn = GetInt(receipt, "resolvedAtTurn", 0),
                ResolvedAtUtc = GetString(receipt, "resolvedAtUtc")
            });
        }

        return result
            .OrderByDescending(entry => entry.ResolvedAtTurn)
            .ThenByDescending(entry => entry.ResolvedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<TransferReceiptEntry> CollectTransferReceipts(JsonElement root, string residentId)
    {
        var result = new List<TransferReceiptEntry>();
        foreach (var receipt in EnumerateTransferReceiptObjects(root))
        {
            if (!string.Equals(GetString(receipt, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new TransferReceiptEntry
            {
                RequestId = GetString(receipt, "requestId"),
                ResidentId = GetString(receipt, "residentId"),
                ResidentName = GetString(receipt, "residentName"),
                SourceGuardianId = GetString(receipt, "sourceGuardianId"),
                SourceGuardianName = GetString(receipt, "sourceGuardianName"),
                SourceAbodeId = GetString(receipt, "sourceAbodeId"),
                SourceAbodeName = GetString(receipt, "sourceAbodeName"),
                TargetGuardianId = GetString(receipt, "targetGuardianId"),
                TargetGuardianName = GetString(receipt, "targetGuardianName"),
                TargetAbodeId = GetString(receipt, "targetAbodeId"),
                TargetAbodeName = GetString(receipt, "targetAbodeName"),
                Status = GetString(receipt, "status"),
                TransferMode = GetString(receipt, "transferMode"),
                DepartureHistoryEntryId = GetString(receipt, "departureHistoryEntryId"),
                ArrivalHistoryEntryId = GetString(receipt, "arrivalHistoryEntryId"),
                Reason = GetString(receipt, "reason"),
                ResolvedAtTurn = GetInt(receipt, "resolvedAtTurn", 0),
                ResolvedAtUtc = GetString(receipt, "resolvedAtUtc")
            });
        }

        return result
            .OrderByDescending(entry => entry.ResolvedAtTurn)
            .ThenByDescending(entry => entry.ResolvedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<HistoryLogEntry> CollectHistoryLogEntries(JsonElement root, string residentId)
    {
        var result = new List<HistoryLogEntry>();
        foreach (var entry in EnumerateHistoryLogObjects(root))
        {
            if (!string.Equals(GetString(entry, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new HistoryLogEntry
            {
                EntryId = GetString(entry, "entryId"),
                ResidentId = GetString(entry, "residentId"),
                Title = GetString(entry, "title"),
                Summary = GetString(entry, "summary"),
                Tags = ReadStringArray(entry, "tags"),
                RevealedAtTurn = GetInt(entry, "revealedAtTurn", 0),
                RevealedAtUtc = GetString(entry, "revealedAtUtc")
            });
        }

        return result
            .OrderByDescending(entry => entry.RevealedAtTurn)
            .ThenByDescending(entry => entry.RevealedAtUtc, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<JournalEntry> CollectThoughtJournalEntries(JsonElement root, string residentId)
    {
        var result = new List<JournalEntry>();
        foreach (var entry in EnumerateJournalObjects(root, ThoughtJournalProperty, UpdateThoughtJournalProperty))
        {
            if (!string.Equals(GetString(entry, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new JournalEntry
            {
                EntryId = GetString(entry, "entryId"),
                ResidentId = GetString(entry, "residentId"),
                Title = GetString(entry, "title"),
                Summary = GetString(entry, "summary"),
                EventType = GetString(entry, "eventType"),
                Consequence = GetString(entry, "consequence"),
                Attitude = GetString(entry, "attitude"),
                Intent = GetString(entry, "intent"),
                Tags = ReadStringArray(entry, "tags"),
                Turn = GetInt(entry, "turn", 0),
                Timestamp = GetString(entry, "timestamp")
            });
        }

        return result
            .OrderByDescending(entry => entry.Turn)
            .ThenByDescending(entry => entry.Timestamp, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static List<JournalEntry> CollectInteractionLogEntries(JsonElement root, string residentId)
    {
        var result = new List<JournalEntry>();
        foreach (var entry in EnumerateJournalObjects(root, InteractionLogProperty, UpdateInteractionLogProperty))
        {
            if (!string.Equals(GetString(entry, "residentId"), residentId, StringComparison.OrdinalIgnoreCase))
                continue;

            result.Add(new JournalEntry
            {
                EntryId = GetString(entry, "entryId"),
                ResidentId = GetString(entry, "residentId"),
                Title = GetString(entry, "title"),
                Summary = GetString(entry, "summary"),
                EventType = GetString(entry, "eventType"),
                Consequence = GetString(entry, "consequence"),
                Attitude = GetString(entry, "attitude"),
                Intent = GetString(entry, "intent"),
                Tags = ReadStringArray(entry, "tags"),
                Turn = GetInt(entry, "turn", 0),
                Timestamp = GetString(entry, "timestamp")
            });
        }

        return result
            .OrderByDescending(entry => entry.Turn)
            .ThenByDescending(entry => entry.Timestamp, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static bool IsSupportedResidentKind(string? residentKind) =>
        !string.IsNullOrWhiteSpace(residentKind) && AllowedResidentKinds.Contains(residentKind.Trim());

    public static bool IsSupportedOriginType(string? originType) =>
        !string.IsNullOrWhiteSpace(originType) && AllowedOriginTypes.Contains(originType.Trim());

    public static bool IsSupportedBondTier(string? bondTier) =>
        !string.IsNullOrWhiteSpace(bondTier) && AllowedBondTiers.Contains(bondTier.Trim());

    public static bool IsSupportedRewardState(string? rewardState) =>
        !string.IsNullOrWhiteSpace(rewardState) && AllowedRewardStates.Contains(rewardState.Trim());

    public static bool IsSupportedAbodeDevotionTier(string? abodeDevotionTier) =>
        !string.IsNullOrWhiteSpace(abodeDevotionTier) && AllowedAbodeDevotionTiers.Contains(abodeDevotionTier.Trim());

    public static bool IsSupportedMigrationState(string? migrationState) =>
        !string.IsNullOrWhiteSpace(migrationState) && AllowedMigrationStates.Contains(migrationState.Trim());

    public static bool IsSupportedPowerSensitivity(string? powerSensitivity) =>
        !string.IsNullOrWhiteSpace(powerSensitivity) && AllowedPowerSensitivityValues.Contains(powerSensitivity.Trim());

    public static bool IsSupportedMigrationDisposition(string? migrationDisposition) =>
        !string.IsNullOrWhiteSpace(migrationDisposition) && AllowedMigrationDispositionValues.Contains(migrationDisposition.Trim());

    public static bool IsSupportedCommunalOrientation(string? communalOrientation) =>
        !string.IsNullOrWhiteSpace(communalOrientation) && AllowedCommunalOrientationValues.Contains(communalOrientation.Trim());

    public static bool IsSupportedStabilityNeed(string? stabilityNeed) =>
        !string.IsNullOrWhiteSpace(stabilityNeed) && AllowedStabilityNeedValues.Contains(stabilityNeed.Trim());

    public static bool IsSupportedInteractionType(string? interactionType) =>
        !string.IsNullOrWhiteSpace(interactionType) && AllowedInteractionTypes.Contains(interactionType.Trim());

    public static bool IsSupportedInteractionStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllowedInteractionStatuses.Contains(status.Trim());

    public static bool IsSupportedTransferStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllowedTransferStatuses.Contains(status.Trim());

    public static bool IsSupportedTransferMode(string? transferMode) =>
        !string.IsNullOrWhiteSpace(transferMode) && AllowedTransferModes.Contains(transferMode.Trim());

    public static bool IsSupportedTransferCompetitionLabel(string? competitionLabel) =>
        !string.IsNullOrWhiteSpace(competitionLabel) && AllowedTransferCompetitionLabels.Contains(competitionLabel.Trim());

    public static bool IsSupportedResponseMode(string? responseMode) =>
        !string.IsNullOrWhiteSpace(responseMode) && AllowedResponseModes.Contains(responseMode.Trim());

    public static string ResolveBondTier(int bondLevel) => Math.Clamp(bondLevel, 0, 100) switch
    {
        >= 75 => BondTierBound,
        >= 50 => BondTierTrusted,
        >= 25 => BondTierFamiliar,
        _ => BondTierStranger
    };

    public static string ResolveAbodeDevotionTier(int abodeDevotionLevel) => Math.Clamp(abodeDevotionLevel, 0, 100) switch
    {
        <= 19 => AbodeDevotionTierAlienated,
        <= 39 => AbodeDevotionTierUncertain,
        <= 59 => AbodeDevotionTierAttached,
        <= 79 => AbodeDevotionTierDevoted,
        _ => AbodeDevotionTierSteadfast
    };

    public static string ResolveMigrationState(int abodeDevotionLevel, int restlessness)
    {
        abodeDevotionLevel = Math.Clamp(abodeDevotionLevel, 0, 100);
        restlessness = Math.Clamp(restlessness, 0, 100);
        if (abodeDevotionLevel <= 15 && restlessness >= 70)
            return MigrationStateReadyToTransfer;
        if (abodeDevotionLevel <= 30 && restlessness >= 55)
            return MigrationStateConsideringDeparture;
        if (abodeDevotionLevel <= 45 || restlessness >= 45)
            return MigrationStateRestless;
        if (abodeDevotionLevel <= 60 || restlessness >= 30)
            return MigrationStateWavering;

        return MigrationStateSettled;
    }

    public static string GetResidentKindLabel(string? residentKind) =>
        (residentKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "junior_spirit" => "Младший дух",
            "attendant_spirit" => "Служащий дух",
            "wayfaring_soul" => "Странствующая душа",
            "bound_soul" => "Связанная душа",
            _ => "Обитатель Обители"
        };

    public static string GetBondTierLabel(string? bondTier) =>
        (bondTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            BondTierStranger => "Чужой",
            BondTierFamiliar => "Знакомый",
            BondTierTrusted => "Доверенный",
            BondTierBound => "Связанный",
            _ => "Неизвестно"
        };

    public static string GetRewardStateLabel(string? rewardState) =>
        (rewardState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            RewardStateEligible => "готов даровать реликвию",
            RewardStateGranted => "реликвия уже дарована",
            RewardStateConsumed => "реликвия уже воплощалась",
            _ => "связь ещё не завершена"
        };

    public static string GetAbodeDevotionTierLabel(string? abodeDevotionTier) =>
        (abodeDevotionTier ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            AbodeDevotionTierAlienated => "Отчуждён",
            AbodeDevotionTierUncertain => "Неуверен",
            AbodeDevotionTierAttached => "Привязан",
            AbodeDevotionTierDevoted => "Предан",
            AbodeDevotionTierSteadfast => "Непоколебим",
            _ => "Неопределён"
        };

    public static string GetMigrationStateLabel(string? migrationState) =>
        (migrationState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            MigrationStateSettled => "Укоренён",
            MigrationStateWavering => "Колеблется",
            MigrationStateRestless => "Беспокоен",
            MigrationStateConsideringDeparture => "Думает об уходе",
            MigrationStateReadyToTransfer => "Готов уйти",
            _ => "Неопределён"
        };

    public static string GetMigrationStatePressureNarrative(string? migrationState) =>
        (migrationState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            MigrationStateWavering => "Обитель больше не кажется безусловным домом.",
            MigrationStateRestless => "В нём растёт тяга к перемене и усталость от слабости Обители.",
            MigrationStateConsideringDeparture => "Если упадок продолжится, этот резидент может уйти к иному свету.",
            MigrationStateReadyToTransfer => "Резидент уже внутренне готов искать другую Обитель, но автоматический переход ещё не выполняется.",
            _ => string.Empty
        };

    public static string GetTransferStatusLabel(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferStatusAccepted => "Переход принят",
            TransferStatusRefused => "Переход отклонён",
            TransferStatusDepartedOnly => "Резидент покинул Обитель",
            _ => string.Empty
        };

    public static string GetTransferCompetitionLabelText(string? competitionLabel) =>
        (competitionLabel ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferCompetitionLabelStrongPull => "сильный зов",
            TransferCompetitionLabelPlausiblePull => "убедительный зов",
            _ => "слабый зов"
        };

    public static string GetPowerSensitivityLabel(string? powerSensitivity) =>
        NormalizeTernaryScaleValue(powerSensitivity) switch
        {
            PowerSensitivityHigh => "сильно чувствителен к силе Обители",
            PowerSensitivityLow => "мало зависит от силы Обители",
            _ => "умеренно чувствителен к силе Обители"
        };

    public static string GetMigrationDispositionLabel(string? migrationDisposition) =>
        NormalizeMigrationDisposition(migrationDisposition) switch
        {
            MigrationDispositionRooted => "укоренён",
            MigrationDispositionOpportunistic => "ищет лучшие условия",
            MigrationDispositionWandering => "склонен к странствию",
            _ => "выбирает, где остаться"
        };

    public static string GetCommunalOrientationLabel(string? communalOrientation) =>
        NormalizeTernaryScaleValue(communalOrientation) switch
        {
            CommunalOrientationHigh => "сильно держится за общину",
            CommunalOrientationLow => "держится скорее за себя, чем за общину",
            _ => "умеренно зависит от жизни общины"
        };

    public static string GetStabilityNeedLabel(string? stabilityNeed) =>
        NormalizeTernaryScaleValue(stabilityNeed) switch
        {
            StabilityNeedHigh => "требует устойчивости",
            StabilityNeedLow => "легко переносит перемены",
            _ => "нуждается в умеренной устойчивости"
        };

    public static bool IsCompanionEchoRelic(JsonElement relic)
    {
        var relicType = GetFirstString(relic, "relicType", "type");
        return string.Equals(relicType, RelicTypeCompanionEcho, StringComparison.OrdinalIgnoreCase) &&
               relic.TryGetProperty("companionSeed", out var companionSeed) &&
               companionSeed.ValueKind == JsonValueKind.Object;
    }

    public static bool HasEmbeddedSoulImprint(JsonElement relic)
    {
        return (relic.TryGetProperty("soulImprint", out var soulImprint) && soulImprint.ValueKind == JsonValueKind.Object) ||
               (relic.TryGetProperty("npcSoulImprint", out var npcSoulImprint) && npcSoulImprint.ValueKind == JsonValueKind.Object);
    }

    public static void NormalizeResidentObject(JsonObject resident) =>
        NormalizeResidentObject(resident, currentAbodePower: null);

    public static void NormalizeResidentObject(JsonObject resident, int? currentAbodePower)
    {
        resident["displayName"] ??= string.Empty;
        resident["roleLabel"] ??= string.Empty;
        resident["summary"] ??= string.Empty;
        resident["bondLevel"] = Math.Clamp(GetNodeInt(resident["bondLevel"]), 0, 100);
        resident["bondTier"] = ResolveBondTier(GetNodeInt(resident["bondLevel"]));
        resident["bondRewardState"] = NormalizeRewardState(GetNodeString(resident["bondRewardState"]));
        resident["isPresent"] = GetNodeBool(resident["isPresent"], true);
        resident["canGrantCompanionRelic"] = GetNodeBool(resident["canGrantCompanionRelic"], false);
        resident["linkedSoulQuestId"] ??= string.Empty;
        resident["grantedRelicId"] ??= string.Empty;
        resident["historyRevealed"] = GetNodeBool(resident["historyRevealed"], false);

        if (resident["availableInteractions"] is not JsonArray interactionArray)
            resident["availableInteractions"] = interactionArray = new JsonArray();
        NormalizeStringArray(interactionArray);

        if (resident["mortalWorldImprint"] is not JsonObject imprint)
            resident["mortalWorldImprint"] = imprint = new JsonObject();

        imprint["originWorldSummary"] ??= string.Empty;
        imprint["futureCompanionPrompt"] ??= string.Empty;
        imprint["bondReason"] ??= string.Empty;
        EnsureStringArray(imprint, "coreTraits");
        EnsureStringArray(imprint, "archetypeHints");
        EnsureStringArray(imprint, "appearanceMotifs");

        var seed = BuildSeedContext(resident);
        var normalizedCurrentAbodePower = ResolveCurrentAbodePower(currentAbodePower);

        if (resident["abodeDisposition"] is not JsonObject abodeDisposition)
            resident["abodeDisposition"] = abodeDisposition = new JsonObject();
        NormalizeAbodeDispositionObject(abodeDisposition, seed);

        if (resident["personalityProfile"] is not JsonObject personalityProfile)
            resident["personalityProfile"] = personalityProfile = new JsonObject();
        NormalizePersonalityProfileObject(personalityProfile, seed, abodeDisposition, normalizedCurrentAbodePower);

        var seededDevotionLevel = SeedAbodeDevotionLevel(seed, abodeDisposition, normalizedCurrentAbodePower);
        resident["abodeDevotionLevel"] = ClampResidentMeter(GetNodeIntOrDefault(resident["abodeDevotionLevel"], seededDevotionLevel));
        resident["abodeDevotionTier"] = ResolveAbodeDevotionTier(GetNodeInt(resident["abodeDevotionLevel"]));

        var seededRestlessness = SeedRestlessness(seed, abodeDisposition, GetNodeInt(resident["abodeDevotionLevel"]), normalizedCurrentAbodePower);
        resident["restlessness"] = ClampResidentMeter(GetNodeIntOrDefault(resident["restlessness"], seededRestlessness));
        resident["migrationState"] = ResolveMigrationState(GetNodeInt(resident["abodeDevotionLevel"]), GetNodeInt(resident["restlessness"]));
    }

    public static void NormalizeInteractionReceiptObject(JsonObject receipt)
    {
        receipt["guardianId"] ??= string.Empty;
        receipt["guardianName"] ??= string.Empty;
        receipt["abodeId"] ??= string.Empty;
        receipt["abodeName"] ??= string.Empty;
        receipt["residentId"] ??= string.Empty;
        receipt["residentName"] ??= string.Empty;
        receipt["interactionType"] = NormalizeInteractionType(GetNodeString(receipt["interactionType"]));
        receipt["status"] = NormalizeInteractionStatus(GetNodeString(receipt["status"]));
        receipt["responseMode"] = NormalizeResponseMode(GetNodeString(receipt["responseMode"]));
        receipt["historyEntryId"] ??= string.Empty;
        receipt["reason"] ??= string.Empty;
        receipt["resolvedAtTurn"] = Math.Max(0, GetNodeInt(receipt["resolvedAtTurn"]));
        receipt["resolvedAtUtc"] ??= DateTime.UtcNow.ToString("o");
    }

    public static void NormalizeRosterReceiptObject(JsonObject receipt)
    {
        receipt["guardianId"] ??= string.Empty;
        receipt["guardianName"] ??= string.Empty;
        receipt["abodeId"] ??= string.Empty;
        receipt["abodeName"] ??= string.Empty;
        receipt["rosterCount"] = Math.Max(0, GetNodeInt(receipt["rosterCount"]));
        receipt["resolvedAtTurn"] = Math.Max(0, GetNodeInt(receipt["resolvedAtTurn"]));
        receipt["resolvedAtUtc"] ??= DateTime.UtcNow.ToString("o");
    }

    public static void NormalizeTransferReceiptObject(JsonObject receipt)
    {
        receipt["residentId"] ??= string.Empty;
        receipt["residentName"] ??= string.Empty;
        receipt["sourceGuardianId"] ??= string.Empty;
        receipt["sourceGuardianName"] ??= string.Empty;
        receipt["sourceAbodeId"] ??= string.Empty;
        receipt["sourceAbodeName"] ??= string.Empty;
        receipt["targetGuardianId"] ??= string.Empty;
        receipt["targetGuardianName"] ??= string.Empty;
        receipt["targetAbodeId"] ??= string.Empty;
        receipt["targetAbodeName"] ??= string.Empty;
        receipt["status"] = NormalizeTransferStatus(GetNodeString(receipt["status"]));
        receipt["transferMode"] = NormalizeTransferMode(GetNodeString(receipt["transferMode"]));
        receipt["departureHistoryEntryId"] ??= string.Empty;
        receipt["arrivalHistoryEntryId"] ??= string.Empty;
        receipt["reason"] ??= string.Empty;
        receipt["resolvedAtTurn"] = Math.Max(0, GetNodeInt(receipt["resolvedAtTurn"]));
        receipt["resolvedAtUtc"] ??= DateTime.UtcNow.ToString("o");
    }

    public static void NormalizeHistoryLogEntry(JsonObject historyEntry)
    {
        historyEntry["title"] ??= string.Empty;
        historyEntry["summary"] ??= string.Empty;
        historyEntry["revealedAtTurn"] = Math.Max(0, GetNodeInt(historyEntry["revealedAtTurn"]));
        historyEntry["revealedAtUtc"] ??= DateTime.UtcNow.ToString("o");

        if (historyEntry["tags"] is not JsonArray tags)
            historyEntry["tags"] = tags = new JsonArray();
        NormalizeStringArray(tags);
    }

    public static ResidentEntry ReadResidentEntry(JsonObject resident, int? currentAbodePower = null)
    {
        var projection = resident.DeepClone().AsObject();
        NormalizeResidentObject(projection, currentAbodePower);
        return BuildResidentEntry(projection);
    }

    private static ResidentEntry BuildResidentEntry(JsonObject resident)
    {
        var imprint = resident["mortalWorldImprint"] as JsonObject;
        var personalityProfile = resident["personalityProfile"] as JsonObject;
        var abodeDisposition = resident["abodeDisposition"] as JsonObject;

        return new ResidentEntry
        {
            ResidentId = GetNodeString(resident["residentId"]) ?? string.Empty,
            GuardianId = GetNodeString(resident["guardianId"]) ?? string.Empty,
            AbodeId = GetNodeString(resident["abodeId"]) ?? string.Empty,
            DisplayName = GetNodeString(resident["displayName"]) ?? string.Empty,
            ResidentKind = GetNodeString(resident["residentKind"]) ?? string.Empty,
            OriginType = GetNodeString(resident["originType"]) ?? string.Empty,
            RoleLabel = GetNodeString(resident["roleLabel"]) ?? string.Empty,
            Summary = GetNodeString(resident["summary"]) ?? string.Empty,
            BondLevel = GetNodeInt(resident["bondLevel"]),
            BondTier = GetNodeString(resident["bondTier"]) ?? BondTierStranger,
            CanGrantCompanionRelic = GetNodeBool(resident["canGrantCompanionRelic"], false),
            BondRewardState = GetNodeString(resident["bondRewardState"]) ?? RewardStateNone,
            LinkedSoulQuestId = GetNodeString(resident["linkedSoulQuestId"]) ?? string.Empty,
            GrantedRelicId = GetNodeString(resident["grantedRelicId"]) ?? string.Empty,
            HistoryRevealed = GetNodeBool(resident["historyRevealed"], false),
            OriginWorldSummary = GetNodeString(imprint?["originWorldSummary"]) ?? string.Empty,
            FutureCompanionPrompt = GetNodeString(imprint?["futureCompanionPrompt"]) ?? string.Empty,
            BondReason = GetNodeString(imprint?["bondReason"]) ?? string.Empty,
            CoreTraits = ReadNodeStringArray(imprint?["coreTraits"]),
            ArchetypeHints = ReadNodeStringArray(imprint?["archetypeHints"]),
            AppearanceMotifs = ReadNodeStringArray(imprint?["appearanceMotifs"]),
            AvailableInteractions = ReadNodeStringArray(resident["availableInteractions"]),
            PersonalityProfile = new ResidentPersonalityProfile
            {
                Archetype = GetNodeString(personalityProfile?["archetype"]) ?? string.Empty,
                Worldview = GetNodeString(personalityProfile?["worldview"]) ?? string.Empty,
                CulturalLayer = GetNodeString(personalityProfile?["culturalLayer"]) ?? string.Empty,
                CoreValues = ReadNodeStringArray(personalityProfile?["coreValues"]),
                PersonalityTraits = ReadPersonalityTraits(personalityProfile?["personalityTraits"])
            },
            AbodeDisposition = new ResidentAbodeDisposition
            {
                PowerSensitivity = GetNodeString(abodeDisposition?["powerSensitivity"]) ?? PowerSensitivityMedium,
                MigrationDisposition = GetNodeString(abodeDisposition?["migrationDisposition"]) ?? MigrationDispositionSelective,
                CommunalOrientation = GetNodeString(abodeDisposition?["communalOrientation"]) ?? CommunalOrientationMedium,
                StabilityNeed = GetNodeString(abodeDisposition?["stabilityNeed"]) ?? StabilityNeedMedium
            },
            AbodeDevotionLevel = GetNodeInt(resident["abodeDevotionLevel"]),
            AbodeDevotionTier = GetNodeString(resident["abodeDevotionTier"]) ?? AbodeDevotionTierAttached,
            Restlessness = GetNodeInt(resident["restlessness"]),
            MigrationState = GetNodeString(resident["migrationState"]) ?? MigrationStateSettled,
            IsPresent = GetNodeBool(resident["isPresent"], true)
        };
    }

    private sealed class ResidentSeedContext
    {
        public string DisplayName { get; init; } = "";
        public string ResidentKind { get; init; } = "";
        public string OriginType { get; init; } = "";
        public string RoleLabel { get; init; } = "";
        public string Summary { get; init; } = "";
        public int BondLevel { get; init; }
        public string BondReason { get; init; } = "";
        public string OriginWorldSummary { get; init; } = "";
        public IReadOnlyList<string> CoreTraits { get; init; } = Array.Empty<string>();
        public IReadOnlyList<string> ArchetypeHints { get; init; } = Array.Empty<string>();
    }

    private static ResidentSeedContext BuildSeedContext(JsonObject resident)
    {
        var imprint = resident["mortalWorldImprint"] as JsonObject;
        return new ResidentSeedContext
        {
            DisplayName = GetNodeString(resident["displayName"]) ?? string.Empty,
            ResidentKind = GetNodeString(resident["residentKind"]) ?? string.Empty,
            OriginType = GetNodeString(resident["originType"]) ?? string.Empty,
            RoleLabel = GetNodeString(resident["roleLabel"]) ?? string.Empty,
            Summary = GetNodeString(resident["summary"]) ?? string.Empty,
            BondLevel = ClampResidentMeter(GetNodeInt(resident["bondLevel"])),
            BondReason = GetNodeString(imprint?["bondReason"]) ?? string.Empty,
            OriginWorldSummary = GetNodeString(imprint?["originWorldSummary"]) ?? string.Empty,
            CoreTraits = ReadNodeStringArray(imprint?["coreTraits"]),
            ArchetypeHints = ReadNodeStringArray(imprint?["archetypeHints"])
        };
    }

    private static void NormalizePersonalityProfileObject(
        JsonObject personalityProfile,
        ResidentSeedContext seed,
        JsonObject abodeDisposition,
        int currentAbodePower)
    {
        personalityProfile["archetype"] = NormalizeNonEmptyString(
            GetNodeString(personalityProfile["archetype"]),
            SeedArchetype(seed));
        personalityProfile["worldview"] = NormalizeNonEmptyString(
            GetNodeString(personalityProfile["worldview"]),
            SeedWorldview(seed));
        personalityProfile["culturalLayer"] = NormalizeNonEmptyString(
            GetNodeString(personalityProfile["culturalLayer"]),
            SeedCulturalLayer(seed));

        if (personalityProfile["coreValues"] is not JsonArray coreValues)
            personalityProfile["coreValues"] = coreValues = new JsonArray();
        NormalizeStringArray(coreValues);
        if (coreValues.Count == 0)
            foreach (var value in SeedCoreValues(seed))
                coreValues.Add(value);

        if (personalityProfile["personalityTraits"] is not JsonArray personalityTraits)
            personalityProfile["personalityTraits"] = personalityTraits = new JsonArray();
        NormalizePersonalityTraitArray(personalityTraits);
        if (personalityTraits.Count == 0)
        {
            foreach (var trait in SeedPersonalityTraits(seed, abodeDisposition, currentAbodePower))
                personalityTraits.Add(trait);
        }
    }

    private static void NormalizeAbodeDispositionObject(JsonObject abodeDisposition, ResidentSeedContext seed)
    {
        var seedDisposition = SeedAbodeDisposition(seed);
        abodeDisposition["powerSensitivity"] = NormalizePowerSensitivity(
            GetNodeString(abodeDisposition["powerSensitivity"]),
            seedDisposition.PowerSensitivity);
        abodeDisposition["migrationDisposition"] = NormalizeMigrationDisposition(
            GetNodeString(abodeDisposition["migrationDisposition"]),
            seedDisposition.MigrationDisposition);
        abodeDisposition["communalOrientation"] = NormalizeCommunalOrientation(
            GetNodeString(abodeDisposition["communalOrientation"]),
            seedDisposition.CommunalOrientation);
        abodeDisposition["stabilityNeed"] = NormalizeStabilityNeed(
            GetNodeString(abodeDisposition["stabilityNeed"]),
            seedDisposition.StabilityNeed);
    }

    private static ResidentAbodeDisposition SeedAbodeDisposition(ResidentSeedContext seed)
    {
        var powerIndex = 1;
        var migrationIndex = 1;
        var communalIndex = 1;
        var stabilityIndex = 1;

        switch ((seed.ResidentKind ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "junior_spirit":
                powerIndex = 2;
                migrationIndex = 1;
                communalIndex = 1;
                stabilityIndex = 1;
                break;
            case "attendant_spirit":
                powerIndex = 1;
                migrationIndex = 0;
                communalIndex = 2;
                stabilityIndex = 2;
                break;
            case "wayfaring_soul":
                powerIndex = 1;
                migrationIndex = 3;
                communalIndex = 0;
                stabilityIndex = 0;
                break;
            case "bound_soul":
                powerIndex = 0;
                migrationIndex = 0;
                communalIndex = 1;
                stabilityIndex = 2;
                break;
        }

        switch ((seed.OriginType ?? string.Empty).Trim().ToLowerInvariant())
        {
            case "native_spirit":
                migrationIndex--;
                communalIndex++;
                stabilityIndex++;
                break;
            case "traveler_soul":
                powerIndex++;
                migrationIndex++;
                communalIndex--;
                break;
        }

        var keywords = CollectSeedKeywords(seed);
        if (keywords.Any(keyword => ContainsAny(keyword, "loyal", "faith", "stead", "duty", "service", "верн", "предан", "служ")))
        {
            migrationIndex--;
            communalIndex++;
            stabilityIndex++;
        }

        if (keywords.Any(keyword => ContainsAny(keyword, "wander", "wayfar", "road", "nomad", "restless", "пут", "стран", "дорог", "вольн")))
        {
            migrationIndex++;
            stabilityIndex--;
        }

        if (keywords.Any(keyword => ContainsAny(keyword, "glory", "prestige", "ambit", "pride", "горд", "слав", "честолюб", "велич")))
            powerIndex++;

        if (keywords.Any(keyword => ContainsAny(keyword, "fear", "anx", "fragile", "care", "vigil", "трев", "хруп", "осторож", "страж")))
            stabilityIndex++;

        if (keywords.Any(keyword => ContainsAny(keyword, "hearth", "house", "garden", "choir", "ritual", "kin", "дом", "сад", "ритуал", "очаг", "общ")))
        {
            communalIndex++;
            migrationIndex--;
        }

        if (keywords.Any(keyword => ContainsAny(keyword, "alone", "solitary", "lone", "independent", "один", "одинок", "уедин", "сам")))
        {
            communalIndex--;
            migrationIndex++;
        }

        powerIndex = Math.Clamp(powerIndex, 0, 2);
        migrationIndex = Math.Clamp(migrationIndex, 0, 3);
        communalIndex = Math.Clamp(communalIndex, 0, 2);
        stabilityIndex = Math.Clamp(stabilityIndex, 0, 2);

        return new ResidentAbodeDisposition
        {
            PowerSensitivity = ScaleOrder[powerIndex],
            MigrationDisposition = MigrationDispositionOrder[migrationIndex],
            CommunalOrientation = ScaleOrder[communalIndex],
            StabilityNeed = ScaleOrder[stabilityIndex]
        };
    }

    private static int SeedAbodeDevotionLevel(ResidentSeedContext seed, JsonObject abodeDisposition, int currentAbodePower)
    {
        var devotion = 20 + (int)Math.Round(seed.BondLevel * 0.65, MidpointRounding.AwayFromZero);
        devotion += GetPowerBandDevotionModifier(currentAbodePower, GetNodeString(abodeDisposition["powerSensitivity"]));
        devotion += GetMigrationDispositionDevotionModifier(GetNodeString(abodeDisposition["migrationDisposition"]));
        devotion += GetCommunalDevotionModifier(GetNodeString(abodeDisposition["communalOrientation"]));
        devotion += GetStabilityDevotionModifier(currentAbodePower, GetNodeString(abodeDisposition["stabilityNeed"]));
        return ClampResidentMeter(devotion);
    }

    private static int SeedRestlessness(ResidentSeedContext seed, JsonObject abodeDisposition, int abodeDevotionLevel, int currentAbodePower)
    {
        var restlessness = 55 - (abodeDevotionLevel / 2);
        restlessness += GetMigrationDispositionRestlessnessModifier(GetNodeString(abodeDisposition["migrationDisposition"]));
        restlessness += GetCommunalRestlessnessModifier(GetNodeString(abodeDisposition["communalOrientation"]));
        restlessness += GetPowerBandRestlessnessModifier(currentAbodePower, GetNodeString(abodeDisposition["powerSensitivity"]));
        restlessness += GetStabilityRestlessnessModifier(currentAbodePower, GetNodeString(abodeDisposition["stabilityNeed"]));
        if (seed.BondLevel >= 75)
            restlessness -= 6;
        else if (seed.BondLevel <= 24)
            restlessness += 4;

        return ClampResidentMeter(restlessness);
    }

    private static JsonObject[] SeedPersonalityTraits(ResidentSeedContext seed, JsonObject abodeDisposition, int currentAbodePower)
    {
        var loyalty = Math.Clamp(
            2 + (int)Math.Round(seed.BondLevel / 15.0, MidpointRounding.AwayFromZero) +
            (string.Equals(GetNodeString(abodeDisposition["migrationDisposition"]), MigrationDispositionRooted, StringComparison.OrdinalIgnoreCase) ? 2 : 0) +
            (string.Equals(GetNodeString(abodeDisposition["communalOrientation"]), CommunalOrientationHigh, StringComparison.OrdinalIgnoreCase) ? 1 : 0),
            1,
            10);
        var restlessness = Math.Clamp(
            2 + Array.IndexOf(MigrationDispositionOrder, NormalizeMigrationDisposition(GetNodeString(abodeDisposition["migrationDisposition"]))) * 2 +
            (currentAbodePower <= 39 ? 1 : 0),
            1,
            10);
        var belonging = Math.Clamp(
            2 + Array.IndexOf(ScaleOrder, NormalizeCommunalOrientation(GetNodeString(abodeDisposition["communalOrientation"]), CommunalOrientationMedium)) * 2 +
            (string.Equals(seed.OriginType, "native_spirit", StringComparison.OrdinalIgnoreCase) ? 1 : 0),
            1,
            10);
        var stability = Math.Clamp(
            2 + Array.IndexOf(ScaleOrder, NormalizeStabilityNeed(GetNodeString(abodeDisposition["stabilityNeed"]), StabilityNeedMedium)) * 2 +
            (currentAbodePower <= 39 ? 1 : 0),
            1,
            10);

        var traits = new List<JsonObject>
        {
            BuildPersonalityTrait("Loyalty", loyalty, DescribeLoyalty(loyalty), "Clings to chosen bonds, vows, and duties once they feel real."),
            BuildPersonalityTrait("Restlessness", restlessness, DescribeRestlessness(restlessness), "Feels the pull of movement, change, or the need to seek a better horizon."),
            BuildPersonalityTrait("Need for Belonging", belonging, DescribeBelonging(belonging), "Measures safety and meaning through the warmth or absence of shared life."),
            BuildPersonalityTrait("Need for Stability", stability, DescribeStabilityNeed(stability), "Feels disorder, weakness, and uncertainty as either tolerable strain or immediate pain.")
        };

        var imprintTrait = seed.CoreTraits.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(imprintTrait))
        {
            var traitName = HumanizeToken(imprintTrait);
            if (!traits.Any(existing => string.Equals(GetNodeString(existing["traitName"]), traitName, StringComparison.OrdinalIgnoreCase)))
            {
                traits.Add(BuildPersonalityTrait(
                    traitName,
                    6,
                    "An old imprint still shapes the resident's reactions.",
                    "This trait is preserved from the resident's earlier imprint and still colors abode life."));
            }
        }

        return traits.Take(5).ToArray();
    }

    private static JsonObject BuildPersonalityTrait(string traitName, int value, string valueDescription, string description)
    {
        return new JsonObject
        {
            ["traitName"] = traitName,
            ["value"] = Math.Clamp(value, 1, 10),
            ["valueDescription"] = valueDescription,
            ["description"] = description
        };
    }

    private static string SeedArchetype(ResidentSeedContext seed)
    {
        var hint = seed.ArchetypeHints.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
        if (!string.IsNullOrWhiteSpace(hint))
            return HumanizeToken(hint);
        if (!string.IsNullOrWhiteSpace(seed.RoleLabel))
            return seed.RoleLabel;

        return (seed.ResidentKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "junior_spirit" => "Awakening Spirit",
            "attendant_spirit" => "Steady Attendant",
            "wayfaring_soul" => "Wayworn Wanderer",
            "bound_soul" => "Bound Witness",
            _ => "Abode Resident"
        };
    }

    private static string SeedWorldview(ResidentSeedContext seed) =>
        (seed.ResidentKind ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "junior_spirit" => "Feels belonging through warmth, recognition, and the promise that growth here is still possible.",
            "attendant_spirit" => "Believes an Abode proves itself through service, continuity, and the honoring of shared rituals.",
            "wayfaring_soul" => "Treats belonging as a chosen shelter, not a permanent chain, and watches closely for signs of decline or renewal.",
            "bound_soul" => "Sees loyalty as weighty and difficult to break; once a vow matters, even dimness becomes part of its burden.",
            _ => "Reads Abode life through memory, attachment, and the question of whether a place still deserves faith."
        };

    private static string SeedCulturalLayer(ResidentSeedContext seed)
    {
        var residentKind = (seed.ResidentKind ?? string.Empty).Trim().ToLowerInvariant();
        var originType = (seed.OriginType ?? string.Empty).Trim().ToLowerInvariant();
        if (residentKind == "attendant_spirit" && originType == "native_spirit")
            return "Household-spirit culture shaped by thresholds, service, and remembered vows.";
        if (residentKind == "wayfaring_soul" && originType == "traveler_soul")
            return "Pilgrim-memory culture shaped by roads, temporary shelters, and chosen loyalties.";
        if (residentKind == "bound_soul")
            return "Vow-bound memory culture shaped by remnants, unfinished ties, and endurance.";
        if (residentKind == "junior_spirit")
            return "Young abode-spirit culture formed by imitation, attachment, and the need to be welcomed.";

        return "Afterlife household culture shaped by ritual, memory, and the search for belonging.";
    }

    private static string[] SeedCoreValues(ResidentSeedContext seed)
    {
        var values = new List<string>();
        foreach (var trait in seed.CoreTraits)
        {
            var normalized = NormalizeCoreValue(trait);
            if (!string.IsNullOrWhiteSpace(normalized) &&
                !values.Contains(normalized, StringComparer.OrdinalIgnoreCase))
            {
                values.Add(normalized);
            }
        }

        foreach (var fallback in (seed.ResidentKind ?? string.Empty).Trim().ToLowerInvariant() switch
                 {
                     "junior_spirit" => new[] { "belonging", "recognition", "growth" },
                     "attendant_spirit" => new[] { "service", "continuity", "gratitude" },
                     "wayfaring_soul" => new[] { "freedom", "honesty", "chosen loyalty" },
                     "bound_soul" => new[] { "endurance", "memory", "fidelity" },
                     _ => new[] { "continuity", "belonging", "memory" }
                 })
        {
            if (!values.Contains(fallback, StringComparer.OrdinalIgnoreCase))
                values.Add(fallback);
            if (values.Count >= 4)
                break;
        }

        return values.Take(4).ToArray();
    }

    private static void NormalizePersonalityTraitArray(JsonArray personalityTraits)
    {
        for (var i = personalityTraits.Count - 1; i >= 0; i--)
        {
            if (personalityTraits[i] is not JsonObject trait)
            {
                personalityTraits.RemoveAt(i);
                continue;
            }

            var traitName = NormalizeNonEmptyString(GetNodeString(trait["traitName"]), string.Empty);
            if (string.IsNullOrWhiteSpace(traitName))
            {
                personalityTraits.RemoveAt(i);
                continue;
            }

            trait["traitName"] = traitName;
            trait["value"] = Math.Clamp(GetNodeIntOrDefault(trait["value"], 5), 1, 10);
            trait["valueDescription"] = NormalizeNonEmptyString(GetNodeString(trait["valueDescription"]), $"Intensity {GetNodeInt(trait["value"])}/10.");
            if (trait["description"] != null)
                trait["description"] = NormalizeNonEmptyString(GetNodeString(trait["description"]), string.Empty);
        }
    }

    public static JsonObject BuildCompanionSeed(JsonObject resident)
    {
        NormalizeResidentObject(resident);
        var imprint = resident["mortalWorldImprint"] as JsonObject ?? new JsonObject();
        var personalityProfile = resident["personalityProfile"] as JsonObject ?? new JsonObject();
        var abodeDisposition = resident["abodeDisposition"] as JsonObject ?? new JsonObject();
        return new JsonObject
        {
            ["sourceResidentId"] = GetNodeString(resident["residentId"]) ?? string.Empty,
            ["sourceGuardianId"] = GetNodeString(resident["guardianId"]) ?? string.Empty,
            ["sourceAbodeId"] = GetNodeString(resident["abodeId"]) ?? string.Empty,
            ["companionNameHint"] = GetNodeString(resident["displayName"]) ?? string.Empty,
            ["originWorldSummary"] = GetNodeString(imprint["originWorldSummary"]) ?? string.Empty,
            ["futureCompanionPrompt"] = GetNodeString(imprint["futureCompanionPrompt"]) ?? string.Empty,
            ["bondReason"] = GetNodeString(imprint["bondReason"]) ?? string.Empty,
            ["coreTraits"] = (imprint["coreTraits"] as JsonArray)?.DeepClone() ?? new JsonArray(),
            ["archetypeHints"] = (imprint["archetypeHints"] as JsonArray)?.DeepClone() ?? new JsonArray(),
            ["appearanceMotifs"] = (imprint["appearanceMotifs"] as JsonArray)?.DeepClone() ?? new JsonArray(),
            ["personalityProfile"] = personalityProfile.DeepClone(),
            ["abodeDisposition"] = abodeDisposition.DeepClone(),
            ["abodeDevotionLevel"] = GetNodeInt(resident["abodeDevotionLevel"]),
            ["abodeDevotionTier"] = GetNodeString(resident["abodeDevotionTier"]) ?? AbodeDevotionTierAttached,
            ["restlessness"] = GetNodeInt(resident["restlessness"]),
            ["migrationState"] = GetNodeString(resident["migrationState"]) ?? MigrationStateSettled
        };
    }

    public static JsonObject BuildCanonicalTransferArrivalResident(JsonObject resident, int? targetAbodePower)
    {
        var projection = resident.DeepClone().AsObject();
        NormalizeResidentObject(projection, targetAbodePower);

        var currentAbodePower = ResolveCurrentAbodePower(targetAbodePower);
        var seed = BuildSeedContext(projection);
        var abodeDisposition = projection["abodeDisposition"] as JsonObject ?? new JsonObject();
        var devotionLevel = SeedAbodeDevotionLevel(seed, abodeDisposition, currentAbodePower);
        projection["abodeDevotionLevel"] = devotionLevel;
        projection["abodeDevotionTier"] = ResolveAbodeDevotionTier(devotionLevel);
        var restlessness = SeedRestlessness(seed, abodeDisposition, devotionLevel, currentAbodePower);
        projection["restlessness"] = restlessness;
        projection["migrationState"] = ResolveMigrationState(devotionLevel, restlessness);
        projection["isPresent"] = true;
        return projection;
    }

    public static IReadOnlyList<ResidentTransferCompetitionCandidate> BuildTransferCompetitionCandidates(
        ResidentEntry resident,
        JsonObject? guardiansRoot,
        JsonObject? residentsRoot)
    {
        var candidates = new List<ResidentTransferCompetitionCandidate>();
        if (guardiansRoot?["guardians"] is not JsonArray guardians)
            return candidates;

        var guardianPowers = CollectGuardianAbodePowerById(guardiansRoot);
        var sourceAbodePower = guardianPowers.TryGetValue(resident.GuardianId, out var currentPower)
            ? currentPower
            : AbodePowerRules.DefaultCurrentPower;
        var presentCounts = CollectPresentResidentCountsByAbode(residentsRoot);

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var targetGuardianId = GetNodeString(guardian["guardianId"]) ?? GetNodeString(guardian["id"]);
            if (string.IsNullOrWhiteSpace(targetGuardianId))
                continue;

            if (guardian["abode"] is not JsonObject abode)
                continue;

            var targetAbodeId = GetNodeString(abode["abodeId"]);
            if (string.IsNullOrWhiteSpace(targetAbodeId))
                continue;

            if (string.Equals(targetGuardianId, resident.GuardianId, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(targetAbodeId, resident.AbodeId, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var targetGuardianName = GuardianManifestation.GetDisplayName(guardian);
            if (string.IsNullOrWhiteSpace(targetGuardianName))
                targetGuardianName = GetNodeString(guardian["canonicalName"]) ?? GetNodeString(guardian["name"]) ?? targetGuardianId;

            var targetAbodeName = GetNodeString(abode["name"]) ?? targetAbodeId;
            var targetAbodePower = AbodePowerRules.GetCurrentPower(guardian);
            presentCounts.TryGetValue($"{targetGuardianId}::{targetAbodeId}", out var targetResidentCount);

            var competitionScore = ScoreTransferCompetition(
                resident,
                sourceAbodePower,
                targetAbodePower,
                targetResidentCount);
            candidates.Add(new ResidentTransferCompetitionCandidate
            {
                TargetGuardianId = targetGuardianId,
                TargetGuardianName = targetGuardianName,
                TargetAbodeId = targetAbodeId,
                TargetAbodeName = targetAbodeName,
                SourceAbodePower = sourceAbodePower,
                TargetAbodePower = targetAbodePower,
                TargetResidentCount = targetResidentCount,
                CompetitionScore = competitionScore,
                CompetitionLabel = ResolveTransferCompetitionLabel(competitionScore),
                CompetitionReason = BuildTransferCompetitionReason(
                    resident,
                    sourceAbodePower,
                    targetAbodePower,
                    targetResidentCount,
                    competitionScore)
            });
        }

        return candidates
            .OrderByDescending(candidate => candidate.CompetitionScore)
            .ThenByDescending(candidate => candidate.TargetAbodePower)
            .ThenByDescending(candidate => candidate.TargetResidentCount)
            .ThenBy(candidate => candidate.TargetGuardianName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(candidate => candidate.TargetAbodeName, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public static Dictionary<string, int> CollectGuardianAbodePowerById(JsonObject? guardiansRoot)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (guardiansRoot?["guardians"] is not JsonArray guardians)
            return result;

        foreach (var guardian in guardians.OfType<JsonObject>())
        {
            var guardianId = GetNodeString(guardian["guardianId"]) ?? GetNodeString(guardian["id"]);
            if (string.IsNullOrWhiteSpace(guardianId))
                continue;

            result[guardianId] = AbodePowerRules.GetCurrentPower(guardian);
        }

        return result;
    }

    public static ResidentAbodeDriftContext BuildCanonicalDriftContext(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentTurnResidentsRoot,
        JsonObject? preTurnResident,
        JsonObject currentResident,
        IReadOnlyDictionary<string, int> previousGuardianPowerById,
        IReadOnlyDictionary<string, int> currentGuardianPowerById,
        IReadOnlyDictionary<string, Dictionary<string, string>> previousQuestFingerprintsByResident,
        IReadOnlyDictionary<string, Dictionary<string, string>> currentQuestFingerprintsByResident)
    {
        var residentId = GetNodeString(currentResident["residentId"]) ?? string.Empty;
        var guardianId = GetNodeString(currentResident["guardianId"]) ?? string.Empty;
        var currentRewardState = GetNodeString(currentResident["bondRewardState"]);
        var previousRewardState = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["bondRewardState"]);
        var currentGrantedRelicId = GetNodeString(currentResident["grantedRelicId"]);
        var previousGrantedRelicId = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["grantedRelicId"]);
        var currentLinkedSoulQuestId = GetNodeString(currentResident["linkedSoulQuestId"]);
        var previousLinkedSoulQuestId = preTurnResident == null ? string.Empty : GetNodeString(preTurnResident["linkedSoulQuestId"]);

        var hasResidentUpdate = CurrentTurnTouchesResidentArray(currentTurnResidentsRoot[UpdateProperty], residentId);
        var hasThoughtUpdate = CurrentTurnTouchesResidentArray(currentTurnResidentsRoot[UpdateThoughtJournalProperty], residentId);
        var hasInteractionLogUpdate = CurrentTurnTouchesResidentArray(currentTurnResidentsRoot[UpdateInteractionLogProperty], residentId);
        var hasHistoryLogUpdate = CurrentTurnTouchesResidentArray(currentTurnResidentsRoot[UpdateHistoryLogProperty], residentId);
        var changedReceipts = CollectChangedResidentInteractionReceipts(preTurnResidentsRoot, currentTurnResidentsRoot, residentId);

        var changedResidentQuest = !string.IsNullOrWhiteSpace(currentLinkedSoulQuestId) &&
                                   !string.Equals(currentLinkedSoulQuestId, previousLinkedSoulQuestId, StringComparison.OrdinalIgnoreCase);
        if (!changedResidentQuest &&
            !string.IsNullOrWhiteSpace(residentId) &&
            currentQuestFingerprintsByResident.TryGetValue(residentId, out var currentQuestFingerprints))
        {
            var previousQuestFingerprints = previousQuestFingerprintsByResident.TryGetValue(residentId, out var prevQuestSet)
                ? prevQuestSet
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            changedResidentQuest = currentQuestFingerprints.Any(pair =>
                !previousQuestFingerprints.TryGetValue(pair.Key, out var previousFingerprint) ||
                !string.Equals(previousFingerprint, pair.Value, StringComparison.Ordinal));
        }

        var rewardAdvanced =
            (string.Equals(currentRewardState, RewardStateGranted, StringComparison.OrdinalIgnoreCase) ||
             string.Equals(currentRewardState, RewardStateConsumed, StringComparison.OrdinalIgnoreCase)) &&
            (!string.Equals(currentRewardState, previousRewardState, StringComparison.OrdinalIgnoreCase) ||
             !string.Equals(currentGrantedRelicId, previousGrantedRelicId, StringComparison.OrdinalIgnoreCase));

        var previousDevotionLevel = preTurnResident == null ? GetNodeInt(currentResident["abodeDevotionLevel"]) : GetNodeInt(preTurnResident["abodeDevotionLevel"]);
        var currentDevotionLevel = GetNodeInt(currentResident["abodeDevotionLevel"]);
        var previousRestlessness = preTurnResident == null ? GetNodeInt(currentResident["restlessness"]) : GetNodeInt(preTurnResident["restlessness"]);
        var currentRestlessness = GetNodeInt(currentResident["restlessness"]);

        var touchesResidentTurnSurface =
            hasResidentUpdate ||
            hasThoughtUpdate ||
            hasInteractionLogUpdate ||
            hasHistoryLogUpdate ||
            changedReceipts.Count > 0 ||
            changedResidentQuest ||
            rewardAdvanced;

        var hasAcceptedTalkScene = false;
        var hasAcceptedHistoryReveal = false;
        var hasRejectedResidentScene = false;
        foreach (var receipt in changedReceipts)
        {
            var status = GetNodeString(receipt["status"]);
            var interactionType = GetNodeString(receipt["interactionType"]);
            var responseMode = GetNodeString(receipt["responseMode"]);

            if (string.Equals(status, InteractionStatusRejected, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(responseMode, ResponseModeHistoryRefused, StringComparison.OrdinalIgnoreCase))
            {
                hasRejectedResidentScene = true;
                continue;
            }

            if (!string.Equals(status, InteractionStatusAccepted, StringComparison.OrdinalIgnoreCase))
                continue;

            if (string.Equals(interactionType, InteractionTypeTalk, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(responseMode, ResponseModeTalkScene, StringComparison.OrdinalIgnoreCase))
            {
                hasAcceptedTalkScene = true;
            }

            if (string.Equals(responseMode, ResponseModeHistoryRevealed, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(responseMode, ResponseModeHistoryPartial, StringComparison.OrdinalIgnoreCase))
            {
                hasAcceptedHistoryReveal = true;
            }
        }

        int? previousAbodePower = previousGuardianPowerById.TryGetValue(guardianId, out var previousPowerValue)
            ? previousPowerValue
            : null;
        int? currentAbodePower = currentGuardianPowerById.TryGetValue(guardianId, out var currentPowerValue)
            ? currentPowerValue
            : null;
        var resolvedPreviousAbodePower = ResolveCurrentAbodePower(previousAbodePower);
        var resolvedCurrentAbodePower = ResolveCurrentAbodePower(currentAbodePower);
        var previousTierRank = GetAbodePowerTierRank(resolvedPreviousAbodePower);
        var currentTierRank = GetAbodePowerTierRank(resolvedCurrentAbodePower);
        var hasPowerTierRise = touchesResidentTurnSurface && currentTierRank > previousTierRank;
        var hasPowerTierDecline = touchesResidentTurnSurface && currentTierRank < previousTierRank;

        var hasExplicitMemoryScene =
            (hasResidentUpdate || hasThoughtUpdate || hasInteractionLogUpdate || hasHistoryLogUpdate) &&
            (hasThoughtUpdate || hasInteractionLogUpdate) &&
            !hasAcceptedTalkScene &&
            !hasAcceptedHistoryReveal &&
            !hasRejectedResidentScene &&
            !changedResidentQuest &&
            !rewardAdvanced &&
            !hasPowerTierRise &&
            !hasPowerTierDecline &&
            (currentDevotionLevel != previousDevotionLevel || currentRestlessness != previousRestlessness);

        return new ResidentAbodeDriftContext
        {
            TouchesResidentTurnSurface = touchesResidentTurnSurface,
            PreviousAbodePower = resolvedPreviousAbodePower,
            CurrentAbodePower = resolvedCurrentAbodePower,
            HasPowerTierRise = hasPowerTierRise,
            HasPowerTierDecline = hasPowerTierDecline,
            HasAcceptedTalkScene = hasAcceptedTalkScene,
            HasAcceptedHistoryReveal = hasAcceptedHistoryReveal,
            HasRejectedResidentScene = hasRejectedResidentScene,
            HasQuestProgress = changedResidentQuest,
            HasRewardFulfilled = rewardAdvanced,
            HasExplicitMemoryScene = hasExplicitMemoryScene,
            ExplicitSceneLeansPositive = hasExplicitMemoryScene &&
                                         (currentDevotionLevel > previousDevotionLevel || currentRestlessness < previousRestlessness),
            ExplicitSceneLeansNegative = hasExplicitMemoryScene &&
                                         (currentDevotionLevel < previousDevotionLevel || currentRestlessness > previousRestlessness)
        };
    }

    public static ResidentAbodeDriftProjection ProjectCanonicalAbodeDrift(
        JsonObject previousResident,
        JsonObject currentResident,
        ResidentAbodeDriftContext context)
    {
        var previousSnapshot = previousResident.DeepClone().AsObject();
        var currentSnapshot = currentResident.DeepClone().AsObject();
        NormalizeResidentObject(previousSnapshot, context.PreviousAbodePower);
        NormalizeResidentObject(currentSnapshot, context.CurrentAbodePower);

        var previousDevotionLevel = GetNodeInt(previousSnapshot["abodeDevotionLevel"]);
        var previousRestlessness = GetNodeInt(previousSnapshot["restlessness"]);
        if (!context.HasCanonicalTrigger)
        {
            return new ResidentAbodeDriftProjection
            {
                HasCanonicalTrigger = false,
                AbodeDevotionLevel = previousDevotionLevel,
                AbodeDevotionTier = ResolveAbodeDevotionTier(previousDevotionLevel),
                Restlessness = previousRestlessness,
                MigrationState = ResolveMigrationState(previousDevotionLevel, previousRestlessness)
            };
        }

        var deltaDevotion = 0;
        var deltaRestlessness = 0;
        var triggerTokens = new List<string>();

        if (context.HasPowerTierRise || context.HasPowerTierDecline)
        {
            ApplyPowerPressureDelta(currentSnapshot, context, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add(context.HasPowerTierRise ? "abode_power_rise" : "abode_power_decline");
        }

        if (context.HasAcceptedTalkScene)
        {
            ApplyPositiveResidentSceneDelta(currentSnapshot, 2, 1, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add("resident_comforted");
        }

        if (context.HasAcceptedHistoryReveal)
        {
            ApplyPositiveResidentSceneDelta(currentSnapshot, 3, 2, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add("resident_recognized");
        }

        if (context.HasRejectedResidentScene)
        {
            ApplyNegativeResidentSceneDelta(currentSnapshot, 3, 3, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add("resident_rejected");
        }

        if (context.HasQuestProgress)
        {
            ApplyPositiveResidentSceneDelta(currentSnapshot, 3, 1, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add("resident_quest_progress");
        }

        if (context.HasRewardFulfilled)
        {
            ApplyPositiveResidentSceneDelta(currentSnapshot, 4, 2, ref deltaDevotion, ref deltaRestlessness);
            triggerTokens.Add("resident_reward_fulfilled");
        }

        if (context.HasExplicitMemoryScene)
        {
            if (context.ExplicitSceneLeansPositive)
            {
                ApplyPositiveResidentSceneDelta(currentSnapshot, 2, 1, ref deltaDevotion, ref deltaRestlessness);
                triggerTokens.Add("resident_recognized");
            }
            else if (context.ExplicitSceneLeansNegative)
            {
                ApplyNegativeResidentSceneDelta(currentSnapshot, 2, 2, ref deltaDevotion, ref deltaRestlessness);
                triggerTokens.Add("resident_neglected");
            }
        }

        deltaDevotion = Math.Clamp(deltaDevotion, -8, 8);
        deltaRestlessness = Math.Clamp(deltaRestlessness, -8, 8);

        var abodeDevotionLevel = ClampResidentMeter(previousDevotionLevel + deltaDevotion);
        var restlessness = ClampResidentMeter(previousRestlessness + deltaRestlessness);
        return new ResidentAbodeDriftProjection
        {
            HasCanonicalTrigger = true,
            AbodeDevotionLevel = abodeDevotionLevel,
            AbodeDevotionTier = ResolveAbodeDevotionTier(abodeDevotionLevel),
            Restlessness = restlessness,
            MigrationState = ResolveMigrationState(abodeDevotionLevel, restlessness),
            TriggerSummary = string.Join(", ", triggerTokens.Distinct(StringComparer.OrdinalIgnoreCase))
        };
    }

    public static void ApplyAbodeDriftProjection(JsonObject resident, ResidentAbodeDriftProjection projection)
    {
        resident["abodeDevotionLevel"] = projection.AbodeDevotionLevel;
        resident["abodeDevotionTier"] = projection.AbodeDevotionTier;
        resident["restlessness"] = projection.Restlessness;
        resident["migrationState"] = projection.MigrationState;
    }

    public static bool HasInteractionReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .Any(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasRosterReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .Any(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasTransferReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .Any(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindRosterReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .FirstOrDefault(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindInteractionReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .FirstOrDefault(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static JsonObject? FindTransferReceipt(JsonArray receipts, string requestId)
    {
        return receipts.OfType<JsonObject>()
            .FirstOrDefault(receipt => string.Equals(GetNodeString(receipt["requestId"]), requestId, StringComparison.OrdinalIgnoreCase));
    }

    public static bool HasHistoryLogEntry(JsonArray historyLog, string entryId)
    {
        return historyLog.OfType<JsonObject>()
            .Any(entry => string.Equals(GetNodeString(entry["entryId"]), entryId, StringComparison.OrdinalIgnoreCase));
    }

    private static string NormalizeRewardState(string? rewardState) =>
        (rewardState ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            RewardStateEligible => RewardStateEligible,
            RewardStateGranted => RewardStateGranted,
            RewardStateConsumed => RewardStateConsumed,
            _ => RewardStateNone
        };

    private static string NormalizeInteractionType(string? interactionType) =>
        (interactionType ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            InteractionTypeTalk => InteractionTypeTalk,
            InteractionTypeHistory => InteractionTypeHistory,
            _ => string.Empty
        };

    private static string NormalizeInteractionStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            InteractionStatusAccepted => InteractionStatusAccepted,
            InteractionStatusRejected => InteractionStatusRejected,
            InteractionStatusCancelled => InteractionStatusCancelled,
            _ => string.Empty
        };

    private static string NormalizeTransferStatus(string? status) =>
        (status ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferStatusAccepted => TransferStatusAccepted,
            TransferStatusRefused => TransferStatusRefused,
            TransferStatusDepartedOnly => TransferStatusDepartedOnly,
            _ => string.Empty
        };

    private static string NormalizeTransferMode(string? transferMode) =>
        (transferMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            TransferModeDepartureOnly => TransferModeDepartureOnly,
            TransferModeAcceptedTransfer => TransferModeAcceptedTransfer,
            TransferModeRefusedTransfer => TransferModeRefusedTransfer,
            _ => string.Empty
        };

    private static string NormalizeResponseMode(string? responseMode) =>
        (responseMode ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            ResponseModeTalkScene => ResponseModeTalkScene,
            ResponseModeHistoryRevealed => ResponseModeHistoryRevealed,
            ResponseModeHistoryRefused => ResponseModeHistoryRefused,
            ResponseModeHistoryPartial => ResponseModeHistoryPartial,
            ResponseModeBondShiftOnly => ResponseModeBondShiftOnly,
            _ => string.Empty
        };

    private static bool IsPresent(JsonElement resident) =>
        !resident.TryGetProperty("isPresent", out var isPresent) ||
        isPresent.ValueKind != JsonValueKind.False;

    private static void UpsertInteractionReceipt(JsonArray receipts, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            receipts[i] = receipt.DeepClone();
            return;
        }

        receipts.Add(receipt.DeepClone());
    }

    private static void UpsertRosterReceipt(JsonArray receipts, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            receipts[i] = receipt.DeepClone();
            return;
        }

        receipts.Add(receipt.DeepClone());
    }

    private static void UpsertTransferReceipt(JsonArray receipts, JsonObject receipt)
    {
        var requestId = GetNodeString(receipt["requestId"]);
        if (string.IsNullOrWhiteSpace(requestId))
            return;

        for (var i = 0; i < receipts.Count; i++)
        {
            if (receipts[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["requestId"]), requestId, StringComparison.OrdinalIgnoreCase))
                continue;

            receipts[i] = receipt.DeepClone();
            return;
        }

        receipts.Add(receipt.DeepClone());
    }

    private static void UpsertHistoryLogEntry(JsonArray historyLog, JsonObject historyEntry)
    {
        var entryId = GetNodeString(historyEntry["entryId"]);
        if (string.IsNullOrWhiteSpace(entryId))
            return;

        for (var i = 0; i < historyLog.Count; i++)
        {
            if (historyLog[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["entryId"]), entryId, StringComparison.OrdinalIgnoreCase))
                continue;

            historyLog[i] = historyEntry.DeepClone();
            return;
        }

        historyLog.Add(historyEntry.DeepClone());
    }

    private static void UpsertJournalEntry(JsonArray entries, JsonObject journalEntry)
    {
        var entryId = GetNodeString(journalEntry["entryId"]);
        if (string.IsNullOrWhiteSpace(entryId))
            return;

        for (var i = 0; i < entries.Count; i++)
        {
            if (entries[i] is not JsonObject existing)
                continue;

            if (!string.Equals(GetNodeString(existing["entryId"]), entryId, StringComparison.OrdinalIgnoreCase))
                continue;

            entries[i] = journalEntry.DeepClone();
            return;
        }

        entries.Add(journalEntry.DeepClone());
    }

    private static IEnumerable<JsonElement> EnumerateResidentObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(EntriesProperty, out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var resident in entries.EnumerateArray())
            {
                if (resident.ValueKind == JsonValueKind.Object)
                    yield return resident;
            }
            yield break;
        }

        if (root.TryGetProperty(UpdateProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var resident in updates.EnumerateArray())
            {
                if (resident.ValueKind == JsonValueKind.Object)
                    yield return resident;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateInteractionReceiptObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(InteractionReceiptsProperty, out var receipts) && receipts.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in receipts.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }

            yield break;
        }

        if (root.TryGetProperty(UpdateInteractionReceiptsProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in updates.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateRosterReceiptObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(RosterReceiptsProperty, out var receipts) && receipts.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in receipts.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }

            yield break;
        }

        if (root.TryGetProperty(UpdateRosterReceiptsProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in updates.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateTransferReceiptObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(TransferReceiptsProperty, out var receipts) && receipts.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in receipts.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }
            yield break;
        }

        if (root.TryGetProperty(UpdateTransferReceiptsProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var receipt in updates.EnumerateArray())
            {
                if (receipt.ValueKind == JsonValueKind.Object)
                    yield return receipt;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateHistoryLogObjects(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(HistoryLogProperty, out var historyLog) && historyLog.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in historyLog.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return entry;
            }

            yield break;
        }

        if (root.TryGetProperty(UpdateHistoryLogProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in updates.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return entry;
            }
        }
    }

    private static IEnumerable<JsonElement> EnumerateJournalObjects(JsonElement root, string propertyName, string updateProperty)
    {
        if (root.ValueKind != JsonValueKind.Object)
            yield break;

        if (root.TryGetProperty(propertyName, out var entries) && entries.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in entries.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return entry;
            }

            yield break;
        }

        if (root.TryGetProperty(updateProperty, out var updates) && updates.ValueKind == JsonValueKind.Array)
        {
            foreach (var entry in updates.EnumerateArray())
            {
                if (entry.ValueKind == JsonValueKind.Object)
                    yield return entry;
            }
        }
    }

    private static IReadOnlyList<string> ReadStringArray(JsonElement root, string propertyName)
    {
        var result = new List<string>();
        if (!root.TryGetProperty(propertyName, out var array) || array.ValueKind != JsonValueKind.Array)
            return result;

        foreach (var item in array.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
                continue;

            var value = item.GetString()?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private static void EnsureStringArray(JsonObject root, string propertyName)
    {
        if (root[propertyName] is not JsonArray arr)
            root[propertyName] = arr = new JsonArray();

        NormalizeStringArray(arr);
    }

    private static void NormalizeStringArray(JsonArray arr)
    {
        for (var i = arr.Count - 1; i >= 0; i--)
        {
            if (arr[i] is not JsonValue value || !value.TryGetValue<string>(out var text) || string.IsNullOrWhiteSpace(text))
                arr.RemoveAt(i);
            else
                arr[i] = text.Trim();
        }
    }

    private static IReadOnlyList<string> ReadNodeStringArray(JsonNode? node)
    {
        var result = new List<string>();
        if (node is not JsonArray array)
            return result;

        foreach (var item in array)
        {
            var value = GetNodeString(item)?.Trim();
            if (!string.IsNullOrWhiteSpace(value))
                result.Add(value);
        }

        return result;
    }

    private static IReadOnlyList<ResidentPersonalityTrait> ReadPersonalityTraits(JsonNode? node)
    {
        var result = new List<ResidentPersonalityTrait>();
        if (node is not JsonArray traits)
            return result;

        foreach (var item in traits.OfType<JsonObject>())
        {
            var traitName = GetNodeString(item["traitName"]);
            if (string.IsNullOrWhiteSpace(traitName))
                continue;

            result.Add(new ResidentPersonalityTrait
            {
                TraitName = traitName,
                Value = Math.Clamp(GetNodeIntOrDefault(item["value"], 5), 1, 10),
                ValueDescription = GetNodeString(item["valueDescription"]) ?? string.Empty,
                Description = GetNodeString(item["description"]) ?? string.Empty
            });
        }

        return result;
    }

    private static string GetString(JsonElement root, string propertyName)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
            return string.Empty;

        return node.GetString() ?? string.Empty;
    }

    private static string GetFirstString(JsonElement root, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.String)
                continue;

            var value = node.GetString();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return string.Empty;
    }

    private static int GetInt(JsonElement root, string propertyName, int fallback)
    {
        if (!root.TryGetProperty(propertyName, out var node) || node.ValueKind != JsonValueKind.Number || !node.TryGetInt32(out var value))
            return fallback;

        return value;
    }

    private static string? GetNodeString(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<string>(out var text))
            return text;

        return null;
    }

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return 0;
    }

    private static int GetNodeIntOrDefault(JsonNode? node, int fallback)
    {
        if (node is JsonValue value && value.TryGetValue<int>(out var number))
            return number;

        return fallback;
    }

    private static bool GetNodeBool(JsonNode? node, bool fallback)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var flag))
            return flag;

        return fallback;
    }

    private static string NormalizeNonEmptyString(string? value, string fallback)
    {
        var normalized = value?.Trim();
        return string.IsNullOrWhiteSpace(normalized) ? fallback : normalized;
    }

    private static int ClampResidentMeter(int value) => Math.Clamp(value, 0, 100);

    private static bool CurrentTurnTouchesResidentArray(JsonNode? node, string residentId)
    {
        if (string.IsNullOrWhiteSpace(residentId) || node is not JsonArray entries)
            return false;

        return entries.OfType<JsonObject>().Any(entry =>
            string.Equals(GetNodeString(entry["residentId"]), residentId, StringComparison.OrdinalIgnoreCase));
    }

    private static List<JsonObject> CollectChangedResidentInteractionReceipts(
        JsonObject? preTurnResidentsRoot,
        JsonObject currentTurnResidentsRoot,
        string residentId)
    {
        var previousFingerprints = preTurnResidentsRoot == null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : CollectResidentInteractionReceiptFingerprints(preTurnResidentsRoot, residentId);
        var currentReceipts = CollectResidentInteractionReceiptObjects(currentTurnResidentsRoot, residentId);
        var changed = new List<JsonObject>();
        foreach (var receipt in currentReceipts)
        {
            var requestId = GetNodeString(receipt["requestId"]);
            if (string.IsNullOrWhiteSpace(requestId))
                continue;

            var fingerprint = receipt.ToJsonString();
            if (!previousFingerprints.TryGetValue(requestId, out var previousFingerprint) ||
                !string.Equals(previousFingerprint, fingerprint, StringComparison.Ordinal))
            {
                changed.Add(receipt);
            }
        }

        return changed;
    }

    private static Dictionary<string, string> CollectResidentInteractionReceiptFingerprints(JsonObject root, string residentId)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var receipt in CollectResidentInteractionReceiptObjects(root, residentId))
        {
            var requestId = GetNodeString(receipt["requestId"]);
            if (string.IsNullOrWhiteSpace(requestId))
                continue;

            result[requestId] = receipt.ToJsonString();
        }

        return result;
    }

    private static List<JsonObject> CollectResidentInteractionReceiptObjects(JsonObject root, string residentId)
    {
        var result = new List<JsonObject>();
        if (string.IsNullOrWhiteSpace(residentId) || root[InteractionReceiptsProperty] is not JsonArray receipts)
            return result;

        foreach (var receipt in receipts.OfType<JsonObject>())
        {
            if (string.Equals(GetNodeString(receipt["residentId"]), residentId, StringComparison.OrdinalIgnoreCase))
                result.Add(receipt.DeepClone().AsObject());
        }

        return result;
    }

    private static int GetAbodePowerTierRank(int currentPower) => AbodePowerRules.ClampCurrentPower(currentPower) switch
    {
        <= 19 => 0,
        <= 39 => 1,
        <= 59 => 2,
        <= 79 => 3,
        _ => 4
    };

    private static void ApplyPowerPressureDelta(
        JsonObject currentResident,
        ResidentAbodeDriftContext context,
        ref int deltaDevotion,
        ref int deltaRestlessness)
    {
        var tierSteps = Math.Abs(GetAbodePowerTierRank(context.CurrentAbodePower) - GetAbodePowerTierRank(context.PreviousAbodePower));
        if (tierSteps <= 0)
            return;

        var powerSensitivity = GetResidentPowerSensitivity(currentResident);
        var communalOrientation = GetResidentCommunalOrientation(currentResident);
        var stabilityNeed = GetResidentStabilityNeed(currentResident);
        var migrationDisposition = GetResidentMigrationDisposition(currentResident);

        if (context.HasPowerTierRise)
        {
            deltaDevotion += tierSteps;
            deltaRestlessness -= tierSteps;

            if (string.Equals(powerSensitivity, PowerSensitivityHigh, StringComparison.OrdinalIgnoreCase))
            {
                deltaDevotion += tierSteps;
                deltaRestlessness -= 1;
            }
            else if (string.Equals(powerSensitivity, PowerSensitivityLow, StringComparison.OrdinalIgnoreCase))
            {
                deltaDevotion -= 1;
            }

            if (string.Equals(communalOrientation, CommunalOrientationHigh, StringComparison.OrdinalIgnoreCase))
                deltaDevotion += 1;

            if (string.Equals(migrationDisposition, MigrationDispositionRooted, StringComparison.OrdinalIgnoreCase))
                deltaRestlessness -= 1;
            else if (string.Equals(migrationDisposition, MigrationDispositionWandering, StringComparison.OrdinalIgnoreCase))
                deltaDevotion -= 1;
        }

        if (context.HasPowerTierDecline)
        {
            deltaDevotion -= tierSteps;
            deltaRestlessness += tierSteps;

            if (string.Equals(powerSensitivity, PowerSensitivityHigh, StringComparison.OrdinalIgnoreCase))
            {
                deltaDevotion -= tierSteps;
                deltaRestlessness += 1;
            }

            if (string.Equals(stabilityNeed, StabilityNeedHigh, StringComparison.OrdinalIgnoreCase))
            {
                deltaDevotion -= 1;
                deltaRestlessness += 1;
            }
            else if (string.Equals(stabilityNeed, StabilityNeedLow, StringComparison.OrdinalIgnoreCase))
            {
                deltaDevotion += 1;
                deltaRestlessness -= 1;
            }

            if (string.Equals(migrationDisposition, MigrationDispositionRooted, StringComparison.OrdinalIgnoreCase))
                deltaRestlessness -= 1;
            else if (string.Equals(migrationDisposition, MigrationDispositionOpportunistic, StringComparison.OrdinalIgnoreCase) ||
                     string.Equals(migrationDisposition, MigrationDispositionWandering, StringComparison.OrdinalIgnoreCase))
                deltaRestlessness += 1;

            ApplyBondProtectionDelta(currentResident, ref deltaDevotion, ref deltaRestlessness);
        }
    }

    private static void ApplyPositiveResidentSceneDelta(
        JsonObject currentResident,
        int baseDevotion,
        int baseRestlessnessReduction,
        ref int deltaDevotion,
        ref int deltaRestlessness)
    {
        deltaDevotion += baseDevotion;
        deltaRestlessness -= baseRestlessnessReduction;

        var communalOrientation = GetResidentCommunalOrientation(currentResident);
        if (string.Equals(communalOrientation, CommunalOrientationHigh, StringComparison.OrdinalIgnoreCase))
            deltaDevotion += 1;
        else if (string.Equals(communalOrientation, CommunalOrientationLow, StringComparison.OrdinalIgnoreCase))
            deltaDevotion -= 1;

        var migrationDisposition = GetResidentMigrationDisposition(currentResident);
        if (string.Equals(migrationDisposition, MigrationDispositionRooted, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(migrationDisposition, MigrationDispositionSelective, StringComparison.OrdinalIgnoreCase))
        {
            deltaRestlessness -= 1;
        }
        else if (string.Equals(migrationDisposition, MigrationDispositionWandering, StringComparison.OrdinalIgnoreCase))
        {
            deltaRestlessness += 1;
        }

        var bondLevel = ClampResidentMeter(GetNodeInt(currentResident["bondLevel"]));
        if (bondLevel >= 75)
            deltaDevotion += 1;
    }

    private static void ApplyNegativeResidentSceneDelta(
        JsonObject currentResident,
        int baseDevotionLoss,
        int baseRestlessnessGain,
        ref int deltaDevotion,
        ref int deltaRestlessness)
    {
        deltaDevotion -= baseDevotionLoss;
        deltaRestlessness += baseRestlessnessGain;

        var migrationDisposition = GetResidentMigrationDisposition(currentResident);
        if (string.Equals(migrationDisposition, MigrationDispositionOpportunistic, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(migrationDisposition, MigrationDispositionWandering, StringComparison.OrdinalIgnoreCase))
        {
            deltaRestlessness += 1;
        }

        var communalOrientation = GetResidentCommunalOrientation(currentResident);
        if (string.Equals(communalOrientation, CommunalOrientationHigh, StringComparison.OrdinalIgnoreCase))
            deltaDevotion += 1;

        ApplyBondProtectionDelta(currentResident, ref deltaDevotion, ref deltaRestlessness);
    }

    private static void ApplyBondProtectionDelta(
        JsonObject currentResident,
        ref int deltaDevotion,
        ref int deltaRestlessness)
    {
        var bondLevel = ClampResidentMeter(GetNodeInt(currentResident["bondLevel"]));
        if (bondLevel >= 75)
        {
            deltaDevotion += 1;
            deltaRestlessness -= 1;
        }
        else if (bondLevel <= 24)
        {
            deltaDevotion -= 1;
            deltaRestlessness += 1;
        }
    }

    private static string GetResidentPowerSensitivity(JsonObject resident)
    {
        var abodeDisposition = resident["abodeDisposition"] as JsonObject;
        return NormalizePowerSensitivity(GetNodeString(abodeDisposition?["powerSensitivity"]), PowerSensitivityMedium);
    }

    private static string GetResidentMigrationDisposition(JsonObject resident)
    {
        var abodeDisposition = resident["abodeDisposition"] as JsonObject;
        return NormalizeMigrationDisposition(GetNodeString(abodeDisposition?["migrationDisposition"]));
    }

    private static string GetResidentCommunalOrientation(JsonObject resident)
    {
        var abodeDisposition = resident["abodeDisposition"] as JsonObject;
        return NormalizeCommunalOrientation(GetNodeString(abodeDisposition?["communalOrientation"]), CommunalOrientationMedium);
    }

    private static string GetResidentStabilityNeed(JsonObject resident)
    {
        var abodeDisposition = resident["abodeDisposition"] as JsonObject;
        return NormalizeStabilityNeed(GetNodeString(abodeDisposition?["stabilityNeed"]), StabilityNeedMedium);
    }

    private static Dictionary<string, int> CollectPresentResidentCountsByAbode(JsonObject? residentsRoot)
    {
        var result = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (residentsRoot?[EntriesProperty] is not JsonArray entries)
            return result;

        foreach (var resident in entries.OfType<JsonObject>())
        {
            NormalizeResidentObject(resident);
            if (!GetNodeBool(resident["isPresent"], true))
                continue;

            var guardianId = GetNodeString(resident["guardianId"]);
            var abodeId = GetNodeString(resident["abodeId"]);
            if (string.IsNullOrWhiteSpace(guardianId) || string.IsNullOrWhiteSpace(abodeId))
                continue;

            var key = $"{guardianId}::{abodeId}";
            result[key] = result.TryGetValue(key, out var currentCount)
                ? currentCount + 1
                : 1;
        }

        return result;
    }

    private static int ScoreTransferCompetition(
        ResidentEntry resident,
        int sourceAbodePower,
        int targetAbodePower,
        int targetResidentCount)
    {
        var score = 40;
        score += GetLeavePressureBonus(resident.AbodeDevotionLevel, resident.Restlessness);
        score += GetPowerCompetitionDelta(targetAbodePower - sourceAbodePower, resident.AbodeDisposition.PowerSensitivity);
        score += GetStabilityCompetitionBonus(sourceAbodePower, targetAbodePower, resident.AbodeDisposition.StabilityNeed);
        score += GetCommunityCompetitionBonus(targetResidentCount, resident.AbodeDisposition.CommunalOrientation);
        score += GetMigrationDispositionCompetitionBonus(resident.AbodeDisposition.MigrationDisposition);
        score -= GetBondReluctancePenalty(resident.BondLevel);
        return Math.Clamp(score, 0, 100);
    }

    private static string ResolveTransferCompetitionLabel(int competitionScore) => Math.Clamp(competitionScore, 0, 100) switch
    {
        >= 70 => TransferCompetitionLabelStrongPull,
        >= 50 => TransferCompetitionLabelPlausiblePull,
        _ => TransferCompetitionLabelWeakPull
    };

    private static string BuildTransferCompetitionReason(
        ResidentEntry resident,
        int sourceAbodePower,
        int targetAbodePower,
        int targetResidentCount,
        int competitionScore)
    {
        var positive = new List<string>();
        var resistance = new List<string>();
        var powerDelta = targetAbodePower - sourceAbodePower;
        if (powerDelta >= 10)
            positive.Add($"цель заметно сильнее текущей Обители ({targetAbodePower}/100 против {sourceAbodePower}/100)");
        else if (powerDelta >= 4)
            positive.Add("новая Обитель выглядит чуть сильнее текущего дома");

        if (GetStabilityCompetitionBonus(sourceAbodePower, targetAbodePower, resident.AbodeDisposition.StabilityNeed) >= 5)
            positive.Add("она обещает более устойчивый порядок");

        var communityBonus = GetCommunityCompetitionBonus(targetResidentCount, resident.AbodeDisposition.CommunalOrientation);
        if (communityBonus >= 4)
            positive.Add(targetResidentCount > 1 ? "там уже есть живая община" : "там уже есть хотя бы один живой узел общины");
        else if (communityBonus < 0)
            resistance.Add("пустая Обитель плохо подходит его потребности в общине");

        if (GetMigrationDispositionCompetitionBonus(resident.AbodeDisposition.MigrationDisposition) >= 6)
            positive.Add("сам характер резидента тянется к перемене");

        if (GetLeavePressureBonus(resident.AbodeDevotionLevel, resident.Restlessness) >= 6)
            positive.Add("внутреннее давление ухода уже велико");

        if (GetBondReluctancePenalty(resident.BondLevel) >= 4)
            resistance.Add("связь с нынешним Хранителем всё ещё удерживает");

        if (positive.Count == 0)
            positive.Add("явного системного притяжения почти нет");

        var summary = char.ToUpperInvariant(positive[0][0]) + positive[0][1..];
        if (positive.Count > 1)
            summary += $", {string.Join(", ", positive.Skip(1).Take(1))}";

        if (resistance.Count > 0)
            return $"{summary}; но {string.Join(" и ", resistance.Take(2))}.";

        return competitionScore >= 50
            ? $"{summary}."
            : $"{summary}.";
    }

    private static int GetLeavePressureBonus(int abodeDevotionLevel, int restlessness) =>
        Math.Clamp((Math.Clamp(restlessness, 0, 100) - Math.Clamp(abodeDevotionLevel, 0, 100)) / 8, 0, 12);

    private static int GetPowerCompetitionDelta(int powerDelta, string? powerSensitivity)
    {
        var divisor = NormalizePowerSensitivity(powerSensitivity, PowerSensitivityMedium) switch
        {
            PowerSensitivityHigh => 6,
            PowerSensitivityLow => 14,
            _ => 9
        };
        return Math.Clamp(powerDelta / divisor, -16, 16);
    }

    private static int GetStabilityCompetitionBonus(int sourceAbodePower, int targetAbodePower, string? stabilityNeed)
    {
        if (sourceAbodePower >= 40 || targetAbodePower < 40)
            return 0;

        var bonus = NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
        {
            StabilityNeedHigh => 8,
            StabilityNeedLow => 2,
            _ => 5
        };

        if (targetAbodePower >= 60)
            bonus += 2;

        return bonus;
    }

    private static int GetCommunityCompetitionBonus(int targetResidentCount, string? communalOrientation)
    {
        return NormalizeCommunalOrientation(communalOrientation, CommunalOrientationMedium) switch
        {
            CommunalOrientationHigh => targetResidentCount switch
            {
                <= 0 => -6,
                1 => 4,
                <= 3 => 7,
                _ => 8
            },
            CommunalOrientationLow => targetResidentCount switch
            {
                <= 0 => 0,
                1 => 1,
                <= 3 => 2,
                _ => 3
            },
            _ => targetResidentCount switch
            {
                <= 0 => -3,
                1 => 2,
                <= 3 => 4,
                _ => 5
            }
        };
    }

    private static int GetMigrationDispositionCompetitionBonus(string? migrationDisposition) =>
        NormalizeMigrationDisposition(migrationDisposition, MigrationDispositionSelective) switch
        {
            MigrationDispositionRooted => -10,
            MigrationDispositionOpportunistic => 6,
            MigrationDispositionWandering => 10,
            _ => 0
        };

    private static int GetBondReluctancePenalty(int bondLevel) =>
        Math.Clamp((Math.Clamp(bondLevel, 0, 100) - 30) / 10, 0, 7);

    private static int ResolveCurrentAbodePower(int? currentAbodePower) =>
        currentAbodePower.HasValue
            ? AbodePowerRules.ClampCurrentPower(currentAbodePower.Value)
            : AbodePowerRules.DefaultCurrentPower;

    private static string NormalizePowerSensitivity(string? value, string fallback) =>
        NormalizeScaleValue(value, AllowedPowerSensitivityValues, fallback);

    private static string NormalizeCommunalOrientation(string? value, string fallback) =>
        NormalizeScaleValue(value, AllowedCommunalOrientationValues, fallback);

    private static string NormalizeStabilityNeed(string? value, string fallback) =>
        NormalizeScaleValue(value, AllowedStabilityNeedValues, fallback);

    private static string NormalizeTernaryScaleValue(string? value) =>
        NormalizeScaleValue(value, AllowedPowerSensitivityValues, PowerSensitivityMedium);

    private static string NormalizeMigrationDisposition(string? value, string fallback = MigrationDispositionSelective)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return AllowedMigrationDispositionValues.Contains(normalized) ? normalized : fallback;
    }

    private static string NormalizeScaleValue(string? value, HashSet<string> allowedValues, string fallback)
    {
        var normalized = (value ?? string.Empty).Trim().ToLowerInvariant();
        return allowedValues.Contains(normalized) ? normalized : fallback;
    }

    private static IReadOnlyList<string> CollectSeedKeywords(ResidentSeedContext seed)
    {
        var result = new List<string>();
        result.AddRange(seed.CoreTraits.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant()));
        result.AddRange(seed.ArchetypeHints.Where(value => !string.IsNullOrWhiteSpace(value)).Select(value => value.Trim().ToLowerInvariant()));
        foreach (var candidate in new[] { seed.RoleLabel, seed.Summary, seed.BondReason, seed.OriginWorldSummary })
        {
            if (!string.IsNullOrWhiteSpace(candidate))
                result.Add(candidate.Trim().ToLowerInvariant());
        }

        return result;
    }

    private static bool ContainsAny(string source, params string[] fragments) =>
        fragments.Any(fragment => source.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string NormalizeCoreValue(string raw)
    {
        var normalized = raw.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        normalized = normalized.Replace('_', ' ').Replace('-', ' ');
        return normalized.ToLowerInvariant() switch
        {
            "верность" => "loyalty",
            "долг" => "duty",
            "память" => "memory",
            "свобода" => "freedom",
            "служение" => "service",
            "благодарность" => "gratitude",
            "рост" => "growth",
            "честность" => "honesty",
            "стойкость" => "endurance",
            _ => normalized
        };
    }

    private static string HumanizeToken(string raw)
    {
        var normalized = raw.Replace('_', ' ').Replace('-', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        return char.ToUpperInvariant(normalized[0]) + normalized[1..];
    }

    private static int GetPowerBandDevotionModifier(int currentAbodePower, string? powerSensitivity)
    {
        var bandModifier = AbodePowerRules.ClampCurrentPower(currentAbodePower) switch
        {
            <= 19 => -12,
            <= 39 => -6,
            <= 59 => 0,
            <= 79 => 5,
            _ => 9
        };

        return ScaleBandBySensitivity(bandModifier, powerSensitivity);
    }

    private static int GetPowerBandRestlessnessModifier(int currentAbodePower, string? powerSensitivity)
    {
        var bandModifier = AbodePowerRules.ClampCurrentPower(currentAbodePower) switch
        {
            <= 19 => 10,
            <= 39 => 5,
            <= 59 => 0,
            <= 79 => -3,
            _ => -6
        };

        return ScaleBandBySensitivity(bandModifier, powerSensitivity);
    }

    private static int ScaleBandBySensitivity(int modifier, string? powerSensitivity) =>
        NormalizePowerSensitivity(powerSensitivity, PowerSensitivityMedium) switch
        {
            PowerSensitivityLow => (int)Math.Round(modifier * 0.5, MidpointRounding.AwayFromZero),
            PowerSensitivityHigh => (int)Math.Round(modifier * 1.4, MidpointRounding.AwayFromZero),
            _ => modifier
        };

    private static int GetMigrationDispositionDevotionModifier(string? migrationDisposition) =>
        NormalizeMigrationDisposition(migrationDisposition) switch
        {
            MigrationDispositionRooted => 6,
            MigrationDispositionOpportunistic => -3,
            MigrationDispositionWandering => -7,
            _ => 1
        };

    private static int GetCommunalDevotionModifier(string? communalOrientation) =>
        NormalizeCommunalOrientation(communalOrientation, CommunalOrientationMedium) switch
        {
            CommunalOrientationHigh => 5,
            CommunalOrientationLow => -2,
            _ => 1
        };

    private static int GetStabilityDevotionModifier(int currentAbodePower, string? stabilityNeed)
    {
        if (currentAbodePower >= 60)
        {
            return NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
            {
                StabilityNeedHigh => 3,
                StabilityNeedLow => 1,
                _ => 2
            };
        }

        if (currentAbodePower >= 40)
            return 0;

        return NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
        {
            StabilityNeedHigh => -4,
            StabilityNeedLow => -1,
            _ => -2
        };
    }

    private static int GetMigrationDispositionRestlessnessModifier(string? migrationDisposition) =>
        NormalizeMigrationDisposition(migrationDisposition) switch
        {
            MigrationDispositionRooted => -12,
            MigrationDispositionOpportunistic => 6,
            MigrationDispositionWandering => 14,
            _ => -4
        };

    private static int GetCommunalRestlessnessModifier(string? communalOrientation) =>
        NormalizeCommunalOrientation(communalOrientation, CommunalOrientationMedium) switch
        {
            CommunalOrientationHigh => -4,
            CommunalOrientationLow => 4,
            _ => 0
        };

    private static int GetStabilityRestlessnessModifier(int currentAbodePower, string? stabilityNeed)
    {
        return AbodePowerRules.ClampCurrentPower(currentAbodePower) switch
        {
            <= 19 => NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
            {
                StabilityNeedHigh => 15,
                StabilityNeedLow => 5,
                _ => 10
            },
            <= 39 => NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
            {
                StabilityNeedHigh => 11,
                StabilityNeedLow => 3,
                _ => 7
            },
            <= 59 => 0,
            <= 79 => NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
            {
                StabilityNeedHigh => -6,
                StabilityNeedLow => -2,
                _ => -4
            },
            _ => NormalizeStabilityNeed(stabilityNeed, StabilityNeedMedium) switch
            {
                StabilityNeedHigh => -10,
                StabilityNeedLow => -4,
                _ => -7
            }
        };
    }

    private static string DescribeLoyalty(int value) => value switch
    {
        >= 8 => "Clings fiercely to chosen bonds and duties.",
        >= 5 => "Tends to stay faithful once trust is earned.",
        _ => "Keeps some distance even from meaningful ties."
    };

    private static string DescribeRestlessness(int value) => value switch
    {
        >= 8 => "Feels a constant pull toward motion or change.",
        >= 5 => "Can remain in place, but not without doubts.",
        _ => "Rarely feels the urge to drift away."
    };

    private static string DescribeBelonging(int value) => value switch
    {
        >= 8 => "Needs shared life and recognition to feel whole.",
        >= 5 => "Cares about belonging, but can endure some distance.",
        _ => "Does not easily root identity in shared community."
    };

    private static string DescribeStabilityNeed(int value) => value switch
    {
        >= 8 => "Instability cuts deeply and quickly unsettles them.",
        >= 5 => "Prefers reliable order and shelter.",
        _ => "Can tolerate uncertainty and disorder better than most."
    };
}
