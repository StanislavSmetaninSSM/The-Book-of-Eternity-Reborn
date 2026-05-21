using System.Reflection;
using BookOfEternityClient.CommandProtocol;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class ExplorerCommandMigrationRegistryTests : IDisposable
{
    private readonly string _rootPath;

    public ExplorerCommandMigrationRegistryTests()
    {
        _rootPath = Path.Combine(Path.GetTempPath(), "boe-command-registry-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_rootPath);
    }

    [Fact]
    public void EveryRegisteredExplorerCommand_HasMigrationMetadata()
    {
        var registeredCommands = ReadRegisteredCommandNames();
        var metadataCommands = ExplorerCommandMigrationRegistry.Entries
            .Select(static entry => entry.Command)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = registeredCommands
            .Where(command => !metadataCommands.Contains(command))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Missing web UI migration metadata for registered commands: " + string.Join(", ", missing));
    }

    [Fact]
    public void MigrationRegistryEntries_AreUniqueAndActionable()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries;
        var duplicateCommands = entries
            .GroupBy(static entry => entry.Command, StringComparer.OrdinalIgnoreCase)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            duplicateCommands.Length == 0,
            "Duplicate migration metadata entries: " + string.Join(", ", duplicateCommands));

        var nonMigratedWithoutIssue = entries
            .Where(static entry => entry.Status != ExplorerCommandMigrationStatus.Migrated)
            .Where(static entry => string.IsNullOrWhiteSpace(entry.FollowUpIssue) || !entry.FollowUpIssue.Contains('#', StringComparison.Ordinal))
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            nonMigratedWithoutIssue.Length == 0,
            "Non-migrated commands must point to a follow-up issue: " + string.Join(", ", nonMigratedWithoutIssue));

        var blockedWithoutReason = entries
            .Where(static entry => entry.Status is ExplorerCommandMigrationStatus.Blocked or ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily)
            .Where(static entry => string.IsNullOrWhiteSpace(entry.Reason))
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            blockedWithoutReason.Length == 0,
            "Blocked or temporary console-only commands must have a reason: " + string.Join(", ", blockedWithoutReason));
    }

    [Fact]
    public void EveryMigratedExplorerCommand_HasWebDtoBuilderCoverage()
    {
        var migratedWithoutBuilder = ExplorerCommandMigrationRegistry.Entries
            .Where(static entry => entry.Status == ExplorerCommandMigrationStatus.Migrated)
            .Where(static entry => !HasWebDtoBuilder(entry.Command))
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            migratedWithoutBuilder.Length == 0,
            "Migrated commands must be backed by a shared ExplorerCommandResult DTO builder: " +
            string.Join(", ", migratedWithoutBuilder));
    }

    [Fact]
    public void HelpCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries["/help"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries["/помощь"].Status);
    }

    [Fact]
    public void UniversalMetaCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[] { "/status", "/статус", "/soul", "/душа", "/codex", "/кодекс", "/story", "/debug", "/галерея", "/saref", "/сареф" })
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
    }

    [Fact]
    public void MortalReadOnlyCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/inv", "/inventory", "/инв", "/npc", "/npcs", "/quests", "/квесты", "/map", "/карта",
                     "/where_am_i", "/где_я", "/factions", "/фракции", "/skills", "/навыки", "/stats", "/статы",
                     "/world_news", "/новости_мира", "/rival_threads", "/чужие_нити", "/guardian_corrections",
                     "/locations", "/локации", "/transport", "/effects", "/combat", "/weather", "/books",
                     "/storage_access", "/interactions"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
        }
    }

    [Fact]
    public void LifecycleAndLocalTurnCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/validate", "/валидация", "/world_setup", "/настройка_мира",
                     "/distribute", "/распределить", "/companion_directive", "/директива_компаньону",
                     "/faction_directive", "/директива_фракции", "/craft", "/ремесло",
                     "/abode_offering", "/подношение_обители", "/found_guardian_mantle", "/учредить_хранителя",
                     "/spiritual_action", "/духовное_действие"
                 })
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
    }

    [Fact]
    public void ChaosSeaReadOnlyCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/chaos_sea", "/море_хаоса", "/guardians", "/хранители", "/abode_power", "/сила_обители",
                     "/guardian_projects", "/проекты_хранителей", "/abodes", "/обители", "/gacha", "/гача"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
        }
    }

    [Fact]
    public void ShiningAbodeCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/shining_abode", "/сияющая_обитель", "/shining_politics", "/сияющая_политика",
                     "/shining_treasury", "/казначейство", "/source_of_light", "/источник_света"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
        }
    }

    [Fact]
    public void AfterlifeCombatAndEntityReadOnlyCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/afterlife_profiles", "/профили_загробья", "/afterlife_inbox", "/уведомления_загробья",
                     "/spiritual_conflict", "/духовный_конфликт", "/spiritual_combat_log", "/журнал_духовного_боя",
                     "/spiritual_combat_help", "/духовный_бой", "/spiritual_arts", "/духовные_искусства"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries[command].Status);
        }
    }

    public void Dispose()
    {
        if (Directory.Exists(_rootPath))
            Directory.Delete(_rootPath, recursive: true);
    }

    private IReadOnlyCollection<string> ReadRegisteredCommandNames()
    {
        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();

        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var explorer = new ExplorerMode(
            stateManager,
            fs,
            new LocalizationManager { CurrentLanguage = "ru" },
            console: new TestExplorerConsole());

        var field = typeof(ExplorerMode).GetField("_allCommandNames", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);

        var commands = Assert.IsAssignableFrom<IReadOnlyCollection<string>>(field.GetValue(explorer));
        return commands;
    }

    private static bool HasWebDtoBuilder(string command) =>
        string.Equals(command, "/help", StringComparison.OrdinalIgnoreCase) ||
        string.Equals(command, "/помощь", StringComparison.OrdinalIgnoreCase) ||
        ExplorerMathCommandResultBuilder.CanBuild(command) ||
        ExplorerUniversalMetaCommandResultBuilder.CanBuild(command) ||
        ExplorerMortalWorldCommandResultBuilder.CanBuild(command) ||
        ExplorerChaosSeaCommandResultBuilder.CanBuild(command) ||
        ExplorerShiningAbodeCommandResultBuilder.CanBuild(command) ||
        ExplorerAfterlifeCombatCommandResultBuilder.CanBuild(command) ||
        ExplorerLifecycleLocalTurnCommandResultBuilder.CanBuild(command);
}
