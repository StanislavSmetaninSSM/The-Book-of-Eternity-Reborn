using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;
using BookOfEternityClient.Models;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private SpiritualWoundSourceSession? _spiritualWoundSourceSession;

    // Acquisition is owned here. No caller-provided snapshot/frame/capability overload exists.
    internal async Task<SpiritualWoundSourcePreparation> BeginSpiritualWoundSourceSessionAsync(
        FileSystemManager.CanonicalWriteLease lease)
    {
        _fs.EnsureCanonicalWriteLeaseActive(lease);
        InvalidateSpiritualWoundSourceSession();
        var result = await SpiritualWoundSourceSession.AcquireAsync(this, lease);
        if (result.Session is not null)
            _spiritualWoundSourceSession = result.Session;
        return result;
    }

    internal sealed record SpiritualWoundSourcePreparation(
        SpiritualWoundSourceSession? Session,
        IReadOnlyList<ValidationIssue> Issues);

    internal enum SpiritualSourceRequirement
    {
        PriorStartDeclaration,
        PriorEscalationDeclaration,
        TerminalExchangeAudit,
        TerminalExchangePrefix,
        TerminalClosure,
        MissingActionCostAudit,
        AppliedSourceBinding
    }

    internal sealed record SpiritualSourcePendingRequirement(
        SpiritualSourceRequirement Kind,
        string Coordinate,
        string CandidateJson);

    internal sealed partial class SpiritualWoundSourceSession
    {
        internal const string SoulPath = "game_state/meta/soul_state.json";
        private static readonly string[] RequiredPaths =
        [
            SoulPath,
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath
        ];
        private static readonly string[] OptionalPaths =
        [
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeEntityProfileState.StatePath,
            ShiningAbodeState.StatePath,
            AfterlifeSpiritualConflictState.DifficultySettingsPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            WoundCarrierCatalog.PlayerPath,
            WoundCarrierCatalog.NpcPath,
            WoundCarrierCatalog.EnemiesPath,
            WoundCarrierCatalog.AlliesPath
        ];

        private readonly ValidationService _validator;
        private readonly AcceptedTurnProjectionClock? _projectionClock;
        private readonly PendingTurnSnapshotReadAuthority _original;
        private readonly Dictionary<string, string?> _before;
        private readonly Dictionary<string, string?> _candidate;
        private SpiritualOriginalDraftInputs? _coldOriginalInputs;
        /// <summary>
        /// Gets the exact capture-bound cold layer currently retained by this source session.
        /// </summary>
        internal SpiritualOriginalDraftInputs? ColdOriginalInputs => _coldOriginalInputs;

        /// <summary>
        /// Binds the initial cold view through its exact capture owner under the real lease.
        /// </summary>
        /// <param name="lease">
        /// Active canonical lease of the owner filesystem.
        /// </param>
        /// <param name="capture">
        /// Exact cold original capture claiming this source session once.
        /// </param>
        internal void BindColdOriginalInputs(FileSystemManager.CanonicalWriteLease lease,
            SpiritualOriginalTurnCapture capture)
        {
            ArgumentNullException.ThrowIfNull(capture);
            _validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            if (_coldOriginalInputs != null || !IsCurrentOwner)
                throw new InvalidOperationException("Cold source inputs are already bound or this source is not current.");
            var inputs = capture.TakeInitialColdSourceInputs(lease, this);
            if (!inputs.MatchesIdentity(SessionId, RequestId, SnapshotToken, TurnNumber))
                throw new InvalidOperationException("Cold source inputs must match the signed original.");
            _coldOriginalInputs = inputs;
        }
        private readonly List<PreparedSpiritualSource> _sources = [];
        private readonly List<SpiritualSourcePendingRequirement> _pending = [];
        private readonly HashSet<int> _claimedDice = [];
        private readonly List<string> _checkedExchanges = [];
        private readonly HashSet<string> _profileAliases = new(StringComparer.Ordinal);
        private readonly Dictionary<string, int?> _expectedActionPoints = new(StringComparer.Ordinal);
        private readonly HashSet<string> _coordinates = new(StringComparer.Ordinal);
        private readonly Dictionary<string, JsonObject> _profiles = new(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _members = new(StringComparer.Ordinal);
        private JsonObject _soul = new();
        private JsonObject _profileRoot = new();
        private JsonObject _originalConflict = new();
        private JsonObject _candidateConflict = new();
        private JsonObject _initialFullCandidateConflict = new();

        /// <summary>
        /// Retains signed original inputs and the projection clock shared by prospective source views.
        /// </summary>
        /// <param name="validator">
        /// Validator owning this source session.
        /// </param>
        /// <param name="original">
        /// Authenticated original snapshot authority.
        /// </param>
        /// <param name="before">
        /// Original path texts, with absent paths represented by <see langword="null"/>.
        /// </param>
        /// <param name="candidate">
        /// Observed candidate path texts, with absent paths represented by <see langword="null"/>.
        /// </param>
        /// <param name="projectionClock">
        /// Shared projection clock, or <see langword="null"/> for ordinary time reads.
        /// </param>
        private SpiritualWoundSourceSession(
            ValidationService validator,
            PendingTurnSnapshotReadAuthority original,
            Dictionary<string, string?> before,
            Dictionary<string, string?> candidate, AcceptedTurnProjectionClock? projectionClock)
        {
            _validator = validator;
            _original = original;
            _before = before;
            _candidate = candidate;
            _projectionClock = projectionClock;
        }

        internal string SessionId => _original.SessionId;
        internal string RequestId => _original.RequestId;
        internal string SnapshotToken => _original.SnapshotToken;
        internal int TurnNumber => _original.TurnNumber;
        internal string Realm => _original.Realm;
        internal IReadOnlyList<PreparedSpiritualSource> Sources => _sources.AsReadOnly();
        internal IReadOnlyList<SpiritualSourcePendingRequirement> PendingRequirements => _pending.AsReadOnly();
        internal IReadOnlyList<string> CheckedExchanges => _checkedExchanges.AsReadOnly();
        internal bool Owns(PreparedSpiritualSource source) =>
            IsCurrentOwner &&
            _sources.Any(value => ReferenceEquals(value, source));
        internal IReadOnlyList<int> ClaimedDice => Array.AsReadOnly(_claimedDice.Order().ToArray());
        internal IReadOnlyList<string> SelectedPaths => Array.AsReadOnly(
            RequiredPaths.Concat(OptionalPaths).ToArray());
        internal bool HasWork => _checkedExchanges.Count != 0 || _pending.Count != 0;
        // Every selected path is classified by the real reader as covered or signed-absent.
        internal bool HasOriginalBytes(string path) =>
            _original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal);
        internal bool IsOriginalAbsent(string path) =>
            _original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal);
        internal string? ReadOriginal(string path) => _before.TryGetValue(path, out var value)
            ? value : throw new ArgumentOutOfRangeException(nameof(path));
        internal string? ReadCandidate(string path) => _candidate.TryGetValue(path, out var value)
            ? value : throw new ArgumentOutOfRangeException(nameof(path));

        internal JsonObject BuildInputBinding()
        {
            var originals = new JsonObject();
            var candidates = new JsonObject();
            foreach (var path in SelectedPaths)
            {
                originals[path] = _before[path];
                candidates[path] = _candidate[path];
            }
            return new JsonObject
            {
                ["sessionId"] = SessionId, ["requestId"] = RequestId,
                ["snapshotToken"] = SnapshotToken, ["turn"] = TurnNumber,
                ["realm"] = Realm, ["originals"] = originals, ["candidates"] = candidates,
                ["checkedExchanges"] = new JsonArray(_checkedExchanges
                    .Select(value => (JsonNode?)JsonValue.Create(value)).ToArray()),
                ["pendingRequirements"] = new JsonArray(_pending.Select(value => (JsonNode)new JsonObject
                {
                    ["kind"] = value.Kind.ToString(), ["coordinate"] = value.Coordinate,
                    ["candidate"] = value.CandidateJson
                }).ToArray())
            };
        }

        /// <summary>
        /// Acquires the current signed snapshot and prepares its spiritual sources under the requested continuation mode.
        /// </summary>
        /// <param name="validator">
        /// Validator providing the filesystem and signed snapshot reader.
        /// </param>
        /// <param name="lease">
        /// Active canonical write lease for reading the current input.
        /// </param>
        /// <param name="allowMissingActionCostAudit">
        /// Allows retained missing-side audit continuation when <see langword="true"/>; defaults to strict complete audits.
        /// </param>
        /// <param name="awaitOriginalPrefix">
        /// Retains inputs without preparing source capabilities until exact prefix binding when <see langword="true"/>; defaults to immediate preparation.
        /// </param>
        /// <param name="projectionClock">
        /// Shared projection clock, or <see langword="null"/> for ordinary time reads.
        /// </param>
        /// <param name="currentInputs">
        /// Detached current images from the named original draft, or <see langword="null"/>
        /// for physical current-file reads. The signed original remains physical.
        /// </param>
        /// <returns>
        /// A source session or acquisition issues; outside afterlife, an empty result without issues. A deferred session has no prepared sources.
        /// </returns>
        internal static async Task<SpiritualWoundSourcePreparation> AcquireAsync(
            ValidationService validator,
            FileSystemManager.CanonicalWriteLease lease,
            bool allowMissingActionCostAudit = false, bool awaitOriginalPrefix = false,
            AcceptedTurnProjectionClock? projectionClock = null,
            SpiritualOriginalDraftInputs? currentInputs = null)
        {
            validator._fs.EnsureCanonicalWriteLeaseActive(lease);
            var read = PendingTurnSnapshotReader.ReadCurrent(
                validator._fs, lease,
                PendingTurnSnapshotPathSelection.CreateWithObservedOptionalPaths(RequiredPaths, OptionalPaths));
            if (!read.Success || read.Snapshot is null)
                return new(null, read.Issues);
            var original = read.Snapshot;
            if (original.Realm is not ("chaos_sea" or "shining_abode"))
                return new(null, Array.Empty<ValidationIssue>());
            if (currentInputs != null && !currentInputs.MatchesIdentity(original.SessionId,
                    original.RequestId, original.SnapshotToken, original.TurnNumber))
                return new(null, [SourceIssue(SoulPath,
                    "spiritual_original_input_identity_mismatch",
                    "the exact physical signed session, request, snapshot and positive turn")]);

            var before = new Dictionary<string, string?>(StringComparer.Ordinal);
            var candidate = new Dictionary<string, string?>(StringComparer.Ordinal);
            var issues = new List<ValidationIssue>();
            try
            {
                foreach (var path in RequiredPaths.Concat(OptionalPaths))
                {
                    if (original.CoveredLogicalPaths.Contains(path, StringComparer.Ordinal))
                        before[path] = DecodeSourceUtf8(original.ReadRequiredBytes(path));
                    else if (original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal))
                        before[path] = null;
                    else
                        throw new InvalidOperationException("original source path is neither covered nor signed-absent: " + path);
                    var bytes = currentInputs is null
                        ? await validator._fs.ReadFileBytesAsync(lease, path)
                        : currentInputs.ReadImage(path).Bytes;
                    candidate[path] = bytes is null ? null : DecodeSourceUtf8(bytes);
                }
                var session = new SpiritualWoundSourceSession(validator, original, before, candidate, projectionClock)
                {
                    _allowMissingActionCostAudit = allowMissingActionCostAudit,
                    _awaitingOriginalPrefix = awaitOriginalPrefix
                };
                session._originalRequestBytes = await validator._fs.ReadFileBytesAsync(
                    lease, "input/turn_request.json");
                if (!awaitOriginalPrefix)
                    session.Prepare(issues);
                return issues.Any(issue => issue.Severity == IssueSeverity.Error)
                    ? new(null, issues.AsReadOnly())
                    : new(session, issues.AsReadOnly());
            }
            catch (Exception exception) when (exception is System.Text.Json.JsonException or
                DecoderFallbackException or InvalidOperationException or ArgumentException or
                OverflowException or KeyNotFoundException)
            {
                issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                    "spiritual_source_input_invalid", exception.Message));
                return new(null, issues.AsReadOnly());
            }
        }

        private void Prepare(List<ValidationIssue> issues)
        {
            _soul = Parse(SoulPath, original: true, required: true);
            _profileRoot = Parse(AfterlifeEntityProfileState.StatePath, original: true, required: false);
            _originalConflict = Parse(AfterlifeSpiritualConflictState.StatePath, original: true, required: false);
            _initialFullCandidateConflict = ProjectFullCandidateConflict();
            _candidateConflict = ProjectAcceptedExchangeFrontier(
                _initialFullCandidateConflict.DeepClone().AsObject());

            var originalRealm = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(
                ExactString(_soul["currentRealm"]));
            if (!string.Equals(originalRealm, Realm, StringComparison.Ordinal))
            {
                issues.Add(SourceIssue(SoulPath, "spiritual_source_realm_mismatch", "original soul realm"));
                return;
            }

            // Historical coordinates cannot be repurposed as current sources; their old
            // per-turn dice indices are deliberately not reserved in this turn's registry.
            foreach (var historical in (_originalConflict["activeConflict"]?["exchangeLog"] as JsonArray
                ?? new JsonArray()).OfType<JsonObject>())
                RegisterRetainedCoordinate(_originalConflict["activeConflict"]!.AsObject(), historical);
            foreach (var historical in (_originalConflict["recentConflicts"] as JsonArray
                ?? new JsonArray()).OfType<JsonObject>())
                if (historical["terminalExchange"] is JsonObject terminal)
                    RegisterRetainedCoordinate(historical, terminal);

            IndexProfiles(issues);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;

            if (_candidateConflict["activeConflict"] is JsonObject active)
            {
                if (_originalConflict["activeConflict"] is JsonObject prior &&
                    ExactString(prior["conflictId"]) == ExactString(active["conflictId"]))
                {
                    PrepareActive(prior, active, issues);
                }
                else
                {
                    _pending.Add(new(SpiritualSourceRequirement.PriorStartDeclaration,
                        "activeConflict", active.ToJsonString()));
                }
            }

            var oldRecent = (_originalConflict["recentConflicts"] as JsonArray)?
                .OfType<JsonObject>().Select(value => value.ToJsonString()).ToList() ?? [];
            var resolvedIds = new HashSet<string>(StringComparer.Ordinal);
            if (_candidateConflict["recentConflicts"] is JsonArray recent)
            {
                for (var index = 0; index < recent.Count; index++)
                {
                    if (recent[index] is not JsonObject resolution)
                        throw new InvalidOperationException("recentConflicts must contain objects");
                    var oldIndex = oldRecent.FindIndex(value => JsonNode.DeepEquals(
                        JsonNode.Parse(value), resolution));
                    if (oldIndex >= 0)
                    {
                        oldRecent.RemoveAt(oldIndex);
                        continue;
                    }
                    var coordinate = $"recentConflicts[{index}]";
                    var conflictId = ExactString(resolution["conflictId"]) ??
                        throw new InvalidOperationException("exact terminal conflict identity");
                    if (!resolvedIds.Add(conflictId) ||
                        ExactString(_candidateConflict["activeConflict"]?["conflictId"]) == conflictId)
                        throw new InvalidOperationException("one non-active terminal resolution per conflict");
                    if (_originalConflict["activeConflict"] is not JsonObject prior ||
                        ExactString(prior["conflictId"]) != ExactString(resolution["conflictId"]))
                    {
                        _pending.Add(new(SpiritualSourceRequirement.PriorStartDeclaration,
                            coordinate, resolution.ToJsonString()));
                        continue;
                    }
                    if (resolution["terminalExchange"] is not JsonObject exchange)
                    {
                        _pending.Add(new(SpiritualSourceRequirement.TerminalExchangeAudit,
                            coordinate, resolution.ToJsonString()));
                        continue;
                    }
                    PrepareTerminal(prior, resolution, exchange, coordinate, issues);
                }
            }
        }

        private void RegisterRetainedCoordinate(JsonObject conflict, JsonObject exchange)
        {
            var conflictId = ExactString(conflict["conflictId"]);
            var exchangeId = ExactString(exchange["exchangeId"]);
            if (conflictId is not null && exchangeId is not null)
                _coordinates.Add(conflictId + "/" + exchangeId);
        }

        private void PrepareActive(JsonObject prior, JsonObject active, List<ValidationIssue> issues)
        {
            if (!CheckDanger(prior, active, "activeConflict"))
                return;
            IndexMembers(prior, active, issues);
            InitializeResourceSequence(prior);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            var oldLog = prior["exchangeLog"] as JsonArray ?? new JsonArray();
            if (active["exchangeLog"] is not JsonArray log)
                throw new InvalidOperationException("source exchange log is required");
            var retained = new PreTurnConflictPayloadTracker(oldLog.OfType<JsonObject>().ToArray());
            var historicalContext = new AfterlifeConflictDiceContext(
                _original.AcceptedD20EventValues.ToArray(),
                HasValidatedTurnBaseline: true, CurrentTurn: TurnNumber);
            JsonObject previous = prior;
            JsonNode? control = prior["controlState"]?.DeepClone();
            for (var index = 0; index < log.Count; index++)
            {
                var exchange = log[index] as JsonObject ??
                    throw new InvalidOperationException("exchange must be an object");
                if (retained.TryConsume(exchange,
                        allowHistoricalSummaryDrift: HasPriorTurnMarker(exchange, historicalContext)))
                    continue;
                CheckExchange(prior, exchange, previous, control,
                    $"activeConflict.exchangeLog[{index}]", issues,
                    allowMissingAuditLeaf: index == log.Count - 1);
                if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                    return;
                if (_missingActionCostAudit is not null)
                {
                    ValidateIncompleteActiveProjection(active, exchange,
                        _frontiers[ExactString(prior["conflictId"])!], issues);
                    return;
                }
                previous = exchange["after"]!.AsObject();
                control = ResolveNextPriorControlState(control, exchange);
            }
            foreach (var key in new[] { "playerSideStrain", "oppositionSideStrain" })
                if (!JsonNode.DeepEquals(previous[key], active[key]))
                    issues.Add(SourceIssue("activeConflict." + key,
                        "spiritual_source_final_strain_mismatch", "last checked exchange strain"));
        }

        private void PrepareTerminal(
            JsonObject prior, JsonObject resolution, JsonObject exchange,
            string coordinate, List<ValidationIssue> issues)
        {
            if (!CheckDanger(prior, resolution, coordinate))
                return;
            IndexMembers(prior, prior, issues);
            InitializeResourceSequence(prior);
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            var terminalId = ExactString(exchange["exchangeId"]);
            if (_acceptedExchangeLimit == 0 && TerminalPreparation != null)
                return;
            if (terminalId is null ||
                _coordinates.Contains(ExactString(prior["conflictId"]) + "/" + terminalId))
                throw new InvalidOperationException("terminal source duplicates a retained or checked exchange");
            if (new[] { "playerSideStrain", "oppositionSideStrain" }.Any(key =>
                !JsonNode.DeepEquals(prior[key], exchange["before"]?[key])))
            {
                _pending.Add(new(SpiritualSourceRequirement.TerminalExchangePrefix,
                    coordinate, resolution.ToJsonString()));
                return;
            }
            CheckExchange(prior, exchange, prior, prior["controlState"],
                coordinate + ".terminalExchange", issues);
            _pending.Add(new(SpiritualSourceRequirement.TerminalClosure,
                coordinate, resolution.ToJsonString()));
            if (exchange["diceAudit"] is JsonObject dice &&
                !JsonNode.DeepEquals(dice, resolution["diceAudit"]))
                issues.Add(SourceIssue(coordinate + ".diceAudit",
                    "spiritual_source_terminal_dice_mismatch", "same terminal exchange dice"));
        }

        private bool CheckDanger(JsonObject prior, JsonObject current, string coordinate)
        {
            var before = SpiritualConflictDangerPolicy.ReadDeclaration(prior) ??
                throw new InvalidOperationException("invalid original danger declaration");
            var after = SpiritualConflictDangerPolicy.ReadDeclaration(current) ??
                throw new InvalidOperationException("invalid candidate danger declaration");
            if (before == after)
                return true;
            _pending.Add(new(SpiritualSourceRequirement.PriorEscalationDeclaration,
                coordinate, current.ToJsonString()));
            return false;
        }

        private AfterlifeDifficultyDefinition? ReadOriginalDifficulty()
        {
            var path = AfterlifeSpiritualConflictState.DifficultySettingsPath;
            if (_original.AbsentLogicalPaths.Contains(path, StringComparer.Ordinal))
                return null; // Existing no-settings contour forbids a difficulty modifier/audit.
            var settings = Parse(path, original: true, required: true);
            return ReadSpiritualConflictDifficulty(settings.ToJsonString()) ??
                throw new InvalidOperationException("covered original difficulty settings must be readable");
        }

        private void InitializeResourceSequence(JsonObject prior)
        {
            var projection = AfterlifeConflictActionPointProjectionService.Resolve(
                _before[ResourceMaterializationContract.DefinitionsPath],
                SourceResourceBaselineJson, prior);
            if (projection.Projection is null ||
                projection.Issues.Any(issue => issue.Severity == IssueSeverity.Error))
                throw new InvalidOperationException("original both-side action-point projection required");
            _expectedActionPoints["player"] = TryReadIntegralResourceValue(projection.Projection?.Player.Current);
            _expectedActionPoints["opposition"] = TryReadIntegralResourceValue(projection.Projection?.Opposition.Current);
            CaptureInitialSourceFrontier(prior);
        }

        /// <summary>
        /// Validates the next original exchange and retains its sources, dice and causal mechanics only after admission succeeds.
        /// </summary>
        /// <param name="conflict">
        /// Candidate conflict with authenticated original membership.
        /// </param>
        /// <param name="exchange">
        /// Next exchange proposed at the current source frontier.
        /// </param>
        /// <param name="previous">
        /// Expected before-state from the preceding accepted frontier.
        /// </param>
        /// <param name="control">
        /// Prior control state, or <see langword="null"/> when no control state exists.
        /// </param>
        /// <param name="coordinate">
        /// Diagnostic path identifying this exchange.
        /// </param>
        /// <param name="issues">
        /// Receives admission failures without committing the proposed frontier.
        /// </param>
        /// <param name="allowMissingAuditLeaf">
        /// Whether the final active exchange may retain a structurally absent audit as pending evidence.
        /// </param>
        private void CheckExchange(
            JsonObject conflict, JsonObject exchange, JsonObject previous,
            JsonNode? control, string coordinate, List<ValidationIssue> issues,
            bool allowMissingAuditLeaf = false)
        {
            var missingSides = _allowMissingActionCostAudit
                ? ReadTrulyMissingAuditSides(exchange) : Array.Empty<string>();
            if (missingSides.Length != 0 && !allowMissingAuditLeaf)
                throw new InvalidOperationException("only the final active exchange may await missing action-cost audit");
            var incomplete = missingSides.Length != 0;
            var id = ExactString(exchange["exchangeId"]);
            if (id is null || _coordinates.Contains(ExactString(conflict["conflictId"]) + "/" + id))
                throw new InvalidOperationException("exact unique source exchange identity");
            // Existing exchange turn markers remain optional. Newness is proved by exact
            // comparison with the signed original, never a caller's "current" flag.
            foreach (var marker in new[] { "turnNumber", "exchangeAtTurn", "resolvedAtTurn" })
                if (exchange.ContainsKey(marker) &&
                    (!ExactInt(exchange[marker], out var turn) || turn != TurnNumber))
                    throw new InvalidOperationException("current exchange marker differs from original turn");
            if (exchange["before"] is not JsonObject before || exchange["after"] is not JsonObject after)
                throw new InvalidOperationException("complete source before/after snapshots required");
            foreach (var key in new[] { "playerSideStrain", "oppositionSideStrain" })
            {
                if (!JsonNode.DeepEquals(previous[key], before[key]) ||
                    SpiritualWoundOpportunityMath.StrainRank(ExactString(before[key])) < 0 ||
                    SpiritualWoundOpportunityMath.StrainRank(ExactString(after[key])) < 0)
                    throw new InvalidOperationException("continuous valid source strain " + key);
            }

            var actingPlayer = ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject);
            var actingOpposition = ResolveActingOpposition(exchange, conflict);
            if (actingPlayer is null || actingOpposition is null ||
                !_members.TryGetValue(actingPlayer, out var playerMembership) || playerMembership != "player" ||
                !_members.TryGetValue(actingOpposition, out var oppositionMembership) || oppositionMembership != "opposition")
                throw new InvalidOperationException("both current action actors must resolve to their exact original side");
            var projection = AfterlifeConflictActionPointProjectionService.Resolve(
                _before[ResourceMaterializationContract.DefinitionsPath],
                SourceResourceBaselineJson, conflict);
            var diceContext = new AfterlifeConflictDiceContext(
                _original.AcceptedD20EventValues.ToArray(),
                LightIncarnateGrantTurn: SourceOfLightCapstoneState.HasLightIncarnate(_soul)
                    ? SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(
                        _soul, Parse(ShiningAbodeState.StatePath, true, false)) : null,
                PreTurnActiveConflictId: ExactString(conflict["conflictId"]),
                PreTurnActiveControlState: conflict["controlState"]?.DeepClone(),
                PreTurnPlayerResourceCurrent: TryReadIntegralResourceValue(projection.Projection?.Player.Current),
                PreTurnPlayerResourceMaximum: TryReadIntegralResourceValue(projection.Projection?.Player.Maximum),
                PreTurnOppositionResourceCurrent: TryReadIntegralResourceValue(projection.Projection?.Opposition.Current),
                PreTurnOppositionResourceMaximum: TryReadIntegralResourceValue(projection.Projection?.Opposition.Maximum),
                HasValidatedTurnBaseline: true,
                Difficulty: ReadOriginalDifficulty(),
                CurrentTurn: TurnNumber);
            var costContext = CreateSpiritualCostAuthority(_mechanicsContext);

            var exchangeIssues = new List<ValidationIssue>();
            ValidateCurrentWoundContributions(conflict, exchange, previous, coordinate, exchangeIssues);
            _validator.ValidateConflictExchange(exchange, control, conflict,
                coordinate, exchangeIssues, diceContext, costContext, isPreTurnExchange: false,
                combatConditionIds: ValidateCombatConditions(conflict["combatConditions"],
                    coordinate + ".combatConditions", exchangeIssues));
            // Only this precise diagnostic for a structurally absent expected side is
            // pending evidence. Null/scalar/empty audits and unrelated failures survive.
            issues.AddRange(exchangeIssues.Where(issue => !incomplete ||
                !IsExpectedMissingAuditIssue(issue, coordinate, missingSides)));
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error) &&
                (_mechanicsContext is null ||
                 !SpiritualOriginalTurnCapture.IsCorrectableDependentConflictFailure(issues)))
                return;

            // Cost-formula failures must not mask the other side's owned chronological balance.
            // Gather both sides without advancing expected balances until every check succeeds.
            var nextActionPoints = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var side in new[] { "player", "opposition" })
            {
                if (!missingSides.Contains(side, StringComparer.Ordinal) &&
                    ((side == "player" ? ExchangeExpectsPlayerActionCostAudit(exchange)
                    : ExchangeExpectsOppositionActionCostAudit(exchange)) || HasConditionalForceCostAudit(exchange, side)))
                {
                    var expectedBefore = _expectedActionPoints[side];
                    if (_mechanicsContext != null)
                    {
                        var actualBefore = _mechanicsContext.ReadActionPointBefore(
                            ExactString(conflict["conflictId"])!, _checkedExchanges.Count);
                        expectedBefore = TryReadIntegralResourceValue(
                            side == "player" ? actualBefore.Player.Current : actualBefore.Opposition.Current)
                            ?? throw new InvalidOperationException("Integral actual action points are required.");
                    }
                    ValidateCurrentActionCostSequence(exchange, side, expectedBefore,
                        coordinate, issues, out var next);
                    if (next.HasValue && !incomplete)
                        nextActionPoints[side] = next.Value;
                }
            }
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            foreach (var next in nextActionPoints)
                _expectedActionPoints[next.Key] = next.Value;
            var prospectiveDice = new HashSet<int>(_claimedDice);
            if (exchange["diceAudit"] is JsonObject audit)
            {
                ValidateWideTotals(audit);
                foreach (var row in audit["diceUsed"]!.AsArray().OfType<JsonObject>())
                {
                    if (!ExactInt(row["sourceIndex"], out var index) ||
                        index < 0 || index >= _original.AcceptedD20EventValues.Count ||
                        !ExactInt(row["value"], out var value) ||
                        _original.AcceptedD20EventValues[index] != value ||
                        !prospectiveDice.Add(index))
                        throw new InvalidOperationException("original die used outside its unique turn-wide claim");
                }
            }

            foreach (var side in new[] { "player", "opposition" })
            {
                var key = side + "SideStrain";
                var previousRank = SpiritualWoundOpportunityMath.StrainRank(ExactString(before[key]));
                var nextRank = SpiritualWoundOpportunityMath.StrainRank(ExactString(after[key]));
                if (nextRank <= previousRank)
                    continue;
                PrepareHarm(conflict, exchange, side, coordinate, costContext, issues,
                    prepareSource: !incomplete);
            }
            if (issues.Any(issue => issue.Severity == IssueSeverity.Error))
                return;
            if (incomplete)
            {
                RetainMissingActionCostAudit(conflict, exchange, coordinate, missingSides);
                return;
            }
            _coordinates.Add(ExactString(conflict["conflictId"]) + "/" + id);
            _claimedDice.UnionWith(prospectiveDice);
            RetainAdmittedExchangeMechanics(conflict, exchange);
            _checkedExchanges.Add(exchange.ToJsonString());
            if (_mechanicsContext != null)
                _exchangeMechanics.Add(ExactString(conflict["conflictId"]) + "/" + id, _mechanicsContext);
            CaptureCheckedSourceFrontier(conflict, exchange, control);
        }

        private static void ValidateWideTotals(JsonObject audit)
        {
            var entries = audit["diceUsed"]!.AsArray().OfType<JsonObject>().ToArray();
            foreach (var side in new[] { "player", "opposition" })
            {
                var rolls = entries.Where(row => side == "player"
                    ? ConflictTokenEquals(ExactString(row["side"]), "player", "playerSide", "soul")
                    : ConflictTokenEquals(ExactString(row["side"]), "opposition", "oppositionSide", "guardian"))
                    .ToArray();
                var selected = rolls.Length == 1 ? rolls[0] :
                    rolls.Single(row => ConflictTokenEquals(ExactString(row["selection"]), "selected"));
                long expected = selected["value"]!.GetValue<int>();
                foreach (var item in audit["modifierBreakdown"]![side]!.AsArray())
                    expected = checked(expected + item!["value"]!.GetValue<int>());
                if (!ExactInt(audit[side + "Total"], out var total) || expected != total)
                    throw new InvalidOperationException("wide source total differs from selected die and modifiers");
            }
        }

        private void PrepareHarm(
            JsonObject conflict, JsonObject exchange, string affectedSide,
            string coordinate, AfterlifeActionCostAuthorityContext costContext,
            List<ValidationIssue> issues, bool prepareSource = true)
        {
            var actingSide = affectedSide == "player" ? "opposition" : "player";
            var operation = actingSide == "player"
                ? ExactString(exchange["operationType"])
                : ExactString(exchange["matchupAudit"]?["oppositionOperation"]) ??
                    ExactString(exchange["incomingAction"]?["finalOperationType"]) ??
                    ExactString(exchange["incomingAction"]?["operationType"]);
            var actorKey = actingSide == "player"
                ? ActorKey(conflict["playerSide"]?["leadContestant"] as JsonObject)
                : ResolveActingOpposition(exchange, conflict);
            var action = actingSide == "player" ? exchange :
                exchange["incomingAction"] as JsonObject ?? exchange;
            var explicitTarget = action["spiritualWoundTarget"] as JsonObject;
            if (action.ContainsKey("spiritualWoundTarget") && explicitTarget is null)
                throw new InvalidOperationException("spiritualWoundTarget must be an exact source action object");
            var targetKey = ActorKey(explicitTarget ??
                conflict[affectedSide + "Side"]?["leadContestant"] as JsonObject);
            if (operation is null || actorKey is null || targetKey is null ||
                !_members.TryGetValue(actorKey, out var actorSide) || actorSide != actingSide ||
                !_members.TryGetValue(targetKey, out var targetSide) || targetSide != affectedSide)
                throw new InvalidOperationException("exact source actor and affected actor membership");

            operation = AfterlifeEntityProfileState.StandardArtIds.FirstOrDefault(art =>
                string.Equals(art, operation, StringComparison.OrdinalIgnoreCase)) ?? operation;
            if (!AfterlifeEntityProfileState.StandardArtIds.Contains(operation) ||
                operation == "champion_coordination")
            {
                // A passive/authority operation is not an invented standard art.
                // C/D must bind the actual applied effect or operation source.
                if (prepareSource)
                    _pending.Add(new(SpiritualSourceRequirement.AppliedSourceBinding,
                        coordinate + "." + affectedSide, exchange.ToJsonString()));
                return;
            }
            var special = actingSide == "player"
                ? ResolvePlayerSpecialArtAudit(exchange, operation)
                : ResolveOppositionSpecialArtAudit(exchange, operation);
            var source = special is null ? null : ResolveSpecialArtAuthority(special, costContext);
            if (special is not null && source is null)
                throw new InvalidOperationException("special source must resolve from original owner arts");
            if (source is not null &&
                (ExactString(source["artId"]) != ExactString(special!["artId"]) ||
                 BuildActorAuthorityKey(ExactString(source["ownerActorType"]),
                     ExactString(source["ownerActorId"])) != actorKey))
                throw new InvalidOperationException("exact original special art source/actor binding");
            var rawTier = source is null ? RawTier(actorKey, operation) : ReadTier(source["tier"]);
            var resilience = RawTier(targetKey, AfterlifeSpiritualConflictState.SpiritualResilienceArtId);
            var envelope = new SpiritualWoundSourceEnvelope(4, null);
            if (source is not null)
            {
                if (!SpiritualWoundSourceEnvelope.TryRead(source, out var parsed, out var error))
                    throw new InvalidOperationException(error);
                envelope = parsed!;
            }
            if (exchange["diceAudit"] is not JsonObject dice)
            {
                // The real checker has already accepted this explicit voluntary non-contest.
                // Do not manufacture an opposed margin or exclude its lawful incoming harm.
                if (prepareSource)
                    _pending.Add(new(SpiritualSourceRequirement.AppliedSourceBinding,
                        coordinate + "." + affectedSide, exchange.ToJsonString()));
                return;
            }
            if (!ExactInt(dice["playerTotal"], out var player) ||
                !ExactInt(dice["oppositionTotal"], out var opposition))
                throw new InvalidOperationException("validated opposed audit requires exact side totals");
            if (!ExactInt(dice["margin"], out var auditedMargin) ||
                (long)player - opposition != auditedMargin)
                throw new InvalidOperationException("wide source margin must equal actual side totals");
            var margin = actingSide == "player" ? (long)player - opposition : (long)opposition - player;
            var beforeStrain = ExactString(exchange["before"]![affectedSide + "SideStrain"])!;
            var afterStrain = ExactString(exchange["after"]![affectedSide + "SideStrain"])!;
            var mode = SpiritualConflictDangerPolicy.ReadDeclaration(conflict)!;
            var calculation = SpiritualWoundOpportunityMath.Calculate(new(
                margin, rawTier, resilience, beforeStrain, afterStrain, mode, envelope.MaximumSeverityRank))
                ?? throw new InvalidOperationException("source opportunity arithmetic overflow/invalid input");
            var retrauma = ResolveRetrauma(explicitTarget, targetKey, out var retraumaWoundJson);
            if (!prepareSource)
                return;
            _sources.Add(new PreparedSpiritualSource(
                coordinate, ExactString(conflict["conflictId"])!, ExactString(exchange["exchangeId"])!,
                affectedSide, actorKey, targetKey, operation,
                source?.ToJsonString(), exchange.ToJsonString(), calculation,
                envelope.GuaranteedSeverityRank, retrauma, retraumaWoundJson));
        }

        private void IndexProfiles(List<ValidationIssue> issues)
        {
            if (_profileRoot[AfterlifeEntityProfileState.ProfilesProperty] is not JsonArray profiles)
                return;
            foreach (var item in profiles)
            {
                if (item is not JsonObject profile ||
                    !AfterlifeEntityProfileState.TryResolveEffectTarget(profile, out _))
                    throw new InvalidOperationException("exact persistent source profile identity");
                var key = ActorKey(profile) ?? throw new InvalidOperationException("profile identity");
                if (!_profileAliases.Add(MortalLocationIdentityState.BuildConfusableKey(key)) ||
                    !_profiles.TryAdd(key, profile))
                    throw new InvalidOperationException("duplicate persistent source profile identity");
                if (profile["specialArts"] is not JsonArray arts)
                    throw new InvalidOperationException("source profile specialArts array required");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var art in arts)
                {
                    if (art is not JsonObject value || ExactString(value["artId"]) is not string id || !ids.Add(MortalLocationIdentityState.BuildConfusableKey(id)))
                        throw new InvalidOperationException("exact unique original special art");
                    if (!SpiritualWoundSourceEnvelope.TryRead(value, out _, out var error))
                        throw new InvalidOperationException(error);
                }
            }
        }

        private void IndexMembers(JsonObject prior, JsonObject current, List<ValidationIssue> issues)
        {
            _members.Clear();
            var memberAliases = new HashSet<string>(StringComparer.Ordinal);
            foreach (var side in new[] { "player", "opposition" })
            {
                var key = side + "Side";
                if (prior[key] is not JsonObject value || current[key] is not JsonObject candidateSide ||
                    ActorKey(value["leadContestant"] as JsonObject) !=
                        ActorKey(candidateSide["leadContestant"] as JsonObject))
                    throw new InvalidOperationException("unchanged exact conflict lead");
                if ((value.ContainsKey("supporters") && value["supporters"] is not JsonArray) ||
                    (candidateSide.ContainsKey("supporters") && candidateSide["supporters"] is not JsonArray))
                    throw new InvalidOperationException("source supporters must be an array");
                var priorSupporters = value["supporters"] as JsonArray ?? new JsonArray();
                var candidateSupporters = candidateSide["supporters"] as JsonArray ?? new JsonArray();
                if (priorSupporters.Count != candidateSupporters.Count ||
                    !priorSupporters.Select(item => ActorKey(item as JsonObject))
                        .OrderBy(actor => actor, StringComparer.Ordinal)
                        .SequenceEqual(candidateSupporters.Select(item => ActorKey(item as JsonObject))
                            .OrderBy(actor => actor, StringComparer.Ordinal), StringComparer.Ordinal))
                    throw new InvalidOperationException("unchanged exact conflict supporter membership");
                var members = new List<JsonObject>
                {
                    value["leadContestant"] as JsonObject ??
                        throw new InvalidOperationException("source side lead required")
                };
                if (value["supporters"] is JsonArray supporters)
                    members.AddRange(supporters.Select(item => item as JsonObject ??
                        throw new InvalidOperationException("source supporter must be an object")));
                foreach (var member in members)
                {
                    var actor = ActorKey(member) ?? throw new InvalidOperationException("source actor identity");
                    if (!memberAliases.Add(MortalLocationIdentityState.BuildConfusableKey(actor)) ||
                        !_members.TryAdd(actor, side))
                        throw new InvalidOperationException("actor belongs to more than one source position");
                    if (actor != "player_soul:player_soul")
                    {
                        if (!_profiles.TryGetValue(actor, out var profile) ||
                            AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(
                                ExactString(profile["realm"])) != Realm)
                            throw new InvalidOperationException("source actor lacks exact original realm profile");
                    }
                }
            }
        }

        /// <summary>
        /// Resolves an explicit re-trauma target against owned current state or the standalone signed baseline.
        /// </summary>
        /// <param name="target">
        /// Optional source-action target; absence of a re-trauma reference produces no target.
        /// </param>
        /// <param name="actor">
        /// Exact affected actor coordinate already resolved from signed membership.
        /// </param>
        /// <param name="woundJson">
        /// Receives immutable canonical wound facts for the retained source, or null when no target is selected.
        /// </param>
        /// <returns>
        /// The validated active wound identity, or null without a re-trauma reference.
        /// </returns>
        private string? ResolveRetrauma(JsonObject? target, string actor, out string? woundJson)
        {
            woundJson = null;
            if (target is null)
                return null;
            var allowed = new HashSet<string>(["actorType", "actorId", "retraumaWoundRef"], StringComparer.Ordinal);
            if (target.Any(pair => !allowed.Contains(pair.Key)))
                throw new InvalidOperationException("closed spiritualWoundTarget source action fields");
            if (!target.ContainsKey("retraumaWoundRef"))
                return null;
            var woundId = ExactString(target["retraumaWoundRef"]) ??
                throw new InvalidOperationException("explicit exact old woundId required");
            WoundOperationBeforeData? current = null;
            if (_mechanicsContext != null)
            {
                var failures = new List<ValidationIssue>();
                current = _mechanicsContext.ReadCurrentWoundView(failures);
                if (current == null || failures.Count != 0)
                    throw new InvalidOperationException("exact current owned re-trauma wound view required");
            }
            var wound = ReadRetraumaWound(woundId, actor, current);
            woundJson = WoundMaterializationContract.SerializeCanonical(wound);
            return woundId;
        }

        /// <summary>
        /// Validates a re-trauma wound against complete carrier, identity and history agreement.
        /// </summary>
        /// <param name="woundId">
        /// Exact requested wound identity.
        /// </param>
        /// <param name="actor">
        /// Exact affected actor coordinate from signed membership.
        /// </param>
        /// <param name="current">
        /// Owned current snapshots, or null to use the standalone signed originals.
        /// </param>
        /// <returns>
        /// The matching active wound; invalid state or ownership throws.
        /// </returns>
        private WoundMaterializationEnvelope ReadRetraumaWound(string woundId, string actor,
            WoundOperationBeforeData? current)
        {
            if (current != null && (current.WoundCarriers == null || current.WoundIdentity == null || current.WoundHistory == null))
                throw new InvalidOperationException("complete current wound carrier, identity and history snapshots required");
            var catalog = WoundCarrierCatalog.Build(current != null ? current.WoundCarriers! : new(
                OptionalRoot(WoundCarrierCatalog.PlayerPath),
                OptionalRoot(WoundCarrierCatalog.NpcPath),
                OptionalRoot(WoundCarrierCatalog.EnemiesPath),
                OptionalRoot(WoundCarrierCatalog.AlliesPath),
                OptionalRoot(WoundCarrierCatalog.AfterlifeProfilesPath)));
            if (catalog.Issues.Count != 0 || !catalog.TryResolveOne(woundId, out var occurrence) ||
                occurrence.Wound.Lifecycle != "active" ||
                occurrence.Coordinate.Realm != Realm)
                throw new InvalidOperationException("exact active re-trauma wound in the authoritative view");
            var ownerId = actor[(actor.IndexOf(':') + 1)..];
            var kind = actor[..actor.IndexOf(':')] switch
            {
                "player_soul" => "player_soul",
                "guardian" => "guardian",
                "resident" or "shining_resident" => "resident",
                "radiant_actor" => "radiant_actor",
                "shining_faction_head" or "saref_agent" or "system_actor" or
                    "custom_afterlife_actor" => "afterlife_actor",
                _ => null
            };
            if (occurrence.Coordinate.OwnerId != ownerId ||
                occurrence.Coordinate.OwnerKind != kind ||
                occurrence.Coordinate.CarrierPath != WoundCarrierCatalog.AfterlifeProfilesPath)
                throw new InvalidOperationException("re-trauma target owner mismatch");
            var identity = WoundIdentityState.Parse(current != null ? current.WoundIdentity!.ToJsonString() : _before[WoundIdentityState.StatePath],
                WoundIdentityState.StatePath);
            if (!identity.IsValid || !identity.State!.TryGetEntry(woundId, out var entry) ||
                entry.Status != "active" || entry.OwnerId != occurrence.Coordinate.OwnerId ||
                entry.OwnerKind != occurrence.Coordinate.OwnerKind ||
                entry.CarrierPath != occurrence.Coordinate.CarrierPath || entry.Realm != Realm ||
                entry.SemanticFingerprint != WoundIdentityState.ComputeSemanticFingerprint(occurrence.Wound))
                throw new InvalidOperationException("original re-trauma identity/carrier agreement");
            var history = WoundHistoryState.Parse(current != null ? current.WoundHistory!.ToJsonString() : _before[WoundHistoryState.HistoryPath],
                WoundHistoryState.HistoryPath);
            if (!history.IsValid ||
                history.State!.ValidateAgreement(identity.State!, catalog).Count != 0)
                throw new InvalidOperationException("original re-trauma history/identity/carrier agreement");
            return occurrence.Wound;
        }

        /// <summary>
        /// Revalidates a retained source's historical target against a fresh owned wound view.
        /// </summary>
        /// <param name="source">
        /// Exact source registered by this session, including its frozen target facts.
        /// </param>
        /// <param name="current">
        /// Fresh owner-selected snapshots read under the original capture gate.
        /// </param>
        /// <param name="issues">
        /// Receives unowned-source, malformed-state or changed-target issues.
        /// </param>
        /// <returns>
        /// The unchanged active target, or null when validation fails.
        /// </returns>
        internal WoundMaterializationEnvelope? RevalidateRetraumaTarget(PreparedSpiritualSource source,
            WoundOperationBeforeData current, List<ValidationIssue> issues)
        {
            try
            {
                if (!Owns(source) || source.RetraumaWoundId == null || source.RetraumaWoundJson == null)
                    throw new InvalidOperationException("exact registered source with retained re-trauma facts required");
                var wound = ReadRetraumaWound(source.RetraumaWoundId, source.AffectedActor, current);
                if (WoundMaterializationContract.SerializeCanonical(wound) != source.RetraumaWoundJson)
                    throw new InvalidOperationException("the retained re-trauma target changed after source acceptance");
                return wound;
            }
            catch (InvalidOperationException)
            {
                issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                    "spiritual_wound_retrauma_target_stale", "the exact unchanged active target retained by this source"));
                return null;
            }
        }

        /// <summary>
        /// Reads the exact active conflict wound from the current owned carrier, identity and history view.
        /// </summary>
        /// <param name="source">
        /// Registered source whose affected actor owns the wound.
        /// </param>
        /// <param name="woundId">
        /// Identity supplied by the capture's actual earlier conflict-wound insertion.
        /// </param>
        /// <param name="current">
        /// Current owner-selected wound state after earlier source insertions.
        /// </param>
        /// <param name="issues">
        /// Receives stale or inconsistent owner-view failures.
        /// </param>
        /// <returns>
        /// The current active wound, or <see langword="null"/> when its owner view fails validation.
        /// </returns>
        internal WoundMaterializationEnvelope? RevalidateSameConflictTarget(PreparedSpiritualSource source,
            string woundId, WoundOperationBeforeData current, List<ValidationIssue> issues)
        {
            try
            {
                if (!Owns(source) || source.RetraumaWoundId != null)
                    throw new InvalidOperationException("exact same-conflict source without explicit older target required");
                return ReadRetraumaWound(woundId, source.AffectedActor, current);
            }
            catch (InvalidOperationException)
            {
                issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                    "spiritual_wound_same_conflict_target_stale",
                    "the exact active conflict wound in the current owned carrier, identity and history view"));
                return null;
            }
        }

        private JsonObject? OptionalRoot(string path) =>
            _before[path] is null ? null : Parse(path, true, true);

        private int RawTier(string actor, string art)
        {
            JsonObject? tiers;
            if (actor == "player_soul:player_soul")
                tiers = _soul[AfterlifeSpiritualConflictState.SoulStateProfileProperty]?["artTiers"] as JsonObject;
            else if (_profiles.TryGetValue(actor, out var profile))
                tiers = profile["standardArts"] as JsonObject;
            else
                throw new InvalidOperationException("missing original actor art authority");
            if (tiers is null || tiers.Count(pair =>
                MortalLocationIdentityState.BuildConfusableKey(pair.Key) ==
                MortalLocationIdentityState.BuildConfusableKey(art)) != 1)
                throw new InvalidOperationException("one exact original art tier authority");
            return ReadTier(tiers[art]);
        }

        private static int ReadTier(JsonNode? node) =>
            ExactInt(node, out var tier) && tier is >= 0 and <= 5
                ? tier : throw new InvalidOperationException("raw spiritual tier integer 0..5 required");

        /// <summary>
        /// Decodes source JSON as strict UTF-8 while accepting one leading UTF-8 preamble from canonical text writers.
        /// </summary>
        /// <param name="bytes">
        /// Exact signed or current bytes, retained unchanged by the caller.
        /// </param>
        /// <returns>
        /// Decoded text without the optional leading preamble; malformed UTF-8 throws a decoder exception.
        /// </returns>
        private static string DecodeSourceUtf8(byte[] bytes)
        {
            var offset = bytes.AsSpan().StartsWith(Encoding.UTF8.GetPreamble()) ? 3 : 0;
            return new UTF8Encoding(false, true).GetString(bytes, offset, bytes.Length - offset);
        }

        private JsonObject Parse(string path, bool original, bool required)
        {
            var text = (original ? _before : _candidate)[path];
            if (text is null && !required)
                return new JsonObject();
            if (text is null || !TryParseStrictResourceObject(text, out var root))
                throw new InvalidOperationException("strict original/candidate object required: " + path);
            return root;
        }

        private static string? ResolveActingOpposition(JsonObject exchange, JsonObject conflict)
        {
            if (exchange["incomingAction"] is JsonObject action &&
                new[] { "actorType", "ownerActorType", "actorId", "actorRef",
                    "ownerActorId", "guardianId", "id" }.Any(action.ContainsKey))
                return ActorKey(action);
            return ActorKey(conflict["oppositionSide"]?["leadContestant"] as JsonObject);
        }

        private static string? ActorKey(JsonObject? actor)
        {
            if (actor is null)
                return null;
            var type = ExactIdentityField(actor, ["actorType", "ownerActorType"]);
            var id = ExactIdentityField(actor, ["actorId", "actorRef", "ownerActorId", "guardianId", "id"]);
            if (type is null || id is null)
                return null;
            if (ConflictTokenEquals(type, "player", "soul", "player_soul"))
                return id == "player_soul" ? "player_soul:player_soul" : null;
            return id == "player_soul" ? null : BuildActorAuthorityKey(type, id);
        }

        private static string? ExactIdentityField(JsonObject actor, IReadOnlyList<string> names)
        {
            string? result = null;
            foreach (var name in names)
            {
                if (!actor.ContainsKey(name))
                    continue;
                var value = ExactString(actor[name]);
                if (value is null || (result is not null && value != result))
                    throw new InvalidOperationException("inexact or contradictory actor identity fields");
                result = value;
            }
            return result;
        }

        private static string? ExactString(JsonNode? node) =>
            node is JsonValue value && value.TryGetValue<string>(out var text) &&
            !string.IsNullOrWhiteSpace(text) && text == text.Trim() ? text : null;

        private static bool ExactInt(JsonNode? node, out int result)
        {
            result = 0;
            return node is JsonValue value && value.TryGetValue<int>(out result);
        }
    }

    /// <summary>
    /// Retains checked exchange facts, including an immutable historical re-trauma target when present.
    /// Data equality does not substitute for the source session's exact object ownership.
    /// </summary>
    /// <param name="Coordinate">
    /// Diagnostic coordinate of the checked exchange.
    /// </param>
    /// <param name="ConflictId">
    /// Exact accepted conflict identity.
    /// </param>
    /// <param name="ExchangeId">
    /// Exact accepted exchange identity.
    /// </param>
    /// <param name="AffectedSide">
    /// Side whose strain increased.
    /// </param>
    /// <param name="ActingActor">
    /// Signed actor coordinate responsible for the harmful action.
    /// </param>
    /// <param name="AffectedActor">
    /// Signed persistent actor coordinate receiving the harm.
    /// </param>
    /// <param name="Operation">
    /// Validated harmful operation.
    /// </param>
    /// <param name="OriginalSpecialArtJson">
    /// Original special-art definition, or null for a standard art.
    /// </param>
    /// <param name="ExchangeJson">
    /// Retained exchange evidence used for the source calculation.
    /// </param>
    /// <param name="Calculation">
    /// Deterministic strain and severity calculation from the validated action.
    /// </param>
    /// <param name="GuaranteedSeverityRank">
    /// Original source's guaranteed rank, or null without a declared guarantee.
    /// </param>
    /// <param name="RetraumaWoundId">
    /// Explicit validated active wound identity, or null for a new-wound opportunity.
    /// </param>
    /// <param name="RetraumaWoundJson">
    /// Canonical historical target facts, or null without re-trauma; these facts require fresh target validation before use.
    /// </param>
    internal sealed record PreparedSpiritualSource(
        string Coordinate,
        string ConflictId,
        string ExchangeId,
        string AffectedSide,
        string ActingActor,
        string AffectedActor,
        string Operation,
        string? OriginalSpecialArtJson,
        string ExchangeJson,
        SpiritualWoundCalculation Calculation,
        int? GuaranteedSeverityRank,
        string? RetraumaWoundId,
        string? RetraumaWoundJson = null);

    internal static void ValidateSpiritualWoundSourceActionShape(
        JsonObject exchange, string context, List<ValidationIssue> issues)
    {
        var actions = new List<(JsonObject? Action, string Path)>
        {
            (exchange, context),
            (exchange["incomingAction"] as JsonObject, context + ".incomingAction"),
            (exchange["specialArtAudit"] as JsonObject, context + ".specialArtAudit")
        };
        if (exchange["specialArtAudits"] is JsonArray audits)
            for (var index = 0; index < audits.Count; index++)
                actions.Add((audits[index] as JsonObject, context + $".specialArtAudits[{index}]"));
        foreach (var (action, path) in actions)
        {
            if (action is null)
                continue;
            foreach (var field in new[]
            {
                "traumaPressure", "sourceSeverityCap", "maximumSeverityRank",
                "guaranteedSeverityRank", SpiritualWoundSourceEnvelope.Property
            })
                if (action.Any(pair => MortalLocationIdentityState.BuildConfusableKey(pair.Key) ==
                        MortalLocationIdentityState.BuildConfusableKey(field)))
                    issues.Add(SourceIssue(path + "." + field,
                        "spiritual_source_computed_field_forbidden", "caller-authored source envelope"));
            var targetKeys = action.Select(pair => pair.Key).Where(key =>
                MortalLocationIdentityState.BuildConfusableKey(key) ==
                MortalLocationIdentityState.BuildConfusableKey("spiritualWoundTarget")).ToArray();
            if (targetKeys.Length == 0)
                continue;
            if (targetKeys.Length != 1 || targetKeys[0] != "spiritualWoundTarget")
            {
                issues.Add(SourceIssue(path + ".spiritualWoundTarget",
                    "spiritual_source_target_invalid", "one exact source-action target property"));
                continue;
            }
            if ((!ReferenceEquals(action, exchange) &&
                 !ReferenceEquals(action, exchange["incomingAction"])) ||
                action["spiritualWoundTarget"] is not JsonObject target ||
                target.Any(pair => pair.Key is not ("actorType" or "actorId" or "retraumaWoundRef")) ||
                target["actorType"] is not JsonValue type || !type.TryGetValue<string>(out var actorType) ||
                string.IsNullOrWhiteSpace(actorType) || actorType != actorType.Trim() ||
                target["actorId"] is not JsonValue id || !id.TryGetValue<string>(out var actorId) ||
                string.IsNullOrWhiteSpace(actorId) || actorId != actorId.Trim() ||
                (target.ContainsKey("retraumaWoundRef") &&
                 (target["retraumaWoundRef"] is not JsonValue wound ||
                  !wound.TryGetValue<string>(out var woundId) || string.IsNullOrWhiteSpace(woundId) ||
                  woundId != woundId.Trim())))
                issues.Add(SourceIssue(path + ".spiritualWoundTarget",
                    "spiritual_source_target_invalid", "closed exact source-action target"));
        }
    }

    private static ValidationIssue SourceIssue(string path, string code, string actual) =>
        new(path, IssueSeverity.Error,
            "Не удалось доказать исходное духовное действие.",
            code: code, section: "AfterlifeSpiritualConflict",
            expected: "exact original-turn spiritual source authority", actual: actual);
}
