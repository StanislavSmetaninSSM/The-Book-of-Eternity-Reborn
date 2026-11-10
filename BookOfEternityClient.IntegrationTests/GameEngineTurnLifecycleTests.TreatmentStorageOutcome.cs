using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.ExceptionServices;
using System.Reflection;
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
    public Task TreatmentStorage_OriginalPublicationUncertaintyRetainsDecision(string mode) =>
        RunTreatmentStorageScenarioAsync(mode);

    [Fact]
    public Task TreatmentStorage_OriginalEngineMirrorUncertaintyRetainsDecision() =>
        RunTreatmentStorageScenarioAsync("engine_mirror");

    [Fact]
    public Task TreatmentStorage_OriginalPreCanonicalTerminalUncertaintyRetainsDecision() =>
        RunTreatmentStorageScenarioAsync("pre_canonical_terminal");

    private async Task RunTreatmentStorageScenarioAsync(string mode)
    {
        using var cut = new TreatmentStorageCut(mode);
        await using var context = await CreateHeldTreatmentPipelineContextAsync(null, withRollbackAuthority: mode == "pre_canonical_terminal", hooks: cut.Hooks,
            configureBeforePreparation: mode == "engine_mirror" ? cut.SeedConsistentMirrorAsync : null);
        cut.Attach(context);
        await context.ReleaseLeaseAsync();
        Exception? failure;
        Exception? disposalFailure = null;
        Exception? repeatedSettlementFailure = null;
        AcceptedTurnValidationDisposition? engineDisposition = null;
        var disposalLeases = 0;
        var disposalRecovery = 0;
        var disposalAttempts = 0;
        bool journalBeforeDispose = false;
        bool journalAfterDisposeExact = false;
        byte[]? retainedJournal = null;
        byte[]? memberBeforeDispose = null;
        byte[]? memberAfterDispose = null;
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
                memberBeforeDispose = ReadOptionalTreatmentMember(cut.Target);
                var leases = cut.LeaseAttempts;
                var recoveries = cut.RecoveryEvents;
                var attempts = cut.RestoreReadAttempts;
                // GREEN extension: explicit settlement is equally fenced before another lease.
                repeatedSettlementFailure = await Record.ExceptionAsync(() => transaction.CompensateAsync(context.FileSystem));
                disposalFailure = await Record.ExceptionAsync(async () => await ((IAsyncDisposable)transaction).DisposeAsync());
                memberAfterDispose = ReadOptionalTreatmentMember(cut.Target);
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
            else if (mode == "pre_canonical_terminal")
            {
                var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
                var rollback = await InvokePrivateTaskResultAsync(engine, "GetValidatedRollbackSnapshotAsync", manifest);
                Assert.NotNull(rollback);
                Assert.True(ReadTreatmentRollbackCount(rollback, "BackupFiles") > 0);
                Assert.True(ReadTreatmentRollbackCount(rollback, "BaselineFiles") > 0);
                cut.TerminalRollbackValidated = true;
                failure = await Record.ExceptionAsync(async () =>
                    engineDisposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                        "SettlePreCanonicalTreatmentPublicationBeforeTerminalRollbackAsync", rollback, "validation_failed"));
            }
            else
            {
                var manifest = await InvokePrivateTaskResultAsync(engine, "LoadPendingTurnSnapshotManifestAsync");
                var snapshot = await InvokePrivateTaskResultAsync(engine, "LoadValidatedPendingTurnSnapshotContextAsync", manifest, true);
                failure = await Record.ExceptionAsync(async () =>
                    engineDisposition = await InvokePrivateAsync<AcceptedTurnValidationDisposition>(engine,
                        "ValidateAcceptedTurnOutcomeWithRepairLoopAsync", "storage-only treatment mirror", snapshot,
                        null, HeldTreatmentPipelineContext.Turn, null));
            }
            cut.Armed = false;
            retainedJournal ??= File.Exists(cut.JournalPath) ? File.ReadAllBytes(cut.JournalPath) : null;
            var memberBytes = mode == "compensate_dispose" ? memberBeforeDispose : ReadOptionalTreatmentMember(cut.Target);
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
                    Assert.True(AcceptedTurnAuthorityRegistry.HasExactMortalWoundTreatmentPublicationRestartBlocker(
                        context.FileSystem, context.Lease, Assert.IsType<MortalWoundTreatmentPublicationTakeReceipt>(cut.OriginalReceipt)));
                    await AssertConfirmedHeldLiveRegistryProbeAsync(context);
                    restartBlocked = true;
                    await context.ReleaseLeaseAsync();
                });
            }
            var raw = new
            {
                mode, engineDisposition, cut.TerminalRollbackValidated, cut.BusinessCutCount, cut.PublishedTreatmentMembers, cut.Cuts,
                cut.Target, cut.TargetIndex, cut.SharingConflictObserved, cut.CutPhase, cut.CutPhaseStack,
                cut.OriginalCohortRestoredBeforeCut, cut.BusinessReadPath,
                MemberAfterDispose = memberAfterDispose is null ? null : Convert.ToBase64String(memberAfterDispose),
                OriginalReceiptCaptured = cut.OriginalReceipt is not null,
                TerminalReleaseOnly = cut.OriginalReceipt?.IsTerminalReleaseOnly,
                cut.RestoreReadAttemptsAfterCut, cut.PublicationsAfterCut,
                TypedCarrier = cut.OriginalUncertainty?.GetType().FullName,
                Failure = failure?.ToString(), DisposalFailure = disposalFailure?.ToString(),
                RepeatedSettlementSameUncertainty = ReferenceEquals(repeatedSettlementFailure, cut.OriginalUncertainty),
                cut.LeaseAttemptsAfterCut, cut.FinalizationLeasesAfterCut, cut.PendingFinalizationLease, cut.RecoveryEventsAfterCut, cut.OriginalReceiptStillOpen,
                journalBeforeDispose, journalAfterDisposeExact, disposalLeases, disposalRecovery, disposalAttempts,
                cut.MirrorBootstrapValidated, cut.InitialMirrorConsistent, cut.MirrorPublishedAuthority17, cut.MirrorDriftPrepared, cut.MirrorLeaseStack, cut.MirrorLeaseAttempt, cut.MirrorBeforeValue, cut.MirrorForeignValue, cut.TreatmentResourceChanged, publishedTreatmentRetained,
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
            if (mode == "pre_canonical_terminal")
            {
                Assert.True(cut.TerminalRollbackValidated);
                Assert.NotNull(cut.OriginalReceipt);
                Assert.True(cut.OriginalReceipt.IsTerminalReleaseOnly);
                Assert.Equal("original-terminal-quarantine", cut.CutPhase);
                Assert.Null(engineDisposition);
            }
            else Assert.True(cut.PublishedTreatmentMembers > 0);
            Assert.NotNull(cut.OriginalUncertainty);
            Assert.NotNull(retainedJournal);
            if (mode == "helper_restore") { Assert.Equal(1, cut.BusinessCutCount); Assert.True(cut.OriginalCohortRestoredBeforeCut); Assert.True(cut.HasBusinessCause(failure)); }
            if (mode == "compensate_dispose") Assert.True(cut.SharingConflictObserved);
            else Assert.Equal(TreatmentStorageCut.ForeignBytes, memberBytes);
            if (mode == "engine_mirror")
            {
                Assert.True(cut.OriginalReceiptStillOpen);
                Assert.Equal(0, cut.LeaseAttemptsAfterCut);
                Assert.True(cut.FinalizationLeasesAfterCut > 0);
                Assert.False(cut.PendingFinalizationLease);
                Assert.Equal(0, cut.RecoveryEventsAfterCut);
                Assert.True(cut.MirrorBootstrapValidated);
                Assert.True(cut.InitialMirrorConsistent);
                Assert.True(cut.MirrorPublishedAuthority17);
                Assert.True(cut.MirrorDriftPrepared);
                Assert.NotEqual(cut.MirrorBeforeValue, cut.MirrorForeignValue);
                Assert.Equal(4, cut.PublishedTreatmentImages.Count);
                Assert.True(cut.TreatmentResourceChanged);
                Assert.True(publishedTreatmentRetained);
            }
            Assert.NotNull(failure);
            if (mode != "engine_mirror") { Assert.Null(blockerFailure); Assert.True(restartBlocked); }
            if (mode == "compensate_dispose")
            {
                Assert.Same(cut.OriginalUncertainty, repeatedSettlementFailure);
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
            await context.DisposeAsync();
            var removed = !Directory.Exists(context.Root);
            _directGachaOutput?.WriteLine(JsonSerializer.Serialize(new { mode, OwnedTreatmentFixtureRemoved = removed }));
            Assert.True(removed, "The original treatment fixture directory was not removed.");
        }
    }

    private static JsonDocument ReadJournalMetadata(byte[] bytes) =>
        bytes.AsSpan().StartsWith("BOELP2\r\n"u8)
            ? JsonDocument.Parse(bytes.AsMemory(16, checked((int)BinaryPrimitives.ReadInt64LittleEndian(bytes.AsSpan(8, 8)))))
            : JsonDocument.Parse(bytes);

    private static byte[]? ReadOptionalTreatmentMember(string? path) =>
        path is not null && File.Exists(path) ? File.ReadAllBytes(path) : null;

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
        private object? _originalRegistryState;
        private MortalWoundTreatmentPublicationTakeReceipt? _mirrorReceipt;
        private string? _selectedMutationPath;
        private string? _selectedPhase;
        private string? _selectedStack;
        private Dictionary<string, byte[]?> _originalBeforeImages = new(StringComparer.Ordinal);
        private readonly InvalidOperationException _forward = new("actual treatment publication cut");
        internal InvalidOperationException BusinessFailure { get; } = new("known post-publication treatment validation failure");
        private readonly EventHandler<FirstChanceExceptionEventArgs> _firstChance;
        internal TreatmentStorageCut(string mode)
        {
            _mode = mode;
            Hooks = new FileSystemManagerHooks
            {
                BeforeCanonicalWriteLockOpenAsync = BeforeLease,
                BeforeCanonicalMutationAsync = BeforeMutation,
                LocalPublicationRecoveryObserver = (_, _) =>
                {
                    if (!Armed) return;
                    RecoveryEvents++;
                    if (Cuts != 0) RecoveryEventsAfterCut++;
                },
                SessionOperationClosingAsync = () =>
                {
                    if (Armed && Cuts != 0)
                    {
                        Assert.False(PendingFinalizationLease);
                        PendingFinalizationLease = true;
                    }
                    return Task.CompletedTask;
                },
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
        internal bool TerminalRollbackValidated { get; set; }
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
        internal bool InitialMirrorConsistent { get; private set; }
        internal bool MirrorPublishedAuthority17 { get; private set; }
        internal bool MirrorDriftPrepared { get; private set; }
        internal string? MirrorLeaseStack { get; private set; }
        internal int MirrorLeaseAttempt { get; private set; }
        internal int LeaseAttemptsAfterCut { get; private set; }
        internal int FinalizationLeasesAfterCut { get; private set; }
        internal bool PendingFinalizationLease { get; private set; }
        internal int RecoveryEventsAfterCut { get; private set; }
        internal bool OriginalReceiptStillOpen => OriginalReceipt is not null && ReferenceEquals(OriginalReceipt,
            _originalRegistryState!.GetType().GetField("_openTreatmentPublicationReceipt", BindingFlags.Instance | BindingFlags.NonPublic)!
                .GetValue(_originalRegistryState));
        internal int MirrorBeforeValue { get; private set; }
        internal int MirrorForeignValue { get; private set; }
        internal string? CutPhase { get; private set; }
        internal string? CutPhaseStack { get; private set; }
        internal bool OriginalCohortRestoredBeforeCut { get; private set; }
        internal string? BusinessReadPath { get; private set; }
        internal bool MirrorBootstrapValidated { get; private set; }
        internal bool TreatmentResourceChanged { get; private set; }
        internal Dictionary<string, byte[]> PublishedTreatmentImages { get; } = new(StringComparer.Ordinal);
        internal CoordinatedStatePublicationUncertainException? OriginalUncertainty { get; private set; }
        internal MortalWoundTreatmentPublicationTakeReceipt? OriginalReceipt { get; private set; }
        internal string JournalPath => Path.Combine(_context!.FileSystem.RuntimeRootPath, "trusted-local-publication-v1", "active.json");
        internal async Task SeedConsistentMirrorAsync(FileSystemManager files)
        {
            // Supplement the existing Mortal quartet before LiveTurnPreparation seals it.
            // The real afterlife planner owns all binding/capacity/history generation.
            const string soulPath = "game_state/meta/soul_state.json";
            const int setupTurn = 41;
            await using var lease = await files.AcquireCanonicalWriteLeaseAsync();
            var before = new Dictionary<string, string?>(StringComparer.Ordinal);
            async Task<string?> Read(string path)
            {
                if (!before.TryGetValue(path, out var value))
                    before[path] = value = await files.ReadFileAsync(lease, path);
                return value;
            }
            var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
                await Read(ResourceMaterializationContract.DefinitionsPath), allowMissingPristine: false);
            Assert.True(definitionsResult.IsValid, string.Join(Environment.NewLine, definitionsResult.Issues));
            var definitions = definitionsResult.Catalog!;
            var stateResult = ResourceStateContract.ParseCanonical(
                await Read(ResourceMaterializationContract.StatePath), definitions, allowMissingPristine: false);
            var historyResult = ResourceHistoryState.ParseCanonical(
                await Read(ResourceMaterializationContract.HistoryPath), definitions, allowMissingPristine: false);
            Assert.True(stateResult.IsValid, string.Join(Environment.NewLine, stateResult.Issues));
            Assert.True(historyResult.IsValid, string.Join(Environment.NewLine, historyResult.Issues));
            var mortalState = stateResult.Ledger!;
            var mortalHistory = historyResult.History!;
            Assert.DoesNotContain(mortalHistory.Transitions, transition => transition.Turn >= setupTurn);
            Assert.Equal(2m, Assert.Single(mortalState.Entries.Where(entry => entry.Coordinate.ResourceKey == "energy")).Current);
            var woundBefore = new Dictionary<string, byte[]>(StringComparer.Ordinal);
            foreach (var path in new[] { WoundCarrierCatalog.PlayerPath, WoundHistoryState.HistoryPath })
                woundBefore.Add(path, (await files.ReadFileBytesAsync(lease, path))!);

            var soul = JsonNode.Parse((await Read(soulPath))!)!.AsObject();
            Assert.Equal("Mortal World", soul["currentRealm"]!.GetValue<string>());
            soul["inkFeathers"] = new JsonObject { ["current"] = 17, ["total"] = 17 };
            var combatProfile = soul["afterlifeCombatProfile"] as JsonObject ?? new JsonObject();
            combatProfile["spiritFocusTier"] = 0;
            if (combatProfile.Parent is null) soul["afterlifeCombatProfile"] = combatProfile;
            var originalProfilesJson = await Read(AfterlifeEntityProfileState.StatePath);
            var originalProfiles = originalProfilesJson is null ? AfterlifeEntityProfileState.CreateDefaultRoot()
                : JsonNode.Parse(originalProfilesJson)!.AsObject();
            Assert.Empty(originalProfiles["profiles"]!.AsArray());
            var profiles = JsonNode.Parse(
                """{"schemaVersion":1,"profiles":[{"actorType":"player_soul","actorId":"player_soul","displayName":"Held fixture","realm":"Chaos Sea","currencies":{"inkFeathers":17,"lightSparks":0}}]}""")!.AsObject();
            profiles = AfterlifeEntityProfileState.ProjectCanonicalRoot(profiles, null);
            AfterlifeEntityProfileState.ApplyPlayerSoulProfileClientAuthority(profiles, soul, null);
            var pristine = ResourceBootstrapStateBuilder.BuildPristine();
            Assert.True(pristine.IsValid, string.Join(Environment.NewLine, pristine.Issues));
            var planning = AfterlifeOwnerResourceStatePlanner.Build(new AfterlifeOwnerResourceStatePlanningInput(
                setupTurn, definitions, pristine.State!, pristine.History!,
                new AfterlifeResourceOwnerRoots(AfterlifeEntityProfileState.CreateDefaultRoot(), null),
                new AfterlifeResourceOwnerRoots(profiles, null, soul)));
            Assert.True(planning.IsValid, string.Join(Environment.NewLine, planning.Issues));
            var addedState = planning.StateAfterImage!;
            var addedHistory = planning.HistoryAfterImage!;
            Assert.NotEmpty(addedState.Entries);
            Assert.All(addedState.Entries, entry =>
            {
                Assert.Equal(ResourceOwnerKind.AfterlifeActor, entry.Coordinate.OwnerKind);
                Assert.Equal("spiritual_action_points", entry.Coordinate.ResourceKey);
                Assert.Equal(ResourceLifecycleState.Suspended, entry.State);
                Assert.DoesNotContain(mortalState.Entries, original => original.Coordinate == entry.Coordinate);
            });
            Assert.All(addedHistory.Transitions, transition =>
            {
                Assert.Equal(setupTurn, transition.Turn);
                Assert.DoesNotContain(mortalHistory.Transitions, original => original.TransitionId == transition.TransitionId);
            });
            var combinedState = new ResourceStateLedger(mortalState.Entries.Concat(addedState.Entries));
            var combinedHistoryResult = ResourceHistoryState.CreateValidated(
                mortalHistory.Transitions.Concat(addedHistory.Transitions), definitions);
            Assert.True(combinedHistoryResult.IsValid, string.Join(Environment.NewLine, combinedHistoryResult.Issues));
            var combinedHistory = combinedHistoryResult.History!;
            Assert.Empty(combinedHistory.ValidateStateAgreement(combinedState));
            var projected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [soulPath] = GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soul).ToJsonString(),
                [AfterlifeEntityProfileState.StatePath] = profiles.ToJsonString()
            };
            foreach (var pair in planning.Composition!.OwnerCompanionAfterImages)
                projected[pair.Key] = pair.Value.ToJsonString();
            var generatedProfile = Assert.Single(JsonNode.Parse(projected[AfterlifeEntityProfileState.StatePath])!["profiles"]!.AsArray());
            Assert.Equal("player_soul", generatedProfile!["actorId"]!.GetValue<string>());
            Assert.Equal(17, generatedProfile["currencies"]!["inkFeathers"]!.GetValue<int>());
            Assert.Equal(17, JsonNode.Parse(projected[soulPath])!["inkFeathers"]!["current"]!.GetValue<int>());
            var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
                definitions, mortalState, mortalHistory, combinedState, combinedHistory, Read, before, projected);
            Assert.NotNull(quartet.Projection);
            Assert.Empty(quartet.Issues);
            projected[ResourceMaterializationContract.StatePath] = combinedState.ToCanonicalJson();
            projected[ResourceMaterializationContract.HistoryPath] = combinedHistory.ToCanonicalJson();
            var writes = projected.Select(pair => new CoordinatedStateWriteHelper.PlannedWrite(
                pair.Key, quartet.Projection!.BeforeImages[pair.Key], pair.Value, RequireCurrentBaseline: true)).ToList();
            CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(writes, quartet.Projection!);
            Assert.True(await CoordinatedStateWriteHelper.TryCommitAsync(files, lease, writes.ToArray()));

            var actualState = ResourceStateContract.ParseCanonical(await files.ReadFileAsync(lease,
                ResourceMaterializationContract.StatePath), definitions, allowMissingPristine: false).Ledger!;
            var actualHistory = ResourceHistoryState.ParseCanonical(await files.ReadFileAsync(lease,
                ResourceMaterializationContract.HistoryPath), definitions, allowMissingPristine: false).History!;
            Assert.Equal(mortalState.ToCanonicalJson(), new ResourceStateLedger(actualState.Entries.Where(entry =>
                !addedState.Entries.Any(added => added.Coordinate == entry.Coordinate))).ToCanonicalJson());
            Assert.Equal(mortalHistory.ToCanonicalJson(), ResourceHistoryState.CreateValidated(actualHistory.Transitions.Where(transition =>
                !addedHistory.Transitions.Any(added => added.TransitionId == transition.TransitionId)), definitions).History!.ToCanonicalJson());
            foreach (var pair in woundBefore)
                Assert.Equal(pair.Value, await files.ReadFileBytesAsync(lease, pair.Key));
            var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(definitions,
                path => files.ReadFileAsync(lease, path), actualState, actualHistory,
                CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
            Assert.True(authority.IsValid, string.Join(Environment.NewLine, authority.Issues));
            Assert.Empty(authority.Authority!.ValidateCanonicalAgreement(actualState, actualHistory));
            MirrorBootstrapValidated = true;
        }

        internal void Attach(HeldTreatmentPipelineContext context)
        {
            _context = context;
            if (_mode == "engine_mirror")
            {
                var soul = ParseJsonObjectBytes(File.ReadAllBytes(context.FileSystem.ResolvePath("game_state/meta/soul_state.json")));
                var profiles = ParseJsonObjectBytes(File.ReadAllBytes(context.FileSystem.ResolvePath(AfterlifeEntityProfileState.StatePath)));
                var profile = Assert.Single(profiles["profiles"]!.AsArray().OfType<JsonObject>().Where(p =>
                    p["actorType"]!.GetValue<string>() == "player_soul" && p["actorId"]!.GetValue<string>() == "player_soul"));
                Assert.Equal(17, soul["inkFeathers"]!["current"]!.GetValue<int>());
                Assert.Equal(17, profile["currencies"]!["inkFeathers"]!.GetValue<int>());
                InitialMirrorConsistent = true;
            }
            // Retain the real already-populated generation registry under the fixture's
            // existing lease. At the cut we observe its actual taken receipt; no token is minted.
            _originalRegistryState = typeof(AcceptedTurnAuthorityRegistry).GetMethod("GetState",
                BindingFlags.Static | BindingFlags.NonPublic)!.Invoke(null, [context.FileSystem, context.Lease]);
            _initialResourceBytes = File.ReadAllBytes(context.FileSystem.ResolvePath(ResourceMaterializationContract.StatePath));
            _originalBeforeImages = CanonicalStateNormalizer.NormalizerRollbackTrackedFiles
                .Distinct(StringComparer.OrdinalIgnoreCase).ToDictionary(
                    relative => context.FileSystem.ResolvePath(relative),
                    relative => ReadOptionalTreatmentMember(context.FileSystem.ResolvePath(relative)), StringComparer.Ordinal);
        }
        private Task BeforeLease()
        {
            if (!Armed) return Task.CompletedTask;
            LeaseAttempts++;
            if (Cuts != 0)
            {
                if (PendingFinalizationLease)
                {
                    PendingFinalizationLease = false;
                    FinalizationLeasesAfterCut++;
                }
                else LeaseAttemptsAfterCut++;
            }
            if (_mode == "engine_mirror" && !MirrorDriftPrepared && PublishedTreatmentMembers > 0 &&
                !File.Exists(JournalPath) && _originalRegistryState!.GetType()
                    .GetField("_openTreatmentPublicationReceipt", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(_originalRegistryState) is MortalWoundTreatmentPublicationTakeReceipt)
            {
                // The original normalizer owns one lease across all its publication.
                // This next acquisition, with committed members and the same open receipt,
                // is after that scope. Retain its stack as evidence, not selection authority.
                MirrorLeaseStack = new StackTrace().ToString();
                MirrorLeaseAttempt = LeaseAttempts;
                _mirrorReceipt = (MortalWoundTreatmentPublicationTakeReceipt)_originalRegistryState!.GetType()
                    .GetField("_openTreatmentPublicationReceipt", BindingFlags.Instance | BindingFlags.NonPublic)!
                    .GetValue(_originalRegistryState)!;
                var path = _context!.FileSystem.ResolvePath(AfterlifeEntityProfileState.StatePath);
                var root = ParseJsonObjectBytes(File.ReadAllBytes(path));
                var profile = root["profiles"]!.AsArray().OfType<JsonObject>().Single(p => p["actorType"]!.GetValue<string>() == "player_soul");
                MirrorBeforeValue = profile["currencies"]!["inkFeathers"]!.GetValue<int>();
                Assert.Equal(17, MirrorBeforeValue);
                MirrorForeignValue = 99;
                profile["currencies"]!.AsObject()["inkFeathers"] = MirrorForeignValue;
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
                string.Equals(path, WoundCarrierCatalog.PlayerPath, StringComparison.Ordinal) &&
                !BytesEqual(_originalBeforeImages[_context!.FileSystem.ResolvePath(path)], ReadOptionalTreatmentMember(_context.FileSystem.ResolvePath(path))))
            {
                BusinessCutCount++; BusinessReadPath = path;
                throw BusinessFailure;
            }
            return Task.CompletedTask;
        }
        private Task BeforeMutation(string relativePath)
        {
            if (!Armed || Cuts != 0) return Task.CompletedTask;
            // Generic rollback skips an already-equal image. A subsequent durable
            // command write with the COMPLETE original cohort already restored therefore
            // identifies the original transaction's forced restoration, without requiring
            // its async ancestors to remain on the physical stack.
            var durable = relativePath == AcceptedMechanicsPlan.WoundCommandPath ||
                relativePath == WoundAcceptedTurnSnapshotContract.PendingResolutionPath;
            var restored = _mode == "helper_restore" && BusinessCutCount == 1 && durable &&
                _originalBeforeImages.All(pair => BytesEqual(pair.Value, ReadOptionalTreatmentMember(pair.Key)));
            var selected = _mode switch
            {
                "helper_restore" => restored,
                "compensate_dispose" => SettlementArmed && durable,
                "pre_canonical_terminal" => TerminalRollbackValidated && relativePath == AcceptedMechanicsPlan.WoundCommandPath &&
                    BytesEqual(_originalBeforeImages[_context!.FileSystem.ResolvePath(relativePath)],
                        ReadOptionalTreatmentMember(_context.FileSystem.ResolvePath(relativePath))),
                _ => MirrorDriftPrepared && relativePath == AfterlifeEntityProfileState.StatePath
            };
            if (restored) OriginalCohortRestoredBeforeCut = true;
            _selectedMutationPath = selected ? _context!.FileSystem.ResolvePath(relativePath) : null;
            _selectedPhase = selected ? (_mode == "engine_mirror" ? "profile-mirror-refresh" : _mode == "pre_canonical_terminal" ? "original-terminal-quarantine" : "original-transaction-restoration") : null;
            _selectedStack = selected ? new StackTrace().ToString() : null;
            return Task.CompletedTask;
        }
        private void Published(TrustedLocalPublicationPhase phase, int index)
        {
            if (!Armed || phase != TrustedLocalPublicationPhase.MemberPublished) return;
            if (Cuts > 0) { PublicationsAfterCut++; return; }
            using var json = ReadJournalMetadata(File.ReadAllBytes(JournalPath));
            var target = json.RootElement.GetProperty("Members")[index].GetProperty("Path").GetString()!;
            var receipt = _originalRegistryState!.GetType().GetField("_openTreatmentPublicationReceipt",
                BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_originalRegistryState);
            if (BusinessCutCount == 0 && !SettlementArmed && receipt is MortalWoundTreatmentPublicationTakeReceipt &&
                new[] { ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                    WoundCarrierCatalog.PlayerPath, WoundHistoryState.HistoryPath }
                    .Any(relative => string.Equals(target, _context!.FileSystem.ResolvePath(relative), StringComparison.Ordinal)))
                PublishedTreatmentMembers++;
            if (_selectedMutationPath is null || !string.Equals(target, _selectedMutationPath, StringComparison.Ordinal)) return;
            if (_mode == "engine_mirror" && !string.Equals(target, _context!.FileSystem.ResolvePath(AfterlifeEntityProfileState.StatePath), StringComparison.Ordinal)) return;
            CutPhase = _selectedPhase; CutPhaseStack = _selectedStack;
            OriginalReceipt = Assert.IsType<MortalWoundTreatmentPublicationTakeReceipt>(
                _originalRegistryState!.GetType().GetField("_openTreatmentPublicationReceipt",
                    BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(_originalRegistryState));
            Target = target; TargetIndex = index; PublishedBytes = File.ReadAllBytes(target); Cuts++;
            if (_mode == "engine_mirror")
            {
                Assert.Same(_mirrorReceipt, OriginalReceipt);
                var published = ParseJsonObjectBytes(PublishedBytes);
                var publishedProfile = published["profiles"]!.AsArray().OfType<JsonObject>().Single(p =>
                    p["actorType"]!.GetValue<string>() == "player_soul" && p["actorId"]!.GetValue<string>() == "player_soul");
                MirrorPublishedAuthority17 = publishedProfile["currencies"]!["inkFeathers"]!.GetValue<int>() == 17;
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
        private static bool BytesEqual(byte[]? left, byte[]? right) =>
            left is null ? right is null : right is not null && left.AsSpan().SequenceEqual(right);
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
