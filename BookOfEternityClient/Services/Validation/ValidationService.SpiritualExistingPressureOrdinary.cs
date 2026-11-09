using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    /// <summary>Detached ordinary proposal DATA. No member grants source, resource or publication authority.</summary>
    internal sealed class SpiritualOrdinaryPressureComparisonData
    {
        internal SpiritualOrdinaryPressureComparisonData(string exchangeJson, SpiritualWoundCalculation calculation,
            IEnumerable<AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition> transitions,
            string rawFingerprint, string pointer)
        {
            OrdinaryExchangeJson = exchangeJson;
            OrdinaryCalculation = calculation;
            ExpectedTransitions = Array.AsReadOnly(transitions.ToArray());
            RawInputFingerprint = rawFingerprint;
            ExchangePointer = pointer;
        }
        internal string OrdinaryExchangeJson { get; }
        internal SpiritualWoundCalculation OrdinaryCalculation { get; }
        internal IReadOnlyList<AfterlifeSpiritualConflictResourceOutcome.ExpectedTransition> ExpectedTransitions { get; }
        internal string RawInputFingerprint { get; }
        internal string ExchangePointer { get; }
        /// <summary>Returns a fresh mutable copy without exposing retained comparison DATA.</summary>
        internal JsonObject ReadOrdinaryExchange() => JsonNode.Parse(OrdinaryExchangeJson)!.AsObject();
    }

    /// <summary>Ordinary DATA or diagnostics; a blocked genuine prefix carries no resource capability.</summary>
    internal sealed record SpiritualOrdinaryPressureComparisonResult(SpiritualOrdinaryPressureComparisonData? Comparison,
        IReadOnlyList<ValidationIssue> Issues, bool PrefixBlocked = false);

    /// <summary>Readiness of the real original prefix, without exporting its owned continuation.</summary>
    internal sealed record SpiritualOriginalResourcePrefixPreparation(bool Ready,
        IReadOnlyList<ValidationIssue> Issues, bool PrefixBlocked = false);

    /// <summary>Detached frontier DATA; each observation entrypoint separately enforces its supported closed contour.</summary>
    internal sealed record SpiritualOrdinaryPressureProbeState(string SourceStateJson, string ResourceStateJson,
        string ResourceHistoryJson, string ResourceTranscriptJson, string EffectPrefixJson,
        int NextOrdinal, int AllocationCursor, string AllocationJournalJson);

    /// <summary>Actual source projection inventory sampled under the same exclusion gate as the full frontier.</summary>
    internal sealed record SpiritualPressureProjectionInventory(int Pending, int Live, int Admitted);

    /// <summary>Immutable latest-closed diagnostic DATA; fingerprints do not represent live routing identity.</summary>
    internal sealed record SpiritualClosedPressureComparisonProbeState(SpiritualOrdinaryPressureProbeState Frontier,
        string CaptureStateJson, string DraftStateJson, long SourceRevision, long WoundVersion,
        long DependencyCutVersion, string RoutingStateFingerprint, SpiritualPressureProjectionInventory Projections);

    /// <summary>Complete detached historical observation; no member is a current owner or capability.</summary>
    internal sealed record SpiritualPressureActualRefusalObservation(SpiritualOrdinaryPressureProbeState Frontier,
        SpiritualPressureProjectionInventory Projections);

    /// <summary>Immutable scalar refusal diagnosis, deliberately excluding mutable repair contexts.</summary>
    internal sealed record SpiritualPressureFrozenIssue(string FilePath, IssueSeverity Severity, string Message,
        IssueCategory Category, string? Code, string? Actor, string? Section, string? Expected, string? Actual,
        string? RepairHint, string RepairTargetFilesJson);

    /// <summary>Distinguishes complete and unavailable requested observations; absence is a null stored result.</summary>
    internal sealed record SpiritualPressureActualRefusalObservationResult(SpiritualPressureActualRefusalObservation? Observation,
        string? UnavailableReason, IReadOnlyList<SpiritualPressureFrozenIssue>? Issues);

    internal sealed partial class SpiritualOriginalTurnCapture
    {
        private bool _pressureRefusalObservationRequested;
        private SpiritualPressureActualRefusalObservationResult? _pressureRefusalObservation;

        /// <summary>Reads stored historical DATA only, including after disposal, without querying any live owner.</summary>
        internal SpiritualPressureActualRefusalObservationResult? ReadExistingPressureActualRefusalObservation() =>
            Volatile.Read(ref _pressureRefusalObservation);

        /// <summary>Requests one diagnostic observation on the next Advance only; no source work or permission is created.</summary>
        /// <param name="lease">Active canonical lease for this exact current capture and closed ordinal-zero prefix.</param>
        /// <returns>Completion after arming the one-shot request; stale or unsupported requests throw without arming.</returns>
        internal async Task RequestExistingPressureActualRefusalObservationAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureExistingPressureProbeContourUnderGate(lease);
                if (Volatile.Read(ref _pressureRefusalObservation) != null)
                    throw new InvalidOperationException("A retained historical pressure observation cannot be replaced.");
                _pressureRefusalObservationRequested = true;
            }
            finally { _gate.Release(); }
        }

        private async Task CaptureRequestedPressureRefusalObservationUnderGateAsync(FileSystemManager.CanonicalWriteLease lease,
            IReadOnlyList<ValidationIssue> issues)
        {
            _pressureRefusalObservationRequested = false;
            IReadOnlyList<SpiritualPressureFrozenIssue>? frozen = null;
            SpiritualPressureActualRefusalObservationResult result;
            try
            {
                frozen = Array.AsReadOnly(issues.Select(issue => new SpiritualPressureFrozenIssue(issue.FilePath,
                    issue.Severity, issue.Message, issue.Category, issue.Code, issue.Actor, issue.Section, issue.Expected,
                    issue.Actual, issue.RepairHint, JsonSerializer.Serialize(issue.RepairTargetFiles))).ToArray());
                var observation = await ReadExistingPressureOrdinaryProbeUnderGateAsync(lease);
                result = new(observation, null, frozen);
            }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException or JsonException or
                FormatException or OverflowException or IOException or UnauthorizedAccessException or NotSupportedException or
                OperationCanceledException)
            {
                // Missing frozen issues remain null if freezing itself refused. Fatal runtime failures are not hidden.
                // Recoverable diagnostics must neither manufacture a frontier nor replace the original refusal/revocation.
                result = new(null, error.GetType().Name + ": " + error.Message, frozen);
            }
            Volatile.Write(ref _pressureRefusalObservation, result);
        }

        /// <summary>Closes genuine original resource work without preparing or admitting a spiritual exchange.</summary>
        /// <param name="lease">Actual active canonical lease for these retained owners.</param>
        /// <returns>Detached readiness, genuine prefix wait description, or validation diagnostics.</returns>
        internal async Task<SpiritualOriginalResourcePrefixPreparation> PrepareOriginalResourcePrefixAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureCurrent(lease);
                var issues = await CheckRetainedInputsAsync(lease);
                if (issues.Count != 0) return new(false, issues);
                var prepared = await PrepareOriginalResourcePrefixUnderGateAsync(lease);
                return new(prepared.Issues.Count == 0 && (!_usesOriginalPrefix || _originalPrefix != null),
                    prepared.Issues, prepared.Step?.PendingResource != null);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Independently validates the frozen ordinary proposal using exact live owners, never a caller proof.</summary>
        /// <param name="lease">Actual active canonical lease for this capture's filesystem.</param>
        /// <returns>Detached comparison DATA, diagnostics or a blocked original-prefix description.</returns>
        internal async Task<SpiritualOrdinaryPressureComparisonResult> ReadExistingPressureOrdinaryComparisonAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _gate.WaitAsync();
            try { return await ReadExistingPressureOrdinaryComparisonUnderGateAsync(lease); }
            finally { _gate.Release(); }
        }

        /// <summary>Shares the identical comparison validation and error handling without reacquiring the held capture gate.</summary>
        /// <param name="lease">Actual active canonical lease, checked again for either caller.</param>
        /// <returns>Detached ordinary DATA or the original diagnostics and prefix-blocked result.</returns>
        private async Task<SpiritualOrdinaryPressureComparisonResult> ReadExistingPressureOrdinaryComparisonUnderGateAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            try
            {
                EnsureCurrent(lease);
                if (_resources == null)
                    throw new InvalidOperationException("Begin the original resource executor first.");
                if (_effects is { HasPendingWoundIntegration: true })
                    return new(null, OrdinaryPressureFailure("spiritual_wound_routing_required", "current installed wound routing"));
                var issues = await CheckExistingPressureOrdinaryInputsAsync(lease);
                if (issues.Count != 0) return new(null, issues);
                var prefix = await PrepareOriginalResourcePrefixUnderGateAsync(lease);
                if (prefix.Issues.Count != 0) return new(null, prefix.Issues);
                if (prefix.Step?.PendingResource != null) return new(null, [], true);
                if (_lastResourceStep is { PendingResource: not null } or { PendingExchange: not null })
                    return new(null, OrdinaryPressureFailure("spiritual_resource_wait_unresolved", "resume the exact owned wait first"));
                if (!_usesOriginalPrefix || _originalPrefix == null || _coldProposedInputs != null || _terminal != null)
                    return new(null, OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "a continuing original-prefix comparison"));
                return await _source.ReadOwnedExistingPressureOrdinaryAsync(lease, this);
            }
            catch
            {
                _source.InvalidateExistingPressureOrdinary();
                throw;
            }
        }

        /// <summary>Fresh exact physical input checks supplement the existing conflict-prefix exceptions.</summary>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckExistingPressureOrdinaryInputsAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            _source.RecordOrdinaryFreshInputCheck();
            var issues = await CheckRetainedInputsAsync(lease);
            if (issues.Count != 0) return issues;
            return await CheckExistingPressureOrdinaryRawInputsCoreAsync(lease);
        }

        /// <summary>
        /// Rechecks exact ordinary conflict and request bytes without replacing the caller's
        /// current control-owner checks. C2 transport authenticates its exact successor pair.
        /// </summary>
        internal async Task<IReadOnlyList<ValidationIssue>> CheckExistingPressureOrdinaryRawInputsAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            _source.RecordOrdinaryFreshInputCheck();
            return await CheckExistingPressureOrdinaryRawInputsCoreAsync(lease);
        }

        private async Task<IReadOnlyList<ValidationIssue>> CheckExistingPressureOrdinaryRawInputsCoreAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            foreach (var path in new[] { AfterlifeSpiritualConflictState.StatePath, LiveTurnPreparationService.TurnRequestPath })
            {
                var expected = path == AfterlifeSpiritualConflictState.StatePath
                    ? _draftInputs.ReadImage(path)
                    : _physicalWitnesses[path];
                var bytes = await _validator._fs.ReadFileBytesAsync(lease, path);
                if (expected.Fingerprint == new CanonicalBeforeImage(bytes != null, bytes).Fingerprint) continue;
                RevokeUnderLease(lease);
                return [SourceIssue(path, "spiritual_original_input_changed", "the exact original ordinary input bytes")];
            }
            return [];
        }

        /// <summary>Derives the real comparison basis only for this locked capture and its exact live source.</summary>
        /// <param name="lease">Actual active canonical lease.</param>
        /// <param name="source">The exact source holding its continuation gate.</param>
        /// <returns>Original resource references and raw input identity, never caller-selected alternatives.</returns>
        internal (ResourceOwnerAuthority Owners, ResourceStateLedger State, string RawFingerprint)
            ReadExistingPressureOrdinaryEvaluationBasisUnderGate(FileSystemManager.CanonicalWriteLease lease,
                SpiritualWoundSourceSession source)
        {
            EnsureCurrent(lease);
            if (_gate.CurrentCount != 0 || !ReferenceEquals(_source, source) || _resources == null ||
                _originalPrefix == null || !OwnsOriginalPrefix(source, _originalPrefix) ||
                _effects is not { HasPendingWoundIntegration: false } || _terminal != null ||
                _coldProposedInputs != null || !TryReadNextSourceFrontier(source, out _))
                throw new InvalidOperationException("Comparison requires this exact locked original source/resource basis.");
            return (_input.PlanningContext!.Owners, SourceResourceBaseline,
                _draftInputs.ReadImage(AfterlifeSpiritualConflictState.StatePath).Fingerprint);
        }

        /// <summary>Captures complete immutable original inventory and capture/source/resource identity for cache keys.</summary>
        internal OperationStructuralSnapshot ReadExistingPressureOrdinaryIdentity(FileSystemManager.CanonicalWriteLease lease,
            string rawFingerprint, string pointer, string groupData, string inputRole, string affectedSide, string actorId)
        {
            EnsureCurrent(lease);
            var values = new List<string?> { SessionId, RequestId, _source.SnapshotToken,
                _source.TurnNumber.ToString(CultureInfo.InvariantCulture), _source.Realm, inputRole, "3", "projection:0",
                affectedSide, actorId, affectedSide == "opposition" ? "/incomingAction" : "/operationType",
                rawFingerprint, pointer, groupData, _coldViewRevision.ToString(CultureInfo.InvariantCulture),
                _nextResourceOrdinal.ToString(CultureInfo.InvariantCulture), _source.ContinuationRevision.ToString(CultureInfo.InvariantCulture) };
            foreach (var path in _draftInputs.PathInventory)
            {
                values.Add(path);
                values.Add(_draftInputs.ReadImage(path).Fingerprint);
            }
            foreach (var pair in _physicalWitnesses.OrderBy(pair => pair.Key, StringComparer.Ordinal))
            {
                values.Add(pair.Key);
                values.Add(pair.Value.Fingerprint);
            }
            return new(values, [this, _source, _resources, _effects, _originalPrefix, SourceResourceBaseline, lease]);
        }

        /// <summary>Reads allocation DATA only for the exact source while both owner gates already exclude continuations.</summary>
        /// <param name="lease">Actual active lease checked again before reading the journal.</param>
        /// <param name="source">The exact current source invoking the bounded diagnostic reader.</param>
        /// <returns>Detached committed cursor and full journal, never the allocation owner.</returns>
        internal (int Cursor, string JournalJson) ReadExistingPressureOrdinaryAllocationUnderGate(
            FileSystemManager.CanonicalWriteLease lease, SpiritualWoundSourceSession source)
        {
            EnsureCurrent(lease);
            if (_gate.CurrentCount != 0 || !ReferenceEquals(_source, source) || _originalPrefix == null ||
                !OwnsOriginalPrefix(source, _originalPrefix))
                throw new InvalidOperationException("Allocation diagnostics require the exact locked original source and capture.");
            return (_allocations.Journal.ReadCursor(), _allocations.Journal.Export().ToJsonString());
        }

        /// <summary>Reads the bounded pre-exchange diagnostic frontier without initializing comparison or mechanics caches.</summary>
        /// <param name="lease">Active canonical lease for this capture.</param>
        /// <returns>Detached source, resource, effect and allocation DATA.</returns>
        internal async Task<SpiritualOrdinaryPressureProbeState> ReadExistingPressureOrdinaryProbeStateAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                return (await ReadExistingPressureOrdinaryProbeUnderGateAsync(lease)).Frontier;
            }
            finally { _gate.Release(); }
        }
        private void EnsureExistingPressureProbeContourUnderGate(FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            if (_gate.CurrentCount != 0 || !_usesOriginalPrefix || _originalPrefix == null || _nextResourceOrdinal != 0 ||
                _lastResourceStep is { PendingResource: not null } or { PendingExchange: not null } ||
                _effects == null || _effects.HasPendingWoundIntegration ||
                _effects.WoundReadVersion != 0 || _resources == null || _closedExchangeEvidence.Count != 0)
                throw new InvalidOperationException("Diagnostics require the closed unstaged ordinal-zero original prefix.");
        }

        /// <summary>Shares strict live snapshot collection without reacquiring the already held capture gate.</summary>
        private Task<SpiritualPressureActualRefusalObservation> ReadExistingPressureOrdinaryProbeUnderGateAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureExistingPressureProbeContourUnderGate(lease);
            return _source.ReadExistingPressureOrdinaryProbeStateAsync(lease, this, _resources!,
                _nextResourceOrdinal, _effects!.WoundReadVersion, _effects.DependencyCutVersion);
        }

        /// <summary>Observes a positive closed original frontier without preparing or validating any continuation.</summary>
        /// <param name="lease">Active canonical lease belonging to this exact live capture.</param>
        /// <returns>One detached image collected under capture, source, draft and resource exclusion.</returns>
        internal async Task<SpiritualClosedPressureComparisonProbeState> ReadClosedPressureComparisonProbeStateAsync(
            FileSystemManager.CanonicalWriteLease lease)
        {
            await _gate.WaitAsync();
            try
            {
                EnsureClosedPressureProbeContourUnderGate(lease);
                return await _source.ReadClosedPressureComparisonProbeStateAsync(lease, this, _resources!,
                    _effects!, _closedExchangeEvidence[^1].Interval, _nextResourceOrdinal);
            }
            finally { _gate.Release(); }
        }

        /// <summary>Checks the retained closed contour only; no physical validation or owner mutation occurs.</summary>
        /// <param name="lease">Active canonical lease for the already locked capture.</param>
        private void EnsureClosedPressureProbeContourUnderGate(FileSystemManager.CanonicalWriteLease lease)
        {
            EnsureCurrent(lease);
            if (_gate.CurrentCount != 0 || !_usesOriginalPrefix || _originalPrefix == null ||
                _nextResourceOrdinal <= 0 || _closedExchangeEvidence.Count != _nextResourceOrdinal ||
                _source.CheckedExchanges.Count != _nextResourceOrdinal || _resources is not { Result: null } ||
                _effects == null || _terminal != null || _coldProposedInputs != null ||
                _completedEffects != null || _completedOrdinaryReduction != null ||
                _lastResourceStep is { PendingResource: not null } or { PendingExchange: not null } ||
                !OwnsOriginalPrefix(_source, _originalPrefix) ||
                _closedExchangeEvidence.Where((entry, ordinal) => entry.Interval.Ordinal != ordinal ||
                    !_resources.Owns(entry.Interval) || entry.Healing != null).Any() ||
                !ReferenceEquals(_lastResourceStep?.Interval, _closedExchangeEvidence[^1].Interval))
                throw new InvalidOperationException("Diagnostics require a live positive contiguous closed original exchange prefix.");
        }

        /// <summary>Serializes capture-owned facts while every observation exclusion is already held.</summary>
        /// <param name="lease">Current canonical lease, rechecked without reading or validating files.</param>
        /// <param name="source">Exact source holding its continuation gate.</param>
        /// <returns>Detached capture inventories, never admissions, selections or insertion owners.</returns>
        internal string ReadClosedPressureCaptureDiagnosticJsonUnderGate(FileSystemManager.CanonicalWriteLease lease,
            SpiritualWoundSourceSession source)
        {
            EnsureClosedPressureProbeContourUnderGate(lease);
            if (!ReferenceEquals(_source, source))
                throw new InvalidOperationException("Diagnostics require the exact capture-owned source.");
            return JsonSerializer.Serialize(new
            {
                SessionId, RequestId, _nextResourceOrdinal, _coldViewRevision, _usesColdOriginalInputs,
                InitialColdInputsTaken = _initialColdSourceInputsTaken,
                OriginalPrefixFingerprint = _originalPrefix!.EffectPrefix.Fingerprint,
                OriginalPrefixReceipts = _originalPrefixReceipts.Select(row => row.Receipts).ToArray(),
                DraftInputs = _draftInputs.PathInventory.Select(path => new
                {
                    Path = path, Fingerprint = _draftInputs.ReadImage(path).Fingerprint
                }).ToArray(),
                Witnesses = _physicalWitnesses.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { Path = pair.Key, pair.Value.Fingerprint }).ToArray(),
                ClosedExchanges = _closedExchangeEvidence.Select(entry => new
                {
                    entry.Interval.ConflictId, entry.Interval.ExchangeId, entry.Interval.Ordinal,
                    entry.ExchangeJson, entry.Sources, entry.DiceClaims, entry.ConflictBeforeFingerprint,
                    entry.ConflictAfterFingerprint, entry.ResourceResultFingerprint, entry.EffectPlanFingerprint,
                    entry.DangerDeclarationFingerprint, entry.ConflictAfter,
                    EffectBefore = entry.Interval.EffectBefore.Fingerprint,
                    EffectAfter = entry.Interval.EffectAfter.Fingerprint
                }).ToArray(),
                Admissions = _woundAdmissions.Values.Select(admission => new
                {
                    admission.Source, admission.SourceRevision, admission.Interval.Ordinal,
                    admission.Interval.ConflictId, admission.Interval.ExchangeId
                }).ToArray(),
                Opportunities = _woundOpportunities.Select(pair => new
                {
                    SourceCoordinate = pair.Key.Source.Coordinate,
                    Binding = ReadPressureBindingDiagnosticData(pair.Value.Binding),
                    Opportunity = ReadPressureOpportunityDiagnosticData(pair.Value.Opportunity),
                    pair.Value.Issues,
                    Satisfaction = pair.Value.Satisfaction is not { } satisfaction ? null : new
                    {
                        satisfaction.InstanceId, satisfaction.OpportunityRef, satisfaction.WoundId,
                        satisfaction.SeverityRank, satisfaction.WoundFingerprint, satisfaction.WoundReadVersion
                    }
                }).ToArray(),
                Selections = _woundSelections.Values.Select(selection => new
                {
                    SourceCoordinate = selection.Admission.Source.Coordinate, selection.ProposalRawFingerprint,
                    LocationsFingerprint = selection.Locations?.Fingerprint,
                    Input = ReadPressureInputDiagnosticData(selection.Input)
                }).ToArray(),
                Declines = _woundDeclines.Select(pair => new
                {
                    SourceCoordinate = pair.Key.Source.Coordinate, pair.Value.Fingerprint,
                    Binding = ReadPressureBindingDiagnosticData(pair.Value.Binding),
                    Command = pair.Value.Composition.CommandRoot,
                    CompositionSourceBinding = ReadPressureBindingDiagnosticData(pair.Value.Composition.SourceBinding),
                    Opportunities = pair.Value.Composition.MaterializedOpportunities.Select(ReadPressureOpportunityDiagnosticData).ToArray(),
                    Transitions = pair.Value.Composition.Transitions.Select(ReadPressureTransitionDiagnosticData).ToArray(),
                    pair.Value.Composition.Notifications, pair.Value.Composition.DecisionReceipts,
                    CompositionIssues = pair.Value.Composition.Issues,
                    WoundJson = ReadPressureWoundResultDiagnosticJson(pair.Value.Result), pair.Value.Result.Issues
                }).ToArray(),
                Materializations = _materializedWounds.Select(pair => new
                {
                    SourceCoordinate = pair.Key.Admission.Source.Coordinate,
                    WoundJson = ReadPressureWoundResultDiagnosticJson(pair.Value), pair.Value.Issues
                }).ToArray(),
                Insertions = _registeredWoundInsertions.Select(pair => new
                {
                    SourceCoordinate = pair.Key.Admission.Source.Coordinate,
                    pair.Value.VersionAfter, WoundJson = WoundMaterializationContract.SerializeCanonical(pair.Value.Wound)
                }).ToArray()
            });
        }
    }

    /// <summary>Projects immutable binding facts instead of default-serializing an internal-property class.</summary>
    /// <param name="binding">Already retained binding, or null.</param>
    /// <returns>Complete plain binding DATA, preserving absence.</returns>
    private static object? ReadPressureBindingDiagnosticData(WoundAcceptedTurnBinding? binding) => binding == null ? null : new
    {
        binding.SessionId, binding.RequestId, binding.SnapshotToken, binding.Realm, binding.Turn,
        binding.AcceptedEvents, binding.AcceptedEventsFingerprint
    };

    /// <summary>Projects complete source opportunity DATA without exporting its authority object.</summary>
    /// <param name="opportunity">Already retained opportunity, or null.</param>
    /// <returns>Plain opportunity facts and existing guarantee/target images.</returns>
    private static object? ReadPressureOpportunityDiagnosticData(WoundOpportunityAuthority? opportunity) => opportunity == null ? null : new
    {
        opportunity.SessionId, opportunity.RequestId, opportunity.SnapshotToken, opportunity.OpportunityId,
        opportunity.PublicRef, opportunity.EventRef, opportunity.EventKind, opportunity.EventAuthorityId,
        opportunity.AcceptedEventsFingerprint, opportunity.Owner, opportunity.Domain, opportunity.ProfileKey,
        opportunity.SourceKind, opportunity.SourceId, opportunity.SourceState, opportunity.MinimumSeverityRank,
        opportunity.MaximumSeverityRank, opportunity.GuaranteedTrigger, opportunity.InputEvidenceFingerprint,
        opportunity.AuthorityFingerprint, opportunity.RequiredSeverityRank, opportunity.GuaranteedTriggerId,
        SafeContext = new { opportunity.SafeContext.Target, opportunity.SafeContext.Cause, opportunity.SafeContext.AllowedLocationKinds },
        Worsening = opportunity.WorseningTarget is not { } target ? null : new
        {
            WoundJson = WoundMaterializationContract.SerializeCanonical(target.Wound), target.CauseKind, target.ExpectedBeforeFingerprint
        },
        OriginalGuarantee = opportunity.OriginalSourceGuarantee is not { } guarantee ? null : new
        {
            guarantee.RequiredSeverityRank, guarantee.RequestTurn, guarantee.TriggerId,
            guarantee.SourceContractFingerprint, guarantee.AuthorityFingerprint
        }
    };

    /// <summary>Projects a selection's detached input once, preserving its complete authored snapshots.</summary>
    /// <param name="input">Detached input already retained by the selection.</param>
    /// <returns>Complete authored input facts without live selection authority.</returns>
    private static object ReadPressureInputDiagnosticData(WoundAcceptedTurnInput input) => new
    {
        Binding = ReadPressureBindingDiagnosticData(input.Binding),
        Opportunities = input.Opportunities.Select(ReadPressureOpportunityDiagnosticData).ToArray(),
        Transitions = input.Transitions.Select(ReadPressureTransitionDiagnosticData).ToArray(),
        input.PreTurnCarriers, input.PreTurnIdentityIndex, input.PreTurnHistory,
        input.PreTurnEffectCarriers, input.PreTurnEffectIdentityIndex
    };

    /// <summary>Projects one retained authored transition with its complete canonical wound and definition images.</summary>
    /// <param name="transition">Existing detached authored transition.</param>
    /// <returns>Plain transition DATA without preparation authority.</returns>
    private static object ReadPressureTransitionDiagnosticData(WoundAcceptedTransitionDraft transition) => new
    {
        transition.Kind, transition.OperationKey, transition.LocalWoundRef, transition.LocalTransitionRef,
        transition.OpportunityId, transition.ReadableSummary,
        WoundJson = WoundMaterializationContract.SerializeCanonical(transition.ProposedAfter),
        Definitions = transition.EffectDefinitions.Select(definition => new { definition.LocalEffectRef, definition.Definition }).ToArray(),
        transition.RootApplications, transition.SlotBindings
    };

    /// <summary>Reads one detached result wound once and preserves its full canonical owned-effect graph.</summary>
    /// <param name="result">Already retained materialization or decline result.</param>
    /// <returns>Complete canonical wound DATA, or null for an empty result.</returns>
    private static string? ReadPressureWoundResultDiagnosticJson(OriginalWoundMaterializationResult result)
    {
        var wound = result.Wound;
        return wound == null ? null : WoundMaterializationContract.SerializeCanonical(wound);
    }

    private static IReadOnlyList<ValidationIssue> OrdinaryPressureFailure(string code, string detail) =>
        [SourceIssue(AfterlifeSpiritualConflictState.StatePath, code, detail)];

    internal sealed partial class SpiritualWoundSourceSession
    {
        private AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext.ExistingPressureComparisonGroup? _ordinaryPressureGroup;
        private ValidationOperation? _ordinaryOperation;
        private FileSystemManager.CanonicalWriteLease? _ordinaryLease;
        private AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext? _ordinaryContext;
        private AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext.ExistingPressureComparisonGroup? _ordinaryGroup;
        private Func<OperationStructuralSnapshot?>? _ordinaryDependencyCapture;
        private object? _ordinaryInputIdentity;
        private OperationStructuralSnapshot? _ordinaryRetainedDependencies;
        private long _ordinarySourceRevision = -1;
        private sealed record OrdinaryPressureEvaluationInput(JsonObject FrozenRaw, int Ordinal,
            string RawFingerprint, string Pointer);
        private OrdinaryPressureEvaluationInput? _ordinaryEvaluationInput;
        private int _ordinaryEvaluations;
        private int _actualEvaluations;
        private int _ordinaryFreshInputChecks;
        private int _ordinaryFreshDependencyChecks;
        private static readonly OperationComputation<SpiritualOrdinaryPressureComparisonData> OrdinaryPressureComputation =
            new("owned-existing-pressure-ordinary", 3, value => value);

        internal void RecordOrdinaryFreshInputCheck() => _ordinaryFreshInputChecks++;

        /// <summary>Invalidates private comparison lifetime on failure, lease/epoch change, cancellation or revocation.</summary>
        internal void InvalidateExistingPressureOrdinary()
        {
            _ordinaryOperation?.Dispose();
            _ordinaryOperation = null;
            _ordinaryLease = null;
            _ordinaryContext = null;
            _ordinaryGroup = null;
            _ordinaryDependencyCapture = null;
            _ordinaryInputIdentity = null;
            _ordinaryRetainedDependencies = null;
            _ordinarySourceRevision = -1;
            _ordinaryEvaluationInput = null;
        }

        private sealed class OrdinaryPressureRefusal(IReadOnlyList<ValidationIssue> issues) : Exception
        {
            internal IReadOnlyList<ValidationIssue> Issues { get; } = issues;
        }

        /// <summary>Serializes cache lookup and computation with all source continuations under their existing gate.</summary>
        internal async Task<SpiritualOrdinaryPressureComparisonResult> ReadOwnedExistingPressureOrdinaryAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _continuationGate.WaitAsync();
            try { return await ReadOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture); }
            finally { _continuationGate.Release(); }
        }

        private async Task<SpiritualOrdinaryPressureComparisonResult> ReadOwnedExistingPressureOrdinaryUnderGateAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            try
            {
                await PrepareOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture);
                return new(await ComputeOwnedExistingPressureOrdinaryUnderGateAsync(lease, capture), []);
            }
            catch (OrdinaryPressureRefusal refused)
            {
                InvalidateExistingPressureOrdinary();
                return new(null, refused.Issues);
            }
            catch (Exception error) when (error is not ObjectDisposedException &&
                error is JsonException or FormatException or ArgumentException or InvalidOperationException or OverflowException)
            {
                InvalidateExistingPressureOrdinary();
                return new(null, OrdinaryPressureFailure("spiritual_source_continuation_invalid", error.Message));
            }
            catch
            {
                InvalidateExistingPressureOrdinary();
                throw;
            }
        }

        /// <summary>Invalidates all retained selection/input/view state, not just cached results, on any complete dependency change.</summary>
        private bool PrepareExistingPressureOrdinaryContextUnderGate(FileSystemManager.CanonicalWriteLease lease,
            SpiritualOriginalTurnCapture capture, List<ValidationIssue> issues)
        {
            if (!ReferenceEquals(_ordinaryLease, lease) || _ordinaryContext?.IsCurrent != true ||
                _ordinarySourceRevision != _continuationRevision)
                InvalidateExistingPressureOrdinary();
            if (_ordinaryContext != null && _ordinaryRetainedDependencies != null)
            {
                _ordinaryFreshDependencyChecks++;
                var current = _ordinaryContext.ReadOrdinaryPressureDependencySnapshot();
                if (current == null || !_ordinaryRetainedDependencies.Matches(current))
                    InvalidateExistingPressureOrdinary();
            }
            _ordinaryContext ??= capture.PrepareSourceMechanics(this, issues);
            if (_ordinaryContext == null || issues.Count != 0) return false;
            _ordinaryLease = lease;
            _ordinarySourceRevision = _continuationRevision;
            return true;
        }

        /// <summary>Prepares singleton III/IV or exact-two III/IV complete-group input for query and the separately guarded actual adapter.</summary>
        private async Task PrepareOwnedExistingPressureOrdinaryUnderGateAsync(FileSystemManager.CanonicalWriteLease lease,
            SpiritualOriginalTurnCapture capture)
        {
            if (!IsCurrentOwner || !ReferenceEquals(_prefixCapture, capture) || _awaitingOriginalPrefix ||
                !capture.TryReadNextSourceFrontier(this, out var limit))
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_source_frontier_owner_mismatch", "exact current capture and source frontier"));
            var basis = capture.ReadExistingPressureOrdinaryEvaluationBasisUnderGate(lease, this);
            await RequireOrdinaryOriginAsync(lease);
            RecordExistingStrainComparisonQuery();
            var issues = new List<ValidationIssue>();
            if (!PrepareExistingPressureOrdinaryContextUnderGate(lease, capture, issues))
                throw new OrdinaryPressureRefusal(issues.Count != 0 ? issues :
                    OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "current exact source/resource mechanics"));
            if (!TryReadInitialActiveExchangeInventory(out var conflictId, out var ids) ||
                limit != ids.Length || _checkedExchanges.Count + 1 != ids.Length || _missingActionCostAudit != null ||
                _originalConflict["activeConflict"] is not JsonObject original ||
                original["exchangeLog"] is not JsonArray historical ||
                _candidate[AfterlifeSpiritualConflictState.StatePath] is not { } rawText)
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "the last complete direct-root ordinary exchange"));
            var raw = SpiritualWoundDependentDraftPolicy.ReadStrictRoot(rawText);
            if (raw.ContainsKey(AfterlifeSpiritualConflictState.ResponseField) || raw["activeConflict"] is not JsonObject conflict ||
                AfterlifeSpiritualConflictState.ResolveRawExchange(_originalConflict, raw, historical.Count + limit - 1) is not { } selected ||
                selected.Exchange["exchangeId"]?.GetValue<string>() != ids[^1] ||
                selected.Pointer != "/activeConflict/exchangeLog/" + (historical.Count + limit - 1).ToString(CultureInfo.InvariantCulture))
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "the exact last original direct-root exchange"));
            _ordinaryGroup ??= AcceptedMechanicsPlanner.ResourceExecutionSession.SpiritualMechanicsContext
                .ExistingPressureComparisonGroup.Capture(_ordinaryContext!, conflict, selected.Exchange, issues);
            if (_ordinaryGroup == null || issues.Count != 0)
                throw new OrdinaryPressureRefusal(issues.Count != 0 ? issues :
                    OrdinaryPressureFailure("spiritual_pressure_ordinary_contour_unsupported", "one complete signed current rank-III/IV parent or a complete rank-III/IV pressure strain group"));
            _ordinaryEvaluationInput ??= new(raw.DeepClone().AsObject(), limit - 1, basis.RawFingerprint, selected.Pointer);
            _ordinaryFreshDependencyChecks++;
            var dependencies = _ordinaryContext!.ReadOrdinaryPressureDependencySnapshot();
            if (dependencies == null)
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "the exact current unstaged resource dependency cut"));
            if (_ordinaryRetainedDependencies != null && !_ordinaryRetainedDependencies.Matches(dependencies))
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "unchanged complete group capture dependencies"));
            _ordinaryRetainedDependencies = dependencies;
        }

        /// <summary>Computes only ordinary DATA, with identical meaning and complete identity for both private callers.</summary>
        private async Task<SpiritualOrdinaryPressureComparisonData> ComputeOwnedExistingPressureOrdinaryUnderGateAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture)
        {
            _ordinaryOperation ??= new DefaultOperationValidationService().BeginOperation();
            _ordinaryDependencyCapture ??= () =>
            {
                _ordinaryFreshDependencyChecks++;
                return _ordinaryContext?.ReadOrdinaryPressureDependencySnapshot();
            };
            var dependencyBefore = _ordinaryDependencyCapture();
            if (dependencyBefore == null || _ordinaryRetainedDependencies == null ||
                !_ordinaryRetainedDependencies.Matches(dependencyBefore) || _ordinaryGroup == null || _ordinaryEvaluationInput == null)
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "the exact current complete comparison dependencies"));
            var operation = _ordinaryOperation;
            var group = _ordinaryGroup;
            var input = _ordinaryEvaluationInput;
            var dependency = operation.GetStructuralDependency(this, _ordinaryDependencyCapture);
            var identity = _ordinaryInputIdentity ??= operation.GetStructuralInputIdentity(this,
                capture.ReadExistingPressureOrdinaryIdentity(lease, input.RawFingerprint, input.Pointer, group.DependencyData,
                    group.InputRole, group.AffectedSide, group.AffectedActorId));
            var comparison = await operation.GetOrComputeAsync(OrdinaryPressureComputation, identity, [dependency], async _ =>
            {
                var result = await PreparedContinuation.EvaluateOrdinaryPressureUnderGateAsync(this, lease, capture);
                if (result.Comparison == null || result.Issues.Count != 0)
                    throw new OrdinaryPressureRefusal(result.Issues);
                var fresh = await capture.CheckExistingPressureOrdinaryInputsAsync(lease);
                if (fresh.Count != 0) throw new OrdinaryPressureRefusal(fresh);
                await RequireOrdinaryOriginAsync(lease);
                if (_ordinaryContext?.IsCurrent != true)
                    throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "unchanged mechanics after ordinary evaluation"));
                return result.Comparison;
            });
            var closing = await capture.CheckExistingPressureOrdinaryInputsAsync(lease);
            if (closing.Count != 0) throw new OrdinaryPressureRefusal(closing);
            await RequireOrdinaryOriginAsync(lease);
            var dependencyAfter = _ordinaryDependencyCapture?.Invoke();
            if (_ordinaryContext?.IsCurrent != true || dependencyAfter == null || !dependencyBefore.Matches(dependencyAfter))
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "current identical mechanics before detached DATA return"));
            return comparison;
        }

        private async Task RequireOrdinaryOriginAsync(FileSystemManager.CanonicalWriteLease lease)
        {
            var read = PendingTurnSnapshotReader.ReadCurrent(_validator._fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, OptionalPaths));
            if (!read.Success || read.Snapshot == null)
            {
                RevokeCurrent();
                throw new OrdinaryPressureRefusal(read.Issues);
            }
            var request = await _validator._fs.ReadFileBytesAsync(lease, LiveTurnPreparationService.TurnRequestPath);
            if (!SameOriginal(read.Snapshot) || !SameBytes(_originalRequestBytes, request))
            {
                RevokeCurrent();
                throw new OrdinaryPressureRefusal(OrdinaryPressureFailure("spiritual_source_continuation_origin_changed", "the same signed original and exact request"));
            }
        }

        /// <summary>Locks the exact current source before acquiring the draft and resource observation exclusions.</summary>
        /// <param name="lease">Active lease for this capture and source.</param>
        /// <param name="capture">Capture already holding its exclusive gate.</param>
        /// <param name="resources">Capture-owned live resource executor.</param>
        /// <param name="draft">Capture-owned unfinished effect draft.</param>
        /// <param name="interval">Capture-derived latest closed interval.</param>
        /// <param name="ordinal">Capture-derived positive next ordinal.</param>
        /// <returns>Complete detached observation or a guard refusal without owner mutation.</returns>
        internal async Task<SpiritualClosedPressureComparisonProbeState> ReadClosedPressureComparisonProbeStateAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources, EffectAcceptedTurnPlanner.EffectAcceptedDraft draft,
            AcceptedMechanicsPlanner.SpiritualExchangeInterval interval, int ordinal)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _continuationGate.WaitAsync();
            try
            {
                EnsureClosedPressureSourceContourUnderGate(capture, resources, ordinal);
                return draft.ReadClosedPressureComparisonProbeState(lease, capture, this, resources, interval, ordinal);
            }
            finally { _continuationGate.Release(); }
        }

        /// <summary>Checks only retained ownership, closed-source inventory and the unsupported projection/wait exclusions.</summary>
        /// <param name="capture">Exact capture holding its gate.</param>
        /// <param name="resources">Capture-owned resource executor.</param>
        /// <param name="ordinal">Capture-derived positive next ordinal.</param>
        private void EnsureClosedPressureSourceContourUnderGate(SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources, int ordinal)
        {
            if (_continuationGate.CurrentCount != 0 || !IsCurrentOwner || !ReferenceEquals(capture, _prefixCapture) ||
                _awaitingOriginalPrefix || _resourcePrefix == null || !capture.OwnsOriginalPrefix(this, _resourcePrefix) ||
                !capture.OwnsMechanicsOwner(resources, this) || ordinal <= 0 || _checkedExchanges.Count != ordinal ||
                _acceptedExchangeLimit != ordinal || _pending.Count != 0 || _missingActionCostAudit != null ||
                _resourceBatches.Count != ordinal || !_resourceBatches.Keys.SequenceEqual(Enumerable.Range(0, ordinal)) ||
                _admittedPressureProjections.Count != 0 || _pendingPressureProjection != null || _livePressureProjection != null)
                throw new InvalidOperationException("Diagnostics require the exact positive closed source frontier without pending source work or projections.");
        }

        /// <summary>Serializes current retained source facts under all four existing observation exclusions.</summary>
        /// <param name="capture">Exact capture already holding its gate.</param>
        /// <param name="resources">Exact live resource executor already holding its gate.</param>
        /// <param name="ordinal">Actual positive next resource ordinal.</param>
        /// <param name="woundVersion">Current draft version read under its exclusion.</param>
        /// <param name="dependencyCutVersion">Current draft cut version read under its exclusion.</param>
        /// <returns>Detached source JSON, without preparation or comparison cache access.</returns>
        internal string ReadClosedPressureSourceDiagnosticJsonUnderGate(SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources, int ordinal, long woundVersion, long dependencyCutVersion)
        {
            EnsureClosedPressureSourceContourUnderGate(capture, resources, ordinal);
            return ReadPressureSourceDiagnosticJson(woundVersion, dependencyCutVersion, includeClosedState: true);
        }

        /// <summary>Shares pure source serialization while preserving the original ordinal-zero payload.</summary>
        /// <param name="woundVersion">Draft version already read under the applicable exclusion.</param>
        /// <param name="dependencyCutVersion">Draft cut version already read under the applicable exclusion.</param>
        /// <param name="includeClosedState">Adds later-only retained facts for the distinct positive observer.</param>
        /// <returns>Detached JSON only; no owner or cache is changed.</returns>
        private string ReadPressureSourceDiagnosticJson(long woundVersion, long dependencyCutVersion, bool includeClosedState)
        {
            var data = new Dictionary<string, object?>
            {
                ["revision"] = _continuationRevision,
                ["sources"] = _sources.ToArray(), ["checkedExchanges"] = _checkedExchanges.ToArray(),
                ["claimedDice"] = _claimedDice.Order().ToArray(), ["pendingRequirements"] = _pending.ToArray(),
                ["frontiers"] = _frontiers.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                ["checkedByCoordinate"] = _checkedByCoordinate.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                ["candidates"] = _candidate.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                ["candidateConflict"] = _candidateConflict, ["initialFullCandidateConflict"] = _initialFullCandidateConflict,
                ["acceptedExchangeLimit"] = _acceptedExchangeLimit,
                ["expectedActionPoints"] = _expectedActionPoints.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                ["coordinates"] = _coordinates.Order(StringComparer.Ordinal).ToArray(),
                ["members"] = _members.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value),
                ["woundVersion"] = woundVersion, ["dependencyCutVersion"] = dependencyCutVersion
            };
            if (includeClosedState)
            {
                data.Add("before", _before.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value));
                data.Add("originalRequestBytes", _originalRequestBytes == null ? null : Convert.ToBase64String(_originalRequestBytes));
                data.Add("allowMissingActionCostAudit", _allowMissingActionCostAudit);
                data.Add("missingActionCostAudit", _missingActionCostAudit);
                data.Add("awaitingOriginalPrefix", _awaitingOriginalPrefix);
                data.Add("originalConflict", _originalConflict);
                data.Add("soul", _soul);
                data.Add("profileRoot", _profileRoot);
                data.Add("profiles", _profiles.OrderBy(pair => pair.Key, StringComparer.Ordinal).ToDictionary(pair => pair.Key, pair => pair.Value));
                data.Add("profileAliases", _profileAliases.Order(StringComparer.Ordinal).ToArray());
                data.Add("resourceBatches", _resourceBatches.Values.Select(batch => new
                {
                    batch.ConflictId, batch.ExchangeId, batch.Ordinal, batch.Player, batch.Opposition,
                    batch.Sources, batch.Mutations, batch.ExpectedTransitions, batch.PendingSides
                }).ToArray());
                data.Add("mechanicsContextJson", _mechanicsContext?.ReadClosedPressureComparisonDescriptorJson());
                data.Add("exchangeMechanics", _exchangeMechanics.OrderBy(pair => pair.Key, StringComparer.Ordinal)
                    .Select(pair => new { Coordinate = pair.Key, StateJson = pair.Value.ReadClosedPressureComparisonDescriptorJson() }).ToArray());
                data.Add("admittedExchangeMechanics", _admittedExchangeMechanics.ToArray());
            }
            return JsonSerializer.Serialize(data);
        }

        /// <summary>Reads only retained ordinal-zero source DATA while excluding all source continuations.</summary>
        internal async Task<SpiritualPressureActualRefusalObservation> ReadExistingPressureOrdinaryProbeStateAsync(
            FileSystemManager.CanonicalWriteLease lease, SpiritualOriginalTurnCapture capture,
            AcceptedMechanicsPlanner.ResourceExecutionSession resources, int ordinal,
            long woundVersion, long dependencyCutVersion)
        {
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            await _continuationGate.WaitAsync();
            try
            {
                if (!IsCurrentOwner || !ReferenceEquals(capture, _prefixCapture) || _awaitingOriginalPrefix ||
                    _resourcePrefix == null || !capture.OwnsMechanicsOwner(resources, this) || ordinal != 0 ||
                    _sources.Count != 0 || _checkedExchanges.Count != 0 || _claimedDice.Count != 0 ||
                    _pending.Count != 0 || _missingActionCostAudit != null || _resourceBatches.Count != 0 ||
                    _exchangeMechanics.Count != 0 || _admittedExchangeMechanics.Count != 0 ||
                    _admittedPressureProjections.Count != 0 || _pendingPressureProjection != null || _livePressureProjection != null)
                    throw new InvalidOperationException("Diagnostics require an unchanged ordinal-zero source prefix.");
                var resource = resources.ReadExistingPressureOrdinaryDiagnosticSnapshot();
                if (resource.NextOrdinal != ordinal)
                    throw new InvalidOperationException("Source and resource diagnostic ordinals must agree.");
                var json = ReadPressureSourceDiagnosticJson(woundVersion, dependencyCutVersion, includeClosedState: false);
                var allocations = capture.ReadExistingPressureOrdinaryAllocationUnderGate(lease, this);
                var frontier = new SpiritualOrdinaryPressureProbeState(json, resource.ResourceStateJson, resource.ResourceHistoryJson,
                    resource.ResourceTranscriptJson, resource.EffectPrefixJson, ordinal, allocations.Cursor, allocations.JournalJson);
                var projections = new SpiritualPressureProjectionInventory(_pendingPressureProjection == null ? 0 : 1,
                    _livePressureProjection == null ? 0 : 1, _admittedPressureProjections.Count);
                return new(frontier, projections);
            }
            finally { _continuationGate.Release(); }
        }

        internal sealed partial class PreparedContinuation
        {
            /// <summary>Runs shared source and real batch validation inside a discarded probe, never constructing a ticket.</summary>
            internal static async Task<SpiritualOrdinaryPressureComparisonResult> EvaluateOrdinaryPressureUnderGateAsync(
                SpiritualWoundSourceSession owner, FileSystemManager.CanonicalWriteLease lease,
                SpiritualOriginalTurnCapture capture)
            {
                owner._validator._fs.EnsureCanonicalWriteLeaseActive(lease);
                var basis = capture.ReadExistingPressureOrdinaryEvaluationBasisUnderGate(lease, owner);
                if (!owner.IsCurrentOwner || !ReferenceEquals(owner._prefixCapture, capture) ||
                    owner._continuationGate.CurrentCount != 0 || !ReferenceEquals(owner._ordinaryLease, lease) ||
                    owner._ordinarySourceRevision != owner._continuationRevision ||
                    owner._ordinaryEvaluationInput is not { } input ||
                    owner._ordinaryGroup is not { } isolation ||
                    !ReferenceEquals(owner._ordinaryContext, isolation.Context) ||
                    !isolation.Context.OwnsExistingPressureOrdinary(capture, owner) ||
                    input.Ordinal != isolation.Context.Ordinal || input.RawFingerprint != basis.RawFingerprint ||
                    owner._projectionClock is not SpiritualWoundProjectionClock clock)
                    return new(null, OrdinaryPressureFailure("spiritual_wound_mechanics_stale", "the exact privately retained comparison input and live owners"));
                var ordinal = input.Ordinal;
                var frozenRaw = input.FrozenRaw;
                using var probe = clock.BeginConflictValidationProbe(RetainedEvidence(owner));
                var evaluated = await EvaluateProspectiveCoreAsync(owner, lease, capture, isolation);
                if (evaluated.Prospective is not { } next) return new(null, evaluated.Issues);
                if (evaluated.Issues.Count != 0) return new(null, evaluated.Issues);
                if (next._checkedExchanges.Count != ordinal + 1 || next._missingActionCostAudit != null ||
                    next._pending.Count != owner._pending.Count || next._sources.Count != owner._sources.Count + 1 ||
                    !owner._sources.SequenceEqual(next._sources.Take(owner._sources.Count)) ||
                    next._candidate[AfterlifeSpiritualConflictState.StatePath] is not { } rawText ||
                    !JsonNode.DeepEquals(frozenRaw, SpiritualWoundDependentDraftPolicy.ReadStrictRoot(rawText)))
                    return new(null, OrdinaryPressureFailure("spiritual_pressure_ordinary_source_mismatch", "one new independently evaluated ordinary player source"));
                var source = next._sources[^1];
                var ordinary = JsonNode.Parse(source.ExchangeJson)!.AsObject();
                if (source.AffectedSide != isolation.AffectedSide || source.Operation != "pressure" ||
                    source.OriginalSpecialArtJson != null || source.GuaranteedSeverityRank != null ||
                    source.RetraumaWoundId != null || source.StrainPreimageJson != null ||
                    source.Calculation.PreviousStrainRank < 0 ||
                    source.Calculation.PreviousStrainRank >= source.Calculation.NewStrainRank ||
                    source.Calculation.NewStrainRank > 4 ||
                    source.Calculation.NewStrainRank == 4 && isolation.IsGuardian ||
                    isolation.IsGuardian && (source.AffectedActor != "guardian:" + isolation.AffectedActorId ||
                        source.Calculation.PreviousStrainRank != 1 || source.Calculation.NewStrainRank is not (2 or 3) ||
                        source.Calculation.MaximumSeverityRank < 0) ||
                    !JsonNode.DeepEquals(ordinary, JsonNode.Parse(next._checkedExchanges[^1])))
                    return new(null, OrdinaryPressureFailure("spiritual_pressure_ordinary_source_mismatch", "the bounded standard increasing ordinary pressure source"));
                var batches = BuildProspectiveResourceBatches(owner, next, basis.Owners, basis.State);
                if (!batches.IsValid) return new(null, batches.Issues);
                var batch = batches.Exchanges.SingleOrDefault(row => row.Ordinal == ordinal);
                if (batch == null || batch.PendingSides.Count != 0 || batch.ConflictId != source.ConflictId || batch.ExchangeId != source.ExchangeId)
                    return new(null, OrdinaryPressureFailure("spiritual_pressure_ordinary_resource_mismatch", "one complete genuine ordinary resource batch"));
                return new(new(source.ExchangeJson, source.Calculation, batch.ExpectedTransitions, input.RawFingerprint, input.Pointer), []);
            }
        }
    }
}
