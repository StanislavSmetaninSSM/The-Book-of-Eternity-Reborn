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
            ConsoleSelections = fixture._console.SelectionChoicesHistory, Cut = probe.Cut.Evidence() }));
        probe.Cut.AssertReachedAndStopped();
        Assert.Same(probe.Cut.OriginalUncertainty, failure);
        Assert.Equal(0, probe.LaterInputs); Assert.Equal(0, probe.RequestAttempts);
        Assert.NotNull(priorAtCut);
        foreach (var pair in priorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
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
            _console.QueueSelection("Казначейство Сияющей Обители", "⬇ Внести Чернильные Перья");
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
            _console.QueueAnySelection("🔍 Искать новую обитель (силой мысли)", "🧲 Притяжение к извечному хранителю", "Азалия (Social)", "✅ Выбрать");
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
    internal int Inputs { get; private set; }
    internal int LaterInputs { get; private set; }
    internal int RequestAttempts { get; private set; }
    internal int IntegerResponse { get; set; } = 1;
    internal ExplorerStorageProbe()
    {
        Hooks = new FileSystemManagerHooks
        {
            LocalPublicationObserver = Cut.Hooks.LocalPublicationObserver,
            LocalPublicationRecoveryObserver = Cut.Hooks.LocalPublicationRecoveryObserver,
            BeforeCanonicalMutationBoundaryAsync = Cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
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
            return prompt is TextPrompt<int> ? (T)(object)probe.IntegerResponse : inner.Prompt(prompt);
        }
        public string? ReadLine() { probe.Input(); return inner.ReadLine(); }
        public bool KeyAvailable => inner.KeyAvailable;
        public ConsoleKeyInfo ReadKey() { probe.Input(); return inner.ReadKey(); }
    }
}
