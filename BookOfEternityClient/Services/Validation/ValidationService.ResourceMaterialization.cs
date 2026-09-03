using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawResourceMaterializationAsync()
    {
        await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
        return await ValidateAcceptedTurnRawResourceMaterializationAsync(writeLease);
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawResourceMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var retainedTreatment = await
            ValidateRetainedMortalWoundTreatmentPublicationAsync(writeLease);
        if (retainedTreatment is not null)
            return retainedTreatment;

        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        var keepCommonHandoff = false;
        try
        {
            var issues = await ValidateAcceptedTurnRawResourceMaterializationCoreAsync(
                writeLease);
            keepCommonHandoff = AcceptedMechanicsPlanAuthority.HasValidated(
                _fs,
                writeLease);
            return issues;
        }
        finally
        {
            if (!keepCommonHandoff)
            {
                AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
            }
        }
    }

    private async Task<IReadOnlyList<ValidationIssue>?>
        ValidateRetainedMortalWoundTreatmentPublicationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(
                _fs,
                writeLease,
                out _,
                out var peeked) ||
            !peeked.Success ||
            peeked.Plan?.TreatmentResourcePublicationAuthority is not
                { } authority)
        {
            return null;
        }

        var issues = new List<ValidationIssue>();
        var authorityIssues = new List<ValidationIssue>();
        var finalization = MortalWoundTreatmentResourceComposer.Finalize(
            authority.ResolutionAuthority);
        authorityIssues.AddRange(finalization.Issues);
        if (finalization.Finalization is { } recomposedFinalization)
        {
            authorityIssues.AddRange(authority.ValidateCandidate(
                peeked.Plan,
                recomposedFinalization));
        }
        issues.AddRange(authorityIssues);

        foreach (var pair in peeked.Plan.BeforeImages.OrderBy(
                     static value => value.Key,
                     StringComparer.Ordinal))
        {
            var current = await _fs.ReadFileBytesAsync(writeLease, pair.Key);
            var expected = pair.Value.Bytes;
            var agrees = pair.Value.Existed == (current is not null) &&
                         (expected is null
                             ? current is null
                             : current is not null &&
                               expected.AsSpan().SequenceEqual(current));
            if (!agrees)
            {
                issues.Add(WoundIssue(
                    pair.Key,
                    "accepted_mechanics_wound_live_before_image_mismatch",
                    "the exact sealed Mortal wound-treatment publication before-image",
                    "canonical bytes changed after treatment-plan admission"));
            }
        }

        var exactConfirmedHold = false;
        if (authority.RequiresCoordinatedSettlement)
        {
            var hold = AcceptedTurnAuthorityRegistry
                .ProbeMortalWoundTreatmentResourcePublicationHold(
                    _fs,
                    writeLease,
                    authority.AcceptedStateAuthority,
                    authority.RequestAuthority,
                    authority.Finalization);
            issues.AddRange(hold.Issues);
            exactConfirmedHold = hold.IsValid &&
                                 hold.Issues.Count == 0 &&
                                 hold.ChangedCount == 0 &&
                                 hold.State ==
                                 MortalWoundTreatmentResourceReservationState
                                     .ConfirmedHeld &&
                                 string.Equals(
                                     hold.OperationKey,
                                     authority.RequestAuthority.Coordinates
                                         .OperationKey,
                                     StringComparison.Ordinal) &&
                                 string.Equals(
                                     hold.AttemptId,
                                     authority.RequestAuthority.Coordinates
                                         .AttemptId,
                                     StringComparison.Ordinal) &&
                                 string.Equals(
                                     hold.RequestFingerprint,
                                     authority.RequestFingerprint,
                                     StringComparison.Ordinal) &&
                                 string.Equals(
                                     hold.ResourceAuthorityFingerprint,
                                     authority.Finalization
                                         .ResourceAuthorityFingerprint,
                                     StringComparison.Ordinal) &&
                                 string.Equals(
                                     hold.FinalizationFingerprint,
                                     authority.FinalizationFingerprint,
                                     StringComparison.Ordinal);
        }

        var retainForExactTerminalSettlement =
            authority.RequiresCoordinatedSettlement &&
            authority.HasValidSeal() &&
            exactConfirmedHold &&
            !authorityIssues.Any(static issue =>
                issue.Severity == IssueSeverity.Error);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) &&
            !retainForExactTerminalSettlement)
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        }
        return issues;
    }

    private async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawResourceMaterializationCoreAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var commandJson = await _fs.ReadFileAsync(ResourceMaterializationContract.CommandPath);
        var definitionsJson = await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath);
        var stateJson = await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath);
        var historyJson = await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath);
        var pendingResolutionJson = await _fs.ReadFileAsync(
            ResourcePendingResolutionState.PendingPath);
        var fullPartyBytes = await _fs.ReadFileBytesAsync(FullPartyInteractionsPath);
        var fullPartyJson = fullPartyBytes == null
            ? null
            : DecodeResourceUtf8(fullPartyBytes);
        var fullParty = ParseFullPartyResourcePackets(
            fullPartyJson,
            FullPartyInteractionsPath);
        issues.AddRange(fullParty.Issues);

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            if (commandJson != null || definitionsJson != null || stateJson != null ||
                historyJson != null || pendingResolutionJson != null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.CommandPath,
                    "resource_materialization_snapshot_required",
                    "usable validated pending-turn snapshot",
                    lookup.Status.ToString()));
            }
            return issues;
        }

        var manifest = lookup.Manifest;
        var preTurnFullPartyJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            manifest,
            FullPartyInteractionsPath);
        var hasStagedFullPartyResourcePackets = fullParty.HasResourcePackets &&
            !string.Equals(
                fullPartyJson,
                preTurnFullPartyJson,
                StringComparison.Ordinal);
        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        issues.AddRange(definitionsResult.Issues);
        var definitions = definitionsResult.Catalog;
        ResourceStateContractResult? stateResult = null;
        ResourceHistoryStateResult? historyResult = null;
        ResourcePendingResolutionState? pendingResolutionState = null;
        if (definitions != null)
        {
            stateResult = ResourceStateContract.ParseCanonical(
                stateJson,
                definitions,
                allowMissingPristine: false);
            issues.AddRange(stateResult.Issues);
            historyResult = ResourceHistoryState.ParseCanonical(
                historyJson,
                definitions,
                allowMissingPristine: false);
            issues.AddRange(historyResult.Issues);
            if (stateResult.Ledger != null && historyResult.History != null)
                issues.AddRange(historyResult.History.ValidateStateAgreement(stateResult.Ledger));
            var pendingResult = ResourcePendingResolutionState.ParseCanonical(
                pendingResolutionJson,
                definitions,
                allowMissingPristine: true);
            issues.AddRange(pendingResult.Issues);
            pendingResolutionState = pendingResult.State;
        }

        await ValidateResourceSnapshotContinuityAsync(
            manifest,
            issues);
        var commands = ResourceAcceptedTurnInputComposer.Parse(commandJson);
        issues.AddRange(commands.Issues);
        var acceptedCommands = commands;
        if (commands.IsValid &&
            hasStagedFullPartyResourcePackets &&
            !issues.Any(static issue => issue.Severity == IssueSeverity.Error))
        {
            acceptedCommands = ComposeAcceptedResourceCommands(
                commands,
                fullParty.OrderedResourceChanges);
            issues.AddRange(acceptedCommands.Issues);
        }
        var events = ResourceAcceptedTurnInputComposer.BindAcceptedEvents(
            manifest.TurnNumber,
            acceptedCommands);
        issues.AddRange(events.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            definitions == null || stateResult?.Ledger == null ||
            historyResult?.History == null)
        {
            return issues;
        }

        var persistedOwnerAuthority = await
            CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                definitions,
                path => string.Equals(
                        path,
                        CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                        StringComparison.Ordinal)
                    ? _fs.ReadFileAsync(path)
                    : ReadValidatedPendingTurnSnapshotFileAsync(manifest, path),
                stateResult.Ledger,
                historyResult.History,
                CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        issues.AddRange(persistedOwnerAuthority.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;

        var acceptedItemOwners = MortalItemAcceptedTurnAuthority.GetValidatedOwners(
            _fs,
            writeLease,
            manifest.SessionId,
            manifest.ManifestPayloadHash);
        var missingGovernedItemIds =
            MortalItemAcceptedTurnAuthority.GetMissingGovernedItemIds(
                _fs,
                writeLease,
                manifest.SessionId,
                manifest.ManifestPayloadHash);
        var mortalOwnerComposition = MortalResourceOwnerComposer.Compose(
            new MortalResourceOwnerCompositionInput(
                definitions,
                await ReadMortalResourceOwnerRootsAsync(manifest, issues),
                await ReadMortalResourceOwnerRootsAsync(null, issues),
                BuildSameTurnResourceOwnerCapabilities(commands),
                acceptedItemOwners,
                missingGovernedItemIds));
        var preTurnAfterlifeOwners = await ReadAfterlifeResourceOwnerRootsAsync(
            manifest,
            issues);
        var acceptedAfterlifeOwners = await ReadAcceptedAfterlifeResourceOwnerRootsAsync(
            manifest,
            preTurnAfterlifeOwners,
            issues);
        var afterlifeOwnerComposition = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                preTurnAfterlifeOwners,
                acceptedAfterlifeOwners));
        var ownerComposition = ResourceOwnerComposition.Combine(
            mortalOwnerComposition,
            afterlifeOwnerComposition);
        issues.AddRange(ownerComposition.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            ownerComposition.Authority == null)
        {
            return issues;
        }

        var owners = ownerComposition.Authority;
        issues.AddRange(owners.ValidateCanonicalAgreement(
            stateResult.Ledger,
            historyResult.History,
            ownerComposition.TerminalOwners));
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;

        var effectCommandJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath);
        var isTerminalReceiptReplay = IsTerminalEffectReceiptReplay(
            effectCommandJson,
            pendingResolutionState);
        var effectIssues = new List<ValidationIssue>();
        var woundHandoffSink = new AcceptedTurnWoundHandoffSink();
        await ValidateAcceptedTurnRawEffectMaterializationAsync(
            effectIssues,
            ownerComposition,
            definitions,
            stateResult.Ledger,
            isTerminalReceiptReplay,
            manifest,
            writeLease,
            woundHandoffSink);
        issues.AddRange(effectIssues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;
        var woundHandoff = woundHandoffSink.Value;

        EffectAcceptedTurnPlan? effectPlan = null;
        if (EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                _fs,
                writeLease,
                out var effectBinding,
                out var effectResult))
        {
            if (!string.Equals(
                    effectBinding.SessionId,
                    manifest.SessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    effectBinding.SnapshotToken,
                    manifest.ManifestPayloadHash,
                    StringComparison.Ordinal))
            {
                EffectAcceptedTurnPlanAuthority.InvalidateValidated(
                    _fs,
                    writeLease);
                issues.Add(ResourceIssue(
                    EffectAcceptedTurnPlan.CommandPath,
                    "effect_materialization_snapshot_binding_mismatch",
                    "effect subplan bound to the exact validated resource session and snapshot",
                    $"{effectBinding.SessionId}/{effectBinding.SnapshotToken}"));
                return issues;
            }
            if (!effectResult.Success || effectResult.Plan == null)
            {
                issues.AddRange(effectResult.Issues);
                return issues;
            }
            effectPlan = effectResult.Plan;
        }
        var registeredSystemOutcomes = await ComposeRegisteredResourceOutcomesAsync(
            manifest,
            preTurnAfterlifeOwners,
            acceptedAfterlifeOwners,
            ownerComposition,
            stateResult.Ledger,
            issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;
        if (commands.IsMissing && effectPlan == null &&
            !hasStagedFullPartyResourcePackets &&
            registeredSystemOutcomes.Count == 0 &&
            ownerComposition.CapacityDrafts.Count == 0 &&
            ownerComposition.TerminalOwners.Count == 0 &&
            ownerComposition.OwnerCompanionAfterImages.Count == 0 &&
            ownerComposition.OwnerTransitions.Count == 0 &&
            !acceptedItemOwners.Any(static owner => owner.SameTurn) &&
            woundHandoff is null)
            return issues;

        var requestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (!TryParseResourceTurnRequest(
                requestJson,
                manifest.SessionId,
                manifest.RequestId,
                manifest.TurnNumber,
                out var request,
                out var realm))
        {
            issues.Add(ResourceIssue(
                "input/turn_request.json",
                "resource_materialization_turn_authority_invalid",
                "exact pending snapshot session/request/turn authority",
                "missing or mismatched request"));
            return issues;
        }

        var ordinarySources = ResourceAcceptedTurnInputComposer.ComposeOrdinarySources(
            manifest.SessionId,
            manifest.RequestId,
            manifest.TurnNumber,
            realm,
            commands,
            events,
            owners);
        issues.AddRange(ordinarySources.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;

        var sourcesResult = ResourceMutationSourceCatalog.Create(
            registeredSystemOutcomes
                .SelectMany(static outcome => outcome.SourceExports)
                .Concat(BuildItemResourceSourceExports(owners))
                .Concat(ordinarySources.Exports));
        issues.AddRange(sourcesResult.Issues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) ||
            sourcesResult.Catalog == null)
        {
            return issues;
        }

        var effectIdentityJson = await _fs.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath);
        JsonObject effectIdentity;
        if (effectPlan != null)
        {
            effectIdentity = effectPlan.IdentityIndexBeforeImage?.DeepClone().AsObject() ??
                EmptyEffectIdentityRoot();
        }
        else if (effectIdentityJson == null)
        {
            effectIdentity = EmptyEffectIdentityRoot();
        }
        else if (!TryParseStrictResourceObject(effectIdentityJson, out effectIdentity))
        {
            issues.Add(ResourceIssue(
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "resource_materialization_effect_index_invalid",
                "strict effect identity object or proven pristine absence",
                "malformed or non-object root"));
            return issues;
        }

        var pendingInput = pendingResolutionState?.ToCanonicalRoot() ?? new JsonObject();
        var effectCommands = effectCommandJson == null
            ? EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot()
            : ParseStrictObjectOrEmpty(effectCommandJson);
        var internalInputs = new JsonObject
        {
            ["definitions"] = JsonNode.Parse(definitionsJson!)!.AsObject(),
            ["state"] = JsonNode.Parse(stateJson!)!.AsObject(),
            ["history"] = JsonNode.Parse(historyJson!)!.AsObject(),
            ["ownerFingerprint"] = owners.Fingerprint,
            ["sourceFingerprint"] = sourcesResult.Catalog.Fingerprint,
            ["ownerCapacityDrafts"] = BuildOwnerCapacityDraftInput(
                ownerComposition.CapacityDrafts),
            ["terminalOwners"] = BuildTerminalOwnerInput(
                ownerComposition.TerminalOwners),
            ["ownerTransitions"] = new JsonArray(ownerComposition.OwnerTransitions
                .Select(static value => (JsonNode)value.ToFingerprintNode())
                .ToArray()),
            ["registeredSystemOutcomes"] = new JsonArray(registeredSystemOutcomes
                .OrderBy(static value => value.Fingerprint, StringComparer.Ordinal)
                .Select(static value =>
                    (JsonNode)JsonValue.Create(value.Fingerprint)!)
                .ToArray()),
            ["fullPartyResourcePackets"] = hasStagedFullPartyResourcePackets
                ? new JsonObject
                {
                    ["sessionId"] = manifest.SessionId,
                    ["requestId"] = manifest.RequestId,
                    ["turn"] = manifest.TurnNumber,
                    ["packets"] = fullParty.FingerprintRoot
                }
                : new JsonObject(),
            ["woundStage"] = woundHandoff is null
                ? new JsonObject()
                : new JsonObject
                {
                    ["inputFingerprint"] =
                        woundHandoff.StageBundle.InputFingerprint,
                    ["bundleFingerprint"] =
                        woundHandoff.StageBundle.BundleFingerprint
                }
        };
        var beforePaths = new HashSet<string>(StringComparer.Ordinal)
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            ResourceMaterializationContract.CommandPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            "input/turn_request.json",
            PendingTurnSnapshotManifestPath,
            PendingTurnSnapshotAuthority.AuthorityPath,
            ResourcePendingResolutionState.PendingPath
        };
        if (effectPlan != null)
        {
            beforePaths.UnionWith(effectPlan.TouchedPaths);
            beforePaths.UnionWith(effectPlan.DeletedPaths);
            beforePaths.UnionWith(effectPlan.CarrierBeforeImages.Keys);
        }
        if (woundHandoff is not null)
        {
            beforePaths.UnionWith(
                WoundAcceptedTurnSnapshotContract.RequiredPaths);
        }
        beforePaths.UnionWith(ownerComposition.OwnerCompanionAfterImages.Keys);
        beforePaths.UnionWith(ownerComposition.OwnerTransitions.Select(static value => value.Path));
        beforePaths.UnionWith(acceptedItemOwners.Select(static owner => owner.FilePath));
        foreach (var outcome in registeredSystemOutcomes)
            beforePaths.UnionWith(outcome.ExpectedBeforeImages.Keys);
        if (hasStagedFullPartyResourcePackets)
            beforePaths.Add(FullPartyInteractionsPath);
        var beforeImages = (await CaptureResourceBeforeImagesAsync(beforePaths))
            .ToDictionary(static pair => pair.Key, static pair => pair.Value, StringComparer.Ordinal);
        if (hasStagedFullPartyResourcePackets)
        {
            beforeImages[FullPartyInteractionsPath] = new CanonicalBeforeImage(
                existed: true,
                fullPartyBytes!);
        }
        foreach (var outcome in registeredSystemOutcomes)
        {
            foreach (var pair in outcome.ExpectedBeforeImages)
                beforeImages[pair.Key] = pair.Value;
        }
        var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
            Definitions: HashText("resource-definitions-v1", definitionsJson!),
            Owners: owners.Fingerprint,
            ResourceState: stateResult.Ledger.Fingerprint,
            ResourceHistory: historyResult.History.Fingerprint,
            EffectSources: HashText(
                "accepted-mechanics-effect-sources-v1",
                effectPlan?.SourceAuthorityFingerprint ?? "<missing>"),
            EffectTargets: HashText(
                "accepted-mechanics-effect-targets-v1",
                effectPlan?.TargetAuthorityFingerprint ?? "<missing>"),
            EffectCarriers: HashText(
                "accepted-mechanics-effect-carriers-v1",
                effectPlan?.CarrierAuthorityFingerprint ?? "<missing>"),
            EffectIdentityIndex: HashText(
                "effect-index-v1",
                effectIdentityJson ?? "<missing>"),
            AcceptedEvents: HashNode("resource-events-v1", events.Root),
            Commands: HashText(
                "accepted-mechanics-commands-v1",
                acceptedCommands.Root.ToJsonString() + "\n" +
                (effectCommandJson ?? "<missing>") + "\n" +
                (woundHandoff?.Commands.ToJsonString() ?? "<missing>")),
            Pending: HashNode("accepted-mechanics-pending-v1", pendingInput),
            InternalInputs: HashNode("accepted-mechanics-internal-v1", internalInputs),
            WoundCarriers: HashText(
                "accepted-mechanics-wound-carriers-v1",
                woundHandoff is null
                    ? "<missing>"
                    : ComputeAcceptedWoundCarrierAuthority(
                        woundHandoff.Input.PreTurnCarriers)),
            WoundIdentityIndex: HashText(
                "accepted-mechanics-wound-index-v1",
                woundHandoff?.Input.PreTurnIdentityIndex.ToJsonString() ??
                "<missing>"),
            WoundHistory: HashText(
                "accepted-mechanics-wound-history-v1",
                woundHandoff?.Input.PreTurnHistory.ToJsonString() ??
                "<missing>"));
        var planningCommands = isTerminalReceiptReplay
            ? ResourceAcceptedTurnInputComposer.Parse("{}")
            : commands;
        var context = new AcceptedMechanicsPlanningContext(
            internalInputs["definitions"]!.AsObject(),
            definitions,
            stateResult.Ledger,
            historyResult.History,
            owners,
            sourcesResult.Catalog,
            planningCommands,
            effectIdentity,
            effectPlan,
            ownerCapacityDrafts: ownerComposition.CapacityDrafts,
            terminalOwners: ownerComposition.TerminalOwners,
            ownerCompanionAfterImages: ownerComposition.OwnerCompanionAfterImages,
            ownerTransitions: ownerComposition.OwnerTransitions,
            registeredSystemOutcomes: registeredSystemOutcomes,
            pendingResolutionState: pendingResolutionState,
            woundStageBundle: woundHandoff?.StageBundle);
        var input = new AcceptedMechanicsInput(
            manifest.SessionId,
            manifest.RequestId,
            manifest.ManifestPayloadHash,
            realm,
            manifest.TurnNumber,
            events.Root,
            acceptedCommands.Root,
            effectCommands,
            pendingInput,
            internalInputs,
            fingerprints,
            beforeImages,
            issues,
            PlanningContext: context,
            WoundCommands: woundHandoff?.Commands,
            WoundInput: woundHandoff?.Input);
        var result = woundHandoff?.StageBundle is { } woundStageBundle &&
                     MortalWoundCanonicalAnchorPlan.RequiresInitialCreateAnchors(
                         woundStageBundle)
            ? AcceptedMechanicsPlanAuthority.GetOrBuildWoundValidated(
                _fs,
                writeLease,
                input,
                woundStageBundle)
            : AcceptedMechanicsPlanAuthority.GetOrBuildValidated(
                _fs,
                writeLease,
                input);
        issues.AddRange(result.Issues);
        if (result.Success &&
            result.Plan?.WoundStageBundle is not null)
        {
            var requiredSnapshotPaths =
                WoundAcceptedTurnSnapshotContract.BuildRequiredPaths(
                    result.Plan.TouchedPaths);
            if (!PendingTurnSnapshotAuthority.HasValidatedRollbackSnapshotCoverage(
                    manifest,
                    static value => value.Files,
                    static value => value.SnapshotFileHashes,
                    static value => value.RollbackBaselineFiles,
                    requiredSnapshotPaths,
                    out var missingSnapshotPath))
            {
                AcceptedMechanicsPlanAuthority.InvalidateValidated(
                    _fs,
                    writeLease);
                issues.Add(WoundIssue(
                    missingSnapshotPath ?? AcceptedMechanicsPlan.WoundCommandPath,
                    "wound_materialization_snapshot_before_image_missing",
                    "exact signed rollback snapshot evidence for every dynamically touched wound-plan path",
                    "missing, contradictory, or incompletely registered snapshot before-image",
                    IssueCategory.ClientOwnedSurface));
            }
        }
        return issues;
    }

    private static ResourceCommandCompositionResult ComposeAcceptedResourceCommands(
        ResourceCommandCompositionResult localCommands,
        IReadOnlyList<JsonObject> remoteResourceChanges)
    {
        ArgumentNullException.ThrowIfNull(localCommands);
        ArgumentNullException.ThrowIfNull(remoteResourceChanges);
        var root = localCommands.Root;
        var changes = root["resourceChanges"] as JsonArray ?? new JsonArray();
        root["resourceChanges"] = changes;
        foreach (var remoteChange in remoteResourceChanges)
            changes.Add(remoteChange.DeepClone());
        return ResourceAcceptedTurnInputComposer.Parse(root.ToJsonString());
    }

    private static bool IsTerminalEffectReceiptReplay(
        string? effectCommandJson,
        ResourcePendingResolutionState? pendingState)
    {
        if (effectCommandJson == null ||
            pendingState == null ||
            pendingState.Requests.Count != 0 ||
            pendingState.TerminalReceipts.Count == 0)
        {
            return false;
        }
        try
        {
            if (JsonNode.Parse(effectCommandJson) is not JsonObject root ||
                (root.ContainsKey("effectChanges") &&
                 root["effectChanges"] is not JsonArray) ||
                (root.ContainsKey(EffectAcceptedEventReportCatalog.ResponseField) &&
                 (root[EffectAcceptedEventReportCatalog.ResponseField] is not JsonArray reports ||
                  reports.Count != 0)) ||
                root["effectResolutionReceipts"] is not JsonArray receipts ||
                receipts.Count == 0)
            {
                return false;
            }
            var terminalIds = pendingState.TerminalReceipts
                .Select(static value => value.RequestId)
                .ToHashSet(StringComparer.Ordinal);
            return receipts.OfType<JsonObject>().Count() == receipts.Count &&
                receipts.OfType<JsonObject>().All(receipt =>
                    receipt["requestId"] is JsonValue requestIdNode &&
                    requestIdNode.TryGetValue<string>(out var requestId) &&
                    terminalIds.Contains(requestId));
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException or ArgumentException)
        {
            return false;
        }
    }

    private async Task<MortalResourceOwnerRoots> ReadMortalResourceOwnerRootsAsync(
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null) =>
        new(
            await ReadMortalResourceOwnerRootAsync(
                "game_state/npcs/npc_core.json",
                "NPCsInScene",
                manifest,
                issues,
                writeLease),
            await ReadMortalResourceOwnerRootAsync(
                EffectCarrierCatalog.EnemiesPath,
                "enemiesData",
                manifest,
                issues,
                writeLease),
            await ReadMortalResourceOwnerRootAsync(
                EffectCarrierCatalog.AlliesPath,
                "alliesData",
                manifest,
                issues,
                writeLease),
            await ReadMortalResourceOwnerRootAsync(
                StorageTransportMoveService.VehiclesPath,
                "vehicles",
                manifest,
                issues,
                writeLease));

    private async Task<AfterlifeResourceOwnerRoots> ReadAfterlifeResourceOwnerRootsAsync(
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null) =>
        new(
            await ReadAfterlifeResourceOwnerRootAsync(
                AfterlifeEntityProfileState.StatePath,
                AfterlifeEntityProfileState.CreateDefaultRoot,
                manifest,
                issues,
                writeLease),
            await ReadAfterlifeResourceOwnerRootAsync(
                AfterlifeSpiritualConflictState.StatePath,
                AfterlifeSpiritualConflictState.CreateDefaultRoot,
                manifest,
                issues,
                writeLease),
            await ReadAfterlifeResourceOwnerRootAsync(
                "game_state/meta/soul_state.json",
                static () => new JsonObject(),
                manifest,
                issues,
                writeLease),
            await ReadAfterlifeResourceOwnerRootAsync(
                ShiningAbodeState.StatePath,
                ShiningAbodeState.CreateDefaultState,
                manifest,
                issues,
                writeLease),
            await ReadAfterlifeResourceOwnerRootAsync(
                "game_state/meta/guardians.json",
                static () => new JsonObject(),
                manifest,
                issues,
                writeLease));

    private async Task<AfterlifeResourceOwnerRoots>
        ReadAcceptedAfterlifeResourceOwnerRootsAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            AfterlifeResourceOwnerRoots preTurn,
            List<ValidationIssue> issues)
    {
        var current = await ReadAfterlifeResourceOwnerRootsAsync(
            manifest: null,
            issues);
        var profiles = AfterlifeEntityProfileState.ProjectCanonicalRoot(
            current.Profiles,
            preTurn.Profiles);
        var conflict = current.SpiritualConflict;
        if (conflict[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update)
        {
            conflict = AfterlifeSpiritualConflictState.ApplyUpdate(
                preTurn.SpiritualConflict,
                update);
        }
        else
        {
            conflict.Remove(AfterlifeSpiritualConflictState.ResponseField);
        }

        return new AfterlifeResourceOwnerRoots(
            profiles,
            conflict,
            current.SoulState,
            current.ShiningAbode,
            current.Guardians);
    }

    private async Task<JsonObject> ReadAfterlifeResourceOwnerRootAsync(
        string path,
        Func<JsonObject> missingFactory,
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease)
    {
        var json = manifest == null
            ? writeLease == null
                ? await _fs.ReadFileAsync(path)
                : await _fs.ReadFileAsync(writeLease, path)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path);
        if (json == null)
            return missingFactory();
        if (TryParseStrictResourceObject(json, out var root))
            return root;

        issues.Add(ResourceIssue(
            path,
            "resource_owner_afterlife_root_invalid",
            "strict object root or proven pristine absence",
            "malformed or non-object afterlife owner root"));
        return missingFactory();
    }

    private async Task<JsonObject> ReadMortalResourceOwnerRootAsync(
        string path,
        string canonicalCollection,
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null)
    {
        var json = manifest == null
            ? writeLease == null
                ? await _fs.ReadFileAsync(path)
                : await _fs.ReadFileAsync(writeLease, path)
            : await ReadValidatedPendingTurnSnapshotFileAsync(manifest, path);
        if (json == null)
            return new JsonObject { [canonicalCollection] = new JsonArray() };
        if (TryParseStrictResourceObject(json, out var root))
            return root;

        issues.Add(ResourceIssue(
            path,
            "resource_owner_root_invalid",
            "strict object root or proven pristine absence",
            "malformed or non-object owner root"));
        return new JsonObject { [canonicalCollection] = new JsonArray() };
    }

    private static IReadOnlyDictionary<ResourceOwnerKind, IReadOnlyList<string>>
        BuildSameTurnResourceOwnerCapabilities(ResourceCommandCompositionResult commands)
    {
        var capabilities = new Dictionary<ResourceOwnerKind, HashSet<string>>();
        foreach (var creation in commands.DefinitionCreations)
        {
            if (creation.Definition["resourceKey"] is not JsonValue keyNode ||
                !keyNode.TryGetValue<string>(out var resourceKey) ||
                !ResourceMaterializationContract.IsExactIdentifier(resourceKey) ||
                creation.Definition["allowedOwnerKinds"] is not JsonArray kinds)
            {
                continue;
            }

            foreach (var kindNode in kinds.OfType<JsonValue>())
            {
                if (!kindNode.TryGetValue<string>(out var token) ||
                    !ResourceDefinitionCatalog.TryParseOwnerKind(token, out var ownerKind))
                {
                    continue;
                }
                if (!capabilities.TryGetValue(ownerKind, out var ownerCapabilities))
                {
                    ownerCapabilities = new HashSet<string>(StringComparer.Ordinal);
                    capabilities.Add(ownerKind, ownerCapabilities);
                }
                ownerCapabilities.Add(resourceKey);
            }
        }

        return capabilities.ToDictionary(
            static pair => pair.Key,
            static pair => (IReadOnlyList<string>)pair.Value
                .OrderBy(static value => value, StringComparer.Ordinal)
                .ToArray());
    }

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalResourceMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        await ValidateCanonicalResourceRootsAsync(null, issues);
        return issues;
    }

    internal async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnCanonicalResourceMaterializationAsync(
            FileSystemManager.CanonicalWriteLease writeLease)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        var issues = new List<ValidationIssue>();
        await ValidateCanonicalResourceRootsAsync(writeLease, issues);
        return issues;
    }

    private async Task ValidateCanonicalResourceRootsAsync(
        FileSystemManager.CanonicalWriteLease? writeLease,
        List<ValidationIssue> issues)
    {
        var definitionsJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.DefinitionsPath);
        var stateJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.StatePath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.StatePath);
        var historyJson = writeLease == null
            ? await _fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath)
            : await _fs.ReadFileAsync(writeLease, ResourceMaterializationContract.HistoryPath);
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        issues.AddRange(definitions.Issues);
        if (definitions.Catalog == null)
        {
            if (stateJson == null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.StatePath,
                    "resource_state_root_missing",
                    "present canonical resource state root",
                    "missing"));
            }
            if (historyJson == null)
            {
                issues.Add(ResourceIssue(
                    ResourceMaterializationContract.HistoryPath,
                    "resource_history_root_missing",
                    "present canonical resource history root",
                    "missing"));
            }
            return;
        }
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        issues.AddRange(state.Issues);
        issues.AddRange(history.Issues);
        if (state.Ledger != null && history.History != null)
        {
            issues.AddRange(history.History.ValidateStateAgreement(state.Ledger));
            var ownerComposition = await
                CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                    definitions.Catalog,
                    path => writeLease == null
                        ? _fs.ReadFileAsync(path)
                        : _fs.ReadFileAsync(writeLease, path),
                    state.Ledger,
                    history.History,
                    CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
            issues.AddRange(ownerComposition.Issues);
            if (ownerComposition.Authority != null)
            {
                issues.AddRange(ownerComposition.Authority.ValidateCanonicalAgreement(
                    state.Ledger,
                    history.History));
            }
        }
        var commandExists = writeLease == null
            ? _fs.FileExists(ResourceMaterializationContract.CommandPath)
            : _fs.FileExists(writeLease, ResourceMaterializationContract.CommandPath);
        if (commandExists)
        {
            issues.Add(ResourceIssue(
                ResourceMaterializationContract.CommandPath,
                "resource_materialization_command_not_consumed",
                "absent consumed command root after canonical publication",
                "present"));
        }
    }

    private async Task ValidateResourceSnapshotContinuityAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues)
    {
        await Compare(ResourceMaterializationContract.DefinitionsPath,
            "resource_materialization_direct_definition_mutation");
        await Compare(ResourceMaterializationContract.StatePath,
            "resource_materialization_direct_state_mutation");
        await Compare(ResourceMaterializationContract.HistoryPath,
            "resource_materialization_direct_history_mutation");
        await Compare(CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            "resource_materialization_direct_owner_authority_mutation");
        return;

        async Task Compare(string path, string code)
        {
            var current = await _fs.ReadFileBytesAsync(path);
            byte[]? previous = null;
            if (manifest.Files.TryGetValue(path, out var snapshotPath) &&
                !string.IsNullOrWhiteSpace(snapshotPath))
            {
                previous = await _fs.ReadFileBytesAsync(snapshotPath);
            }
            if (previous == null
                    ? current == null
                    : current != null && previous.AsSpan().SequenceEqual(current))
            {
                return;
            }
            issues.Add(ResourceIssue(
                path,
                code,
                "exact validated pre-turn bytes and prior existence",
                previous == null
                    ? "created after snapshot"
                    : current == null
                        ? "deleted after snapshot"
                        : "changed after snapshot"));
        }
    }

    private async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>>
        CaptureResourceBeforeImagesAsync(IEnumerable<string> paths)
    {
        var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            var bytes = await _fs.ReadFileBytesAsync(path);
            result[path] = bytes == null
                ? new CanonicalBeforeImage(false, null)
                : new CanonicalBeforeImage(true, bytes);
        }
        return result;
    }

    private async Task<IReadOnlyList<IResourceRegisteredSystemOutcomeDraft>>
        ComposeRegisteredResourceOutcomesAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            AfterlifeResourceOwnerRoots preTurnAfterlifeOwners,
            AfterlifeResourceOwnerRoots acceptedAfterlifeOwners,
            ResourceOwnerCompositionResult ownerComposition,
            ResourceStateLedger state,
            List<ValidationIssue> issues)
    {
        var outcomes = new List<IResourceRegisteredSystemOutcomeDraft>();
        var owners = ownerComposition.Authority!;
        var conflictBuild = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
            manifest.TurnNumber,
            preTurnAfterlifeOwners.SpiritualConflict,
            acceptedAfterlifeOwners.SpiritualConflict,
            owners,
            state);
        issues.AddRange(conflictBuild.Issues);
        if (conflictBuild.Draft != null)
            outcomes.Add(conflictBuild.Draft);

        var acceptedShining = ownerComposition.OwnerCompanionAfterImages.TryGetValue(
            ShiningAbodeState.StatePath,
            out var shiningAfterImage)
            ? shiningAfterImage
            : acceptedAfterlifeOwners.ShiningAbode;
        var shiningBytes = await _fs.ReadFileBytesAsync(ShiningAbodeState.StatePath);
        if (shiningBytes != null)
        {
            var shiningBuild = AfterlifeShiningGachaResourceOutcome.TryCreate(
                manifest.TurnNumber,
                preTurnAfterlifeOwners.ShiningAbode,
                acceptedShining,
                owners,
                state,
                ownerComposition.CapacityDrafts,
                new CanonicalBeforeImage(true, shiningBytes));
            issues.AddRange(shiningBuild.Issues);
            if (shiningBuild.Draft != null)
                outcomes.Add(shiningBuild.Draft);
        }

        const string guardiansPath = "game_state/meta/guardians.json";
        var guardiansBytes = await _fs.ReadFileBytesAsync(guardiansPath);
        if (guardiansBytes != null)
        {
            var guardianBuild = AfterlifeGuardianGachaResourceOutcome
                .TryCreate(
                    manifest.TurnNumber,
                    manifest.RequestTimestamp,
                    preTurnAfterlifeOwners.Guardians,
                    acceptedAfterlifeOwners.Guardians,
                    owners,
                    state,
                    ownerComposition.CapacityDrafts,
                    new CanonicalBeforeImage(true, guardiansBytes));
            issues.AddRange(guardianBuild.Issues);
            if (guardianBuild.Draft != null)
                outcomes.Add(guardianBuild.Draft);
        }

        const string soulPath = "game_state/meta/soul_state.json";
        const string worldEventsPath = "game_state/world/world_events.json";
        var currentSoulBytes = await _fs.ReadFileBytesAsync(soulPath);
        if (currentSoulBytes == null ||
            !TryParseStrictResourceObject(DecodeResourceUtf8(currentSoulBytes), out var currentSoul))
        {
            return outcomes;
        }

        var preTurnSoulJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, soulPath);
        var preTurnSoul = TryParseStrictResourceObject(preTurnSoulJson, out var parsedPreTurnSoul)
            ? parsedPreTurnSoul
            : null;
        var lifeTransitionsJson = await _fs.ReadFileAsync("game_state/control/life_transitions.json");
        JsonObject normalizedSoul;
        try
        {
            normalizedSoul = CanonicalStateNormalizer.BuildNormalizedSoulStateRoot(
                currentSoul,
                preTurnSoul,
                manifest.TurnNumber,
                CanonicalStateNormalizer.HasLifecycleAuthorizedTriggerLifeEnd(
                    lifeTransitionsJson,
                    preTurnSoul,
                    currentSoul),
                enforceStrictCanonicalRoots: true);
        }
        catch (InvalidOperationException exception)
        {
            issues.Add(ResourceIssue(
                soulPath,
                "shining_survival_soul_composition_invalid",
                "one valid normalized soul-state authority",
                exception.Message));
            return outcomes;
        }

        var currentWorldBytes = await _fs.ReadFileBytesAsync(worldEventsPath);
        if (currentWorldBytes == null ||
            !TryParseStrictResourceObject(
                DecodeResourceUtf8(currentWorldBytes),
                out var currentWorldEvents))
        {
            return outcomes;
        }
        var preTurnWorldEventsJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            manifest,
            worldEventsPath);
        JsonNode? preTurnWorldEvents = null;
        if (preTurnWorldEventsJson != null)
        {
            try
            {
                preTurnWorldEvents = JsonNode.Parse(preTurnWorldEventsJson);
            }
            catch (JsonException)
            {
                issues.Add(ResourceIssue(
                    worldEventsPath,
                    "shining_survival_world_snapshot_invalid",
                    "validated JSON world-event snapshot",
                    "malformed snapshot root"));
                return outcomes;
            }
        }

        var build = ShiningBlessingEffectState.TryCreateSurvivalResourceOutcomeDraft(
            manifest.TurnNumber,
            normalizedSoul,
            currentWorldEvents,
            preTurnWorldEvents,
            CanonicalStateNormalizer.CreateExpectedSoulNormalizationBeforeImage(
                currentSoulBytes,
                currentSoul,
                normalizedSoul),
            new CanonicalBeforeImage(true, currentWorldBytes),
            DateTime.UtcNow.ToString("o"));
        issues.AddRange(build.Issues);
        if (build.Draft != null)
            outcomes.Add(build.Draft);
        return outcomes;
    }

    private static IEnumerable<ResourceMutationSourceExport>
        BuildItemResourceSourceExports(ResourceOwnerAuthority owners)
    {
        foreach (var owner in owners.Entries.Values
                     .Where(static owner =>
                         owner.Key.OwnerKind == ResourceOwnerKind.Item &&
                         owner.Lifecycle == ResourceOwnerLifecycle.Active)
                     .OrderBy(static owner => owner.Key.Realm, StringComparer.Ordinal)
                     .ThenBy(static owner => owner.Key.ResourceOwnerId, StringComparer.Ordinal))
        {
            yield return new ResourceMutationSourceExport(
                "local_item_cost",
                owner.Key.ResourceOwnerId,
                owner.AuthorityFingerprint,
                ResourceMutationSourceState.Active,
                owner.SameTurn,
                owner.Key);
            yield return new ResourceMutationSourceExport(
                "local_item_outcome",
                owner.Key.ResourceOwnerId,
                owner.AuthorityFingerprint,
                ResourceMutationSourceState.Active,
                owner.SameTurn,
                owner.Key);
        }
    }

    private static string DecodeResourceUtf8(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private static JsonArray BuildOwnerCapacityDraftInput(
        IEnumerable<ResourceOwnerCapacityDraft> drafts) =>
        new(drafts
            .OrderBy(static draft => draft.Coordinate.Realm, StringComparer.Ordinal)
            .ThenBy(static draft => draft.Coordinate.OwnerKind)
            .ThenBy(static draft => draft.Coordinate.ResourceOwnerId, StringComparer.Ordinal)
            .ThenBy(static draft => draft.Coordinate.ResourceKey, StringComparer.Ordinal)
            .Select(draft =>
            {
                var capacity = draft.ResolvedCapacity.Capacity!;
                return (JsonNode)new JsonObject
                {
                    ["realm"] = draft.Coordinate.Realm,
                    ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(
                        draft.Coordinate.OwnerKind),
                    ["resourceOwnerId"] = draft.Coordinate.ResourceOwnerId,
                    ["resourceKey"] = draft.Coordinate.ResourceKey,
                    ["acceptedMaximum"] = draft.AcceptedMaximum,
                    ["capacityFingerprint"] = capacity.Binding.AuthorityFingerprint,
                    ["initializationFingerprint"] =
                        capacity.Initialization!.AuthorityFingerprint,
                    ["sourceKind"] = draft.SourceEvidence.SourceKind,
                    ["sourceId"] = draft.SourceEvidence.SourceId,
                    ["sourceFingerprint"] = draft.SourceEvidence.AuthorityFingerprint
                };
            })
            .ToArray());

    private static JsonArray BuildTerminalOwnerInput(
        IEnumerable<ResourceOwnerKey> owners) =>
        new(owners
            .OrderBy(static owner => owner.Realm, StringComparer.Ordinal)
            .ThenBy(static owner => owner.OwnerKind)
            .ThenBy(static owner => owner.ResourceOwnerId, StringComparer.Ordinal)
            .Select(owner => (JsonNode)new JsonObject
            {
                ["realm"] = owner.Realm,
                ["ownerKind"] = ResourceDefinitionCatalog.GetOwnerKindToken(owner.OwnerKind),
                ["resourceOwnerId"] = owner.ResourceOwnerId
            })
            .ToArray());

    private static bool TryParseResourceTurnRequest(
        string? json,
        string expectedSessionId,
        string expectedRequestId,
        int expectedTurn,
        out JsonObject root,
        out string realm)
    {
        root = new JsonObject();
        realm = "mortal_world";
        if (!TryParseStrictResourceObject(json, out root) ||
            !string.Equals(ReadExactResourceString(root["sessionId"]), expectedSessionId, StringComparison.Ordinal) ||
            !string.Equals(ReadExactResourceString(root["requestId"]), expectedRequestId, StringComparison.Ordinal) ||
            root["turnNumber"] is not JsonValue turnValue ||
            !turnValue.TryGetValue<int>(out var turn) || turn != expectedTurn)
        {
            return false;
        }

        var token = ReadExactResourceString(root["currentRealm"]) ??
                    ReadExactResourceString(root["progressionControl"]?["currentRealm"]);
        realm = token switch
        {
            "Chaos Sea" or "chaos_sea" => "chaos_sea",
            "Shining Abode" or "shining_abode" => "shining_abode",
            _ => "mortal_world"
        };
        return true;
    }

    private static JsonObject ParseStrictObjectOrEmpty(string json) =>
        TryParseStrictResourceObject(json, out var root) ? root : new JsonObject();

    private static bool TryParseStrictResourceObject(string? json, out JsonObject root)
    {
        root = new JsonObject();
        if (json == null)
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return false;
            var duplicateIssues = new List<ValidationIssue>();
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                "resourceInput",
                duplicateIssues,
                "resource_materialization_duplicate_property");
            if (duplicateIssues.Count != 0)
                return false;
            root = JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string? ReadExactResourceString(JsonNode? node) =>
        node is JsonValue value && value.TryGetValue<string>(out var text) &&
        ResourceMaterializationContract.IsExactIdentifier(text)
            ? text
            : null;

    private static JsonObject EmptyEffectIdentityRoot() => new()
    {
        ["schemaVersion"] = 1,
        ["entries"] = new JsonArray()
    };

    private static string HashNode(string domain, JsonNode node) =>
        HashText(domain, node.ToJsonString());

    private static string HashText(string domain, string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(domain + "\0" + value))).ToLowerInvariant();

    private static ValidationIssue ResourceIssue(
        string path,
        string code,
        string expected,
        string actual) => new(
            path,
            IssueSeverity.Error,
            "Resource materialization violates exact accepted-turn authority.",
            code: code,
            section: "resource_materialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore the validated pre-turn resource roots and express resource changes only through the strict resource command envelope.");
}
