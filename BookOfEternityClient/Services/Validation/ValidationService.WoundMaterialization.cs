using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private static readonly string[] CanonicalWoundCarrierPaths =
    {
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    };

    private static readonly string[] CanonicalWoundSnapshotReadPaths =
        CanonicalWoundCarrierPaths
            .Concat(
            [
                WoundIdentityState.StatePath,
                WoundHistoryState.HistoryPath,
                MortalWoundOccurrenceState.StatePath,
                MortalWoundOpportunityReceiptState.StatePath
            ])
            .ToArray();

    private sealed record AcceptedTurnRawWoundDraft(
        WoundResponseCommandParsingResult ParsedCommands,
        WoundCarrierCatalogInput PreTurnCarriers,
        JsonObject PreTurnIdentityIndex,
        JsonObject PreTurnHistory,
        MortalWoundOccurrenceState SignedOccurrences,
        MortalWoundOpportunityReceiptState SignedReceipts);

    private sealed record AcceptedTurnPreparedWoundHandoff(
        JsonObject Commands,
        WoundAcceptedTurnInput Input,
        WoundPreparedAcceptedTurnPlan PreparedPlan,
        EffectApplicationDiagnosticLocations? EffectLocations);

    private sealed record AcceptedTurnWoundPlanningHandoff(
        JsonObject Commands,
        WoundAcceptedTurnInput Input,
        AcceptedMechanicsWoundStageBundle StageBundle);

    private sealed class AcceptedTurnWoundHandoffSink
    {
        internal AcceptedTurnWoundPlanningHandoff? Value { get; set; }
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalWoundMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            issues,
            writeLease: null);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
            issues,
            writeLease);
        return issues;
    }

    private async Task ValidateAcceptedTurnCanonicalWoundMaterializationAsync(
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var carrierJson = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in CanonicalWoundCarrierPaths)
        {
            carrierJson[path] = writeLease == null
                ? await _fs.ReadFileAsync(path)
                : await _fs.ReadFileAsync(writeLease, path);
        }
        var identityJson = writeLease == null
            ? await _fs.ReadFileAsync(WoundIdentityState.StatePath)
            : await _fs.ReadFileAsync(writeLease, WoundIdentityState.StatePath);
        var historyJson = writeLease == null
            ? await _fs.ReadFileAsync(WoundHistoryState.HistoryPath)
            : await _fs.ReadFileAsync(writeLease, WoundHistoryState.HistoryPath);
        var commandJson = writeLease == null
            ? await _fs.ReadFileAsync(AcceptedMechanicsPlan.WoundCommandPath)
            : await _fs.ReadFileAsync(
                writeLease,
                AcceptedMechanicsPlan.WoundCommandPath);
        if (identityJson is null &&
            historyJson is null &&
            commandJson is null &&
            carrierJson.Values.All(static value => value is null))
        {
            return;
        }

        if (commandJson is not null)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_command_unconsumed",
                "no accepted wound command after canonical publication",
                "command file remains present",
                IssueCategory.ClientOwnedSurface));
        }

        var carriers = new WoundCarrierCatalogInput(
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.PlayerPath],
                WoundCarrierCatalog.PlayerPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.NpcPath],
                WoundCarrierCatalog.NpcPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.EnemiesPath],
                WoundCarrierCatalog.EnemiesPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.AlliesPath],
                WoundCarrierCatalog.AlliesPath,
                issues),
            ParseWoundCarrierRoot(
                carrierJson[WoundCarrierCatalog.AfterlifeProfilesPath],
                WoundCarrierCatalog.AfterlifeProfilesPath,
                issues));
        var catalog = WoundCarrierCatalog.Build(carriers);
        issues.AddRange(catalog.Issues);

        var identity = WoundIdentityState.Parse(
            identityJson,
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            historyJson,
            WoundHistoryState.HistoryPath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
        if (catalog.Issues.Count == 0 &&
            identity.State is not null &&
            history.State is not null)
        {
            issues.AddRange(history.State.ValidateAgreement(identity.State, catalog));
        }
    }

    private async Task<AcceptedTurnRawWoundDraft?>
        LoadAcceptedTurnRawWoundDraftAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            FileSystemManager.CanonicalWriteLease writeLease,
            string commandJson,
            List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(commandJson);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        var parsedCommands = ParseAcceptedTurnWoundCommands(
            commandJson,
            manifest,
            issues);
        if (parsedCommands is null || parsedCommands.CommandRoot is null)
            return null;

        var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
            _fs,
            writeLease,
            CanonicalWoundSnapshotReadPaths);
        if (!snapshotRead.Success || snapshotRead.Snapshot is null)
        {
            issues.AddRange(snapshotRead.Issues);
            return null;
        }
        var snapshot = snapshotRead.Snapshot;
        var computedManifestHash = ComputeManifestPayloadHash(manifest);
        if (!string.Equals(
                computedManifestHash,
                manifest.ManifestPayloadHash,
                StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(snapshot.SessionId, manifest.SessionId, StringComparison.Ordinal) ||
            !string.Equals(snapshot.RequestId, manifest.RequestId, StringComparison.Ordinal) ||
            !string.Equals(
                snapshot.SnapshotToken,
                manifest.ManifestPayloadHash,
                StringComparison.OrdinalIgnoreCase) ||
            snapshot.TurnNumber != manifest.TurnNumber)
        {
            issues.Add(WoundIssue(
                LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                "wound_materialization_snapshot_binding_mismatch",
                "one exact validated pending-turn manifest and strict snapshot-reader authority",
                "the validation manifest does not identify the strict current snapshot",
                IssueCategory.ClientOwnedSurface));
            return null;
        }

        if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(
                manifest,
                static value => value.Files,
                static value => value.SnapshotFileHashes,
                static value => value.RollbackBaselineFiles,
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                out var missingSnapshotPath))
        {
            issues.Add(WoundIssue(
                missingSnapshotPath ?? AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_snapshot_before_image_missing",
                "exact signed rollback snapshot evidence for every wound, scheduler, and output authority path",
                "missing, contradictory, or incompletely registered snapshot before-image",
                IssueCategory.ClientOwnedSurface));
            return null;
        }

        var preTurnBytes = CanonicalWoundSnapshotReadPaths.ToDictionary(
            static path => path,
            snapshot.ReadRequiredBytes,
            StringComparer.Ordinal);
        foreach (var path in new[]
                 {
                     WoundCarrierCatalog.PlayerPath,
                     WoundCarrierCatalog.NpcPath,
                     WoundIdentityState.StatePath,
                     WoundHistoryState.HistoryPath,
                     MortalWoundOccurrenceState.StatePath,
                     MortalWoundOpportunityReceiptState.StatePath
                 })
        {
            await ValidateWoundClientOwnedBaselineAsync(
                path,
                preTurnBytes[path],
                writeLease,
                issues);
        }
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return null;

        var preTurnJson = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (path, bytes) in preTurnBytes)
        {
            var json = DecodeWoundSnapshotJson(bytes, path, issues);
            if (json is not null)
                preTurnJson[path] = json;
        }
        if (preTurnJson.Count != preTurnBytes.Count)
            return null;
        var preTurnIdentityJson = preTurnJson[WoundIdentityState.StatePath];
        var preTurnHistoryJson = preTurnJson[WoundHistoryState.HistoryPath];
        var signedOccurrenceJson = preTurnJson[MortalWoundOccurrenceState.StatePath];
        var signedReceiptJson = preTurnJson[MortalWoundOpportunityReceiptState.StatePath];

        var preTurnCarriers = new WoundCarrierCatalogInput(
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.PlayerPath],
                WoundCarrierCatalog.PlayerPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.NpcPath],
                WoundCarrierCatalog.NpcPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.EnemiesPath],
                WoundCarrierCatalog.EnemiesPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.AlliesPath],
                WoundCarrierCatalog.AlliesPath,
                issues),
            ParseWoundCarrierRoot(
                preTurnJson[WoundCarrierCatalog.AfterlifeProfilesPath],
                WoundCarrierCatalog.AfterlifeProfilesPath,
                issues));
        foreach (var path in new[]
                 {
                     WoundCarrierCatalog.EnemiesPath,
                     WoundCarrierCatalog.AlliesPath,
                     WoundCarrierCatalog.AfterlifeProfilesPath
                 })
        {
            await ValidateSharedWoundCarrierBaselineAsync(
                path,
                preTurnCarriers,
                writeLease,
                issues);
        }
        var carrierCatalog = WoundCarrierCatalog.Build(preTurnCarriers);
        issues.AddRange(carrierCatalog.Issues);
        var identity = WoundIdentityState.Parse(
            preTurnIdentityJson,
            WoundIdentityState.StatePath);
        var history = WoundHistoryState.Parse(
            preTurnHistoryJson,
            WoundHistoryState.HistoryPath);
        var occurrences = MortalWoundOccurrenceState.Parse(
            signedOccurrenceJson,
            MortalWoundOccurrenceState.StatePath);
        var receipts = MortalWoundOpportunityReceiptState.Parse(
            signedReceiptJson,
            MortalWoundOpportunityReceiptState.StatePath);
        issues.AddRange(identity.Issues);
        issues.AddRange(history.Issues);
        issues.AddRange(occurrences.Issues);
        issues.AddRange(receipts.Issues);
        if (carrierCatalog.Issues.Count == 0 &&
            identity.State is not null &&
            history.State is not null)
        {
            issues.AddRange(history.State.ValidateAgreement(
                identity.State,
                carrierCatalog));
        }
        if (occurrences.State is not null && receipts.State is not null)
        {
            issues.AddRange(
                MortalWoundOpportunityReceiptState
                    .ValidateConsumedOccurrenceAgreement(
                        occurrences.State,
                        receipts.State));
            if (history.State is not null)
            {
                issues.AddRange(
                    MortalWoundOpportunityReceiptState.ValidateHistoryAgreement(
                        receipts.State,
                        history.State));
            }
        }
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            identity.State is null ||
            history.State is null ||
            occurrences.State is null ||
            receipts.State is null)
        {
            return null;
        }

        return new AcceptedTurnRawWoundDraft(
            parsedCommands,
            WoundAcceptedTurnData.CloneWoundCarriers(preTurnCarriers)!,
            JsonNode.Parse(WoundIdentityState.SerializeCanonical(identity.State))!
                .AsObject(),
            JsonNode.Parse(WoundHistoryState.SerializeCanonical(history.State))!
                .AsObject(),
            occurrences.State,
            receipts.State);
    }

    private AcceptedTurnPreparedWoundHandoff? PrepareAcceptedTurnWoundHandoff(
        AcceptedTurnRawWoundDraft? draft,
        ValidationPendingTurnSnapshotManifest manifest,
        string realm,
        EffectAcceptedTurnInput effectInput,
        ResourceOwnerCompositionResult? resourceOwners,
        ResourceDefinitionCatalog? resourceDefinitions,
        ResourceStateLedger? resourceState,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        if (draft is null)
            return null;
        ArgumentNullException.ThrowIfNull(manifest);
        ArgumentNullException.ThrowIfNull(effectInput);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        WoundAcceptedTurnBinding binding;
        IReadOnlyList<WoundOpportunityDecisionReceipt> priorReceipts;
        if (draft.ParsedCommands.TreatmentCommands.Count != 0)
        {
            var acceptedEvents = WoundAcceptedEventAuthorityComposer
                .ComposeDefaultAcceptedTurn(
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.ManifestPayloadHash,
                    manifest.TurnNumber);
            issues.AddRange(acceptedEvents.Issues);
            if (!acceptedEvents.Success)
            {
                AddMissingWoundStageIssue(
                    issues,
                    "wound_materialization_treatment_validation_authority_partial",
                    "independent accepted-turn treatment event authority");
                return null;
            }
            binding = new WoundAcceptedTurnBinding(
                manifest.SessionId,
                manifest.RequestId,
                manifest.ManifestPayloadHash,
                realm,
                manifest.TurnNumber,
                acceptedEvents.Events,
                acceptedEvents.EventsFingerprint);
            priorReceipts = Array.Empty<WoundOpportunityDecisionReceipt>();
        }
        else
        {
            var validationAuthority =
                MortalWoundOpportunityValidationAuthority.Reconstruct(
                    manifest.SessionId,
                    manifest.RequestId,
                    manifest.ManifestPayloadHash,
                    realm,
                    manifest.TurnNumber,
                    draft.SignedOccurrences,
                    draft.SignedReceipts,
                    draft.PreTurnCarriers,
                    draft.ParsedCommands.Commands
                        .Select(static value => value.Opportunity)
                        .ToArray());
            issues.AddRange(validationAuthority.Issues);
            if (!validationAuthority.Success ||
                validationAuthority.Binding is not { } occurrenceBinding)
            {
                AddMissingWoundStageIssue(
                    issues,
                    "wound_materialization_validation_authority_partial",
                    "independent occurrence validation authority");
                return null;
            }
            binding = occurrenceBinding;
            priorReceipts = validationAuthority.PriorReceipts;
        }
        var recomposed = WoundResponseInputComposer.RecomposeCommandRoot(
            binding,
            draft.ParsedCommands,
            priorReceipts);
        issues.AddRange(recomposed.Issues);
        if (!recomposed.Success || recomposed.CommandRoot is null)
        {
            AddMissingWoundStageIssue(
                issues,
                "wound_materialization_recomposition_partial",
                "exact typed wound command recomposition");
            return null;
        }

        var sourceBinding = WoundAcceptedTurnPlanner.BindAcceptedSourceTargets(
            binding,
            draft.ParsedCommands.Commands
                .Select(static value => value.Opportunity)
                .ToArray(),
            effectInput.TargetAuthority);
        issues.AddRange(sourceBinding.Issues);
        if (!sourceBinding.Success)
        {
            AddMissingWoundStageIssue(
                issues,
                "wound_materialization_source_binding_partial",
                "accepted wound source/target binding");
            return null;
        }

        var resourceIssues = ValidateAcceptedWoundResourceBindings(
            draft.ParsedCommands,
            effectInput.TargetAuthority,
            resourceOwners,
            resourceDefinitions,
            resourceState);
        if (resourceIssues.Count != 0)
        {
            var commands = draft.ParsedCommands.Commands;
            issues.AddRange(WoundResponseInputComposer.AttachRepairContexts(
                binding,
                commands.Select(static value => value.Opportunity).ToArray(),
                commands.Select(static value => value.Decision).ToArray(),
                commands.Count == 0 ? null : commands[0].FinalSceneText,
                resourceIssues));
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return null;
        }

        var skillScopeIssues = WoundResponseInputComposer.ValidateSkillScopes(
            binding, draft.ParsedCommands.Commands, recomposed.Transitions,
            effectInput.SkillScopeAuthority, out var effectLocations);
        issues.AddRange(skillScopeIssues);
        if (skillScopeIssues.Count != 0)
            return null;

        var acceptedOwnerCarriers = WoundAcceptedOwnerCarrierAuthority.Compose(
            draft.PreTurnCarriers,
            effectInput.AcceptedCarrierBaselines ??
            effectInput.PreTurnCarriers ??
            new EffectCarrierCatalogInput(null, null, null, null, null, null));
        issues.AddRange(acceptedOwnerCarriers.Issues);
        if (!acceptedOwnerCarriers.Success || acceptedOwnerCarriers.Carriers is not { } carriers)
        {
            AddMissingWoundStageIssue(
                issues,
                "wound_materialization_owner_carrier_partial",
                "accepted wound owner carrier authority");
            return null;
        }

        var input = new WoundAcceptedTurnInput(
            binding,
            recomposed.MaterializedOpportunities,
            recomposed.Transitions,
            carriers,
            draft.PreTurnIdentityIndex,
            draft.PreTurnHistory,
            effectInput.PreTurnCarriers,
            effectInput.PreTurnIdentityIndex);
        var prepared = WoundAcceptedTurnPlanAuthority.GetOrBuildPreparedValidated(
            _fs,
            writeLease,
            input);
        issues.AddRange(prepared.Issues);
        if (!prepared.Success || prepared.Plan is null)
        {
            AddMissingWoundStageIssue(
                issues,
                "wound_materialization_prepared_stage_partial",
                "one complete prepared wound stage");
            return null;
        }
        return new AcceptedTurnPreparedWoundHandoff(
            recomposed.CommandRoot,
            WoundAcceptedTurnData.CloneInput(input)!,
            prepared.Plan,
            effectLocations?.BindPreparedSources(prepared.Plan));
    }

    private static void AddMissingWoundStageIssue(
        List<ValidationIssue> issues,
        string code,
        string expected)
    {
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return;
        issues.Add(WoundIssue(
            AcceptedMechanicsPlan.WoundCommandPath,
            code,
            expected,
            "the stage returned neither complete authority nor a typed error"));
    }

    private static IReadOnlyList<ValidationIssue>
        ValidateAcceptedWoundResourceBindings(
            WoundResponseCommandParsingResult parsedCommands,
            EffectTargetAuthority targetAuthority,
            ResourceOwnerCompositionResult? resourceOwners,
            ResourceDefinitionCatalog? resourceDefinitions,
            ResourceStateLedger? resourceState)
    {
        ArgumentNullException.ThrowIfNull(parsedCommands);
        ArgumentNullException.ThrowIfNull(targetAuthority);
        if (resourceOwners?.Authority is null ||
            resourceDefinitions is null ||
            resourceState is null)
        {
            return Array.Empty<ValidationIssue>();
        }

        var issues = new List<ValidationIssue>();
        var commands = parsedCommands.Commands;
        for (var commandIndex = 0; commandIndex < commands.Count; commandIndex++)
        {
            var command = commands[commandIndex];
            if (command.Decision.ValueKind != JsonValueKind.Object ||
                !command.Decision.TryGetProperty("decision", out var decision) ||
                decision.ValueKind != JsonValueKind.String ||
                !string.Equals(
                    decision.GetString(),
                    "materialize",
                    StringComparison.Ordinal) ||
                !command.Decision.TryGetProperty("proposal", out var proposal) ||
                proposal.ValueKind != JsonValueKind.Object ||
                !proposal.TryGetProperty(
                    "consequenceDefinitions",
                    out var consequenceDefinitions) ||
                consequenceDefinitions.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            var ownerResolved = TryResolveAcceptedWoundResourceOwner(
                command.Opportunity,
                targetAuthority,
                out var resourceOwner);
            var definitionIndex = 0;
            foreach (var consequenceDefinition in
                     consequenceDefinitions.EnumerateArray())
            {
                if (consequenceDefinition.ValueKind != JsonValueKind.Object ||
                    !consequenceDefinition.TryGetProperty(
                        "definition",
                        out var effectDefinition) ||
                    effectDefinition.ValueKind != JsonValueKind.Object ||
                    !effectDefinition.TryGetProperty(
                        "components",
                        out var components) ||
                    components.ValueKind != JsonValueKind.Array)
                {
                    definitionIndex++;
                    continue;
                }

                var componentIndex = 0;
                foreach (var component in components.EnumerateArray())
                {
                    if (TryReadPeriodicWoundResource(
                            component,
                            out var operation,
                            out var resourceKey) &&
                        (!ownerResolved ||
                         !HasAcceptedWoundResourceBound(
                             resourceOwner,
                             resourceKey,
                             operation,
                             resourceOwners,
                             resourceDefinitions,
                             resourceState)))
                    {
                        issues.Add(WoundIssue(
                            $"woundDecisions[{commandIndex}].proposal." +
                            $"consequenceDefinitions[{definitionIndex}]." +
                            $"definition.components[{componentIndex}].payload.resource",
                            "wound_consequence_resource_bound_missing",
                            "one exact active resource bound for the accepted wound owner",
                            resourceKey));
                    }
                    componentIndex++;
                }
                definitionIndex++;
            }
        }
        return issues;
    }

    private static bool TryResolveAcceptedWoundResourceOwner(
        WoundOpportunityAuthority opportunity,
        EffectTargetAuthority targetAuthority,
        out ResourceOwnerKey owner)
    {
        owner = null!;
        return WoundEffectCarrierAdapter.TryCreateTargetKey(
                   opportunity.Owner,
                   out var target) &&
               targetAuthority.TryResolveAcceptedTarget(target, out var export) &&
               export is not null &&
               EffectAcceptedTurnPlanner.TryMapEffectTargetToResourceOwner(
                   target,
                   export,
                   out owner);
    }

    private static bool TryReadPeriodicWoundResource(
        JsonElement component,
        out ResourceOperation operation,
        out string resourceKey)
    {
        operation = default;
        resourceKey = string.Empty;
        if (component.ValueKind != JsonValueKind.Object ||
            !component.TryGetProperty("profile", out var profile) ||
            profile.ValueKind != JsonValueKind.String ||
            !component.TryGetProperty("payload", out var payload) ||
            payload.ValueKind != JsonValueKind.Object ||
            !payload.TryGetProperty("resource", out var resource) ||
            resource.ValueKind != JsonValueKind.String ||
            !ResourceMaterializationContract.IsExactIdentifier(
                resource.GetString()))
        {
            return false;
        }

        var profileKey = profile.GetString();
        operation = profileKey switch
        {
            "periodic_damage" => ResourceOperation.Damage,
            "periodic_restore" => ResourceOperation.Restore,
            _ => default
        };
        if (profileKey is not ("periodic_damage" or "periodic_restore"))
        {
            return false;
        }

        resourceKey = resource.GetString()!;
        return true;
    }

    private static bool HasAcceptedWoundResourceBound(
        ResourceOwnerKey owner,
        string resourceKey,
        ResourceOperation operation,
        ResourceOwnerCompositionResult resourceOwners,
        ResourceDefinitionCatalog resourceDefinitions,
        ResourceStateLedger resourceState)
    {
        if (!resourceDefinitions.TryResolveExact(
                resourceKey,
                out var definition) ||
            definition is null ||
            !definition.AllowedOwnerKinds.Contains(owner.OwnerKind) ||
            !definition.AllowedOperations.Contains(operation) ||
            resourceOwners.Authority is null ||
            !resourceOwners.Authority.ResolveAcceptedCoordinate(
                owner,
                resourceKey).Success)
        {
            return false;
        }

        var coordinate = new ResourceCoordinate(
            owner.Realm,
            owner.OwnerKind,
            owner.ResourceOwnerId,
            resourceKey);
        var capacityDrafts = resourceOwners.CapacityDrafts
            .Where(value => value.Coordinate == coordinate)
            .ToArray();
        decimal maximum;
        if (capacityDrafts.Length == 1)
        {
            maximum = capacityDrafts[0].AcceptedMaximum;
        }
        else if (capacityDrafts.Length == 0 &&
                 resourceState.TryResolveExact(coordinate, out var state) &&
                 state is not null &&
                 state.State == ResourceLifecycleState.Active)
        {
            maximum = state.Maximum;
        }
        else
        {
            return false;
        }

        return maximum > 0m &&
               definition.Quantum > 0m &&
               ResourceMaterializationContract.IsQuantumAligned(
                   maximum,
                   0m,
                   definition.Quantum);
    }

    private AcceptedTurnWoundPlanningHandoff? FinalizeAcceptedTurnWoundHandoff(
        AcceptedTurnPreparedWoundHandoff? prepared,
        WoundEffectBatchPlanningResult? effectResult,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        if (prepared is null)
            return null;
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        if (effectResult is null || !effectResult.Success || effectResult.Plan is null)
            return null;

        var final = WoundAcceptedTurnPlanAuthority.GetOrBuildFinalValidated(
            _fs,
            writeLease,
            prepared.PreparedPlan,
            effectResult);
        issues.AddRange(final.Issues);
        if (!final.Success || final.Plan is null)
            return null;
        try
        {
            return new AcceptedTurnWoundPlanningHandoff(
                prepared.Commands.DeepClone().AsObject(),
                WoundAcceptedTurnData.CloneInput(prepared.Input)!,
                new AcceptedMechanicsWoundStageBundle(
                    prepared.Input,
                    prepared.PreparedPlan,
                    effectResult.Plan,
                    final.Plan));
        }
        catch (ArgumentException exception)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_materialization_stage_handoff_invalid",
                "one exact prepared/effect/final wound stage bundle",
                exception.GetType().Name));
            return null;
        }
    }

    private static WoundResponseCommandParsingResult? ParseAcceptedTurnWoundCommands(
        string json,
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException exception)
        {
            issues.Add(WoundIssue(
                AcceptedMechanicsPlan.WoundCommandPath,
                "wound_command_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name));
            return null;
        }

        using (document)
        {
            var root = document.RootElement;
            var parsed = WoundResponseInputComposer.ParseCommandRoot(root);
            issues.AddRange(parsed.Issues);
            if (!parsed.Success)
                return null;
            ValidateWoundCommandBinding(
                root,
                "sessionId",
                manifest.SessionId,
                issues);
            ValidateWoundCommandBinding(
                root,
                "requestId",
                manifest.RequestId,
                issues);
            ValidateWoundCommandBinding(
                root,
                "snapshotToken",
                manifest.ManifestPayloadHash,
                issues);

            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return null;
            return parsed;
        }
    }

    private static void ValidateWoundCommandBinding(
        JsonElement root,
        string field,
        string expected,
        List<ValidationIssue> issues)
    {
        if (root.TryGetProperty(field, out var value) &&
            value.ValueKind == JsonValueKind.String &&
            ResourceMaterializationContract.IsExactIdentifier(value.GetString()) &&
            string.Equals(value.GetString(), expected, StringComparison.Ordinal))
        {
            return;
        }

        issues.Add(WoundIssue(
            AcceptedMechanicsPlan.WoundCommandPath + "." + field,
            "wound_command_binding_mismatch",
            expected,
            root.TryGetProperty(field, out value)
                ? value.GetRawText()
                : "missing"));
    }

    private async Task ValidateWoundClientOwnedBaselineAsync(
        string path,
        byte[] preTurnBytes,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(preTurnBytes);
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var currentBytes = await _fs.ReadFileBytesAsync(writeLease, path);
        if (currentBytes is not null &&
            currentBytes.AsSpan().SequenceEqual(preTurnBytes))
        {
            return;
        }
        issues.Add(WoundIssue(
            path,
            "wound_materialization_client_owned_root_mutated",
            "byte-identical validated pre-turn client-owned wound root",
            currentBytes is null
                ? "current root missing"
                : "current bytes differ from validated snapshot",
            IssueCategory.ClientOwnedSurface));
    }

    private async Task ValidateSharedWoundCarrierBaselineAsync(
        string path,
        WoundCarrierCatalogInput preTurnCarriers,
        FileSystemManager.CanonicalWriteLease writeLease,
        List<ValidationIssue> issues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(preTurnCarriers);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(issues);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);

        var preTurnRoot = WoundCarrierCollectionAuthority.GetRoot(
            preTurnCarriers,
            path);
        if (preTurnRoot is null)
            return;

        var currentBytes = await _fs.ReadFileBytesAsync(writeLease, path);
        if (currentBytes is null)
        {
            AddSharedCarrierMutation(path, "current shared carrier root is missing");
            return;
        }

        var issueCount = issues.Count;
        var currentJson = DecodeWoundSnapshotJson(currentBytes, path, issues);
        var currentRoot = currentJson is null
            ? null
            : ParseWoundCarrierRoot(currentJson, path, issues);
        if (currentRoot is null)
        {
            AddSharedCarrierMutation(
                path,
                issues.Count == issueCount
                    ? "current shared carrier root is invalid"
                    : "current shared carrier root failed strict duplicate-safe parsing");
            return;
        }

        var preTurnCatalog = WoundCarrierCatalog.Build(
            SingleWoundCarrier(path, preTurnRoot));
        if (preTurnCatalog.Issues.Count != 0)
            return;

        var currentCatalog = WoundCarrierCatalog.Build(
            SingleWoundCarrier(path, currentRoot));
        if (currentCatalog.Issues.Count != 0)
        {
            issues.AddRange(currentCatalog.Issues);
            AddSharedCarrierMutation(
                path,
                "current shared activeWounds projection is not a valid canonical carrier");
            return;
        }

        var expected = ComputeSharedWoundCarrierProjection(
            path,
            preTurnCatalog);
        var actual = ComputeSharedWoundCarrierProjection(
            path,
            currentCatalog);
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            AddSharedCarrierMutation(
                path,
                "current shared activeWounds projection differs from the validated snapshot");
        }

        return;

        void AddSharedCarrierMutation(string filePath, string actual) =>
            issues.Add(WoundIssue(
                filePath,
                "wound_materialization_client_owned_root_mutated",
                "the exact canonical wound-owned projection from the validated pre-turn shared carrier",
                actual,
                IssueCategory.ClientOwnedSurface));
    }

    private static WoundCarrierCatalogInput SingleWoundCarrier(
        string path,
        JsonObject root) => path switch
        {
            WoundCarrierCatalog.EnemiesPath =>
                new WoundCarrierCatalogInput(null, null, root, null, null),
            WoundCarrierCatalog.AlliesPath =>
                new WoundCarrierCatalogInput(null, null, null, root, null),
            WoundCarrierCatalog.AfterlifeProfilesPath =>
                new WoundCarrierCatalogInput(null, null, null, null, root),
            _ => throw new ArgumentOutOfRangeException(nameof(path), path, null)
        };

    private static string ComputeSharedWoundCarrierProjection(
        string path,
        WoundCarrierCatalog catalog)
    {
        var ordered = catalog.Occurrences
            .OrderBy(static value => value.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.OwnerKind, StringComparer.Ordinal)
            .ThenBy(static value => value.Coordinate.OwnerId, StringComparer.Ordinal)
            .ThenBy(static value => value.WoundId, StringComparer.Ordinal)
            .ToArray();
        var fields = new List<string?>
        {
            "book_of_eternity.wound.shared_carrier_projection",
            "1",
            path,
            ordered.Length.ToString(System.Globalization.CultureInfo.InvariantCulture)
        };
        foreach (var occurrence in ordered)
        {
            fields.Add(occurrence.Coordinate.Realm);
            fields.Add(occurrence.Coordinate.OwnerKind);
            fields.Add(occurrence.Coordinate.OwnerId);
            fields.Add(occurrence.Coordinate.CarrierPath);
            fields.Add(occurrence.WoundId);
            fields.Add(WoundMaterializationContract.SerializeCanonical(
                occurrence.Wound));
        }
        return WoundAcceptedTurnFingerprintWriter.Compute(fields);
    }

    private static string? DecodeWoundSnapshotJson(
        byte[] bytes,
        string path,
        List<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(issues);
        try
        {
            var preamble = Encoding.UTF8.GetPreamble();
            var offset = bytes.AsSpan().StartsWith(preamble)
                ? preamble.Length
                : 0;
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(bytes, offset, bytes.Length - offset);
        }
        catch (DecoderFallbackException exception)
        {
            issues.Add(WoundIssue(
                path,
                "wound_materialization_snapshot_encoding_invalid",
                "strict UTF-8 JSON bytes in the validated pending-turn snapshot",
                exception.GetType().Name,
                IssueCategory.ClientOwnedSurface));
            return null;
        }
    }

    private static string ComputeAcceptedWoundCarrierAuthority(
        WoundCarrierCatalogInput carriers)
    {
        ArgumentNullException.ThrowIfNull(carriers);
        return new JsonObject
        {
            ["player"] = carriers.PlayerWounds?.DeepClone(),
            ["npcs"] = carriers.NpcWounds?.DeepClone(),
            ["enemies"] = carriers.EnemyCombatants?.DeepClone(),
            ["allies"] = carriers.AllyCombatants?.DeepClone(),
            ["afterlifeProfiles"] = carriers.AfterlifeProfiles?.DeepClone()
        }.ToJsonString();
    }

    private static JsonObject? ParseWoundCarrierRoot(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json is null)
            return null;
        try
        {
            using var document = JsonDocument.Parse(json);
            var rootElement = document.RootElement;
            if (rootElement.ValueKind != JsonValueKind.Object)
                return AddInvalidRoot();

            var issueCount = issues.Count;
            ResourceMaterializationContract.FindDuplicateProperties(
                rootElement,
                path,
                issues,
                "wound_carrier_duplicate_property");
            if (issues.Count != issueCount)
                return null;

            return JsonNode.Parse(rootElement.GetRawText())!.AsObject();
        }
        catch (Exception exception) when (
            exception is JsonException or ArgumentException)
        {
            issues.Add(WoundIssue(
                path,
                "wound_carrier_invalid_json",
                "well-formed strict JSON object",
                exception.GetType().Name));
            return null;
        }

        JsonObject? AddInvalidRoot()
        {
            issues.Add(WoundIssue(
                path,
                "wound_carrier_invalid_root",
                "strict JSON object",
                "non-object root"));
            return null;
        }
    }

    private static ValidationIssue WoundIssue(
        string path,
        string code,
        string expected,
        string actual,
        IssueCategory category = IssueCategory.StateConsistency) => new(
            path,
            IssueSeverity.Error,
            "Wound materialization violates exact accepted-turn authority.",
            code: code,
            section: "wound_materialization",
            expected: expected,
            actual: actual,
            repairHint:
                "Restore the validated wound roots and use only the strict accepted wound command flow.",
            category: category);
}
