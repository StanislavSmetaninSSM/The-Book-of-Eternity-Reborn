using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentRematerializationAuthority(
    string PreparedInputFingerprint,
    string RequestFingerprint,
    string ResolutionAuthorityFingerprint,
    string ResultFingerprint,
    string AttemptId,
    string OperationKey,
    string TransitionId,
    string ExpectedBeforeFingerprint,
    string ProjectionFingerprint,
    string AuthoritySeedFingerprint,
    string SourceExportFingerprint,
    string BatchTopologyFingerprint,
    string AuthoritySeal);

internal sealed class MortalWoundTreatmentSeverityRematerializationResult
{
    internal MortalWoundTreatmentSeverityRematerializationResult(
        WoundEffectOperationBatch? batch,
        MortalWoundTreatmentRematerializationAuthority? authority,
        IReadOnlyList<ValidationIssue> issues)
    {
        _batch = batch is null
            ? null
            : WoundAcceptedTurnData.CloneOperationBatch(batch);
        Authority = authority is null ? null : authority with { };
        Issues = Array.AsReadOnly(issues.ToArray());
    }

    internal WoundEffectOperationBatch? Batch => _batch is null
        ? null
        : WoundAcceptedTurnData.CloneOperationBatch(_batch);
    private readonly WoundEffectOperationBatch? _batch;
    internal MortalWoundTreatmentRematerializationAuthority? Authority { get; }
    internal IReadOnlyList<ValidationIssue> Issues { get; }
    internal bool IsValid => Issues.Count == 0 &&
                             ((Batch is null) == (Authority is null));
}

internal static class MortalWoundTreatmentSeverityRematerializationPlanner
{
    private const string SeedDomain =
        "book_of_eternity.mortal_wound_treatment.rematerialization_seed";
    private const string CoordinateDomain =
        "book_of_eternity.mortal_wound_treatment.rematerialization_coordinate";
    private const string TopologyDomain =
        "book_of_eternity.mortal_wound_treatment.rematerialization_topology";
    private const string SealDomain =
        "book_of_eternity.mortal_wound_treatment.rematerialization_authority";
    private const string Version = "1";

    internal static MortalWoundTreatmentSeverityRematerializationResult Prepare(
        WoundAcceptedTurnInput input,
        MortalWoundTreatmentOutcomePreparation preparation,
        string requestFingerprint,
        string resolutionAuthorityFingerprint,
        string resultFingerprint,
        string attemptId,
        string operationKey,
        string expectedBeforeFingerprint)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(preparation);
        if (preparation.SeverityReduction is null)
        {
            return new MortalWoundTreatmentSeverityRematerializationResult(
                null,
                null,
                Array.Empty<ValidationIssue>());
        }

        try
        {
            return PrepareReduction(
                input,
                preparation,
                requestFingerprint,
                resolutionAuthorityFingerprint,
                resultFingerprint,
                attemptId,
                operationKey,
                expectedBeforeFingerprint);
        }
        catch (Exception exception) when (exception is ArgumentException or
                                           InvalidOperationException or
                                           JsonException or OverflowException)
        {
            return Invalid(exception.GetType().Name);
        }
    }

    internal static bool Agrees(
        WoundAcceptedTurnInput input,
        MortalWoundTreatmentOutcomePreparation preparation,
        string requestFingerprint,
        string resolutionAuthorityFingerprint,
        string resultFingerprint,
        string attemptId,
        string operationKey,
        string expectedBeforeFingerprint,
        WoundEffectOperationBatch batch,
        object authority)
    {
        if (batch is null ||
            authority is not MortalWoundTreatmentRematerializationAuthority supplied)
        {
            return false;
        }
        var recomputed = Prepare(
            input,
            preparation,
            requestFingerprint,
            resolutionAuthorityFingerprint,
            resultFingerprint,
            attemptId,
            operationKey,
            expectedBeforeFingerprint);
        return recomputed.IsValid &&
               recomputed.Batch is not null &&
               recomputed.Authority is not null &&
               recomputed.Authority == supplied &&
               string.Equals(
                   ComputeBatchTopologyFingerprint(batch),
                   supplied.BatchTopologyFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   batch.SourceExportFingerprint,
                   supplied.SourceExportFingerprint,
                   StringComparison.Ordinal) &&
               string.Equals(
                   WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
                   supplied.SourceExportFingerprint,
                   StringComparison.Ordinal);
    }

    private static MortalWoundTreatmentSeverityRematerializationResult
        PrepareReduction(
            WoundAcceptedTurnInput input,
            MortalWoundTreatmentOutcomePreparation preparation,
            string requestFingerprint,
            string resolutionAuthorityFingerprint,
            string resultFingerprint,
            string attemptId,
            string operationKey,
            string expectedBeforeFingerprint)
    {
        var projection = preparation.SeverityReduction!;
        var before = preparation.Before;
        var projectionBefore = projection.Before;
        var after = projection.ProvisionalAfter;
        var inputFingerprint = WoundAcceptedTurnFingerprints.ComputeInput(input);
        var actualBeforeFingerprint =
            WoundIdentityState.ComputeSemanticFingerprint(before);
        var recomputedProjection = MortalWoundTreatmentSeverityReductionPlanner.Project(
            projectionBefore,
            projection.Steps,
            after.Severity.LastChangeEventRef);
        var acceptedEvents = input.Binding.AcceptedEvents.Where(value =>
                string.Equals(
                    value.EventRef,
                    after.Severity.LastChangeEventRef,
                    StringComparison.Ordinal))
            .ToArray();
        if (!ResourceMaterializationContract.IsExactIdentifier(attemptId) ||
            !ResourceMaterializationContract.IsExactIdentifier(operationKey) ||
            !ResourceMaterializationContract.IsExactIdentifier(
                preparation.TransitionId) ||
            string.IsNullOrWhiteSpace(requestFingerprint) ||
            string.IsNullOrWhiteSpace(resolutionAuthorityFingerprint) ||
            string.IsNullOrWhiteSpace(resultFingerprint) ||
            !string.Equals(
                expectedBeforeFingerprint,
                actualBeforeFingerprint,
                StringComparison.Ordinal) ||
            !CanonicalEquals(preparation.ProvisionalAfter, after) ||
            !recomputedProjection.IsValid ||
            recomputedProjection.Projection is null ||
            !ProjectionEquals(recomputedProjection.Projection, projection) ||
            acceptedEvents.Length != 1 ||
            input.PreTurnEffectCarriers is null ||
            input.PreTurnEffectIdentityIndex is null)
        {
            return Invalid("changed or incomplete sealed rematerialization input");
        }

        var sourceOccurrence = WoundCarrierCatalog.Build(input.PreTurnCarriers)
            .Occurrences.Where(value => string.Equals(
                value.WoundId,
                before.WoundId,
                StringComparison.Ordinal))
            .ToArray();
        if (sourceOccurrence.Length != 1 ||
            !CanonicalEquals(sourceOccurrence[0].Wound, before))
        {
            return Invalid("the exact pre-turn wound is not present");
        }

        using var identityDocument = JsonDocument.Parse(
            input.PreTurnEffectIdentityIndex.ToJsonString());
        var identityParse = EffectIdentityState.Parse(
            identityDocument.RootElement,
            EffectIdentityState.StatePath);
        if (identityParse.State is null || identityParse.Issues.Count != 0)
            return Invalid("the pre-turn effect identity index is invalid");

        var seed = WoundAcceptedTurnFingerprintWriter.Compute(new string?[]
        {
            SeedDomain,
            Version,
            inputFingerprint,
            requestFingerprint,
            resolutionAuthorityFingerprint,
            resultFingerprint,
            attemptId,
            operationKey,
            preparation.TransitionId,
            expectedBeforeFingerprint,
            projection.Fingerprint
        });
        var localWoundRef = Coordinate("wound_ref", requestFingerprint,
            resultFingerprint, preparation.TransitionId);
        var acceptedEvent = acceptedEvents[0];
        var definitions = before.Consequences.OwnedEffectSources.Definitions
            .Select(value =>
            {
                var json = JsonNode.Parse(value.GetRawText())!.AsObject();
                return new WoundEffectSourceDefinition(
                    json["definitionKey"]!.GetValue<string>(),
                    json);
            })
            .ToArray();
        var export = new WoundEffectSourceExport(
            1,
            "wound",
            before.WoundId,
            localWoundRef,
            "active",
            false,
            before.Owner.Realm,
            before.Owner,
            acceptedEvent.EventRef,
            acceptedEvent.SemanticFingerprint,
            attemptId,
            seed,
            definitions);

        if (!WoundEffectCarrierAdapter.TryCreateTargetKey(
                before.Owner,
                out var targetKey))
        {
            return Invalid("unsupported wound owner target");
        }
        var definitionsByKey = definitions.ToDictionary(
            static value => value.DefinitionKey,
            StringComparer.Ordinal);
        var applications = new List<WoundRootEffectApplication>();
        var lineage = new List<WoundRootLineageAuthorityRow>();
        for (var index = 0; index < projection.Roots.Count; index++)
        {
            var root = projection.Roots[index];
            if (!definitionsByKey.TryGetValue(
                    root.DefinitionKey,
                    out var exportedDefinition))
            {
                return Invalid("projection root definition is missing");
            }
            var definition = exportedDefinition.Definition;
            if (definition["components"] is not JsonArray components ||
                !WoundEffectCarrierAdapter.TryCreateCarrierCoordinate(
                    before.Owner,
                    targetKey,
                    definition,
                    out var carrierCoordinate))
            {
                return Invalid("projection root carrier is invalid");
            }
            var applicationRef = Coordinate(
                "wound_application",
                requestFingerprint,
                resultFingerprint,
                preparation.TransitionId,
                (index + 1).ToString(CultureInfo.InvariantCulture),
                root.PriorEffectId,
                root.DefinitionKey);
            var rootOperationKey = Coordinate(
                "wound_operation",
                requestFingerprint,
                resultFingerprint,
                preparation.TransitionId,
                (index + 1).ToString(CultureInfo.InvariantCulture),
                root.PriorEffectId,
                root.DefinitionKey);
            var sourceKey = new EffectSourceKey(
                before.Owner.Realm,
                "wound",
                before.WoundId,
                root.DefinitionKey);
            var parameters = new JsonObject();
            applications.Add(new WoundRootEffectApplication(
                applicationRef,
                mechanicsOrdinal: 1,
                operationOrdinal: index + 1,
                operationKind: "apply",
                operationKey: rootOperationKey,
                definitionKey: root.DefinitionKey,
                new WoundEffectTargetSelector(
                    targetKey.Kind,
                    targetKey.TargetId,
                    null),
                targetKey,
                new WoundEffectSourceSelector(
                    sourceKey.Realm,
                    sourceKey.Kind,
                    null,
                    localWoundRef,
                    sourceKey.DefinitionKey),
                sourceKey,
                parameters,
                root.Slots,
                components.Count,
                WoundEffectMaterializationFingerprint.Compute(
                    sourceKey,
                    definition["schemaVersion"]!.GetValue<int>(),
                    parameters,
                    components),
                root.OwnershipDomain,
                acceptedEvent.EventRef,
                carrierCoordinate,
                root.PriorEffectId));
            lineage.Add(new WoundRootLineageAuthorityRow(
                applicationRef,
                null,
                root.DefinitionKey,
                root.OwnershipDomain));
        }

        var terminal = WoundEffectTerminalOperationPlanner.Plan(
            before,
            input.PreTurnEffectCarriers,
            identityParse.State,
            before.Consequences.OwnedEffectSources.RootBindings
                .Select(static value => value.EffectId)
                .ToArray(),
            acceptedEvent.EventRef,
            mechanicsOrdinal: 1,
            operationOrdinalOffset: applications.Count,
            operationKey);
        if (!terminal.Success)
        {
            return new MortalWoundTreatmentSeverityRematerializationResult(
                null,
                null,
                terminal.Issues.ToArray());
        }

        var structuralSeal = WoundAcceptedTurnFingerprints.ComputeTransitionAuthority(
            inputFingerprint,
            localWoundRef,
            before.WoundId,
            attemptId,
            seed,
            operationKey,
            WoundAcceptedTurnPlanner.TreatmentPublicationSummary,
            before.Severity.Rank,
            "treat",
            null,
            expectedBeforeFingerprint);
        var transition = new WoundPreparedTransitionAuthority(
            inputFingerprint,
            attemptId,
            seed,
            operationKey,
            WoundAcceptedTurnPlanner.TreatmentPublicationSummary,
            before.Severity.Rank,
            "treat",
            null,
            expectedBeforeFingerprint,
            structuralSeal);
        var provisional = new WoundEffectOperationBatch(
            localWoundRef,
            before.WoundId,
            export,
            applications,
            terminal.Operations,
            lineage,
            string.Empty,
            transition);
        var sourceFingerprint =
            WoundAcceptedTurnFingerprints.ComputeSourceExport(provisional);
        var batch = new WoundEffectOperationBatch(
            localWoundRef,
            before.WoundId,
            export,
            applications,
            terminal.Operations,
            lineage,
            sourceFingerprint,
            transition);
        var topology = ComputeBatchTopologyFingerprint(batch);
        var authoritySeal = WoundAcceptedTurnFingerprintWriter.Compute(
            new[] { SealDomain, Version, seed, topology });
        var authority = new MortalWoundTreatmentRematerializationAuthority(
            inputFingerprint,
            requestFingerprint,
            resolutionAuthorityFingerprint,
            resultFingerprint,
            attemptId,
            operationKey,
            preparation.TransitionId,
            expectedBeforeFingerprint,
            projection.Fingerprint,
            seed,
            sourceFingerprint,
            topology,
            authoritySeal);
        return new MortalWoundTreatmentSeverityRematerializationResult(
            batch,
            authority,
            Array.Empty<ValidationIssue>());
    }

    private static bool ProjectionEquals(
        MortalWoundTreatmentSeverityReductionProjection expected,
        MortalWoundTreatmentSeverityReductionProjection actual) =>
        expected.Steps == actual.Steps &&
        string.Equals(expected.Fingerprint, actual.Fingerprint,
            StringComparison.Ordinal) &&
        CanonicalEquals(expected.Before, actual.Before) &&
        CanonicalEquals(expected.ProvisionalAfter, actual.ProvisionalAfter) &&
        expected.Roots.Count == actual.Roots.Count &&
        expected.Roots.Zip(actual.Roots).All(static pair =>
            string.Equals(pair.First.PriorEffectId, pair.Second.PriorEffectId,
                StringComparison.Ordinal) &&
            string.Equals(pair.First.DefinitionKey, pair.Second.DefinitionKey,
                StringComparison.Ordinal) &&
            pair.First.OwnershipDomain == pair.Second.OwnershipDomain &&
            pair.First.Slots.SequenceEqual(pair.Second.Slots));

    internal static string ComputeBatchTopologyFingerprint(
        WoundEffectOperationBatch batch)
    {
        ArgumentNullException.ThrowIfNull(batch);
        var terminals = batch.TerminalOperations;
        var fields = new List<string?>
        {
            TopologyDomain,
            Version,
            batch.LocalWoundRef,
            batch.PreparedWoundId,
            WoundAcceptedTurnFingerprints.ComputeSourceExport(batch),
            batch.SourceExportFingerprint,
            terminals.Count.ToString(CultureInfo.InvariantCulture)
        };
        for (var index = 0; index < terminals.Count; index++)
        {
            var terminal = terminals[index];
            fields.Add(index.ToString(CultureInfo.InvariantCulture));
            fields.Add(terminal.OperationRef);
            fields.Add(terminal.OperationKey);
            fields.Add(terminal.EffectId);
            fields.Add(terminal.MechanicsOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(terminal.OperationOrdinal.ToString(CultureInfo.InvariantCulture));
            fields.Add(terminal.OperationKind);
            fields.Add(terminal.CausalEventRef);
            var source = terminal.ExpectedSourceKey;
            fields.Add(source.Realm);
            fields.Add(source.Kind);
            fields.Add(source.SourceId);
            fields.Add(source.DefinitionKey);
            var target = terminal.ExpectedTargetKey;
            fields.Add(target.Realm);
            fields.Add(target.Kind);
            fields.Add(target.TargetId);
            var carrier = terminal.ExpectedCarrierCoordinate;
            fields.Add(carrier.Kind);
            fields.Add(carrier.OwnerId);
            fields.Add(carrier.Path);
            fields.Add(carrier.Category);
            fields.Add(terminal.ExpectedCarrierFilePath);
            fields.Add(terminal.ExpectedCarrierJsonPath);
            var owner = terminal.ExpectedIdentityOwner;
            fields.Add(owner.Kind);
            fields.Add(owner.OwnerId);
            fields.Add(owner.CarrierPath);
            fields.Add(owner.Collection);
            var stack = terminal.ExpectedStackCoordinate;
            fields.Add(stack.Realm);
            fields.Add(stack.TargetKind);
            fields.Add(stack.TargetId);
            fields.Add(stack.SourceKind);
            fields.Add(stack.SourceId);
            fields.Add(stack.StackKey);
            fields.Add(terminal.ExpectedEffectFingerprint);
            fields.Add(terminal.ExpectedIdentityFingerprint);
            var ownership = terminal.OwnershipDomain;
            fields.Add(ownership.Kind);
            fields.Add(ownership.ComplicationId);
        }
        var transition = batch.TransitionAuthority;
        fields.Add(transition.PreparedInputFingerprint);
        fields.Add(transition.OpportunityId);
        fields.Add(transition.OpportunityAuthorityFingerprint);
        fields.Add(transition.OperationKey);
        fields.Add(transition.ReadableSummary);
        fields.Add(transition.MaximumSeverityRank.ToString(CultureInfo.InvariantCulture));
        fields.Add(transition.TransitionKind);
        fields.Add(transition.CauseKind);
        fields.Add(transition.ExpectedBeforeFingerprint);
        fields.Add(transition.AuthoritySeal);
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string Coordinate(string prefix, params string[] values)
    {
        var fingerprint = WoundAcceptedTurnFingerprintWriter.Compute(
            new string?[] { CoordinateDomain, Version, prefix }.Concat(values));
        return prefix + "_" + fingerprint["sha256:".Length..];
    }

    private static bool CanonicalEquals(
        WoundMaterializationEnvelope left,
        WoundMaterializationEnvelope right) => string.Equals(
        WoundMaterializationContract.SerializeCanonical(left),
        WoundMaterializationContract.SerializeCanonical(right),
        StringComparison.Ordinal);

    private static MortalWoundTreatmentSeverityRematerializationResult Invalid(
        string actual) => new(
        null,
        null,
        new[]
        {
            WoundAcceptedTurnPlannerCore.NewIssue(
                "wound_plan_treatment_rematerialization_invalid",
                "Treatment severity rematerialization requires one independently recomputable sealed batch: " + actual,
                "complete sealed treatment rematerialization authority",
                actual)
        });
}
