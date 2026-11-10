using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact]
    public Task ConsoleFactionServiceConfirmsPriceAndPublishesTreatmentAndResidentMemory() =>
        RunServiceJourneyAsync(browser: false, realm: "Shining Abode", faction: true);

    [Fact]
    public Task BrowserFactionServiceConfirmsPriceAndPublishesTreatmentAndResidentMemory() =>
        RunServiceJourneyAsync(browser: true, realm: "Shining Abode", faction: true);

    [Fact]
    public Task FactionServicePublicationFailureRestoresWalletWoundWorldAndResidentMemory() =>
        RunServiceJourneyAsync(browser: false, failPublication: true, realm: "Shining Abode", faction: true);

    // Same controlled response channel and complete ordinary Guardian reasoning
    // as the existing working service journey. Only this admitted participant's
    // protected memory is added; the GM never writes the canonical roster.
    private static async Task WriteFactionServiceReasoningAsync(FileSystemManager files,
        FileSystemManager.CanonicalWriteLease lease, TurnRequest turn)
    {
        var timestamp = DateTime.UtcNow.ToString("o");
        var response = JsonNode.Parse((await files.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
        response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty] = new JsonArray(new JsonObject
        {
            ["entryId"] = "faction_service_thought_" + turn.RequestId,
            ["residentId"] = AfterlifeHealingPaymentLifecycleTests.FactionHealerId,
            ["turn"] = turn.TurnNumber, ["timestamp"] = timestamp, ["title"] = "Подтверждённая попытка лечения",
            ["summary"] = "Я принимаю отдельное подтверждение души на попытку за51 Чернильное Перо и применяю своё ИсцелениеIII.",
            ["eventType"] = "conversation", ["consequence"] = "Клиент рассчитывает исход, одну оплату и обычный цикл; нового долга нет.",
            ["attitude"] = "attentive", ["intent"] = "Помочь душе в рамках подтверждённых условий.", ["tags"] = new JsonArray()
        });
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
        var debug = JsonNode.Parse((await files.ReadFileAsync(lease, "output/debug_logs.json"))!)!.AsObject();
        var text = debug["gm_thoughts_markdown"]!.GetValue<string>();
        text = text.Replace("- Relevant actors: Элиара Последней Раны",
            "- Relevant actors: Элиара Последней Раны, " + AfterlifeHealingPaymentLifecycleTests.FactionHealerName,
            StringComparison.Ordinal);
        debug["gm_thoughts_markdown"] = text + string.Join("\n", "", "### " + AfterlifeHealingPaymentLifecycleTests.FactionHealerName,
            "- Current location: Зал сопровождения; остаюсь в текущем зале своей фракции.",
            "- Situation: Душа отдельно подтвердила цену51 за попытку лечения собственной духовной раныII.",
            "- Profile inputs: Живой видимый вознесённый житель, healing_support, ИсцелениеIII, публичный доступный профиль101% без дополнительных условий; фракция leaderless, торговой привилегии нет.",
            "- Thoughts: Я готов помочь на подтверждённых условиях, сохраняя право души отказаться до попытки.",
            "- Motivation: Восстановить целостность души в рамках публичной услуги.",
            "- Constraints: Результат и оплата не принадлежат ГМ; клиент сохраняет исходный бросок и один цикл. Другая Элиара не является исполнителем этой попытки.",
            "- Strategy options:", "1. Провести подтверждённую попытку. Benefit: помочь душе. Risk: неудачный бросок.",
            "2. Навязать другое решение. Benefit: больше контроля. Risk: нарушение отдельного выбора души.",
            "- Chosen strategy: Провести только подтверждённую попытку.", "- Rejected alternatives: Нет основания менять цену, исполнителя или создавать новый долг.",
            "- Actions: Я применяю своё ИсцелениеIII; фактический исход считает клиент.",
            "- State changes: spiritualResidentThoughtJournalUpdates передаёт мою first-person память через существующий protected residentThoughtJournalUpdates нормализатор. Канонический roster, рану, эффекты, оплату, History, время и client_auto_strategy публикует клиент.");
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", debug.ToJsonString());
    }
}
