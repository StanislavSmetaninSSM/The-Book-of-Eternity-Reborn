using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class ExplorerModeCommandTests
{
    [Theory]
    [InlineData("normalize")]
    [InlineData("equip")]
    [InlineData("unequip")]
    [InlineData("offering_relic")]
    [InlineData("offering_archive")]
    [InlineData("offering_feathers")]
    [InlineData("treasury")]
    [InlineData("companion")]
    [InlineData("faction")]
    [InlineData("candidate")]
    [InlineData("realignment")]
    [InlineData("leadership")]
    [InlineData("attraction")]
    [InlineData("forge")]
    public async Task StorageUnknown_OriginalExplorerCommandStopsWithoutCompensationOrInput(string mode)
    {
        using var outerRoot = new CleanupOwnedFixture(_rootPath, WriteStorageOutcome);
        Assert.True(OperatingSystem.IsLinux());
        var probe = new ExplorerStorageProbe();
        using var fixture = new ExplorerModeCommandTests(probe.Hooks, probe.Wrap);
        using var owned = new CleanupOwnedFixture(fixture._rootPath, WriteStorageOutcome);
        using var observer = probe;
        probe.Attach(fixture._fs);
        var (command, target) = await fixture.SeedExplorerStorageOutcomeAsync(mode, probe);
        await fixture._stateManager.RefreshGameStateAsync();
        string? selectedPath = null;
        byte[]? pendingAtCut = null;
        Dictionary<string, byte[]?>? priorAtCut = null;
        probe.Cut.Select = (path, member) =>
        {
            var selected = mode == "forge" ? probe.IsForgeMember(path, fixture._fs) :
                path == fixture._fs.ResolvePath(target) && member.GetProperty("After").GetProperty("Exists").GetBoolean();
            if (selected) selectedPath = path;
            return selected;
        };
        probe.Cut.BeforeCut = () =>
        {
            var published = File.ReadAllText(selectedPath!);
            if (mode != "forge") fixture.AssertExplorerStoragePublishedImage(mode, JsonNode.Parse(published)!);
            else probe.AssertForgeJournal(fixture._fs);
            using var current = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
            var member = current.RootElement.GetProperty("Members")[0];
            var before = member.GetProperty("Before").GetProperty("Exists").GetBoolean()
                ? member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64() : null;
            Assert.NotEqual(before, File.ReadAllBytes(selectedPath!));
            pendingAtCut = CleanupPublicationCut.ReadOptional(fixture._fs.ResolvePath(GuardianAbodeOfferingState.PendingRequestPath));
            if (mode.StartsWith("offering_", StringComparison.Ordinal))
            {
                Assert.NotNull(pendingAtCut);
                Assert.Equal(probe.Committed[fixture._fs.ResolvePath(GuardianAbodeOfferingState.PendingRequestPath)], pendingAtCut);
            }
            if (mode == "treasury") Assert.Contains(fixture._fs.ResolvePath(ShiningAbodeState.StatePath), probe.Committed.Keys);
            priorAtCut = probe.Committed.Where(x => x.Key != selectedPath)
                .ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
            foreach (var pair in priorAtCut) Assert.Equal(probe.Committed[pair.Key], pair.Value);
        };
        probe.Cut.Armed = true;
        string? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture._explorer.TryProcessCommand(command));
        var afterImages = priorAtCut?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        WriteStorageOutcome(JsonSerializer.Serialize(new { mode, command, target, result, Failure = failure?.ToString(),
            probe.Inputs, probe.LaterInputs, probe.RequestAttempts, pendingAtCut, priorAtCut, afterImages,
            ConsoleSelections = fixture._console.SelectionChoicesHistory, ActualSelections = probe.ActualSelections, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Equal(0, probe.LaterInputs); Assert.Equal(0, probe.RequestAttempts);
        Assert.NotNull(priorAtCut);
        foreach (var pair in priorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
    }

    [Theory]
    [InlineData("offering_throw", true)]
    [InlineData("offering_false", true)]
    [InlineData("treasury", true)]
    [InlineData("offering_throw", false)]
    [InlineData("treasury", false)]
    public async Task StorageCompensation_OriginalCommandStopsUnknownAndPreservesKnownRollback(string mode, bool uncertain)
    {
        using var outerRoot = new CleanupOwnedFixture(_rootPath, WriteStorageOutcome);
        Assert.True(OperatingSystem.IsLinux());
        var probe = new ExplorerStorageProbe();
        using var fixture = new ExplorerModeCommandTests(probe.Hooks, probe.Wrap);
        using var owned = new CleanupOwnedFixture(fixture._rootPath, WriteStorageOutcome);
        using var observer = probe;
        probe.Attach(fixture._fs);
        var treasury = mode == "treasury";
        var (command, _) = await fixture.SeedExplorerStorageOutcomeAsync(treasury ? "treasury" : "offering_relic", probe);
        await fixture._stateManager.RefreshGameStateAsync();
        const string soul = "game_state/meta/soul_state.json";
        var pending = GuardianAbodeOfferingState.PendingRequestPath;
        var soulFull = fixture._fs.ResolvePath(soul);
        var shiningFull = fixture._fs.ResolvePath(ShiningAbodeState.StatePath);
        var pendingFull = fixture._fs.ResolvePath(pending);
        var soulBefore = File.ReadAllBytes(soulFull);
        var shiningBefore = CleanupPublicationCut.ReadOptional(shiningFull);
        var known = new InvalidOperationException("known original command prepublication refusal");
        var knownHits = 0;
        var events = new List<string>();
        Dictionary<string, byte[]?>? evidenceBefore = null;
        Dictionary<string, byte[]?>? atCut = null;
        byte[]? committedShining = null;
        var selected = treasury ? shiningFull : mode == "offering_false" ? pendingFull : soulFull;
        probe.BeforeMutationBoundary = path =>
        {
            if (!probe.Cut.Armed || probe.Cut.Cuts != 0 || knownHits != 0) return Task.CompletedTask;
            if (path != (mode == "offering_throw" ? pending : soul)) return Task.CompletedTask;
            if (treasury)
            {
                Assert.True(probe.Committed.TryGetValue(shiningFull, out committedShining));
                Assert.NotEqual(shiningBefore, committedShining);
                Assert.Equal(committedShining, File.ReadAllBytes(shiningFull));
            }
            else
            {
                var snapshot = (ExplorerMode.PendingLocalTurnRollbackSnapshot?)typeof(ExplorerMode)
                    .GetField("_pendingLocalTurnRollbackSnapshot", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .GetValue(fixture._explorer);
                Assert.NotNull(snapshot);
                Assert.False(snapshot!.RestoreCompleted);
                Assert.Equal(soulBefore, File.ReadAllBytes(fixture._fs.ResolvePath(snapshot.BackupFiles[soul])));
                evidenceBefore = snapshot.TechnicalArtifacts.Concat(snapshot.BackupFiles.Values).Distinct(StringComparer.Ordinal)
                    .ToDictionary(x => fixture._fs.ResolvePath(x), x => CleanupPublicationCut.ReadOptional(fixture._fs.ResolvePath(x)), StringComparer.Ordinal);
                Assert.All(evidenceBefore.Values, value => Assert.NotNull(value));
                if (mode == "offering_false")
                    Assert.Equal(probe.Committed[pendingFull], File.ReadAllBytes(pendingFull));
                else Assert.False(File.Exists(pendingFull));
            }
            events.Add("known prepublication refusal: " + path);
            knownHits++;
            throw known;
        };
        probe.AfterCommitted = path =>
        {
            if (knownHits != 0) events.Add("committed after known refusal: " + path);
        };
        probe.Cut.Select = (path, member) => uncertain && knownHits == 1 && path == selected &&
            member.GetProperty("After").GetProperty("Exists").GetBoolean() == (mode != "offering_false");
        probe.Cut.BeforeCut = () =>
        {
            Assert.Equal(1, knownHits);
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
            var member = journal.RootElement.GetProperty("Members")[0];
            if (treasury)
            {
                Assert.Equal(committedShining, member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                Assert.Equal(shiningBefore, File.ReadAllBytes(shiningFull));
            }
            else if (mode == "offering_throw")
            {
                // The real restoration publishes unchanged baseline bytes; equality is intentional.
                Assert.Equal(soulBefore, member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                Assert.Equal(soulBefore, File.ReadAllBytes(soulFull));
            }
            else
            {
                Assert.True(member.GetProperty("Before").GetProperty("Exists").GetBoolean());
                Assert.Equal(probe.Committed[pendingFull], member.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64());
                Assert.False(File.Exists(pendingFull));
            }
            if (evidenceBefore != null)
                foreach (var pair in evidenceBefore) Assert.Equal(pair.Value, CleanupPublicationCut.ReadOptional(pair.Key));
            atCut = (evidenceBefore ?? new Dictionary<string, byte[]?>()).ToDictionary(x => x.Key, x => x.Value, StringComparer.Ordinal);
            if (selected != soulFull) atCut[soulFull] = File.ReadAllBytes(soulFull);
            events.Add("actual restoration MemberPublished: " + selected);
        };
        probe.Cut.Armed = true;
        string? result = null;
        var failure = await Record.ExceptionAsync(async () => result = await fixture._explorer.TryProcessCommand(command));
        var afterImages = atCut?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        var soulAfter = CleanupPublicationCut.ReadOptional(soulFull);
        var shiningAfter = CleanupPublicationCut.ReadOptional(shiningFull);
        var evidenceAfter = evidenceBefore?.ToDictionary(x => x.Key, x => CleanupPublicationCut.ReadOptional(x.Key), StringComparer.Ordinal);
        WriteStorageOutcome(JsonSerializer.Serialize(new { mode, uncertain, command, result, knownHits, events,
            KnownCauseRetained = probe.Cut.RetainsDiagnostic(known), Failure = failure?.ToString(),
            soulBefore, soulAfter, shiningBefore, shiningAfter, committedShining, evidenceBefore, evidenceAfter,
            atCut, afterImages, probe.Inputs, probe.LaterInputs, probe.RequestAttempts, Cut = probe.Cut.Evidence() }));
        Assert.Equal(1, knownHits); Assert.Equal(0, probe.RequestAttempts);
        if (uncertain)
        {
            probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
            Assert.Equal(0, probe.LaterInputs);
            // The bool-failure route deliberately has no exception-retention contract.
            if (mode != "offering_false") Assert.True(probe.Cut.RetainsDiagnostic(known));
            Assert.NotNull(atCut);
            foreach (var pair in atCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
        }
        else
        {
            Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts); Assert.False(File.Exists(probe.Cut.JournalPath));
            Assert.Equal(soulBefore, soulAfter);
            if (treasury) Assert.Equal(shiningBefore, shiningAfter);
            else { Assert.False(File.Exists(pendingFull)); Assert.All(evidenceAfter!.Values, value => Assert.Null(value)); }
            Assert.Contains(events, x => x == "committed after known refusal: " + (treasury ? shiningFull : soulFull));
        }
    }

    private void WriteStorageOutcome(string line) => _storageOutcomeOutput?.WriteLine(line);

    private async Task<(string Command, string Target)> SeedExplorerStorageOutcomeAsync(string mode, ExplorerStorageProbe probe)
    {
        const string soulPath = "game_state/meta/soul_state.json";
        if (mode is "normalize" or "equip" or "unequip")
        {
            await SeedAfterlifeStateAsync();
            var soul = JsonNode.Parse((await _fs.ReadFileAsync(soulPath))!)!.AsObject();
            var relic = new JsonObject { ["relicId"] = "storage_relic", ["name"] = "Storage relic", ["quality"] = "rare", ["slot"] = "talisman",
                ["gameplayStatus"] = new JsonObject { ["equipped"] = mode == "unequip" } };
            soul["soulRelics"] = mode == "normalize" ? new JsonArray(relic) : new JsonObject
            {
                ["equipped"] = mode == "unequip" ? new JsonArray(relic) : new JsonArray(),
                ["stored"] = mode == "equip" ? new JsonArray(relic) : new JsonArray()
            };
            await WriteJsonAsync(soulPath, soul);
            _console.QueueSelection("Действие", mode == "equip" ? "⚔ Экипировать" : "📦 Снять (в хранилище)");
            _console.QueueSelection("Подтвердить локальную", mode == "equip" ? "✅ Экипировать" : "✅ Снять");
            return ("/реликвии", soulPath);
        }
        if (mode.StartsWith("offering_", StringComparison.Ordinal))
        {
            await SeedAbodeOfferingRelicAliasStateAsync("quality", "Rare");
            var soul = JsonNode.Parse((await _fs.ReadFileAsync(soulPath))!)!.AsObject();
            soul["inkFeathers"] = new JsonObject { ["current"] = 100, ["total"] = 100 };
            soul["afterlifeArchive"] = new JsonObject { ["stored"] = new JsonArray(new JsonObject
            {
                ["archiveId"] = "storage_archive", ["entryType"] = "lore_fragment", ["title"] = "Storage archive",
                ["summary"] = "A complete local entry", ["content"] = "Bounded content", ["rarity"] = "Rare", ["sourceLife"] = 4,
                ["sourceKind"] = "codex", ["acquiredAtUtc"] = "2026-10-09T00:00:00Z"
            }), ["actionReceipts"] = new JsonArray() };
            await WriteJsonAsync(soulPath, soul);
            _console.QueueSelection("Чем поднести", mode switch { "offering_relic" => "💎 Реликвия Души", "offering_archive" => "📚 Фрагмент Знания", _ => "🪶 Чернильные Перья" });
            probe.IntegerResponse = 50;
            return ("/подношение_обители", soulPath);
        }
        if (mode == "treasury")
        {
            await SeedShiningInspectionStateAsync(includePreparedPackage: false);
            _console.QueueSelection("Казначейство Сияющей Обители", "⬇ Внести Чернильные Перья", "← Назад");
            _console.QueueAnyAskResponse("1"); _console.QueueAnyConfirmResponse(true);
            return ("/shining_treasury", soulPath);
        }
        if (mode is "companion" or "faction")
        {
            await SeedMortalStateAsync();
            var path = mode == "companion" ? "game_state/npcs/npc_core.json" : "game_state/factions/faction_core.json";
            if (mode == "companion") await WriteJsonAsync(path, new { UpdateNPCs = new[] { new { npcId = "storage_companion", name = "Лира", progressionType = "Companion", playerCompanionDirective = "" } } });
            else await WriteJsonAsync(path, new[] { new { factionId = "storage_faction", name = "Дом Пепла", isPlayerFaction = true, playerStrategyDirective = "" } });
            _console.QueueAnyAskResponse("Storage outcome directive");
            return (mode == "companion" ? "/директива_компаньону" : "/директива_фракции", path);
        }
        if (mode is "realignment" or "leadership" or "forge")
            return await SeedExplorerShiningStorageOutcomeAsync(mode);
        if (mode == "attraction")
        {
            await SeedAfterlifeStateAsync();
            await SeedSystemGuardianPresetAsync("azalia", "Азалия", "Social", "Обитель Неутолимого Пламени");
            await WriteJsonAsync("game_state/meta/guardians.json", new { guardians = Array.Empty<object>(), chaosSeaNavigation = new { discoveredAbodes = Array.Empty<string>() } });
            _console.QueueAnySelection("🔍 Искать новую обитель (силой мысли)", "🧲 Притяжение к извечному хранителю", "Азалия", "✅ Выбрать");
            return ("/хранители", SystemGuardianLibraryService.AttractionRequestPath);
        }
        Assert.Equal("candidate", mode);
        await SeedAfterlifeStateAsync();
        await WriteJsonAsync(soulPath, new { soulName = "Storage candidate", currentRealm = "Chaos Sea", currentIncarnation = 1,
            livesHistory = new[] { new { incarnation = 1 } }, afterlifeArchive = new { stored = Array.Empty<object>() } });
        await WriteJsonAsync("lore/codex_entries.json", new { entries = new[] { new { entryId = "storage_codex", title = "Storage codex", category = "history", content = "Bounded complete content", incarnation = 1, discoveredAt = "2026-10-09T00:00:00Z" } }, totalEntries = 1, categories = new { history = 1 } });
        return ("/архив_кандидаты", AfterlifeArchiveCandidateService.ManifestPath);
    }

    private async Task<(string Command, string Target)> SeedExplorerShiningStorageOutcomeAsync(string mode)
    {
        if (mode is "realignment" or "leadership")
        {
            await SeedShiningInspectionStateAsync(includePreparedPackage: false);
            if (mode == "realignment")
            {
                _console.QueueSelection("Режим перестройки", "Перейти в другую фракцию");
                _console.QueueSelection("Подтвердить перестройку резидента", "✅ Создать запрос");
                return ("/shining_faction_realignment", ShiningFactionRequestState.PendingRealignmentsRequestPath);
            }
            _console.QueueSelection("Режим смены главы", "отречение");
            _console.QueueAnyConfirmResponse(false);
            _console.QueueSelection("Подтвердить смену главы", "✅ Создать запрос");
            return ("/shining_faction_leadership", ShiningFactionRequestState.PendingLeadershipTransitionsRequestPath);
        }
        await SeedShiningInspectionStateAsync(includePreparedPackage: false);
        var forgeSoul = JsonNode.Parse((await _fs.ReadFileAsync("game_state/meta/soul_state.json"))!)!.AsObject();
        var forgeRelics = forgeSoul["soulRelics"]!["stored"]!.AsArray();
        var thirdRelic = forgeRelics[0]!.DeepClone().AsObject();
        thirdRelic["relicId"] = "storage_third_form";
        thirdRelic["name"] = "Storage third form";
        thirdRelic["formTag"] = "lance";
        forgeRelics.Add(thirdRelic);
        Assert.Equal(3, forgeRelics.Select(x => x!["formTag"]!.GetValue<string>()).Distinct(StringComparer.Ordinal).Count());
        await WriteJsonAsync("game_state/meta/soul_state.json", forgeSoul);
        await CanonicalResourceQuartetTestFixture.CommitFreshBootstrapAsync(
            _fs,
            new AfterlifeOwnerResourceAcceptedState(
                Profiles: CreatePlayerSoulProfileRoot("Shining Abode")));
        var materialized = await ShiningBlessingEffectState.MaterializeForBootstrapAsync(
            _fs,
            new JsonObject
            {
                ["preparedAtTurn"] = 155,
                ["selectedCardIds"] = new JsonArray("card_relic_reroll"),
                ["selectedCards"] = new JsonArray
                {
                    new JsonObject
                    {
                        ["cardId"] = "card_relic_reroll",
                        ["dedupeKey"] = "relic:card_relic_reroll",
                        ["sourceType"] = ShiningAbodeState.CardSourceTypeProject,
                        ["sourceFactionId"] = "faction_dawn",
                        ["sourceActorId"] = "project_refinement",
                        ["effectFamily"] = ShiningAbodeState.EffectFamilyRelic,
                        ["rarity"] = ShiningAbodeState.RarityCommon,
                        ["displayName"] = "Переброс реликвии",
                        ["displaySummary"] = "Один переброс реликвии.",
                        ["effectPayload"] = new JsonObject
                        {
                            ["type"] = "grant_relic_refinement",
                            [ShiningBlessingRerollAllocationContract.PropertyName] =
                                ShiningBlessingRerollAllocationContract.Create(1),
                            ["freeShape"] = false,
                            ["freeRetune"] = false
                        }
                    }
                }
            },
            currentIncarnation: 7);
        Assert.True(materialized.Success, materialized.ErrorMessage);

        _console.QueueSelection("[bold yellow]Сияющая Обитель[/]", "⚒ Торговля и кузня", "← Назад");
        _console.QueueSelection("Торговля и кузня Сияющей Обители", "⚒ Создать запрос на перековку", "← Назад");
        _console.QueueSelection("Выберите фракцию для кузни", "Хор Рассвета");
        _console.QueueSelection("Выберите действие кузни", "Перековать форму реликвии");
        _console.QueueSelection("Выберите Реликвию Души для перековки", "Стекло Пути");
        _console.QueueSelection("Новая форма реликвии", "🔄 Перебросить благословением (1)", "✅ Использовать предложенную форму");
        _console.QueueSelection("Подтвердить запрос на перековку", "✅ Создать запрос");
        return ("/shining_abode", ShiningCoreActionRequestState.PendingActionsRequestPath);
    }

    private void AssertExplorerStoragePublishedImage(string mode, JsonNode published)
    {
        if (mode is "normalize" or "equip" or "unequip")
        {
            var relics = Assert.IsType<JsonObject>(published["soulRelics"]);
            Assert.Single(relics[mode == "equip" ? "equipped" : "stored"]!.AsArray());
        }
        else if (mode == "offering_relic") Assert.Empty(published["soulRelics"]!["stored"]!.AsArray());
        else if (mode == "offering_archive") Assert.Empty(published["afterlifeArchive"]!["stored"]!.AsArray());
        else if (mode == "offering_feathers") Assert.Equal(50, published["inkFeathers"]!["current"]!.GetValue<int>());
        else if (mode == "treasury") Assert.Equal(20, published["inkFeathers"]!["current"]!.GetValue<int>());
        else if (mode == "candidate") Assert.Single(published["candidates"]!.AsArray());
        else if (mode is "realignment" or "leadership") Assert.Single(published["requests"]!.AsArray());
        else if (mode == "attraction") Assert.Contains("azalia", published.ToJsonString(), StringComparison.OrdinalIgnoreCase);
        else Assert.Contains("Storage outcome directive", published.ToJsonString(), StringComparison.Ordinal);
    }
}

internal sealed class ExplorerStorageProbe : IDisposable
{
    internal CleanupPublicationCut Cut { get; } = new();
    internal Dictionary<string, byte[]?> Committed { get; } = new(StringComparer.Ordinal);
    internal FileSystemManagerHooks Hooks { get; }
    internal List<string> ActualSelections { get; } = [];
    internal int Inputs { get; private set; }
    internal int LaterInputs { get; private set; }
    internal int RequestAttempts { get; private set; }
    internal int IntegerResponse { get; set; } = 1;
    internal Func<string, Task>? BeforeMutationBoundary { get; set; }
    internal Action<string>? AfterCommitted { get; set; }
    internal ExplorerStorageProbe()
    {
        Hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
            LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver,
            BeforeCanonicalMutationBoundaryAsync = async path =>
            {
                await Cut.Hooks.BeforeCanonicalMutationBoundaryAsync!(path);
                if (BeforeMutationBoundary != null) await BeforeMutationBoundary(path);
            },
            AfterCanonicalReadInitialValidationAsync = Cut.Hooks.AfterCanonicalReadInitialValidationAsync,
            SessionOperationClosingAsync = Cut.Hooks.SessionOperationClosingAsync,
            BeforeCanonicalWriteLockOpenAsync = async () =>
            {
                var before = Cut.LaterLeases;
                await Cut.Hooks.BeforeCanonicalWriteLockOpenAsync!();
                if (Cut.LaterLeases > before) throw new InvalidOperationException("fixture refuses post-Unknown canonical admission");
            },
            BeforeCanonicalMutationAsync = path =>
            {
                if (Cut.Armed && path == "input/turn_request.json")
                { RequestAttempts++; throw new InvalidOperationException("fixture refuses GM dispatch"); }
                return Task.CompletedTask;
            }
        };
        Cut.ObserveBeforeCut = (phase, _) =>
        {
            if (phase != TrustedLocalPublicationPhase.Committed) return;
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
            foreach (var member in journal.RootElement.GetProperty("Members").EnumerateArray())
            {
                var path = member.GetProperty("Path").GetString()!;
                Committed[path] = CleanupPublicationCut.ReadOptional(path);
                AfterCommitted?.Invoke(path);
            }
        };
    }
    internal bool IsForgeMember(string path, FileSystemManager files)
    {
        using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
        var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
        return members[0].GetProperty("Path").GetString() == path &&
            members.Any(x => x.GetProperty("Path").GetString() == files.ResolvePath(ShiningCoreActionRequestState.PendingActionsRequestPath)) &&
            members.Any(x => x.GetProperty("Path").GetString() == files.ResolvePath(ResourceMaterializationContract.StatePath));
    }
    internal void AssertForgeJournal(FileSystemManager files)
    {
        using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(Cut.JournalPath));
        Assert.Equal(1, journal.RootElement.GetProperty("Format").GetInt32());
        var members = journal.RootElement.GetProperty("Members").EnumerateArray().ToArray();
        var request = members.Single(x => x.GetProperty("Path").GetString() == files.ResolvePath(ShiningCoreActionRequestState.PendingActionsRequestPath));
        var planned = JsonNode.Parse(System.Text.Encoding.UTF8.GetString(request.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64()).TrimStart('\uFEFF'))!;
        var item = Assert.Single(planned["requests"]!.AsArray());
        Assert.Equal(ShiningCoreActionRequestState.ActionTypeForgeRelicReshape, item!["actionType"]!.GetValue<string>());
        Assert.False(string.IsNullOrWhiteSpace(item["requestId"]!.GetValue<string>()));
        var resource = members.Single(x => x.GetProperty("Path").GetString() == files.ResolvePath(ResourceMaterializationContract.StatePath));
        Assert.True(resource.GetProperty("Before").GetProperty("Exists").GetBoolean());
        Assert.NotEqual(resource.GetProperty("Before").GetProperty("Bytes").GetBytesFromBase64(),
            resource.GetProperty("After").GetProperty("Bytes").GetBytesFromBase64());
        // These are planned images in the same uncommitted journal, not independent committed effects.
    }
    internal void Attach(FileSystemManager files) => Cut.Attach(files);
    internal IExplorerConsole Wrap(TestExplorerConsole console) => new GuardedConsole(this, console);
    private void Input()
    {
        Inputs++;
        if (Cut.Cuts == 0) return;
        LaterInputs++;
        throw new InvalidOperationException("fixture refuses input after actual publication Unknown");
    }
    public void Dispose() => Cut.Dispose();
    private sealed class GuardedConsole(ExplorerStorageProbe probe, TestExplorerConsole inner) : IExplorerConsole
    {
        public void Clear() => inner.Clear();
        public void Write(IRenderable content) => inner.Write(content);
        public void WriteLine() => inner.WriteLine();
        public void Markup(string markup) => inner.Markup(markup);
        public void MarkupLine(string markup) => inner.MarkupLine(markup);
        public string Ask(string prompt, string defaultValue = "") { probe.Input(); return inner.Ask(prompt, defaultValue); }
        public bool Confirm(string prompt, bool defaultValue = false) { probe.Input(); return inner.Confirm(prompt, defaultValue); }
        public T Prompt<T>(IPrompt<T> prompt)
        {
            probe.Input();
            if (prompt is TextPrompt<int>) return (T)(object)probe.IntegerResponse;
            var requested = inner.Prompt(prompt);
            if (prompt is not SelectionPrompt<string> selection) return requested;
            var choices = (List<string>)typeof(TestExplorerConsole)
                .GetMethod("ReadChoices", System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)!
                .Invoke(null, [selection])!;
            var fragment = Assert.IsType<string>(requested);
            var exact = choices.Where(x => string.Equals(x, fragment, StringComparison.Ordinal)).ToArray();
            var matches = exact.Length != 0 ? exact : choices.Where(x => x.Contains(fragment, StringComparison.OrdinalIgnoreCase)).ToArray();
            var actual = Assert.Single(matches);
            probe.ActualSelections.Add(actual);
            return (T)(object)actual;
        }
        public string? ReadLine() { probe.Input(); return inner.ReadLine(); }
        public bool KeyAvailable => inner.KeyAvailable;
        public ConsoleKeyInfo ReadKey() { probe.Input(); return inner.ReadKey(); }
    }
}
