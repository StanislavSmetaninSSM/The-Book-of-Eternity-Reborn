using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class QteSceneServiceTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupUnknown_OriginalQteActionStopsNestedAndFinalContinuation(bool finalCleanup)
    {
        using var ownedFixture = new CleanupOwnedFixture(_rootPath, line => _cleanupOutput?.WriteLine(line));
        Assert.True(OperatingSystem.IsLinux());
        await SeedMortalPlayerResourceQuartetAsync();
        using var cut = new CleanupPublicationCut();
        var files = new FileSystemManager(_rootPath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, cut.Hooks);
        cut.Attach(files);
        var historyWrites = 0;
        var terminalRuntimeWrites = 0;
        byte[]? historyAtCut = null;
        byte[]? runtimeAtCut = null;
        var logger = new CleanupThrowingLogger<QteSceneService>(cut);
        var service = CreateRuntimeCapableService(files, logger: finalCleanup ? logger : null, hooks: new QteSceneServiceHooks
        {
            AfterHistoryWrittenAsync = () => { if (cut.Armed) historyWrites++; return Task.CompletedTask; },
            AfterRuntimeWrittenAsync = state => { if (cut.Armed && state.ActiveScene == null) terminalRuntimeWrites++; return Task.CompletedTask; }
        });
        await service.BeginAcceptedSceneAsync(BuildResourceTerminalOffer("fail"), currentTurnNumber: 12);
        var backupRoot = files.ResolvePath(QteNormalizerBackupDirectory) + Path.DirectorySeparatorChar;
        var history = files.ResolvePath(QteSceneService.QteHistoryPath);
        var runtime = files.ResolvePath(QteSceneService.QteRuntimePath);
        cut.Select = (path, member) => path.StartsWith(backupRoot, StringComparison.Ordinal) &&
            !member.GetProperty("After").GetProperty("Exists").GetBoolean() &&
            (finalCleanup ? terminalRuntimeWrites > 0 : terminalRuntimeWrites == 0 && historyWrites == 0);
        cut.BeforeCut = () => { historyAtCut = CleanupPublicationCut.ReadOptional(history); runtimeAtCut = File.ReadAllBytes(runtime); };
        cut.Armed = true;
        string? returnedState = null;
        var failure = await Record.ExceptionAsync(async () => returnedState = (await service.ResolveActiveActionAsync(
            "brace", "fail", currentTurnNumber: 12, allowPreexistingStateIssues: true)).State);
        _cleanupOutput?.WriteLine(JsonSerializer.Serialize(new { finalCleanup, returnedState, Failure = failure?.ToString(),
            historyWrites, terminalRuntimeWrites, DiagnosticThrows = logger.Throws, historyAtCut, runtimeAtCut,
            HistoryAfter = CleanupPublicationCut.ReadOptional(history), RuntimeAfter = CleanupPublicationCut.ReadOptional(runtime), Cut = cut.Evidence() }));
        cut.AssertReachedAndStopped();
        Assert.Same(cut.OriginalUncertainty, failure); Assert.Null(returnedState);
        Assert.Equal(finalCleanup ? 1 : 0, historyWrites); Assert.Equal(finalCleanup ? 1 : 0, terminalRuntimeWrites);
        Assert.Equal(historyAtCut, CleanupPublicationCut.ReadOptional(history)); Assert.Equal(runtimeAtCut, File.ReadAllBytes(runtime));
    }
}
