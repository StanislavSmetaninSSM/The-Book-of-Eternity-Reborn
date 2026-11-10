using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text.Json;
using BookOfEternityClient.Core;
using BookOfEternityClient.IO;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("protected-v2")]
    [InlineData("generation")]
    [InlineData("namespace")]
    [InlineData("commit-only")]
    [InlineData("second-disappearance")]
    [InlineData("leaf-directory")]
    [InlineData("root-file")]
    public async Task BrowserOriginalAdmission_JournalReselectionRetainsStrictRefusals(string damage)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var originalBefore = OriginalAdmissionTree();
        var request = _fs.ResolvePath("input/turn_request.json");
        var requestBefore = File.ReadAllBytes(request);
        var journalRoot = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1");
        var intent = Path.Combine(journalRoot, "intent.tmp");
        var active = Path.Combine(journalRoot, "active.json");
        var commit = Path.Combine(journalRoot, "commit.tmp");
        byte[]? journalBytes = null;
        var scope = new TrustedLocalFileScope([_fs.BasePath]);
        await using (var lease = await _fs.AcquireCanonicalWriteLeaseAsync())
        {
            var publication = new TrustedLocalFilePublication(_fs, scope);
            void Capture(TrustedLocalPublicationPhase phase, int _)
            {
                if (phase != TrustedLocalPublicationPhase.IntentStaged) return;
                journalBytes = File.ReadAllBytes(intent);
                if (damage != "protected-v2") throw new C5RecoveryInterruption();
            }
            var generation = TrustedLocalGeneration.Existing(staged.Binding.Generation);
            var outcome = damage == "protected-v2"
                ? publication.PublishImagesWithOutcome(lease, generation,
                    [new(request,
                        TrustedLocalFileImage.CaptureFile(scope, request),
                        TrustedLocalFileImage.FromBytes([1, 2, 3]))], Capture)
                : publication.PublishWithOutcome(lease, generation,
                    [new(_fs.ResolvePath("game_state/control/c5_recovery_probe.json"), null, [1, 2, 3])], Capture);
            if (damage == "protected-v2")
            {
                Assert.True(outcome.Disposition == TrustedLocalPublicationDisposition.Committed && outcome.Failure == null,
                    "Completed v2 fixture publication failed: " + outcome);
                Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(request));
                File.WriteAllBytes(request, requestBefore);
            }
            else
            {
                Assert.Equal(TrustedLocalPublicationDisposition.RolledBack, outcome.Disposition);
                Assert.IsType<C5RecoveryInterruption>(outcome.Failure);
            }
            AssertOriginalAdmissionTree(originalBefore);
        }
        Assert.NotNull(journalBytes);
        Assert.Empty(Directory.EnumerateFileSystemEntries(journalRoot));
        // These are owned negative fixtures made from a real encoded journal,
        // not evidence about the historical publisher or a new causal race.
        if (damage == "generation")
        {
            var json = System.Text.Json.Nodes.JsonNode.Parse(journalBytes!)!;
            var other = Guid.NewGuid().ToString("N");
            json["GenerationBefore"]!["Id"] = other;
            json["GenerationAfter"]!["Id"] = other;
            journalBytes = System.Text.Encoding.UTF8.GetBytes(json.ToJsonString());
        }
        File.WriteAllBytes(intent, journalBytes!);
        var selections = 0;
        var physicalLocks = 0;
        var recoveries = 0;
        Dictionary<string, byte[]>? retained = null;
        var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance,
            PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
            {
                BrowserRecoveryJournalSelectedObserver = path =>
                {
                    selections++;
                    Assert.InRange(selections, 1, 2);
                    Assert.Equal(selections == 1 ? intent : active, path);
                    if (selections == 2)
                    {
                        if (damage == "second-disappearance") File.Delete(active);
                    }
                    else if (damage == "root-file")
                    {
                        File.Delete(intent);
                        Directory.Delete(journalRoot);
                        File.WriteAllText(journalRoot, "An owned wrong-type journal root.");
                    }
                    else
                    {
                        File.Move(intent, damage == "commit-only" ? commit : active);
                        if (damage == "namespace") File.WriteAllBytes(active, "BOELP3\r\n"u8.ToArray());
                        if (damage == "leaf-directory")
                        {
                            File.Delete(active);
                            Directory.CreateDirectory(active);
                        }
                    }
                    retained = OriginalAdmissionTree();
                },
                AfterCanonicalWriteLockOpenedAsync = () => { physicalLocks++; return Task.CompletedTask; },
                LocalPublicationRecoveryObserver = (_, _) => recoveries++
            });
        var failure = await Record.ExceptionAsync(async () => { await using var lease = await cold.AcquireCanonicalWriteLeaseAsync(); });
        if (damage == "second-disappearance")
        {
            Assert.Equal(active, Assert.IsType<FileNotFoundException>(failure).FileName);
            Assert.Equal(2, selections);
        }
        else
        {
            Assert.IsType<InvalidDataException>(failure);
            var reason = damage switch
            {
                "protected-v2" => "Uncommitted publication would alter original browser evidence",
                "generation" => "Original browser publication belongs to a different generation",
                "namespace" => "Original browser session has pending namespace publication",
                "commit-only" => "Original browser publication has unresolved commit evidence",
                "leaf-directory" => "The local file path is not a regular file",
                "root-file" => "A local storage ancestor is missing, linked or not a directory",
                _ => throw new InvalidOperationException("Unknown journal refusal fixture.")
            };
            Assert.Contains(reason, failure!.Message, StringComparison.Ordinal);
            Assert.Equal(damage is "protected-v2" or "generation" or "namespace" ? 2 : 1, selections);
        }
        Assert.Equal(0, physicalLocks);
        Assert.Equal(0, recoveries);
        Assert.NotNull(retained);
        AssertOriginalAdmissionTree(retained!);
        if (damage == "leaf-directory") Assert.True(Directory.Exists(active));
        if (damage == "root-file") Assert.True(File.Exists(journalRoot));
        _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { damage, selections, physicalLocks, recoveries,
            failure = failure!.GetType().FullName, failure.Message, retained = retained!.ToDictionary(pair => pair.Key,
                pair => Convert.ToHexString(SHA256.HashData(pair.Value))) }));
    }

    [Theory]
    [InlineData("intent-rename")]
    [InlineData("committed-cleanup")]
    public async Task BrowserOriginalAdmission_SelectedJournalSurvivesOwnedPublicationTransition(string transition)
    {
        var (_, staged) = await PrepareBrowserInputStagingAsync(withRollback: true);
        var before = OriginalAdmissionTree();
        var evidence = Path.Combine(TestRepoPaths.RepoRoot, "TestResults/journal-selection-race", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(evidence);
        var rows = new ConcurrentQueue<object>();
        var paused = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var advance = new ManualResetEventSlim();
        using var transitioned = new ManualResetEventSlim();
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(30));
        var journalRoot = Path.Combine(_fs.RuntimeRootPath, "trusted-local-publication-v1");
        var intent = Path.Combine(journalRoot, "intent.tmp");
        var active = Path.Combine(journalRoot, "active.json");
        var probe = _fs.ResolvePath("game_state/control/c5_recovery_probe.json");
        var pausePhase = transition == "intent-rename" ? TrustedLocalPublicationPhase.IntentStaged : TrustedLocalPublicationPhase.Committed;
        var witnessPhase = transition == "intent-rename" ? TrustedLocalPublicationPhase.IntentPublished : TrustedLocalPublicationPhase.CleanupComplete;
        FileSystemManager.CanonicalWriteLease? publisherLease = null;
        TrustedLocalPublicationResult? publication = null;
        string? selectedPath = null;
        string? selectedTransaction = null;
        var selectedCount = 0;
        var joined = false;
        Exception? primary = null;
        Exception? joinFailure = null;
        Exception? cancellationFailure = null;
        var freshPhysicalProof = false;

        void Record(string kind, object detail) => rows.Enqueue(new { kind, ticks = Stopwatch.GetTimestamp(), frequency = Stopwatch.Frequency, detail });
        object JournalImage(string path)
        {
            var bytes = File.ReadAllBytes(path);
            Assert.InRange(bytes.Length, 1, 65536);
            using var json = JsonDocument.Parse(bytes);
            return new { path = Path.GetRelativePath(_fs.BasePath, path), transactionId = json.RootElement.GetProperty("TransactionId").GetString(),
                sha256 = Convert.ToHexString(SHA256.HashData(bytes)), bytes = Convert.ToBase64String(bytes) };
        }
        void SaveTree(string name, Dictionary<string, byte[]> tree)
        {
            foreach (var pair in tree)
            {
                var path = Path.Combine(evidence, name, pair.Key);
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                File.WriteAllBytes(path, pair.Value);
            }
        }
        SaveTree("before", before);

        // The publisher obtains its own real lease in this independent task.
        // The browser participant has no ambient publisher lease to borrow.
        var publisher = Task.Run(async () =>
        {
            publisherLease = await _fs.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
            try
            {
                _fs.EnsureCanonicalWriteLeaseActive(publisherLease);
                Record("publisher-lease", new { publisherLease.IsActive, generation = _fs.ReadExistingSessionGeneration(publisherLease) });
                var result = new TrustedLocalFilePublication(_fs, new TrustedLocalFileScope([_fs.BasePath])).Publish(
                    publisherLease, TrustedLocalGeneration.Existing(staged.Binding.Generation), [new(probe, null, [1, 2, 3])],
                    (phase, _) =>
                    {
                        if (phase == pausePhase)
                        {
                            _fs.EnsureCanonicalWriteLeaseActive(publisherLease);
                            Record("publisher-paused", new { phase, publisherLease.IsActive, image = JournalImage(transition == "intent-rename" ? intent : active) });
                            paused.TrySetResult();
                            Assert.True(advance.Wait(TimeSpan.FromSeconds(10)), "Publisher release latch timed out; causal observation incomplete.");
                        }
                        if (phase == witnessPhase)
                        {
                            _fs.EnsureCanonicalWriteLeaseActive(publisherLease);
                            Assert.False(File.Exists(intent));
                            if (transition == "intent-rename")
                            {
                                Assert.True(File.Exists(active));
                                Record("actual-intent-moved", new { phase, publisherLease.IsActive, intentAbsent = true, active = JournalImage(active) });
                            }
                            else
                            {
                                Assert.False(File.Exists(active));
                                Record("actual-committed-cleanup", new { phase, publisherLease.IsActive, intentAbsent = true, activeAbsent = true });
                            }
                            transitioned.Set();
                        }
                    });
                Record("publisher-committed", new { result.TransactionId, result.Generation });
                return result;
            }
            finally
            {
                await publisherLease.DisposeAsync();
                Record("publisher-lease-disposed", new { publisherLease.IsActive });
            }
        });

        try
        {
            var cold = new FileSystemManager(_fs.BasePath, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BrowserRecoveryJournalSelectedObserver = path =>
                    {
                    var callbackStarted = Stopwatch.GetTimestamp();
                    TimeSpan Remaining()
                    {
                        var remaining = TimeSpan.FromSeconds(10) - Stopwatch.GetElapsedTime(callbackStarted);
                        if (remaining <= TimeSpan.Zero)
                            throw new TimeoutException("Shared selected-path observation deadline expired; causal observation incomplete.");
                        return remaining;
                    }
                        Assert.Equal(1, Interlocked.Increment(ref selectedCount));
                        selectedPath = path;
                        Assert.Equal(transition == "intent-rename" ? intent : active, path);
                        using (var json = JsonDocument.Parse(File.ReadAllBytes(path)))
                            selectedTransaction = json.RootElement.GetProperty("TransactionId").GetString();
                        Record("browser-selected-before-fresh-validation", JournalImage(path));
                        advance.Set();
                    Assert.True(transitioned.Wait(Remaining()), "Actual publication transition was not observed; causal observation incomplete.");
                        // Observe full success and actual lease disposal, not merely
                        // a phase callback, before original preflight resumes.
                    publication = publisher.WaitAsync(Remaining()).GetAwaiter().GetResult();
                        Assert.NotNull(publisherLease);
                        Assert.False(publisherLease!.IsActive);
                        Assert.Equal(selectedTransaction, publication.TransactionId);
                        Assert.False(File.Exists(path));
                    Record("browser-resumes-after-publisher-disposal", new { selectedPath, selectedTransaction, publication.TransactionId, publisherLease.IsActive,
                        callbackSeconds = Stopwatch.GetElapsedTime(callbackStarted).TotalSeconds });
                    }
                });
            await paused.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await using var fresh = await cold.AcquireCanonicalWriteLeaseAsync(cancellationToken: deadline.Token);
            cold.EnsureCanonicalWriteLeaseActive(fresh);
            Assert.True(fresh.IsActive);
            Assert.Equal(staged.Binding.Generation, cold.ReadExistingSessionGeneration(fresh));
            Assert.Equal(staged.RequestJson, await cold.ReadFileAsync(fresh, "input/turn_request.json"));
            Assert.Equal(staged.Json, await cold.ReadFileAsync(fresh, PendingPlayerActionService.PendingPath));
            Assert.Equal(staged.ManifestJson, File.ReadAllText(_fs.ResolvePath("game_state/control/pending_turn_snapshot.json")));
            Assert.Equal(staged.AuthorityJson, File.ReadAllText(_fs.ResolvePath(PendingTurnSnapshotAuthority.AuthorityPath)));
            var after = OriginalAdmissionTree();
            foreach (var pair in before) Assert.Equal(pair.Value, after[pair.Key]);
            Assert.Equal(new byte[] { 1, 2, 3 }, File.ReadAllBytes(probe));
            freshPhysicalProof = true;
            Record("fresh-held-original-proof", new { fresh.IsActive, generation = staged.Binding.Generation });
        }
        catch (Exception failure)
        {
            primary = failure;
            throw;
        }
        finally
        {
            advance.Set();
            // A failed constructor/admission must not leave a still-waiting
            // publisher acquisition behind when the deadline source is disposed.
            try { deadline.Cancel(); }
            catch (Exception failure)
            {
                cancellationFailure = failure;
                if (primary != null) primary.Data["JournalRaceCancellationFailure"] = failure;
            }
            try
            {
                publication = await publisher.WaitAsync(TimeSpan.FromSeconds(10));
                joined = true;
            }
            catch (Exception failure)
            {
                joinFailure = failure;
                joined = publisher.IsCompleted;
                if (primary != null) primary.Data["JournalRacePublisherJoinFailure"] = failure;
            }
            var report = new
            {
                transition, selectedPath, selectedTransaction, selectedCount, publication, publisherJoined = joined,
                publisherSuccessful = publisher.IsCompletedSuccessfully,
                publisherCanceled = publisher.IsCanceled, publisherStatus = publisher.Status.ToString(),
                publisherLeaseDisposed = publisherLease != null && !publisherLease.IsActive, freshPhysicalProof,
                primary = primary == null ? null : new { type = primary.GetType().FullName, primary.Message, primary.StackTrace,
                    fileName = (primary as FileNotFoundException)?.FileName },
                joinFailure = joinFailure == null ? null : new { type = joinFailure.GetType().FullName, joinFailure.Message },
                cancellationFailure = cancellationFailure == null ? null : new { type = cancellationFailure.GetType().FullName, cancellationFailure.Message },
                rows = rows.ToArray(), limits = new { latchSeconds = 10, caseCancellationSeconds = 30 },
                attribution = "Owned deterministic interleaving only; historical actor/transaction and helper refusal relation remain unknown."
            };
            try
            {
                SaveTree("after", OriginalAdmissionTree());
                File.WriteAllText(Path.Combine(evidence, "observation.json"), JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                _directGachaOutput?.WriteLine(JsonSerializer.Serialize(report));
                _directGachaOutput?.WriteLine("Journal race evidence: " + evidence);
            }
            catch (Exception failure)
            {
                if (primary != null) primary.Data["JournalRaceEvidenceFailure"] = failure;
                else throw;
            }
            if (primary == null && joinFailure != null) ExceptionDispatchInfo.Capture(joinFailure).Throw();
            if (primary == null && cancellationFailure != null) ExceptionDispatchInfo.Capture(cancellationFailure).Throw();
        }
    }
}
