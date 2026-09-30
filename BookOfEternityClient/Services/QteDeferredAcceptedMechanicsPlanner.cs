using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class QteDeferredAcceptedMechanicsPreparation
{
    internal QteDeferredAcceptedMechanicsPreparation(
        AcceptedMechanicsPlan plan,
        JsonObject continuationRoot,
        JsonObject selectedTerminalBinding)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        ContinuationRoot = (continuationRoot ??
            throw new ArgumentNullException(nameof(continuationRoot)))
            .DeepClone().AsObject();
        SelectedTerminalBinding = (selectedTerminalBinding ??
            throw new ArgumentNullException(nameof(selectedTerminalBinding)))
            .DeepClone().AsObject();
    }

    internal AcceptedMechanicsPlan Plan { get; }
    internal JsonObject ContinuationRoot { get; }
    internal JsonObject SelectedTerminalBinding { get; }
}

/// <summary>
/// Rehydrates one accepted QTE terminal boundary exclusively from the
/// acceptance-time continuation. Direct QTE resource mutations are registered
/// as one system outcome in the same accepted-mechanics graph as effect
/// reactions; no live authority is rebuilt and no second reducer is invoked.
/// </summary>
internal static class QteDeferredAcceptedMechanicsPlanner
{
    private const string Realm = "mortal_world";

    internal static async Task<JsonArray> PreallocateIdentityLedgerAtAcceptanceAsync(
        JsonObject continuation,
        QteSceneService.QteOffer offer)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(offer);

        var roots = ReadSealedRoots(continuation);
        var reservations = new Dictionary<string, string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        var selections = EnumerateTerminalSelections(
                offer,
                ReadPositiveInt(continuation, "acceptedSourceTurn"))
            .ToArray();
        if (selections.Length == 0)
        {
            throw new InvalidDataException(
                "qte_deferred_identity_preallocation_empty: accepted offer has no reachable terminal selection authority.");
        }

        foreach (var selection in selections)
        {
            var rehydrated = await RehydrateAsync(
                continuation,
                roots,
                selection);
            var selectedBinding = CreateSelectedTerminalBinding(
                selection,
                rehydrated.ResourceDraft?.RegisteredOutcome?.Fingerprint);
            var ledger = QtePersistentIdentityLedger.CreateRecording(
                ReadExact(continuation, "continuationId"),
                ReadFingerprint(selectedBinding, "fingerprint"));
            var plan = BuildCommonPlan(
                continuation,
                roots,
                rehydrated,
                selectedBinding,
                ledger);
            if (plan.AwaitsPendingResolution)
            {
                PreallocateReceiptReplayIdentities(
                    continuation,
                    roots,
                    rehydrated,
                    selectedBinding,
                    plan,
                    ledger);
            }

            foreach (var node in ledger.ToCanonicalRoot().OfType<JsonObject>())
            {
                var semanticKey = ReadExact(node, "semanticKey");
                var identity = ReadExact(node, "identity");
                if (reservations.TryGetValue(semanticKey, out var existing))
                {
                    if (!string.Equals(existing, identity, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "qte_deferred_identity_preallocation_conflict: one semantic key produced different acceptance-time identities.");
                    }
                    continue;
                }
                if (!identities.Add(identity))
                {
                    throw new InvalidDataException(
                        "qte_deferred_identity_preallocation_conflict: one acceptance-time identity was assigned to different semantic keys.");
                }
                reservations.Add(semanticKey, identity);
            }
        }

        return new JsonArray(reservations
            .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
            .Select(static pair => (JsonNode)new JsonObject
            {
                ["semanticKey"] = pair.Key,
                ["identity"] = pair.Value
            })
            .ToArray());
    }

    internal static async Task<QteDeferredAcceptedMechanicsPreparation>
        PrepareAndSealTerminalSelectionAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            QteSceneService.QteRuntimeState runtime,
            QteSceneService.ActiveQteSceneState activeScene,
            QteSceneService.QteOffer offer,
            QteTerminalResourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(activeScene);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(selection);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var continuation = await QteDeferredEffectContinuation
            .ValidateForTerminalSelectionAsync(
                fileSystem,
                writeLease,
                activeScene,
                offer,
                selection.SourceTurn);
        var roots = ReadSealedRoots(continuation);
        var rehydrated = await RehydrateAsync(
            continuation,
            roots,
            selection);
        var selectedBinding = CreateSelectedTerminalBinding(
            selection,
            rehydrated.ResourceDraft?.RegisteredOutcome?.Fingerprint);
        var state = ReadExact(continuation, "state");
        var ledger = QtePersistentIdentityLedger.CreateReplay(
            ReadExact(continuation, "continuationId"),
            ReadFingerprint(selectedBinding, "fingerprint"),
            continuation["identityLedger"] as JsonArray,
            allowUnusedReservations: true);

        if (state == "terminal_selected")
        {
            if (continuation["selectedTerminalBinding"] is not JsonObject persisted ||
                !JsonNode.DeepEquals(persisted, selectedBinding))
            {
                throw new InvalidDataException(
                    "qte_deferred_terminal_selection_conflict: terminal selection is write-once and differs from the persisted binding.");
            }
        }

        var plan = BuildCommonPlan(
            continuation,
            roots,
            rehydrated,
            selectedBinding,
            ledger);
        ledger.ValidateComplete();

        if (state == "terminal_selected")
        {
            return new QteDeferredAcceptedMechanicsPreparation(
                plan,
                continuation,
                selectedBinding);
        }

        var continuationBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            QteDeferredEffectContinuation.StatePath) ??
            throw new InvalidDataException(
                "qte_deferred_continuation_missing: continuation disappeared before terminal selection was sealed.");
        var runtimeBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            QteSceneService.QteRuntimePath) ??
            throw new InvalidDataException(
                "qte_deferred_runtime_missing: active QTE runtime disappeared before terminal selection was sealed.");

        var selectedContinuation = continuation.DeepClone().AsObject();
        selectedContinuation["state"] = "terminal_selected";
        selectedContinuation["selectedTerminalBinding"] =
            selectedBinding.DeepClone();
        var continuationFingerprint =
            QteDeferredEffectContinuation.RefreshAuthorityFingerprint(
                selectedContinuation);

        var runtimeRoot = JsonSerializer.SerializeToNode(
            runtime,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        if (runtimeRoot["activeScene"] is not JsonObject activeRoot)
        {
            throw new InvalidDataException(
                "qte_deferred_runtime_invalid: active QTE scene is missing while sealing terminal selection.");
        }
        activeRoot["deferredEffectContinuationFingerprint"] =
            continuationFingerprint;
        activeRoot["effectResolutionState"] = "terminal_selected";

        var committed = await CoordinatedStateWriteHelper.TryCommitAsync(
            fileSystem,
            writeLease,
            ExactWrite(
                QteDeferredEffectContinuation.StatePath,
                continuationBytes,
                selectedContinuation.ToJsonString(
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)),
            ExactWrite(
                QteSceneService.QteRuntimePath,
                runtimeBytes,
                runtimeRoot.ToJsonString(
                    SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
        if (!committed)
        {
            throw new InvalidOperationException(
                "qte_deferred_terminal_selection_commit_conflict: continuation or runtime changed before write-once selection commit.");
        }

        activeScene.DeferredEffectContinuationFingerprint =
            continuationFingerprint;
        activeScene.EffectResolutionState = "terminal_selected";
        return new QteDeferredAcceptedMechanicsPreparation(
            plan,
            selectedContinuation,
            selectedBinding);
    }

    internal static async Task<QteDeferredAcceptedMechanicsPreparation>
        PrepareReceiptResumeAsync(
            JsonObject continuation,
            QteTerminalResourceSelection selection,
            ResourcePendingResolutionState pendingState,
            CanonicalBeforeImage pendingBeforeImage,
            JsonArray receipts)
    {
        ArgumentNullException.ThrowIfNull(continuation);
        ArgumentNullException.ThrowIfNull(selection);
        ArgumentNullException.ThrowIfNull(pendingState);
        ArgumentNullException.ThrowIfNull(pendingBeforeImage);
        ArgumentNullException.ThrowIfNull(receipts);
        if (ReadExact(continuation, "state") != "awaiting_receipt" ||
            continuation["selectedTerminalBinding"] is not JsonObject persisted)
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_selection_missing: receipt replay requires one persisted awaiting terminal selection.");
        }

        var roots = ReadSealedRoots(continuation);
        var rehydrated = await RehydrateAsync(
            continuation,
            roots,
            selection);
        var selectedBinding = CreateSelectedTerminalBinding(
            selection,
            rehydrated.ResourceDraft?.RegisteredOutcome?.Fingerprint);
        if (!JsonNode.DeepEquals(persisted, selectedBinding))
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_selection_conflict: receipt replay differs from the write-once terminal selection.");
        }
        var ledger = QtePersistentIdentityLedger.CreateReplay(
            ReadExact(continuation, "continuationId"),
            ReadFingerprint(selectedBinding, "fingerprint"),
            continuation["identityLedger"] as JsonArray,
            allowUnusedReservations: true);
        var plan = BuildCommonPlan(
            continuation,
            roots,
            rehydrated,
            selectedBinding,
            ledger,
            pendingState,
            pendingBeforeImage,
            receipts);
        ledger.ValidateComplete();
        return new QteDeferredAcceptedMechanicsPreparation(
            plan,
            continuation,
            selectedBinding);
    }

    private static void PreallocateReceiptReplayIdentities(
        JsonObject continuation,
        IReadOnlyDictionary<string, CanonicalBeforeImage> roots,
        RehydratedAuthority rehydrated,
        JsonObject selectedBinding,
        AcceptedMechanicsPlan initialPlan,
        QtePersistentIdentityLedger selectedLedger)
    {
        var currentPlan = initialPlan;
        for (var wave = 0;
             currentPlan.AwaitsPendingResolution;
             wave++)
        {
            if (wave > ResourceMaterializationContract.MaxTriggerDepth)
            {
                throw new InvalidDataException(
                    "qte_deferred_identity_preallocation_exhausted: bounded receipt discovery exceeded the canonical trigger-depth limit.");
            }
            if (!currentPlan.PendingAfterImages.TryGetValue(
                ResourcePendingResolutionState.PendingPath,
                out var pendingRoot) ||
                pendingRoot == null)
            {
                throw new InvalidDataException(
                    "qte_deferred_identity_preallocation_invalid: awaiting plan has no pending after-image.");
            }
            var parsed = ResourcePendingResolutionState.ParseCanonical(
                pendingRoot.ToJsonString(),
                rehydrated.Definitions,
                allowMissingPristine: false);
            if (!parsed.IsValid || parsed.State == null ||
                parsed.State.Requests.Count == 0)
            {
                throw PlanningFailure("identity-preallocation-pending", parsed.Issues);
            }
            var receipts = new JsonArray(parsed.State.Requests
                .Select(static request => (JsonNode)new JsonObject
                {
                    ["requestId"] = request.RequestId,
                    ["resultKind"] = "resource_delta",
                    ["amount"] = request.MaximumAmount,
                    ["reason"] = "Предварительное вычисление достижимой QTE-механики."
                })
                .ToArray());
            var simulationLedger = QtePersistentIdentityLedger.CreateRecording(
                ReadExact(continuation, "continuationId"),
                ReadFingerprint(selectedBinding, "fingerprint"));
            var pendingJson = pendingRoot.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
            currentPlan = BuildCommonPlan(
                continuation,
                roots,
                rehydrated,
                selectedBinding,
                simulationLedger,
                parsed.State,
                new CanonicalBeforeImage(
                    true,
                    Encoding.UTF8.GetBytes(pendingJson)),
                receipts);
            selectedLedger.MergeReservationsFrom(simulationLedger);
        }
    }

    private static IEnumerable<QteTerminalResourceSelection>
        EnumerateTerminalSelections(
            QteSceneService.QteOffer offer,
            int sourceTurn)
    {
        foreach (var chapter in offer.Chapters)
        {
            foreach (var action in chapter.Actions)
            {
                foreach (var (grade, target) in new[]
                         {
                             ("success", action.Routing.Success),
                             ("partial", action.Routing.Partial),
                             ("fail", action.Routing.Fail)
                         })
                {
                    if (string.IsNullOrWhiteSpace(target.TerminalOutcomeId))
                        continue;
                    var outcomeOrdinal = offer.TerminalOutcomes.FindIndex(outcome =>
                        string.Equals(
                            outcome.OutcomeId,
                            target.TerminalOutcomeId,
                            StringComparison.OrdinalIgnoreCase)) + 1;
                    if (outcomeOrdinal <= 0)
                    {
                        throw new InvalidDataException(
                            "qte_deferred_identity_preallocation_route_invalid: terminal route differs from the accepted offer graph.");
                    }
                    var outcome = offer.TerminalOutcomes[outcomeOrdinal - 1];
                    yield return new QteTerminalResourceSelection(
                        sourceTurn,
                        offer.QteId,
                        chapter.ChapterId,
                        action.ActionId,
                        grade,
                        outcomeOrdinal,
                        outcome.OutcomeId,
                        outcome.ResponseFragment);
                }
            }
        }
    }

    internal static async Task PublishAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(plan);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        if (plan.OwnerTransitions.Count != 0)
        {
            throw new InvalidDataException(
                "qte_deferred_owner_transition_unsupported: QTE-v1 cannot publish owner transition patches.");
        }

        var writes = CreatePublicationWrites(plan);

        if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                fileSystem,
                writeLease,
                writes))
        {
            throw new InvalidOperationException(
                "qte_deferred_accepted_mechanics_commit_conflict: sealed mechanics authority changed before atomic publication.");
        }
    }

    internal static CoordinatedStateWriteHelper.PlannedWrite[]
        CreatePublicationWrites(AcceptedMechanicsPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var afterImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        if (!plan.AwaitsPendingResolution)
        {
            afterImages[ResourceMaterializationContract.DefinitionsPath] =
                plan.DefinitionAfterImage.ToJsonString();
            afterImages[ResourceMaterializationContract.StatePath] =
                plan.StateAfterImage.ToJsonString();
            afterImages[ResourceMaterializationContract.HistoryPath] =
                plan.HistoryAfterImage.ToJsonString();

            var definitions = ResourceDefinitionCatalog.ParseCanonical(
                plan.DefinitionAfterImage.ToJsonString(),
                allowMissingPristine: false);
            var state = definitions.Catalog == null
                ? null
                : ResourceStateContract.ParseCanonical(
                    plan.StateAfterImage.ToJsonString(),
                    definitions.Catalog,
                    allowMissingPristine: false).Ledger;
            var history = definitions.Catalog == null
                ? null
                : ResourceHistoryState.ParseCanonical(
                    plan.HistoryAfterImage.ToJsonString(),
                    definitions.Catalog,
                    allowMissingPristine: false).History;
            if (definitions.Catalog == null || state == null || history == null)
            {
                throw new InvalidDataException(
                    "qte_deferred_resource_afterimage_invalid: common plan produced an invalid canonical resource quartet.");
            }
            afterImages[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
                CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(
                    plan.OwnerAuthority,
                    state,
                    history);
            afterImages[EffectAcceptedTurnPlan.IdentityIndexPath] =
                plan.EffectIdentityAfterImage.ToJsonString();
            foreach (var pair in plan.OwnerCompanionAfterImages)
                afterImages[pair.Key] = pair.Value.ToJsonString();
            foreach (var pair in plan.EffectCarrierAfterImages)
                afterImages[pair.Key] = pair.Value.ToJsonString();
        }
        foreach (var pair in plan.PendingAfterImages)
            afterImages[pair.Key] = pair.Value?.ToJsonString();
        foreach (var path in plan.ConsumedPaths)
            afterImages[path] = null;

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>();
        foreach (var path in plan.TouchedPaths
                     .OrderBy(static value => value, StringComparer.Ordinal))
        {
            if (!plan.BeforeImages.TryGetValue(path, out var beforeImage))
            {
                throw new InvalidDataException(
                    $"qte_deferred_before_image_missing: common plan path '{path}' has no sealed exact baseline.");
            }
            if (afterImages.TryGetValue(path, out var afterImage))
            {
                writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                    path,
                    PreviousJson: null,
                    NextJson: afterImage,
                    RequireCurrentBaseline: true,
                    GuardOnly: false,
                    ExactPrevious: beforeImage));
            }
            else
            {
                writes.Add(
                    CoordinatedStateWriteHelper.CreateExactGuardWrite(
                        path,
                        beforeImage));
            }
        }

        return writes.ToArray();
    }

    internal static async Task MarkTerminalAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject selectedTerminalBinding,
        string planFingerprint,
        JsonObject? resolvedWaveBinding = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(selectedTerminalBinding);
        if (!ResourceMaterializationContract.IsAuthorityFingerprint(planFingerprint))
            throw new ArgumentException("Expected exact common-plan fingerprint.", nameof(planFingerprint));

        var bytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            QteDeferredEffectContinuation.StatePath) ??
            throw new InvalidDataException(
                "qte_deferred_continuation_missing: terminal continuation disappeared before closure.");
        var root = ParseObject(bytes);
        var expectedState = resolvedWaveBinding == null
            ? "terminal_selected"
            : "awaiting_receipt";
        if (ReadExact(root, "state") != expectedState ||
            root["selectedTerminalBinding"] is not JsonObject persisted ||
            !JsonNode.DeepEquals(persisted, selectedTerminalBinding))
        {
            throw new InvalidDataException(
                "qte_deferred_terminal_closure_mismatch: only the exact write-once selected terminal may close continuation authority.");
        }
        if (resolvedWaveBinding != null)
        {
            if (root["currentWave"] is not JsonObject currentWave ||
                root["resolvedWaveBindings"] is not JsonArray resolvedWaves ||
                currentWave["requestId"]?.GetValue<string>() !=
                resolvedWaveBinding["requestId"]?.GetValue<string>() ||
                currentWave["waveId"]?.GetValue<string>() !=
                resolvedWaveBinding["waveId"]?.GetValue<string>() ||
                currentWave["ordinal"]?.GetValue<int>() !=
                resolvedWaveBinding["ordinal"]?.GetValue<int>())
            {
                throw new InvalidDataException(
                    "qte_deferred_terminal_wave_mismatch: terminal closure differs from the current resolved wave.");
            }
            resolvedWaves.Add(resolvedWaveBinding.DeepClone());
            root.Remove("currentWave");
        }
        root["state"] = "terminal";
        root["terminalFingerprint"] = HashText(
            "qte-deferred-terminal-v1",
            ReadFingerprint(selectedTerminalBinding, "fingerprint") + "\n" +
            planFingerprint + "\n" +
            (resolvedWaveBinding?["fingerprint"]?.GetValue<string>() ??
             "no-receipt-wave"));
        QteDeferredEffectContinuation.RefreshAuthorityFingerprint(root);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(
                fileSystem,
                writeLease,
                ExactWrite(
                    QteDeferredEffectContinuation.StatePath,
                    bytes,
                    root.ToJsonString(
                        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed))))
        {
            throw new InvalidOperationException(
                "qte_deferred_terminal_closure_conflict: continuation changed before terminal closure.");
        }
    }

    private static AcceptedMechanicsPlan BuildCommonPlan(
        JsonObject continuation,
        IReadOnlyDictionary<string, CanonicalBeforeImage> roots,
        RehydratedAuthority rehydrated,
        JsonObject selectedBinding,
        QtePersistentIdentityLedger ledger,
        ResourcePendingResolutionState? pendingStateOverride = null,
        CanonicalBeforeImage? pendingBeforeImageOverride = null,
        JsonArray? receiptsOverride = null)
    {
        var effectFactory = new LedgerEffectIdentityFactory(ledger);
        var publicationRoots = roots;
        if (pendingBeforeImageOverride != null)
        {
            var updatedRoots = roots.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(
                    pair.Value.Existed,
                    pair.Value.Bytes),
                StringComparer.Ordinal);
            updatedRoots[ResourcePendingResolutionState.PendingPath] =
                new CanonicalBeforeImage(
                    pendingBeforeImageOverride.Existed,
                    pendingBeforeImageOverride.Bytes);
            publicationRoots = new ReadOnlyDictionary<
                string,
                CanonicalBeforeImage>(updatedRoots);
        }
        var effectInput = EffectAcceptedTurnInputComposer.Compose(
            ReadExact(continuation, "sessionId"),
            ReadExact(continuation, "continuationId"),
            ReadPositiveInt(continuation, "acceptedSourceTurn"),
            EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot(),
            rehydrated.Carriers,
            rehydrated.Carriers,
            rehydrated.EffectIdentityRoot,
            rehydrated.SourceRoots,
            currentWorldTime: rehydrated.CurrentWorldTime,
            publicationCarrierBaselines: rehydrated.Carriers,
            realm: Realm);
        var effectResult = new EffectAcceptedTurnPlanCache(effectFactory)
            .GetOrBuild(effectInput);
        if (!effectResult.Success || effectResult.Plan == null)
            throw PlanningFailure("effect", effectResult.Issues);

        var emptyCommands = ResourceAcceptedTurnInputComposer.Parse(null);
        var pendingState = pendingStateOverride ?? rehydrated.Pending;
        var pendingInput = pendingState?.ToCanonicalRoot() ?? new JsonObject();
        var acceptedEvents = new JsonObject
        {
            ["acceptedEvents"] = new JsonArray()
        };
        var effectCommands = EffectAcceptedTurnInputComposer.CreateEmptyCommandRoot();
        if (receiptsOverride != null)
        {
            effectCommands["effectResolutionReceipts"] =
                receiptsOverride.DeepClone();
        }
        var internalInputs = new JsonObject
        {
            ["authorityKind"] = "qte_continuation",
            ["continuationId"] = ReadExact(continuation, "continuationId"),
            ["semanticTurnFingerprint"] =
                ReadFingerprint(continuation, "semanticTurnFingerprint"),
            ["selectedTerminalBinding"] = selectedBinding.DeepClone(),
            ["resourceDraftFingerprint"] =
                rehydrated.ResourceDraft?.RegisteredOutcome?.Fingerprint,
            ["ownerFingerprint"] = rehydrated.Owners.Fingerprint,
            ["sourceFingerprint"] = rehydrated.ResourceSources.Fingerprint
        };
        var definitionJson = ReadJson(
            roots[ResourceMaterializationContract.DefinitionsPath])!;
        var stateJson = ReadJson(
            roots[ResourceMaterializationContract.StatePath])!;
        var historyJson = ReadJson(
            roots[ResourceMaterializationContract.HistoryPath])!;
        var identityJson = ReadJson(
            roots[EffectAcceptedTurnPlan.IdentityIndexPath]);
        var fingerprints = new AcceptedMechanicsAuthorityFingerprints(
            Definitions: HashText("resource-definitions-v1", definitionJson),
            Owners: rehydrated.Owners.Fingerprint,
            ResourceState: rehydrated.State.Fingerprint,
            ResourceHistory: rehydrated.History.Fingerprint,
            EffectSources: HashText(
                "accepted-mechanics-effect-sources-v1",
                effectResult.Plan.SourceAuthorityFingerprint),
            EffectTargets: HashText(
                "accepted-mechanics-effect-targets-v1",
                effectResult.Plan.TargetAuthorityFingerprint),
            EffectCarriers: HashText(
                "accepted-mechanics-effect-carriers-v1",
                effectResult.Plan.CarrierAuthorityFingerprint),
            EffectIdentityIndex: HashText(
                "effect-index-v1",
                identityJson ?? "<missing>"),
            AcceptedEvents: HashNode(
                "resource-events-v1",
                acceptedEvents),
            Commands: HashText(
                "accepted-mechanics-commands-v1",
                emptyCommands.Root.ToJsonString() + "\n" +
                effectCommands.ToJsonString()),
            Pending: HashNode(
                "accepted-mechanics-pending-v1",
                pendingInput),
            InternalInputs: HashNode(
                "accepted-mechanics-internal-v1",
                internalInputs),
            WoundCarriers: HashText(
                "accepted-mechanics-wound-carriers-v1",
                "<missing>"),
            WoundIdentityIndex: HashText(
                "accepted-mechanics-wound-index-v1",
                "<missing>"),
            WoundHistory: HashText(
                "accepted-mechanics-wound-history-v1",
                "<missing>"));
        var registered = rehydrated.ResourceDraft?.RegisteredOutcome == null
            ? Array.Empty<IResourceRegisteredSystemOutcomeDraft>()
            : new[] { rehydrated.ResourceDraft.RegisteredOutcome };
        var context = new AcceptedMechanicsPlanningContext(
            JsonNode.Parse(definitionJson)!.AsObject(),
            rehydrated.Definitions,
            rehydrated.State,
            rehydrated.History,
            rehydrated.Owners,
            rehydrated.ResourceSources,
            emptyCommands,
            rehydrated.EffectIdentityRoot,
            effectResult.Plan,
            registeredSystemOutcomes: registered,
            pendingResolutionState: pendingState,
            resourceIdentityFactory: new LedgerResourceIdentityFactory(ledger),
            effectIdentityFactory: effectFactory,
            executionSequenceOffset:
                rehydrated.ResourceDraft?.ExecutionSequenceOffset ?? 0);
        var input = new AcceptedMechanicsInput(
            ReadExact(continuation, "sessionId"),
            ReadExact(continuation, "continuationId") + "_terminal",
            ReadExact(continuation, "continuationId"),
            Realm,
            ReadPositiveInt(continuation, "acceptedSourceTurn"),
            acceptedEvents,
            emptyCommands.Root,
            effectCommands,
            pendingInput,
            internalInputs,
            fingerprints,
            publicationRoots,
            Array.Empty<ValidationIssue>(),
            context);
        var commonResult = new AcceptedMechanicsPlanCache(
                AcceptedMechanicsPlanner.BuildAcceptedPlan)
            .GetOrBuildValidated(input);
        if (!commonResult.Success || commonResult.Plan == null)
            throw PlanningFailure("common", commonResult.Issues);
        return commonResult.Plan;
    }

    private static async Task<RehydratedAuthority> RehydrateAsync(
        JsonObject continuation,
        IReadOnlyDictionary<string, CanonicalBeforeImage> roots,
        QteTerminalResourceSelection selection)
    {
        var definitionsResult = ResourceDefinitionCatalog.ParseCanonical(
            ReadJson(roots[ResourceMaterializationContract.DefinitionsPath]),
            allowMissingPristine: false);
        if (!definitionsResult.IsValid || definitionsResult.Catalog == null)
            throw PlanningFailure("definitions", definitionsResult.Issues);
        var definitions = definitionsResult.Catalog;
        var stateResult = ResourceStateContract.ParseCanonical(
            ReadJson(roots[ResourceMaterializationContract.StatePath]),
            definitions,
            allowMissingPristine: false);
        if (!stateResult.IsValid || stateResult.Ledger == null)
            throw PlanningFailure("state", stateResult.Issues);
        var historyResult = ResourceHistoryState.ParseCanonical(
            ReadJson(roots[ResourceMaterializationContract.HistoryPath]),
            definitions,
            allowMissingPristine: false);
        if (!historyResult.IsValid || historyResult.History == null)
            throw PlanningFailure("history", historyResult.Issues);
        var agreement = historyResult.History.ValidateStateAgreement(
            stateResult.Ledger);
        if (agreement.Count != 0)
            throw PlanningFailure("resource-agreement", agreement);

        Task<string?> ReadSealedAsync(string path)
        {
            if (!roots.TryGetValue(path, out var beforeImage))
            {
                throw new InvalidDataException(
                    $"qte_deferred_sealed_root_missing: owner authority requested unsealed path '{path}'.");
            }
            return Task.FromResult(ReadJson(beforeImage));
        }
        var ownerResult = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            ReadSealedAsync,
            stateResult.Ledger,
            historyResult.History,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!ownerResult.IsValid || ownerResult.Authority == null)
            throw PlanningFailure("owners", ownerResult.Issues);

        var resourceDraft = QteTerminalResourceOutcome.BuildPlanningDraft(
            selection,
            definitions,
            stateResult.Ledger,
            historyResult.History,
            ownerResult.Authority);
        if (!resourceDraft.IsValid)
            throw PlanningFailure("qte-resource", resourceDraft.Issues);
        var sourceResult = resourceDraft.Sources == null
            ? ResourceMutationSourceCatalog.Create(
                Array.Empty<ResourceMutationSourceExport>())
            : null;
        var resourceSources = resourceDraft.Sources ?? sourceResult!.Catalog;
        if (resourceSources == null ||
            sourceResult is { IsValid: false })
        {
            throw PlanningFailure(
                "resource-sources",
                sourceResult?.Issues ?? Array.Empty<ValidationIssue>());
        }

        var carriers = new EffectCarrierCatalogInput(
            ReadObject(roots[EffectCarrierCatalog.PlayerPath]),
            ReadObject(roots[EffectCarrierCatalog.NpcPath]),
            ReadObject(roots[EffectCarrierCatalog.EnemiesPath]),
            ReadObject(roots[EffectCarrierCatalog.AlliesPath]),
            ReadObject(roots[EffectCarrierCatalog.AfterlifeProfilesPath]),
            ReadObject(roots[EffectCarrierCatalog.SpiritualConflictPath]));
        var carrierCatalog = EffectCarrierCatalog.Build(carriers);
        if (carrierCatalog.Issues.Count != 0)
            throw PlanningFailure("effect-carriers", carrierCatalog.Issues);
        var effectIdentityRoot = ReadObject(
            roots[EffectAcceptedTurnPlan.IdentityIndexPath]) ??
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["entries"] = new JsonArray()
            };
        var sourceRoots = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .Distinct(StringComparer.Ordinal)
            .ToDictionary(
                static path => path,
                path => ReadNode(roots[path]),
                StringComparer.Ordinal);
        var currentWorldTime = EffectAcceptedTurnInputComposer
            .ReadCanonicalWorldTime(
                ReadJson(roots[EffectAcceptedTurnInputComposer.WorldTimePath]));
        var pendingJson = ReadJson(
            roots[ResourcePendingResolutionState.PendingPath]);
        var pendingResult = ResourcePendingResolutionState.ParseCanonical(
            pendingJson,
            definitions,
            allowMissingPristine: true);
        if (pendingResult.Issues.Count != 0)
            throw PlanningFailure("pending", pendingResult.Issues);

        return new RehydratedAuthority(
            definitions,
            stateResult.Ledger,
            historyResult.History,
            ownerResult.Authority,
            resourceDraft,
            resourceSources,
            carriers,
            effectIdentityRoot,
            new ReadOnlyDictionary<string, JsonNode?>(sourceRoots),
            currentWorldTime,
            pendingResult.State);
    }

    private static JsonObject CreateSelectedTerminalBinding(
        QteTerminalResourceSelection selection,
        string? resourceDraftFingerprint)
    {
        var binding = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["sourceTurn"] = selection.SourceTurn,
            ["qteId"] = selection.QteId,
            ["chapterId"] = selection.ChapterId,
            ["actionId"] = selection.ActionId,
            ["grade"] = selection.Grade,
            ["outcomeOrdinal"] = selection.OutcomeOrdinal,
            ["outcomeId"] = selection.OutcomeId,
            ["responseFingerprint"] = HashNode(
                "qte-deferred-terminal-response-v1",
                selection.ResponseFragment),
            ["resourceDraftFingerprint"] = resourceDraftFingerprint
        };
        binding["fingerprint"] = HashNode(
            "qte-deferred-terminal-selection-v1",
            binding);
        return binding;
    }

    private static IReadOnlyDictionary<string, CanonicalBeforeImage>
        ReadSealedRoots(JsonObject continuation)
    {
        if (continuation["sealedRootBindings"] is not JsonArray bindings)
            throw new InvalidDataException("QTE continuation has no sealed roots.");
        var result = new Dictionary<string, CanonicalBeforeImage>(
            StringComparer.Ordinal);
        foreach (var node in bindings.OfType<JsonObject>())
        {
            var path = ReadExact(node, "path");
            var existed = node["existed"]?.GetValue<bool>() ??
                throw new InvalidDataException(
                    "QTE sealed-root existence evidence is missing.");
            byte[]? bytes = null;
            if (existed)
            {
                try
                {
                    bytes = Convert.FromBase64String(
                        node["payloadBase64"]?.GetValue<string>() ?? "");
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException(
                        "QTE sealed-root payload is invalid.",
                        ex);
                }
            }
            result.Add(path, new CanonicalBeforeImage(existed, bytes));
        }
        return new ReadOnlyDictionary<string, CanonicalBeforeImage>(result);
    }

    private static CoordinatedStateWriteHelper.PlannedWrite ExactWrite(
        string path,
        byte[] previousBytes,
        string nextJson) =>
        new(
            path,
            PreviousJson: null,
            NextJson: nextJson,
            RequireCurrentBaseline: true,
            GuardOnly: false,
            ExactPrevious: new CanonicalBeforeImage(true, previousBytes));

    private static string? ReadJson(CanonicalBeforeImage beforeImage)
    {
        if (!beforeImage.Existed)
            return null;
        try
        {
            return new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(beforeImage.Bytes!)
                .TrimStart('\uFEFF');
        }
        catch (DecoderFallbackException ex)
        {
            throw new InvalidDataException(
                "QTE continuation contains non-UTF8 authority.",
                ex);
        }
    }

    private static JsonObject? ReadObject(CanonicalBeforeImage beforeImage)
    {
        var node = ReadNode(beforeImage);
        return node == null
            ? null
            : node as JsonObject ??
              throw new InvalidDataException(
                  "QTE sealed JSON authority must be an object.");
    }

    private static JsonNode? ReadNode(CanonicalBeforeImage beforeImage)
    {
        var json = ReadJson(beforeImage);
        if (json == null)
            return null;
        try
        {
            return JsonNode.Parse(json) ??
                throw new InvalidDataException(
                    "QTE sealed JSON authority cannot be JSON null.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException(
                "QTE sealed JSON authority is malformed.",
                ex);
        }
    }

    private static JsonObject ParseObject(byte[] bytes)
    {
        var beforeImage = new CanonicalBeforeImage(true, bytes);
        return ReadObject(beforeImage) ??
            throw new InvalidDataException("Expected QTE continuation object.");
    }

    private static string ReadExact(JsonObject root, string field)
    {
        var value = root[field]?.GetValue<string>();
        return ResourceMaterializationContract.IsExactIdentifier(value)
            ? value!
            : throw new InvalidDataException(
                $"QTE continuation field '{field}' is not an exact identifier.");
    }

    private static string ReadFingerprint(JsonObject root, string field)
    {
        var value = root[field]?.GetValue<string>();
        return ResourceMaterializationContract.IsAuthorityFingerprint(value)
            ? value!
            : throw new InvalidDataException(
                $"QTE continuation field '{field}' is not a fingerprint.");
    }

    private static int ReadPositiveInt(JsonObject root, string field)
    {
        if (root[field] is JsonValue node &&
            node.TryGetValue<int>(out var value) &&
            value > 0)
        {
            return value;
        }
        throw new InvalidDataException(
            $"QTE continuation field '{field}' is not a positive integer.");
    }

    private static InvalidDataException PlanningFailure(
        string boundary,
        IReadOnlyList<ValidationIssue> issues)
    {
        var summary = string.Join("; ", issues.Take(8).Select(issue =>
            string.IsNullOrWhiteSpace(issue.Code)
                ? $"{issue.FilePath}: {issue.Message}; expected={issue.Expected}; actual={issue.Actual}"
                : $"{issue.Code} at {issue.FilePath}; expected={issue.Expected}; actual={issue.Actual}"));
        return new InvalidDataException(
            $"qte_deferred_{boundary}_planning_invalid: {summary}");
    }

    private static string HashNode(string domain, JsonNode node) =>
        HashText(domain, node.ToJsonString());

    private static string HashText(string domain, string value) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(
            Encoding.UTF8.GetBytes(domain + "\0" + value)))
            .ToLowerInvariant();

    private sealed record RehydratedAuthority(
        ResourceDefinitionCatalog Definitions,
        ResourceStateLedger State,
        ResourceHistoryState History,
        ResourceOwnerAuthority Owners,
        QteTerminalResourcePlanningDraft? ResourceDraft,
        ResourceMutationSourceCatalog ResourceSources,
        EffectCarrierCatalogInput Carriers,
        JsonObject EffectIdentityRoot,
        IReadOnlyDictionary<string, JsonNode?> SourceRoots,
        long? CurrentWorldTime,
        ResourcePendingResolutionState? Pending);

    private sealed class LedgerResourceIdentityFactory :
        AcceptedMechanicsIdentityFactory
    {
        private readonly QtePersistentIdentityLedger _ledger;

        internal LedgerResourceIdentityFactory(
            QtePersistentIdentityLedger ledger) =>
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

        internal override string CreateOperationId() =>
            _ledger.Allocate("resource_operation", "resource_operation_");

        internal override string CreateTransitionId() =>
            _ledger.Allocate("resource_transition", "resource_transition_");
    }

    private sealed class LedgerEffectIdentityFactory : EffectIdentityFactory
    {
        private readonly QtePersistentIdentityLedger _ledger;

        internal LedgerEffectIdentityFactory(QtePersistentIdentityLedger ledger) =>
            _ledger = ledger ?? throw new ArgumentNullException(nameof(ledger));

        internal override string CreateEffectId() =>
            _ledger.Allocate("effect", "effect_");

        internal override string CreateTransitionId() =>
            _ledger.Allocate("effect_transition", "effect_transition_");

        internal override string CreateResolutionId() =>
            _ledger.Allocate("effect_resolution", "effect_resolution_");

        internal override string CreateCombatantId() =>
            _ledger.Allocate("combatant", "combatant_");

        internal override string CreateMemberId() =>
            _ledger.Allocate("member", "member_");
    }

    private sealed class QtePersistentIdentityLedger
    {
        private readonly string _continuationId;
        private readonly string _selectionFingerprint;
        private readonly bool _replay;
        private readonly bool _allowUnusedReservations;
        private readonly Dictionary<string, string> _persisted;
        private readonly Dictionary<string, string> _reservations = new(
            StringComparer.Ordinal);
        private readonly Dictionary<string, int> _counts = new(
            StringComparer.Ordinal);
        private readonly List<KeyValuePair<string, string>> _used = new();

        private QtePersistentIdentityLedger(
            string continuationId,
            string selectionFingerprint,
            bool replay,
            bool allowUnusedReservations,
            Dictionary<string, string> persisted)
        {
            _continuationId = continuationId;
            _selectionFingerprint = selectionFingerprint;
            _replay = replay;
            _allowUnusedReservations = allowUnusedReservations;
            _persisted = persisted;
        }

        internal static QtePersistentIdentityLedger CreateRecording(
            string continuationId,
            string selectionFingerprint) =>
            new(
                continuationId,
                selectionFingerprint,
                replay: false,
                allowUnusedReservations: false,
                new Dictionary<string, string>(StringComparer.Ordinal));

        internal static QtePersistentIdentityLedger CreateReplay(
            string continuationId,
            string selectionFingerprint,
            JsonArray? root,
            bool allowUnusedReservations = false)
        {
            if (root == null)
            {
                throw new InvalidDataException(
                    "qte_deferred_identity_ledger_missing: terminal-selected continuation has no persistent identity ledger.");
            }
            var persisted = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var node in root.OfType<JsonObject>())
            {
                var key = ReadExact(node, "semanticKey");
                var identity = ReadExact(node, "identity");
                if (!persisted.TryAdd(key, identity))
                {
                    throw new InvalidDataException(
                        "qte_deferred_identity_ledger_duplicate: semantic identity is duplicated.");
                }
            }
            return new QtePersistentIdentityLedger(
                continuationId,
                selectionFingerprint,
                replay: true,
                allowUnusedReservations,
                persisted);
        }

        internal string Allocate(string kind, string prefix)
        {
            _counts.TryGetValue(kind, out var ordinal);
            ordinal++;
            _counts[kind] = ordinal;
            var semanticKey = "selected:" + _selectionFingerprint + ":" +
                              kind + ":" + ordinal.ToString("D4");
            string identity;
            if (_replay)
            {
                if (!_persisted.TryGetValue(semanticKey, out identity!))
                {
                    throw new InvalidDataException(
                        $"qte_deferred_identity_ledger_exhausted: no persisted identity for '{semanticKey}'.");
                }
            }
            else
            {
                var suffix = Convert.ToHexString(SHA256.HashData(
                        Encoding.UTF8.GetBytes(
                            "qte-deferred-identity-v1\0" + _continuationId +
                            "\0" + semanticKey)))
                    .ToLowerInvariant()[..32];
                identity = prefix + suffix;
            }
            _used.Add(KeyValuePair.Create(semanticKey, identity));
            return identity;
        }

        internal void ValidateComplete()
        {
            if (!_replay || _allowUnusedReservations)
                return;
            var used = _used.Select(static pair => pair.Key)
                .ToHashSet(StringComparer.Ordinal);
            var unused = _persisted.Keys.Where(key => !used.Contains(key)).ToArray();
            if (unused.Length != 0)
            {
                throw new InvalidDataException(
                    "qte_deferred_identity_ledger_replay_mismatch: persisted selection identities were not consumed exactly.");
            }
        }

        internal void MergeReservationsFrom(
            QtePersistentIdentityLedger other)
        {
            ArgumentNullException.ThrowIfNull(other);
            foreach (var pair in other.ExportEntries())
            {
                var usedIdentity = _used.FirstOrDefault(candidate =>
                    string.Equals(
                        candidate.Key,
                        pair.Key,
                        StringComparison.Ordinal));
                if (!string.IsNullOrEmpty(usedIdentity.Key))
                {
                    if (!string.Equals(
                            usedIdentity.Value,
                            pair.Value,
                            StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "qte_deferred_identity_reservation_conflict: one semantic key resolved to different identities.");
                    }
                    continue;
                }
                if (_reservations.TryGetValue(pair.Key, out var existing))
                {
                    if (!string.Equals(existing, pair.Value, StringComparison.Ordinal))
                    {
                        throw new InvalidDataException(
                            "qte_deferred_identity_reservation_conflict: one semantic key resolved to different identities.");
                    }
                    continue;
                }
                if (_used.Any(candidate =>
                        string.Equals(
                            candidate.Value,
                            pair.Value,
                            StringComparison.Ordinal)) ||
                    _reservations.Values.Contains(
                        pair.Value,
                        StringComparer.Ordinal))
                {
                    throw new InvalidDataException(
                        "qte_deferred_identity_reservation_conflict: one identity was assigned to different semantic keys.");
                }
                _reservations.Add(pair.Key, pair.Value);
            }
        }

        private IReadOnlyDictionary<string, string> ExportEntries()
        {
            var entries = new Dictionary<string, string>(
                _reservations,
                StringComparer.Ordinal);
            foreach (var pair in _used)
                entries[pair.Key] = pair.Value;
            return entries;
        }

        internal JsonArray ToCanonicalRoot() =>
            new(ExportEntries()
                .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                .Select(pair => (JsonNode)new JsonObject
                {
                    ["semanticKey"] = pair.Key,
                    ["identity"] = pair.Value
                })
                .ToArray());
    }
}
