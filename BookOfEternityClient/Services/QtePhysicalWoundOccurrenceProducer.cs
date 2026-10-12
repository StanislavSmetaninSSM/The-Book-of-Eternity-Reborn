using System.Text;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

/// <summary>One registered, write-free resolver for the actual selected QTE health result.</summary>
internal sealed class QtePhysicalWoundOccurrenceProducer : IMortalWoundOccurrenceProducerDraft
{
    private readonly QtePhysicalInjurySource _source;
    private readonly QteTerminalResourceSelection _selection;
    private readonly ResourceDefinitionCatalog _definitions;
    private readonly AcceptedMechanicsResourcePlanningResult _resources;
    private readonly Lazy<MortalWoundOccurrenceProducerProjectionResult> _projection;

    private QtePhysicalWoundOccurrenceProducer(QtePhysicalInjurySource source,
        QteTerminalResourceSelection selection, AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction completed)
    {
        if (source.SessionId != completed.Input.SessionId || source.RequestId != completed.Input.RequestId ||
            source.SnapshotToken != completed.Input.SnapshotToken || source.Turn != completed.Input.Turn ||
            completed.Input.Realm != "mortal_world")
            throw new InvalidDataException("qte_physical_injury_completed_source_mismatch: original source differs from the issued owner.");
        _source = source;
        _selection = selection;
        _definitions = completed.Definitions;
        _resources = completed.Resources;
        _projection = new(() => ProjectCore(_resources, _resources.EffectBoundaryTranscript!));
    }

    internal static QtePhysicalWoundOccurrenceProducer Create(QtePhysicalInjurySource source,
        QteTerminalResourceSelection selection, AcceptedMechanicsPlanner.CompletedOrdinaryMechanicsReduction completed) =>
        new(source, selection, completed);
    internal MortalWoundOccurrenceProducerProjectionResult FinalizedProjection => _projection.Value;
    internal QtePhysicalInjurySource OriginalSource => _source;

    public string AdapterKind => "qte";
    internal string ProducerOperationKey => "qte_health_damage_" + Seal(
        _source.SessionId, _source.RequestId, _source.SnapshotToken, _source.Turn.ToString(System.Globalization.CultureInfo.InvariantCulture),
        _selection.QteId, _selection.ChapterId, _selection.ActionId, _selection.Grade,
        _selection.OutcomeId, _selection.OutcomeOrdinal.ToString(System.Globalization.CultureInfo.InvariantCulture),
        (_selection.PhysicalInjury?.ResourceCommandOrdinal ?? 0).ToString(System.Globalization.CultureInfo.InvariantCulture))[7..];

    public MortalWoundOccurrenceProducerProjectionResult Project(
        AcceptedMechanicsResourcePlanningResult resources, AcceptedEffectBoundaryTranscript transcript)
    {
        return ReferenceEquals(resources, _resources) && ReferenceEquals(transcript, _resources.EffectBoundaryTranscript)
            ? _projection.Value : Invalid("mortal_wound_producer_boundary_invalid");
    }

    private MortalWoundOccurrenceProducerProjectionResult ProjectCore(
        AcceptedMechanicsResourcePlanningResult resources, AcceptedEffectBoundaryTranscript transcript)
    {
        if (!resources.IsValid || !ReferenceEquals(resources.EffectBoundaryTranscript, transcript) ||
            !transcript.IsComplete || _source.Turn != _selection.SourceTurn)
            return Invalid("mortal_wound_producer_boundary_invalid");
        var injury = _selection.PhysicalInjury;
        var selectedRef = injury is null ? null : FormattableString.Invariant(
            $"turn_{_source.Turn}:qte_terminal:{_selection.OutcomeOrdinal}:resource:{injury.ResourceCommandOrdinal}");
        var selected = selectedRef is null ? Array.Empty<AcceptedResourceMutationEvidence>() :
            transcript.ResourceMutations.Where(row => row.Transition.EventRef == selectedRef).ToArray();
        if (injury is not null && selected.Length != 1)
            return Invalid("qte_physical_injury_selected_transition_invalid");
        var rows = transcript.ProjectAcceptedWoundEvents();
        if (rows.Count is < 1 or > 160)
            return Invalid("wound_accepted_event_limit_exceeded");
        var selectedOrdinal = -1;
        var coordinates = new List<WoundAcceptedResponseEventCoordinate>(rows.Count);
        foreach (var row in rows)
        {
            var isSelected = selected.Length == 1 && row.Mutation == selected[0].Mutation;
            if (isSelected) selectedOrdinal = coordinates.Count;
            coordinates.Add(new WoundAcceptedResponseEventCoordinate(
                isSelected ? selectedRef! : ProducerOperationKey + ":accepted:" +
                    coordinates.Count.ToString(System.Globalization.CultureInfo.InvariantCulture),
                row.Kind, row.AuthorityId));
        }
        var response = new WoundAcceptedResponseEventProjection(_source.SessionId, _source.RequestId,
            _source.SnapshotToken, _source.Turn, coordinates);
        if (injury is null)
            return new(ProducerOperationKey, response, [], []);
        var transition = selected[0].Transition;
        var healthCoordinate = new ResourceCoordinate("mortal_world", ResourceOwnerKind.Player, "player_current", "health");
        if (selectedOrdinal < 0 || transition.Coordinate != healthCoordinate ||
            transition.Turn != _source.Turn || transition.Operation != ResourceTransitionOperation.Damage ||
            !_definitions.TryResolveExact("health", out var definition) || definition is null ||
            definition.CapacityPolicy.Kind != ResourceCapacityKind.RegisteredFormula ||
            definition.CapacityPolicy.FormulaKey != ResourceCapacityFormulaCatalog.MortalHealthCapacityV1 ||
            !definition.AllowedOwnerKinds.Contains(ResourceOwnerKind.Player) ||
            !definition.AllowedOperations.Contains(ResourceOperation.Damage))
            return Invalid("qte_physical_injury_selected_health_invalid");
        if (selected[0].ExecutionKind != ResourceMutationExecutionKind.Applied ||
            transition.Outcome != ResourceTransitionOutcome.Applied || transition.AppliedAmount <= 0)
            return new(ProducerOperationKey, response, [], []);
        if (injury.SchemaVersion != 1 || injury.MaximumSeverityRank is < 1 or > 4 ||
            injury.ResourceCommandOrdinal <= 0 || transition.BeforeState is null || transition.AfterState is null ||
            transition.BeforeState.Current - transition.AfterState.Current != transition.AppliedAmount)
            return Invalid("qte_physical_injury_selected_health_invalid");
        var harm = new MortalWoundAcceptedHarmResult(0, selectedOrdinal,
            new WoundOwnerCoordinate("mortal_world", "player", "player_current", WoundCarrierCatalog.PlayerPath),
            "physical", "mortal_qte_terminal_health_damage_v1",
            new MortalWoundOccurrenceSource("qte_terminal_health_damage", ProducerOperationKey, "accepted"),
            new MortalWoundOccurrenceOutcome("harmful", injury.MaximumSeverityRank, injury.Cause),
            4, null, null, new WoundOpportunitySafeContext("вы", injury.Cause, injury.AllowedLocationKinds), null);
        return new(ProducerOperationKey, response, [harm], []);
    }

    internal static (MortalWoundOccurrenceState Pending, MortalWoundOpportunityReceiptState Consumed)
        ValidateCapturedWounds(IReadOnlyDictionary<string, CanonicalBeforeImage> roots)
    {
        var pending = MortalWoundOccurrenceState.Parse(Read(roots, MortalWoundOccurrenceState.StatePath) ??
            "{\"schemaVersion\":1,\"occurrences\":[]}", MortalWoundOccurrenceState.StatePath);
        var receipts = MortalWoundOpportunityReceiptState.Parse(Read(roots, MortalWoundOpportunityReceiptState.StatePath) ??
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}", MortalWoundOpportunityReceiptState.StatePath);
        var identity = WoundIdentityState.Parse(Read(roots, WoundIdentityState.StatePath) ??
            "{\"schemaVersion\":1,\"entries\":[]}", WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(Read(roots, WoundHistoryState.HistoryPath) ??
            "{\"schemaVersion\":1,\"nextOrdinal\":1,\"transitions\":[]}", WoundHistoryState.HistoryPath);
        JsonObject? Object(string path) => Read(roots, path) is { } json
            ? StrictJsonAuthority.Deserialize<JsonObject>(json, SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed,
                "QTE captured wound carrier") : null;
        var carriers = WoundCarrierCatalog.Build(new(Object(WoundCarrierCatalog.PlayerPath),
            Object(WoundCarrierCatalog.NpcPath), Object(WoundCarrierCatalog.EnemiesPath),
            Object(WoundCarrierCatalog.AlliesPath), Object(WoundCarrierCatalog.AfterlifeProfilesPath)));
        var issues = pending.Issues.Concat(receipts.Issues).Concat(identity.Issues)
            .Concat(history.Issues).Concat(carriers.Issues).ToList();
        if (history.State is not null && identity.State is not null)
            issues.AddRange(history.State.ValidateAgreement(identity.State, carriers));
        if (pending.State is not null && receipts.State is not null)
        {
            issues.AddRange(MortalWoundOpportunityReceiptState.ValidateConsumedOccurrenceAgreement(pending.State, receipts.State));
            if (history.State is not null)
                issues.AddRange(MortalWoundOpportunityReceiptState.ValidateHistoryAgreement(receipts.State, history.State));
        }
        if (issues.Count != 0 || pending.State is null || receipts.State is null)
            throw new InvalidDataException("qte_physical_injury_wound_authority_invalid: " +
                string.Join("; ", issues.Select(static issue => issue.Code + "@" + issue.FilePath)));
        return (pending.State, receipts.State);
    }

    private MortalWoundOccurrenceProducerProjectionResult Invalid(string code) => new(ProducerOperationKey,
        null, [], [new ValidationIssue("QTE.physicalInjury", IssueSeverity.Error,
            "The actual finalized QTE health result does not match its frozen injury declaration.", code: code)]);

    private static string? Read(IReadOnlyDictionary<string, CanonicalBeforeImage> roots, string path)
    {
        if (!roots.TryGetValue(path, out var image))
            throw new InvalidDataException("qte_physical_injury_wound_before_missing: " + path);
        return image.Bytes is { } bytes ? Encoding.UTF8.GetString(bytes).TrimStart('\uFEFF') : null;
    }

    private static string Seal(params string?[] fields) =>
        WoundAcceptedTurnFingerprintWriter.Compute(new string?[] { "qte-terminal-health-producer-v1" }.Concat(fields));
}
