using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;

namespace BookOfEternityClient.Tests;

// T034 authored DATA only. Real local UI/services, no turn/provider/hosted Load construction.
internal static class MortalFullRouteSmoke
{
    internal const string LocationName = "Хозяйственный двор у нижней лестницы";
    internal const string StorageName = "Запертая кладовая разметочных материалов";
    internal const string LocationId = "loc_t034_geometry";
    internal const string StorageId = "storage_t034_geometry";
    internal const string SwordId = "itm_t034_sword";
    internal const string ChalkId = "itm_t034_chalk";
    private static readonly string[] ObservedPaths = [StorageTransportMoveService.InventoryPath,
        StorageTransportMoveService.CurrentLocationPath, MortalLocationMaterializationContract.WorldMapPath,
        MortalLocationIdentityState.StatePath, MortalItemIdentityState.StatePath,
        MortalStorageAccessProjection.StatePath, "stories/t034.md", "config.json"];

    internal static async Task<int> RunAsync(string evidence, string plainOutput)
    {
        Console.InputEncoding = Encoding.UTF8; Console.OutputEncoding = Encoding.UTF8;
        if (!ClientStartupOptions.Parse([evidence, plainOutput], evidence).PlainOutput)
            throw new InvalidDataException("Expected --plain-output.");
        Environment.SetEnvironmentVariable("NO_COLOR", "1");
        var root = Path.Combine(evidence, "isolated-data");
        if (Directory.Exists(root) || File.Exists(root)) throw new IOException("Fixture destination exists.");
        Directory.CreateDirectory(root);
        Exception? failure = null;
        var stages = new List<object>();
        var cleanup = new List<string>();
        try
        {
            var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance);
            var settings = new GameSettings { Language = "ru", MusicEnabled = false, SoundEnabled = false,
                GenerateSceneImages = false, ShowImagesInConsole = false, AutoDiscardBrokenItems = false };
            var state = new StateManager(files, settings, NullLogger<StateManager>.Instance);
            var generation = await state.BootstrapLocalStorageAsync();
            var loc = new LocalizationManager();
            var clipboard = new SystemClipboardService(NullLogger<SystemClipboardService>.Instance);
            var explorer = new ExplorerMode(state, files, loc, clipboardService: clipboard,
                inputSource: SystemConsoleInputSource.Instance);
            var loop = new GameLoop(); loop.SetSession("t034-authored-geometry", 43);
            var saveLogger = new PortableSaveFixture.CaptureLogger();
            var saves = new SaveLoadService(files, state, saveLogger);
            var engine = CreateEngine(files, state, loop, explorer, loc, saves, clipboard);
            async Task<T> Expected<T>(Func<Task<T>> operation) =>
                await SessionOperationContext.RunParticipatingExpectedSessionAsync(files, generation, operation);
            async Task<Dictionary<string, string?>> Pins() => await Expected(async () =>
            {
                var pins = new Dictionary<string, string?>();
                foreach (var path in ObservedPaths)
                {
                    var bytes = await files.ReadFileBytesAsync(path);
                    pins[path] = bytes == null ? null : Hash(bytes);
                }
                foreach (var path in new[] { "input/turn_request.json", "ready/turn_complete.json", "ready/turn_error.json",
                    PendingTurnSnapshotAuthority.AuthorityPath, PendingPlayerActionService.PendingPath,
                    "game_state/control/pending_turn_snapshot.json", "game_state/control/validation_repair_request.json",
                    "game_state/control/validation_repair_ready.json" })
                    Require(!files.FileExists(path), "Unexpected turn/repair authority: " + path);
                Require(!Directory.Exists(files.ResolvePath("game_state/control/pending_turn_snapshot")), "Unexpected original directory.");
                return pins;
            });
            await Expected(async () => { await InstallAsync(files); await state.RefreshGameStateAsync(); return true; });
            Require(state.CurrentState.CurrentLocation == LocationName, "Actual StateManager location mismatch.");
            await Expected(async () =>
            {
                var context = await StorageTransportMoveService.ReadStorageMoveContextAsync(files);
                Require(context.Success && context.Storages.Count == 0 && context.InventoryItems.Count == 1,
                    "Locked fixture reader precondition: " + context.Message);
                Require(!ValidationService.ValidateCanonicalPlayerSkillMastery(
                    await files.ReadFileAsync(ValidationService.CanonicalActiveSkillsPath),
                    await files.ReadFileAsync(ValidationService.CanonicalSkillMasteryPath)).Any(i => i.Severity == IssueSeverity.Error),
                    "Skill/mastery setup is invalid.");
                return true;
            });
            Write("geometry.json", new { ConsoleWidth = Console.WindowWidth, ConsoleHeight = Console.WindowHeight,
                SpectreWidth = AnsiConsole.Profile.Width, SpectreHeight = AnsiConsole.Profile.Height,
                InputRedirected = Console.IsInputRedirected, OutputRedirected = Console.IsOutputRedirected,
                ErrorRedirected = Console.IsErrorRedirected });
            var before = await Pins();
            await ExplorerStage("locked-denial");
            Require(before.OrderBy(x => x.Key).SequenceEqual((await Pins()).OrderBy(x => x.Key)), "Locked interaction mutated DATA.");
            await Expected(async () => { await GrantFixtureAccessAsync(files); await state.RefreshGameStateAsync(); return true; });
            await ExplorerStage("retrieve");
            await Expected(async () => { await RequirePosition(files, player: true); await state.RefreshGameStateAsync(); return true; });
            Require(state.CurrentState.CurrentLocation == LocationName && loop.TurnNumber == 43, "Save metadata source changed.");
            var saveBefore = await Pins();
            await OptionsStage("save");
            var archive = await Expected(async () =>
            {
                var found = await saves.GetAvailableSavesAsync();
                Require(found.Count == 1, "Expected exactly one actual save, no duplicate CreateSave.");
                var sole = found.Single();
                Require(sole.Metadata?.SaveName == "s" && sole.Metadata.TurnNumber == 43 && sole.Metadata.CurrentLocation == LocationName,
                    "Actual save metadata mismatch.");
                Require(saveLogger.Errors.Count == 0, "Actual Save emitted failure: " + string.Join("\n", saveLogger.Errors));
                using var zip = ZipFile.OpenRead(sole.FileName);
                foreach (var path in ObservedPaths)
                {
                    var entry = zip.GetEntry(path);
                    Require(entry != null, "Missing saved member " + path);
                    using var stream = entry!.Open(); using var bytes = new MemoryStream(); stream.CopyTo(bytes);
                    Require(Hash(bytes.ToArray()) == saveBefore[path], "Saved bytes differ: " + path);
                }
                Require(!zip.Entries.Any(e => e.FullName.Contains("pending_turn_snapshot", StringComparison.Ordinal)), "Save contains original.");
                var receipt = new { Path = sole.FileName, SHA256 = Hash(File.ReadAllBytes(sole.FileName)), sole.Metadata,
                    sole.FileSize, LoadLabel = $"s | Ход 43 | {LocationName} | {sole.Metadata!.Timestamp:dd.MM.yyyy HH:mm} | {sole.FileSize / 1024}KB" };
                Write("archive.json", receipt);
                return (sole.FileName, Hash(File.ReadAllBytes(sole.FileName)));
            });
            await ExplorerStage("deposit");
            await Expected(async () => { await RequirePosition(files, player: false); return true; });
            var loadBefore = await Pins();
            await OptionsStage("load-focus-back");
            Require(loadBefore.OrderBy(x => x.Key).SequenceEqual((await Pins()).OrderBy(x => x.Key)), "Back-only Load changed DATA.");
            Require(Hash(File.ReadAllBytes(archive.FileName)) == archive.Item2, "Back-only Load changed archive.");
            Write("completed.json", new { Success = true, Generation = generation, Turn = loop.TurnNumber,
                Stages = stages, ProviderConstructed = false, LoadCommitAttempted = false, HostedReplacementClaimed = false });

            async Task ExplorerStage(string stage)
            {
                var prior = await Pins(); Write("stage.json", new { Stage = stage, Generation = generation, Before = prior });
                // The existing expected-generation participation owns the actual public dispatch,
                // rather than a second CurrentSession scope that could adopt a replacement.
                var action = await Expected(() => explorer.TryProcessCommand("/storage_move"));
                Require(action == string.Empty, "Local route refused/null or returned a GM action.");
                var after = await Pins(); Require(prior["stories/t034.md"] == after["stories/t034.md"], "Local Explorer changed story.");
                stages.Add(new { Stage = stage, ReturnedAction = action, Before = prior, After = after });
                Write(stage + ".json", stages.Last());
            }
            async Task OptionsStage(string stage)
            {
                var prior = await Pins(); Write("stage.json", new { Stage = stage, Generation = generation, Before = prior });
                Require(await Expected(engine.InGameOptionsMenu), "Options did not return continuation.");
                var after = await Pins(); Require(prior["stories/t034.md"] == after["stories/t034.md"], "Options changed story.");
                stages.Add(new { Stage = stage, Returned = true, Before = prior, After = after });
                Write(stage + ".json", stages.Last());
            }
        }
        catch (Exception error) { failure = error; }
        finally
        {
            // This root belongs solely to this invocation. Never remove an old/live root.
            try { Directory.Delete(root, recursive: true); } catch (Exception error) { cleanup.Add(error.ToString()); }
            Write("fullroute-result.json", new { Success = failure == null && cleanup.Count == 0, Failure = failure?.ToString(),
                Stages = stages, CleanupFailures = cleanup, FixtureRootAbsent = !Directory.Exists(root) });
        }
        return failure == null && cleanup.Count == 0 ? 0 : 1;
        void Write(string name, object value)
        {
            var temporary = Path.Combine(evidence, name + ".tmp");
            File.WriteAllText(temporary, JsonSerializer.Serialize(value));
            File.Move(temporary, Path.Combine(evidence, name), overwrite: true);
        }
    }

    private static GameEngine CreateEngine(FileSystemManager files, StateManager state, GameLoop loop,
        ExplorerMode explorer, LocalizationManager loc, SaveLoadService saves, IClipboardService clipboard) =>
        new(fs: files, stateManager: state, gameLoop: loop, normalizer: null!, progressionSchedule: null!,
            ui: null!, explorer: explorer, loc: loc, saveLoad: saves, imageService: null!, validator: null!,
            charService: null!, storyService: null!, actorMemoryService: null!,
            audioService: new AudioService(files, state.Settings, NullLogger<AudioService>.Instance),
            consoleAppearance: new ConsoleAppearanceService(state.Settings, NullLogger<ConsoleAppearanceService>.Instance),
            systemModService: new SystemModService(files, state.Settings, NullLogger<SystemModService>.Instance),
            systemGuardianLibraryService: null!, criticalStateHealth: null!, worldDirectiveService: null!, scenarioCoreService: null!,
            afterlifeArchiveCandidateService: null!, afterlifeReturnGuardService: null!, rivalSoulArcService: null!,
            guardianCorrectionService: null!, pendingTurnState: null!, qteSceneService: null!, clipboardService: clipboard,
            logger: NullLogger<GameEngine>.Instance, inputSource: SystemConsoleInputSource.Instance);

    private static async Task InstallAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        async Task Put(string path, JsonNode value) => await files.WriteFileAtomicAsync(lease, path, value.ToJsonString());
        var sword = MortalItemTestFixture.CreateCanonicalRoot(SwordId);
        sword["name"] = "Древний меч"; sword["type"] = "Оружие"; sword["weight"] = 1.8; sword["equipmentSlot"] = "MainHand";
        sword["materialization"]!["sections"]!["equipment"] = new JsonObject { ["state"] = "populated", ["reason"] = null };
        MortalItemTestFixture.ResealCanonical(sword);
        var chalk = MortalItemTestFixture.CreateCanonicalRoot(ChalkId);
        chalk["name"] = "Кусочек разметочного мела"; chalk["type"] = "Бытовой предмет"; chalk["weight"] = 0.02; chalk["volume"] = 0.01;
        MortalItemTestFixture.ResealCanonical(chalk);
        await Put(StorageTransportMoveService.InventoryPath, new JsonObject { ["items"] = new JsonArray(sword.DeepClone()),
            ["equippedItems"] = new JsonObject { ["MainHand"] = SwordId }, ["money"] = 500, ["totalWeight"] = "1.8kg", ["maxWeight"] = "45.0kg", ["isOverloaded"] = false });
        var location = MortalLocationTestFixture.CreateCanonicalLocationWithIdentity(LocationId, LocationName, discoveryTier: "visited");
        location["locationStorages"] = new JsonArray(MortalLocationTestFixture.CreateStorageMetadata(StorageId, StorageName, false));
        location["materialization"]!["sections"]!["storageMetadata"] = new JsonObject { ["disposition"] = "populated", ["reason"] = null };
        MortalLocationTestFixture.ResealCanonicalLocation(location);
        var current = MortalLocationTestFixture.CreateCurrentProjection(location);
        current["locationStorages"]![0]!["contents"] = new JsonArray(chalk.DeepClone());
        await Put(MortalLocationMaterializationContract.WorldMapPath, MortalLocationTestFixture.CreateWorldMap(location));
        await Put(StorageTransportMoveService.CurrentLocationPath, current);
        await Put(MortalLocationIdentityState.StatePath, MortalLocationTestFixture.CreateIdentityIndex(location));
        await Put(MortalItemIdentityState.StatePath, MortalItemTestFixture.CreateIndexForCarriers(
            (sword, "player_inventory", "player", null), (chalk, "location_storage", LocationId, StorageId)));
        await Put("game_state/meta/soul_state.json", new JsonObject { ["soulName"] = "T034 fixture", ["currentRealm"] = "Mortal World", ["currentIncarnation"] = 2 });
        await files.WriteFileAtomicAsync(lease, "stories/t034.md", "# T034 authored geometry DATA\n");
        var bootstrap = MortalBootstrapStateBuilder.BuildFreshMortalBootstrapFiles(2, 41, null, null, null, DateTimeOffset.UnixEpoch);
        foreach (var path in new[] { StorageTransportMoveService.VehiclesPath, "game_state/npcs/npc_skills.json", "game_state/inventory/item_bonds.json", "game_state/inventory/item_text_updates.json" })
            await Put(path, bootstrap[path]);
        var resources = ResourceBootstrapStateBuilder.BuildMortalPlayer(2, 41, 20, 20, 20, 20, 20);
        Require(resources.IsValid, JsonSerializer.Serialize(resources.Issues));
        await files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(lease, ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(resources.Definitions,
            path => files.ReadFileAsync(lease, path), resources.State, resources.History, CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        Require(authority.IsValid, JsonSerializer.Serialize(authority.Issues));
        await files.WriteFileAtomicAsync(lease, CanonicalResourceOwnerAuthorityComposer.AuthorityPath, authority.CanonicalAuthorityJson!);
        await WriteAccess(files, lease);
    }

    private static async Task GrantFixtureAccessAsync(FileSystemManager files)
    {
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        foreach (var path in new[] { MortalLocationMaterializationContract.WorldMapPath, StorageTransportMoveService.CurrentLocationPath })
        {
            var root = JsonNode.Parse((await files.ReadFileAsync(lease, path))!)!.AsObject();
            var location = path == StorageTransportMoveService.CurrentLocationPath ? root : root["locations"]![0]!.AsObject();
            location["locationStorages"]![0]!["hasFullAccess"] = true;
            await files.WriteFileAtomicAsync(lease, path, root.ToJsonString());
        }
        await WriteAccess(files, lease);
    }
    private static async Task WriteAccess(FileSystemManager files, FileSystemManager.CanonicalWriteLease lease)
    {
        var map = JsonNode.Parse((await files.ReadFileAsync(lease, MortalLocationMaterializationContract.WorldMapPath))!)!.AsObject();
        var projection = MortalStorageAccessProjection.Build(map, null);
        Require(projection.IsValid, JsonSerializer.Serialize(projection.Issues));
        await files.WriteFileAtomicAsync(lease, MortalStorageAccessProjection.StatePath, projection.Root!.ToJsonString());
    }
    private static async Task RequirePosition(FileSystemManager files, bool player)
    {
        var context = await StorageTransportMoveService.ReadStorageMoveContextAsync(files);
        Require(context.Success && context.Storages.Count == 1, "Storage context lost: " + context.Message);
        Require(context.InventoryItems.Any(i => i.Identity == ChalkId) == player &&
            context.Storages.Single().Contents.Any(i => i.Identity == ChalkId) != player, "Exact chalk carrier mismatch.");
    }
    private static string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    private static void Require(bool condition, string message) { if (!condition) throw new InvalidDataException(message); }
}
