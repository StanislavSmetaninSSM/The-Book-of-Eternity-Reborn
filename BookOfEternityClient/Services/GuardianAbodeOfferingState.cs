using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal static class GuardianAbodeOfferingState
{
    public const string PendingRequestPath = "game_state/control/pending_abode_offering.json";
    public const string ActionTag = "ABODE_OFFERING";
    public const string OfferingTypeInkFeathers = "ink_feathers";
    public const string OfferingTypeSoulRelic = "soul_relic";
    public const string OfferingTypeArchiveLoreFragment = "archive_lore_fragment";
    public const string OfferingTypeArchiveSecretRecord = "archive_secret_record";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    public sealed class PendingAbodeOfferingRequest
    {
        [JsonPropertyName("guardianId")]
        public string GuardianId { get; set; } = "";

        [JsonPropertyName("guardianName")]
        public string GuardianName { get; set; } = "";

        [JsonPropertyName("offeringType")]
        public string OfferingType { get; set; } = OfferingTypeInkFeathers;

        [JsonPropertyName("inkFeathersOffered")]
        public int InkFeathersOffered { get; set; }

        [JsonPropertyName("returnCycleId")]
        public string ReturnCycleId { get; set; } = "";

        [JsonPropertyName("relicId")]
        public string? RelicId { get; set; }

        [JsonPropertyName("relicName")]
        public string? RelicName { get; set; }

        [JsonPropertyName("relicRarity")]
        public string? RelicRarity { get; set; }

        [JsonPropertyName("createdAtUtc")]
        public string CreatedAtUtc { get; set; } = DateTime.UtcNow.ToString("o");

        [JsonPropertyName("archiveId")]
        public string? ArchiveId { get; set; }

        [JsonPropertyName("archiveTitle")]
        public string? ArchiveTitle { get; set; }

        [JsonPropertyName("archiveEntryType")]
        public string? ArchiveEntryType { get; set; }

        [JsonPropertyName("archiveRarity")]
        public string? ArchiveRarity { get; set; }
    }

    public static string BuildReturnCycleId(int incarnation) => $"return_{Math.Max(0, incarnation)}";

    public static int ResolvePowerGainForInkFeatherOffering(int inkFeathersOffered)
        => AbodePowerRules.ResolvePowerGainForInkFeatherOffering(inkFeathersOffered);

    public static int ResolvePowerGainForSoulRelicOffering(string? relicRarity) =>
        AbodePowerRules.ResolvePowerGainForSoulRelicOffering(relicRarity);

    public static bool IsCanonicalSoulRelicRarity(string? relicRarity) =>
        AbodePowerRules.IsCanonicalSoulRelicRarity(relicRarity);

    public static string DescribeCanonicalSoulRelicRarities() =>
        AbodePowerRules.AllowedSoulRelicRaritiesDisplay;

    public static int ResolvePowerGainForPendingRequest(PendingAbodeOfferingRequest request)
    {
        if (string.Equals(request.OfferingType, OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            return ResolvePowerGainForInkFeatherOffering(request.InkFeathersOffered);

        if (string.Equals(request.OfferingType, OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
            return ResolvePowerGainForSoulRelicOffering(request.RelicRarity);

        if (string.Equals(request.OfferingType, OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return AbodePowerRules.ResolvePowerGainForArchiveRarity(request.ArchiveRarity);
        }

        return 0;
    }

    public static async Task WriteAsync(FileSystemManager fs, PendingAbodeOfferingRequest request)
    {
        var existingJson = await fs.ReadFileAsync(PendingRequestPath);
        if (fs.FileExists(PendingRequestPath))
        {
            if (string.IsNullOrWhiteSpace(existingJson))
            {
                throw new InvalidOperationException(
                    "pending_abode_offering.json повреждён или пуст. Исправьте или очистите pending contract перед созданием нового подношения.");
            }

            try
            {
                var existing = JsonSerializer.Deserialize<PendingAbodeOfferingRequest>(existingJson, JsonOpts);
                if (existing == null)
                {
                    throw new InvalidOperationException(
                        "pending_abode_offering.json повреждён и не может быть перезаписан новым подношением.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                throw new InvalidOperationException(
                    "pending_abode_offering.json повреждён и не может быть перезаписан новым подношением.");
            }

            throw new InvalidOperationException(
                "Уже существует ожидающее подношение Обители. Дождитесь его закрытия или явно очистите pending contract перед новым подношением.");
        }

        await fs.WriteFileAtomicAsync(PendingRequestPath, JsonSerializer.Serialize(request, JsonOpts));
    }

    public static async Task<PendingAbodeOfferingRequest?> ReadAsync(FileSystemManager fs)
    {
        var json = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<PendingAbodeOfferingRequest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public static void Clear(FileSystemManager fs) => fs.DeleteFile(PendingRequestPath);

    public static async Task EnsureHealthyAsync(FileSystemManager fs, string? currentRealm)
    {
        if (!fs.FileExists(PendingRequestPath))
            return;

        if (!RealmSemantics.HasResolvedRealm(currentRealm))
            return;

        if (!IsAfterlifeRealm(currentRealm))
        {
            fs.DeleteFile(PendingRequestPath);
            return;
        }

        var json = await fs.ReadFileAsync(PendingRequestPath);
        if (string.IsNullOrWhiteSpace(json))
            return;

        PendingAbodeOfferingRequest? request;
        try
        {
            request = JsonSerializer.Deserialize<PendingAbodeOfferingRequest>(json, JsonOpts);
        }
        catch
        {
            return;
        }

        if (request == null ||
            string.IsNullOrWhiteSpace(request.GuardianId) ||
            string.IsNullOrWhiteSpace(request.GuardianName) ||
            string.IsNullOrWhiteSpace(request.ReturnCycleId))
        {
            return;
        }

        if (string.Equals(request.OfferingType, OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
        {
            if (request.InkFeathersOffered <= 0 ||
                request.InkFeathersOffered % 50 != 0 ||
                request.InkFeathersOffered > 150)
            {
                return;
            }
        }
        else if (string.Equals(request.OfferingType, OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.RelicId) ||
                string.IsNullOrWhiteSpace(request.RelicName) ||
                string.IsNullOrWhiteSpace(request.RelicRarity))
            {
                return;
            }
        }
        else if (string.Equals(request.OfferingType, OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(request.OfferingType, OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            if (string.IsNullOrWhiteSpace(request.ArchiveId) ||
                string.IsNullOrWhiteSpace(request.ArchiveTitle) ||
                string.IsNullOrWhiteSpace(request.ArchiveEntryType) ||
                string.IsNullOrWhiteSpace(request.ArchiveRarity) ||
                !AfterlifeArchiveState.IsAllowedEntryType(request.ArchiveEntryType) ||
                !AfterlifeArchiveState.IsSupportedArchiveRarity(request.ArchiveRarity) ||
                !AfterlifeArchiveState.OfferingTypeMatchesEntryType(request.OfferingType, request.ArchiveEntryType))
            {
                return;
            }
        }
        else
        {
            return;
        }

        if (await HasMatchingResolvedOfferingJournalEntryAsync(fs, request))
            fs.DeleteFile(PendingRequestPath);
    }

    public static int CountOfferedInkFeathersForReturnCycle(JsonElement journalRoot, string guardianId, string returnCycleId)
    {
        if (journalRoot.ValueKind != JsonValueKind.Object ||
            !journalRoot.TryGetProperty("entries", out var entries) ||
            entries.ValueKind != JsonValueKind.Array)
        {
            return 0;
        }

        var total = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            if (entry.ValueKind != JsonValueKind.Object)
                continue;

            var entryGuardianId = GetString(entry, "guardianId");
            var reasonType = GetString(entry, "reasonType");
            if (!string.Equals(entryGuardianId, guardianId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(reasonType, "offering", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!entry.TryGetProperty("audit", out var audit) || audit.ValueKind != JsonValueKind.Object)
                continue;

            var cycle = GetString(audit, "returnCycleId");
            var offeringType = GetString(audit, "offeringType");
            if (!string.Equals(cycle, returnCycleId, StringComparison.OrdinalIgnoreCase) ||
                !string.Equals(offeringType, OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            total += GetInt(audit, "inkFeathersOffered");
        }

        return total;
    }

    private static async Task<bool> HasMatchingResolvedOfferingJournalEntryAsync(FileSystemManager fs, PendingAbodeOfferingRequest request)
    {
        var journalJson = await fs.ReadFileAsync(GuardianPowerEventState.JournalPath);
        if (string.IsNullOrWhiteSpace(journalJson) ||
            !DateTimeOffset.TryParse(request.CreatedAtUtc, out var requestCreatedAt))
        {
            return false;
        }

        try
        {
            using var doc = JsonDocument.Parse(journalJson);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("entries", out var entries) ||
                entries.ValueKind != JsonValueKind.Array)
            {
                return false;
            }

            var expectedGain = ResolvePowerGainForPendingRequest(request);
            if (expectedGain <= 0)
                return false;

            foreach (var entry in entries.EnumerateArray())
            {
                if (OfferingJournalEntryMatchesRequest(entry, request, expectedGain, requestCreatedAt))
                    return true;
            }
        }
        catch
        {
            return false;
        }

        return false;
    }

    private static bool OfferingJournalEntryMatchesRequest(
        JsonElement entry,
        PendingAbodeOfferingRequest request,
        int expectedGain,
        DateTimeOffset requestCreatedAt)
    {
        if (entry.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetString(entry, "guardianId"), request.GuardianId, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetString(entry, "reasonType"), "offering", StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetString(entry, "sourceSurface"), "guardianAbodeOffering", StringComparison.OrdinalIgnoreCase) ||
            GetInt(entry, "delta") != expectedGain ||
            !DateTimeOffset.TryParse(GetString(entry, "appliedAt"), out var appliedAt) ||
            appliedAt < requestCreatedAt ||
            !entry.TryGetProperty("audit", out var audit) ||
            audit.ValueKind != JsonValueKind.Object ||
            !string.Equals(GetString(audit, "offeringType"), request.OfferingType, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(GetString(audit, "returnCycleId"), request.ReturnCycleId, StringComparison.OrdinalIgnoreCase) ||
            GetInt(audit, "finalDelta") != expectedGain)
        {
            return false;
        }

        if (string.Equals(request.OfferingType, OfferingTypeInkFeathers, StringComparison.OrdinalIgnoreCase))
            return GetInt(audit, "inkFeathersOffered") == request.InkFeathersOffered;

        if (string.Equals(request.OfferingType, OfferingTypeSoulRelic, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetString(audit, "relicId"), request.RelicId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetString(audit, "relicName"), request.RelicName, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetString(audit, "relicRarity"), request.RelicRarity, StringComparison.OrdinalIgnoreCase);
        }

        if (string.Equals(request.OfferingType, OfferingTypeArchiveLoreFragment, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(request.OfferingType, OfferingTypeArchiveSecretRecord, StringComparison.OrdinalIgnoreCase))
        {
            return string.Equals(GetString(audit, "archiveId"), request.ArchiveId, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetString(audit, "archiveTitle"), request.ArchiveTitle, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetString(audit, "archiveEntryType"), request.ArchiveEntryType, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(GetString(audit, "archiveRarity"), request.ArchiveRarity, StringComparison.OrdinalIgnoreCase);
        }

        return false;
    }

    private static bool IsAfterlifeRealm(string? currentRealm) =>
        string.Equals(currentRealm, "Chaos Sea", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Море Хаоса", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Shining Abode", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(currentRealm, "Сияющая Обитель", StringComparison.OrdinalIgnoreCase);

    private static string GetString(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.String)
            return string.Empty;

        return value.GetString() ?? string.Empty;
    }

    private static int GetInt(JsonElement element, string propertyName)
    {
        if (!element.TryGetProperty(propertyName, out var value))
            return 0;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var parsed))
            return parsed;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out var parsedFromString))
            return parsedFromString;

        return 0;
    }
}
