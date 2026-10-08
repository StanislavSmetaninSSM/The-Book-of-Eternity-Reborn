using System.Text;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed class PortableBackupConsumerTests : IDisposable
{
    private const string Weather = "game_state/world/weather.json";
    private const string Output = "output/narrative_response.json";
    private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-backup-consumer-" + Guid.NewGuid().ToString("N"));
    private static readonly byte[] Baseline = Encoding.UTF8.GetBytes("{\"marker\":\"baseline\"}");
    private sealed class CutFailure : Exception { }

    [Fact]
    public async Task BoundDistributorClassifiesGenerationConflictInCommittedBackupDebtBeforeCompensation()
    {
        FileSystemManager? files = null; var backups = 0; var reached = 0;
        files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            {
                if (phase != TrustedLocalPublicationPhase.Committed) return;
                var members = ReadMembers(files!);
                if (members.Length != 1 || !members[0].Contains(".backup.", StringComparison.Ordinal) || ++backups != 2) return;
                reached++; SeedGeneration(files!); throw new CutFailure();
            } });
        Seed(files, Weather); Seed(files, Output); SeedGeneration(files);
        await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
        var generation = files.GetOrCreateSessionGeneration(lease);
        Exception? directFailure = null;
        var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance);
        _ = await Record.ExceptionAsync(() => SessionOperationContext.RunBoundAsync(files, generation, lease, async () =>
        {
            directFailure = await Record.ExceptionAsync(() => distributor.DistributeAsync(lease, Response()));
            return false;
        }));
        // Inspect the direct consumer result, independently of the outer bound
        // scope's existing finalization fence (not migrated by this slice).
        Assert.Equal(1, reached);
        Assert.IsType<CoordinatedStatePublicationUncertainException>(directFailure);
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Weather)));
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Output)));
        Assert.True(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ActualDistributorStopsAtUnknownBackupOrSameLeaseCommittedDebt(bool committedDebt)
    {
        FileSystemManager? files = null; var backups = 0; var reached = 0; var rollback = 0; var laterBoundaries = 0;
        files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks
            {
                BeforeCanonicalMutationBoundaryAsync = _ => { if (reached > 0) laterBoundaries++; return Task.CompletedTask; },
                LocalPublicationObserver = (phase, _) =>
                {
                    if (phase != (committedDebt ? TrustedLocalPublicationPhase.Committed : TrustedLocalPublicationPhase.MemberPublished)) return;
                    var members = ReadMembers(files!);
                    if (members.Length != 1 || !members[0].Contains(".backup.", StringComparison.Ordinal)) return;
                    if (++backups != 2) return; // Retain the first ordinary backup as a later compensation hazard.
                    reached++; File.WriteAllBytes(members[0], [99]); throw new CutFailure();
                }
            });
        Seed(files, Weather); Seed(files, Output); SeedGeneration(files);
        var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks { BeforeFileMutationRollback = _ => rollback++ });
        await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => distributor.DistributeAsync(Response()));
        Assert.Equal(1, reached); Assert.Equal(0, laterBoundaries); Assert.Equal(0, rollback);
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Weather)));
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Output)));
        Assert.Equal(2, Directory.GetFiles(files.GameSessionPath, "*.backup.*", SearchOption.AllDirectories).Length);
        Assert.True(File.Exists(Journal(files)));
    }

    [Fact]
    public async Task ActualDistributorStopsReverseCompensationWhenRestoreIsUnresolved()
    {
        FileSystemManager? files = null; var rollbackStarted = false; var reached = 0; var rollbackPaths = new List<string>();
        var primaryFailure = new InvalidOperationException("Distinct primary distribution failure.");
        var restoreFailure = new CutFailure();
        files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, index) =>
            {
                if (!rollbackStarted || phase != TrustedLocalPublicationPhase.MemberPublished || index != 0) return;
                var members = ReadMembers(files!);
                Assert.Equal(2, members.Length); reached++; File.WriteAllBytes(members[1], [99]); throw restoreFailure;
            } });
        Seed(files, Weather); Seed(files, Output); SeedGeneration(files);
        var distributor = new StateDistributor(files, NullLogger<StateDistributor>.Instance,
            new StateDistributorHooks
            {
                AfterFileMutationAppliedAsync = _ => throw primaryFailure,
                BeforeFileMutationRollback = path => { rollbackStarted = true; rollbackPaths.Add(path); }
            });
        var failure = await Assert.ThrowsAsync<CoordinatedStatePublicationUncertainException>(() => distributor.DistributeAsync(Response()));
        Assert.Equal(1, reached); Assert.Equal(new[] { Weather }, rollbackPaths);
        Assert.Contains(restoreFailure, Assert.IsType<AggregateException>(failure.InnerException).InnerExceptions);
        Assert.Same(primaryFailure, failure.Data["StateDistributionFailure"]);
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Weather)));
        Assert.Equal(Baseline, File.ReadAllBytes(files.ResolvePath(Output)));
        Assert.Single(Directory.GetFiles(Path.GetDirectoryName(files.ResolvePath(Output))!, "*.backup.*"));
        Assert.True(File.Exists(Journal(files)));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AcceptedDistributionSurvivesCleanupDebtAndDiagnosticFailure(bool unknown)
    {
        FileSystemManager? files = null; var cleanup = false; var reached = 0;
        files = new(_root, NullLogger<FileSystemManager>.Instance, PhysicalLoadTransactionOperations.Instance,
            new FileSystemManagerHooks { LocalPublicationObserver = (phase, _) =>
            {
                if (!cleanup || phase != (unknown ? TrustedLocalPublicationPhase.MemberPublished : TrustedLocalPublicationPhase.Committed)) return;
                reached++; var path = Assert.Single(ReadMembers(files!));
                if (unknown) File.WriteAllBytes(path, [99]);
                throw new CutFailure();
            } });
        Seed(files, Weather); Seed(files, Output); SeedGeneration(files);
        var distributor = new StateDistributor(files, new FailingDistributorLogger(),
            new StateDistributorHooks { BeforeBackupCleanupAsync = () => { cleanup = true; return Task.CompletedTask; } });
        var result = await distributor.DistributeAsync(Response());
        Assert.Contains(Weather, result); Assert.True(reached > 0);
        Assert.Contains("weatherChange", File.ReadAllText(files.ResolvePath(Weather)), StringComparison.Ordinal);
        Assert.Contains("accepted narrative", File.ReadAllText(files.ResolvePath(Output)), StringComparison.Ordinal);
        Assert.True(File.Exists(Journal(files)));
    }

    internal static string[] ReadMembers(FileSystemManager files)
    {
        using var doc = JsonDocument.Parse(File.ReadAllBytes(Journal(files)));
        return doc.RootElement.GetProperty("Members").EnumerateArray().Select(x => x.GetProperty("Path").GetString()!).ToArray();
    }
    private static string Journal(FileSystemManager files) => Path.Combine(files.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
    private static void Seed(FileSystemManager files, string path)
    { Directory.CreateDirectory(Path.GetDirectoryName(files.ResolvePath(path))!); File.WriteAllBytes(files.ResolvePath(path), Baseline); }
    private static void SeedGeneration(FileSystemManager files)
    { Directory.CreateDirectory(Path.GetDirectoryName(files.SessionGenerationPath)!); File.WriteAllBytes(files.SessionGenerationPath, JsonSerializer.SerializeToUtf8Bytes(new { schemaVersion = 1, generationId = Guid.NewGuid().ToString("N") })); }
    private static GameResponse Response() => new()
    { WeatherChange = JsonSerializer.SerializeToElement(new { description = "accepted weather", tendency = "NO_CHANGE" }), Response = "accepted narrative" };
    private sealed class FailingDistributorLogger : ILogger<StateDistributor>
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel level) => true;
        public void Log<TState>(LogLevel level, EventId id, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        { throw new InvalidOperationException("Diagnostic failure must not reverse accepted distribution."); }
    }
    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
