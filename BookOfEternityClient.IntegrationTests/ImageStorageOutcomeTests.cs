using System.Net;
using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using BookOfEternityClient.UI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Spectre.Console;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

[Collection(GameEngineTurnLifecycleCollection.CollectionName)]
public sealed class ImageStorageOutcomeTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData("cleanup_unknown")]
    [InlineData("cleanup_known_error")]
    [InlineData("cleanup_success")]
    [InlineData("export_canonical")]
    [InlineData("export_canonical_alias")]
    [InlineData("export_external")]
    [InlineData("export_external_alias")]
    [InlineData("scene_unknown")]
    [InlineData("scene_success")]
    [InlineData("entity_literal")]
    [InlineData("staged_literal")]
    [InlineData("entity_log_failure")]
    public async Task OriginalImageOperationsPreserveStorageOutcomeAndExactTargets(string mode)
    {
        Assert.True(OperatingSystem.IsLinux());
        var root = Path.Combine(Path.GetTempPath(), "boe-image-storage-" + Guid.NewGuid().ToString("N"));
        using var owned = new CleanupOwnedFixture(root, output.WriteLine);
        using var probe = new WorkerStorageProbe();
        var fs = new FileSystemManager(root, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, probe.Hooks);
        fs.EnsureDirectoryStructure();
        await using (var lease = await fs.AcquireCanonicalWriteLeaseAsync()) fs.GetOrCreateSessionGeneration(lease);
        probe.Attach(fs);
        using var handler = new OfflineImageHandler();
        using var http = new HttpClient(handler);
        var logger = new ImageOutcomeLogger { ThrowAfterSave = mode == "entity_log_failure" };
        var service = new ImageService(fs, new GameSettings
        {
            GenerateSceneImages = true, GenerateImagesWithoutDisplay = true,
            ImageProvider = "pollinations", PollinationsApiKey = ""
        }, new LocalizationManager(), logger, new DesktopPathOpener(_ => throw new InvalidOperationException("No desktop request is permitted.")), http);
        var previousConsole = AnsiConsole.Console;
        using var consoleText = new StringWriter();
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings
        {
            Ansi = AnsiSupport.No, ColorSystem = ColorSystemSupport.NoColors,
            Interactive = InteractionSupport.No, Out = new AnsiConsoleOutput(consoleText)
        });
        try
        {
            var initial = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            void Seed(string relative, byte[] bytes)
            {
                var path = fs.ResolvePath(relative); Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, bytes); initial[path] = bytes;
            }
            if (mode.StartsWith("cleanup_", StringComparison.Ordinal))
            {
                Seed("images/scenes/first.png", [1, 2]);
                Seed("images/scenes/literal\\second.png", [3, 4]);
                Seed("output/third.png", [5, 6]);
            }
            if (mode == "cleanup_success")
            {
                Seed("images/npcs/keeper__img_old.png", [7, 8]);
                Seed("images/npcs/keeper__img_latest.png", [9, 10]);
                Seed("images/npcs/keep.txt", [11, 12]);
                File.SetLastWriteTimeUtc(fs.ResolvePath("images/npcs/keeper__img_old.png"), new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
                File.SetLastWriteTimeUtc(fs.ResolvePath("images/npcs/keeper__img_latest.png"), new DateTime(2026, 1, 2, 0, 0, 0, DateTimeKind.Utc));
            }
            if (mode.StartsWith("export_", StringComparison.Ordinal)) Seed("images/npcs/original.png", handler.Bytes);
            foreach (var path in Directory.EnumerateFiles(fs.GameSessionPath, "*", SearchOption.AllDirectories))
                if (!path.EndsWith(".lock", StringComparison.Ordinal)) probe.Committed[path] = File.ReadAllBytes(path);
            var committed = new List<string>();
            var observeCommitted = probe.Cut.ObserveBeforeCut;
            probe.Cut.ObserveBeforeCut = (phase, index) =>
            {
                observeCommitted!(phase, index);
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                using var journal = CleanupPublicationCut.Metadata(File.ReadAllBytes(probe.Cut.JournalPath));
                committed.AddRange(journal.RootElement.GetProperty("Members").EnumerateArray().Select(m => m.GetProperty("Path").GetString()!));
            };
            var deletionPublications = 0;
            var mutationAttempts = 0;
            probe.BeforeMutation = path =>
            {
                if (mode == "cleanup_known_error" && path.EndsWith(".png", StringComparison.Ordinal) && ++mutationAttempts == 2)
                    throw new InvalidOperationException("Known pre-publication image deletion refusal.");
                return Task.CompletedTask;
            };
            probe.Cut.Select = (path, member) =>
            {
                var selected = mode == "scene_unknown" && path.EndsWith(".png", StringComparison.Ordinal) ||
                    mode == "cleanup_unknown" && !member.GetProperty("After").GetProperty("Exists").GetBoolean() && ++deletionPublications == 2;
                if (selected) probe.Target = path;
                return selected;
            };
            probe.Cut.Armed = true;
            ImageService.ImageCleanupResult? cleanup = null;
            ImageExportResult? export = null;
            StagedEntityImage? staged = null;
            bool? generated = null;
            string? destination = null;
            string? destinationParent = null;
            var failure = await Record.ExceptionAsync(async () =>
            {
                if (mode.StartsWith("cleanup_", StringComparison.Ordinal)) cleanup = service.CleanupExtraImages();
                else if (mode.StartsWith("export_", StringComparison.Ordinal))
                {
                    var canonical = mode.Contains("canonical", StringComparison.Ordinal);
                    var targetRoot = canonical ? fs.GameSessionPath : Path.Combine(root, "exports");
                    if (mode.EndsWith("alias", StringComparison.Ordinal))
                    {
                        Directory.CreateDirectory(targetRoot);
                        var alias = Path.Combine(root, "export-alias");
                        Directory.CreateSymbolicLink(alias, targetRoot); targetRoot = alias;
                    }
                    // GetFullPath must classify the normalized destination before any mkdir/copy.
                    destination = Path.Combine(targetRoot, "unused", "..", "new-export", "copy.png");
                    destinationParent = Path.GetDirectoryName(Path.GetFullPath(destination));
                    export = service.ExportEntityImage("npc", "original", destination);
                }
                else if (mode.StartsWith("scene_", StringComparison.Ordinal))
                    await service.ProcessSceneImagePrompt("A quiet synthetic scene.");
                else if (mode == "staged_literal")
                    staged = await service.StageEntityImageAsync("A synthetic item.", "npc", "literal\\name");
                else
                    generated = await service.GenerateEntityImageAsync("A synthetic item.", "npc",
                        mode == "entity_literal" ? "literal\\name" : "ordinary", displayAfterGenerate: false);
            });
            var afterImages = probe.PriorAtCut?.ToDictionary(pair => pair.Key,
                pair => CleanupPublicationCut.ReadOptional(pair.Key), StringComparer.Ordinal);
            var images = Directory.EnumerateFiles(fs.GameSessionPath, "*.png", SearchOption.AllDirectories)
                .ToDictionary(path => Path.GetRelativePath(fs.GameSessionPath, path), File.ReadAllBytes, StringComparer.Ordinal);
            output.WriteLine(JsonSerializer.Serialize(new
            {
                mode, failure = failure?.ToString(), SameOriginalUncertainty = probe.Cut.OriginalUncertainty != null && ReferenceEquals(probe.Cut.OriginalUncertainty, failure),
                cleanup, export, generated, staged, destination, destinationParent,
                destinationParentExists = destinationParent != null && Directory.Exists(destinationParent),
                destinationBytes = destination == null ? null : CleanupPublicationCut.ReadOptional(Path.GetFullPath(destination)),
                initial, images, committed, mutationAttempts, handler.Calls, logger.SaveLogs, logger.WarningLogs,
                console = consoleText.ToString(), probe.PriorAtCut, afterImages, Cut = probe.Cut.Evidence()
            }));
            if (mode is "scene_unknown" or "cleanup_unknown")
            {
                probe.Cut.AssertReachedAndStopped(); Assert.Same(probe.Cut.OriginalUncertainty, failure);
                Assert.Null(cleanup); Assert.Null(generated);
                Assert.NotNull(probe.PriorAtCut);
                foreach (var pair in probe.PriorAtCut!) Assert.Equal(pair.Value, afterImages![pair.Key]);
                if (mode == "cleanup_unknown") Assert.Single(committed);
                else Assert.Equal(1, handler.Calls);
            }
            else
            {
                if (mode == "entity_log_failure") Assert.Equal(1, logger.SaveLogs);
                Assert.Null(failure); Assert.Equal(0, probe.Cut.Cuts); Assert.False(File.Exists(probe.Cut.JournalPath));
                if (cleanup != null)
                {
                    Assert.Equal(mode == "cleanup_known_error" ? 2 : 3, cleanup.DeletedSceneImages);
                    Assert.Equal(mode == "cleanup_success" ? 1 : 0, cleanup.DeletedEntityImages);
                    Assert.Single(images);
                    if (mode == "cleanup_success")
                    {
                        Assert.Equal("images/npcs/keeper__img_latest.png", Assert.Single(images).Key);
                        Assert.Equal(new byte[] { 9, 10 }, Assert.Single(images).Value);
                        Assert.Equal(new byte[] { 11, 12 }, File.ReadAllBytes(fs.ResolvePath("images/npcs/keep.txt")));
                    }
                    if (mode == "cleanup_known_error") Assert.Equal(3, mutationAttempts);
                }
                if (export != null)
                {
                    var canonical = mode.Contains("canonical", StringComparison.Ordinal);
                    Assert.Equal(!canonical, export.Success);
                    if (canonical)
                    {
                        Assert.Equal(ImageExportFailureReason.InvalidTarget, export.FailureReason);
                        Assert.False(Directory.Exists(destinationParent));
                        Assert.Null(CleanupPublicationCut.ReadOptional(Path.GetFullPath(destination!)));
                    }
                    else Assert.Equal(handler.Bytes, File.ReadAllBytes(Path.GetFullPath(destination!)));
                    Assert.Empty(committed); Assert.Equal(0, handler.Calls);
                }
                if (mode.StartsWith("scene_", StringComparison.Ordinal) || mode.StartsWith("entity_", StringComparison.Ordinal))
                {
                    Assert.Equal(1, handler.Calls); Assert.Equal(handler.Bytes, Assert.Single(images).Value); Assert.Single(committed);
                    if (mode.StartsWith("entity_", StringComparison.Ordinal)) Assert.True(generated);
                    if (mode == "entity_literal") Assert.Contains("literal\\name__img_", Assert.Single(images).Key, StringComparison.Ordinal);
                }
                if (mode == "staged_literal")
                {
                    Assert.NotNull(staged); Assert.Equal(handler.Bytes, staged.Content);
                    Assert.StartsWith("images/npcs/literal\\name__img_", staged.CanonicalRelativePath, StringComparison.Ordinal);
                    Assert.Empty(images); Assert.Empty(committed); Assert.Equal(1, handler.Calls);
                }
            }
        }
        finally { AnsiConsole.Console = previousConsole; }
    }

    private sealed class OfflineImageHandler : HttpMessageHandler
    {
        internal byte[] Bytes { get; } = Enumerable.Range(0, 2048).Select(i => (byte)(i % 251)).ToArray();
        internal int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(Bytes) });
        }
    }

    private sealed class ImageOutcomeLogger : ILogger<ImageService>
    {
        internal bool ThrowAfterSave { get; init; }
        internal int SaveLogs { get; private set; }
        internal int WarningLogs { get; private set; }
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? error, Func<TState, Exception?, string> formatter)
        {
            if (level == LogLevel.Warning) WarningLogs++;
            if (!formatter(state, error).Contains("Image saved through generation fence", StringComparison.Ordinal)) return;
            SaveLogs++;
            if (ThrowAfterSave) throw new InvalidOperationException("Known diagnostic failure after image publication.");
        }
    }
}
