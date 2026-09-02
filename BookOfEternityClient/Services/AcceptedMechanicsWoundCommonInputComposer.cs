using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record AcceptedMechanicsWoundCommonInputCompositionResult(
    AcceptedMechanicsInput? Input,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool Success => Input is not null && Issues.Count == 0;
}

internal static class AcceptedMechanicsWoundCommonInputComposer
{
    internal sealed class PublicationAuthorityProof
    {
        private PublicationAuthorityProof()
        {
        }

        internal static PublicationAuthorityProof Create() => new();
    }

    private static readonly PublicationAuthorityProof DirectPublicationProof =
        PublicationAuthorityProof.Create();

    internal static bool IsPublicationAuthorityProof(PublicationAuthorityProof proof) =>
        ReferenceEquals(proof, DirectPublicationProof);

    internal static AcceptedMechanicsWoundCommonInputCompositionResult Compose(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundCanonicalAnchorPlan anchorPlan)
        => ComposeCore(
            fileSystem,
            writeLease,
            bundle,
            anchorPlan,
            treatmentCurrentTimeInMinutes: null);

    internal static AcceptedMechanicsWoundCommonInputCompositionResult
        ComposeTreatmentContinuation(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            AcceptedMechanicsWoundStageBundle bundle,
            object continuationAuthority,
            object reservationAuthority,
            long currentTimeInMinutes) => ComposeCore(
                fileSystem,
                writeLease,
                bundle,
                anchorPlan: null,
                treatmentCurrentTimeInMinutes: currentTimeInMinutes,
                treatmentContinuationAuthority: continuationAuthority,
                treatmentReservationAuthority: reservationAuthority);

    private static AcceptedMechanicsWoundCommonInputCompositionResult ComposeCore(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsWoundStageBundle bundle,
        MortalWoundCanonicalAnchorPlan? anchorPlan,
        long? treatmentCurrentTimeInMinutes,
        object? treatmentContinuationAuthority = null,
        object? treatmentReservationAuthority = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(bundle);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        if ((anchorPlan is null) == (treatmentCurrentTimeInMinutes is null))
        {
            return Failed(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_publication_authority_mismatch",
                "exactly one initial-create anchor or treatment-continuation clock authority",
                "missing or competing publication authority");
        }
        if (treatmentCurrentTimeInMinutes is not null &&
            (treatmentContinuationAuthority is null ||
             !ReferenceEquals(
                 bundle.PreparedPlan.TreatmentContinuationAuthority,
                 treatmentContinuationAuthority) ||
             !WoundAcceptedTurnPlanner.TreatmentContinuationPreparedAgrees(
                 bundle.PreparedPlan) ||
             treatmentReservationAuthority is null ||
             !WoundAcceptedTurnPlanner.TreatmentContinuationReservationAgrees(
                 treatmentContinuationAuthority,
                 treatmentReservationAuthority)))
        {
            return Failed(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_treatment_continuation_provenance_mismatch",
                "the exact private-minted continuation authority carried by the prepared stage",
                "foreign or ordinary wound-stage bundle");
        }
        IReadOnlyDictionary<string, CanonicalBeforeImage> treatmentSkillBaselines =
            new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        IReadOnlyDictionary<string, JsonObject> treatmentSkillAfterImages =
            new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var treatmentSkillProjectionFingerprint = string.Empty;
        if (treatmentCurrentTimeInMinutes is not null &&
            !WoundAcceptedTurnPlanner.TryReadTreatmentSkillProjection(
                treatmentContinuationAuthority!,
                treatmentReservationAuthority!,
                out treatmentSkillBaselines,
                out treatmentSkillAfterImages,
                out treatmentSkillProjectionFingerprint))
        {
            return Failed(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_treatment_continuation_provenance_mismatch",
                "the exact private-minted treatment skill projection",
                "missing, foreign, or changed projection authority");
        }
        if (anchorPlan is not null && !anchorPlan.AgreesWith(bundle))
        {
            return Failed(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_anchor_authority_mismatch",
                bundle.BundleFingerprint,
                anchorPlan.WoundStageBundleFingerprint);
        }

        var issues = new List<ValidationIssue>();
        var beforeImages = new Dictionary<string, CanonicalBeforeImage>(
            StringComparer.Ordinal);
        var textByPath = new Dictionary<string, string?>(StringComparer.Ordinal);

        string? Read(string path)
        {
            if (textByPath.TryGetValue(path, out var cached))
                return cached;
            var bytes = fileSystem.ReadFileBytesAsync(writeLease, path)
                .GetAwaiter()
                .GetResult();
            beforeImages[path] = bytes is null
                ? new CanonicalBeforeImage(false, null)
                : new CanonicalBeforeImage(true, bytes);
            string? text = null;
            if (bytes is not null)
            {
                try
                {
                    var preamble = Encoding.UTF8.GetPreamble();
                    var offset = bytes.AsSpan().StartsWith(preamble)
                        ? preamble.Length
                        : 0;
                    text = new UTF8Encoding(
                            encoderShouldEmitUTF8Identifier: false,
                            throwOnInvalidBytes: true)
                        .GetString(bytes, offset, bytes.Length - offset);
                }
                catch (DecoderFallbackException)
                {
                    issues.Add(Issue(
                        path,
                        "accepted_mechanics_wound_common_utf8_invalid",
                        "valid UTF-8 canonical bytes",
                        "invalid UTF-8"));
                }
            }
            textByPath[path] = text;
            return text;
        }

        CanonicalBeforeImage CaptureBeforeImage(string path)
        {
            _ = Read(path);
            var image = beforeImages[path];
            return new CanonicalBeforeImage(image.Existed, image.Bytes);
        }

        try
        {
            foreach (var (path, expected) in treatmentSkillBaselines)
            {
                _ = Read(path);
                var actual = beforeImages[path];
                if (expected.Existed != actual.Existed ||
                    !(expected.Bytes ?? Array.Empty<byte>()).AsSpan().SequenceEqual(
                        actual.Bytes ?? Array.Empty<byte>()))
                {
                    issues.Add(Issue(
                        path,
                        "accepted_mechanics_wound_treatment_skill_before_image_mismatch",
                        "the exact sealed skill projection baseline",
                        "canonical bytes changed after projection"));
                }
            }
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var binding = bundle.Input.Binding;
            ValidateTurnRequest(Read(LiveTurnPreparationService.TurnRequestPath), binding, issues);
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var snapshotRead = PendingTurnSnapshotReader.ReadCurrent(
                fileSystem,
                writeLease,
                new[] { EffectAcceptedTurnInputComposer.WorldTimePath });
            if (!snapshotRead.Success || snapshotRead.Snapshot is null)
            {
                issues.AddRange(snapshotRead.Issues);
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);
            }
            var snapshot = snapshotRead.Snapshot;
            if (!string.Equals(
                    snapshot.SessionId,
                    binding.SessionId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.RequestId,
                    binding.RequestId,
                    StringComparison.Ordinal) ||
                !string.Equals(
                    snapshot.SnapshotToken,
                    binding.SnapshotToken,
                    StringComparison.Ordinal) ||
                snapshot.TurnNumber != binding.Turn ||
                !string.Equals(snapshot.Realm, binding.Realm, StringComparison.Ordinal))
            {
                issues.Add(Issue(
                    LiveTurnPreparationService.PendingTurnSnapshotManifestPath,
                    "accepted_mechanics_wound_snapshot_binding_mismatch",
                    $"{binding.SessionId}/{binding.RequestId}/{binding.SnapshotToken}/{binding.Realm}/{binding.Turn}",
                    $"{snapshot.SessionId}/{snapshot.RequestId}/{snapshot.SnapshotToken}/{snapshot.Realm}/{snapshot.TurnNumber}"));
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);
            }
            _ = Read(LiveTurnPreparationService.PendingTurnSnapshotManifestPath);
            _ = Read(PendingTurnSnapshotAuthority.AuthorityPath);
            var worldTimeJson = Read(EffectAcceptedTurnInputComposer.WorldTimePath);
            var currentTime = EffectAcceptedTurnInputComposer.ReadCanonicalWorldTime(
                worldTimeJson);
            var expectedCurrentTime = anchorPlan?.CurrentTimeInMinutes ??
                treatmentCurrentTimeInMinutes!.Value;
            if (currentTime != expectedCurrentTime)
            {
                issues.Add(Issue(
                    EffectAcceptedTurnInputComposer.WorldTimePath,
                    "accepted_mechanics_wound_anchor_clock_changed",
                    expectedCurrentTime.ToString(
                        System.Globalization.CultureInfo.InvariantCulture),
                    currentTime?.ToString(
                        System.Globalization.CultureInfo.InvariantCulture) ??
                    "missing or malformed"));
            }

            ValidateLiveWoundRoots(Read, bundle.Input, issues);
            var stagedEffectPlan = bundle.EffectBatchPlan.EffectPlan;
            ValidateLiveEffectRoots(Read, stagedEffectPlan, issues);
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var definitionsJson = Read(ResourceMaterializationContract.DefinitionsPath);
            var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
                definitionsJson,
                allowMissingPristine: true);
            issues.AddRange(definitionsResult.Issues);
            if (definitionsResult.Catalog is not { } definitions)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var stateJson = Read(ResourceMaterializationContract.StatePath);
            var historyJson = Read(ResourceMaterializationContract.HistoryPath);
            var stateResult = ResourceStateContract.ParseCanonical(
                stateJson,
                definitions,
                allowMissingPristine: true);
            var historyResult = ResourceHistoryState.ParseCanonical(
                historyJson,
                definitions,
                allowMissingPristine: true);
            issues.AddRange(stateResult.Issues);
            issues.AddRange(historyResult.Issues);
            if (stateResult.Ledger is not { } state ||
                historyResult.History is not { } history)
            {
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);
            }
            issues.AddRange(history.ValidateStateAgreement(state));
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var resourcePurpose = definitionsResult.IsMissing &&
                                  stateResult.IsMissing &&
                                  historyResult.IsMissing
                ? CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap
                : CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation;
            var ownerResult = CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
                    definitions,
                    path => Task.FromResult(Read(path)),
                    state,
                    history,
                    resourcePurpose)
                .GetAwaiter()
                .GetResult();
            issues.AddRange(ownerResult.Issues);
            if (ownerResult.Authority is not { } owners || issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            if (anchorPlan is null)
            {
                var treatmentItemProjectionRoots =
                    CaptureMortalItemProjectionRoots(Read, issues);
                if (issues.Count != 0)
                {
                    return new AcceptedMechanicsWoundCommonInputCompositionResult(
                        null,
                        issues);
                }

                if (MortalItemAcceptedTurnAuthority.HasValidatedItems(
                        fileSystem,
                        writeLease))
                {
                    issues.AddRange(MortalItemAcceptedTurnAuthority
                        .ConfirmValidatedTreatmentItems(
                            fileSystem,
                            writeLease,
                            binding.SessionId,
                            binding.SnapshotToken,
                            treatmentItemProjectionRoots,
                            treatmentContinuationAuthority!,
                            treatmentReservationAuthority!));
                }
                else
                {
                    var itemCatalog = MortalItemCarrierCatalog.Build(
                        CreateMortalItemCarrierCatalogInput(
                            treatmentItemProjectionRoots));
                    foreach (var itemIssue in itemCatalog.Issues)
                    {
                        issues.Add(Issue(
                            itemIssue.Path,
                            itemIssue.Code,
                            "one exact non-ambiguous Mortal item carrier catalog",
                            itemIssue.Identity ?? itemIssue.IdentityKind));
                    }
                    var itemIdentity = MortalItemIdentityState.Parse(
                        treatmentItemProjectionRoots[
                            MortalItemIdentityState.StatePath]);
                    issues.AddRange(itemIdentity.Issues);
                    if (issues.Count == 0)
                    {
                        issues.AddRange(MortalItemAcceptedTurnAuthority
                            .RegisterValidatedTreatmentItems(
                                fileSystem,
                                writeLease,
                                binding.SessionId,
                                binding.SnapshotToken,
                                itemCatalog,
                                itemIdentity.EntriesByItemId.Keys,
                                treatmentItemProjectionRoots,
                                treatmentContinuationAuthority!,
                                treatmentReservationAuthority!));
                    }
                }
                if (issues.Count != 0)
                {
                    return new AcceptedMechanicsWoundCommonInputCompositionResult(
                        null,
                        issues);
                }

                var npcCoreAuthority = NpcCoreChangesContract
                    .CreateAuthorityFromCanonicalJson(
                        Read(MortalLocationMaterializationContract.WorldMapPath),
                        Read(MortalLocationMaterializationContract.CurrentLocationPath),
                        Read("game_state/factions/faction_core.json"),
                        Read("game_state/misc/characteristics.json"));
                var npcTradePending = CaptureBeforeImage(
                    NpcTradeRequestState.PendingRequestPath);
                var trainingPending = CaptureBeforeImage(
                    TrainingRequestState.PendingRequestPath);
                if (issues.Count == 0)
                {
                    issues.AddRange(MortalItemAcceptedTurnAuthority
                        .SealValidatedTreatmentPublicationBaseline(
                            fileSystem,
                            writeLease,
                            binding.SessionId,
                            binding.SnapshotToken,
                            binding.Turn,
                            npcCoreAuthority,
                            npcTradePending,
                            trainingPending,
                            treatmentContinuationAuthority!,
                            treatmentReservationAuthority!));
                }
                if (issues.Count != 0)
                {
                    return new AcceptedMechanicsWoundCommonInputCompositionResult(
                        null,
                        issues);
                }
            }

            MortalWoundTreatmentResourcePublicationAuthority?
                treatmentResourcePublicationAuthority = null;
            if (anchorPlan is null)
            {
                var resourcePublication = WoundAcceptedTurnPlanner
                    .BuildTreatmentResourcePublication(
                        treatmentContinuationAuthority!,
                        treatmentReservationAuthority!,
                        definitions,
                        state,
                        history,
                        owners,
                        beforeImages);
                issues.AddRange(resourcePublication.Issues);
                treatmentResourcePublicationAuthority =
                    resourcePublication.Authority;
                if (treatmentResourcePublicationAuthority is null ||
                    issues.Count != 0)
                {
                    return new AcceptedMechanicsWoundCommonInputCompositionResult(
                        null,
                        issues);
                }
                foreach (var pair in treatmentResourcePublicationAuthority
                             .RegisteredOutcome.ExpectedBeforeImages)
                {
                    _ = Read(pair.Key);
                    var actual = beforeImages[pair.Key];
                    if (pair.Value.Existed != actual.Existed ||
                        !(pair.Value.Bytes ?? Array.Empty<byte>()).AsSpan()
                        .SequenceEqual(actual.Bytes ?? Array.Empty<byte>()))
                    {
                        issues.Add(Issue(
                            pair.Key,
                            "mortal_wound_treatment_publication_resource_before_image_mismatch",
                            "the exact sealed canonical resource before-image",
                            "canonical bytes changed during composition"));
                    }
                }
                if (issues.Count != 0)
                {
                    return new AcceptedMechanicsWoundCommonInputCompositionResult(
                        null,
                        issues);
                }
            }

            var sourcesResult = ResourceMutationSourceCatalog.Create(
                treatmentResourcePublicationAuthority?.RegisteredOutcome
                    .SourceExports ??
                Array.Empty<ResourceMutationSourceExport>());
            issues.AddRange(sourcesResult.Issues);
            if (sourcesResult.Catalog is not { } sources || issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var pendingResult = ResourcePendingResolutionState.ParseCanonical(
                Read(ResourcePendingResolutionState.PendingPath),
                definitions,
                allowMissingPristine: true);
            issues.AddRange(pendingResult.Issues);
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            var resourceCommands = ResourceAcceptedTurnInputComposer.Parse(null);
            issues.AddRange(resourceCommands.Issues);
            var acceptedEvents = new JsonObject
            {
                ["acceptedEvents"] = new JsonArray()
            };
            var effectCommands = bundle.EffectBatchPlan.EffectInput.RawCommands;
            var pendingInput = pendingResult.State?.ToCanonicalRoot() ??
                               new JsonObject();
            var woundCommands = new JsonObject
            {
                ["schemaVersion"] = 1,
                ["inputFingerprint"] = bundle.InputFingerprint,
                ["bundleFingerprint"] = bundle.BundleFingerprint
            };
            if (anchorPlan is not null)
                woundCommands["anchorPlanFingerprint"] = anchorPlan.Fingerprint;
            else
                woundCommands["authorityKind"] =
                    "accepted_mortal_wound_treatment_continuation";
            var definitionRoot = definitions.ToCanonicalRoot();
            var stateRoot = JsonNode.Parse(state.ToCanonicalJson())!.AsObject();
            var historyRoot = JsonNode.Parse(history.ToCanonicalJson())!.AsObject();
            var internalInputs = new JsonObject
            {
                ["authorityKind"] = anchorPlan is null
                    ? "accepted_mortal_wound_treatment_continuation"
                    : "accepted_mortal_wound_initial_create",
                ["definitions"] = definitionRoot.DeepClone(),
                ["state"] = stateRoot.DeepClone(),
                ["history"] = historyRoot.DeepClone(),
                ["ownerFingerprint"] = owners.Fingerprint,
                ["sourceFingerprint"] = sources.Fingerprint,
                ["woundInputFingerprint"] = bundle.InputFingerprint,
                ["woundStageBundleFingerprint"] = bundle.BundleFingerprint
            };
            if (anchorPlan is not null)
                internalInputs["woundAnchorPlanFingerprint"] = anchorPlan.Fingerprint;
            if (treatmentCurrentTimeInMinutes is not null)
            {
                internalInputs["treatmentSkillProjectionFingerprint"] =
                    treatmentSkillProjectionFingerprint;
                internalInputs["treatmentResourceFinalizationFingerprint"] =
                    treatmentResourcePublicationAuthority!.FinalizationFingerprint;
                internalInputs["treatmentResourceDraftFingerprint"] =
                    treatmentResourcePublicationAuthority.DraftFingerprint;
                internalInputs["treatmentResourcePublicationAuthorityFingerprint"] =
                    treatmentResourcePublicationAuthority.AuthorityFingerprint;
            }
            var effectIdentityJson = Read(
                EffectAcceptedTurnPlan.IdentityIndexPath);
            var effectIdentityRoot = stagedEffectPlan.IdentityIndexBeforeImage ??
                ParseObjectOrEmpty(effectIdentityJson);

            var requiredPaths = new HashSet<string>(
                WoundAcceptedTurnSnapshotContract.RequiredPaths,
                StringComparer.Ordinal)
            {
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                EffectAcceptedTurnPlan.IdentityIndexPath,
                EffectAcceptedTurnPlan.CommandPath,
                ResourceMaterializationContract.CommandPath,
                ResourcePendingResolutionState.PendingPath,
                LiveTurnPreparationService.TurnRequestPath,
                EffectAcceptedTurnInputComposer.WorldTimePath
            };
            requiredPaths.UnionWith(stagedEffectPlan.TouchedPaths);
            requiredPaths.UnionWith(stagedEffectPlan.DeletedPaths);
            requiredPaths.UnionWith(stagedEffectPlan.CarrierBeforeImages.Keys);
            requiredPaths.UnionWith(treatmentSkillBaselines.Keys);
            if (treatmentResourcePublicationAuthority is not null)
            {
                requiredPaths.UnionWith(
                    treatmentResourcePublicationAuthority.RegisteredOutcome
                        .ExpectedBeforeImages.Keys);
            }
            if (anchorPlan is not null)
            {
                requiredPaths.UnionWith(
                    EffectAcceptedTurnInputComposer.SourceAuthorityPaths);
            }
            foreach (var path in requiredPaths)
                _ = Read(path);
            if (issues.Count != 0)
                return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);

            AcceptedMechanicsDirectWoundPublicationAuthority?
                directPublicationAuthority = null;
            if (anchorPlan is not null)
            {
                directPublicationAuthority =
                    AcceptedMechanicsDirectWoundPublicationAuthority.Create(
                        bundle,
                        anchorPlan,
                        beforeImages,
                        DirectPublicationProof);
                internalInputs["directWoundPublicationAuthorityFingerprint"] =
                    directPublicationAuthority.Fingerprint;
            }
            var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
                Definitions: HashText(
                    "resource-definitions-v1",
                    definitionRoot.ToJsonString()),
                Owners: owners.Fingerprint,
                ResourceState: state.Fingerprint,
                ResourceHistory: history.Fingerprint,
                EffectSources: HashText(
                    "accepted-mechanics-effect-sources-v1",
                    stagedEffectPlan.SourceAuthorityFingerprint),
                EffectTargets: HashText(
                    "accepted-mechanics-effect-targets-v1",
                    stagedEffectPlan.TargetAuthorityFingerprint),
                EffectCarriers: HashText(
                    "accepted-mechanics-effect-carriers-v1",
                    stagedEffectPlan.CarrierAuthorityFingerprint),
                EffectIdentityIndex: HashText(
                    "effect-index-v1",
                    effectIdentityJson ?? "<missing>"),
                AcceptedEvents: HashNode("resource-events-v1", acceptedEvents),
                Commands: HashText(
                    "accepted-mechanics-commands-v1",
                    resourceCommands.Root.ToJsonString() + "\n" +
                    effectCommands.ToJsonString() + "\n" +
                    woundCommands.ToJsonString()),
                Pending: HashNode("accepted-mechanics-pending-v1", pendingInput),
                InternalInputs: HashNode(
                    "accepted-mechanics-internal-v1",
                    internalInputs),
                WoundCarriers: HashText(
                    "accepted-mechanics-wound-carriers-v1",
                    ComputeWoundCarrierAuthority(bundle.Input.PreTurnCarriers)),
                WoundIdentityIndex: HashText(
                    "accepted-mechanics-wound-index-v1",
                    bundle.Input.PreTurnIdentityIndex.ToJsonString()),
                WoundHistory: HashText(
                    "accepted-mechanics-wound-history-v1",
                    bundle.Input.PreTurnHistory.ToJsonString()));
            var context = new AcceptedMechanicsPlanningContext(
                definitionRoot,
                definitions,
                state,
                history,
                owners,
                sources,
                resourceCommands,
                effectIdentityRoot,
                stagedEffectPlan,
                ownerCompanionAfterImages: treatmentSkillAfterImages,
                registeredSystemOutcomes:
                    treatmentResourcePublicationAuthority is null
                        ? Array.Empty<IResourceRegisteredSystemOutcomeDraft>()
                        : new[]
                        {
                            treatmentResourcePublicationAuthority.RegisteredOutcome
                        },
                pendingResolutionState: pendingResult.State,
                resourceIdentityFactory:
                    treatmentResourcePublicationAuthority?.CreateIdentityFactory(),
                woundStageBundle: bundle,
                woundAnchorPlan: anchorPlan,
                directWoundPublicationAuthority: directPublicationAuthority,
                treatmentResourcePublicationAuthority:
                    treatmentResourcePublicationAuthority);
            var input = new AcceptedMechanicsInput(
                binding.SessionId,
                binding.RequestId,
                binding.SnapshotToken,
                binding.Realm,
                binding.Turn,
                acceptedEvents,
                resourceCommands.Root,
                effectCommands,
                pendingInput,
                internalInputs,
                fingerprints,
                beforeImages,
                issues,
                context,
                woundCommands,
                bundle.Input);
            return new AcceptedMechanicsWoundCommonInputCompositionResult(
                input,
                Array.Empty<ValidationIssue>());
        }
        catch (Exception exception) when (
            exception is ArgumentException or InvalidOperationException or
                InvalidDataException or JsonException or NullReferenceException)
        {
            issues.Add(Issue(
                WoundIdentityState.StatePath,
                "accepted_mechanics_wound_common_input_invalid",
                "one complete canonical common-plan input for the sealed wound bundle",
                exception.GetType().Name));
            return new AcceptedMechanicsWoundCommonInputCompositionResult(null, issues);
        }
    }

    private static void ValidateTurnRequest(
        string? json,
        WoundAcceptedTurnBinding binding,
        List<ValidationIssue> issues)
    {
        try
        {
            var root = JsonNode.Parse(json ?? string.Empty) as JsonObject;
            if (root is not null &&
                string.Equals(
                    root["sessionId"]?.GetValue<string>(),
                    binding.SessionId,
                    StringComparison.Ordinal) &&
                string.Equals(
                    root["requestId"]?.GetValue<string>(),
                    binding.RequestId,
                    StringComparison.Ordinal) &&
                root["turnNumber"]?.GetValue<int>() == binding.Turn &&
                string.Equals(
                    NormalizeRealm(
                        root["progressionControl"]?["currentRealm"]
                            ?.GetValue<string>() ??
                        root["currentRealm"]?.GetValue<string>()),
                    binding.Realm,
                    StringComparison.Ordinal))
            {
                return;
            }
        }
        catch (Exception exception) when (
            exception is JsonException or InvalidOperationException)
        {
        }
        issues.Add(Issue(
            LiveTurnPreparationService.TurnRequestPath,
            "accepted_mechanics_wound_turn_binding_mismatch",
            $"{binding.SessionId}/{binding.RequestId}/{binding.Realm}/{binding.Turn}",
            "missing, malformed, or different turn request"));
    }

    private static string NormalizeRealm(string? value) => value switch
    {
        "Mortal World" or "mortal_world" => "mortal_world",
        "Chaos Sea" or "chaos_sea" => "chaos_sea",
        "Shining Abode" or "shining_abode" => "shining_abode",
        _ => string.Empty
    };

    private static void ValidateLiveWoundRoots(
        Func<string, string?> read,
        WoundAcceptedTurnInput input,
        List<ValidationIssue> issues)
    {
        var expected = new Dictionary<string, JsonObject?>(StringComparer.Ordinal)
        {
            [WoundCarrierCatalog.PlayerPath] = input.PreTurnCarriers.PlayerWounds,
            [WoundCarrierCatalog.NpcPath] = input.PreTurnCarriers.NpcWounds,
            [WoundCarrierCatalog.EnemiesPath] = input.PreTurnCarriers.EnemyCombatants,
            [WoundCarrierCatalog.AlliesPath] = input.PreTurnCarriers.AllyCombatants,
            [WoundCarrierCatalog.AfterlifeProfilesPath] =
                input.PreTurnCarriers.AfterlifeProfiles,
            [WoundIdentityState.StatePath] = input.PreTurnIdentityIndex,
            [WoundHistoryState.HistoryPath] = input.PreTurnHistory
        };
        foreach (var pair in expected)
        {
            var liveJson = read(pair.Key);
            var liveRoot = ParseNullableObject(liveJson);
            if (JsonNode.DeepEquals(liveRoot, pair.Value) ||
                (liveJson is null && IsPristineMissingWoundRoot(pair.Key, pair.Value)))
            {
                continue;
            }

            issues.Add(Issue(
                pair.Key,
                "accepted_mechanics_wound_live_before_image_mismatch",
                pair.Value?.ToJsonString() ?? "missing",
                liveJson ?? "missing"));
        }
    }

    private static bool IsPristineMissingWoundRoot(
        string path,
        JsonObject? expected)
    {
        if (expected is null)
            return false;
        if (string.Equals(path, WoundIdentityState.StatePath, StringComparison.Ordinal))
        {
            var parsed = WoundIdentityState.Parse(expected.ToJsonString(), path);
            return parsed.IsValid && parsed.State!.Entries.Count == 0;
        }
        if (string.Equals(path, WoundHistoryState.HistoryPath, StringComparison.Ordinal))
        {
            var parsed = WoundHistoryState.Parse(expected.ToJsonString(), path);
            return parsed.IsValid && parsed.State!.NextOrdinal == 1 &&
                   parsed.State.Transitions.Count == 0;
        }
        if (!WoundCarrierCollectionAuthority.IsRegisteredPath(path))
            return false;

        var carriers = new WoundCarrierCatalogInput(
            string.Equals(path, WoundCarrierCatalog.PlayerPath, StringComparison.Ordinal)
                ? expected
                : null,
            string.Equals(path, WoundCarrierCatalog.NpcPath, StringComparison.Ordinal)
                ? expected
                : null,
            string.Equals(path, WoundCarrierCatalog.EnemiesPath, StringComparison.Ordinal)
                ? expected
                : null,
            string.Equals(path, WoundCarrierCatalog.AlliesPath, StringComparison.Ordinal)
                ? expected
                : null,
            string.Equals(
                path,
                WoundCarrierCatalog.AfterlifeProfilesPath,
                StringComparison.Ordinal)
                ? expected
                : null);
        var catalog = WoundCarrierCatalog.Build(carriers);
        return catalog.Issues.Count == 0 && catalog.Occurrences.Count == 0;
    }

    private static void ValidateLiveEffectRoots(
        Func<string, string?> read,
        EffectAcceptedTurnPlan effectPlan,
        List<ValidationIssue> issues)
    {
        foreach (var pair in effectPlan.CarrierBeforeImages)
        {
            if (!JsonNode.DeepEquals(ParseNullableObject(read(pair.Key)), pair.Value))
            {
                issues.Add(Issue(
                    pair.Key,
                    "accepted_mechanics_wound_effect_before_image_mismatch",
                    pair.Value?.ToJsonString() ?? "missing",
                    read(pair.Key) ?? "missing"));
            }
        }
        var liveIdentity = ParseObjectOrEmpty(read(
            EffectAcceptedTurnPlan.IdentityIndexPath));
        var expectedIdentity = effectPlan.IdentityIndexBeforeImage ??
                               new JsonObject
                               {
                                   ["schemaVersion"] = 1,
                                   ["entries"] = new JsonArray()
                               };
        if (!JsonNode.DeepEquals(liveIdentity, expectedIdentity))
        {
            issues.Add(Issue(
                EffectAcceptedTurnPlan.IdentityIndexPath,
                "accepted_mechanics_wound_effect_index_before_image_mismatch",
                expectedIdentity.ToJsonString(),
                liveIdentity.ToJsonString()));
        }
    }

    private static JsonObject? ParseNullableObject(string? json)
    {
        if (json is null)
            return null;
        return JsonNode.Parse(json) as JsonObject ??
               throw new InvalidDataException("Expected a strict JSON object.");
    }

    private static Dictionary<string, JsonNode?> CaptureMortalItemProjectionRoots(
        Func<string, string?> read,
        ICollection<ValidationIssue> issues)
    {
        var roots = new Dictionary<string, JsonNode?>(StringComparer.Ordinal);
        foreach (var path in MortalItemCanonicalProjectionPlanner.ProjectionRootPaths)
        {
            var parsed = MortalItemProjectionRootParser.Parse(read(path), path);
            foreach (var issue in parsed.Issues)
                issues.Add(issue);
            roots.Add(path, parsed.Root?.DeepClone());
        }
        return roots;
    }

    private static MortalItemCarrierCatalogInput CreateMortalItemCarrierCatalogInput(
        IReadOnlyDictionary<string, JsonNode?> roots)
    {
        var companions = MortalItemCanonicalProjectionPlanner.ProjectionRootPaths
            .Skip(8)
            .Where(path => roots[path] is JsonObject)
            .ToDictionary(
                static path => path,
                path => roots[path]!.DeepClone().AsObject(),
                StringComparer.Ordinal);
        return new MortalItemCarrierCatalogInput(
            roots[InventoryEquipmentService.ItemsPath] as JsonObject,
            roots[NpcCoreChangesContract.NpcCorePath] as JsonObject,
            roots[MortalItemAcceptedTransferCatalog.NpcCommandsPath] as JsonObject,
            roots[StorageTransportMoveService.CurrentLocationPath] as JsonObject,
            MortalItemProjectionRootParser.ToCarrierCatalogObject(
                roots[StorageTransportMoveService.VehiclesPath],
                StorageTransportMoveService.VehiclesPath),
            companions,
            roots[MortalLocationStorageContentsState.StatePath] as JsonObject);
    }

    private static JsonObject ParseObjectOrEmpty(string? json) =>
        json is null
            ? new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            }
            : JsonNode.Parse(json)?.AsObject() ??
              throw new InvalidDataException("Expected a strict JSON object.");

    private static string ComputeWoundCarrierAuthority(
        WoundCarrierCatalogInput carriers) =>
        new JsonObject
        {
            ["player"] = carriers.PlayerWounds?.DeepClone(),
            ["npcs"] = carriers.NpcWounds?.DeepClone(),
            ["enemies"] = carriers.EnemyCombatants?.DeepClone(),
            ["allies"] = carriers.AllyCombatants?.DeepClone(),
            ["afterlifeProfiles"] = carriers.AfterlifeProfiles?.DeepClone()
        }.ToJsonString();

    private static string HashNode(string domain, JsonNode node) =>
        HashText(domain, node.ToJsonString());

    private static string HashText(string domain, string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(domain + "\0" + value))).ToLowerInvariant();

    private static AcceptedMechanicsWoundCommonInputCompositionResult Failed(
        string path,
        string code,
        string expected,
        string actual) =>
        new(null, new[] { Issue(path, code, expected, actual) });

    private static ValidationIssue Issue(
        string path,
        string code,
        string expected,
        string actual) =>
        new(
            path,
            IssueSeverity.Error,
            "The sealed wound stage could not be composed into the common accepted plan.",
            code: code,
            section: "accepted_mechanics",
            expected: expected,
            actual: actual,
            repairHint:
                "Discard the stale stage and rebuild it from the current canonical turn and roots.");
}
