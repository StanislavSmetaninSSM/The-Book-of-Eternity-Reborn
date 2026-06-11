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
            "Дарен добирается под мост, когда над водой ещё бегут крики Орвальда и красные отсветы сигнального кристалла. " +
            "Посох у него под плащом, но добыча тяжела от чужих взглядов: где-то помнит лицо ключник, где-то в грязи лежит сломанная ветка, где-то вода не успела съесть отпечаток сапога. " +
            "Дарен не празднует победу, он слушает, как убежище дышит вместе с ним и как каждый камень может стать свидетелем. " +
            "Ему приходится менять нору до рассвета, прятать посох глубже обычного и учиться жить с тем, что эта тень выжила ценой шума. " +
            "И всё же Дарен остаётся главным в этой ночи: не пойманный, не сломленный, с добычей в руках и с новым страхом, который будет точить его осторожность.",
            "Книга признаёт постоянный урок Дарена не как славу, а как выжженный на полях след. " +
            "Он вынес посох из пасти погони, поэтому будущая новая игра услышит слабый шорох этой спасённой тени и развернёт для него одно Чернильное Перо. " +
            "Перья не падают как плата за удачу: они становятся памятью о ночи, где Дарен выжил, хотя город ещё долго шептал его имя неуверенно."),
        new(
            "broken_trail",
            "Сорванный след",
            55,
            2,
            "Дарен сорвал преследование, но оставил заметные улики.",
            "Дарен рвёт цепочку дворов у самой воды, и погоня наконец теряет прямую дорогу к мосту. " +
            "Но ночь не отдаёт ему чистую тишину: Лукьян мог запомнить обрывок голоса, Ренара могла оставить в доме холодное подозрение, а царапина у кабинета ещё пахнет металлом. " +
            "Дарен закрывает тайник медленно, будто накладывает повязку на рану, и каждый узел проверяет дважды. " +
            "Посох спасён, Орвальд отстал, но на улицах останутся рассказы, из которых умная стража когда-нибудь сложит почти верную карту. " +
            "Дарен принимает это как цену сорванного следа: он победил не красотой, а выдержкой, и эта выдержка уже тяжелее украденного золота.",
            "Книга сохраняет постоянное достижение Дарена за ночь, где он не стал легендой, но отнял у погони последнюю верную нитку. " +
            "На будущей новой странице этот оборванный след проснётся двумя Чернильными Перьями, потому что Дарен доказал: даже грязную дорогу можно закрыть за собой волей. " +
            "Так Книга помнит не расписку о добыче, а урок осторожного отхода, когда посох уже в тайнике, а опасность ещё ходит по крышам."),
        new(
            "clean_heist",
            "Чистая кража",
            75,
            4,
            "Дарен вынес посох и добрался до убежища с управляемыми последствиями.",
            "Дарен входит под мост не беглецом, а хозяином собственной тишины. " +
            "Посох ложится в тайник ровно, без звона, и вода за камнями смывает последние следы так мягко, будто сама ночь решила стать его сообщницей. " +
            "Орвальд ещё рыщет по дворам, но его крик уходит выше, мимо убежища, мимо мокрых ступеней, мимо Дарена, который наконец позволяет пальцам разжаться. " +
            "Улики не исчезли чудом: Дарен сам утопил лишние нити, сам погасил подозрение, сам выбрал путь, где свидетели спорят с пустотой. " +
            "Эта кража не безупречна, но она чиста настолько, что её можно перечитать без стыда: Дарен вынес добычу и оставил за собой управляемую, почти послушную ночь.",
            "Книга отмечает постоянное достижение Дарена как страницу, где хитрость стала ремеслом, а ремесло удержало посох, тень и убежище вместе. " +
            "В будущей новой игре эта чистая работа раскроет перед ним четыре Чернильных Пера, словно вода под мостом заранее приготовит место для удачного шага. " +
            "Книга не ведёт счёт чужим монетам; она помнит, как Дарен заставил последствия идти за ним на коротком поводке."),
        new(
            "perfect_shadow",
            "Идеальная тень",
            90,
            6,
            "Дарен ушёл с посохом как настоящая тень: чисто, быстро и без следов.",
            "Поместье просыпается слишком поздно, и даже это пробуждение похоже на сон, из которого вынули сердце. " +
            "Замки молчат, руны не могут назвать вора, вода не держит отпечатков, а Орвальд спорит с пустым двором так яростно, будто пустота обязана ответить. " +
            "Дарен идёт под мост без спешки; посох за его плечом кажется не украденным, а вернувшимся к тому, кто всегда знал его вес. " +
            "Ни Мира, ни Лукьян, ни Ренара не получают полной истории, потому что Дарен оставил каждому только тень на краю взгляда. " +
            "Когда тайник закрывается, ночь не хлопает дверью, не зовёт стражу и не требует расплаты. " +
            "Она просто вписывает Дарена в свою чёрную страницу как идеальную тень: героя кражи, которого нельзя догнать даже памятью.",
            "Книга закрепляет постоянную легенду Дарена о краже, после которой сам дом сомневается, был ли вор настоящим. " +
            "На будущей новой странице эта легенда раскроет шесть Чернильных Перьев, как шесть тихих взмахов над водой у мостового тайника. " +
            "Дарен не получает сухую отметку; он получает память Книги о безупречной ночи, где посох исчез, а след отказался родиться.")
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
                Epilogue: "Ночь не закрывается, потому что Дарен не сумел довести её до безопасной тишины. " +
                "Тревога идёт слишком близко, погоня читает след почти до убежища, а вода под мостом больше не кажется союзницей. " +
                "Дарен прижимает посох или пустые руки к груди и впервые понимает, что добыча без выхода превращается в груз для могилы. " +
                "Ему приходится спасать дыхание, имя и последний тёмный поворот, вместо того чтобы закрыть тайник как победитель. " +
                "Эта вылазка остаётся на коже Дарена холодным уроком: главный герой ночи выжил или сорвался из кольца, но не принёс Книге исход, который можно было бы оставить потомкам.",
                RewardExplanation: "Книга отказывается делать эту ночь постоянным итогом Дарена, потому что безопасный финал не достигнут. " +
                "Она не записывает победу там, где убежище осталось под угрозой, а след ещё может привести стражу к имени вора. " +
                "Будущая новая игра пройдёт без Чернильных Перьев за эту попытку: Книга молчит не из жестокости, а потому что незавершённая тень не должна становиться наследием.");
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
                $"Книга уже хранит постоянный итог Дарена: {existing.BestTierName}. Будущая новая игра пойдёт за лучшей тенью и не обменяет её на более слабый след; Чернильные Перья не складываются от повторной вылазки.");
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
