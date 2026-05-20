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
    public void HelpCommands_AreMarkedAsMigrated()
    {
        var entries = ExplorerCommandMigrationRegistry.Entries
            .ToDictionary(static entry => entry.Command, StringComparer.OrdinalIgnoreCase);

        Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries["/help"].Status);
        Assert.Equal(ExplorerCommandMigrationStatus.Migrated, entries["/помощь"].Status);
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
}
