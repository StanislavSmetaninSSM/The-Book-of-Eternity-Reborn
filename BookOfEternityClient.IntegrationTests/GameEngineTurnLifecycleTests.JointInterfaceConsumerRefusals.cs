using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;
using Xunit;

namespace BookOfEternityClient.Tests;

public sealed partial class GameEngineTurnLifecycleTests
{
    [Theory]
    [InlineData("Parent1")]
    [InlineData("ParentUnknown")]
    [InlineData("Tuple")]
    [InlineData("SealMissing")]
    [InlineData("RecordMissing")]
    [InlineData("Empty")]
    [InlineData("UnknownEmpty")]
    public async Task GuaranteedTreatment_ActualGenericInterfaceConsumerRefusesSelectedMarkerDamage(string damage)
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "Selector:" + damage);
        await RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        probe.AssertSelectorWasObserved();
        probe.AssertEarlyInterfaceAndSingleConsumption();
    }

    [Fact]
    public async Task GuaranteedTreatment_FirstInstallOwningCloseCannotArmInterfacePermission()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "InitialClose");
        var operation = RunJointCompletionControlAsync(false, useLiveCaller: true, interfaceLifecycleProbe: probe);
        try
        {
            await probe.AttachInitialOwnerCloseAsync(operation);
            await operation;
        }
        finally
        {
            probe.ReleaseInitialRead();
            if (!operation.IsCompleted) await Record.ExceptionAsync(() => operation);
        }
    }

    [Fact]
    public async Task GuaranteedTreatment_NewLateMainConsumesOwnReadyWithoutRevivingHotOwner()
    {
        using var probe = new JointInterfaceLifecycleProbe(this, "LateCurrent");
        await RunJointCompletionControlAsync(false, useLiveCaller: false, interfaceLifecycleProbe: probe);
        probe.AssertEarlyInterfaceAndSingleConsumption();
        probe.AssertNewLateOwnerObserved();
    }

    private sealed partial class JointInterfaceLifecycleProbe
    {
        private bool ChangesInterface => scenario is "Changed" or "Error" or "LateCurrent" or "InterfaceClose" or "OuterClose" or "ReadRace" or "ReadyDrift" ||
            scenario.StartsWith("Selector:", StringComparison.Ordinal) || scenario.StartsWith("Crash:", StringComparison.Ordinal);
        private int _selectorChecks;
        private int _lateWarningCount = -1;
        private readonly List<int> _genericWarningCounts = [];
        private bool _newLateOwnerObserved;
        private readonly TaskCompletionSource _initialReadEntered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _releaseInitialRead = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private Func<string, Task>? _priorInitialRead;
        private bool _initialInstalled, _initialPaused;
        private FileSystemManager.CanonicalWriteLease? _initialOwner;
        private readonly IOException _initialCloseFailure;
        private InitialInterfaceThrowingClose? _initialCloser;
        private byte[]? _initialPending;
        private Dictionary<string, byte[]>? _initialCanonical;

        internal bool ExpectsInitialClose => scenario == "InitialClose";
        private bool ExpectsObservedOwningClose => scenario is "InitialClose" or "InterfaceClose";

        private async Task ObserveFrozenWarningConsumersAsync(GameEngine engine)
        {
            var refresh = GetPrivateFieldValue<object?>(engine, "_mortalJointRepairCanonicalRefresh");
            Assert.NotNull(refresh);
            var issues = Assert.IsAssignableFrom<IReadOnlyList<ValidationIssue>>(refresh.GetType().GetProperty("Issues")!.GetValue(refresh));
            _lateWarningCount = issues.Count(issue => issue.Code == "accepted_turn_stale_interface_updates");
            var before = CaptureCanonicalFiles();
            var interfaceMtime = Mtime();
            for (var invocation = 0; invocation < 2; invocation++)
            {
                var returned = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine, "ReadMortalJointInterfaceAtGenericSlotAsync");
                _genericWarningCounts.Add(returned.Count(issue => issue.Code == "accepted_turn_stale_interface_updates"));
                Assert.Equal(JsonSerializer.Serialize(MortalJointInterfacePublication.Read(
                    JsonSerializer.SerializeToElement(_resultCommits[1])).Issues), JsonSerializer.Serialize(returned));
            }
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_warning_consumer_vectors", new
            { LateCount = _lateWarningCount, GenericIndependentInvocationCounts = _genericWarningCounts,
                LateIssues = issues, Frozen = _resultCommits[1]["issues"] });
            foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
        }

        private void ObserveNewLateReadyOwner(GameEngine engine)
        {
            var ready = GetPrivateFieldValue<object?>(engine, "_mortalJointInterfaceReady");
            Assert.NotNull(ready);
            var owner = GetPrivateField<FileSystemManager.MainAdmission>(ready, "Owner");
            var stage = GetPrivateFieldValue<object?>(ready, "Stage"); Assert.NotNull(stage);
            var oldOwner = GetPrivateField<FileSystemManager.MainAdmission>(stage, "ConsoleOwner");
            var hotReady = GetPrivateFieldValue<object?>(engine, "_hotRepairReady"); Assert.NotNull(hotReady);
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_new_late_main_ready", new
            { NewOwnerClosed = owner.Closed, NewOwnerAcquired = owner.Acquired, OldOwnerClosed = oldOwner.Closed,
                SameOwner = ReferenceEquals(owner, oldOwner), StageOrdinal = GetPrivateFieldValue<long>(ready, "StageOrdinal"),
                ReadyOrdinal = GetPrivateFieldValue<long>(ready, "ReadyOrdinal"),
                DeleteReturned = GetPrivateFieldValue<bool>(ready, "DeleteReturned"), ReleaseReturned = GetPrivateFieldValue<bool>(ready, "ReleaseReturned"),
                HotFaulted = GetPrivateFieldValue<bool>(hotReady, "Faulted") });
            Assert.False(owner.Closed); Assert.True(owner.Acquired); Assert.True(oldOwner.Closed);
            Assert.NotSame(oldOwner, owner); Assert.True(GetPrivateFieldValue<bool>(hotReady, "Faulted"));
            Assert.True(GetPrivateFieldValue<bool>(ready, "DeleteReturned")); Assert.True(GetPrivateFieldValue<bool>(ready, "ReleaseReturned"));
            Assert.True(GetPrivateFieldValue<long>(ready, "StageOrdinal") > 0); Assert.True(GetPrivateFieldValue<long>(ready, "ReadyOrdinal") > 0);
            _newLateOwnerObserved = true;
        }

        internal void AssertNewLateOwnerObserved() => Assert.True(_newLateOwnerObserved);

        private async Task ObserveActualGenericSelectorAsync(GameEngine engine)
        {
            Assert.Equal(0, _selectorChecks++);
            var markerPath = fixture._fs.ResolvePath(MortalQteDecisionPublication.StatePath);
            var markerBytes = File.ReadAllBytes(markerPath);
            var marker = ParseJsonObjectBytes(markerBytes);
            Assert.Equal(3, marker["outputBinding"]!["version"]!.GetValue<int>());
            var record = MortalJointInterfacePublication.Read(JsonSerializer.SerializeToElement(
                marker["outputBinding"]!["publishedInterfaceResult"]!["record"]));
            Assert.Equal("Published", record.State); Assert.False(record.HasErrors);
            var before = CaptureCanonicalFiles();
            // First prove that the current real generic slot can consume THIS
            // genuine common result under the still-current original/Main owner.
            var positive = await InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(engine, "ReadMortalJointInterfaceAtGenericSlotAsync");
            Assert.Equal(JsonSerializer.Serialize(record.Issues), JsonSerializer.Serialize(positive));
            foreach (var (path, bytes) in before) Assert.Equal(bytes, File.ReadAllBytes(path));
            switch (scenario["Selector:".Length..])
            {
                case "Parent1": marker["schemaVersion"] = 1; break;
                case "ParentUnknown": marker["schemaVersion"] = 7; break;
                case "Tuple": marker["requestId"] = marker["requestId"]!.GetValue<string>() + "-foreign"; break;
                case "SealMissing": Assert.True(marker.Remove("niPublication")); break;
                case "RecordMissing": Assert.True(marker["outputBinding"]!["publishedInterfaceResult"]!.AsObject().Remove("record")); break;
                case "Empty": marker.Clear(); break;
                case "UnknownEmpty": marker.Clear(); marker["schemaVersion"] = 7; break;
                default: throw new InvalidOperationException(scenario);
            }
            var damaged = Encoding.UTF8.GetBytes(marker.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed));
            File.WriteAllBytes(markerPath, damaged); // exactly one unauthenticated partial fault, never reseal authority
            Exception? refusal = null;
            try
            {
                refusal = await Record.ExceptionAsync(() => InvokePrivateAsync<IReadOnlyList<ValidationIssue>>(
                    engine, "ReadMortalJointInterfaceAtGenericSlotAsync"));
                fixture.WritePhysicalInjuryDiagnostic("iobs_actual_generic_selected_damage", new
                { scenario, Positive = positive, OriginalMarker = Raw(markerBytes), DamagedMarker = Raw(damaged),
                    Refusal = refusal?.ToString(), Before = before.ToDictionary(row => row.Key, row => Raw(row.Value)),
                    After = CaptureCanonicalFiles().ToDictionary(row => row.Key, row => Raw(row.Value)),
                    InterfaceMtimeBefore = interfaceMtime, InterfaceMtimeAfter = Mtime() });
                Assert.IsType<InvalidDataException>(refusal);
                if (scenario == "Selector:Tuple") Assert.Contains("joint_interface_selector_tuple_conflict", refusal!.Message);
                if (scenario is "Selector:Parent1" or "Selector:ParentUnknown" or "Selector:Empty" or "Selector:UnknownEmpty")
                    Assert.Contains("joint_interface_selected_parent_version_changed", refusal!.Message);
                Assert.Equal(interfaceMtime, Mtime());
                Assert.Equal(before.Keys.Order(StringComparer.Ordinal), CaptureCanonicalFiles().Keys.Order(StringComparer.Ordinal));
                foreach (var (path, bytes) in before) Assert.Equal(path == markerPath ? damaged : bytes, File.ReadAllBytes(path));
            }
            finally { File.WriteAllBytes(markerPath, markerBytes); } // fixture fault settlement only, after preserved refusal evidence
        }

        internal void AssertSelectorWasObserved() => Assert.Equal(1, _selectorChecks);

        private Dictionary<string, byte[]> CaptureCanonicalFiles() => Directory.EnumerateFiles(fixture._fs.GameSessionPath, "*", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes, StringComparer.Ordinal);

        private void AttachInitialCloseObservation()
        {
            if (!ExpectsObservedOwningClose) return;
            _priorInitialRead = fixture._consoleCanonicalReadObserver;
            fixture._consoleCanonicalReadObserver = async path =>
            {
                if (_priorInitialRead is { } prior) await prior(path);
                if (!_initialInstalled || _initialPaused || path != WoundAcceptedTurnSnapshotContract.PendingResolutionPath) return;
                _initialPaused = true;
                _initialPending = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
                _initialCanonical = CaptureCanonicalFiles();
                _initialReadEntered.TrySetResult();
                await _releaseInitialRead.Task;
            };
        }

        private void ObserveInitialInstallation(TrustedLocalPublicationPhase phase)
        {
            if (!ExpectsObservedOwningClose || _initialInstalled || phase != TrustedLocalPublicationPhase.Committed) return;
            var raw = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            if (raw is null) return;
            var pending = ParseJsonObjectBytes(raw);
            if (pending["jointRepairCheckpoint"] is not JsonObject checkpoint || checkpoint["schemaVersion"]?.GetValue<int>() != 2) return;
            if (ExpectsInitialClose) Assert.False(pending.ContainsKey(MortalJointInterfacePublication.PendingField));
            else if (pending[MortalJointInterfacePublication.PendingField] is not JsonObject result || result["state"]?.GetValue<string>() != "Published") return;
            _initialInstalled = true;
        }

        internal async Task AttachInitialOwnerCloseAsync(Task operation)
        {
            var first = await Task.WhenAny(_initialReadEntered.Task, operation).WaitAsync(TimeSpan.FromSeconds(150));
            if (ReferenceEquals(first, operation))
                fixture.WritePhysicalInjuryDiagnostic("iobs_initial_close_prerequisite_outcome", new
                { Failure = (await Record.ExceptionAsync(() => operation))?.ToString(), _initialInstalled, _initialPaused });
            Assert.Same(_initialReadEntered.Task, first);
            var inspector = typeof(OriginalOwnedLeaseCloseTests).GetMethod("InspectOriginalNestedOwningLease", BindingFlags.Static | BindingFlags.NonPublic)!;
            var boundary = ExpectsInitialClose ? "GameEngine+<RestoreMortalJointRepairBaselineAsync>" : "PrepareAndPublishWithOwningReleaseAsync";
            var inspected = ((FileSystemManager.CanonicalWriteLease Lease, string State))inspector.Invoke(null, [operation, boundary])!;
            _initialOwner = inspected.Lease;
            Assert.Same(fixture._fs, _initialOwner.Owner); Assert.True(_initialOwner.IsActive);
            Assert.NotNull(_initialOwner.MainAdmission); Assert.Null(_initialOwner.ExternalPublicationContext);
            _initialCloser = new InitialInterfaceThrowingClose(_initialCloseFailure);
            _initialOwner.ExternalPublicationContext = _initialCloser;
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_initial_install_close_attached", new
            { ActualOwner = inspected.State, Root = fixture._fs.BasePath, Pending = Raw(_initialPending), _initialInstalled, _initialPaused });
            ReleaseInitialRead();
        }

        internal void ReleaseInitialRead() => _releaseInitialRead.TrySetResult();

        internal void AssertInitialCloseHeld(GameEngine engine, Exception? escaped)
        {
            var pendingBytes = ReadPhysical(WoundAcceptedTurnSnapshotContract.PendingResolutionPath);
            fixture.WritePhysicalInjuryDiagnostic("iobs_actual_initial_install_close_outcome", new
            { Escaped = escaped?.ToString(), CloseFailure = _initialCloseFailure.ToString(), Calls = _initialCloser?.Calls,
                Active = _initialOwner?.IsActive, MainClosed = _initialOwner?.MainAdmission is null,
                AmbientClosed = _initialOwner?.AmbientRegistration is null, ExternalClosed = _initialOwner?.ExternalPublicationContext is null,
                PendingBefore = Raw(_initialPending), PendingAfter = Raw(pendingBytes),
                FreshCheckpoint = GetPrivateFieldValue<string?>(engine, "_mortalJointInterfaceFreshCheckpoint"),
                FreshConsumed = GetPrivateFieldValue<bool>(engine, "_mortalJointInterfaceFreshConsumed"),
                Held = GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld") });
            Assert.True(_initialInstalled); Assert.True(_initialPaused); Assert.Equal(1, _initialCloser!.Calls);
            Assert.NotNull(_initialOwner); Assert.False(_initialOwner.IsActive); Assert.Null(_initialOwner.MainAdmission);
            Assert.Null(_initialOwner.AmbientRegistration); Assert.Null(_initialOwner.ExternalPublicationContext);
            Assert.Equal(_initialPending, pendingBytes);
            Assert.Null(GetPrivateFieldValue<string?>(engine, "_mortalJointInterfaceFreshCheckpoint"));
            Assert.False(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfaceFreshConsumed"));
            Assert.True(GetPrivateFieldValue<bool>(engine, "_mortalJointInterfacePreparationHeld"));
            Assert.Equal(42, GetPrivateField<GameLoop>(engine, "_gameLoop").TurnNumber);
            Assert.Equal(0, _captureCallbacks); Assert.Empty(_resultCommits); Assert.Empty(_publications);
            // Operational repair/report surfaces may record the failure. The
            // installed command/pending/public original and mechanics cannot move.
            foreach (var path in new[] { AcceptedMechanicsPlan.WoundCommandPath,
                WoundAcceptedTurnSnapshotContract.PendingResolutionPath, "input/turn_request.json",
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath, PendingTurnSnapshotAuthority.AuthorityPath,
                ResourceMaterializationContract.DefinitionsPath, ResourceMaterializationContract.StatePath, ResourceMaterializationContract.HistoryPath,
                WoundCarrierCatalog.PlayerPath, WoundIdentityState.StatePath, WoundHistoryState.HistoryPath,
                EffectIdentityState.StatePath, EffectCarrierCatalog.PlayerPath, MortalWoundOccurrenceState.StatePath,
                MortalWoundOpportunityReceiptState.StatePath, "game_state/world/world_time.json" })
            {
                var absolute = fixture._fs.ResolvePath(path);
                Assert.Equal(_initialCanonical!.GetValueOrDefault(absolute), ReadPhysical(path));
            }
        }

        private void DisposeInitialCloseObservation()
        {
            ReleaseInitialRead();
            if (ExpectsObservedOwningClose) fixture._consoleCanonicalReadObserver = _priorInitialRead;
        }

        private sealed class InitialInterfaceThrowingClose(IOException failure) : IDisposable
        {
            internal int Calls { get; private set; }
            public void Dispose() { Calls++; throw failure; }
        }
    }
}
