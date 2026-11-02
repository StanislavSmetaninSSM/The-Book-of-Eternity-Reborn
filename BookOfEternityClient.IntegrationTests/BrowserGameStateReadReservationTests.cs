using System.Text.Json;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Xunit.Abstractions;

namespace BookOfEternityClient.Tests;

/// <summary>Actual browser originals; the manual reservation is an independent equal-work control.</summary>
public sealed class BrowserGameStateReadReservationTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task CrossReferencesRequiresOnePhaseReadReservation(bool controlFirst) =>
        ComparePhaseAsync(GameStateValidationPhase.CrossReferences, controlFirst);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public Task MetaMiscRequiresOnePhaseReadReservation(bool controlFirst) =>
        ComparePhaseAsync(GameStateValidationPhase.MetaMiscStateFiles, controlFirst);

    private async Task ComparePhaseAsync(GameStateValidationPhase phase, bool controlFirst)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            Observation subject, control;
            if (controlFirst)
            {
                control = await fixture.ValidateAsync(manifest, phase, reserved: true);
                subject = await fixture.ValidateAsync(manifest, phase, reserved: false);
            }
            else
            {
                subject = await fixture.ValidateAsync(manifest, phase, reserved: false);
                control = await fixture.ValidateAsync(manifest, phase, reserved: true);
            }
            Assert.Equal(before, fixture.Tree());
            Assert.Equal(subject.OrderedIssues, control.OrderedIssues);
            Assert.NotEmpty(subject.SourceReads);
            Assert.Equal(subject.SourceReads, control.SourceReads);
            Assert.True(subject.PhysicalVerifications > 0);
            Assert.True(control.PhysicalVerifications > 0);
            if (phase == GameStateValidationPhase.CrossReferences)
                Assert.Contains("faction_relation_unknown_target", subject.OrderedIssues);
            else
            {
                // Both arms must execute the nested flexible/strict pair AND its fresh end comparison.
                Assert.True(subject.SourceReads.Count(path => path == Fixture.MetaPath) >= 3);
                Assert.Contains("game_state/meta/soul_state.json", subject.SourceReads);
            }
            output.WriteLine(JsonSerializer.Serialize(new { phase, controlFirst, Subject = subject, Control = control }));
            // Causal RED belongs here, after every DATA/issue/original/absence premise.
            // No universal admission count or stopwatch saving is assumed.
            Assert.True(subject.PhysicalVerifications <= control.PhysicalVerifications,
                $"Missing phase read reservation: actual physical-verification work subject={subject.PhysicalVerifications}, control={control.PhysicalVerifications} (opening/end/close included).");
        });
    }

    [Fact]
    public async Task MetaCurrentReadsetStillRefusesDriftUnderOuterReservation()
    {
        await using var fixture = await Fixture.CreateAsync(mutableMetaReadset: true);
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var path = fixture.Files.ResolvePath(Fixture.MetaPath);
            var original = File.ReadAllBytes(path);
            using var snapshot = fixture.Runtime.ValidationService.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var outer = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginWorldValidationRead());
            try
            {
                await outer.OpenAsync();
                outer.RequireCurrent();
                fixture.Arm();
                fixture.AfterMetaRead = count =>
                {
                    // Both initial reads return the old DATA; only the preserved final readset can refuse.
                    if (count == 2) File.WriteAllText(path, "{\"reason\":\"Voluntary\",\"summary\":\"changed after capture\"}");
                };
                List<ValidationIssue>? escaped = null;
                Exception? failure;
                try
                {
                    failure = await Record.ExceptionAsync(async () => escaped =
                        await fixture.Runtime.ValidationService.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles));
                }
                finally
                {
                    fixture.Disarm();
                    File.WriteAllBytes(path, original);
                }
                Assert.Null(escaped);
                var refusal = Assert.IsAssignableFrom<IOException>(failure);
                Assert.Equal("Meta shape input changed before issue publication.", refusal.Message);
                Assert.Contains("RequireUnchangedReadsAsync", refusal.StackTrace);
                Assert.True(fixture.MetaAttempts >= 2);
                // Borrowed inner failure releases only its borrow; the outer remains authentic and open.
                Assert.True(outer.Lease!.IsActive);
                outer.RequireCurrent();
                outer.RequireFullEndProof();
            }
            finally { await outer.DisposeAsync(); }
            outer.RequireOwnerAfterClose();
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Fact]
    public async Task BorrowUsesExistingScopeAndRefusesOverlapAndPrematureOuterClose()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var validator = new ValidationService(fixture.Files, NullLogger<ValidationService>.Instance);
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            // The normal inner path owns and closes its own physical reservation.
            var ownedIssues = await validator.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles);
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            var outer = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginWorldValidationRead());
            try
            {
                await outer.OpenAsync();
                var borrow = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BorrowWorldValidationRead());
                Assert.Same(outer, borrow);
                try
                {
                    Assert.Throws<InvalidDataException>(() => fixture.Files.BorrowWorldValidationRead());
                    await Assert.ThrowsAsync<InvalidOperationException>(() => outer.DisposeAsync().AsTask());
                    Assert.True(outer.Lease!.IsActive);
                    outer.RequireCurrent();
                }
                finally { borrow.ReleaseMetaBorrow(); }
                // Actual nested Meta success must borrow/release without closing the outer lease.
                var borrowedIssues = await validator.ValidateGameStateAsync(GameStateValidationPhase.MetaMiscStateFiles);
                Assert.Equal(JsonSerializer.Serialize(ownedIssues), JsonSerializer.Serialize(borrowedIssues));
                Assert.True(outer.Lease!.IsActive);
                outer.RequireCurrent();
                outer.RequireFullEndProof();
            }
            finally { await outer.DisposeAsync(); }
            outer.RequireOwnerAfterClose();
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Theory]
    [InlineData("same-root-foreign-manager")]
    [InlineData("different-live-original")]
    [InlineData("disposed-original")]
    public async Task BorrowRefusesForeignOrRetiredOriginal(string boundary)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async _ =>
        {
            var before = fixture.Tree();
            var outer = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginWorldValidationRead());
            try
            {
                await outer.OpenAsync();
                if (boundary == "same-root-foreign-manager")
                {
                    var foreign = new FileSystemManager(fixture.Files.BasePath, NullLogger<FileSystemManager>.Instance);
                    Assert.Throws<InvalidDataException>(() => foreign.BorrowWorldValidationRead());
                }
                else if (boundary == "different-live-original")
                {
                    using (fixture.BeginAnotherActualOriginal())
                        Assert.Throws<InvalidDataException>(() => fixture.Files.BorrowWorldValidationRead());
                }
                else
                {
                    fixture.Original.Dispose();
                    Assert.Null(fixture.Files.BorrowWorldValidationRead());
                    Assert.Throws<InvalidDataException>(() => outer.RequireCurrent());
                }
                if (boundary != "disposed-original")
                {
                    outer.RequireCurrent();
                    outer.RequireFullEndProof();
                }
            }
            finally { await outer.DisposeAsync(); }
            if (boundary == "disposed-original")
                Assert.Throws<InvalidDataException>(() => outer.RequireOwnerAfterClose());
            else outer.RequireOwnerAfterClose();
            Assert.True(outer.Closed);
            Assert.Equal(before, fixture.Tree());
        });
    }

    // These are primitive World-scope controls: mutation occurs by direct typed test steps,
    // never by a passive observer or by relaxing production selector eligibility.
    [Theory]
    [InlineData("trusted")]
    [InlineData("legacy")]
    public async Task WorldScopeRefusesAndRetainsLatePublicationDebt(string rootKind)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            using var snapshot = fixture.Runtime.ValidationService.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var scope = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginGameStateSourceReadReservation());
            var evidenceRoot = rootKind == "trusted"
                ? Path.Combine(fixture.Files.RuntimeRootPath, "trusted-local-publication-v1")
                : fixture.Files.PhysicalPublicationTransactionsRootPath;
            var evidence = Path.Combine(evidenceRoot, "late-owned-evidence.json");
            byte[] bytes = [0x7b, 0x7d, 0x0a];
            try
            {
                await scope.OpenAsync();
                Assert.Contains(await fixture.Runtime.ValidationService.ValidateGameStateAsync(GameStateValidationPhase.CrossReferences),
                    issue => issue.Code == "faction_relation_unknown_target");
                scope.RequireFullEndProof();
                Directory.CreateDirectory(evidenceRoot);
                File.WriteAllBytes(evidence, bytes);
                var refusal = Assert.Throws<IOException>(() => scope.RequireNoPublicationDebt());
                Assert.Equal("Publication debt appeared during game-state source validation.", refusal.Message);
                Assert.Equal(bytes, File.ReadAllBytes(evidence));
                Assert.True(scope.Lease!.IsActive);
            }
            finally { await scope.DisposeAsync(); }
            scope.RequireOwnerAfterClose();
            Assert.Equal(bytes, File.ReadAllBytes(evidence)); // Refusal/close never recover or delete evidence.
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Theory]
    [InlineData("snapshot-bytes")]
    [InlineData("staged-history")]
    public async Task WorldScopeFullEndRefusesChangedOriginalOrStagedHistory(string boundary)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var path = fixture.Files.ResolvePath(boundary == "snapshot-bytes"
                ? manifest.Files["game_state/factions/faction_core.json"] : PendingPlayerActionService.PendingPath);
            var bytes = File.ReadAllBytes(path);
            var scope = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginGameStateSourceReadReservation());
            try
            {
                await scope.OpenAsync();
                if (boundary == "snapshot-bytes") File.WriteAllText(path, "{}");
                else
                {
                    // Valid phase-proof hashing; only the genuine retained original/History identity differs.
                    var state = PendingPlayerActionService.Parse(fixture.Original.Staged!.Json, fixture.Original.Binding.Generation);
                    var proof = PendingPlayerActionService.StagingProof(fixture.Original.Staged!);
                    proof["historyBeforeJson"] = "{\"History/foreign.json\":null}";
                    File.WriteAllText(path, PendingPlayerActionService.CreatePhase(state, "staged", proof));
                }
                Assert.Throws<InvalidDataException>(() => scope.RequireFullEndProof());
            }
            finally { File.WriteAllBytes(path, bytes); await scope.DisposeAsync(); }
            scope.RequireOwnerAfterClose();
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WorldScopeRefusesRetiredOwnerBeforeEndOrAfterClose(bool retireAfterClose)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async _ =>
        {
            var before = fixture.Tree();
            var scope = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginGameStateSourceReadReservation());
            try
            {
                await scope.OpenAsync();
                if (!retireAfterClose)
                {
                    fixture.Original.Dispose();
                    Assert.Throws<InvalidDataException>(() => scope.RequireFullEndProof());
                }
                else { scope.RequireFullEndProof(); scope.RequireNoPublicationDebt(); }
            }
            finally { await scope.DisposeAsync(); }
            if (retireAfterClose) fixture.Original.Dispose();
            Assert.Throws<InvalidDataException>(() => scope.RequireOwnerAfterClose());
            Assert.True(scope.Closed);
            Assert.False(scope.Lease!.IsActive);
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Theory]
    [InlineData("write")]
    [InlineData("acquire")]
    public async Task WorldScopeRefusesCanonicalMutation(string boundary)
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async _ =>
        {
            var before = fixture.Tree();
            var scope = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginGameStateSourceReadReservation());
            try
            {
                await scope.OpenAsync();
                if (boundary == "write")
                    await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Files.WriteFileAtomicAsync(scope.Lease!, Fixture.MetaPath, "17"));
                else await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Files.AcquireCanonicalWriteLeaseAsync());
                Assert.Equal(before, fixture.Tree());
                scope.RequireFullEndProof();
                scope.RequireNoPublicationDebt();
            }
            finally { await scope.DisposeAsync(); }
            scope.RequireOwnerAfterClose();
            Assert.Equal(before, fixture.Tree());
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutomaticPhaseRefusesInstanceReentryAndOverlap(bool meta, bool overlap)
    {
        var phase = meta ? GameStateValidationPhase.MetaMiscStateFiles : GameStateValidationPhase.CrossReferences;
        await using var fixture = await Fixture.CreateAsync(admissionScheduling: true);
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var validator = fixture.Runtime.ValidationService;
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            var calls = 0;
            fixture.BeforeBorrow = async () =>
            {
                if (Interlocked.Increment(ref calls) != 1) return;
                if (overlap) { entered.TrySetResult(); await release.Task; }
                else
                {
                    await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateGameStateAsync(GameStateValidationPhase.RequiredFiles));
                    Assert.Throws<InvalidOperationException>(() => validator.UsePrevalidatedPendingTurnSnapshotScope(null));
                }
            };
            fixture.Arm();
            var admitted = validator.ValidateGameStateAsync(phase);
            List<ValidationIssue>? admittedIssues = null;
            try
            {
                if (overlap)
                {
                    await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
                    await Assert.ThrowsAsync<InvalidOperationException>(() => validator.ValidateGameStateAsync(GameStateValidationPhase.RequiredFiles));
                    Assert.Throws<InvalidOperationException>(() => validator.UsePrevalidatedPendingTurnSnapshotScope(null));
                    Assert.False(admitted.IsCompleted);
                }
            }
            finally
            {
                release.TrySetResult();
                try { admittedIssues = await admitted; }
                finally { fixture.Disarm(); }
            }
            Assert.NotNull(admittedIssues);
            Assert.True(calls > 0); // Existing actual admission scheduling hook executed.
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            Assert.Equal(before, fixture.Tree());
            Assert.NotNull(await validator.ValidateGameStateAsync(phase));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticSnapshotWithdrawalInvalidatesSourceReservation(bool meta)
    {
        var phase = meta ? GameStateValidationPhase.MetaMiscStateFiles : GameStateValidationPhase.CrossReferences;
        await using var fixture = await Fixture.CreateAsync(admissionScheduling: true);
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var validator = fixture.Runtime.ValidationService;
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var reached = 0;
            fixture.BeforeBorrow = () =>
            {
                if (++reached == 1) Assert.Throws<InvalidOperationException>(() => snapshot.Dispose());
                return Task.CompletedTask;
            };
            fixture.Arm();
            List<ValidationIssue>? escaped = null;
            Exception? failure;
            try { failure = await Record.ExceptionAsync(async () => escaped = await validator.ValidateGameStateAsync(phase)); }
            finally { fixture.Disarm(); }
            Assert.True(reached > 0);
            Assert.Null(escaped);
            var refusal = Assert.IsType<IOException>(failure);
            Assert.Equal("Original game-state source snapshot changed before issue publication.", refusal.Message);
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            Assert.Equal(before, fixture.Tree());
            // Failed withdrawal has not replaced the override; normal disposal and a new call remain possible.
            snapshot.Dispose();
            using var next = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            Assert.NotNull(await validator.ValidateGameStateAsync(phase));
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AutomaticPhaseObservesChangedCurrentSourcesOnNextCall(bool meta)
    {
        var phase = meta ? GameStateValidationPhase.MetaMiscStateFiles : GameStateValidationPhase.CrossReferences;
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async manifest =>
        {
            var validator = fixture.Runtime.ValidationService;
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var first = await validator.ValidateGameStateAsync(phase);
            var path = fixture.Files.ResolvePath(phase == GameStateValidationPhase.CrossReferences
                ? "game_state/factions/faction_core.json" : Fixture.MetaPath);
            var bytes = File.ReadAllBytes(path);
            try
            {
                File.WriteAllText(path, phase == GameStateValidationPhase.CrossReferences ? "{\"factions\":[]}" : "17");
                fixture.Arm();
                var second = await validator.ValidateGameStateAsync(phase);
                Assert.NotEmpty(fixture.ObservedReads);
                if (phase == GameStateValidationPhase.CrossReferences)
                {
                    Assert.Contains(first, issue => issue.Code == "faction_relation_unknown_target");
                    Assert.DoesNotContain(second, issue => issue.Code == "faction_relation_unknown_target");
                }
                else
                {
                    Assert.DoesNotContain(first, issue => issue.FilePath == Fixture.MetaPath && issue.Code == "strict_state_invalid_root");
                    Assert.Contains(second, issue => issue.FilePath == Fixture.MetaPath && issue.Code == "strict_state_invalid_root");
                    Assert.True(fixture.ObservedReads.Count(path => path == Fixture.MetaPath) >= 3);
                }
                Assert.Null(fixture.Files.BorrowWorldValidationRead());
            }
            finally { fixture.Disarm(); File.WriteAllBytes(path, bytes); }
        });
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task AutomaticOpeningFailureKeepsPrimaryClosesAndNextPhaseReadsFresh(bool meta, bool cancel)
    {
        var phase = meta ? GameStateValidationPhase.MetaMiscStateFiles : GameStateValidationPhase.CrossReferences;
        await using var fixture = await Fixture.CreateAsync(admissionScheduling: true);
        await fixture.WithOriginalAsync(async manifest =>
        {
            var before = fixture.Tree();
            var validator = fixture.Runtime.ValidationService;
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            Exception primary = cancel ? new OperationCanceledException("actual source opening cancelled", new CancellationToken(true))
                : new IOException("actual source opening failed");
            var reached = 0;
            fixture.BeforeBorrow = () => { reached++; return Task.FromException(primary); };
            fixture.Arm();
            List<ValidationIssue>? escaped = null;
            var failure = await Record.ExceptionAsync(async () => escaped = await validator.ValidateGameStateAsync(phase));
            fixture.Disarm();
            Assert.Equal(1, reached);
            Assert.Same(primary, failure);
            Assert.Null(escaped);
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            Assert.Equal(before, fixture.Tree());
            var path = fixture.Files.ResolvePath(phase == GameStateValidationPhase.CrossReferences
                ? "game_state/factions/faction_core.json" : Fixture.MetaPath);
            var bytes = File.ReadAllBytes(path);
            try
            {
                File.WriteAllText(path, phase == GameStateValidationPhase.CrossReferences ? "{\"factions\":[]}" : "17");
                fixture.Arm();
                var next = await validator.ValidateGameStateAsync(phase);
                Assert.NotEmpty(fixture.ObservedReads);
                if (phase == GameStateValidationPhase.CrossReferences)
                    Assert.DoesNotContain(next, issue => issue.Code == "faction_relation_unknown_target");
                else Assert.Contains(next, issue => issue.FilePath == Fixture.MetaPath && issue.Code == "strict_state_invalid_root");
            }
            finally { fixture.Disarm(); File.WriteAllBytes(path, bytes); }
        });
    }

    [Fact]
    public async Task WorldScopeCloseFailureStillReleasesItsActualReservation()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.WithOriginalAsync(async _ =>
        {
            var before = fixture.Tree();
            await using var scope = Assert.IsType<FileSystemManager.WorldValidationReadScope>(fixture.Files.BeginGameStateSourceReadReservation());
            var close = new IOException("actual source scope close failed");
            var probe = new CloseFailure(close);
            await scope.OpenAsync();
            scope.Lease!.ExternalPublicationContext = probe;
            scope.RequireFullEndProof();
            scope.RequireNoPublicationDebt();
            Assert.Same(close, await Record.ExceptionAsync(() => scope.DisposeAsync().AsTask()));
            Assert.Equal(1, probe.Calls);
            Assert.True(scope.Closed);
            Assert.False(scope.Lease.IsActive);
            Assert.Null(fixture.Files.BorrowWorldValidationRead());
            Assert.Equal(before, fixture.Tree());
        });
    }

    private sealed class CloseFailure(Exception failure) : IDisposable
    {
        internal int Calls;
        public void Dispose() { Calls++; System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw(); }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SelectorKeepsMutableSourceHookAndFreshOnlyFallback(bool freshOnly)
    {
        await using var fixture = await Fixture.CreateAsync(mutableMetaReadset: !freshOnly);
        await fixture.WithOriginalAsync(async manifest =>
        {
            using var operation = freshOnly ? fixture.Files.BeginSourceDataOperationIfNeeded() : null;
            using var fresh = freshOnly ? FileSystemManager.SuppressValidationReuse() : null;
            Assert.False(fixture.Files.CanReuseCurrentSourceData);
            Assert.Null(fixture.Files.BeginGameStateSourceReadReservation());
            using var snapshot = fixture.Runtime.ValidationService.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            Assert.Contains(await fixture.Runtime.ValidationService.ValidateGameStateAsync(GameStateValidationPhase.CrossReferences),
                issue => issue.Code == "faction_relation_unknown_target");
        });
    }

    private sealed record Observation(string OrderedIssues, string[] SourceReads, int PhysicalVerifications);

    private sealed class Fixture : IAsyncDisposable
    {
        internal const string MetaPath = "game_state/control/life_transitions.json";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-source-reservation-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> _reads = [];
        private int _physicalVerifications;
        private bool _armed;
        internal int MetaAttempts { get; private set; }
        internal Action<int>? AfterMetaRead;
        internal Func<Task>? BeforeBorrow;
        internal IReadOnlyList<string> ObservedReads => _reads;
        internal FileSystemManager Files { get; }
        internal GameEngineTestRuntime Runtime { get; private set; } = null!;
        internal FileSystemManager.BrowserOriginalOperationScope Original { get; private set; } = null!;
        private PendingPlayerActionService.Staged _staged = null!;

        private Fixture(bool mutableMetaReadset, bool admissionScheduling)
        {
            Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BrowserOriginalAdmissionTimingObserver = (stage, _) =>
                    { if (_armed && stage == "physical-verification") _physicalVerifications++; },
                    CanonicalReadActivityObserver = path => { if (_armed) _reads.Add(path); },
                    BeforeMainBorrowRetainAsync = !admissionScheduling ? null : () =>
                        _armed && BeforeBorrow != null ? BeforeBorrow() : Task.CompletedTask,
                    // A mutable callback disables production reuse eligibility. Install it ONLY for drift.
                    AfterCanonicalReadAttemptAsync = !mutableMetaReadset ? null : path =>
                    {
                        if (_armed && path == MetaPath)
                        {
                            MetaAttempts++;
                            AfterMetaRead?.Invoke(MetaAttempts);
                        }
                        return Task.CompletedTask;
                    }
                });
            Assert.Equal(!mutableMetaReadset, Files.CanReuseCurrentSourceData);
        }

        internal static async Task<Fixture> CreateAsync(bool mutableMetaReadset = false, bool admissionScheduling = false)
        {
            var fixture = new Fixture(mutableMetaReadset, admissionScheduling);
            try { await fixture.PrepareAsync(); return fixture; }
            catch { await fixture.DisposeAsync(); throw; }
        }

        private async Task PrepareAsync()
        {
            Files.EnsureDirectoryStructure();
            await Files.WriteFileAtomicAsync("game_state/meta/soul_state.json",
                "{\"soulName\":\"Проверочная душа\",\"sessionId\":\"browser-source-session\",\"currentRealm\":\"Mortal World\",\"currentIncarnation\":1}");
            await Files.WriteFileAtomicAsync("game_state/factions/faction_core.json",
                "{\"factions\":[{\"factionId\":\"faction_present\",\"name\":\"Present faction\",\"relations\":[{\"targetFactionId\":\"faction_missing\"}]}]}");
            await Files.WriteFileAtomicAsync(MetaPath, "{\"reason\":\"Voluntary\",\"summary\":\"original current shape\"}");
            var binding = await SessionOperationContext.RunParticipatingCurrentSessionAsync(Files, async () =>
            {
                await using var lease = await Files.AcquireCanonicalWriteLeaseAsync();
                var queued = PendingPlayerActionService.PrepareQueued(Files, lease,
                    "Я читаю исходное письмо.", "browser-composer", DateTime.UtcNow.ToString("O"));
                var json = queued.ToJsonString();
                await Files.WriteFileAtomicAsync(lease, PendingPlayerActionService.PendingPath, json);
                return PendingPlayerActionService.Parse(json, Files.GetOrCreateSessionGeneration(lease)).Binding;
            });
            Runtime = GameEngineTurnLifecycleTests.CreateGameEngineRuntime(Files);
            Runtime.GameLoop.SetSession("browser-source-session", 0);
            _staged = await Runtime.Engine.StageBrowserOriginalForValidationAsync(binding, new TurnRequest
            {
                SessionId = Runtime.GameLoop.SessionId, RequestId = binding.ActionId, TurnNumber = 1,
                PlayerAction = binding.Action, Timestamp = DateTime.UtcNow.ToString("O"), PreGeneratedDices1d20 = [3, 17],
                ProgressionControl = new ProgressionControl { CurrentRealm = "Mortal World" }
            }, withRollback: true);
            Assert.Equal("staged", PendingPlayerActionService.Parse(_staged.Json, binding.Generation).Phase);
        }

        internal Task WithOriginalAsync(Func<GameEngine.PendingTurnSnapshotManifest, Task> body) =>
            SessionOperationContext.RunParticipatingExpectedSessionAsync(Files, _staged.Binding.Generation, async () =>
            {
                using var original = Files.BeginBrowserOriginalOperation(_staged.Binding, _staged);
                Original = original;
                Files.BeginBrowserOriginalProcessing(_staged);
                // Produced manifest is DATA only. Both arms authenticate with the actual original/World scope.
                var manifest = JsonSerializer.Deserialize<GameEngine.PendingTurnSnapshotManifest>(
                    _staged.ManifestJson, LiveTurnPreparationService.ManifestJsonOptions)!;
                Assert.Equal(_staged.Binding.ActionId, manifest.RequestId);
                Assert.Equal(_staged.Binding.Generation, manifest.BrowserSessionGeneration);
                Assert.NotEmpty(manifest.Files);
                Assert.NotEmpty(manifest.RollbackBackups);
                Assert.Contains("game_state/factions/faction_core.json", manifest.Files.Keys);
                // Setup authenticity is proved by actual physical admission, never by the parsed override.
                var preflight = Assert.IsType<FileSystemManager.WorldValidationReadScope>(Files.BeginWorldValidationRead());
                try
                {
                    await preflight.OpenAsync();
                    Assert.True(preflight.MatchesOriginalFrame(manifest.SessionId, manifest.RequestId,
                        manifest.TurnNumber, manifest.ManifestPayloadHash));
                    preflight.RequireFullEndProof();
                }
                finally { await preflight.DisposeAsync(); }
                preflight.RequireOwnerAfterClose();
                Assert.False(File.Exists(Files.ResolvePath("game_state/control/pending_npc_trade_inventory_requests.json")));
                await body(manifest);
                return true;
            });

        internal FileSystemManager.BrowserOriginalOperationScope BeginAnotherActualOriginal()
        {
            var original = Files.BeginBrowserOriginalOperation(_staged.Binding, _staged);
            Files.BeginBrowserOriginalProcessing(_staged);
            return original;
        }

        internal async Task<Observation> ValidateAsync(GameEngine.PendingTurnSnapshotManifest manifest,
            GameStateValidationPhase phase, bool reserved)
        {
            // Independent per-arm services isolate all faction/Guardian context caches; neither arm primes the other.
            var validator = new ValidationService(Files, NullLogger<ValidationService>.Instance);
            using var snapshot = validator.UsePrevalidatedPendingTurnSnapshotScope(manifest);
            var before = Tree();
            Arm();
            List<ValidationIssue> issues;
            FileSystemManager.WorldValidationReadScope? physical = null;
            try
            {
                if (reserved)
                {
                    physical = Assert.IsType<FileSystemManager.WorldValidationReadScope>(Files.BeginWorldValidationRead());
                    await physical.OpenAsync();
                    Assert.True(physical.MatchesOriginalFrame(manifest.SessionId, manifest.RequestId,
                        manifest.TurnNumber, manifest.ManifestPayloadHash));
                }
                issues = await validator.ValidateGameStateAsync(phase);
                if (physical != null)
                {
                    physical.RequireFullEndProof();
                    physical.RequireNoPublicationDebt();
                }
            }
            finally
            {
                try
                {
                    if (physical != null)
                    {
                        await physical.DisposeAsync();
                        physical.RequireOwnerAfterClose();
                    }
                }
                finally { Disarm(); }
            }
            Assert.Equal(before, Tree());
            return new Observation(JsonSerializer.Serialize(issues), _reads.OrderBy(p => p, StringComparer.Ordinal).ToArray(),
                _physicalVerifications);
        }

        internal void Arm() { _reads.Clear(); MetaAttempts = _physicalVerifications = 0; _armed = true; }
        internal void Disarm() { _armed = false; AfterMetaRead = null; BeforeBorrow = null; }
        internal string[] Tree() => Directory.EnumerateFiles(Files.GameSessionPath, "*", SearchOption.AllDirectories)
            .OrderBy(p => p, StringComparer.Ordinal).Select(p =>
                Path.GetRelativePath(Files.GameSessionPath, p).Replace('\\', '/') + ":" + Convert.ToBase64String(File.ReadAllBytes(p))).ToArray();

        public async ValueTask DisposeAsync()
        {
            if (Runtime != null) await Runtime.AudioService.DisposeAsync();
            if (Path.GetDirectoryName(_root) != Path.GetTempPath().TrimEnd(Path.DirectorySeparatorChar) ||
                !Path.GetFileName(_root).StartsWith("boe-browser-source-reservation-", StringComparison.Ordinal))
                throw new InvalidOperationException("Cannot clean a foreign read-reservation fixture.");
            if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        }
    }
}
