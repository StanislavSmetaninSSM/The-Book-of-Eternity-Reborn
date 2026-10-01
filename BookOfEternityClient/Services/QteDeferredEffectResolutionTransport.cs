using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed record QteDeferredEffectReceiptResume(
    QteDeferredAcceptedMechanicsPreparation Preparation,
    JsonObject ResolvedWaveBinding);

/// <summary>
/// Publishes the narrow GM-facing receipt frontier for one selected Mortal QTE
/// without creating ordinary-turn authority or exposing tentative mechanics.
/// </summary>
internal static class QteDeferredEffectResolutionTransport
{
    private sealed record WaveAuthority(
        JsonObject Request,
        JsonObject CurrentWave);

    private const string RequestKind = "qte_deferred_effect_resolution";
    private static readonly string[] RequestFields =
    [
        "schemaVersion", "requestKind", "sessionId", "sessionGeneration",
        "continuationId", "requestId", "waveId", "waveOrdinal",
        "acceptedSourceTurn", "qteId", "selectedTerminalFingerprint",
        "pendingStateFingerprint", "fullTurnFingerprint",
        "semanticTurnFingerprint", "safePacket"
    ];
    private static readonly string[] ReceiptFields =
    [
        "schemaVersion", "requestKind", "sessionId", "sessionGeneration",
        "continuationId", "requestId", "waveId", "waveOrdinal",
        "acceptedSourceTurn", "qteId", "selectedTerminalFingerprint",
        "pendingStateFingerprint", "fullTurnFingerprint",
        "semanticTurnFingerprint", "effectResolutionReceipts"
    ];
    private static readonly string[] ReadyFields =
    [
        "schemaVersion", "requestKind", "sessionId", "sessionGeneration",
        "continuationId", "requestId", "waveId", "waveOrdinal",
        "acceptedSourceTurn", "qteId", "pendingStateFingerprint",
        "receiptsFingerprint", "timestamp", "status"
    ];
    private static readonly string[] ReceiptCorrelationFields =
    [
        "requestKind", "sessionId", "sessionGeneration", "continuationId",
        "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId",
        "selectedTerminalFingerprint", "pendingStateFingerprint",
        "fullTurnFingerprint", "semanticTurnFingerprint"
    ];
    private static readonly string[] ReadyCorrelationFields =
    [
        "requestKind", "sessionId", "sessionGeneration", "continuationId",
        "requestId", "waveId", "waveOrdinal", "acceptedSourceTurn", "qteId",
        "pendingStateFingerprint"
    ];

    internal static async Task<QteDeferredEffectReceiptResume>
        PrepareResumeAsync(
            FileSystemManager fileSystem,
            FileSystemManager.CanonicalWriteLease writeLease,
            QteSceneService.ActiveQteSceneState activeScene,
            QteSceneService.QteOffer offer,
            QteTerminalResourceSelection selection)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(activeScene);
        ArgumentNullException.ThrowIfNull(offer);
        ArgumentNullException.ThrowIfNull(selection);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var continuation = await QteDeferredEffectContinuation
            .ValidateForReceiptResumeAsync(
                fileSystem,
                writeLease,
                activeScene,
                offer,
                selection.SourceTurn);
        var requestBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.RequestPath,
            "qte_deferred_request_missing");
        var receiptBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReceiptPath,
            "qte_deferred_receipt_missing");
        var readyBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReadyPath,
            "qte_deferred_ready_missing");
        var request = ParseObject(
            requestBytes,
            QteDeferredEffectContinuation.RequestPath);
        var receipt = ParseObject(
            receiptBytes,
            QteDeferredEffectContinuation.ReceiptPath);
        var ready = ParseObject(
            readyBytes,
            QteDeferredEffectContinuation.ReadyPath);

        ValidateRequestRoot(request);
        ValidateReceiptRoot(receipt);
        ValidateReadyRoot(ready);
        ValidateCommonCorrelation(request, receipt, ReceiptCorrelationFields);
        ValidateCommonCorrelation(request, ready, ReadyCorrelationFields);
        ValidateRequestAgainstContinuation(request, continuation, selection);

        var definitionBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            ResourceMaterializationContract.DefinitionsPath,
            "qte_deferred_definitions_missing");
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            Encoding.UTF8.GetString(definitionBytes).TrimStart('\uFEFF'),
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_definitions_invalid: sealed resource definitions are invalid.");
        }
        var pendingBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            ResourcePendingResolutionState.PendingPath,
            "qte_deferred_pending_missing");
        var pendingResult = ResourcePendingResolutionState.ParseCanonical(
            Encoding.UTF8.GetString(pendingBytes).TrimStart('\uFEFF'),
            definitions.Catalog,
            allowMissingPristine: false);
        if (!pendingResult.IsValid || pendingResult.State == null ||
            pendingResult.State.Requests.Count == 0)
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_pending_invalid: current wave has no valid pending authority.");
        }
        var pending = pendingResult.State;
        ValidateRequestAgainstPending(request, continuation, pending);

        var expectedReceiptFingerprint = HashBytes(receiptBytes);
        if (ReadFingerprint(ready, "receiptsFingerprint") !=
            expectedReceiptFingerprint ||
            ReadExact(ready, "status") != "success" ||
            ready["timestamp"] is not JsonValue timestampNode ||
            !timestampNode.TryGetValue<string>(out var timestamp) ||
            !DateTimeOffset.TryParse(
                timestamp,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.RoundtripKind,
                out _))
        {
            throw new InvalidDataException(
                "qte_deferred_ready_invalid: ready-last authority does not seal the exact receipt bytes.");
        }
        if (receipt["effectResolutionReceipts"] is not JsonArray receipts)
        {
            throw new InvalidDataException(
                "qte_deferred_receipts_invalid: effectResolutionReceipts must be an array.");
        }

        var preparation = await QteDeferredAcceptedMechanicsPlanner
            .PrepareReceiptResumeAsync(
                continuation,
                selection,
                pending,
                new CanonicalBeforeImage(true, pendingBytes),
                receipts);
        var resolvedWaveBinding = new JsonObject
        {
            ["requestId"] = ReadExact(request, "requestId"),
            ["waveId"] = ReadExact(request, "waveId"),
            ["ordinal"] = ReadInt(request, "waveOrdinal"),
            ["pendingStateFingerprint"] = pending.Fingerprint,
            ["receiptsFingerprint"] = expectedReceiptFingerprint
        };
        resolvedWaveBinding["fingerprint"] = HashNode(
            "qte-deferred-resolved-wave-v1",
            resolvedWaveBinding);
        return new QteDeferredEffectReceiptResume(
            preparation,
            resolvedWaveBinding);
    }

    internal static async Task PublishAwaitingReceiptAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.ActiveQteSceneState activeScene,
        QteDeferredAcceptedMechanicsPreparation preparation,
        Func<string, Task>? afterLegacyMutationAsync = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(activeScene);
        ArgumentNullException.ThrowIfNull(preparation);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var plan = preparation.Plan;
        if (!plan.AwaitsPendingResolution || plan.PendingGmPacket == null)
        {
            throw new InvalidOperationException(
                "qte_deferred_pending_transport_unexpected: the common plan has no bounded receipt frontier.");
        }

        if (!plan.PendingAfterImages.TryGetValue(
                ResourcePendingResolutionState.PendingPath,
                out var pendingRoot) ||
            pendingRoot == null)
        {
            throw new InvalidDataException(
                "qte_deferred_pending_afterimage_missing: bounded QTE planning produced no canonical pending state.");
        }

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            plan.DefinitionAfterImage.ToJsonString(),
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
        {
            throw new InvalidDataException(
                "qte_deferred_pending_definitions_invalid: bounded QTE planning produced invalid resource definitions.");
        }
        var parsedPending = ResourcePendingResolutionState.ParseCanonical(
            pendingRoot.ToJsonString(),
            definitions.Catalog,
            allowMissingPristine: false);
        if (!parsedPending.IsValid || parsedPending.State == null ||
            parsedPending.State.Requests.Count == 0)
        {
            throw new InvalidDataException(
                "qte_deferred_pending_state_invalid: bounded QTE planning produced invalid or empty pending authority.");
        }
        var pending = parsedPending.State;
        var safePacket = plan.PendingGmPacket;
        if (!JsonNode.DeepEquals(safePacket, pending.BuildSafeGmPacket()))
        {
            throw new InvalidDataException(
                "qte_deferred_safe_packet_mismatch: the common plan packet differs from canonical pending authority.");
        }

        var continuationBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.StatePath,
            "qte_deferred_continuation_missing");
        var continuation = ParseObject(
            continuationBytes,
            QteDeferredEffectContinuation.StatePath);
        if (!JsonNode.DeepEquals(continuation, preparation.ContinuationRoot) ||
            ReadExact(continuation, "state") != "terminal_selected" ||
            continuation["selectedTerminalBinding"] is not JsonObject selected ||
            !JsonNode.DeepEquals(selected, preparation.SelectedTerminalBinding))
        {
            throw new InvalidDataException(
                "qte_deferred_pending_selection_mismatch: only the exact write-once terminal selection may publish a receipt frontier.");
        }

        var runtimeBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteSceneService.QteRuntimePath,
            "qte_deferred_runtime_missing");
        var runtime = ParseObject(runtimeBytes, QteSceneService.QteRuntimePath);
        if (runtime["activeScene"] is not JsonObject activeRoot ||
            ReadExact(activeRoot, "deferredEffectContinuationId") !=
            ReadExact(continuation, "continuationId") ||
            ReadExact(activeRoot, "effectResolutionState") != "terminal_selected")
        {
            throw new InvalidDataException(
                "qte_deferred_pending_runtime_mismatch: active QTE runtime is not at the sealed terminal boundary.");
        }

        var requestBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.RequestPath);
        var receiptBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReceiptPath);
        var readyBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReadyPath);
        if (requestBefore.Existed || receiptBefore.Existed || readyBefore.Existed)
        {
            throw new InvalidDataException(
                "qte_deferred_transport_stale: initial bounded wave requires empty request, receipt, and ready paths.");
        }

        var wave = CreateWaveAuthority(
            continuation,
            selected,
            pending,
            safePacket);

        var awaitingContinuation = continuation.DeepClone().AsObject();
        awaitingContinuation["state"] = "awaiting_receipt";
        awaitingContinuation["currentWave"] = wave.CurrentWave;
        var continuationFingerprint =
            QteDeferredEffectContinuation.RefreshAuthorityFingerprint(
                awaitingContinuation);
        activeRoot["effectResolutionState"] = "awaiting_receipt";
        activeRoot["deferredEffectContinuationFingerprint"] =
            continuationFingerprint;

        var writes = QteDeferredAcceptedMechanicsPlanner
            .CreatePublicationWrites(plan)
            .ToList();
        writes.Add(CoordinatedStateWriteHelper.CreateExactGuardWrite(
            QteDeferredEffectContinuation.ReceiptPath,
            receiptBefore));
        writes.Add(CoordinatedStateWriteHelper.CreateExactGuardWrite(
            QteDeferredEffectContinuation.ReadyPath,
            readyBefore));
        writes.Add(ExactWrite(
            QteDeferredEffectContinuation.StatePath,
            new CanonicalBeforeImage(true, continuationBytes),
            awaitingContinuation));
        writes.Add(ExactWrite(
            QteSceneService.QteRuntimePath,
            new CanonicalBeforeImage(true, runtimeBytes),
            runtime));
        // The request is deliberately the last visible write. A daemon cannot
        // observe work before pending/continuation/runtime authority is durable.
        writes.Add(ExactWrite(
            QteDeferredEffectContinuation.RequestPath,
            requestBefore,
            wave.Request));

        if (!await CommitWithOptionalHookAsync(
                fileSystem,
                writeLease,
                "initial",
                afterLegacyMutationAsync,
                writes.ToArray()))
        {
            throw new InvalidOperationException(
                "qte_deferred_pending_publication_conflict: sealed mechanics or transport authority changed before awaiting-receipt publication.");
        }

        activeScene.EffectResolutionState = "awaiting_receipt";
        activeScene.DeferredEffectContinuationFingerprint =
            continuationFingerprint;
    }

    internal static async Task PublishNextWaveAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.ActiveQteSceneState activeScene,
        QteDeferredEffectReceiptResume resume,
        Func<string, Task>? afterLegacyMutationAsync = null)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(activeScene);
        ArgumentNullException.ThrowIfNull(resume);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        var preparation = resume.Preparation;
        var plan = preparation.Plan;
        if (!plan.AwaitsPendingResolution || plan.PendingGmPacket == null ||
            !plan.PendingAfterImages.TryGetValue(
                ResourcePendingResolutionState.PendingPath,
                out var pendingRoot) ||
            pendingRoot == null)
        {
            throw new InvalidOperationException(
                "qte_deferred_next_wave_unexpected: resolved receipt produced no bounded pending frontier.");
        }
        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            plan.DefinitionAfterImage.ToJsonString(),
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_definitions_invalid: common plan definitions are invalid.");
        }
        var pendingResult = ResourcePendingResolutionState.ParseCanonical(
            pendingRoot.ToJsonString(),
            definitions.Catalog,
            allowMissingPristine: false);
        if (!pendingResult.IsValid || pendingResult.State == null ||
            pendingResult.State.Requests.Count == 0)
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_pending_invalid: common plan produced no valid next-wave authority.");
        }
        var pending = pendingResult.State;
        var safePacket = plan.PendingGmPacket;
        if (!JsonNode.DeepEquals(safePacket, pending.BuildSafeGmPacket()))
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_packet_mismatch: common plan packet differs from next pending authority.");
        }

        var continuationBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.StatePath,
            "qte_deferred_continuation_missing");
        var continuation = ParseObject(
            continuationBytes,
            QteDeferredEffectContinuation.StatePath);
        if (!JsonNode.DeepEquals(continuation, preparation.ContinuationRoot) ||
            ReadExact(continuation, "state") != "awaiting_receipt" ||
            continuation["selectedTerminalBinding"] is not JsonObject selected ||
            !JsonNode.DeepEquals(selected, preparation.SelectedTerminalBinding) ||
            continuation["currentWave"] is not JsonObject currentWave ||
            continuation["resolvedWaveBindings"] is not JsonArray resolvedWaves ||
            ReadExact(currentWave, "requestId") !=
            ReadExact(resume.ResolvedWaveBinding, "requestId") ||
            ReadExact(currentWave, "waveId") !=
            ReadExact(resume.ResolvedWaveBinding, "waveId") ||
            ReadInt(currentWave, "ordinal") !=
            ReadInt(resume.ResolvedWaveBinding, "ordinal"))
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_continuation_mismatch: current wave changed before rotation.");
        }
        var runtimeBytes = await RequiredBytesAsync(
            fileSystem,
            writeLease,
            QteSceneService.QteRuntimePath,
            "qte_deferred_runtime_missing");
        var runtime = ParseObject(runtimeBytes, QteSceneService.QteRuntimePath);
        if (runtime["activeScene"] is not JsonObject activeRoot ||
            ReadExact(activeRoot, "effectResolutionState") != "awaiting_receipt" ||
            ReadExact(activeRoot, "deferredEffectContinuationFingerprint") !=
            ReadFingerprint(continuation, "authorityFingerprint"))
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_runtime_mismatch: runtime changed before wave rotation.");
        }
        var requestBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.RequestPath);
        var receiptBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReceiptPath);
        var readyBefore = await ReadBeforeImageAsync(
            fileSystem,
            writeLease,
            QteDeferredEffectContinuation.ReadyPath);
        if (!requestBefore.Existed || !receiptBefore.Existed ||
            !readyBefore.Existed)
        {
            throw new InvalidDataException(
                "qte_deferred_next_wave_transport_missing: current request, receipt, and ready authority must all exist.");
        }

        var nextWave = CreateWaveAuthority(
            continuation,
            selected,
            pending,
            safePacket);
        var nextContinuation = continuation.DeepClone().AsObject();
        resolvedWaves = nextContinuation["resolvedWaveBindings"]!.AsArray();
        resolvedWaves.Add(resume.ResolvedWaveBinding.DeepClone());
        nextContinuation["currentWave"] = nextWave.CurrentWave;
        var continuationFingerprint =
            QteDeferredEffectContinuation.RefreshAuthorityFingerprint(
                nextContinuation);
        activeRoot["deferredEffectContinuationFingerprint"] =
            continuationFingerprint;

        var writes = QteDeferredAcceptedMechanicsPlanner
            .CreatePublicationWrites(plan)
            .ToList();
        writes.Add(ExactDelete(
            QteDeferredEffectContinuation.ReceiptPath,
            receiptBefore));
        writes.Add(ExactDelete(
            QteDeferredEffectContinuation.ReadyPath,
            readyBefore));
        writes.Add(ExactWrite(
            QteDeferredEffectContinuation.StatePath,
            new CanonicalBeforeImage(true, continuationBytes),
            nextContinuation));
        writes.Add(ExactWrite(
            QteSceneService.QteRuntimePath,
            new CanonicalBeforeImage(true, runtimeBytes),
            runtime));
        writes.Add(ExactWrite(
            QteDeferredEffectContinuation.RequestPath,
            requestBefore,
            nextWave.Request));
        if (!await CommitWithOptionalHookAsync(
                fileSystem,
                writeLease,
                "next_wave",
                afterLegacyMutationAsync,
                writes.ToArray()))
        {
            throw new InvalidOperationException(
                "qte_deferred_next_wave_publication_conflict: current wave changed before atomic rotation.");
        }

        activeScene.EffectResolutionState = "awaiting_receipt";
        activeScene.DeferredEffectContinuationFingerprint =
            continuationFingerprint;
    }

    private static Task<bool> CommitWithOptionalHookAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string phase,
        Func<string, Task>? afterLegacyMutationAsync,
        CoordinatedStateWriteHelper.PlannedWrite[] writes)
    {
        return afterLegacyMutationAsync == null
            ? CoordinatedStateWriteHelper.TryCommitAsync(
                fileSystem,
                writeLease,
                writes)
            : CoordinatedStateWriteHelper.TryCommitWithHookAsync(
                fileSystem,
                writeLease,
                write => afterLegacyMutationAsync($"{phase}:{write.Path}"),
                writes);
    }

    private static WaveAuthority CreateWaveAuthority(
        JsonObject continuation,
        JsonObject selectedTerminalBinding,
        ResourcePendingResolutionState pending,
        JsonObject safePacket)
    {
        var fullTurnFingerprint = SingleExact(
            pending.Requests.Select(static request =>
                request.FullTurnFingerprint),
            "full-turn fingerprint");
        var semanticTurnFingerprint = SingleExact(
            pending.Requests.Select(static request =>
                request.SemanticTurnFingerprint),
            "semantic-turn fingerprint");
        var pendingSessionId = SingleExact(
            pending.Requests.Select(static request => request.SessionId),
            "pending session");
        var pendingAcceptedRequestId = SingleExact(
            pending.Requests.Select(static request => request.AcceptedRequestId),
            "pending accepted request");
        var pendingTurn = Single(
            pending.Requests.Select(static request => request.RequestTurn),
            "pending request turn");
        var waveOrdinal = Single(
            pending.Requests.Select(static request =>
                request.CausalAuthority.WaveOrdinal),
            "pending wave ordinal");
        var continuationId = ReadExact(continuation, "continuationId");
        var sessionId = ReadExact(continuation, "sessionId");
        var sessionGeneration = ReadExact(continuation, "sessionGeneration");
        var acceptedSourceTurn = ReadInt(continuation, "acceptedSourceTurn");
        var qteId = ReadExact(continuation, "qteId");
        var selectedFingerprint = ReadFingerprint(
            selectedTerminalBinding,
            "fingerprint");
        if (sessionId != pendingSessionId ||
            continuationId + "_terminal" != pendingAcceptedRequestId ||
            acceptedSourceTurn != pendingTurn ||
            !JsonNode.DeepEquals(safePacket, pending.BuildSafeGmPacket()))
        {
            throw new InvalidDataException(
                "qte_deferred_pending_correlation_mismatch: pending authority differs from the selected continuation.");
        }

        var safePacketFingerprint = HashNode(
            "qte-deferred-safe-packet-v1",
            safePacket);
        var correlationSeed = string.Join(
            '\n',
            continuationId,
            selectedFingerprint,
            pending.Fingerprint,
            fullTurnFingerprint,
            semanticTurnFingerprint,
            waveOrdinal.ToString(
                System.Globalization.CultureInfo.InvariantCulture));
        var requestId = CreateCorrelationId(
            "qte_effect_request_",
            "qte-deferred-effect-request-id-v1",
            correlationSeed);
        var waveId = CreateCorrelationId(
            "qte_effect_wave_",
            "qte-deferred-effect-wave-id-v1",
            correlationSeed);
        return new WaveAuthority(
            new JsonObject
            {
                ["schemaVersion"] = 1,
                ["requestKind"] = RequestKind,
                ["sessionId"] = sessionId,
                ["sessionGeneration"] = sessionGeneration,
                ["continuationId"] = continuationId,
                ["requestId"] = requestId,
                ["waveId"] = waveId,
                ["waveOrdinal"] = waveOrdinal,
                ["acceptedSourceTurn"] = acceptedSourceTurn,
                ["qteId"] = qteId,
                ["selectedTerminalFingerprint"] = selectedFingerprint,
                ["pendingStateFingerprint"] = pending.Fingerprint,
                ["fullTurnFingerprint"] = fullTurnFingerprint,
                ["semanticTurnFingerprint"] = semanticTurnFingerprint,
                ["safePacket"] = safePacket.DeepClone()
            },
            new JsonObject
            {
                ["requestId"] = requestId,
                ["waveId"] = waveId,
                ["ordinal"] = waveOrdinal,
                ["safePacketFingerprint"] = safePacketFingerprint
            });
    }

    private static void ValidateRequestRoot(JsonObject root)
    {
        ValidateClosedRoot(root, RequestFields, "request");
        ValidateEnvelope(root, "request");
        _ = ReadFingerprint(root, "selectedTerminalFingerprint");
        _ = ReadFingerprint(root, "pendingStateFingerprint");
        _ = ReadFingerprint(root, "fullTurnFingerprint");
        _ = ReadFingerprint(root, "semanticTurnFingerprint");
        if (root["safePacket"] is not JsonObject)
        {
            throw new InvalidDataException(
                "qte_deferred_request_invalid: safePacket must be an object.");
        }
    }

    private static void ValidateReceiptRoot(JsonObject root)
    {
        ValidateClosedRoot(root, ReceiptFields, "receipt");
        ValidateEnvelope(root, "receipt");
        _ = ReadFingerprint(root, "selectedTerminalFingerprint");
        _ = ReadFingerprint(root, "pendingStateFingerprint");
        _ = ReadFingerprint(root, "fullTurnFingerprint");
        _ = ReadFingerprint(root, "semanticTurnFingerprint");
        if (root["effectResolutionReceipts"] is not JsonArray)
        {
            throw new InvalidDataException(
                "qte_deferred_receipts_invalid: effectResolutionReceipts must be an array.");
        }
    }

    private static void ValidateReadyRoot(JsonObject root)
    {
        ValidateClosedRoot(root, ReadyFields, "ready");
        ValidateEnvelope(root, "ready");
        _ = ReadFingerprint(root, "pendingStateFingerprint");
        _ = ReadFingerprint(root, "receiptsFingerprint");
    }

    private static void ValidateEnvelope(JsonObject root, string label)
    {
        if (ReadInt(root, "schemaVersion") != 1 ||
            ReadExact(root, "requestKind") != RequestKind)
        {
            throw new InvalidDataException(
                $"qte_deferred_{label}_invalid: only the closed current QTE receipt envelope is supported.");
        }
        _ = ReadExact(root, "sessionId");
        _ = ReadExact(root, "sessionGeneration");
        _ = ReadExact(root, "continuationId");
        _ = ReadExact(root, "requestId");
        _ = ReadExact(root, "waveId");
        _ = ReadExact(root, "qteId");
        _ = ReadInt(root, "waveOrdinal");
        _ = ReadInt(root, "acceptedSourceTurn");
    }

    private static void ValidateClosedRoot(
        JsonObject root,
        IReadOnlyList<string> expectedFields,
        string label)
    {
        var expected = expectedFields.ToHashSet(StringComparer.Ordinal);
        if (root.Count != expected.Count ||
            root.Select(static pair => pair.Key)
                .Any(key => !expected.Contains(key)))
        {
            throw new InvalidDataException(
                $"qte_deferred_{label}_invalid: envelope fields differ from the closed current schema.");
        }
    }

    private static void ValidateCommonCorrelation(
        JsonObject request,
        JsonObject other,
        IEnumerable<string> fields)
    {
        foreach (var field in fields)
        {
            if (!JsonNode.DeepEquals(request[field], other[field]))
            {
                throw new InvalidDataException(
                    $"qte_deferred_transport_correlation_mismatch: '{field}' differs from the published request.");
            }
        }
    }

    private static void ValidateRequestAgainstContinuation(
        JsonObject request,
        JsonObject continuation,
        QteTerminalResourceSelection selection)
    {
        if (continuation["selectedTerminalBinding"] is not JsonObject selected ||
            continuation["currentWave"] is not JsonObject currentWave ||
            ReadExact(request, "sessionId") != ReadExact(continuation, "sessionId") ||
            ReadExact(request, "sessionGeneration") !=
            ReadExact(continuation, "sessionGeneration") ||
            ReadExact(request, "continuationId") !=
            ReadExact(continuation, "continuationId") ||
            ReadExact(request, "qteId") != ReadExact(continuation, "qteId") ||
            ReadInt(request, "acceptedSourceTurn") != selection.SourceTurn ||
            ReadFingerprint(request, "selectedTerminalFingerprint") !=
            ReadFingerprint(selected, "fingerprint") ||
            ReadExact(request, "requestId") != ReadExact(currentWave, "requestId") ||
            ReadExact(request, "waveId") != ReadExact(currentWave, "waveId") ||
            ReadInt(request, "waveOrdinal") != ReadInt(currentWave, "ordinal") ||
            request["safePacket"] is not JsonObject safePacket ||
            HashNode("qte-deferred-safe-packet-v1", safePacket) !=
            ReadFingerprint(currentWave, "safePacketFingerprint"))
        {
            throw new InvalidDataException(
                "qte_deferred_request_correlation_mismatch: request differs from the current continuation wave.");
        }
    }

    private static void ValidateRequestAgainstPending(
        JsonObject request,
        JsonObject continuation,
        ResourcePendingResolutionState pending)
    {
        var fullTurnFingerprint = SingleExact(
            pending.Requests.Select(static value => value.FullTurnFingerprint),
            "full-turn fingerprint");
        var semanticTurnFingerprint = SingleExact(
            pending.Requests.Select(static value => value.SemanticTurnFingerprint),
            "semantic-turn fingerprint");
        var waveOrdinal = Single(
            pending.Requests.Select(static value =>
                value.CausalAuthority.WaveOrdinal),
            "pending wave ordinal");
        var sessionId = SingleExact(
            pending.Requests.Select(static value => value.SessionId),
            "pending session");
        var acceptedRequestId = SingleExact(
            pending.Requests.Select(static value => value.AcceptedRequestId),
            "pending accepted request");
        var requestTurn = Single(
            pending.Requests.Select(static value => value.RequestTurn),
            "pending request turn");
        if (ReadFingerprint(request, "pendingStateFingerprint") != pending.Fingerprint ||
            ReadFingerprint(request, "fullTurnFingerprint") != fullTurnFingerprint ||
            ReadFingerprint(request, "semanticTurnFingerprint") != semanticTurnFingerprint ||
            ReadInt(request, "waveOrdinal") != waveOrdinal ||
            ReadExact(request, "sessionId") != sessionId ||
            acceptedRequestId != ReadExact(continuation, "continuationId") + "_terminal" ||
            requestTurn != ReadInt(continuation, "acceptedSourceTurn") ||
            request["safePacket"] is not JsonObject safePacket ||
            !JsonNode.DeepEquals(safePacket, pending.BuildSafeGmPacket()))
        {
            throw new InvalidDataException(
                "qte_deferred_pending_correlation_mismatch: request differs from current pending authority.");
        }
    }

    private static CoordinatedStateWriteHelper.PlannedWrite ExactWrite(
        string path,
        CanonicalBeforeImage beforeImage,
        JsonObject afterImage) =>
        new(
            path,
            PreviousJson: null,
            NextJson: afterImage.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            RequireCurrentBaseline: true,
            GuardOnly: false,
            ExactPrevious: beforeImage);

    private static CoordinatedStateWriteHelper.PlannedWrite ExactDelete(
        string path,
        CanonicalBeforeImage beforeImage) =>
        new(
            path,
            PreviousJson: null,
            NextJson: null,
            RequireCurrentBaseline: true,
            GuardOnly: false,
            ExactPrevious: beforeImage);

    private static async Task<CanonicalBeforeImage> ReadBeforeImageAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path)
    {
        var bytes = await fileSystem.ReadFileBytesAsync(writeLease, path);
        return new CanonicalBeforeImage(bytes != null, bytes);
    }

    private static async Task<byte[]> RequiredBytesAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        string path,
        string issueCode) =>
        await fileSystem.ReadFileBytesAsync(writeLease, path) ??
        throw new InvalidDataException($"{issueCode}: required path '{path}' is missing.");

    private static JsonObject ParseObject(byte[] bytes, string path)
    {
        try
        {
            var json = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(bytes)
                .TrimStart('\uFEFF');
            return JsonNode.Parse(json) as JsonObject ??
                   throw new InvalidDataException(
                       $"QTE deferred transport path '{path}' must contain one JSON object.");
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException(
                $"QTE deferred transport path '{path}' contains malformed JSON.",
                ex);
        }
    }

    private static string SingleExact(
        IEnumerable<string> values,
        string label)
    {
        var distinct = values.Distinct(StringComparer.Ordinal).ToArray();
        if (distinct.Length != 1 ||
            !ResourceMaterializationContract.IsExactIdentifier(distinct[0]) &&
            !ResourceMaterializationContract.IsAuthorityFingerprint(distinct[0]))
        {
            throw new InvalidDataException(
                $"QTE deferred transport requires one exact {label}.");
        }
        return distinct[0];
    }

    private static int Single(IEnumerable<int> values, string label)
    {
        var distinct = values.Distinct().ToArray();
        if (distinct.Length != 1 || distinct[0] < 0)
        {
            throw new InvalidDataException(
                $"QTE deferred transport requires one non-negative {label}.");
        }
        return distinct[0];
    }

    private static string ReadExact(JsonObject root, string field)
    {
        var value = root[field]?.GetValue<string>();
        return ResourceMaterializationContract.IsExactIdentifier(value)
            ? value!
            : throw new InvalidDataException(
                $"QTE deferred transport field '{field}' is not an exact identifier.");
    }

    private static string ReadFingerprint(JsonObject root, string field)
    {
        var value = root[field]?.GetValue<string>();
        return ResourceMaterializationContract.IsAuthorityFingerprint(value)
            ? value!
            : throw new InvalidDataException(
                $"QTE deferred transport field '{field}' is not an authority fingerprint.");
    }

    private static int ReadInt(JsonObject root, string field)
    {
        if (root[field] is JsonValue value &&
            value.TryGetValue<int>(out var result) &&
            result >= 0)
        {
            return result;
        }
        throw new InvalidDataException(
            $"QTE deferred transport field '{field}' is not a non-negative integer.");
    }

    private static string CreateCorrelationId(
        string prefix,
        string domain,
        string value)
    {
        var payload = Encoding.UTF8.GetBytes(domain + "\0" + value);
        return prefix + Convert.ToHexString(SHA256.HashData(payload))
            .ToLowerInvariant()[..32];
    }

    private static string HashNode(string domain, JsonNode node)
    {
        var payload = Encoding.UTF8.GetBytes(
            domain + "\0" + node.ToJsonString());
        return "sha256:" + Convert.ToHexString(SHA256.HashData(payload))
            .ToLowerInvariant();
    }

    private static string HashBytes(byte[] bytes) =>
        "sha256:" + Convert.ToHexString(SHA256.HashData(bytes))
            .ToLowerInvariant();
}
