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
                Assert.True(subject.MetaAttempts >= 2);
                Assert.Equal(subject.MetaAttempts, control.MetaAttempts);
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
        await using var fixture = await Fixture.CreateAsync();
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

    private sealed record Observation(string OrderedIssues, string[] SourceReads, int MetaAttempts, int PhysicalVerifications);

    private sealed class Fixture : IAsyncDisposable
    {
        internal const string MetaPath = "game_state/control/life_transitions.json";
        private readonly string _root = Path.Combine(Path.GetTempPath(), "boe-browser-source-reservation-" + Guid.NewGuid().ToString("N"));
        private readonly List<string> _reads = [];
        private int _physicalVerifications;
        private bool _armed;
        internal int MetaAttempts { get; private set; }
        internal Action<int>? AfterMetaRead;
        internal FileSystemManager Files { get; }
        internal GameEngineTestRuntime Runtime { get; private set; } = null!;
        private PendingPlayerActionService.Staged _staged = null!;

        private Fixture()
        {
            Files = new FileSystemManager(_root, NullLogger<FileSystemManager>.Instance,
                PhysicalLoadTransactionOperations.Instance, new FileSystemManagerHooks
                {
                    BrowserOriginalAdmissionTimingObserver = (stage, _) =>
                    { if (_armed && stage == "physical-verification") _physicalVerifications++; },
                    CanonicalReadActivityObserver = path => { if (_armed) _reads.Add(path); },
                    AfterCanonicalReadAttemptAsync = path =>
                    {
                        if (_armed && path == MetaPath)
                        {
                            MetaAttempts++;
                            AfterMetaRead?.Invoke(MetaAttempts);
                        }
                        return Task.CompletedTask;
                    }
                });
        }

        internal static async Task<Fixture> CreateAsync()
        {
            var fixture = new Fixture();
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
                if (physical != null) physical.RequireFullEndProof();
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
                MetaAttempts, _physicalVerifications);
        }

        internal void Arm() { _reads.Clear(); MetaAttempts = _physicalVerifications = 0; _armed = true; }
        internal void Disarm() { _armed = false; AfterMetaRead = null; }
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
