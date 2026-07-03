using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public static class ChaosSeaBootstrapStateBuilder
{
    private static readonly string[] CodexCategoryOrder =
    [
        "cosmology",
        "geography",
        "history",
        "cultures",
        "creatures",
        "characters",
        "artifacts",
        "factions",
        "magic",
        "other"
    ];

    public static IReadOnlyDictionary<string, JsonObject> BuildFreshNewGameFiles(
        string soulName,
        string soulFormDescription,
        string guardianName,
        string abodeName,
        DateTimeOffset createdAtUtc)
    {
        var safeSoulName = FirstNonEmpty(soulName, "Безымянная душа");
        var safeSoulForm = FirstNonEmpty(soulFormDescription, "душа без устойчивого внешнего описания");
        var safeGuardianName = FirstNonEmpty(guardianName, "выбранный Хранитель");
        var safeAbodeName = FirstNonEmpty(abodeName, "первая Обитель");
        var timestamp = createdAtUtc.ToUniversalTime().ToString("o");

        var files = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase)
        {
            ["game_state/meta/character_chronicle.json"] = BuildCharacterChronicle(
                safeSoulName,
                safeSoulForm,
                safeGuardianName,
                safeAbodeName),
            ["lore/chaos_sea/cosmology.json"] = BuildCosmologyLore(),
            ["lore/chaos_sea/soul_system_lore.json"] = BuildSoulSystemLore(safeSoulName, safeSoulForm),
            ["lore/chaos_sea/guardians_lore.json"] = BuildGuardiansLore(safeGuardianName, safeAbodeName)
        };

        files["lore/codex_entries.json"] = BuildCodexEntries(timestamp);
        return files;
    }

    private static JsonObject BuildCharacterChronicle(
        string soulName,
        string soulFormDescription,
        string guardianName,
        string abodeName) =>
        new()
        {
            ["entries"] = new JsonArray
            {
                new JsonObject
                {
                    ["entryId"] = "chronicle_chaos_sea_first_awakening_001",
                    ["turn"] = 0,
                    ["title"] = "Первое пробуждение в Море Хаоса",
                    ["summary"] = $"Душа «{soulName}» проявилась как {soulFormDescription} и встретила Хранителя: {guardianName}. Первая точка опоры души - {abodeName}.",
                    ["entry"] = $"#0. {soulName} впервые осознаёт себя в Море Хаоса. Форма души видима ГМу: {soulFormDescription}. Рядом уже есть выбранный Хранитель, {guardianName}, и открытая стартовая Обитель: {abodeName}."
                }
            }
        };

    private static JsonObject BuildCosmologyLore() =>
        new()
        {
            ["universalLaws"] = new JsonObject
            {
                ["soulReincarnation"] = "Души проходят через Море Хаоса между смертными жизнями и могут возвращаться к новым воплощениям.",
                ["inkFeathers"] = "Чернильные Перья являются мета-ресурсом памяти, опыта и будущих возможностей души.",
                ["guardianBond"] = "Хранитель не заменяет волю души, а помогает ей выдерживать переходы между жизнями."
            },
            ["cosmicStructure"] = new JsonObject
            {
                ["chaosSeaDescription"] = "Море Хаоса - пространство между мирами, где душа сохраняет себя до следующего воплощения.",
                ["abodes"] = "Обители Хранителей служат безопасными точками в хаотическом пространстве.",
                ["soulGates"] = "Врата Душ ведут в смертные миры, когда душа готова принять новую жизнь."
            }
        };

    private static JsonObject BuildSoulSystemLore(string soulName, string soulFormDescription) =>
        new()
        {
            ["soulIdentity"] = new JsonObject
            {
                ["currentName"] = soulName,
                ["visibleForm"] = soulFormDescription,
                ["identityRule"] = "Имя и форма души являются ролевым самоописанием игрока в посмертии."
            },
            ["progression"] = new JsonObject
            {
                ["inkFeathers"] = "Чернильные Перья сохраняются между циклами и могут тратиться на долгосрочное развитие.",
                ["soulRelics"] = "Реликвии Души являются устойчивыми следами прошлых жизней и важных выборов.",
                ["enlightenment"] = "Просветление растёт через завершённые жизни, обучение и значимые сцены с Хранителями."
            }
        };

    private static JsonObject BuildGuardiansLore(string guardianName, string abodeName) =>
        new()
        {
            ["guardianRole"] = "Хранители сопровождают души между жизнями, предлагают испытания, помощь и личные сюжетные линии.",
            ["currentGuardian"] = new JsonObject
            {
                ["name"] = guardianName,
                ["abode"] = abodeName,
                ["startingRule"] = "Выбранный системный Хранитель уже материализован клиентом как canonical state; ГМ описывает встречу, но не создаёт Хранителя заново."
            },
            ["abodes"] = new JsonObject
            {
                ["meaning"] = "Обитель - личное пространство Хранителя и первая безопасная сцена души в Море Хаоса.",
                ["playerUse"] = "Игрок может возвращаться к Хранителю, разговаривать, обучаться и искать новые пути через доступные команды."
            }
        };

    private static JsonObject BuildCodexEntries(string discoveredAt)
    {
        var entries = new JsonArray
        {
            BuildCodexEntry(
                "codex_chaos_sea_first_law",
                "Море Хаоса",
                "cosmology",
                "Море Хаоса связывает смертные миры и посмертие. Здесь душа сохраняет имя, форму, Чернильные Перья и путь к новым воплощениям.",
                "lore/chaos_sea/cosmology.json",
                discoveredAt,
                "Стартовая запись новой игры в Море Хаоса",
                "chaos_sea"),
            BuildCodexEntry(
                "codex_chaos_sea_guardians",
                "Хранители",
                "characters",
                "Хранители сопровождают души между жизнями. Они могут наставлять, спорить, проверять и открывать личные сюжетные линии.",
                "lore/chaos_sea/guardians_lore.json",
                discoveredAt,
                "Стартовая запись о выбранном Хранителе",
                "guardians"),
            BuildCodexEntry(
                "codex_chaos_sea_soul_path",
                "Путь души",
                "magic",
                "Душа сохраняет устойчивую личность между циклами, но каждое воплощение добавляет новый опыт, цену и память.",
                "lore/chaos_sea/soul_system_lore.json",
                discoveredAt,
                "Стартовая запись о природе души",
                "soul")
        };

        return new JsonObject
        {
            ["entries"] = entries,
            ["totalEntries"] = entries.Count,
            ["categories"] = BuildCodexCategoryCounts(entries)
        };
    }

    private static JsonObject BuildCodexEntry(
        string entryId,
        string title,
        string category,
        string content,
        string sourceFile,
        string discoveredAt,
        string discoveryContext,
        string tag) =>
        new()
        {
            ["entryId"] = entryId,
            ["title"] = title,
            ["category"] = category,
            ["content"] = content,
            ["summary"] = content,
            ["sourceFile"] = sourceFile,
            ["discoveryContext"] = discoveryContext,
            ["incarnation"] = 0,
            ["discoveredAt"] = discoveredAt,
            ["tags"] = new JsonArray("bootstrap", tag),
            ["relatedEntries"] = new JsonArray()
        };

    private static JsonObject BuildCodexCategoryCounts(JsonArray entries)
    {
        var counts = CodexCategoryOrder.ToDictionary(category => category, _ => 0, StringComparer.OrdinalIgnoreCase);
        foreach (var entry in entries.OfType<JsonObject>())
        {
            var category = entry["category"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(category))
                continue;

            if (!counts.ContainsKey(category))
                counts["other"]++;
            else
                counts[category]++;
        }

        var result = new JsonObject();
        foreach (var category in CodexCategoryOrder)
            result[category] = counts[category];

        return result;
    }

    private static string FirstNonEmpty(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
}
