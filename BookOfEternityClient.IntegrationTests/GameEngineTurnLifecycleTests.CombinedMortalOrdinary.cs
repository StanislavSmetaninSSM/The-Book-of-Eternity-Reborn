using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

#if DEBUG
public sealed partial class GameEngineTurnLifecycleTests
{
    [Fact]
    public Task CombinedMortalBirth_OrdinaryDeferAcceptsThroughActualPipeline()
        => RunCombinedMortalBirthAsync(false, null, validateRetiredState: true, validateOrdinaryContinuation: true);

    [Fact]
    public Task CombinedMortalBirth_OrdinaryMiraInspectionAcceptsThroughActualPipeline()
        => RunCombinedMortalBirthAsync(false, null, validateRetiredState: true,
            validateOrdinaryContinuation: true, inspectWithMira: true);

    internal static void AssertMiraMovementProjections(JsonObject expectedMap, JsonObject priorIndex,
        JsonObject actualMap, JsonObject actualIndex, string targetId, int turn)
    {
        expectedMap = expectedMap.DeepClone().AsObject();
        priorIndex = priorIndex.DeepClone().AsObject();
        var destination = Assert.Single(expectedMap["locations"]!.AsArray(), row =>
            row!["locationId"]!.GetValue<string>() == targetId)!;
        var beforeDiscovery = destination["discovery"]!.DeepClone();
        destination["discovery"] = new JsonObject
        {
            ["tier"] = "visited", ["audience"] = "player_known", ["rumorSummary"] = null
        };
        Assert.True(JsonNode.DeepEquals(expectedMap,
            actualMap),
            "Only the selected destination discovery may change.");
        actualIndex = actualIndex.DeepClone().AsObject();
        var actualRow = Assert.Single(actualIndex["locationEntries"]!.AsArray(), row =>
            row!["locationId"]!.GetValue<string>() == targetId)!;
        var priorRow = Assert.Single(priorIndex["locationEntries"]!.AsArray(), row =>
            row!["locationId"]!.GetValue<string>() == targetId)!;
        var actualTransitions = actualRow["transitions"]!.AsArray();
        var priorTransitions = priorRow["transitions"]!.AsArray();
        Assert.Equal(priorTransitions.Count + 1, actualTransitions.Count);
        var appended = actualTransitions[^1]!.AsObject();
        var transitionId = appended["transitionId"]!.GetValue<string>();
        Assert.Matches("^mltrn_[0-9a-f]{32}$", transitionId);
        Assert.DoesNotContain(priorIndex["locationEntries"]!.AsArray().Concat(priorIndex["linkEntries"]!.AsArray())
            .SelectMany(row => row!["transitions"]!.AsArray()), row => row!["transitionId"]!.GetValue<string>() == transitionId);
        var expectedTransition = new JsonObject
        {
            ["transitionId"] = transitionId, ["kind"] = "current_selection", ["turn"] = turn,
            ["entityId"] = targetId,
            ["beforeState"] = new JsonObject
            {
                ["discovery"] = beforeDiscovery,
                ["lastEventsDescription"] = destination["lastEventsDescription"]?.DeepClone()
            },
            ["afterState"] = new JsonObject
            {
                ["discovery"] = destination["discovery"]!.DeepClone(),
                ["lastEventsDescription"] = destination["lastEventsDescription"]?.DeepClone()
            },
            ["sourceAuthorityKind"] = "turn_outcome", ["sourceAuthorityId"] = "turn_" + turn,
            ["operationRef"] = "currentLocationData", ["sourceLocationId"] = null, ["targetLocationId"] = null
        };
        Assert.True(JsonNode.DeepEquals(expectedTransition, appended),
            "Only the exact current_selection evidence for the accepted turn may append.");
        actualTransitions.RemoveAt(actualTransitions.Count - 1);
        // Publishing any existing-location selection parses the signed original index first.
        // Preserve every row and transition, allowing only its required ordinal ID order.
        foreach (var (section, idField) in new[] { ("locationEntries", "locationId"), ("linkEntries", "linkId") })
        {
            priorIndex[section] = new JsonArray(priorIndex[section]!.AsArray()
                .OrderBy(row => row![idField]!.GetValue<string>(), StringComparer.Ordinal)
                .Select(row => row!.DeepClone()).ToArray());
        }
        Assert.True(JsonNode.DeepEquals(priorIndex, actualIndex),
            "Prior location/link identity evidence changed beyond canonical ordinal ID order.");
    }

    private async Task AssertCombinedBirthOrdinaryContinuationAsync(
        GameEngineTestRuntime runtime, string artifact, Action<Func<TurnRequest, Task>> setAuthor, Func<int> calls, bool inspectWithMira)
    {
        const string npcPath = "game_state/npcs/npc_core.json";
        const string journalPath = "game_state/npcs/npc_journals.json";
        const string storyPath = "stories/mortal_life_2.jsonl";
        var action = inspectWithMira
            ? "Иду по открытому пути из двора к покоям Вальмонта, нахожу Миру и начинаю вместе с ней осмотр видимых опор и уровня воды. Прошу отметить только то, что удастся увидеть."
            : "Прошу у совета один кусочек мела для разметки у переправы. Ничего не беру и не перемещаю.";
        Assert.Equal(42, runtime.GameLoop.TurnNumber);
        Assert.Equal(1, calls());
        Assert.True(runtime.Engine.HasRetiredOrdinaryOriginal);
        var retained = new Dictionary<string, byte[]>();
        foreach (var path in new[] { npcPath, MortalLocationMaterializationContract.WorldMapPath,
            MortalLocationIdentityState.StatePath, "game_state/world/current_location.json",
            MortalStorageAccessProjection.StatePath, "game_state/inventory/items.json",
            "game_state/inventory/item_identity_index.json", StorageTransportMoveService.VehiclesPath,
            "game_state/factions/faction_core.json", "game_state/factions/faction_projects.json" })
        {
            var bytes = await _fs.ReadFileBytesAsync(path);
            Assert.NotNull(bytes);
            retained.Add(path, bytes);
        }
        var core = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(retained[npcPath]))!;
        var steward = Assert.Single(core["UpdateNPCs"]!.AsArray());
        var stewardId = steward!["NPCId"]!.GetValue<string>();
        var stewardName = steward["name"]!.GetValue<string>();
        var locationId = steward["currentLocationId"]!.GetValue<string>();
        Assert.Equal(42, steward["materialization"]!["materializedAtTurn"]!.GetValue<int>());
        Assert.Empty(steward["inventory"]!.AsArray());
        var priorStory = (await _fs.ReadFileAsync(storyPath))!;
        Assert.Single(priorStory.Split('\n', StringSplitOptions.RemoveEmptyEntries));
        var journalBytesBefore = (await _fs.ReadFileBytesAsync(journalPath))!;
        var journalBefore = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(journalBytesBefore))!;
        const string currentPath = "game_state/world/current_location.json";
        var actorId = inspectWithMira ? JourneyScoutId : stewardId;
        var actorName = inspectWithMira
            ? Assert.Single(core["NPCsInScene"]!.AsArray(), row => row!["NPCId"]!.GetValue<string>() == actorId)!["name"]!.GetValue<string>()
            : stewardName;
        if (inspectWithMira)
        {
            var scout = Assert.Single(core["NPCsInScene"]!.AsArray(), row => row!["NPCId"]!.GetValue<string>() == actorId)!;
            Assert.Equal(JourneyLocationId, scout["currentLocationId"]!.GetValue<string>());
            var map = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(retained[MortalLocationMaterializationContract.WorldMapPath]))!;
            Assert.Single(map["links"]!.AsArray(), link =>
                link!["sourceLocationId"]!.GetValue<string>() == locationId &&
                link["targetLocationId"]!.GetValue<string>() == JourneyLocationId &&
                link["access"]!["state"]!.GetValue<string>() == "open" &&
                link["access"]!["requirements"]!.AsArray().Count == 0 &&
                link["discovery"]!["audience"]!.GetValue<string>() == "player_known");
            Assert.Single(journalBefore["NPCJournals"]!.AsArray(), row =>
                (row!["npcId"] ?? row["NPCId"])?.GetValue<string>() == actorId &&
                row["journalEntries"]!.AsArray().Count > 0);
        }
        var scheduleBefore = JsonSerializer.Deserialize<ProgressionScheduleState>(
            (await _fs.ReadFileAsync(ProgressionScheduleService.SchedulePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.Equal(0, scheduleBefore.PendingWorldCycles);
        Assert.Equal(0, scheduleBefore.PendingFactionCycles);
        JsonNode? expectedJournals = null;
        TurnRequest? actualRequest = null;
        setAuthor(async request =>
        {
            actualRequest = request;
            Assert.Equal(43, request.TurnNumber);
            Assert.Equal("session_mortal_living_world_journey", request.SessionId);
            Assert.Equal(action, request.PlayerAction);
            Assert.NotNull(request.ProgressionControl);
            Assert.Equal(0, request.ProgressionControl!.WorldCyclesAlreadyPendingBeforeTurn);
            Assert.Equal(0, request.ProgressionControl.FactionCyclesAlreadyPendingBeforeTurn);
            var manifest = JsonSerializer.Deserialize<LiveTurnPendingSnapshotManifest>(
                (await _fs.ReadFileAsync(LiveTurnPreparationService.PendingTurnSnapshotManifestPath))!,
                LiveTurnPreparationService.ManifestJsonOptions)!;
            Assert.True(PendingTurnSnapshotAuthority.TryValidateManifestForReaderAuthority(manifest,
                await _fs.ReadFileAsync(PendingTurnSnapshotAuthority.AuthorityPath), LiveTurnPreparationService.ManifestHashJsonOptions,
                m => m.ManifestPayloadHash, (m, h) => m.ManifestPayloadHash = h,
                m => m.SessionId, m => m.RequestId, m => m.TurnNumber, m => m.Files,
                m => m.SnapshotFileHashes, m => m.ClientOwnedValidationHashes, m => m.RollbackBaselineFiles,
                m => m.SourceLabel, m => m.RollbackBackups,
                path => File.Exists(_fs.ResolvePath(path)) ? File.ReadAllBytes(_fs.ResolvePath(path)) : null,
                out var admitted, out var failure), failure);
            Assert.NotNull(admitted);
            Assert.Equal(request.RequestId, admitted!.RequestId);
            Assert.Equal(request.SessionId, admitted.SessionId);
            Assert.Equal(43, admitted.TurnNumber);
            Assert.Equal(request.Timestamp, manifest.RequestTimestamp);
            Assert.Equal(retained[npcPath], await _fs.ReadFileBytesAsync(manifest.Files[npcPath]));
            Assert.Equal(journalBytesBefore, await _fs.ReadFileBytesAsync(manifest.Files[journalPath]));
            Assert.Equal(journalBytesBefore, await _fs.ReadFileBytesAsync(journalPath));
            Assert.Equal(retained[npcPath], await _fs.ReadFileBytesAsync(npcPath));
            Assert.False(_fs.FileExists(ProgressionScheduleService.ReportPath));
            if (inspectWithMira)
            {
                foreach (var path in new[] { currentPath, MortalLocationMaterializationContract.WorldMapPath })
                    Assert.Equal(retained[path], await _fs.ReadFileBytesAsync(manifest.Files[path]));
                await WriteJsonAsync(currentPath, new { currentLocationData = new { locationId = JourneyLocationId } });
            }
            var journals = journalBefore.DeepClone();
            var row = Assert.Single(journals["NPCJournals"]!.AsArray(), j => (j!["npcId"] ?? j["NPCId"])?.GetValue<string>() == actorId)!;
            row["journalEntries"]!.AsArray().Add(new JsonObject
            {
                ["entryId"] = inspectWithMira ? "mira_crossing_initial_observation_43" : "steward_chalk_request_deferred_t034_visit43", ["timestamp"] = request.Timestamp,
                ["event"] = inspectWithMira ? "Начало осмотра" : "Просьба о меле", ["description"] = inspectWithMira ? "Я начала наблюдение из поместья. Отсюда видны не все опоры; состояние скрытых частей не проверено." : "Я записал просьбу о меле и передам её совету. Пока согласия нет, кладовая останется закрытой.",
                ["emotionalImpact"] = "сдержанность", ["relationshipChange"] = "0"
            });
            row["lastJournalNote"] = inspectWithMira ? "Я пока не могу судить о состоянии скрытых частей опор." : "Я передам просьбу совету, не выдавая мел без согласия.";
            expectedJournals = journals.DeepClone();
            await _fs.WriteFileAtomicAsync(journalPath, journals.ToJsonString());
            var time = await ReadCombinedJsonAsync(MortalLivingWorldSource.WorldTimePath);
            Assert.Equal(500, time["currentTimeInMinutes"]!.GetValue<int>());
            time["currentTimeInMinutes"] = 505; time["timeOfDay"] = "08:25";
            await _fs.WriteFileAtomicAsync(MortalLivingWorldSource.WorldTimePath, time.ToJsonString());
            var fixedPath = Path.Combine(TestRepoPaths.RepoRoot, "BookOfEternityClient.Tests", "TestData", "mm25-authored-debug-fixed.json");
            var debug = JsonNode.Parse(await File.ReadAllTextAsync(fixedPath))!;
            var reasoning = debug["gm_thoughts_markdown"]!.GetValue<string>()
                .Replace("Распорядитель двора совета", stewardName, StringComparison.Ordinal)
                .Replace("npc_steward_t034_visit42", stewardId, StringComparison.Ordinal)
                .Replace("loc_42ab98676d6f4e258e2250db02f656e4", locationId, StringComparison.Ordinal)
                .Replace("Хозяйственный двор у нижней лестницы", steward["currentLocationName"]!.GetValue<string>(), StringComparison.Ordinal);
            const string inspectionNarrative = "Вы идёте из двора к покоям Вальмонта и начинаете осмотр с Мирой. Часть опор не видна из поместья. Мира отмечает предел видимости; осмотр не завершён.";
            if (inspectWithMira)
                reasoning = $$"""
                    ## NPC Scope
                    - Mode: Scene-local
                    - Relevant actors: {{actorName}}
                    - Why relevant: Мира начинает наблюдение вместе с героем.
                    - Actors outside scope: {{stewardName}}
                    - Why outside scope: Распорядитель остаётся во дворе; его состояние не меняется.

                    ## Actor Brain 2.0
                    ### {{actorName}}
                    - Текущая локация: loc_valmont_private_chambers; остаётся в поместье.
                    - Ситуация: Герой просит начать осмотр, но не все опоры видны.
                    - Данные профиля: Наблюдательная осторожная разведчица, действует в рамках прежнего поручения.
                    - Мотивация: Сообщать только подтверждённое наблюдением.
                    - Ограничения: Скрытые части опор не проверены.
                    - Мысли: Я могу отметить предел видимости, но не подтверждать безопасность переправы.
                    - Варианты стратегий:
                      1. Записать предел наблюдения. Выгода: совет получит точную границу известного. Риск: окончательный вывод откладывается.
                      2. Объявить осмотр завершённым. Выгода: ответ немедленный. Риск: скрытые опоры не проверены.
                    - Выбранная стратегия: Начать наблюдение и записать предел видимости.
                    - Почему альтернативы отвергнуты: Нельзя считать скрытые части проверенными.
                    - Действия: Мира наблюдает из поместья и добавляет одну запись; не перемещается и не завершает поручение.
                    - Изменения состояния: В game_state/npcs/npc_journals.json только запись mira_crossing_initial_observation_43 и lastJournalNote у {{actorId}}. Герой прибывает в известную локацию; время 500→505 минут. Доступ, предметы и прогресс поручения не меняются.
                    """;
            await WriteMortalLivingWorldJourneyOutputAsync(request, inspectWithMira ? inspectionNarrative :
                "Распорядитель записывает просьбу и обещает передать её совету. Кладовая остаётся закрытой; мел не выдан.",
                inspectWithMira ? [journalPath, MortalLivingWorldSource.WorldTimePath, currentPath] :
                    [journalPath, MortalLivingWorldSource.WorldTimePath], reasoning, omitNoDueProgressionReport: true,
                authoredTimestamp: inspectWithMira ? request.Timestamp : null);
            foreach (var pair in retained.Where(pair => !inspectWithMira || pair.Key != currentPath))
                Assert.Equal(pair.Value, await _fs.ReadFileBytesAsync(pair.Key));
        });
        try
        {
            await runtime.Engine.ProcessPlayerTurn(action);
        }
        finally
        {
            await File.WriteAllTextAsync(Path.Combine(artifact, "ordinary43-logger.txt"), _journeyLogger.Describe());
        }
        Assert.NotNull(actualRequest);
        Assert.Equal(2, calls());
        Assert.True(JsonNode.DeepEquals(expectedJournals, await ReadCombinedJsonAsync(journalPath)));
        Assert.True(runtime.GameLoop.TurnNumber == 43, _journeyLogger.Describe());
        var story = (await _fs.ReadFileAsync(storyPath))!;
        Assert.StartsWith(priorStory, story);
        Assert.Equal(2, story.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length);
        Assert.Contains(inspectWithMira ? "осмотр не завершён" : "мел не выдан", story);
        if (inspectWithMira)
        {
            Assert.Equal(JourneyLocationId, (await ReadCombinedJsonAsync(currentPath))["locationId"]!.GetValue<string>());
            // Moving parks the original court contents off-screen; it must not take the chalk.
            var oldCurrent = JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(retained[currentPath]))!;
            var storage = Assert.Single(oldCurrent["locationStorages"]!.AsArray())!;
            var parked = await ReadCombinedJsonAsync(MortalLocationStorageContentsState.StatePath);
            var matching = Assert.Single(parked["entries"]!.AsArray(), entry =>
                entry!["locationId"]!.GetValue<string>() == locationId &&
                entry["storageId"]!.GetValue<string>() == storage["storageId"]!.GetValue<string>())!;
            Assert.True(JsonNode.DeepEquals(storage["contents"], matching["contents"]));
        }
        Assert.Equal(505, (await ReadCombinedJsonAsync(MortalLivingWorldSource.WorldTimePath))["currentTimeInMinutes"]!.GetValue<int>());
        var retainedAfterMove = new Dictionary<string, byte[]>();
        foreach (var (path, before) in retained)
        {
            var after = (await _fs.ReadFileBytesAsync(path))!;
            retainedAfterMove.Add(path, after);
            if (!inspectWithMira || (path != currentPath && path != MortalLocationMaterializationContract.WorldMapPath &&
                                     path != MortalLocationIdentityState.StatePath))
                Assert.True(before.AsSpan().SequenceEqual(after), $"Unexpected accepted-turn change: {path}");
        }
        if (inspectWithMira)
        {
            JsonObject Parse(byte[] raw) => JsonNode.Parse(CanonicalJsonUtf8.DecodeOneOptionalBom(raw))!.AsObject();
            var mapPath = MortalLocationMaterializationContract.WorldMapPath;
            var indexPath = MortalLocationIdentityState.StatePath;
            AssertMiraMovementProjections(Parse(retained[mapPath]), Parse(retained[indexPath]),
                Parse(retainedAfterMove[mapPath]), Parse(retainedAfterMove[indexPath]), JourneyLocationId, 43);
        }
        foreach (var path in new[] { LiveTurnPreparationService.TurnRequestPath,
            LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
            "game_state/control/validation_repair_request.json", "game_state/control/validation_repair_ready.json",
            "game_state/control/validation_diagnostic_failure_report.json", "ready/turn_complete.json", "ready/turn_error.json",
            ProgressionScheduleService.ReportPath }) Assert.False(_fs.FileExists(path), path);
        Assert.False(await runtime.Engine.ProcessLateTerminalAndIdleTransitionsForCurrentSessionAsync());
        Assert.True(runtime.Engine.HasRetiredOrdinaryOriginal);
        Assert.True(await runtime.Engine.ValidateBeforeOrdinaryPlayerTurnAsync(), _journeyLogger.Describe());
        Assert.Equal(43, runtime.GameLoop.TurnNumber);
        Assert.Equal(2, calls());
        foreach (var (path, accepted) in retainedAfterMove)
        {
            var afterPreSend = (await _fs.ReadFileBytesAsync(path))!;
            Assert.True(accepted.AsSpan().SequenceEqual(afterPreSend), $"Unexpected after-acceptance change: {path}");
        }
        var scheduleAfter = JsonSerializer.Deserialize<ProgressionScheduleState>(
            (await _fs.ReadFileAsync(ProgressionScheduleService.SchedulePath))!, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!;
        Assert.Equal(505, scheduleAfter.CurrentWorldTimeInMinutes);
        Assert.Equal(scheduleBefore.LastWorldSimulationTimeInMinutes, scheduleAfter.LastWorldSimulationTimeInMinutes);
        Assert.Equal(scheduleBefore.LastFactionSimulationTimeInMinutes, scheduleAfter.LastFactionSimulationTimeInMinutes);
        Assert.Equal(0, scheduleAfter.PendingWorldCycles);
        Assert.Equal(0, scheduleAfter.PendingFactionCycles);
        var snapshotDirectory = _fs.ResolvePath(LiveTurnPreparationService.PendingTurnSnapshotDirectory);
        Assert.True(!Directory.Exists(snapshotDirectory) || !Directory.EnumerateFileSystemEntries(snapshotDirectory).Any());
    }
}

public sealed class MiraMovementProjectionOracleDataTests
{
    [Fact]
    public void MiraMovementProjectionOracleKeepsExactCanonicalOrderAndHistory()
    {
        const string from = "loc_z_mira_origin";
        const string to = "loc_a_mira_target";
        var source = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(from, "Court");
        var target = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(to, "Rooms", "discovered", x: 1);
        var forward = MortalLocationTestFixture.CreateCanonicalLink(from, to);
        var reverse = MortalLocationTestFixture.CreateCanonicalLink(to, from);
        reverse["linkId"] = "lnk_a_mira_return";
        var envelope = reverse["materialization"]!.AsObject();
        envelope["initialId"] = "linkref_mira_return";
        envelope["materializationId"] = "mlinkmat_mira_return";
        var receipt = reverse["materializationReceipt"]!.AsObject();
        receipt["linkId"] = reverse["linkId"]!.DeepClone();
        receipt["initialId"] = envelope["initialId"]!.DeepClone();
        receipt["materializationId"] = envelope["materializationId"]!.DeepClone();
        receipt["receiptId"] = "mlinkrec_mira_return";
        receipt["seal"] = MortalLocationMaterializationContract.ComputeSeal(envelope, receipt);
        var map = MortalLocationTestFixture.CreateWorldMap(source, target);
        map["links"]!.AsArray().Add(forward.DeepClone());
        map["links"]!.AsArray().Add(reverse.DeepClone());
        var index = MortalLocationTestFixture.CreateIdentityIndex(source, forward);
        var targetIndex = MortalLocationTestFixture.CreateIdentityIndex(target, reverse);
        index["locationEntries"]!.AsArray().Add(targetIndex["locationEntries"]![0]!.DeepClone());
        index["linkEntries"]!.AsArray().Add(targetIndex["linkEntries"]![0]!.DeepClone());
        var origin = MortalLocationTestFixture.CreateCurrentProjection(source);
        MortalLocationAcceptedTurnPlan Select(JsonObject beforeMap, JsonObject beforeIndex, JsonObject current, int turn)
        {
            var result = MortalLocationAcceptedTurnPlanner.Build(new(
                beforeMap, current, beforeIndex, new JsonObject { ["locationId"] = to }, null, turn));
            Assert.True(result.Success, string.Join(", ", result.Issues.Select(i => i.Code + " " + i.FilePath)));
            return result.Plan!;
        }
        var first = Select(map, index, origin, 43);
        var second = Select(first.FinalWorldMap, first.FinalIdentityIndex, first.FinalCurrentLocation!, 44);
        var prior = second.FinalIdentityIndex.DeepClone().AsObject();
        foreach (var section in new[] { "locationEntries", "linkEntries" })
        {
            var rows = prior[section]!.AsArray();
            prior[section] = new JsonArray(rows.Reverse().Select(row => row!.DeepClone()).ToArray());
        }
        var actual = Select(second.FinalWorldMap, prior, origin, 45);
        Assert.Equal(new[] { to, from }, actual.FinalIdentityIndex["locationEntries"]!.AsArray()
            .Select(row => row!["locationId"]!.GetValue<string>()));
        Assert.Equal(new[] { "lnk_a_mira_return", MortalLocationTestFixture.LinkId },
            actual.FinalIdentityIndex["linkEntries"]!.AsArray().Select(row => row!["linkId"]!.GetValue<string>()));
        void Check(JsonObject actualMap, JsonObject actualIndex) =>
            GameEngineTurnLifecycleTests.AssertMiraMovementProjections(second.FinalWorldMap, prior, actualMap, actualIndex, to, 45);
        var inputSnapshots = new[] {second.FinalWorldMap, prior, actual.FinalWorldMap, actual.FinalIdentityIndex}
            .Select(node => (Node: node, Json: node.ToJsonString())).ToArray();
        Check(actual.FinalWorldMap, actual.FinalIdentityIndex);
        foreach (var (node, originalJson) in inputSnapshots)
            Assert.Equal(originalJson, node.ToJsonString());

        JsonArray History(JsonObject data) => data["locationEntries"]![0]!["transitions"]!.AsArray();
        var reordered = actual.FinalIdentityIndex.DeepClone().AsObject();
        var history = History(reordered);
        Assert.Equal(3, history.Count);
        var firstHistoricalRow = history[0]!.DeepClone();
        history.RemoveAt(0);
        history.Insert(1, firstHistoricalRow);
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => Check(actual.FinalWorldMap, reordered)));
        var changed = actual.FinalIdentityIndex.DeepClone().AsObject();
        History(changed)[0]!["operationRef"] = "wrong_prior_operation";
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => Check(actual.FinalWorldMap, changed)));
        var extra = actual.FinalIdentityIndex.DeepClone().AsObject();
        History(extra).Add(History(extra)[^1]!.DeepClone());
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => Check(actual.FinalWorldMap, extra)));
        var malformed = actual.FinalIdentityIndex.DeepClone().AsObject();
        History(malformed)[^1]!["sourceAuthorityId"] = "turn_44";
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => Check(actual.FinalWorldMap, malformed)));
        var changedMap = actual.FinalWorldMap.DeepClone().AsObject();
        changedMap["locations"]![0]!["lastEventsDescription"] = "unrelated mutation";
        Assert.IsAssignableFrom<Xunit.Sdk.XunitException>(Record.Exception(() => Check(changedMap, actual.FinalIdentityIndex)));
    }
}
#endif
