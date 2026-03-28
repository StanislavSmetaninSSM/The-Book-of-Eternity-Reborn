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

    public const string ResponseModeTalkScene = "talk_scene";
    public const string ResponseModeHistoryRevealed = "history_revealed";
    public const string ResponseModeHistoryRefused = "history_refused";
    public const string ResponseModeHistoryPartial = "history_partial";
    public const string ResponseModeBondShiftOnly = "bond_shift_only";

    public const string BondTierStranger = "stranger";
    public const string BondTierFamiliar = "familiar";
    public const string BondTierTrusted = "trusted";
    public const string BondTierBound = "bound";

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

    private static readonly HashSet<string> AllowedResponseModes = new(StringComparer.OrdinalIgnoreCase)
    {
        ResponseModeTalkScene,
        ResponseModeHistoryRevealed,
        ResponseModeHistoryRefused,
        ResponseModeHistoryPartial,
        ResponseModeBondShiftOnly
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
        public bool IsPresent { get; init; } = true;
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

    public static List<ResidentEntry> CollectEntries(JsonElement root, string guardianId, string abodeId, bool presentOnly = true)
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

            var imprint = resident.TryGetProperty("mortalWorldImprint", out var imprintNode) && imprintNode.ValueKind == JsonValueKind.Object
                ? imprintNode
                : default;

            result.Add(new ResidentEntry
            {
                ResidentId = GetString(resident, "residentId"),
                GuardianId = GetString(resident, "guardianId"),
                AbodeId = GetString(resident, "abodeId"),
                DisplayName = GetString(resident, "displayName"),
                ResidentKind = GetString(resident, "residentKind"),
                OriginType = GetString(resident, "originType"),
                RoleLabel = GetString(resident, "roleLabel"),
                Summary = GetString(resident, "summary"),
                BondLevel = GetInt(resident, "bondLevel", 0),
                BondTier = GetString(resident, "bondTier"),
                CanGrantCompanionRelic = resident.TryGetProperty("canGrantCompanionRelic", out var grantProp) && grantProp.ValueKind == JsonValueKind.True,
                BondRewardState = GetString(resident, "bondRewardState"),
                LinkedSoulQuestId = GetString(resident, "linkedSoulQuestId"),
                GrantedRelicId = GetString(resident, "grantedRelicId"),
                HistoryRevealed = resident.TryGetProperty("historyRevealed", out var historyNode) && historyNode.ValueKind == JsonValueKind.True,
                OriginWorldSummary = imprint.ValueKind == JsonValueKind.Object ? GetString(imprint, "originWorldSummary") : "",
                FutureCompanionPrompt = imprint.ValueKind == JsonValueKind.Object ? GetString(imprint, "futureCompanionPrompt") : "",
                BondReason = imprint.ValueKind == JsonValueKind.Object ? GetString(imprint, "bondReason") : "",
                CoreTraits = imprint.ValueKind == JsonValueKind.Object ? ReadStringArray(imprint, "coreTraits") : Array.Empty<string>(),
                ArchetypeHints = imprint.ValueKind == JsonValueKind.Object ? ReadStringArray(imprint, "archetypeHints") : Array.Empty<string>(),
                AppearanceMotifs = imprint.ValueKind == JsonValueKind.Object ? ReadStringArray(imprint, "appearanceMotifs") : Array.Empty<string>(),
                AvailableInteractions = ReadStringArray(resident, "availableInteractions"),
                IsPresent = IsPresent(resident)
            });
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

    public static bool IsSupportedInteractionType(string? interactionType) =>
        !string.IsNullOrWhiteSpace(interactionType) && AllowedInteractionTypes.Contains(interactionType.Trim());

    public static bool IsSupportedInteractionStatus(string? status) =>
        !string.IsNullOrWhiteSpace(status) && AllowedInteractionStatuses.Contains(status.Trim());

    public static bool IsSupportedResponseMode(string? responseMode) =>
        !string.IsNullOrWhiteSpace(responseMode) && AllowedResponseModes.Contains(responseMode.Trim());

    public static string ResolveBondTier(int bondLevel) => Math.Clamp(bondLevel, 0, 100) switch
    {
        >= 75 => BondTierBound,
        >= 50 => BondTierTrusted,
        >= 25 => BondTierFamiliar,
        _ => BondTierStranger
    };

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

    public static void NormalizeResidentObject(JsonObject resident)
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

    public static JsonObject BuildCompanionSeed(JsonObject resident)
    {
        NormalizeResidentObject(resident);
        var imprint = resident["mortalWorldImprint"] as JsonObject ?? new JsonObject();
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
            ["appearanceMotifs"] = (imprint["appearanceMotifs"] as JsonArray)?.DeepClone() ?? new JsonArray()
        };
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
        }
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

    private static bool GetNodeBool(JsonNode? node, bool fallback)
    {
        if (node is JsonValue value && value.TryGetValue<bool>(out var flag))
            return flag;

        return fallback;
    }
}
