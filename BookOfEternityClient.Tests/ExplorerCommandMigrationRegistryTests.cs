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
    public void CommandDescriptorCatalog_CoversEveryRegisteredExplorerCommand()
    {
        var registeredCommands = ReadRegisteredCommandNames();
        var descriptorCommands = ExplorerCommandCatalog.AllAliases
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var missing = registeredCommands
            .Where(command => !descriptorCommands.Contains(command))
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            missing.Length == 0,
            "Missing shared Explorer command descriptors for registered commands: " + string.Join(", ", missing));
    }

    [Fact]
    public void ConsoleHandlerDictionaries_CoverEveryCommandDescriptorAlias()
    {
        var handlerCommands = ReadConsoleHandlerCommandNames();
        var descriptorCommands = ExplorerCommandCatalog.AllAliases
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Assert.Equal(
            descriptorCommands.Order(StringComparer.OrdinalIgnoreCase),
            handlerCommands.Order(StringComparer.OrdinalIgnoreCase));
    }

    [Fact]
    public void MigrationRegistryEntries_AreGeneratedFromCommandDescriptors()
    {
        var descriptorCommands = ExplorerCommandCatalog.AllAliases
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var registryCommands = ExplorerCommandMigrationRegistry.Entries
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.Equal(descriptorCommands, registryCommands);
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

        var nonParityWithoutIssue = entries
            .Where(static entry => entry.Status is not ExplorerCommandMigrationStatus.ReadOnlyParity and not ExplorerCommandMigrationStatus.MutatingParity)
            .Where(static entry => string.IsNullOrWhiteSpace(entry.FollowUpIssue) || !entry.FollowUpIssue.Contains('#', StringComparison.Ordinal))
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            nonParityWithoutIssue.Length == 0,
            "Commands without full browser parity must point to a follow-up issue: " + string.Join(", ", nonParityWithoutIssue));

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
    public void BrowserParityMatrix_UsesGranularStatuses()
    {
        var statusNames = Enum.GetNames<ExplorerCommandMigrationStatus>();

        Assert.Contains(nameof(ExplorerCommandMigrationStatus.ReadOnlyParity), statusNames);
        Assert.Contains(nameof(ExplorerCommandMigrationStatus.InteractiveFormPending), statusNames);
        Assert.Contains(nameof(ExplorerCommandMigrationStatus.MutatingParity), statusNames);
        Assert.Contains(nameof(ExplorerCommandMigrationStatus.StatusOnly), statusNames);
        Assert.Contains(nameof(ExplorerCommandMigrationStatus.ConsoleOnlyTemporarily), statusNames);
        Assert.DoesNotContain("Migrated", statusNames);
    }

    [Fact]
    public void BrowserParityMatrix_ClassifiesKnownPartialBrowserSurfaces()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries["/validate"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/world_setup"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/distribute"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/abode_offering"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/spiritual_action"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/shining_treasury"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/source_of_light"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/afterlife_inbox"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries["/spiritual_arts"].Status);
    }

    [Fact]
    public void NonParityBrowserCommands_HaveFollowUpIssueOrExplicitReason()
    {
        var incomplete = ExplorerCommandMigrationRegistry.Entries
            .Where(static entry => entry.Status is not ExplorerCommandMigrationStatus.ReadOnlyParity and not ExplorerCommandMigrationStatus.MutatingParity)
            .Where(static entry => string.IsNullOrWhiteSpace(entry.FollowUpIssue) && string.IsNullOrWhiteSpace(entry.Reason))
            .Select(static entry => $"{entry.Command} ({entry.Status})")
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            incomplete.Length == 0,
            "Commands without full browser parity must carry a follow-up issue or explicit reason: " + string.Join(", ", incomplete));
    }

    [Fact]
    public void EveryBrowserExecutableExplorerCommand_HasWebDtoBuilderCoverage()
    {
        var executableWithoutBuilder = ExplorerCommandMigrationRegistry.Entries
            .Where(static entry => ExplorerCommandMigrationRegistry.IsBrowserExecutable(entry.Status))
            .Where(static entry => !HasWebDtoBuilder(entry.Command))
            .Select(static entry => entry.Command)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        Assert.True(
            executableWithoutBuilder.Length == 0,
            "Browser-executable commands must be backed by a shared ExplorerCommandResult DTO builder: " +
            string.Join(", ", executableWithoutBuilder));
    }

    [Fact]
    [Trait("Category", "BrowserWebUiParity")]
    public void CommandCatalog_RequiresExplicitBrowserStatusForEveryDescriptor()
    {
        var source = File.ReadAllText(Path.Combine(
            TestRepoPaths.RepoRoot,
            "BookOfEternityClient",
            "CommandProtocol",
            "ExplorerCommandCatalog.cs"));

        Assert.DoesNotContain("browserStatus = ExplorerCommandMigrationStatus.ReadOnlyParity", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExplorerCommandMigrationStatus browserStatus =", source, StringComparison.Ordinal);
        Assert.DoesNotContain("ExplorerCommandMigrationStatus BrowserStatus =", source, StringComparison.Ordinal);
    }

    [Fact]
    public void HelpCommands_HaveReadOnlyParity()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries["/help"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries["/помощь"].Status);
    }

    [Fact]
    public void UniversalMetaCommands_HaveReadOnlyParity()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[] { "/status", "/статус", "/soul", "/душа", "/codex", "/кодекс", "/story", "/debug", "/галерея", "/saref", "/сареф" })
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);
    }

    [Fact]
    public void MortalReadOnlyCommands_HaveReadOnlyParity()
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
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);
        }
    }

    [Fact]
    public void LifecycleAndLocalTurnCommands_HaveAccurateBrowserParityStatus()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[] { "/validate", "/валидация" })
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);

        foreach (var command in new[]
                 {
                     "/world_setup", "/настройка_мира",
                     "/distribute", "/распределить", "/companion_directive", "/директива_компаньону",
                     "/faction_directive", "/директива_фракции", "/craft", "/ремесло",
                     "/abode_offering", "/подношение_обители", "/found_guardian_mantle", "/учредить_хранителя",
                     "/spiritual_action", "/духовное_действие"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries[command].Status);
        }
    }

    [Fact]
    public void ChaosSeaReadOnlyCommands_HaveReadOnlyParity()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/chaos_sea", "/море_хаоса", "/guardians", "/хранители", "/abode_power", "/сила_обители",
                     "/guardian_projects", "/проекты_хранителей", "/guardian_politics", "/политика_хранителей",
                     "/abodes", "/обители", "/gacha", "/гача"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);
        }
    }

    [Fact]
    public void ShiningAbodeCommands_HaveAccurateBrowserParityStatus()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[] { "/shining_abode", "/сияющая_обитель", "/shining_politics", "/сияющая_политика" })
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);

        foreach (var command in new[] { "/shining_treasury", "/казначейство", "/source_of_light", "/источник_света" })
            Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries[command].Status);
    }

    [Fact]
    public void AfterlifeCombatAndEntityCommands_HaveAccurateBrowserParityStatus()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        foreach (var command in new[]
                 {
                     "/afterlife_profiles", "/профили_загробья",
                     "/spiritual_conflict", "/духовный_конфликт", "/spiritual_combat_log", "/журнал_духовного_боя",
                     "/spiritual_combat_help", "/духовный_бой"
                 })
        {
            Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, entries[command].Status);
        }

        foreach (var command in new[] { "/afterlife_inbox", "/уведомления_загробья", "/spiritual_arts", "/духовные_искусства" })
            Assert.Equal(ExplorerCommandMigrationStatus.MutatingParity, entries[command].Status);
    }

    [Fact]
    public void AfterlifeChroniclesCommand_IsReadOnlyBrowserParity()
    {
        var descriptor = ExplorerCommandCatalog.Require("afterlife_chronicles");

        Assert.Equal(ExplorerCommandGroup.AfterlifeCombatAndEntities, descriptor.Group);
        Assert.Equal(ExplorerCommandMutationMode.ReadOnly, descriptor.MutationMode);
        Assert.Equal(ExplorerCommandBrowserHandlerKind.AfterlifeCombat, descriptor.BrowserHandlerKind);
        Assert.Equal(ExplorerCommandMigrationStatus.ReadOnlyParity, descriptor.BrowserStatus);
        Assert.Contains("/afterlife_chronicles", descriptor.Aliases);
        Assert.Contains("/хроники_посмертия", descriptor.Aliases);
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

    private IReadOnlyCollection<string> ReadConsoleHandlerCommandNames()
    {
        var fs = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance);
        fs.EnsureDirectoryStructure();

        var stateManager = new StateManager(fs, new GameSettings(), NullLogger<StateManager>.Instance);
        var explorer = new ExplorerMode(
            stateManager,
            fs,
            new LocalizationManager { CurrentLanguage = "ru" },
            console: new TestExplorerConsole());

        var commands = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var fieldName in new[] { "_universalCommands", "_chaosSeaOnlyCommands", "_mortalOnlyCommands" })
        {
            var field = typeof(ExplorerMode).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.NotNull(field);
            var map = Assert.IsAssignableFrom<IReadOnlyDictionary<string, Func<Task>>>(field.GetValue(explorer));
            foreach (var key in map.Keys)
                commands.Add(key);
        }

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
