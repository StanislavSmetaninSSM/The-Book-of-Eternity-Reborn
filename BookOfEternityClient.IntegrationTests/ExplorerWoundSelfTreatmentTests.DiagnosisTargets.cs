using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Models;
using BookOfEternityClient.IO;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerWoundSelfTreatmentTests
{
    [Fact] public Task ResidentDiagnosisPublishesOrdinaryMemoryAndRefusesCurrentLocalityDrift() => RunForeignDiagnosisNativeAsync("resident");
    [Fact] public Task RadiantDiagnosisPublishesOrdinaryMemoryAndRefusesCurrentLocalityDrift() => RunForeignDiagnosisNativeAsync("radiant_actor");

    private async Task RunForeignDiagnosisNativeAsync(string kind)
    {
        await using var fixture = await AfterlifeHealingPaymentLifecycleTests.CreateFactionServiceFixtureAsync(3, 14, engine: false,
            playerHealingTier: 3, additionalBeforeResourceBootstrap: async (files, lease, profiles) =>
            {
                await SeedShiningPatientAsync(files, lease, profiles, kind);
                var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind &&
                    p["actorId"]?.GetValue<string>() == ShiningPatientId);
                var woundNode = Assert.IsType<JsonObject>(Assert.Single(patient["activeWounds"]!.AsArray()));
                woundNode["complications"] = new JsonArray(new JsonObject { ["complicationId"] = "foreign_hidden", ["kind"] = "spiritual_instability",
                    ["state"] = "active", ["displayName"] = "Скрытая неустойчивость", ["treatmentDifficultyModifier"] = 0,
                    ["ownedEffectIds"] = new JsonArray(), ["visibility"] = "hidden" });
                woundNode["treatment"]!["diagnosisPaths"] = new JsonArray(new JsonObject
                {
                    ["diagnosisPathId"] = "foreign_diagnosis", ["displayName"] = "Осмотреть известную рану", ["visibility"] = "public",
                    ["requiresKnownFacts"] = new JsonArray(), ["requirements"] = new JsonArray(new JsonObject
                    { ["kind"] = "source_capability", ["actorRole"] = "provider", ["capabilityRef"] = "spiritual_healing" }),
                    ["check"] = new JsonObject(), ["reveals"] = new JsonArray("complication:foreign_hidden"),
                    ["failurePolicy"] = "no_reveal"
                });
                woundNode["treatment"]!["knownRouteIds"] = new JsonArray();
                var parsed = WoundMaterializationContract.Parse(woundNode.ToJsonString(), "diagnosis.foreign.seed");
                Assert.True(parsed.IsValid, string.Join(";", parsed.Issues));
                var fingerprint = WoundIdentityState.ComputeSemanticFingerprint(parsed.Wound!);
                var identities = JsonNode.Parse((await files.ReadFileAsync(lease, WoundIdentityState.StatePath))!)!.AsObject();
                Assert.Single(identities["entries"]!.AsArray())!["semanticFingerprint"] = fingerprint;
                var history = JsonNode.Parse((await files.ReadFileAsync(lease, WoundHistoryState.HistoryPath))!)!.AsObject();
                Assert.Single(history["transitions"]!.AsArray())!["afterFingerprint"] = fingerprint;
                await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
                await files.WriteFileAtomicAsync(lease, WoundIdentityState.StatePath, identities.ToJsonString());
                await files.WriteFileAtomicAsync(lease, WoundHistoryState.HistoryPath, history.ToJsonString());
            });
        var fs = fixture.Files;
        var opened = await WoundApplicationService.OpenAsync(fs, "/лечить", treatment: true);
        var state = Assert.IsType<WoundCommandState>(opened.WoundCommandState);
        var target = Assert.Single(state.Targets, t => t.Available && t.Owner.OwnerKind == kind && t.Owner.OwnerId == ShiningPatientId);
        SpiritualSelfTreatmentSelection confirmed;
        ProgressionControl control;
        await using (var ownedLease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            var detail = await WoundApplicationService.SelectTargetAsync(fs, ownedLease, state, target.Value, "/лечить");
            var choice = Assert.Single(detail.WoundCommandState!.Choices, c => c.Available && c.Selection.Diagnosis is not null);
            confirmed = await WoundApplicationService.ConfirmAsync(fs, ownedLease, detail.WoundCommandState, choice.Value);
            Assert.Equal(choice.Selection, confirmed);
            var scheduler = new ProgressionScheduleService(fs, NullLogger<ProgressionScheduleService>.Instance);
            control = await scheduler.BuildControlForNextTurnAsync(ownedLease, activeTurnRealm: null);
            var context = new SpiritualSelfTreatmentStageContext("session_diagnosis_target", "request_diagnosis_target", 43, control);
            var before = fixture.CaptureMutationRoots(); before.Remove(AcceptedMechanicsPlan.WoundCommandPath);
            var staged = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fs, ownedLease, context, confirmed);
            Assert.True(staged.IsStaged, staged.IssueCode); fixture.AssertMutationRootsUnchanged(before);
            var request = JsonNode.Parse((await fs.ReadFileAsync(ownedLease, AcceptedMechanicsPlan.WoundCommandPath))!)!.AsObject();
            Assert.Equal(confirmed, SpiritualDiagnosisRequestCodec.ReadSelection(request));
            Assert.Null(request["participation"]); Assert.Null(request["attempt"]); Assert.Null(request["payment"]);
            var originalProfiles = (await fs.ReadFileAsync(ownedLease, WoundCarrierCatalog.AfterlifeProfilesPath))!;
            var profilesAfter = JsonNode.Parse(originalProfiles)!.AsObject();
            var moved = profilesAfter["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind &&
                p["actorId"]?.GetValue<string>() == ShiningPatientId);
            Assert.Null(moved[SpiritualTreatmentParticipation.RecordProperty]);
            moved["locationId"] = "hall_foreign"; moved["locationName"] = "Другой зал";
            await fs.WriteFileAtomicAsync(ownedLease, WoundCarrierCatalog.AfterlifeProfilesPath, profilesAfter.ToJsonString());
            var changed = fixture.CaptureMutationRoots();
            var refused = await AfterlifeWoundTreatmentAuthority.StageDiagnosisAsync(fs, ownedLease, context, confirmed);
            Assert.False(refused.IsStaged); Assert.Equal("spiritual_diagnosis_admission_invalid", refused.IssueCode);
            fixture.AssertMutationRootsUnchanged(changed);
            // Restore only this isolated pre-snapshot fault. No accepted turn,
            // receipt, signed original or live owner is reset or manufactured.
            await fs.WriteFileAtomicAsync(ownedLease, WoundCarrierCatalog.AfterlifeProfilesPath, originalProfiles);
        }
        await new LiveTurnPreparationService(fs).PrepareAsync(new LiveTurnPreparationOptions
        { SessionId = "session_diagnosis_target", RequestId = "request_diagnosis_target", TurnNumber = 43,
            CurrentRealm = "Shining Abode", ProgressionControl = control, PlayerAction = WoundApplicationService.DiagnosisActionText });
        var validator = new ValidationService(fs, NullLogger<ValidationService>.Instance);
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync())
        {
            var turn = JsonSerializer.Deserialize<TurnRequest>((await fs.ReadFileAsync(lease, LiveTurnPreparationService.TurnRequestPath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
            await WriteNativeCompletionAsync(fs, lease, turn, narrative: "Я осматриваю духовную рану; клиент раскрывает предусмотренные сведения и сохраняет обычный цикл мира.");
            await WriteDiagnosisWorldReasoningAsync(fs, lease, turn, kind);
            var response = JsonNode.Parse((await fs.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
            var ordinary = response[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.Deserialize<JsonElement[]>()!;
            var distributor = new StateDistributor(fs, NullLogger<StateDistributor>.Instance);
            var untouched = fixture.CaptureMutationRoots();
            await Assert.ThrowsAsync<InvalidDataException>(() => distributor.DistributeAsync(lease, new GameResponse
            { ResidentThoughtJournalUpdates = ordinary, ResidentThoughtsFromPatientInterface = true,
                SpiritualShiningPatientMemory = JsonSerializer.SerializeToElement<object?>(null) }));
            fixture.AssertMutationRootsUnchanged(untouched);
            await distributor.DistributeAsync(lease, new GameResponse
            { ResidentThoughtJournalUpdates = ordinary, ResidentThoughtsFromPatientInterface = true });

            var shiningBytes = (await fs.ReadFileBytesAsync(lease, ShiningAbodeState.StatePath))!;
            var changed = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(shiningBytes))!.AsObject();
            changed["factions"]![0]!["factionLifecycle"]!["state"] = "weakened";
            var normalized = changed.DeepClone().AsObject();
            ShiningAbodeState.NormalizeStateRoot(normalized, JsonNode.Parse((await fs.ReadFileAsync(lease, GuardianAbodeResidentState.StatePath))!)!.AsObject());
            Assert.True(JsonNode.DeepEquals(changed, normalized)); // A no-op normalizer cannot repair this real tuple drift.
            await fs.WriteFileAtomicAsync(lease, ShiningAbodeState.StatePath, changed.ToJsonString());
            var drifted = fixture.CaptureMutationRoots(new[] { ShiningAbodeState.StatePath, GuardianAbodeResidentState.StatePath });
            var manifest = await fixture.ReadManifestAsync(lease);
            await Assert.ThrowsAsync<InvalidDataException>(() => AfterlifeWoundHealingPlanner.Continuation.AdmitDiagnosisAsync(fs, lease, manifest));
            fixture.AssertMutationRootsUnchanged(drifted);
            await fs.WriteFileAtomicBytesAsync(lease, ShiningAbodeState.StatePath, shiningBytes);
            var preparation = await validator.PrepareSpiritualSelfTreatmentPublicationAsync(lease);
            output.WriteLine(JsonSerializer.Serialize(preparation));
            Assert.DoesNotContain(preparation, i => i.Severity == IssueSeverity.Error);
            await new CanonicalStateNormalizer(fs, NullLogger<CanonicalStateNormalizer>.Instance).BindTo(lease)
                .NormalizeAccumulatedStateWithPlanAsync((await fixture.ReadManifestAsync(lease)).Files);
        }
        var history = await fixture.ReadHistoryAsync();
        var diagnosis = Assert.IsType<SpiritualDiagnosisPersistedResult>(Assert.Single(history.Transitions, t => t.Kind == "diagnose").TransitionResult);
        Assert.Equal(kind, diagnosis.Original.Owner.OwnerKind);
        Assert.Equal("known_to_player", Assert.Single(diagnosis.KnowledgeAfter.Complications).Visibility);
        Assert.DoesNotContain(history.Transitions, t => t.Kind == "treat");
        await AssertShiningPatientResponseMemoryAsync(fs, kind, 43);
        Assert.DoesNotContain(await validator.ValidateAcceptedTurnInterfacePayloadAsync(), i => i.Severity == IssueSeverity.Error);
        Assert.DoesNotContain(await validator.ValidateAcceptedTurnReasoningAsync(), i => i.Severity == IssueSeverity.Error);
        fixture.AssertResourcesUnchanged();
    }

    private static async Task WriteDiagnosisWorldReasoningAsync(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease,
        TurnRequest turn, string kind)
    {
        // Reuse the complete ordinary witness ledger/journal envelopes, then author
        // this operation's truthful purpose and ordinary patient memory surface.
        await WriteShiningPatientReasoningAsync(files, lease, turn, kind);
        const string thought = "Я прохожу осмотр моей духовной раны; выбранный способ раскрывает разрешённые сведения, а клиент отдельно рассчитывает естественное восстановление.";
        var response = JsonNode.Parse((await files.ReadFileAsync(lease, "output/interface_updates.json"))!)!.AsObject();
        var ordinary = response[GuardianAbodeResidentState.UpdateThoughtJournalProperty]!.AsArray();
        if (kind == "resident")
        {
            var patient = response[SpiritualTreatmentParticipation.ResidentThoughtResponseProperty]![0]!.DeepClone().AsObject();
            patient["summary"] = thought; patient["title"] = "Осмотр духовной раны"; ordinary.Add(patient);
        }
        else
        {
            var profiles = JsonNode.Parse((await files.ReadFileAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath))!)!.AsObject();
            var patient = profiles["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]?.GetValue<string>() == kind &&
                p["actorId"]?.GetValue<string>() == ShiningPatientId);
            patient["ledger"]!.AsArray().Add(new JsonObject { ["entryId"] = "diagnosis_patient_" + turn.RequestId,
                ["summary"] = thought, ["turnNumber"] = turn.TurnNumber });
            patient["gmThoughtsSummary"] = thought;
            await files.WriteFileAtomicAsync(lease, WoundCarrierCatalog.AfterlifeProfilesPath, profiles.ToJsonString());
        }
        response.Remove(SpiritualTreatmentParticipation.ResidentThoughtResponseProperty);
        response.Remove(SpiritualTreatmentParticipation.ShiningPatientMemoryResponseProperty);
        await files.WriteFileAtomicAsync(lease, "output/interface_updates.json", response.ToJsonString());
        var names = new[] { PatientName(kind), "Элиара Последней Раны", AfterlifeHealingPaymentLifecycleTests.FactionHealerName };
        string Brain(string name, bool patient) => string.Join("\n", "", "### " + name,
            "- Current location: Зал сопровождения; остаюсь в текущем каноническом зале.",
            "- Situation: Игрок осматривает известную духовную рану; обычный мир продолжает один безопасный цикл.",
            "- Profile inputs: Живой видимый местный обитатель в собственной канонической роли; у игрока ИсцелениеIII, у пациента Исцеление0.",
            "- Thoughts: " + (patient ? thought : "Я продолжаю собственную обычную стратегию, не подменяя игрока в осмотре."),
            "- Motivation: Сохранить самостоятельность участников и точные сведения о ране.",
            "- Constraints: Осмотр не лечит, не бросает кубик и не расходует ОД, Перья или предметы; клиент владеет знаниями, восстановлением и временем.",
            "- Strategy options:", "1. Сохранить точный осмотр и обычную жизнь зала. Benefit: достоверные сведения. Risk: неизвестные требования сделают иной способ недоступным.",
            "2. Придумать другой исход. Benefit: контроль. Risk: нарушение точного способа.",
            "- Chosen strategy: Сохранить выбранный способ и обычный цикл.", "- Rejected alternatives: Нет основания менять зал, роль, членство или навязывать лечение.",
            "- Actions: " + (patient ? thought : "Я продолжаю обычную стратегию; автоматическое развитие рассчитывает клиент."),
            "- State changes: " + (patient && kind == "radiant_actor" ? "Моя обычная first-person запись добавлена в ledger, gmThoughtsSummary обновлён." :
                name == names[1] ? "guardianThoughtJournalUpdates добавляет мою first-person память обычной стратегии." :
                "residentThoughtJournalUpdates добавляет мою обычную first-person память без protected patient channels."));
        var text = string.Join("\n", "## NPC Scope", "- Mode: Scene-local", "- Relevant actors: " + string.Join(", ", names),
            "- Why relevant: Осмотр затрагивает пациента, а безопасный цикл продолжается для остальных местных обитателей.",
            "- Actors outside scope: нет", "- Why outside scope: Другие акторы не изменяются.", "", "## Reasoning") +
            Brain(names[0], true) + Brain(names[1], false) + Brain(names[2], false);
        await files.WriteFileAtomicAsync(lease, "output/debug_logs.json", new JsonObject
        { ["timestamp"] = DateTime.UtcNow.ToString("o"), ["gm_thoughts_markdown"] = text }.ToJsonString());
    }
}
