using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public sealed class DarenQteRewardProfileService
{
    public const int SchemaVersion = 1;
    public const string Source = "daren_qte_showcase";
    public const string ProfileRelativePath = "client_profile/qte_showcase_rewards.json";
    public const string GrantMarkerProperty = "darenQteShowcase";

    private static readonly JsonSerializerOptions JsonOpts = SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed;

    private static readonly DarenEndingTier[] TierDefinitions =
    [
        new(
            "shadow_on_the_run",
            "Тень в бегах",
            40,
            1,
            "Дарен выжил и ушёл от погони, но след получился грязным.",
            "Дарен добирается под мост с посохом, но погоня ещё держит его запах: улики мокнут не до конца, свидетели спорят о лице вора, а шум ночи вынуждает его менять убежище до рассвета.",
            "Книга признаёт постоянный урок Дарена: грязный, но спасённый след. В будущей новой игре этот итог добавит 1 Чернильное Перо к старту."),
        new(
            "broken_trail",
            "Сорванный след",
            55,
            2,
            "Дарен сорвал преследование, но оставил заметные улики.",
            "След оборван не там, где хотелось бы: Орвальд теряет темп у дворов, но свидетели помнят обрывки плаща, на замке остаются улики, и Дарену приходится закрывать тайник осторожнее обычного.",
            "Книга сохраняет постоянное достижение Дарена за сорванную погоню и добытый посох. В будущей новой игре этот след принесёт 2 Чернильных Пера на старте."),
        new(
            "clean_heist",
            "Чистая кража",
            75,
            4,
            "Дарен вынес посох и добрался до убежища с управляемыми последствиями.",
            "Убежище под мостом остаётся тихим: посох ложится в тайник, улики размыты, погоня упирается в пустую воду, и Дарен впервые позволяет себе улыбнуться чистой работе.",
            "Книга отмечает постоянное достижение Дарена за чистую кражу и управляемый отход. В будущей новой игре этот опыт откроет 4 Чернильных Пера с первого шага."),
        new(
            "perfect_shadow",
            "Идеальная тень",
            90,
            6,
            "Дарен ушёл с посохом как настоящая тень: чисто, быстро и без следов.",
            "Поместье просыпается слишком поздно: замки молчат, вода не держит отпечатков, Орвальд спорит с пустым двором, а легенда об идеальной тени уходит под мост без следов.",
            "Книга закрепляет постоянную легенду Дарена о безупречной краже. В будущей новой игре эта тень принесёт 6 Чернильных Перьев на старт.")
    ];

    private readonly FileSystemManager _fs;

    public DarenQteRewardProfileService(FileSystemManager fs)
    {
        _fs = fs;
    }

    public static IReadOnlyList<DarenEndingTier> EndingTiers => TierDefinitions;

    public static DarenEndingResult ResolveEnding(bool reachedHideout, int normalizedScore)
    {
        var clampedScore = Math.Clamp(normalizedScore, 0, 100);
        if (!reachedHideout || clampedScore < TierDefinitions[0].MinimumNormalizedScore)
        {
            return new DarenEndingResult(
                OutcomeId: "no_reward_failure",
                TierId: null,
                DisplayName: "Провал вылазки",
                NormalizedScore: clampedScore,
                InkFeatherBonus: 0,
                GrantsReward: false,
                Summary: "Дарен не достиг безопасного исхода, поэтому постоянная награда не записана.",
                Epilogue: "Ночь не закрывается: тревога ведёт погоню слишком близко, след уводит к опасному убежищу, и Дарен прячет дыхание вместо посоха. Такая вылазка остаётся уроком выживания, а не победой книги.",
                RewardExplanation: "Книга не записывает постоянный итог Дарена, потому что безопасный финал не достигнут и будущая новая игра не получает Чернильных Перьев за эту попытку.");
        }

        var tier = TierDefinitions
            .Where(item => clampedScore >= item.MinimumNormalizedScore)
            .OrderByDescending(item => item.MinimumNormalizedScore)
            .First();

        return new DarenEndingResult(
            OutcomeId: tier.TierId,
            TierId: tier.TierId,
            DisplayName: tier.DisplayName,
            NormalizedScore: clampedScore,
            InkFeatherBonus: tier.InkFeatherBonus,
            GrantsReward: true,
            Summary: tier.Summary,
            Epilogue: tier.Epilogue,
            RewardExplanation: tier.RewardExplanation);
    }

    public async Task<DarenRewardProfileState> ReadProfileAsync()
    {
        var path = ResolveProfilePath();
        if (!File.Exists(path))
            return new DarenRewardProfileState { SchemaVersion = SchemaVersion };

        DarenRewardProfileState normalized;
        try
        {
            var raw = await File.ReadAllTextAsync(path);
            normalized = NormalizeProfile(raw);
        }
        catch
        {
            normalized = new DarenRewardProfileState { SchemaVersion = SchemaVersion };
        }

        await WriteProfileAsync(normalized);
        return normalized;
    }

    public async Task<DarenRewardProfileWriteResult> RecordCompletionAsync(DarenEndingResult ending, DateTime completedAtUtc)
    {
        if (!ending.GrantsReward || string.IsNullOrWhiteSpace(ending.TierId))
        {
            return new DarenRewardProfileWriteResult(false, await ReadProfileAsync(), ending.RewardExplanation);
        }

        var tier = FindTier(ending.TierId);
        if (tier == null)
            return new DarenRewardProfileWriteResult(false, await ReadProfileAsync(), "Постоянная награда Дарена не записана: итог не распознан и будущая новая игра не получает Чернильных Перьев.");

        var profile = await ReadProfileAsync();
        var existing = profile.DarenShowcase;
        if (existing != null && CompareTierRank(existing.BestTierId, tier.TierId) >= 0)
        {
            return new DarenRewardProfileWriteResult(
                false,
                profile,
                $"Книга уже хранит постоянный итог Дарена: {existing.BestTierName}. Будущая новая игра сохранит лучший открытый бонус Чернильных Перьев без повторного сложения.");
        }

        profile = new DarenRewardProfileState
        {
            SchemaVersion = SchemaVersion,
            DarenShowcase = new DarenRewardRecord
            {
                BestTierId = tier.TierId,
                BestTierName = tier.DisplayName,
                InkFeatherBonus = tier.InkFeatherBonus,
                BestScore = ending.NormalizedScore,
                CompletedAtUtc = completedAtUtc.ToUniversalTime(),
                Source = Source
            }
        };

        await WriteProfileAsync(profile);
        return new DarenRewardProfileWriteResult(
            true,
            profile,
            tier.RewardExplanation);
    }

    public async Task<DarenRewardGrantResult> ApplyBestRewardToNewSoulStateAsync(JsonObject soulRoot)
    {
        var profile = await ReadProfileAsync();
        var reward = profile.DarenShowcase;
        if (reward == null)
            return DarenRewardGrantResult.NotGranted("Награда Дарена ещё не открыта.");

        var tier = FindTier(reward.BestTierId);
        if (tier == null)
            return DarenRewardGrantResult.NotGranted("Награда Дарена повреждена и не применена.");

        var grants = soulRoot["clientRewardGrants"] as JsonObject ?? new JsonObject();
        if (grants.TryGetPropertyValue(GrantMarkerProperty, out _))
            return DarenRewardGrantResult.NotGranted("Награда Дарена уже применена к этой новой игре.");

        var inkFeathers = NormalizeInkFeathers(soulRoot);
        var current = GetNodeInt(inkFeathers["current"]);
        var total = GetNodeInt(inkFeathers["total"]);
        inkFeathers["current"] = current + tier.InkFeatherBonus;
        inkFeathers["total"] = total + tier.InkFeatherBonus;
        soulRoot["inkFeathers"] = inkFeathers;

        grants[GrantMarkerProperty] = new JsonObject
        {
            ["source"] = Source,
            ["tierId"] = tier.TierId,
            ["tierName"] = tier.DisplayName,
            ["inkFeatherBonus"] = tier.InkFeatherBonus,
            ["profileSchemaVersion"] = SchemaVersion,
            ["grantedAtUtc"] = DateTime.UtcNow.ToString("o")
        };
        soulRoot["clientRewardGrants"] = grants;

        var message = $"Постоянный итог Дарена «{tier.DisplayName}» просыпается вместе с новой страницей и добавляет {tier.InkFeatherBonus} Чернильных Перьев этой новой игре.";
        return new DarenRewardGrantResult(true, tier.TierId, tier.DisplayName, tier.InkFeatherBonus, message);
    }

    private string ResolveProfilePath() =>
        Path.Combine(_fs.BasePath, ProfileRelativePath.Replace('/', Path.DirectorySeparatorChar));

    private async Task WriteProfileAsync(DarenRewardProfileState profile)
    {
        var path = ResolveProfilePath();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tempPath = path + ".tmp." + Guid.NewGuid().ToString("N")[..8];
        try
        {
            await File.WriteAllTextAsync(tempPath, JsonSerializer.Serialize(profile, JsonOpts));
            File.Move(tempPath, path, overwrite: true);
        }
        catch
        {
            if (File.Exists(tempPath))
                File.Delete(tempPath);
            throw;
        }
    }

    private static DarenRewardProfileState NormalizeProfile(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || JsonNode.Parse(raw) is not JsonObject root)
            return new DarenRewardProfileState { SchemaVersion = SchemaVersion };

        var candidates = new List<DarenRewardRecord>();
        if (root["darenShowcase"] is JsonObject single && TryNormalizeRecord(single, out var singleRecord))
            candidates.Add(singleRecord);

        if (root["darenShowcases"] is JsonArray duplicates)
        {
            foreach (var item in duplicates.OfType<JsonObject>())
            {
                if (TryNormalizeRecord(item, out var duplicateRecord))
                    candidates.Add(duplicateRecord);
            }
        }

        var best = candidates
            .OrderByDescending(item => TierRank(item.BestTierId))
            .ThenByDescending(item => item.BestScore)
            .ThenByDescending(item => item.CompletedAtUtc)
            .FirstOrDefault();

        return new DarenRewardProfileState
        {
            SchemaVersion = SchemaVersion,
            DarenShowcase = best
        };
    }

    private static bool TryNormalizeRecord(JsonObject source, out DarenRewardRecord record)
    {
        record = new DarenRewardRecord();

        var tierId = GetNodeString(source["bestTierId"]);
        var tier = FindTier(tierId);
        if (tier == null)
            return false;

        var bestScore = Math.Clamp(GetNodeInt(source["bestScore"]), 0, 100);
        if (bestScore < tier.MinimumNormalizedScore)
            return false;

        var completedAtUtc = DateTime.UtcNow;
        var completedAtRaw = GetNodeString(source["completedAtUtc"]);
        if (!string.IsNullOrWhiteSpace(completedAtRaw) &&
            DateTime.TryParse(completedAtRaw, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            completedAtUtc = parsed.ToUniversalTime();
        }

        record = new DarenRewardRecord
        {
            BestTierId = tier.TierId,
            BestTierName = tier.DisplayName,
            InkFeatherBonus = tier.InkFeatherBonus,
            BestScore = bestScore,
            CompletedAtUtc = completedAtUtc,
            Source = Source
        };
        return true;
    }

    private static JsonObject NormalizeInkFeathers(JsonObject soulRoot)
    {
        if (soulRoot["inkFeathers"] is JsonObject objectRoot)
        {
            objectRoot["current"] = Math.Max(0, GetNodeInt(objectRoot["current"]));
            objectRoot["total"] = Math.Max(0, GetNodeInt(objectRoot["total"]));
            return objectRoot;
        }

        var value = Math.Max(0, GetNodeInt(soulRoot["inkFeathers"]));
        return new JsonObject
        {
            ["current"] = value,
            ["total"] = value
        };
    }

    private static DarenEndingTier? FindTier(string? tierId) =>
        TierDefinitions.FirstOrDefault(item => string.Equals(item.TierId, tierId, StringComparison.OrdinalIgnoreCase));

    private static int CompareTierRank(string? leftTierId, string? rightTierId) =>
        TierRank(leftTierId).CompareTo(TierRank(rightTierId));

    private static int TierRank(string? tierId)
    {
        for (var index = 0; index < TierDefinitions.Length; index++)
        {
            if (string.Equals(TierDefinitions[index].TierId, tierId, StringComparison.OrdinalIgnoreCase))
                return index;
        }

        return -1;
    }

    private static string? GetNodeString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

    private static int GetNodeInt(JsonNode? node)
    {
        if (node is JsonValue value)
        {
            if (value.TryGetValue<int>(out var intValue))
                return intValue;
            if (value.TryGetValue<long>(out var longValue))
                return longValue > int.MaxValue ? int.MaxValue : longValue < int.MinValue ? int.MinValue : (int)longValue;
        }

        return 0;
    }
}

public sealed record DarenEndingTier(
    string TierId,
    string DisplayName,
    int MinimumNormalizedScore,
    int InkFeatherBonus,
    string Summary,
    string Epilogue,
    string RewardExplanation);

public sealed record DarenEndingResult(
    string OutcomeId,
    string? TierId,
    string DisplayName,
    int NormalizedScore,
    int InkFeatherBonus,
    bool GrantsReward,
    string Summary,
    string Epilogue,
    string RewardExplanation);

public sealed record DarenRewardProfileWriteResult(
    bool Updated,
    DarenRewardProfileState Profile,
    string Message);

public sealed record DarenRewardGrantResult(
    bool Granted,
    string? TierId,
    string TierName,
    int InkFeatherBonus,
    string PlayerMessage)
{
    public static DarenRewardGrantResult NotGranted(string message) =>
        new(false, null, string.Empty, 0, message);
}

public sealed class DarenRewardProfileState
{
    [JsonPropertyName("schemaVersion")]
    public int SchemaVersion { get; set; } = DarenQteRewardProfileService.SchemaVersion;

    [JsonPropertyName("darenShowcase")]
    public DarenRewardRecord? DarenShowcase { get; set; }
}

public sealed class DarenRewardRecord
{
    [JsonPropertyName("bestTierId")]
    public string BestTierId { get; set; } = "";

    [JsonPropertyName("bestTierName")]
    public string BestTierName { get; set; } = "";

    [JsonPropertyName("inkFeatherBonus")]
    public int InkFeatherBonus { get; set; }

    [JsonPropertyName("bestScore")]
    public int BestScore { get; set; }

    [JsonPropertyName("completedAtUtc")]
    public DateTime CompletedAtUtc { get; set; }

    [JsonPropertyName("source")]
    public string Source { get; set; } = DarenQteRewardProfileService.Source;
}
