using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.AgentConsole;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("helper_restore")]
    [InlineData("compensate_dispose")]
    [InlineData("engine_mirror")]
    public async Task TreatmentStorage_OriginalPublicationUncertaintyRetainsDecision(string mode)
    {
        using var cut = new TreatmentStorageCut(mode);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(null, hooks: cut.Hooks);
        cut.Attach(context);
        if (mode == "engine_mirror")
        {
            var soul = ParseJsonObjectBytes((await context.ReadFileBytesAsync("game_state/meta/soul_state.json"))!);
            soul["inkFeathers"] = new JsonObject { ["current"] = 17, ["total"] = 17 };
            await context.FileSystem.WriteFileAtomicAsync(context.Lease, "game_state/meta/soul_state.json", soul.ToJsonString());
            await context.FileSystem.WriteFileAtomicAsync(context.Lease, AfterlifeEntityProfileState.StatePath,
                """{"schemaVersion":1,"profiles":[{"actorType":"player_soul","actorId":"player_soul","displayName":"Held fixture","realm":"Mortal World","currencies":{"inkFeathers":99,"lightSparks":0}}]}""");
        }
        await context.ReleaseLeaseAsync();
        Exception? failure;
        Exception? disposalFailure = null;
        var disposalLeases = 0;
        var disposalRecovery = 0;
        var disposalAttempts = 0;
        bool journalBeforeDispose = false;
        bool journalAfterDisposeExact = false;
        byte[]? retainedJournal = null;
        MortalWoundTreatmentResourcePublicationTransaction? transaction = null;
        var store = new AgentConsoleStateStore();
        using var input = new AgentConsoleLiveInputSource(store, readTimeout: TimeSpan.FromSeconds(5));
        var engine = CreateGameEngine(input, configureSettings: settings =>
        { settings.GmBridgeAutoStart = false; settings.GmCliLaunchCommand = "exit 74"; }, fileSystem: context.FileSystem);
        cut.Armed = true;
        try
        {
            if (mode == "compensate_dispose")
            {
                var published = await NormalizeTreatmentStorageAsync(context);
                transaction = Assert.IsType<MortalWoundTreatmentResourcePublicationTransaction>(published.TreatmentResourcePublicationTransaction);
                cut.SettlementArmed = true;
                failure = await Record.ExceptionAsync(() => transaction.CompensateAsync(context.FileSystem));
                retainedJournal = File.ReadAllBytes(cut.JournalPath);
                journalBeforeDispose = true;
                cut.ReleaseHolder();
                var leases = cut.LeaseAttempts;
                var recoveries = cut.RecoveryEvents;
                var attempts = cut.RestoreReadAttempts;
                disposalFailure = await Record.ExceptionAsync(async () => await ((IAsyncDisposable)transaction).DisposeAsync());
                disposalLeases = cut.LeaseAttempts - leases;
                disposalRecovery = cut.RecoveryEvents - recoveries;
                disposalAttempts = cut.RestoreReadAttempts - attempts;
                journalAfterDisposeExact = File.Exists(cut.JournalPath) && retainedJournal.AsSpan().SequenceEqual(File.ReadAllBytes(cut.JournalPath));
                transaction = null; // Original Dispose has been observed; no fixture replay.
            }
            else if (mode == "helper_restore")
            {
                failure = await Record.ExceptionAsync(() => NormalizeTreatmentStorageAsync(context));
            }
            else
            {
                var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
                var snapshot = await InvokePrivateTaskResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
                failure = await Record.ExceptionAsync(async () =>
                    await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "storage-only treatment mirror", snapshot,
                        null, HeldTreatmentPipelineContext.Turn, null));
            }
            cut.Armed = false;
            retainedJournal ??= File.Exists(cut.JournalPath) ? File.ReadAllBytes(cut.JournalPath) : null;
            var memberBytes = cut.Target is null ? null : File.ReadAllBytes(cut.Target);
            var publishedTreatmentRetained = cut.PublishedTreatmentImages.All(pair =>
                File.Exists(pair.Key) && pair.Value.AsSpan().SequenceEqual(File.ReadAllBytes(pair.Key)));
            if (failure is not null) InvokePrivate(engine, "RecordGameLoopErrorObservation", failure);
            var notice = store.GetSnapshot();
            // Evidence has been captured. Explicit fixture-only repair now permits a fresh
            // recovery lease solely to inspect the original in-memory restart blocker.
            bool? restartBlocked = null;
            Exception? blockerFailure = null;
            if (mode != "engine_mirror" && cut.Target is not null && cut.PublishedBytes is not null)
            {
                if (File.Exists(cut.JournalPath)) File.WriteAllBytes(cut.Target, cut.PublishedBytes);
                blockerFailure = await Record.ExceptionAsync(async () =>
                {
                    await context.AcquireLeaseAsync();
                    AssertTreatmentPublicationRestartBlocked(context);
                    await AssertConfirmedHeldLiveRegistryProbeAsync(context);
                    restartBlocked = true;
                    await context.ReleaseLeaseAsync();
                });
            }
            var raw = new
            {
                mode, cut.BusinessCutCount, cut.PublishedTreatmentMembers, cut.Cuts,
                cut.Target, cut.TargetIndex, cut.SharingConflictObserved,
                cut.RestoreReadAttemptsAfterCut, cut.PublicationsAfterCut,
                TypedCarrier = cut.OriginalUncertainty?.GetType().FullName,
                Failure = failure?.ToString(), DisposalFailure = disposalFailure?.ToString(),
                journalBeforeDispose, journalAfterDisposeExact, disposalLeases, disposalRecovery, disposalAttempts,
                cut.MirrorDriftPrepared, cut.TreatmentResourceChanged, publishedTreatmentRetained,
                BusinessCausePreserved = cut.HasBusinessCause(failure),
                BusinessCause = cut.BusinessFailure.ToString(),
                PublishedTreatmentImages = cut.PublishedTreatmentImages.ToDictionary(p => p.Key, p => Convert.ToBase64String(p.Value)),
                JournalBytesBeforeCleanup = retainedJournal is null ? null : Convert.ToBase64String(retainedJournal),
                JournalRetainedBeforeExplicitCleanup = retainedJournal is not null, restartBlocked,
                BlockerFailure = blockerFailure?.ToString(),
                JournalHashBeforeCleanup = retainedJournal is null ? null : Convert.ToHexString(SHA256.HashData(retainedJournal)),
                MemberBytesBeforeCleanup = memberBytes is null ? null : Convert.ToBase64String(memberBytes),
                Notice = notice?.PlainText
            };
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(raw));

            // Establish the actual fault and physical evidence before checking the new propagation contract.
            Assert.Equal(1, cut.Cuts);
            Assert.Equal(0, cut.TargetIndex);
            Assert.True(cut.PublishedTreatmentMembers > 0);
            Assert.NotNull(cut.OriginalUncertainty);
            Assert.NotNull(retainedJournal);
            if (mode == "helper_restore") { Assert.Equal(1, cut.BusinessCutCount); Assert.True(cut.HasBusinessCause(failure)); }
            if (mode == "compensate_dispose") Assert.True(cut.SharingConflictObserved);
            else Assert.Equal(TreatmentStorageCut.ForeignBytes, memberBytes);
            if (mode == "engine_mirror")
            {
                Assert.True(cut.MirrorDriftPrepared);
                Assert.Equal(4, cut.PublishedTreatmentImages.Count);
                Assert.True(cut.TreatmentResourceChanged);
                Assert.True(publishedTreatmentRetained);
            }
            Assert.NotNull(failure);
            if (mode != "engine_mirror") { Assert.Null(blockerFailure); Assert.True(restartBlocked); }
            if (mode == "compensate_dispose")
            {
                Assert.Equal(0, disposalLeases);
                Assert.Equal(0, disposalRecovery);
                Assert.Equal(0, disposalAttempts);
                Assert.Null(disposalFailure);
                Assert.True(journalBeforeDispose);
                Assert.True(journalAfterDisposeExact);
            }
            Assert.Equal(0, cut.RestoreReadAttemptsAfterCut);
            Assert.Equal(0, cut.PublicationsAfterCut);
            Assert.Same(cut.OriginalUncertainty, failure);
            Assert.Contains(CoordinatedStatePublicationUncertainException.PlayerMessage, notice!.PlainText, StringComparison.Ordinal);
            Assert.DoesNotContain("повторите", notice.PlainText, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            cut.Armed = false;
            cut.ReleaseHolder();
            // No canonical read/recovery in fixture teardown: retain evidence until owned directory disposal.
            if (transaction is not null)
                _directGachaOutput?.WriteLine("Original transaction Dispose was not reached; fixture failed before its lifecycle boundary.");
        }
    }

    private static Task<AcceptedTurnCanonicalStateRefresh.Result> NormalizeTreatmentStorageAsync(HeldTreatmentPipelineContext context) =>
        AcceptedTurnCanonicalStateRefresh.NormalizeAndValidateWithPlanAsync(context.FileSystem,
            new CanonicalStateNormalizer(context.FileSystem, NullLogger<CanonicalStateNormalizer>.Instance),
            new ValidationService(context.FileSystem, NullLogger<ValidationService>.Instance),
            new Dictionary<string, string>(StringComparer.Ordinal));

    private sealed class TreatmentStorageCut : IDisposable
    {
        internal static readonly byte[] ForeignBytes = Encoding.UTF8.GetBytes("foreign-treatment-storage-image");
        private readonly string _mode;
        private HeldTreatmentPipelineContext? _context;
        private FileStream? _holder;
        private byte[]? _initialResourceBytes;
        private readonly InvalidOperationException _forward = new("actual treatment publication cut");
        internal InvalidOperationException BusinessFailure { get; } = new("known post-publication treatment validation failure");
        private readonly EventHandler<FirstChanceExceptionEventArgs> _firstChance;
        internal TreatmentStorageCut(string mode)
        {
            _mode = mode;
            Hooks = new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = BeforeLease,
                LocalPublicationRecoveryObserver = (_, _) => { if (Armed) RecoveryEvents++; },
                AfterCanonicalReadInitialValidationAsync = Read,
                LocalPublicationObserver = Published
            };
            _firstChance = (_, e) =>
            {
                if (e.Exception is CoordinatedStatePublicationUncertainException typed && Contains(typed, _forward))
                    OriginalUncertainty ??= typed;
            };
            AppDomain.CurrentDomain.FirstChanceException += _firstChance;
        }
        internal FileSystemManagerHooks Hooks { get; }
        internal bool Armed { get; set; }
        internal bool SettlementArmed { get; set; }
        internal int BusinessCutCount { get; private set; }
        internal int PublishedTreatmentMembers { get; private set; }
        internal int Cuts { get; private set; }
        internal int TargetIndex { get; private set; } = -1;
        internal string? Target { get; private set; }
        internal byte[]? PublishedBytes { get; private set; }
        internal int LeaseAttempts { get; private set; }
        internal int RecoveryEvents { get; private set; }
        internal int RestoreReadAttempts { get; private set; }
        internal int RestoreReadAttemptsAfterCut { get; private set; }
        internal int PublicationsAfterCut { get; private set; }
        internal bool SharingConflictObserved { get; private set; }
        internal bool MirrorDriftPrepared { get; private set; }
        internal bool TreatmentResourceChanged { get; private set; }
        internal Dictionary<string, byte[]> PublishedTreatmentImages { get; } = new(StringComparer.Ordinal);
        internal CoordinatedStatePublicationUncertainException? OriginalUncertainty { get; private set; }
        internal string JournalPath => Path.Combine(_context!.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        internal void Attach(HeldTreatmentPipelineContext context)
        {
            _context = context;
            _initialResourceBytes = File.ReadAllBytes(context.FileSystem.ResolvePath(ResourceMaterializationContract.StatePath));
        }
        private Task BeforeLease()
        {
            if (!Armed) return Task.CompletedTask;
            LeaseAttempts++;
            if (_mode == "engine_mirror" && !MirrorDriftPrepared && PublishedTreatmentMembers > 0 &&
                OnStack("RefreshCanonicalStateAsync") && OnStack("RefreshGameStateAsync"))
            {
                // Controlled external mirror drift after treatment publication, before the
                // original runtime refresh takes its lease. The normalizer had repaired it.
                var path = _context!.FileSystem.ResolvePath(AfterlifeEntityProfileState.StatePath);
                var root = ParseJsonObjectBytes(File.ReadAllBytes(path));
                var profile = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]!.GetValue<string>() == "player_soul");
                profile["currencies"]!.AsObject()["inkFeathers"] = 99;
                File.WriteAllText(path, root.ToJsonString(), new UTF8Encoding(false));
                MirrorDriftPrepared = true;
            }
            return Task.CompletedTask;
        }
        private Task Read(string path)
        {
            if (!Armed) return Task.CompletedTask;
            if (OnStack("RestoreExactBeforeImagesAsync"))
            {
                RestoreReadAttempts++;
                if (Cuts > 0) RestoreReadAttemptsAfterCut++;
            }
            if (_mode == "helper_restore" && BusinessCutCount == 0 && PublishedTreatmentMembers > 0 &&
                OnStack("ValidatePublishedOutputAuthorityAsync") && OnStack("NormalizeAndValidateWithPlanAsync"))
            {
                BusinessCutCount++;
                throw BusinessFailure;
            }
            return Task.CompletedTask;
        }
        private void Published(TrustedLocalPublicationPhase phase, int index)
        {
            if (!Armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            if (OnStack("NormalizeAccumulatedStateWithTreatmentPublicationTransactionAsync")) PublishedTreatmentMembers++;
            if (Cuts > 0) { PublicationsAfterCut++; return; }
            var restoring = OnStack("RestoreExactBeforeImagesAsync");
            var select = _mode switch
            {
                "helper_restore" => BusinessCutCount == 1 && restoring && OnStack("FinishHelperFailureAsync"),
                "compensate_dispose" => SettlementArmed && restoring,
                _ => OnStack("ApplyPlayerSoulProfileClientAuthorityForRefreshAsync") && OnStack("RefreshCanonicalStateAsync")
            };
            if (!select) return;
            using var json = ReadJournalMetadata(File.ReadAllBytes(JournalPath));
            var target = json.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString()!;
            if (_mode == "engine_mirror" && !string.Equals(target, _context!.FileSystem.ResolvePath(AfterlifeEntityProfileState.StatePath), StringComparison.Ordinal)) return;
            Target = target; TargetIndex = index; PublishedBytes = File.ReadAllBytes(target); Cuts++;
            if (_mode == "engine_mirror")
            {
                foreach (var relative in new[] { ResourceMaterializationContract.StatePath,
                    ResourceMaterializationContract.HistoryPath, WoundCarrierCatalog.PlayerPath, WoundHistoryState.HistoryPath })
                {
                    var path = _context!.FileSystem.ResolvePath(relative);
                    PublishedTreatmentImages[path] = File.ReadAllBytes(path);
                }
                TreatmentResourceChanged = !_initialResourceBytes!.AsSpan().SequenceEqual(
                    PublishedTreatmentImages[_context!.FileSystem.ResolvePath(ResourceMaterializationContract.StatePath)]);
            }
            if (_mode == "compensate_dispose")
            {
                _holder = new FileStream(target, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
                try { using var conflict = new FileStream(target, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete); }
                catch (IOException) { SharingConflictObserved = true; }
                if (!SharingConflictObserved) throw new InvalidOperationException("Fixture sharing refusal is unsupported.");
            }
            else File.WriteAllBytes(target, ForeignBytes);
            throw _forward;
        }
        private static JsonDocument ReadJournalMetadata(byte[] bytes) =>
            bytes.AsSpan().StartsWith("BOELP2\r\n"u8)
                ? JsonDocument.Parse(bytes.AsMemory(16, checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8)))))
                : JsonDocument.Parse(bytes);
        internal bool HasBusinessCause(Exception? failure) => failure is not null && Contains(failure, BusinessFailure);
        private static bool Contains(Exception failure, Exception original)
        {
            var pending = new Stack<Exception>();
            var seen = new HashSet<Exception>(ReferenceEqualityComparer.Instance);
            pending.Push(failure);
            while (pending.TryPop(out var current))
            {
                if (!seen.Add(current)) continue;
                if (ReferenceEquals(current, original)) return true;
                if (current.InnerException is { } inner) pending.Push(inner);
                if (current is AggregateException aggregate)
                    foreach (var child in aggregate.InnerExceptions) pending.Push(child);
                foreach (var value in current.Data.Values)
                    if (value is Exception diagnostic) pending.Push(diagnostic);
            }
            return false;
        }
        private static bool OnStack(string marker) => new StackTrace().GetFrames().Any(frame =>
            frame.GetMethod()?.Name.Contains(marker, StringComparison.Ordinal) == true ||
            frame.GetMethod()?.DeclaringType?.FullName?.Contains(marker, StringComparison.Ordinal) == true);
        internal void ReleaseHolder() { _holder?.Dispose(); _holder = null; }
        public void Dispose() { ReleaseHolder(); AppDomain.CurrentDomain.FirstChanceException -= _firstChance; }
    }
}
