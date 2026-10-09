using System.Reflection;
using System.Text;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Spectre.Console.Rendering;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class StoryReadExportAdmissionTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("list_unknown")]
    [InlineData("read_unknown")]
    [InlineData("list_link")]
    [InlineData("read_link")]
    [InlineData("format")]
    [InlineData("read_io")]
    [InlineData("chapter_unknown")]
    [InlineData("all_unknown")]
    [InlineData("all_debt")]
    [InlineData("chapter_displayed")]
    public async Task OriginalStoryReadsAndExportsKeepStorageAuthority(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-story-read-export-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var seedCut = new CleanupPublicationCut();
        var debtArmed = false;
        var debtCuts = 0;
        var seedFiles = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    seedCut.Hooks.LocalPublicationObserver!(phase, index);
                    if (debtArmed && phase == TrustedLocalPublicationPhase.Committed)
                    { debtCuts++; throw new InvalidOperationException("Known committed story cleanup debt."); }
                }
            });
        seedCut.Attach(seedFiles);
        seedFiles.EnsureDirectoryStructure();
        await using (var lease = await seedFiles.AcquireCanonicalWriteLeaseAsync()) seedFiles.GetOrCreateSessionGeneration(lease);
        const string relative = "stories/chaos_sea.jsonl";
        var sourcePath = seedFiles.ResolvePath(relative);
        var exportDir = seedFiles.ResolvePath("stories/export");
        var weatherPath = seedFiles.ResolvePath("game_state/world/weather.json");
        var outside = Path.Combine(root, "outside");
        var outsidePath = Path.Combine(outside, "linked.jsonl");
        string Line(int turn) => JsonSerializer.Serialize(new StoryEntry
        { Turn = turn, Timestamp = "2026-01-02T03:04:05Z", Player = "Читатель", Narrative = "Сцена " + turn });
        var sourceText = Line(1) + "\n\n" + Line(2) + "\n{broken\n" + Line(3) + "\n";
        var sourceBytes = Encoding.Unicode.GetPreamble().Concat(Encoding.Unicode.GetBytes(sourceText)).ToArray();
        if (mode.EndsWith("_link", StringComparison.Ordinal))
        {
            Directory.CreateDirectory(outside); File.WriteAllBytes(outsidePath, sourceBytes);
            File.CreateSymbolicLink(sourcePath, outsidePath);
        }
        else File.WriteAllBytes(sourcePath, sourceBytes);
        if (mode is "list_unknown" or "read_unknown")
        {
            seedCut.Select = (path, _) => path == weatherPath; seedCut.Armed = true;
            await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(
                () => seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", [7, 11, 13]));
            seedCut.AssertReachedAndStopped(); seedCut.Armed = false;
        }
        if (mode == "all_debt")
        {
            debtArmed = true;
            await seedFiles.WriteFileAtomicBytesAsync("game_state/world/weather.json", [7, 11, 13]);
            debtArmed = false;
            Assert.Equal(1, debtCuts);
            using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(seedCut.JournalPath));
            Assert.True(journal.RootElement.GetProperty("Committed").GetBoolean());
        }
        var journalBefore = CleanupPublicationCut.ReadOptional(seedCut.JournalPath);
        var weatherBefore = CleanupPublicationCut.ReadOptional(weatherPath);
        using var cut = new CleanupPublicationCut();
        var admissions = 0;
        var reads = new List<string>();
        var recovery = new List<string>();
        var recoveryBeforeSourceRead = new List<bool>();
        var ioCuts = 0;
        var committed = 0;
        byte[]? committedJournal = null;
        byte[]? committedBytes = null;
        string? committedPath = null;
        var files = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                LocalPublicationObserver = (phase, index) =>
                {
                    cut.Hooks.LocalPublicationObserver!(phase, index);
                    if (phase != TrustedLocalPublicationPhase.Committed) return;
                    committed++;
                    committedJournal = File.ReadAllBytes(cut.JournalPath);
                    using var journal = CleanupPublicationCut.Metadata(committedJournal);
                    committedPath = journal.RootElement.GetProperty("Members")[0].GetProperty("Path").GetString();
                    committedBytes = File.ReadAllBytes(committedPath!);
                },
                BeforeCanonicalWriteLockOpenAsync = () =>
                { admissions++; return cut.Hooks.BeforeCanonicalWriteLockOpenAsync!(); },
                BeforeCanonicalMutationBoundaryAsync = cut.Hooks.BeforeCanonicalMutationBoundaryAsync,
                AfterCanonicalReadInitialValidationAsync = cut.Hooks.AfterCanonicalReadInitialValidationAsync,
                SessionOperationClosingAsync = cut.Hooks.SessionOperationClosingAsync,
                LocalPublicationRecoveryObserver = (phase, index) =>
                { recovery.Add(phase.ToString()); cut.Hooks.LocalPublicationRecoveryObserver!(phase, index); },
                BeforeCanonicalReadOpenAsync = path =>
                {
                    reads.Add(path);
                    if (path == relative)
                    {
                        recoveryBeforeSourceRead.Add(!File.Exists(seedCut.JournalPath) &&
                            (mode != "all_debt" || new byte[] { 7, 11, 13 }.SequenceEqual(File.ReadAllBytes(weatherPath))));
                        if (mode == "read_io") { ioCuts++; throw new IOException("Known original story read interruption."); }
                    }
                    return Task.CompletedTask;
                }
            });
        cut.Attach(files);
        cut.Select = (path, _) => path.StartsWith(exportDir + Path.DirectorySeparatorChar, StringComparison.Ordinal);
        cut.Armed = mode is "chapter_unknown" or "all_unknown";
        var storyService = new StoryService(files, NullLogger<StoryService>.Instance);
        var console = new StoryConsole(cut, mode);
        var state = new StateManager(files, new GameSettings(), NullLogger<StateManager>.Instance);
        state.CurrentState.CurrentRealm = "Chaos Sea";
        var explorer = new ExplorerMode(state, files, new LocalizationManager(), storyService: storyService, console: console);
        var admissionsAtKey = -1;
        bool? releasedBeforeKey = null;
        console.Inner.ReadKeyCallback = () =>
        {
            admissionsAtKey = admissions;
            if (mode is not ("all_debt" or "chapter_displayed")) return;
            // Observe first; this probe must never supply missing export recovery itself.
            if (File.Exists(cut.JournalPath) || committed != 1 || committedBytes is null) return;
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var lease = files.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token).GetAwaiter().GetResult();
            lease.DisposeAsync().AsTask().GetAwaiter().GetResult(); releasedBeforeKey = true;
        };
        List<StoryFileInfo>? stories = null;
        List<StoryEntry>? entries = null;
        List<StoryEntry>? last = null;
        List<StoryEntry>? missing = null;
        string? commandResult = null;
        const string chapterName = "Глава\\[ночь]";
        var displayed = new List<StoryEntry> { new() { Turn = 91, Player = "Герой", Narrative = "Уже показанная сцена" } };
        var failure = await Record.ExceptionAsync(async () =>
        {
            if (mode.StartsWith("list_", StringComparison.Ordinal)) stories = storyService.GetAvailableStories();
            else if (mode.StartsWith("read_", StringComparison.Ordinal)) entries = await storyService.ReadStoryAsync(relative);
            else if (mode == "format")
            {
                stories = storyService.GetAvailableStories(); entries = await storyService.ReadStoryAsync(relative);
                last = await storyService.ReadStoryAsync(relative, lastN: 2);
                missing = await storyService.ReadStoryAsync("stories/missing.jsonl");
            }
            else if (mode is "chapter_unknown" or "all_unknown") commandResult = await explorer.TryProcessCommand("/story");
            else if (mode == "all_debt")
                await InvokeExport(explorer, "ExportAllStoriesToTxt", new List<StoryFileInfo>
                { new() { FileName = "chaos_sea.jsonl", RelativePath = relative, DisplayName = "Выбранная история", EntryCount = 5 } });
            else await InvokeExport(explorer, "ExportStoryToTxt", chapterName, displayed);
        });
        var exports = Directory.Exists(exportDir)
            ? Directory.GetFiles(exportDir).ToDictionary(path => path, File.ReadAllBytes) : new Dictionary<string, byte[]>();
        output.WriteLine(JsonSerializer.Serialize(new
        {
            mode, root, relative, sourcePath, sourceBytes, sourceAfter = CleanupPublicationCut.ReadOptional(sourcePath),
            outsidePath, outsideAfter = CleanupPublicationCut.ReadOptional(outsidePath),
            linkTarget = mode.EndsWith("_link", StringComparison.Ordinal) ? new FileInfo(sourcePath).LinkTarget : null,
            failure = failure?.ToString(), stories, entries, last, missing, commandResult,
            admissions, reads, recovery, recoveryBeforeSourceRead, ioCuts, committed, committedJournal, committedBytes, committedPath,
            journalBefore, journalAfter = CleanupPublicationCut.ReadOptional(cut.JournalPath), weatherBefore,
            weatherAfter = CleanupPublicationCut.ReadOptional(weatherPath), debtCuts, exports,
            admissionsAtKey, releasedBeforeKey, console.ExportChoices, console.LaterInputs,
            KeyReads = console.Inner.ReadKeyCalls, console.ActualChoices,
            ConsoleChoices = console.Inner.SelectionChoicesHistory.Select(row => row.Choices), console.Inner.MarkupLines,
            SeedCut = seedCut.Evidence(), Cut = cut.Evidence()
        }));
        if (mode is "chapter_unknown" or "all_unknown")
        {
            Assert.Equal(1, console.ExportChoices); cut.AssertReachedAndStopped();
            Assert.Same(cut.OriginalUncertainty, failure); Assert.Equal(0, console.LaterInputs); Assert.Equal(0, console.Inner.ReadKeyCalls);
            Assert.Null(commandResult); Assert.Equal(0, committed);
            Assert.DoesNotContain(console.Inner.MarkupLines, line => line.Contains("Экспортировано:", StringComparison.Ordinal));
        }
        else if (mode is "list_unknown" or "read_unknown")
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Equal(1, admissions); Assert.Empty(reads);
            Assert.Null(stories); Assert.Null(entries);
            Assert.Equal(journalBefore, File.ReadAllBytes(cut.JournalPath));
            Assert.Equal(weatherBefore, File.ReadAllBytes(weatherPath));
        }
        else if (mode.EndsWith("_link", StringComparison.Ordinal))
        {
            Assert.IsType<InvalidDataException>(failure); Assert.Null(stories); Assert.Null(entries); Assert.Empty(reads);
            Assert.Equal(outsidePath, new FileInfo(sourcePath).LinkTarget); Assert.Equal(sourceBytes, File.ReadAllBytes(outsidePath));
            Assert.Single(Directory.GetFileSystemEntries(outside));
        }
        else
        {
            Assert.Null(failure);
            if (mode == "format")
            {
                var story = Assert.Single(stories!); Assert.Equal(5, story.EntryCount); Assert.Equal(relative, story.RelativePath);
                Assert.Equal(new[] { 1, 2, 3 }, entries!.Select(entry => entry.Turn));
                Assert.Equal(3, Assert.Single(last!).Turn); Assert.Empty(missing!);
            }
            else if (mode == "read_io") { Assert.Equal(1, ioCuts); Assert.Empty(entries!); }
            else
            {
                var export = Assert.Single(exports); Assert.Equal(1, committed); Assert.Equal(committedPath, export.Key);
                Assert.Equal(committedBytes, export.Value); Assert.True(export.Value.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
                Assert.Null(CleanupPublicationCut.ReadOptional(cut.JournalPath)); Assert.True(releasedBeforeKey);
                Assert.Equal(1, admissionsAtKey); Assert.Equal(2, admissions); Assert.Equal(1, console.Inner.ReadKeyCalls);
                var text = Encoding.UTF8.GetString(export.Value);
                if (mode == "all_debt")
                {
                    Assert.Equal(new[] { relative }, reads); Assert.True(Assert.Single(recoveryBeforeSourceRead));
                    Assert.NotEmpty(recovery); Assert.Equal(new byte[] { 7, 11, 13 }, File.ReadAllBytes(weatherPath));
                    Assert.Contains("Сцена 1", text); Assert.Contains("Сцена 3", text); Assert.Contains("(3 записей)", text);
                    Assert.Matches("^Полная_История_[0-9]{8}_[0-9]{6}\\.txt$", Path.GetFileName(export.Key));
                }
                else
                {
                    Assert.Empty(reads); Assert.Contains("Уже показанная сцена", text); Assert.DoesNotContain("Сцена 3", text);
                    Assert.StartsWith(chapterName + "_", Path.GetFileName(export.Key)); Assert.EndsWith(".txt", export.Key);
                }
            }
        }
        Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePath));
    }

    private static async Task InvokeExport(ExplorerMode explorer, string name, params object[] args)
    {
        Task task;
        try { task = (Task)typeof(ExplorerMode).GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(explorer, args)!; }
        catch (TargetInvocationException failure) when (failure.InnerException is not null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure.InnerException).Throw(); throw; }
        await task;
    }

    private sealed class StoryConsole(CleanupPublicationCut cut, string mode) : IExplorerConsole
    {
        internal TestExplorerConsole Inner { get; } = new();
        internal List<string> ActualChoices { get; } = [];
        internal int ExportChoices { get; private set; }
        internal int LaterInputs { get; private set; }
        public T Prompt<T>(IPrompt<T> prompt)
        {
            if (cut.Cuts != 0) LaterInputs++;
            _ = Inner.Prompt(prompt);
            Assert.IsType<SelectionPrompt<string>>(prompt);
            var choices = Inner.SelectionChoicesHistory.Last().Choices;
            string selected;
            if (ExportChoices != 0) selected = choices.First(choice => choice.Contains("←", StringComparison.Ordinal));
            else if (mode == "all_unknown") { selected = choices.Single(choice => choice.Contains("Экспортировать всё", StringComparison.Ordinal)); ExportChoices++; }
            else if (choices.Any(choice => choice.Contains("Экспортировать главу", StringComparison.Ordinal)))
            { selected = choices.Single(choice => choice.Contains("Экспортировать главу", StringComparison.Ordinal)); ExportChoices++; }
            else selected = choices.First(choice => choice.StartsWith("📖", StringComparison.Ordinal));
            Assert.Contains(selected, choices); ActualChoices.Add(selected); return (T)(object)selected;
        }
        public ConsoleKeyInfo ReadKey() { if (cut.Cuts != 0) LaterInputs++; return Inner.ReadKey(); }
        public string? ReadLine() => throw new InvalidOperationException("Unexpected story text input.");
        public bool KeyAvailable => false;
        public void Clear() => Inner.Clear();
        public void Write(IRenderable content) => Inner.Write(content);
        public void Markup(string markup) => Inner.Markup(markup);
        public void MarkupLine(string markup) => Inner.MarkupLine(markup);
        public void WriteLine() => Inner.WriteLine();
        public string Ask(string prompt, string defaultValue = "") => throw new InvalidOperationException("Unexpected story ask.");
        public bool Confirm(string prompt, bool defaultValue = false) => throw new InvalidOperationException("Unexpected story confirmation.");
    }
}
