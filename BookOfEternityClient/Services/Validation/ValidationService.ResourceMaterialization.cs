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
            FileSystemManager.CanonicalWriteLease writeLease,
            Func<FileSystemManager.CanonicalWriteLease, Task<IReadOnlyList<ValidationIssue>>>? stageMortalQteDecision = null,
            Func<FileSystemManager.CanonicalWriteLease, IReadOnlyDictionary<string, CanonicalBeforeImage>, Task<IReadOnlyList<ValidationIssue>>>? sealMortalQteDecision = null,
            Func<MortalWoundOpportunityReceiptConsumePlanResult?>? readMortalQteReplay = null,
            Func<FileSystemManager.CanonicalWriteLease, Task<IReadOnlyList<ValidationIssue>?>>? prepareMortalGuaranteedTreatment = null,
            Func<FileSystemManager.CanonicalWriteLease, Task<GameEngine.MortalJointRepairRawAdmission>>? inspectMortalJointRepair = null,
            bool deferSpiritualSelfTreatmentPublication = false,
            Func<FileSystemManager.CanonicalWriteLease, WoundApplicationService.MortalDiagnosisAdmission, Task<IReadOnlyList<ValidationIssue>>>? prepareMortalDiagnosis = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        await ResumeOwnedMortalDiagnosisRetryAsync(writeLease);
        var spiritualPublication = await TryPrepareSpiritualC4PublicationAsync(writeLease);
        if (spiritualPublication is not null)
            return spiritualPublication;
        InvalidateSpiritualWoundSourceSession();
        var retainedTreatment = await
            ValidateRetainedMortalWoundTreatmentPublicationAsync(writeLease);
        if (retainedTreatment is not null)
            return retainedTreatment;

        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        var spiritualTreatment = await TryPrepareSpiritualCycleAsync(writeLease,
            deferSpiritualSelfTreatmentPublication ? SpiritualSelfTreatmentPhase.DeferredRouting
                : SpiritualSelfTreatmentPhase.DirectHeldLease);
        if (spiritualTreatment is not null)
            return spiritualTreatment;
        var keepCommonHandoff = false;
        var keepSpiritualSourceHandoff = false;
        try
        {
            var issues = await ValidateAcceptedTurnRawResourceMaterializationCoreAsync(
                writeLease, stageMortalQteDecision: stageMortalQteDecision, sealMortalQteDecision: sealMortalQteDecision,
                readMortalQteReplay: readMortalQteReplay, prepareMortalGuaranteedTreatment: prepareMortalGuaranteedTreatment,
                inspectMortalJointRepair: inspectMortalJointRepair, prepareMortalDiagnosis: prepareMortalDiagnosis);
            keepCommonHandoff = AcceptedMechanicsPlanAuthority.HasValidated(
                _fs,
                writeLease);
            keepSpiritualSourceHandoff =
                !issues.Any(static issue => issue.Severity == IssueSeverity.Error);
            return issues;
        }
        finally
        {
            if (!keepSpiritualSourceHandoff)
                InvalidateSpiritualWoundSourceSession();
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

        await ValidateMortalDiagnosisDynamicCoverageAsync(writeLease, peeked.Plan, authority, issues);
        var diagnosisInvalid = authority.RequestAuthority.Mode == "diagnosis" &&
            issues.Any(value => value.Severity == IssueSeverity.Error);
        var retainForExactTerminalSettlement =
            authority.RequiresCoordinatedSettlement &&
            authority.HasValidSeal() &&
            exactConfirmedHold &&
            !authorityIssues.Any(static issue =>
                issue.Severity == IssueSeverity.Error);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error) &&
            (!retainForExactTerminalSettlement || diagnosisInvalid))
        {
            AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
            EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
        }
        return issues;
    }

    /// <summary>
    /// Validates original accepted resource inputs and either composes the common plan or retains a private pre-execution capture.
    /// </summary>
    /// <param name="writeLease">
    /// Active canonical write lease protecting validation and retained plan authority.
    /// </param>
    /// <param name="spiritualCapture">
    /// Private spiritual input sink; null follows ordinary planning unless a Mortal sink is supplied.
    /// </param>
    /// <param name="mortalCapture">
    /// Private Mortal input sink deferring wound preparation and common execution; null preserves ordinary behavior.
    /// </param>
    /// <param name="naturalOwner">Private ordinary recovery owner held through this complete common composition, or null for other families.</param>
    /// <returns>
    /// Validation diagnostics from the selected original-input pipeline.
    /// </returns>
    private async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawResourceMaterializationCoreAsync(
            FileSystemManager.CanonicalWriteLease writeLease,
            SpiritualOriginalInputSink? spiritualCapture = null,
            MortalOriginalInputSink? mortalCapture = null,
            Func<FileSystemManager.CanonicalWriteLease, Task<IReadOnlyList<ValidationIssue>>>? stageMortalQteDecision = null,
            Func<FileSystemManager.CanonicalWriteLease, IReadOnlyDictionary<string, CanonicalBeforeImage>, Task<IReadOnlyList<ValidationIssue>>>? sealMortalQteDecision = null,
            Func<MortalWoundOpportunityReceiptConsumePlanResult?>? readMortalQteReplay = null,
            Func<FileSystemManager.CanonicalWriteLease, Task<IReadOnlyList<ValidationIssue>?>>? prepareMortalGuaranteedTreatment = null,
            Func<FileSystemManager.CanonicalWriteLease, Task<GameEngine.MortalJointRepairRawAdmission>>? inspectMortalJointRepair = null,
            AfterlifeWoundHealingPlanner.Continuation? naturalOwner = null,
            Func<FileSystemManager.CanonicalWriteLease, WoundApplicationService.MortalDiagnosisAdmission, Task<IReadOnlyList<ValidationIssue>>>? prepareMortalDiagnosis = null)
    {
        ArgumentNullException.ThrowIfNull(writeLease);
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
        var issues = new List<ValidationIssue>();
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var currentInputs = spiritualCapture?.CurrentInputs;
        if (currentInputs is not null)
        {
            if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
                lookup.Manifest is not { } currentManifest ||
                !currentInputs.MatchesIdentity(currentManifest.SessionId,
                    currentManifest.RequestId, currentManifest.ManifestPayloadHash,
                    currentManifest.TurnNumber))
            {
                issues.Add(ResourceIssue(ResourceMaterializationContract.CommandPath,
                    "spiritual_original_input_identity_mismatch",
                    "the exact validated physical session, request, snapshot and positive turn",
                    "retained resource current-input identity mismatch"));
                return issues;
            }
            var selectedPaths = new[]
            {
                ResourceMaterializationContract.CommandPath,
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                ResourcePendingResolutionState.PendingPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                FullPartyInteractionsPath,
                NpcCoreChangesContract.NpcCorePath,
                EffectCarrierCatalog.EnemiesPath,
                EffectCarrierCatalog.AlliesPath,
                StorageTransportMoveService.VehiclesPath,
                AfterlifeEntityProfileState.StatePath,
                AfterlifeSpiritualConflictState.StatePath,
                SpiritualWoundSourceSession.SoulPath,
                ShiningAbodeState.StatePath,
                "game_state/meta/guardians.json",
                "game_state/control/life_transitions.json",
                "game_state/world/world_events.json"
            };
            try
            {
                foreach (var path in selectedPaths.Distinct(StringComparer.Ordinal))
                    _ = currentInputs.ReadImage(path);
            }
            catch (KeyNotFoundException exception)
            {
                issues.Add(ResourceIssue(ResourceMaterializationContract.CommandPath,
                    "spiritual_original_resource_input_unregistered",
                    "every selected current resource path retained by the original draft",
                    exception.Message));
                return issues;
            }
        }
        var commandJson = await ReadResourceCurrentTextAsync(ResourceMaterializationContract.CommandPath, currentInputs);
        var definitionsJson = await ReadResourceCurrentTextAsync(ResourceMaterializationContract.DefinitionsPath, currentInputs);
        var stateJson = await ReadResourceCurrentTextAsync(ResourceMaterializationContract.StatePath, currentInputs);
        var historyJson = await ReadResourceCurrentTextAsync(ResourceMaterializationContract.HistoryPath, currentInputs);
        var pendingResolutionJson = await ReadResourceCurrentTextAsync(ResourcePendingResolutionState.PendingPath, currentInputs);
        var fullPartyBytes = await ReadResourceCurrentBytesAsync(FullPartyInteractionsPath, currentInputs);
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

        var completedMortalReplay = false;
        if (stageMortalQteDecision is not null && currentInputs is null && spiritualCapture is null && mortalCapture is null)
        {
            // Read-only classification precedes staging and original-quartet continuity.
            // A current receipt alone cannot authorize changed canonical resource bytes.
            try
            {
                var draft = await _fs.ReadFileAsync(writeLease, MortalWoundOpportunityAdapter.DecisionPath);
                var narrative = await _fs.ReadFileAsync(writeLease, "output/narrative_response.json");
                var scene = narrative is null ? null : StrictJsonAuthority.Deserialize<JsonObject>(narrative.TrimStart('\uFEFF'),
                    Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed, "Mortal exact replay scene");
                var classified = MortalWoundOpportunityAdapter.ComposeQteAcceptedResponse(_fs, writeLease, draft,
                    scene?["response"]?.GetValue<string>(), out var exact);
                if (classified is { Success: true } && exact is not null)
                {
                    var canonicalReplayIssues = new List<ValidationIssue>();
                    await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(canonicalReplayIssues, writeLease);
                    await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(canonicalReplayIssues, writeLease);
                    if (canonicalReplayIssues.Any(static issue => issue.Severity == IssueSeverity.Error))
                    {
                        issues.AddRange(canonicalReplayIssues.Select(MortalWoundOpportunityAdapter.PrivateSourceIssue));
                        return issues;
                    }
                    await MortalQteDecisionPublication.ValidateExactReplayAsync(_fs, writeLease, exact);
                    completedMortalReplay = true;
                }
                else if (classified is not null && exact is null)
                    await MortalQteDecisionPublication.ValidateUnchangedMarkerBeforeAsync(_fs, writeLease);
            }
            catch (Exception failure) when (failure is JsonException or InvalidDataException or InvalidOperationException or DecoderFallbackException)
            {
                issues.Add(MortalWoundOpportunityAdapter.PrivateSourceIssue(new ValidationIssue(MortalQteDecisionPublication.StatePath,
                    IssueSeverity.Error, failure.Message, "mortal_qte_completed_publication_invalid")));
                return issues;
            }
        }
        if (!completedMortalReplay)
            await ValidateResourceSnapshotContinuityAsync(manifest, issues, currentInputs);
        var commands = ResourceAcceptedTurnInputComposer.Parse(commandJson);
        issues.AddRange(commands.Issues);
        if (naturalOwner?.IsDiagnosis == true && commands.IsValid &&
            (commands.ResourceChanges.Count != 0 || commands.CapacityChanges.Count != 0 || commands.DefinitionCreations.Count != 0 || hasStagedFullPartyResourcePackets))
        {
            issues.Add(ResourceIssue(ResourceMaterializationContract.CommandPath, "spiritual_diagnosis_cost_forbidden",
                "a guaranteed diagnosis without resource, item, capacity or party charges", "nonempty diagnostic resource work"));
            return issues;
        }
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
                    ? ReadResourceCurrentTextAsync(path, currentInputs)
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
            manifest.ManifestPayloadHash, spiritualCapture?.Allocations.Items);
        var missingGovernedItemIds =
            MortalItemAcceptedTurnAuthority.GetMissingGovernedItemIds(
                _fs,
                writeLease,
                manifest.SessionId,
                manifest.ManifestPayloadHash, spiritualCapture?.Allocations.Items);
        var mortalOwnerInput = new MortalResourceOwnerCompositionInput(
                definitions,
                await ReadMortalResourceOwnerRootsAsync(manifest, issues),
                await ReadMortalResourceOwnerRootsAsync(null, issues, currentInputs: currentInputs),
                BuildSameTurnResourceOwnerCapabilities(commands),
                acceptedItemOwners,
                missingGovernedItemIds);
        var mortalOwnerComposition = spiritualCapture is null
            ? MortalResourceOwnerComposer.Compose(mortalOwnerInput)
            : MortalResourceOwnerComposer.Compose(mortalOwnerInput,
                spiritualCapture.Allocations.Effects, spiritualCapture.Allocations.Vehicles);
        var preTurnAfterlifeOwners = await ReadAfterlifeResourceOwnerRootsAsync(
            manifest,
            issues);
        var acceptedAfterlifeOwners = await ReadAcceptedAfterlifeResourceOwnerRootsAsync(
            manifest,
            preTurnAfterlifeOwners,
            issues, spiritualCapture?.Allocations.Clock, currentInputs, naturalOwner);
        var afterlifeOwnerComposition = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                definitions,
                preTurnAfterlifeOwners,
                acceptedAfterlifeOwners));
        var ownerComposition = ResourceOwnerComposition.Combine(
            persistedOwnerAuthority.Authority!.ExportHistoricalOwners(),
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

        if (AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(
                preTurnAfterlifeOwners.SoulState["currentRealm"]?.GetValue<string>()) is
            "chaos_sea" or "shining_abode")
        {
            // The source owner performs its own signed read under this real lease.
            // These already-read owner DTOs are not spiritual source authority.
            var preparation = spiritualCapture == null
                ? await BeginSpiritualWoundSourceSessionAsync(writeLease)
                : await BeginLiveSpiritualWoundSourceSessionAsync(writeLease, spiritualCapture.AwaitOriginalPrefix,
                    spiritualCapture.Allocations.Clock, spiritualCapture.CurrentInputs);
            issues.AddRange(preparation.Issues);
            if (preparation.Session is not { } sourceSession ||
                sourceSession.SessionId != manifest.SessionId ||
                sourceSession.RequestId != manifest.RequestId ||
                sourceSession.SnapshotToken != manifest.ManifestPayloadHash ||
                sourceSession.TurnNumber != manifest.TurnNumber)
                issues.Add(SourceIssue(AfterlifeSpiritualConflictState.StatePath,
                    "spiritual_source_snapshot_binding_mismatch",
                    "source session must match the current resource snapshot"));
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                return issues;
        }
        var effectCommandJson = spiritualCapture?.CurrentInputs is { } effectCurrentInputs
            ? effectCurrentInputs.ReadText(EffectAcceptedTurnPlan.CommandPath)
            : await _fs.ReadFileAsync(EffectAcceptedTurnPlan.CommandPath);
        var isTerminalReceiptReplay = IsTerminalEffectReceiptReplay(
            effectCommandJson,
            pendingResolutionState);
        if (mortalCapture != null && (pendingResolutionState is { Requests.Count: > 0 } ||
            TryParseStrictResourceObject(effectCommandJson, out var mortalEffectCommands) &&
            mortalEffectCommands["effectResolutionReceipts"] is JsonArray { Count: > 0 }))
        {
            issues.Add(WoundIssue(ResourcePendingResolutionState.PendingPath,
                "mortal_original_pending_replay_unsupported", "a fixed original turn without active pending requests or incoming effect resolution receipts",
                "the ordinary pending-resolution owner must resolve this input"));
            return issues;
        }
        var effectIssues = new List<ValidationIssue>();
        if (currentInputs is null && spiritualCapture is null && mortalCapture is null)
        {
            var rawWound = await _fs.ReadFileAsync(writeLease, AcceptedMechanicsPlan.WoundCommandPath);
            WoundResponseCommandParsingResult? parsedWound = null;
            if (rawWound is not null)
            {
                try
                {
                    parsedWound = WoundResponseInputComposer.ParseCommandRoot(StrictJsonAuthority.Deserialize<JsonElement>(
                        rawWound.TrimStart('\uFEFF'), new JsonSerializerOptions(), "ordinary wound command"));
                }
                catch (Exception failure) when (failure is JsonException or InvalidDataException)
                {
                    // Classification grants no admission. Malformed/non-treatment input
                    // retains its existing QTE/generic parser diagnostics below.
                }
            }
            WoundApplicationService.MortalDiagnosisAdmission? diagnosis = null;
            if (IsMortalDiagnosisInput(rawWound))
            {
                if (prepareMortalDiagnosis is null)
                {
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath, "mortal_diagnosis_live_owner_unavailable",
                        "the actual retained diagnosis preparation callback", "primitive input alone cannot publish or allocate a procedure"));
                    return issues;
                }
                try { diagnosis = await ReadRetainedMortalDiagnosisAsync(writeLease, manifest); }
                catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
                    failure is (InvalidDataException or InvalidOperationException or ArgumentException or JsonException))
                {
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath, "mortal_diagnosis_retained_input_invalid",
                        "the current original input and exact pre-GM procedure hold", failure.Message));
                    return issues;
                }
            }
            var hasOriginalMortalOccurrence = false;
            if (diagnosis is not null ||
                (prepareMortalGuaranteedTreatment is not null || inspectMortalJointRepair is not null) && parsedWound?.TreatmentCommands.Count > 0)
            {
                if (commandJson is not null || effectCommandJson is not null || hasStagedFullPartyResourcePackets ||
                    pendingResolutionState is { Requests.Count: > 0 } || isTerminalReceiptReplay ||
                    ownerComposition.CapacityDrafts.Count != 0 || ownerComposition.TerminalOwners.Count != 0 || ownerComposition.OwnerTransitions.Count != 0)
                {
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_treatment_ordinary_concurrent_work_unsupported", "the proved-empty concurrent mechanical contour",
                        "raw resource/effect/party/pending or owner work requires its ordinary owner"));
                    return issues;
                }
                foreach (var companion in ownerComposition.OwnerCompanionAfterImages)
                {
                    var physical = await _fs.ReadFileAsync(writeLease, companion.Key);
                    if (physical is null || !JsonNode.DeepEquals(companion.Value,
                            StrictJsonAuthority.Deserialize<JsonNode>(physical.TrimStart('\uFEFF'), new JsonSerializerOptions(), companion.Key)))
                        issues.Add(WoundIssue(companion.Key, "mortal_wound_treatment_ordinary_concurrent_work_unsupported",
                            "no effective new owner companion image", "the early treatment return cannot omit a changed owner image"));
                }
                // Reuse the original-bound READ-ONLY wound admission (including rollback,
                // client-owned baselines and occurrence/receipt/history agreement), not its
                // later Prepare/effect/Finalize reducer.
                var admittedWound = await LoadAcceptedTurnRawWoundDraftAsync(manifest, writeLease, rawWound!, issues, diagnosis);
                if (admittedWound is null)
                {
                    if (!issues.Any(static issue => issue.Severity == IssueSeverity.Error))
                        issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath, "mortal_wound_treatment_ordinary_intake_unavailable",
                            "one original-bound read-only wound intake", "the treatment owner has no admitted wound input"));
                    return issues;
                }
                hasOriginalMortalOccurrence = admittedWound.SignedOccurrences.Occurrences.Count != 0;
                if (hasOriginalMortalOccurrence && (diagnosis is not null || inspectMortalJointRepair is null))
                    issues.Add(WoundIssue(MortalWoundOccurrenceState.StatePath,
                        "mortal_wound_treatment_ordinary_concurrent_work_unsupported", "no competing unconsumed wound occurrence",
                        "the occurrence/decision owner must handle this original input"));
                if (issues.Any(static issue => issue.Severity == IssueSeverity.Error)) return issues;
                var originalEffects = await ReadSnapshotEffectCarriersAsync(manifest, issues);
                var currentEffects = await ReadEffectCarriersAsync(writeLease, issues);
                ValidateEffectOwnedContinuity(originalEffects, currentEffects,
                    await ReadValidatedPendingTurnSnapshotFileAsync(manifest, EffectAcceptedTurnPlan.IdentityIndexPath),
                    await _fs.ReadFileAsync(writeLease, EffectAcceptedTurnPlan.IdentityIndexPath), issues);
                var originalSources = await ReadSnapshotEffectSourceRootsAsync(manifest, issues);
                var currentSources = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
                foreach (var path in EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
                    currentSources[path] = ParseEffectNode(await _fs.ReadFileAsync(writeLease, path), path, issues);
                issues.AddRange(EffectAcceptedTurnInputComposer.ValidateAcceptedSkillComposition(originalSources, currentSources));
                ValidateEffectSourceDefinitions(currentSources, issues);
                issues.AddRange(EffectAcceptedTurnInputComposer.ValidateRegisteredSourceOwners(currentSources));
                issues.AddRange(EffectAcceptedTurnInputComposer.BuildCanonicalSourceAuthority(currentSources).Issues);
                var registered = await ComposeRegisteredResourceOutcomesAsync(manifest, preTurnAfterlifeOwners,
                    acceptedAfterlifeOwners, ownerComposition, stateResult.Ledger, issues);
                if (registered.Count != 0)
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_treatment_ordinary_concurrent_work_unsupported", "no unrelated registered system outcome",
                        "the early treatment return cannot omit an existing outcome owner"));
                if (issues.Any(static issue => issue.Severity == IssueSeverity.Error)) return issues;
            }
            // The explicit private intake follows the SAME upstream resource,
            // owner/party/pending, effect-continuity and source/skill exclusions.
            // It authorizes no cached plan: the canonical owner reconstructs under
            // its own lease before the sole joint preparation/reduction/publication.
            if (inspectMortalJointRepair is not null && parsedWound?.TreatmentCommands.Count > 0)
            {
                var joint = await inspectMortalJointRepair(writeLease);
                if (joint.Disposition != GameEngine.MortalJointRepairRawDisposition.NotApplicable)
                {
                    try
                    {
                        await joint.ValidateCurrentAsync(_fs, writeLease, manifest.SessionId, manifest.RequestId,
                            manifest.ManifestPayloadHash, manifest.TurnNumber);
                        issues.AddRange(joint.Issues);
                    }
                    catch (Exception error) when (error is InvalidDataException or IOException or InvalidOperationException or ArgumentException or JsonException)
                    {
                        issues.Add(MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_joint_repair_raw_intake_changed", "the exact private current-source intake", error.Message)));
                    }
                    if (joint.Disposition == GameEngine.MortalJointRepairRawDisposition.Rejected &&
                        !issues.Any(issue => issue.Severity == IssueSeverity.Error))
                        issues.Add(MortalWoundOpportunityAdapter.PrivateSourceIssue(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                            "mortal_joint_repair_raw_intake_refused", "a genuine accepted read-only joint correction", "rejected")));
                    return issues;
                }
                if (hasOriginalMortalOccurrence)
                {
                    issues.Add(WoundIssue(MortalWoundOccurrenceState.StatePath,
                        "mortal_wound_treatment_ordinary_concurrent_work_unsupported", "the explicit real joint owner for a competing occurrence",
                        "the private joint intake did not admit this original"));
                    return issues;
                }
            }
            // This callback joins only after original quartet and current owner admission.
            // Its owner issues the existing treatment common plan under THIS lease;
            // the generic wound/effect pipeline must not reduce that work a second time.
            var treatmentIssues = diagnosis is not null
                ? await prepareMortalDiagnosis!(writeLease, diagnosis)
                : prepareMortalGuaranteedTreatment is null || parsedWound?.TreatmentCommands.Count is not > 0
                    ? null : await prepareMortalGuaranteedTreatment(writeLease);
            if (treatmentIssues is not null)
            {
                issues.AddRange(treatmentIssues);
                if (issues.Any(static issue => issue.Severity == IssueSeverity.Error)) return issues;
                var retained = await ValidateRetainedMortalWoundTreatmentPublicationAsync(writeLease);
                if (retained is null)
                    issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                        "mortal_wound_treatment_ordinary_plan_missing", "the admitted owner's issued treatment plan",
                        "the callback produced no validated treatment publication authority"));
                else issues.AddRange(retained);
                return issues;
            }
            if (parsedWound?.TreatmentCommands.Count > 0)
            {
                issues.Add(WoundIssue(AcceptedMechanicsPlan.WoundCommandPath,
                    "mortal_wound_treatment_ordinary_owner_unavailable", "a fresh admitted ordinary treatment owner frame",
                    "held treatment wire alone cannot authorize generic publication"));
                return issues;
            }
        }
        if (stageMortalQteDecision is not null)
        {
            issues.AddRange(await stageMortalQteDecision(writeLease));
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error)) return issues;
        }
        var mortalReplay = readMortalQteReplay?.Invoke();
        var woundHandoffSink = new AcceptedTurnWoundHandoffSink();
        if (mortalReplay is not null)
        {
            // An already-consumed decision may finish its transient publication only.
            // New raw mechanical work requires a new ordinary source, never replay.
            if (mortalReplay.Disposition != "exact_replay" || !commands.IsMissing || effectCommandJson is not null ||
                hasStagedFullPartyResourcePackets || ownerComposition.CapacityDrafts.Count != 0 ||
                ownerComposition.TerminalOwners.Count != 0 || ownerComposition.OwnerTransitions.Count != 0)
            {
                issues.Add(WoundIssue(MortalWoundOpportunityAdapter.DecisionPath, "mortal_qte_replay_additional_mutation",
                    "exact durable decision replay without new raw mechanical work", "new mechanical work on consumed source"));
                return issues;
            }
            await ValidateAcceptedTurnCanonicalWoundMaterializationAsync(effectIssues, writeLease);
            await ValidateAcceptedTurnCanonicalEffectMaterializationAsync(effectIssues, writeLease);
        }
        else await ValidateAcceptedTurnRawEffectMaterializationAsync(
            effectIssues,
            ownerComposition,
            definitions,
            stateResult.Ledger,
            isTerminalReceiptReplay,
            manifest,
            writeLease,
            woundHandoffSink,
            requireSpiritualBase: spiritualCapture != null || mortalCapture != null || naturalOwner is not null,
            mortalCapture: mortalCapture, spiritualAllocations: spiritualCapture?.Allocations,
            currentInputs: spiritualCapture?.CurrentInputs, naturalOwner: naturalOwner,
            combatCommand: spiritualCapture?.CombatCommand);
        issues.AddRange(effectIssues);
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            return issues;
        var woundHandoff = woundHandoffSink.Value;
        var mortalReceipt = woundHandoff?.MortalQteReceipt ?? mortalReplay;
        var ownerCompanionAfterImages = ownerComposition.OwnerCompanionAfterImages.ToDictionary(
            static pair => pair.Key, static pair => pair.Value.DeepClone().AsObject(), StringComparer.Ordinal);
        if (mortalReceipt is not null)
        {
            ownerCompanionAfterImages.Add(MortalWoundOccurrenceState.StatePath,
                JsonNode.Parse(MortalWoundOccurrenceState.SerializeCanonical(mortalReceipt.OccurrenceState!))!.AsObject());
            ownerCompanionAfterImages.Add(MortalWoundOpportunityReceiptState.StatePath,
                JsonNode.Parse(MortalWoundOpportunityReceiptState.SerializeCanonical(mortalReceipt.ReceiptState!))!.AsObject());
        }
        if (naturalOwner is not null)
            ownerCompanionAfterImages.Add(ProgressionScheduleService.SchedulePath,
                JsonNode.Parse(naturalOwner.SchedulerMutation.ScheduleAfterJson)!.AsObject());

        EffectAcceptedTurnPlan? effectPlan = naturalOwner is null ? null : woundHandoff?.StageBundle.EffectBatchPlan.EffectPlan;
        if (effectPlan is null && EffectAcceptedTurnPlanAuthority.TryPeekValidated(
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
        var registeredSystemOutcomes = mortalReplay is not null ? Array.Empty<IResourceRegisteredSystemOutcomeDraft>() : await ComposeRegisteredResourceOutcomesAsync(
            manifest,
            preTurnAfterlifeOwners,
            acceptedAfterlifeOwners,
            ownerComposition,
            stateResult.Ledger,
            issues, deferSpiritualConflict: spiritualCapture?.AwaitOriginalPrefix == true,
            projectionClock: spiritualCapture?.Allocations.Clock,
            currentInputs: currentInputs);
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
            woundHandoff is null && mortalReceipt is null && spiritualCapture == null && mortalCapture == null)
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

        var effectIdentityJson = spiritualCapture?.CurrentInputs is { } indexCurrentInputs
            ? indexCurrentInputs.ReadText(EffectAcceptedTurnPlan.IdentityIndexPath)
            : await _fs.ReadFileAsync(EffectAcceptedTurnPlan.IdentityIndexPath);
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
        var commonAcceptedEvents = events.Root.DeepClone().AsObject();
        if (mortalReceipt is not null)
        {
            var sourceEvents = woundHandoff?.Input.Binding.AcceptedEvents;
            if (sourceEvents is null)
            {
                var signed = MortalQteWoundOpportunityProjection.ReadSnapshot(_fs, writeLease);
                var originalPending = MortalWoundOccurrenceState.Parse(MortalQteWoundOpportunityProjection.ReadObservedRoot(signed,
                    MortalWoundOccurrenceState.StatePath), MortalWoundOccurrenceState.StatePath);
                var originalReceipts = MortalWoundOpportunityReceiptState.Parse(MortalQteWoundOpportunityProjection.ReadObservedRoot(signed,
                    MortalWoundOpportunityReceiptState.StatePath) ?? "{\"schemaVersion\":1,\"nextOrdinal\":1,\"receipts\":[]}", MortalWoundOpportunityReceiptState.StatePath);
                if (!originalPending.IsValid || originalPending.State is null || !originalReceipts.IsValid || originalReceipts.State is null ||
                    signed.SessionId != manifest.SessionId || signed.RequestId != manifest.RequestId ||
                    signed.SnapshotToken != manifest.ManifestPayloadHash || signed.TurnNumber != manifest.TurnNumber)
                {
                    issues.AddRange(originalPending.Issues.Concat(originalReceipts.Issues));
                    AddMissingWoundStageIssue(issues, "mortal_qte_replay_source_mismatch", "the original official decision snapshot");
                    return issues;
                }
                var original = originalPending.State.Occurrences.Single(row => row.OccurrenceId == mortalReceipt.Receipt!.OpportunityId);
                var rebound = WoundAcceptedEventAuthorityComposer.RebindMortalOccurrence(original,
                    originalPending.State.Occurrences.Where(row => row.ProducerOperationKey == original.ProducerOperationKey).ToArray(),
                    originalReceipts.State.Receipts.Where(row => row.ProducerOperationKey == original.ProducerOperationKey).ToArray(),
                    signed.SessionId, signed.RequestId, signed.SnapshotToken, signed.TurnNumber);
                issues.AddRange(rebound.Issues);
                if (!rebound.Success) return issues;
                sourceEvents = rebound.Events;
            }
            commonAcceptedEvents["mortalWoundSourceAcceptedEvents"] = JsonSerializer.SerializeToNode(
                sourceEvents, Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            internalInputs["mortalQteDecisionReceipt"] = new JsonObject
            {
                ["pendingAfter"] = ownerCompanionAfterImages[MortalWoundOccurrenceState.StatePath].DeepClone(),
                ["receiptsAfter"] = ownerCompanionAfterImages[MortalWoundOpportunityReceiptState.StatePath].DeepClone(),
                ["historyAgreement"] = JsonSerializer.SerializeToNode(mortalReceipt.HistoryAgreement,
                    Configuration.SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                ["finalWoundPlanFingerprint"] = woundHandoff?.StageBundle.FinalPlan.WoundFinalPlanFingerprint,
                ["exactReplay"] = mortalReplay is not null
            };
        }
        if (_spiritualWoundSourceSession is { HasWork: true } sourceSessionInput)
            internalInputs["spiritualSourcePreparation"] = sourceSessionInput.BuildInputBinding();
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
        if (naturalOwner is not null)
        {
            beforePaths.UnionWith(AfterlifeWoundTreatmentAuthority.SourcePaths);
            beforePaths.Add("output/interface_updates.json");
        }
        if (spiritualCapture?.CombatCommand is not null)
            beforePaths.Add(AcceptedMechanicsPlan.WoundCommandPath);
        if (mortalReceipt is not null)
        {
            beforePaths.Add(MortalQteDecisionPublication.StatePath);
            beforePaths.Add(MortalWoundOpportunityAdapter.DecisionPath);
            beforePaths.Add(AcceptedMechanicsPlan.WoundCommandPath);
            beforePaths.Add("output/narrative_response.json");
            beforePaths.UnionWith(CanonicalWoundSnapshotReadPaths);
            if (mortalReplay is not null)
            {
                beforePaths.UnionWith(EffectAcceptedTurnInputComposer.SourceAuthorityPaths);
                beforePaths.UnionWith(new[] { EffectCarrierCatalog.PlayerPath, EffectCarrierCatalog.NpcPath,
                    EffectCarrierCatalog.EnemiesPath, EffectCarrierCatalog.AlliesPath, EffectCarrierCatalog.AfterlifeProfilesPath,
                    EffectCarrierCatalog.SpiritualConflictPath });
            }
        }
        if (_spiritualWoundSourceSession is { HasWork: true } sourceSessionForBinding)
            beforePaths.UnionWith(sourceSessionForBinding.SelectedPaths);
        beforePaths.UnionWith(ownerCompanionAfterImages.Keys);
        beforePaths.UnionWith(ownerComposition.OwnerTransitions.Select(static value => value.Path));
        beforePaths.UnionWith(acceptedItemOwners.Select(static owner => owner.FilePath));
        foreach (var outcome in registeredSystemOutcomes)
            beforePaths.UnionWith(outcome.ExpectedBeforeImages.Keys);
        if (hasStagedFullPartyResourcePackets)
            beforePaths.Add(FullPartyInteractionsPath);
        if (currentInputs is not null)
        {
            var unknownPath = beforePaths.FirstOrDefault(path =>
                SpiritualOriginalDraftInputs.IsDraftPath(path) &&
                !currentInputs.PathInventory.Contains(path, StringComparer.Ordinal));
            if (unknownPath is not null)
            {
                issues.Add(ResourceIssue(unknownPath,
                    "spiritual_original_resource_input_unregistered",
                    "a registered original draft publication path",
                    "unregistered draft before-image"));
                return issues;
            }
        }
        var beforeImages = (await CaptureResourceBeforeImagesAsync(beforePaths, currentInputs))
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
        if (sealMortalQteDecision is not null)
        {
            issues.AddRange(await sealMortalQteDecision(writeLease, beforeImages));
            if (issues.Any(static issue => issue.Severity == IssueSeverity.Error)) return issues;
        }
        if (mortalReceipt is not null)
        {
            internalInputs["mortalQteDecisionInputs"] = new JsonObject();
            foreach (var path in new[] { MortalWoundOpportunityAdapter.DecisionPath,
                         AcceptedMechanicsPlan.WoundCommandPath, "output/narrative_response.json" })
            {
                var image = beforeImages[path];
                internalInputs["mortalQteDecisionInputs"]![path] = HashText("mortal-qte-decision-exact-input-v1",
                    image.Existed ? Convert.ToBase64String(image.Bytes!) : "<absent>");
            }
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
            AcceptedEvents: HashNode("resource-events-v1", commonAcceptedEvents),
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
            ownerCompanionAfterImages: ownerCompanionAfterImages,
            ownerTransitions: ownerComposition.OwnerTransitions,
            registeredSystemOutcomes: registeredSystemOutcomes,
            pendingResolutionState: pendingResolutionState,
            woundStageBundle: woundHandoff?.StageBundle,
            resourceIdentityFactory: spiritualCapture?.Allocations.Resources,
            effectIdentityFactory: spiritualCapture?.Allocations.Effects);
        var input = new AcceptedMechanicsInput(
            manifest.SessionId,
            manifest.RequestId,
            manifest.ManifestPayloadHash,
            realm,
            manifest.TurnNumber,
            commonAcceptedEvents,
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
        if (mortalCapture != null)
        {
            mortalCapture.Input = input;
            return issues;
        }
        if (spiritualCapture != null)
        {
            spiritualCapture.Input = input;
            spiritualCapture.Source = _spiritualWoundSourceSession;
            return issues;
        }
        var result = woundHandoff?.StageBundle is { } woundStageBundle &&
                     (naturalOwner is not null || MortalWoundCanonicalAnchorPlan.RequiresInitialCreateAnchors(
                          woundStageBundle) ||
                      MortalWoundCanonicalAnchorPlan.RequiresConditionReentryAnchors(
                          woundStageBundle))
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
            if (naturalOwner is { IsDiagnosis: true } diagnosisOwner)
            {
                try
                {
                    // The common factory already checked this same private
                    // continuation against its sealed StageBundle. Serialized
                    // family markers never select the prepared-input exception.
                    if (!ReferenceEquals(diagnosisOwner, result.Plan.SpiritualHealingContinuation))
                        throw new InvalidDataException("Diagnosis dynamic rollback coverage requires its actual common-plan owner.");
                    if (diagnosisOwner.HasCurrentDiagnosisPreparedCommandRole(_fs, writeLease,
                            manifest.SessionId, manifest.RequestId, manifest.TurnNumber,
                            manifest.ManifestPayloadHash, ComputeManifestPayloadHash(manifest)))
                        requiredSnapshotPaths = requiredSnapshotPaths.Where(path => path != AcceptedMechanicsPlan.WoundCommandPath).ToArray();
                }
                catch
                {
                    AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
                    throw;
                }
            }
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

    /// <summary>
    /// Reads a selected current resource text image from the named original view when supplied.
    /// </summary>
    /// <param name="path">
    /// Exact logical resource input path.
    /// </param>
    /// <param name="currentInputs">
    /// Retained original view, or <see langword="null"/> for ordinary physical reads.
    /// </param>
    /// <returns>
    /// Current text, or <see langword="null"/> when the selected image is absent.
    /// </returns>
    private Task<string?> ReadResourceCurrentTextAsync(string path,
        SpiritualOriginalDraftInputs? currentInputs) => currentInputs is null
            ? _fs.ReadFileAsync(path)
            : Task.FromResult(currentInputs.ReadText(path));

    /// <summary>
    /// Reads exact current resource bytes from the named original view when supplied.
    /// </summary>
    /// <param name="path">
    /// Exact logical resource input path.
    /// </param>
    /// <param name="currentInputs">
    /// Retained original view, or <see langword="null"/> for ordinary physical reads.
    /// </param>
    /// <returns>
    /// Current bytes, or <see langword="null"/> when the selected image is absent.
    /// </returns>
    private Task<byte[]?> ReadResourceCurrentBytesAsync(string path,
        SpiritualOriginalDraftInputs? currentInputs) => currentInputs is null
            ? _fs.ReadFileBytesAsync(path)
            : Task.FromResult(currentInputs.ReadImage(path).Bytes);

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

    /// <summary>
    /// Reads the Mortal resource-owner roots from either signed pre-turn or selected current inputs.
    /// </summary>
    /// <param name="manifest">
    /// Signed pre-turn manifest, or <see langword="null"/> for current roots.
    /// </param>
    /// <param name="issues">
    /// Destination for malformed-root diagnostics.
    /// </param>
    /// <param name="writeLease">
    /// Optional lease for physical current reads.
    /// </param>
    /// <param name="currentInputs">
    /// Named current draft view, or <see langword="null"/> for physical current reads.
    /// </param>
    /// <returns>
    /// Parsed Mortal owner roots with defaults for absent or invalid files.
    /// </returns>
    private async Task<MortalResourceOwnerRoots> ReadMortalResourceOwnerRootsAsync(
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null,
        SpiritualOriginalDraftInputs? currentInputs = null) =>
        new(
            await ReadMortalResourceOwnerRootAsync(
                "game_state/npcs/npc_core.json",
                "NPCsInScene",
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadMortalResourceOwnerRootAsync(
                EffectCarrierCatalog.EnemiesPath,
                "enemiesData",
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadMortalResourceOwnerRootAsync(
                EffectCarrierCatalog.AlliesPath,
                "alliesData",
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadMortalResourceOwnerRootAsync(
                StorageTransportMoveService.VehiclesPath,
                "vehicles",
                manifest,
                issues,
                writeLease, currentInputs));

    /// <summary>
    /// Reads afterlife resource-owner roots from either signed pre-turn or selected current inputs.
    /// </summary>
    /// <param name="manifest">
    /// Signed pre-turn manifest, or <see langword="null"/> for current roots.
    /// </param>
    /// <param name="issues">
    /// Destination for malformed-root diagnostics.
    /// </param>
    /// <param name="writeLease">
    /// Optional lease for physical current reads.
    /// </param>
    /// <param name="currentInputs">
    /// Named current draft view, or <see langword="null"/> for physical current reads.
    /// </param>
    /// <returns>
    /// Parsed afterlife owner roots with defaults for absent or invalid files.
    /// </returns>
    private async Task<AfterlifeResourceOwnerRoots> ReadAfterlifeResourceOwnerRootsAsync(
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null,
        SpiritualOriginalDraftInputs? currentInputs = null) =>
        new(
            await ReadAfterlifeResourceOwnerRootAsync(
                AfterlifeEntityProfileState.StatePath,
                AfterlifeEntityProfileState.CreateDefaultRoot,
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadAfterlifeResourceOwnerRootAsync(
                AfterlifeSpiritualConflictState.StatePath,
                AfterlifeSpiritualConflictState.CreateDefaultRoot,
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadAfterlifeResourceOwnerRootAsync(
                "game_state/meta/soul_state.json",
                static () => new JsonObject(),
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadAfterlifeResourceOwnerRootAsync(
                ShiningAbodeState.StatePath,
                ShiningAbodeState.CreateDefaultState,
                manifest,
                issues,
                writeLease, currentInputs),
            await ReadAfterlifeResourceOwnerRootAsync(
                "game_state/meta/guardians.json",
                static () => new JsonObject(),
                manifest,
                issues,
                writeLease, currentInputs));

    /// <summary>
    /// Projects accepted afterlife resource owners using the attempt's shared closure clock when supplied.
    /// </summary>
    /// <param name="manifest">
    /// Validated original turn manifest.
    /// </param>
    /// <param name="preTurn">
    /// Original owner roots used as the projection baseline.
    /// </param>
    /// <param name="issues">
    /// Destination for input diagnostics.
    /// </param>
    /// <param name="projectionClock">
    /// Shared capture clock, or <see langword="null"/> for ordinary time reads.
    /// </param>
    /// <param name="currentInputs">
    /// Retained named current owner images, or <see langword="null"/> for physical reads.
    /// </param>
    /// <returns>
    /// Accepted resource owner roots without publication.
    /// </returns>
    private async Task<AfterlifeResourceOwnerRoots>
        ReadAcceptedAfterlifeResourceOwnerRootsAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            AfterlifeResourceOwnerRoots preTurn,
            List<ValidationIssue> issues, AcceptedTurnProjectionClock? projectionClock = null,
            SpiritualOriginalDraftInputs? currentInputs = null,
            AfterlifeWoundHealingPlanner.Continuation? naturalOwner = null)
    {
        var current = await ReadAfterlifeResourceOwnerRootsAsync(
            manifest: null,
            issues, currentInputs: currentInputs);
        var profiles = naturalOwner is null
            ? AfterlifeEntityProfileState.ProjectCanonicalRoot(current.Profiles, preTurn.Profiles)
            : naturalOwner.ProjectAcceptedProfiles(current.Profiles, preTurn.Profiles,
                current.SoulState, current.ShiningAbode);
        try
        {
            var changes = SpiritualTreatmentParticipation.ReadChanges(current.Profiles, preTurn.Profiles);
            if (changes.Count != 0)
            {
                if (naturalOwner is null) throw new InvalidDataException("Participation changes require an actual ordinary natural publication owner.");
                await naturalOwner.PrepareParticipationAsync(changes, profiles);
            }
            naturalOwner?.RequireMemoryProgramsBound();
        }
        catch (Exception failure) when (failure is not CoordinatedStatePublicationUncertainException &&
            failure is (InvalidDataException or InvalidOperationException or JsonException or ArgumentException or OverflowException))
        {
            issues.Add(new(AfterlifeEntityProfileState.StatePath, IssueSeverity.Error,
                "Spiritual participation cannot be issued by this profile response.", code: "spiritual_participation_invalid",
                actual: failure.Message, repairHint: "Keep the same turn; author only the closed typed participation change on the current Guardian or present local resident during an ordinary safe cycle."));
        }
        var conflict = current.SpiritualConflict;
        if (conflict[AfterlifeSpiritualConflictState.ResponseField] is JsonObject update)
        {
            conflict = AfterlifeSpiritualConflictState.ApplyUpdate(
                preTurn.SpiritualConflict,
                update, projectionClock);
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

    /// <summary>
    /// Reads one afterlife owner root from its selected signed or current authority.
    /// </summary>
    /// <param name="path">
    /// Exact logical owner path.
    /// </param>
    /// <param name="missingFactory">
    /// Creates the default root for a missing or malformed image.
    /// </param>
    /// <param name="manifest">
    /// Signed pre-turn manifest, or <see langword="null"/> for a current read.
    /// </param>
    /// <param name="issues">
    /// Destination for malformed-root diagnostics.
    /// </param>
    /// <param name="writeLease">
    /// Optional lease for a physical current read.
    /// </param>
    /// <param name="currentInputs">
    /// Named current draft view, or <see langword="null"/> for a physical current read.
    /// </param>
    /// <returns>
    /// Parsed owner root, or the default from <paramref name="missingFactory"/>.
    /// </returns>
    private async Task<JsonObject> ReadAfterlifeResourceOwnerRootAsync(
        string path,
        Func<JsonObject> missingFactory,
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease,
        SpiritualOriginalDraftInputs? currentInputs = null)
    {
        var json = manifest == null
            ? writeLease == null
                ? await ReadResourceCurrentTextAsync(path, currentInputs)
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

    /// <summary>
    /// Reads one Mortal owner root from its selected signed or current authority.
    /// </summary>
    /// <param name="path">
    /// Exact logical owner path.
    /// </param>
    /// <param name="canonicalCollection">
    /// Collection name used in the default root when the image is absent or invalid.
    /// </param>
    /// <param name="manifest">
    /// Signed pre-turn manifest, or <see langword="null"/> for a current read.
    /// </param>
    /// <param name="issues">
    /// Destination for malformed-root diagnostics.
    /// </param>
    /// <param name="writeLease">
    /// Optional lease for a physical current read.
    /// </param>
    /// <param name="currentInputs">
    /// Named current draft view, or <see langword="null"/> for a physical current read.
    /// </param>
    /// <returns>
    /// Parsed owner root, or an empty <paramref name="canonicalCollection"/> collection.
    /// </returns>
    private async Task<JsonObject> ReadMortalResourceOwnerRootAsync(
        string path,
        string canonicalCollection,
        ValidationPendingTurnSnapshotManifest? manifest,
        List<ValidationIssue> issues,
        FileSystemManager.CanonicalWriteLease? writeLease = null,
        SpiritualOriginalDraftInputs? currentInputs = null)
    {
        var json = manifest == null
            ? writeLease == null
                ? await ReadResourceCurrentTextAsync(path, currentInputs)
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

    /// <summary>
    /// Compares current immutable resource roots with their signed pre-turn images.
    /// </summary>
    /// <param name="manifest">
    /// Validated manifest locating signed pre-turn images.
    /// </param>
    /// <param name="issues">
    /// Destination for direct-mutation diagnostics.
    /// </param>
    /// <param name="currentInputs">
    /// Named current draft view, or <see langword="null"/> for physical current reads.
    /// </param>
    private async Task ValidateResourceSnapshotContinuityAsync(
        ValidationPendingTurnSnapshotManifest manifest,
        List<ValidationIssue> issues,
        SpiritualOriginalDraftInputs? currentInputs = null)
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
            var current = await ReadResourceCurrentBytesAsync(path, currentInputs);
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

    /// <summary>
    /// Captures publication before-images from retained draft data or physical control files.
    /// </summary>
    /// <param name="paths">
    /// Exact logical paths requiring publication before-images.
    /// </param>
    /// <param name="currentInputs">
    /// Named draft view for draft paths, or <see langword="null"/> for physical reads.
    /// </param>
    /// <returns>
    /// Exact presence and bytes for each distinct requested path.
    /// </returns>
    private async Task<IReadOnlyDictionary<string, CanonicalBeforeImage>>
        CaptureResourceBeforeImagesAsync(IEnumerable<string> paths,
            SpiritualOriginalDraftInputs? currentInputs = null)
    {
        var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in paths.Distinct(StringComparer.Ordinal))
        {
            if (currentInputs is not null && SpiritualOriginalDraftInputs.IsDraftPath(path))
            {
                result[path] = currentInputs.ReadImage(path);
            }
            else
            {
                var bytes = await _fs.ReadFileBytesAsync(path);
                result[path] = new CanonicalBeforeImage(bytes is not null, bytes);
            }
        }
        return result;
    }

    /// <summary>
    /// Prepares registered resource outcomes from the original manifest and current owner projections.
    /// </summary>
    /// <param name="manifest">
    /// Validated original turn context and retained request time.
    /// </param>
    /// <param name="preTurnAfterlifeOwners">
    /// Original afterlife owner roots from the pending snapshot.
    /// </param>
    /// <param name="acceptedAfterlifeOwners">
    /// Current admitted afterlife owner projections.
    /// </param>
    /// <param name="ownerComposition">
    /// Actual resource owner authority, capacity drafts and companion projections.
    /// </param>
    /// <param name="state">
    /// Original resource ledger used to construct registered outcomes.
    /// </param>
    /// <param name="issues">
    /// Destination for admission and projection diagnostics.
    /// </param>
    /// <param name="deferSpiritualConflict">
    /// Whether the chronological original executor owns conflict resource preparation separately.
    /// </param>
    /// <param name="projectionClock">
    /// Optional capture clock shared by soul normalization and survival consumption.
    /// </param>
    /// <param name="currentInputs">
    /// Retained named system-outcome inputs, or <see langword="null"/> for physical reads.
    /// </param>
    /// <returns>
    /// Prepared outcomes; callers must reject any accompanying validation errors before execution.
    /// </returns>
    private async Task<IReadOnlyList<IResourceRegisteredSystemOutcomeDraft>>
        ComposeRegisteredResourceOutcomesAsync(
            ValidationPendingTurnSnapshotManifest manifest,
            AfterlifeResourceOwnerRoots preTurnAfterlifeOwners,
            AfterlifeResourceOwnerRoots acceptedAfterlifeOwners,
            ResourceOwnerCompositionResult ownerComposition,
            ResourceStateLedger state,
            List<ValidationIssue> issues, bool deferSpiritualConflict = false,
            AcceptedTurnProjectionClock? projectionClock = null,
            SpiritualOriginalDraftInputs? currentInputs = null)
    {
        var outcomes = new List<IResourceRegisteredSystemOutcomeDraft>();
        var owners = ownerComposition.Authority!;
        if (!deferSpiritualConflict)
        {
            var conflictBuild = AfterlifeSpiritualConflictResourceOutcome.TryCreate(
                manifest.TurnNumber,
                preTurnAfterlifeOwners.SpiritualConflict,
                acceptedAfterlifeOwners.SpiritualConflict,
                owners,
                state);
            issues.AddRange(conflictBuild.Issues);
            if (conflictBuild.Draft != null)
                outcomes.Add(conflictBuild.Draft);
        }

        var acceptedShining = ownerComposition.OwnerCompanionAfterImages.TryGetValue(
            ShiningAbodeState.StatePath,
            out var shiningAfterImage)
            ? shiningAfterImage
            : acceptedAfterlifeOwners.ShiningAbode;
        var shiningBytes = await ReadResourceCurrentBytesAsync(ShiningAbodeState.StatePath, currentInputs);
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
        var guardiansBytes = await ReadResourceCurrentBytesAsync(guardiansPath, currentInputs);
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
        var currentSoulBytes = await ReadResourceCurrentBytesAsync(soulPath, currentInputs);
        if (currentSoulBytes == null ||
            !TryParseStrictResourceObject(DecodeResourceUtf8(currentSoulBytes), out var currentSoul))
        {
            return outcomes;
        }

        var preTurnSoulJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, soulPath);
        var preTurnSoul = TryParseStrictResourceObject(preTurnSoulJson, out var parsedPreTurnSoul)
            ? parsedPreTurnSoul
            : null;
        var lifeTransitionsJson = await ReadResourceCurrentTextAsync(
            "game_state/control/life_transitions.json", currentInputs);
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
                enforceStrictCanonicalRoots: true,
                projectionClock: projectionClock);
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

        var currentWorldBytes = await ReadResourceCurrentBytesAsync(worldEventsPath, currentInputs);
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

        var build = ShiningBlessingEffectState.TryCreateSurvivalResourceOutcomeDraftWithClock(
            manifest.TurnNumber,
            normalizedSoul,
            currentWorldEvents,
            preTurnWorldEvents,
            CanonicalStateNormalizer.CreateExpectedSoulNormalizationBeforeImage(
                currentSoulBytes,
                currentSoul,
                normalizedSoul),
            new CanonicalBeforeImage(true, currentWorldBytes),
            projectionClock);
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
