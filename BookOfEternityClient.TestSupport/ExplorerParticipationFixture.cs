using System.Security.Cryptography;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Minimal authored inventory/menu DATA, not a live accepted-world or main-owner substitute.</summary>
internal sealed class ExplorerParticipationFixture
{
    internal FileSystemManager Files { get; }
    internal StateManager State { get; }
    private ExplorerParticipationFixture(FileSystemManager files, StateManager state) { Files = files; State = state; }

    /// <summary>Seeds before owner launch; the accepted item shape forces actual resource projection rather than the empty-item shortcut.</summary>
    internal static async Task<ExplorerParticipationFixture> CreateAsync(FileSystemManager files)
    {
        files.EnsureDirectoryStructure();
        var item = MortalItemTestFixture.CreateCanonicalRoot();
        if (!MortalItemMaterializationContract.TryReadAcceptedIdentity(item, out var identity) || identity != MortalItemTestFixture.ItemId)
            throw new InvalidDataException("Fixture accepted item premise is invalid.");
        var resources = ResourceBootstrapStateBuilder.BuildPristine();
        if (!resources.IsValid) throw new InvalidDataException("Fixture pristine resource premise is invalid.");
        await files.WriteFileAtomicAsync("game_state/meta/soul_state.json", "{\"currentRealm\":\"Mortal World\"}");
        await files.WriteFileAtomicAsync("game_state/inventory/items.json", new JsonObject { ["items"] = new JsonArray(item) }.ToJsonString());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.DefinitionsPath, resources.Definitions!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.StatePath, resources.State!.ToCanonicalJson());
        await files.WriteFileAtomicAsync(ResourceMaterializationContract.HistoryPath, resources.History!.ToCanonicalJson());
        var projection = await ResourceProjectionService.ProjectCanonicalAsync(files,
            [new(new("mortal_world", ResourceOwnerKind.Item, identity), "предмет игрока №1", true)], ResourceProjectionAudience.Player);
        if (!projection.IsAvailable) throw new InvalidDataException("Fixture resource projection premise is unavailable.");
        var state = new StateManager(files, new GameSettings { AutoDiscardBrokenItems = false }, NullLogger<StateManager>.Instance);
        await state.RefreshGameStateAsync();
        return new(files, state);
    }

    /// <summary>Each dispatch receives a fresh deterministic console, with no retained operation or owner scope.</summary>
    internal (ExplorerMode Explorer, TestExplorerConsole Console) Menu()
    {
        var console = new TestExplorerConsole();console.QueueAnySelection("← Назад");
        return (new ExplorerMode(State, Files, new LocalizationManager(), console: console), console);
    }

    /// <summary>Checks the actual menu frame, not the empty return that also follows swallowed exceptions.</summary>
    internal static bool MenuObserved(TestExplorerConsole console) => console.SelectionChoicesHistory.Any(row =>
        row.Choices.Contains("← Назад") && row.Choices.Any(choice => choice.Contains("Test", StringComparison.OrdinalIgnoreCase) || choice.Contains("Тест", StringComparison.OrdinalIgnoreCase))) &&
        !console.MarkupLines.Any(line => line.Contains("IOException", StringComparison.Ordinal) || line.Contains("Ошибка при выполнении команды", StringComparison.Ordinal));

    /// <summary>Freezes canonical gameplay and protocol request bytes; excludes host-owned live diagnostics.</summary>
    internal string[] GameInventory() => Directory.GetFiles(Files.GameSessionPath, "*", SearchOption.AllDirectories)
        .Where(path => !Path.GetRelativePath(Files.GameSessionPath, path).Replace('\\','/').StartsWith("game_state/control/gm_bridge", StringComparison.Ordinal))
        .OrderBy(path => path, StringComparer.Ordinal).Select(path => Path.GetRelativePath(Files.GameSessionPath, path) + ":" +
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)))).ToArray();
}
