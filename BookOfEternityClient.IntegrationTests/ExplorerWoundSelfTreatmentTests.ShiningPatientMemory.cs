using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    private static async Task WriteShiningPatientReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        TurnRequest turn, string kind)
    {
        var grant = turn.TurnNumber == 43; var timestamp = DateTime.UtcNow.ToString("o");
        var thought = grant ? "Я осознанно согласился на отдельную попытку лечения моей известной раны игроком; сейчас лечение ещё не началось."
            : "Я принимаю отдельно подтверждённую попытку лечения моей раны игроком; исход и естественное восстановление рассчитывает клиент.";
        JsonObject Memory(string id, string summary) => new()
        {
            ["entryId"] = "shining_patient_thought_" + id + "_" + turn.RequestId, ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp,
            ["title"] = grant ? "Осознанное согласие" : "Подтверждённая попытка", ["summary"] = summary,
            ["eventType"] = "conversation", ["consequence"] = "Обычный цикл продолжается без платы, долга и навязанного результата.",
            ["attitude"] = "attentive", ["intent"] = "Сохранить самостоятельное решение и обычную стратегию.", ["tags"] = new JsonArray()
        };
        var guardianMemory = Memory(AfterlifeWoundTreatmentAuthority.ElyaraId,
            "Я остаюсь в текущем зале, продолжаю обычную стратегию и не подменяю игрока в лечении другого обитателя.");
        guardianMemory["guardianId"] = AfterlifeWoundTreatmentAuthority.ElyaraId;
        var guardianJournal = JsonNode.Parse((await files.ReadFileAsync(lease, GuardianThoughtJournalState.StatePath)) ?? "{\"entries\":[]}")!.AsObject();
        guardianJournal[GuardianThoughtJournalState.UpdateProperty] = new JsonArray(guardianMemory);
        await files.WriteFileAtomicAsync(lease, GuardianThoughtJournalState.StatePath, guardianJournal.ToJsonString());
        var response = JsonNode.Parse((await files.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
        var supportMemory = Memory(AfterlifeHealingPaymentLifecycleTests.FactionHealerId,
            "Я продолжаю собственную обычную стратегию; лечение другого обитателя не делает меня целителем или пациентом этой попытки.");
        supportMemory["residentId"] = AfterlifeHealingPaymentLifecycleTests.FactionHealerId;
        response[GuardianAbodeResidentState.UpdateThoughtJournalProperty] = new JsonArray(supportMemory);
        if (kind == "resident")
        {
            var memory = Memory(ShiningPatientId, thought); memory["residentId"] = ShiningPatientId;
            response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(memory);
        }
        else
        {
            response[SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty] = new JsonObject
            {
                ["entryId"] = "shining_patient_ledger_" + turn.RequestId, ["summary"] = thought,
                ["turnNumber"] = turn.TurnNumber, ["gmThoughtsSummary"] = thought
            };
        }
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
        string Brain(string name, bool patient) => string.Join("\n", "", "### " + name,
            "- Current location: Зал сопровождения; остаюсь в текущем каноническом зале.",
            "- Situation: " + (patient ? thought : "Душа обсуждает или выполняет отдельно подтверждённое лечение другого обитателя; мой обычный цикл продолжается."),
            "- Profile inputs: " + (patient ? "Живой видимый сознательный способный " + (kind == "resident" ? "вознесённый житель archive_support" : "канонический radiant_actor глава фракции") + "; Исцеление0, известная духовная рана; игрок владеет ИсцелениемIII."
                : name == AfterlifeHealingPaymentLifecycleTests.FactionHealerName ? "Живой видимый вознесённый healing_support, ИсцелениеIII; в этой попытке не является целителем или пациентом." : "Единственная живая видимая Элиара, ИсцелениеV; в этой попытке не является целителем или пациентом."),
            "- Thoughts: " + (patient ? thought : "Я сохраняю отдельное решение души и пациента и продолжаю обычную стратегию."),
            "- Motivation: Поддерживать безопасный порядок и самостоятельный выбор участников.",
            "- Constraints: Нет платы, долга, принуждения или нового правила; клиент владеет броском, раной, History и циклом.",
            "- Strategy options:", "1. Сохранить добровольное согласие и обычную жизнь зала. Benefit: осознанная помощь. Risk: возможная неудача.",
            "2. Навязать другой исход. Benefit: контроль. Risk: нарушение согласия и полномочий клиента.",
            "- Chosen strategy: Сохранить точный выбор и обычный цикл мира.",
            "- Rejected alternatives: Нет основания менять зал, роль, членство, степень раны или плату.",
            "- Actions: " + (patient ? thought : "Я продолжаю обычную стратегию; автоматическое развитие рассчитывает клиент."),
            "- State changes: " + (patient ? kind == "resident" ? "spiritualResidentThoughtJournalUpdates сохраняет мою first-person память; physical roster меняет только owned normalizer."
                : "spiritualShiningPatientMemory добавляет мою first-person запись в afterlife_entity_profiles ledger и обновляет gmThoughtsSummary; resident journal не используется."
                : name == AfterlifeHealingPaymentLifecycleTests.FactionHealerName
                    ? "residentThoughtJournalUpdates добавляет мою отдельную first-person память обычной стратегии; я не участник лечения."
                    : "guardianThoughtJournalUpdates добавляет мою first-person память обычной стратегии.") +
                " Клиент публикует реальный исход, effects/History и один цикл; сохранённый бросок не меняется.");
        var names = new[] { PatientName(kind), "Элиара Последней Раны", AfterlifeHealingPaymentLifecycleTests.FactionHealerName };
        var text = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + string.Join(", ", names),
            "- Why relevant: Один обитатель принимает помощь игрока; обычный цикл затрагивает стратегию остальных нынешних обитателей зала.",
            "- Actors outside scope: нет", "- Why outside scope: Другие акторы не получают изменений.", "", "## Reasoning") +
            Brain(names[0], true) + Brain(names[1], false) + Brain(names[2], false);
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject { ["timestamp"] = timestamp, ["gm_thoughts_markdown"] = text }.ToJsonString());
    }

    private static async Task<(JsonArray? Ledger, JsonArray Residents, JsonArray Guardians)> AssertShiningPatientResponseMemoryAsync(
        FileSystemManager files, string kind, int turn, JsonArray? previousLedger = null, JsonArray? previousResidents = null, JsonArray? previousGuardians = null)
    {
        void Prefix(JsonArray? before, JsonArray after)
        {
            if (before is null) return;
            Assert.True(after.Count >= before.Count);
            for (var i = 0; i < before.Count; i++) Assert.True(JsonNode.DeepEquals(before[i], after[i]));
        }
        var residents = JsonNode.Parse((await files.ReadFileAsync(GuardianAbodeResidentState.StatePath))!)!["thoughtJournal"]!.AsArray();
        Prefix(previousResidents, residents);
        Assert.Contains(residents, e => e!["residentId"]?.GetValue<string>() == AfterlifeHealingPaymentLifecycleTests.FactionHealerId && e["turn"]!.GetValue<int>() == turn);
        if (kind == "resident") Assert.Contains(residents, e => e!["residentId"]?.GetValue<string>() == ShiningPatientId && e["turn"]!.GetValue<int>() == turn);
        else Assert.DoesNotContain(residents, e => e!["residentId"]?.GetValue<string>() == ShiningPatientId);
        Assert.Equal(residents.Count, residents.Select(e => e!["entryId"]!.GetValue<string>()).Distinct(StringComparer.OrdinalIgnoreCase).Count());
        var guardians = JsonNode.Parse((await files.ReadFileAsync(GuardianThoughtJournalState.StatePath))!)!["entries"]!.AsArray();
        Prefix(previousGuardians, guardians);
        Assert.Contains(guardians, e => e!["guardianId"]?.GetValue<string>() == AfterlifeWoundTreatmentAuthority.ElyaraId && e["turn"]!.GetValue<int>() == turn);
        JsonArray? ledger = null;
        if (kind == "radiant_actor")
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(WoundCarrierCatalog.AfterlifeProfilesPath))!)!;
            var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind && p["actorId"]?.GetValue<string>() == ShiningPatientId);
            ledger = patient["ledger"]!.AsArray(); Prefix(previousLedger, ledger);
            var entry = Assert.Single(ledger.OfType<JsonObject>(), e => e["turnNumber"]?.GetValue<int>() == turn);
            Assert.Contains("Я", entry["summary"]!.GetValue<string>());
            Assert.Equal(entry["summary"]!.GetValue<string>(), patient["gmThoughtsSummary"]!.GetValue<string>());
        }
        return (ledger?.DeepClone().AsArray(), residents.DeepClone().AsArray(), guardians.DeepClone().AsArray());
    }
}
