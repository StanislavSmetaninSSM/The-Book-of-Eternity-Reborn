using System.Collections.ObjectModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class QteDeferredEffectContinuationCapture
{
    private readonly Dictionary<string, CanonicalBeforeImage> _sealedRoots;

    internal QteDeferredEffectContinuationCapture(
        string continuationId,
        string authorityFingerprint,
        JsonObject continuationRoot,
        IReadOnlyDictionary<string, CanonicalBeforeImage> sealedRoots,
        CanonicalBeforeImage runtimeBeforeImage,
        CanonicalBeforeImage offerBeforeImage,
        CanonicalBeforeImage continuationBeforeImage)
    {
        ContinuationId = continuationId;
        AuthorityFingerprint = authorityFingerprint;
        ContinuationRoot = continuationRoot.DeepClone().AsObject();
        _sealedRoots = sealedRoots.ToDictionary(
            static pair => pair.Key,
            static pair => new CanonicalBeforeImage(
                pair.Value.Existed,
                pair.Value.Bytes),
            StringComparer.Ordinal);
        RuntimeBeforeImage = new CanonicalBeforeImage(
            runtimeBeforeImage.Existed,
            runtimeBeforeImage.Bytes);
        OfferBeforeImage = new CanonicalBeforeImage(
            offerBeforeImage.Existed,
            offerBeforeImage.Bytes);
        ContinuationBeforeImage = new CanonicalBeforeImage(
            continuationBeforeImage.Existed,
            continuationBeforeImage.Bytes);
    }

    internal string ContinuationId { get; }
    internal string AuthorityFingerprint { get; }
    internal JsonObject ContinuationRoot { get; }
    internal string ContinuationJson => ContinuationRoot.ToJsonString(
        SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed);
    internal IReadOnlyDictionary<string, CanonicalBeforeImage> SealedRoots =>
        new ReadOnlyDictionary<string, CanonicalBeforeImage>(
            _sealedRoots.ToDictionary(
                static pair => pair.Key,
                static pair => new CanonicalBeforeImage(
                    pair.Value.Existed,
                    pair.Value.Bytes),
                StringComparer.Ordinal));
    internal CanonicalBeforeImage RuntimeBeforeImage { get; }
    internal CanonicalBeforeImage OfferBeforeImage { get; }
    internal CanonicalBeforeImage ContinuationBeforeImage { get; }
}

internal static class QteDeferredEffectContinuation
{
    internal const string StatePath =
        "game_state/control/qte_deferred_effect_continuation.json";
    internal const string RequestPath =
        "input/qte_effect_resolution_request.json";
    internal const string ReceiptPath =
        "output/qte_effect_resolution_receipts.json";
    internal const string ReadyPath =
        "ready/qte_effect_resolution_complete.json";

    private static readonly string[] CarrierPaths =
    [
        EffectCarrierCatalog.PlayerPath,
        EffectCarrierCatalog.NpcPath,
        EffectCarrierCatalog.EnemiesPath,
        EffectCarrierCatalog.AlliesPath,
        EffectCarrierCatalog.AfterlifeProfilesPath,
        EffectCarrierCatalog.SpiritualConflictPath
    ];

    internal static readonly IReadOnlyList<string> SealedRootPaths =
        new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
            EffectAcceptedTurnPlan.IdentityIndexPath,
            EffectAcceptedTurnPlan.CommandPath,
            ResourcePendingResolutionState.PendingPath,
            EffectAcceptedTurnInputComposer.WorldTimePath,
            "game_state/history/chat_log.json",
            "input/turn_request.json"
        }
        .Concat(CarrierPaths)
        .Concat(EffectAcceptedTurnInputComposer.SourceAuthorityPaths)
        .Concat(CanonicalResourceOwnerAuthorityComposer.SourceAuthorityPaths)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static path => path, StringComparer.Ordinal)
        .ToArray();

    internal static async Task<QteDeferredEffectContinuationCapture> CaptureAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.QteOffer offer,
        int acceptedSourceTurn)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(offer);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);
        if (acceptedSourceTurn <= 0 ||
            offer.SourceTurnNumber != acceptedSourceTurn)
        {
            throw new InvalidDataException(
                "QTE deferred continuation requires one exact positive source-turn binding.");
        }

        var sessionGeneration = SessionOperationContext.TryGetExpectedGeneration(
            fileSystem.BasePath,
            out var boundGeneration)
            ? boundGeneration
            : fileSystem.GetOrCreateSessionGeneration(writeLease);
        if (!fileSystem.IsCurrentSessionGeneration(
                writeLease,
                sessionGeneration))
        {
            throw new SessionReplacedException(
                "QTE deferred continuation cannot bind a replaced game session.",
                sessionGeneration,
                actualGeneration: null);
        }

        var sealedRoots = new Dictionary<string, CanonicalBeforeImage>(
            StringComparer.Ordinal);
        foreach (var path in SealedRootPaths)
        {
            var bytes = await fileSystem.ReadFileBytesAsync(writeLease, path);
            sealedRoots.Add(
                path,
                new CanonicalBeforeImage(bytes != null, bytes));
        }

        var runtimeBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            QteSceneService.QteRuntimePath);
        var offerBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            QteSceneService.QteOfferPath);
        var priorContinuationBytes = await fileSystem.ReadFileBytesAsync(
            writeLease,
            StatePath);
        if (priorContinuationBytes != null)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_already_exists: a new QTE cannot replace active continuation authority.");
        }

        var runtimeBefore = new CanonicalBeforeImage(
            runtimeBytes != null,
            runtimeBytes);
        var offerBefore = new CanonicalBeforeImage(
            offerBytes != null,
            offerBytes);
        var continuationBefore = new CanonicalBeforeImage(
            existed: false,
            bytes: null);
        var sessionId = ResolveSessionId(
            sealedRoots,
            sessionGeneration);
        var continuationId =
            "qte_continuation_" + Guid.NewGuid().ToString("N");
        var offerRoot = JsonSerializer.SerializeToNode(
            offer,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        var offerFingerprint = HashNode(
            "qte-deferred-offer-v1",
            offerRoot);

        var carrierInput = new EffectCarrierCatalogInput(
            ParseObject(sealedRoots[EffectCarrierCatalog.PlayerPath]),
            ParseObject(sealedRoots[EffectCarrierCatalog.NpcPath]),
            ParseObject(sealedRoots[EffectCarrierCatalog.EnemiesPath]),
            ParseObject(sealedRoots[EffectCarrierCatalog.AlliesPath]),
            ParseObject(sealedRoots[EffectCarrierCatalog.AfterlifeProfilesPath]),
            ParseObject(sealedRoots[EffectCarrierCatalog.SpiritualConflictPath]));
        var carrierCatalog = EffectCarrierCatalog.Build(carrierInput);
        if (carrierCatalog.Issues.Count != 0)
        {
            throw new InvalidDataException(
                "QTE deferred continuation cannot seal invalid effect carriers: " +
                string.Join(
                    "; ",
                    carrierCatalog.Issues.Take(8).Select(static issue =>
                        $"{issue.Code}@{issue.FilePath}")));
        }

        var sourcePaths = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var targetPaths = CarrierPaths
            .Concat(new[]
            {
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var sourceBinding = CreateRootAuthorityBinding(
            "qte-deferred-effect-sources-v1",
            sourcePaths,
            sealedRoots);
        var targetBinding = CreateRootAuthorityBinding(
            "qte-deferred-effect-targets-v1",
            targetPaths,
            sealedRoots);
        var triggerBinding = CreateTriggerCandidateBinding(carrierCatalog);
        var pendingBinding = CreatePendingBinding(
            sealedRoots[ResourcePendingResolutionState.PendingPath]);
        var semanticTurnFingerprint = HashText(
            "qte-deferred-semantic-turn-v1",
            string.Join(
                "\n",
                sealedRoots
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair =>
                        pair.Key + "=" + pair.Value.Fingerprint)) +
            "\nsourceTurn=" + acceptedSourceTurn +
            "\noffer=" + offerFingerprint);
        var sourceFingerprint = HashText(
            "qte-deferred-accepted-mechanics-source-v1",
            sourceBinding["fingerprint"]!.GetValue<string>() + "\n" +
            targetBinding["fingerprint"]!.GetValue<string>() + "\n" +
            triggerBinding["fingerprint"]!.GetValue<string>() + "\n" +
            pendingBinding["fingerprint"]!.GetValue<string>() + "\n" +
            semanticTurnFingerprint);

        var root = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["continuationId"] = continuationId,
            ["sessionId"] = sessionId,
            ["sessionGeneration"] = sessionGeneration,
            ["acceptedSourceTurn"] = acceptedSourceTurn,
            ["qteId"] = offer.QteId,
            ["offerFingerprint"] = offerFingerprint,
            ["runtimeBeforeFingerprint"] = runtimeBefore.Fingerprint,
            ["sealedRootBindings"] = new JsonArray(
                sealedRoots
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair => (JsonNode)CreateSealedRootBinding(
                        pair.Key,
                        pair.Value))
                    .ToArray()),
            ["sourceAuthorityBinding"] = sourceBinding,
            ["targetAuthorityBinding"] = targetBinding,
            ["triggerCandidateBinding"] = triggerBinding,
            ["pendingCausalBinding"] = pendingBinding,
            ["semanticTurnFingerprint"] = semanticTurnFingerprint,
            ["acceptedMechanicsAuthority"] = new JsonObject
            {
                ["kind"] = "qte_continuation",
                ["continuationId"] = continuationId,
                ["sourceFingerprint"] = sourceFingerprint
            },
            ["identityLedger"] = new JsonArray(),
            ["state"] = "armed",
            ["resolvedWaveBindings"] = new JsonArray()
        };
        root["identityLedger"] = await QteDeferredAcceptedMechanicsPlanner
            .PreallocateIdentityLedgerAtAcceptanceAsync(root, offer);
        var authorityFingerprint = HashNode(
            "qte-deferred-effect-continuation-v1",
            root);
        root["authorityFingerprint"] = authorityFingerprint;

        return new QteDeferredEffectContinuationCapture(
            continuationId,
            authorityFingerprint,
            root,
            sealedRoots,
            runtimeBefore,
            offerBefore,
            continuationBefore);
    }

    internal static Task<JsonObject> ValidateForTerminalSelectionAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.ActiveQteSceneState activeScene,
        QteSceneService.QteOffer offer,
        int acceptedSourceTurn) =>
        ValidateAsync(
            fileSystem,
            writeLease,
            activeScene,
            offer,
            acceptedSourceTurn,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "armed",
                "terminal_selected"
            },
            new HashSet<string>(StringComparer.Ordinal));

    internal static async Task<JsonObject> ValidateForReceiptResumeAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.ActiveQteSceneState activeScene,
        QteSceneService.QteOffer offer,
        int acceptedSourceTurn)
    {
        var root = await ValidateAsync(
            fileSystem,
            writeLease,
            activeScene,
            offer,
            acceptedSourceTurn,
            new HashSet<string>(StringComparer.Ordinal)
            {
                "awaiting_receipt"
            },
            new HashSet<string>(StringComparer.Ordinal)
            {
                ResourcePendingResolutionState.PendingPath
            });
        if (root["selectedTerminalBinding"] is not JsonObject selected ||
            root["currentWave"] is not JsonObject currentWave ||
            root["resolvedWaveBindings"] is not JsonArray resolvedWaves)
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_authority_invalid: awaiting continuation lacks its selected terminal or current wave.");
        }
        _ = ReadRequiredFingerprint(selected, "fingerprint");
        var allowedWaveFields = new HashSet<string>(StringComparer.Ordinal)
        {
            "requestId", "waveId", "ordinal", "safePacketFingerprint"
        };
        if (currentWave.Count != allowedWaveFields.Count ||
            currentWave.Select(static pair => pair.Key)
                .Any(key => !allowedWaveFields.Contains(key)))
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_authority_invalid: currentWave must use the closed current schema.");
        }
        _ = ReadRequiredExact(currentWave, "requestId");
        _ = ReadRequiredExact(currentWave, "waveId");
        _ = ReadRequiredFingerprint(currentWave, "safePacketFingerprint");
        if (ReadRequiredInt(currentWave, "ordinal") < 0)
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_authority_invalid: current wave ordinal cannot be negative.");
        }
        ValidateResolvedWaveBindings(resolvedWaves, currentWave);
        return root;
    }

    private static async Task<JsonObject> ValidateAsync(
        FileSystemManager fileSystem,
        FileSystemManager.CanonicalWriteLease writeLease,
        QteSceneService.ActiveQteSceneState activeScene,
        QteSceneService.QteOffer offer,
        int acceptedSourceTurn,
        IReadOnlySet<string> allowedStates,
        IReadOnlySet<string> evolvedSealedPaths)
    {
        ArgumentNullException.ThrowIfNull(fileSystem);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(activeScene);
        ArgumentNullException.ThrowIfNull(offer);
        fileSystem.EnsureCanonicalWriteLeaseActive(writeLease);

        var bytes = await fileSystem.ReadFileBytesAsync(writeLease, StatePath);
        if (bytes == null)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_missing: terminal selection requires its acceptance-time authority.");
        }
        var root = ParseObject(new CanonicalBeforeImage(true, bytes)) ??
                   throw new InvalidDataException(
                       "qte_deferred_continuation_invalid: continuation root must be an object.");
        ValidateClosedRoot(root);
        if (ReadRequiredInt(root, "schemaVersion") != 1)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_schema_invalid: only current schemaVersion 1 is supported.");
        }
        var continuationId = ReadRequiredExact(root, "continuationId");
        var authorityFingerprint = ReadRequiredFingerprint(
            root,
            "authorityFingerprint");
        var fingerprintRoot = root.DeepClone().AsObject();
        fingerprintRoot.Remove("authorityFingerprint");
        var expectedAuthorityFingerprint = HashNode(
            "qte-deferred-effect-continuation-v1",
            fingerprintRoot);
        if (!string.Equals(
                authorityFingerprint,
                expectedAuthorityFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_fingerprint_mismatch: continuation authority was modified after acceptance.");
        }
        if (!string.Equals(
                continuationId,
                activeScene.DeferredEffectContinuationId,
                StringComparison.Ordinal) ||
            !string.Equals(
                authorityFingerprint,
                activeScene.DeferredEffectContinuationFingerprint,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_runtime_mismatch: active QTE runtime points to different continuation authority.");
        }

        var sessionGeneration = ReadRequiredExact(root, "sessionGeneration");
        if (!fileSystem.IsCurrentSessionGeneration(
                writeLease,
                sessionGeneration))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_session_mismatch: continuation belongs to another game-session generation.");
        }
        if (acceptedSourceTurn <= 0 ||
            ReadRequiredInt(root, "acceptedSourceTurn") != acceptedSourceTurn ||
            offer.SourceTurnNumber != acceptedSourceTurn ||
            !string.Equals(
                ReadRequiredExact(root, "qteId"),
                offer.QteId,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_offer_mismatch: QTE identity or accepted source turn changed.");
        }
        var offerRoot = JsonSerializer.SerializeToNode(
            offer,
            SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)!.AsObject();
        var offerFingerprint = ReadRequiredFingerprint(root, "offerFingerprint");
        if (!string.Equals(
                offerFingerprint,
                HashNode("qte-deferred-offer-v1", offerRoot),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_offer_mismatch: active QTE offer differs from its accepted fingerprint.");
        }
        var continuationState = ReadRequiredExact(root, "state");
        if (!allowedStates.Contains(continuationState) ||
            !string.Equals(
                activeScene.EffectResolutionState,
                continuationState,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_state_invalid: continuation and active runtime are not at the required exact state.");
        }
        ValidateStateShape(root, continuationState);

        if (root["sealedRootBindings"] is not JsonArray bindings)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_roots_invalid: sealedRootBindings must be an array.");
        }
        var expectedPaths = SealedRootPaths.ToHashSet(StringComparer.Ordinal);
        var actualPaths = new HashSet<string>(StringComparer.Ordinal);
        var sealedRoots = new Dictionary<string, CanonicalBeforeImage>(
            StringComparer.Ordinal);
        foreach (var bindingNode in bindings)
        {
            if (bindingNode is not JsonObject binding ||
                binding.Select(static pair => pair.Key).Any(static key => key is not (
                    "path" or "existed" or "payloadBase64" or "sha256")))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_roots_invalid: every sealed root binding must use the closed current schema.");
            }
            var path = ReadRequiredExact(binding, "path");
            AcceptedMechanicsPlanBinding.ValidatePath(path, "sealedRootBindings");
            if (!actualPaths.Add(path) || !expectedPaths.Contains(path))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_roots_invalid: sealed root paths are duplicated or unknown.");
            }
            if (binding["existed"] is not JsonValue existedNode ||
                !existedNode.TryGetValue<bool>(out var existed))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_roots_invalid: existed must be boolean.");
            }
            byte[]? sealedBytes;
            if (existed)
            {
                var payload = binding["payloadBase64"]?.GetValue<string>();
                try
                {
                    sealedBytes = string.IsNullOrWhiteSpace(payload)
                        ? throw new FormatException()
                        : Convert.FromBase64String(payload);
                }
                catch (FormatException ex)
                {
                    throw new InvalidDataException(
                        "qte_deferred_continuation_roots_invalid: present root payloadBase64 is invalid.",
                        ex);
                }
            }
            else
            {
                if (binding.ContainsKey("payloadBase64"))
                {
                    throw new InvalidDataException(
                        "qte_deferred_continuation_roots_invalid: missing root cannot carry payloadBase64.");
                }
                sealedBytes = null;
            }
            var beforeImage = new CanonicalBeforeImage(existed, sealedBytes);
            sealedRoots.Add(path, beforeImage);
            if (!string.Equals(
                    ReadRequiredFingerprint(binding, "sha256"),
                    beforeImage.Fingerprint,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_roots_invalid: sealed root hash does not match exact payload/existence evidence.");
            }
            if (!evolvedSealedPaths.Contains(path))
            {
                var currentBytes = await fileSystem.ReadFileBytesAsync(
                    writeLease,
                    path);
                if (existed != (currentBytes != null) ||
                    (currentBytes != null &&
                     !currentBytes.AsSpan().SequenceEqual(sealedBytes!)))
                {
                    throw new InvalidDataException(
                        "qte_deferred_continuation_sealed_root_mismatch: " +
                        $"'{path}' changed after QTE acceptance.");
                }
            }
        }
        if (!actualPaths.SetEquals(expectedPaths))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_roots_invalid: continuation does not cover the complete current sealed-root set.");
        }

        ValidateAuthorityBindings(
            root,
            sealedRoots,
            sessionGeneration,
            acceptedSourceTurn,
            offerFingerprint);
        ValidateIdentityLedger(root["identityLedger"] as JsonArray);
        return root.DeepClone().AsObject();
    }

    internal static string RefreshAuthorityFingerprint(JsonObject root)
    {
        ArgumentNullException.ThrowIfNull(root);
        var refreshed = root.DeepClone().AsObject();
        refreshed.Remove("authorityFingerprint");
        var fingerprint = HashNode(
            "qte-deferred-effect-continuation-v1",
            refreshed);
        root["authorityFingerprint"] = fingerprint;
        return fingerprint;
    }

    private static void ValidateClosedRoot(JsonObject root)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "continuationId", "sessionId",
            "sessionGeneration", "acceptedSourceTurn", "qteId",
            "offerFingerprint", "runtimeBeforeFingerprint",
            "sealedRootBindings", "sourceAuthorityBinding",
            "targetAuthorityBinding", "triggerCandidateBinding",
            "pendingCausalBinding", "semanticTurnFingerprint",
            "acceptedMechanicsAuthority", "identityLedger", "state",
            "selectedTerminalBinding", "currentWave",
            "resolvedWaveBindings", "terminalFingerprint",
            "authorityFingerprint"
        };
        if (root.Select(static pair => pair.Key)
            .Any(key => !allowed.Contains(key)))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_schema_invalid: continuation contains an unknown root field.");
        }
        foreach (var required in new[]
                 {
                     "schemaVersion", "continuationId", "sessionId",
                     "sessionGeneration", "acceptedSourceTurn", "qteId",
                     "offerFingerprint", "runtimeBeforeFingerprint",
                     "sealedRootBindings", "sourceAuthorityBinding",
                     "targetAuthorityBinding", "triggerCandidateBinding",
                     "pendingCausalBinding", "semanticTurnFingerprint",
                     "acceptedMechanicsAuthority", "identityLedger", "state",
                     "resolvedWaveBindings", "authorityFingerprint"
                 })
        {
            if (!root.ContainsKey(required))
            {
                throw new InvalidDataException(
                    $"qte_deferred_continuation_schema_invalid: required field '{required}' is missing.");
            }
        }
        _ = ReadRequiredExact(root, "sessionId");
        _ = ReadRequiredFingerprint(root, "runtimeBeforeFingerprint");
        _ = ReadRequiredFingerprint(root, "semanticTurnFingerprint");
        if (root["sourceAuthorityBinding"] is not JsonObject ||
            root["targetAuthorityBinding"] is not JsonObject ||
            root["triggerCandidateBinding"] is not JsonObject ||
            root["pendingCausalBinding"] is not JsonObject ||
            root["acceptedMechanicsAuthority"] is not JsonObject ||
            root["resolvedWaveBindings"] is not JsonArray)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_schema_invalid: typed authority fields have invalid shapes.");
        }
    }

    private static void ValidateIdentityLedger(JsonArray? ledger)
    {
        if (ledger == null)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_identity_ledger_invalid: identityLedger must be an array.");
        }
        var keys = new HashSet<string>(StringComparer.Ordinal);
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in ledger)
        {
            if (node is not JsonObject entry ||
                entry.Count != 2 ||
                !entry.ContainsKey("semanticKey") ||
                !entry.ContainsKey("identity"))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_identity_ledger_invalid: ledger entries use a closed two-field schema.");
            }
            var key = ReadRequiredExact(entry, "semanticKey");
            var identity = ReadRequiredExact(entry, "identity");
            if (!keys.Add(key) || !identities.Add(identity))
            {
                throw new InvalidDataException(
                    "qte_deferred_continuation_identity_ledger_invalid: semantic keys and identities must be unique.");
            }
        }
    }

    private static void ValidateStateShape(
        JsonObject root,
        string state)
    {
        var resolvedWaves = root["resolvedWaveBindings"] as JsonArray ??
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: resolvedWaveBindings must be an array.");
        var hasSelected = root.ContainsKey("selectedTerminalBinding");
        var hasCurrentWave = root.ContainsKey("currentWave");
        var hasTerminalFingerprint = root.ContainsKey("terminalFingerprint");
        var valid = state switch
        {
            "armed" =>
                !hasSelected &&
                !hasCurrentWave &&
                !hasTerminalFingerprint &&
                resolvedWaves.Count == 0,
            "terminal_selected" =>
                hasSelected &&
                !hasCurrentWave &&
                !hasTerminalFingerprint &&
                resolvedWaves.Count == 0,
            "awaiting_receipt" =>
                hasSelected &&
                hasCurrentWave &&
                !hasTerminalFingerprint,
            "terminal" =>
                hasSelected &&
                !hasCurrentWave &&
                hasTerminalFingerprint,
            _ => false
        };
        if (!valid)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: continuation optional fields do not match its lifecycle state.");
        }

        if (hasSelected)
            ValidateSelectedTerminalBinding(root["selectedTerminalBinding"]);
        if (hasTerminalFingerprint)
            _ = ReadRequiredFingerprint(root, "terminalFingerprint");
    }

    private static void ValidateSelectedTerminalBinding(JsonNode? node)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "schemaVersion", "sourceTurn", "qteId", "chapterId", "actionId",
            "grade", "outcomeOrdinal", "outcomeId", "responseFingerprint",
            "resourceDraftFingerprint", "fingerprint"
        };
        if (node is not JsonObject binding ||
            binding.Count != allowed.Count ||
            binding.Select(static pair => pair.Key)
                .Any(key => !allowed.Contains(key)) ||
            ReadRequiredInt(binding, "schemaVersion") != 1 ||
            ReadRequiredInt(binding, "sourceTurn") <= 0 ||
            ReadRequiredInt(binding, "outcomeOrdinal") <= 0)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: selected terminal binding must use the closed current schema.");
        }
        _ = ReadRequiredExact(binding, "qteId");
        _ = ReadRequiredExact(binding, "chapterId");
        _ = ReadRequiredExact(binding, "actionId");
        _ = ReadRequiredExact(binding, "outcomeId");
        var grade = ReadRequiredExact(binding, "grade");
        if (grade is not ("success" or "partial" or "fail"))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: selected terminal grade is not registered.");
        }
        _ = ReadRequiredFingerprint(binding, "responseFingerprint");
        if (binding["resourceDraftFingerprint"] != null)
            _ = ReadRequiredFingerprint(binding, "resourceDraftFingerprint");
        var fingerprint = ReadRequiredFingerprint(binding, "fingerprint");
        var fingerprintRoot = binding.DeepClone().AsObject();
        fingerprintRoot.Remove("fingerprint");
        if (!string.Equals(
                fingerprint,
                HashNode("qte-deferred-terminal-selection-v1", fingerprintRoot),
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: selected terminal fingerprint does not seal its exact binding.");
        }
    }

    private static void ValidateAuthorityBindings(
        JsonObject root,
        IReadOnlyDictionary<string, CanonicalBeforeImage> sealedRoots,
        string sessionGeneration,
        int acceptedSourceTurn,
        string offerFingerprint)
    {
        var sourcePaths = EffectAcceptedTurnInputComposer.SourceAuthorityPaths
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var targetPaths = CarrierPaths
            .Concat(new[]
            {
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath
            })
            .Distinct(StringComparer.Ordinal)
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray();
        var expectedSource = CreateRootAuthorityBinding(
            "qte-deferred-effect-sources-v1",
            sourcePaths,
            sealedRoots);
        var expectedTarget = CreateRootAuthorityBinding(
            "qte-deferred-effect-targets-v1",
            targetPaths,
            sealedRoots);

        EffectCarrierCatalog carrierCatalog;
        try
        {
            carrierCatalog = EffectCarrierCatalog.Build(
                new EffectCarrierCatalogInput(
                    ParseObject(sealedRoots[EffectCarrierCatalog.PlayerPath]),
                    ParseObject(sealedRoots[EffectCarrierCatalog.NpcPath]),
                    ParseObject(sealedRoots[EffectCarrierCatalog.EnemiesPath]),
                    ParseObject(sealedRoots[EffectCarrierCatalog.AlliesPath]),
                    ParseObject(sealedRoots[EffectCarrierCatalog.AfterlifeProfilesPath]),
                    ParseObject(sealedRoots[EffectCarrierCatalog.SpiritualConflictPath])));
        }
        catch (Exception ex) when (ex is JsonException or InvalidDataException)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: sealed effect carriers cannot rebuild trigger authority.",
                ex);
        }
        if (carrierCatalog.Issues.Count != 0)
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: sealed effect carriers no longer produce valid trigger authority.");
        }
        var expectedTrigger = CreateTriggerCandidateBinding(carrierCatalog);
        var expectedPending = CreatePendingBinding(
            sealedRoots[ResourcePendingResolutionState.PendingPath]);
        var expectedSemanticTurnFingerprint = HashText(
            "qte-deferred-semantic-turn-v1",
            string.Join(
                "\n",
                sealedRoots
                    .OrderBy(static pair => pair.Key, StringComparer.Ordinal)
                    .Select(static pair =>
                        pair.Key + "=" + pair.Value.Fingerprint)) +
            "\nsourceTurn=" + acceptedSourceTurn +
            "\noffer=" + offerFingerprint);
        var expectedSourceFingerprint = HashText(
            "qte-deferred-accepted-mechanics-source-v1",
            expectedSource["fingerprint"]!.GetValue<string>() + "\n" +
            expectedTarget["fingerprint"]!.GetValue<string>() + "\n" +
            expectedTrigger["fingerprint"]!.GetValue<string>() + "\n" +
            expectedPending["fingerprint"]!.GetValue<string>() + "\n" +
            expectedSemanticTurnFingerprint);
        var expectedAcceptedMechanics = new JsonObject
        {
            ["kind"] = "qte_continuation",
            ["continuationId"] = ReadRequiredExact(root, "continuationId"),
            ["sourceFingerprint"] = expectedSourceFingerprint
        };

        if (!string.Equals(
                ReadRequiredExact(root, "sessionId"),
                ResolveSessionId(sealedRoots, sessionGeneration),
                StringComparison.Ordinal) ||
            !string.Equals(
                ReadRequiredFingerprint(root, "semanticTurnFingerprint"),
                expectedSemanticTurnFingerprint,
                StringComparison.Ordinal) ||
            !JsonNode.DeepEquals(root["sourceAuthorityBinding"], expectedSource) ||
            !JsonNode.DeepEquals(root["targetAuthorityBinding"], expectedTarget) ||
            !JsonNode.DeepEquals(root["triggerCandidateBinding"], expectedTrigger) ||
            !JsonNode.DeepEquals(root["pendingCausalBinding"], expectedPending) ||
            !JsonNode.DeepEquals(
                root["acceptedMechanicsAuthority"],
                expectedAcceptedMechanics))
        {
            throw new InvalidDataException(
                "qte_deferred_continuation_authority_binding_invalid: nested source, target, trigger, pending, session, or semantic authority differs from sealed acceptance roots.");
        }
    }

    private static void ValidateResolvedWaveBindings(
        JsonArray resolvedWaves,
        JsonObject currentWave)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            "requestId", "waveId", "ordinal", "pendingStateFingerprint",
            "receiptsFingerprint", "fingerprint"
        };
        var requestIds = new HashSet<string>(StringComparer.Ordinal);
        var waveIds = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < resolvedWaves.Count; index++)
        {
            if (resolvedWaves[index] is not JsonObject binding ||
                binding.Count != allowed.Count ||
                binding.Select(static pair => pair.Key)
                    .Any(key => !allowed.Contains(key)))
            {
                throw new InvalidDataException(
                    "qte_deferred_receipt_authority_invalid: resolvedWaveBindings entries must use the closed current schema.");
            }
            var requestId = ReadRequiredExact(binding, "requestId");
            var waveId = ReadRequiredExact(binding, "waveId");
            if (!requestIds.Add(requestId) || !waveIds.Add(waveId) ||
                ReadRequiredInt(binding, "ordinal") != index)
            {
                throw new InvalidDataException(
                    "qte_deferred_receipt_authority_invalid: resolved wave identities must be unique and ordinal-contiguous.");
            }
            _ = ReadRequiredFingerprint(binding, "pendingStateFingerprint");
            _ = ReadRequiredFingerprint(binding, "receiptsFingerprint");
            var fingerprint = ReadRequiredFingerprint(binding, "fingerprint");
            var fingerprintRoot = binding.DeepClone().AsObject();
            fingerprintRoot.Remove("fingerprint");
            if (!string.Equals(
                    fingerprint,
                    HashNode("qte-deferred-resolved-wave-v1", fingerprintRoot),
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    "qte_deferred_receipt_authority_invalid: resolved wave fingerprint does not seal its exact binding.");
            }
        }

        var currentRequestId = ReadRequiredExact(currentWave, "requestId");
        var currentWaveId = ReadRequiredExact(currentWave, "waveId");
        if (ReadRequiredInt(currentWave, "ordinal") != resolvedWaves.Count ||
            requestIds.Contains(currentRequestId) ||
            waveIds.Contains(currentWaveId))
        {
            throw new InvalidDataException(
                "qte_deferred_receipt_authority_invalid: current wave must follow every resolved binding exactly once.");
        }
    }

    private static string ReadRequiredExact(JsonObject root, string property)
    {
        var value = root[property]?.GetValue<string>();
        return ResourceMaterializationContract.IsExactIdentifier(value)
            ? value!
            : throw new InvalidDataException(
                $"qte_deferred_continuation_schema_invalid: '{property}' must be an exact identifier.");
    }

    private static string ReadRequiredFingerprint(
        JsonObject root,
        string property)
    {
        var value = root[property]?.GetValue<string>();
        return ResourceMaterializationContract.IsAuthorityFingerprint(value)
            ? value!
            : throw new InvalidDataException(
                $"qte_deferred_continuation_schema_invalid: '{property}' must be a sha256 authority fingerprint.");
    }

    private static int ReadRequiredInt(JsonObject root, string property)
    {
        if (root[property] is JsonValue value &&
            value.TryGetValue<int>(out var result))
        {
            return result;
        }
        throw new InvalidDataException(
            $"qte_deferred_continuation_schema_invalid: '{property}' must be an integer.");
    }

    private static JsonObject CreateSealedRootBinding(
        string path,
        CanonicalBeforeImage beforeImage)
    {
        var result = new JsonObject
        {
            ["path"] = path,
            ["existed"] = beforeImage.Existed,
            ["sha256"] = beforeImage.Fingerprint
        };
        if (beforeImage.Existed)
            result["payloadBase64"] = Convert.ToBase64String(beforeImage.Bytes!);
        return result;
    }

    private static JsonObject CreateRootAuthorityBinding(
        string domain,
        IReadOnlyList<string> paths,
        IReadOnlyDictionary<string, CanonicalBeforeImage> roots)
    {
        var binding = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["paths"] = new JsonArray(
                paths.Select(static path => (JsonNode)path).ToArray())
        };
        binding["fingerprint"] = HashText(
            domain,
            string.Join(
                "\n",
                paths.Select(path => path + "=" + roots[path].Fingerprint)));
        return binding;
    }

    private static JsonObject CreateTriggerCandidateBinding(
        EffectCarrierCatalog catalog)
    {
        var candidates = new JsonArray();
        foreach (var occurrence in catalog.Occurrences
                     .OrderBy(static value => value.FilePath, StringComparer.Ordinal)
                     .ThenBy(static value => value.JsonPath, StringComparer.Ordinal)
                     .ThenBy(static value => value.EffectId, StringComparer.Ordinal))
        {
            if (occurrence.Effect["state"]?.GetValue<string>() != "active" ||
                occurrence.Effect["triggers"] is not JsonArray triggers)
            {
                continue;
            }

            var resourceTriggers = new JsonArray(
                triggers
                    .OfType<JsonObject>()
                    .Where(static trigger =>
                        trigger["eventType"]?.GetValue<string>() is
                            "resource_damaged" or "resource_depleted")
                    .OrderBy(static trigger =>
                        trigger["priority"]?.GetValue<int>() ?? 0)
                    .ThenBy(static trigger =>
                        trigger["triggerId"]?.GetValue<string>(),
                        StringComparer.Ordinal)
                    .Select(static trigger => trigger.DeepClone())
                    .ToArray());
            if (resourceTriggers.Count == 0)
                continue;
            candidates.Add(new JsonObject
            {
                ["effectId"] = occurrence.EffectId,
                ["carrierPath"] = occurrence.FilePath,
                ["jsonPath"] = occurrence.JsonPath,
                ["owner"] = new JsonObject
                {
                    ["kind"] = occurrence.Coordinate.Kind,
                    ["ownerId"] = occurrence.Coordinate.OwnerId,
                    ["category"] = occurrence.Coordinate.Category
                },
                ["source"] = occurrence.Effect["source"]?.DeepClone(),
                ["target"] = occurrence.Effect["target"]?.DeepClone(),
                ["triggers"] = resourceTriggers
            });
        }

        var result = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["candidates"] = candidates
        };
        result["fingerprint"] = HashNode(
            "qte-deferred-trigger-candidates-v1",
            result);
        return result;
    }

    private static JsonObject CreatePendingBinding(
        CanonicalBeforeImage beforeImage)
    {
        var result = new JsonObject
        {
            ["schemaVersion"] = 1,
            ["path"] = ResourcePendingResolutionState.PendingPath,
            ["existed"] = beforeImage.Existed,
            ["rootFingerprint"] = beforeImage.Fingerprint
        };
        result["fingerprint"] = HashNode(
            "qte-deferred-pending-causal-v1",
            result);
        return result;
    }

    private static string ResolveSessionId(
        IReadOnlyDictionary<string, CanonicalBeforeImage> roots,
        string sessionGeneration)
    {
        foreach (var path in new[]
                 {
                     "game_state/history/chat_log.json",
                     "input/turn_request.json"
                 })
        {
            CanonicalBeforeImage? beforeImage = null;
            if (roots.TryGetValue(path, out var sealedImage))
                beforeImage = sealedImage;
            if (beforeImage == null || !beforeImage.Existed)
                continue;
            var root = ParseObject(beforeImage);
            var value = root?["sessionId"]?.GetValue<string>();
            if (ResourceMaterializationContract.IsExactIdentifier(value))
                return value!;
        }

        return "qte_session_" + sessionGeneration;
    }

    private static JsonObject? ParseObject(CanonicalBeforeImage beforeImage)
    {
        if (!beforeImage.Existed)
            return null;
        try
        {
            var json = new UTF8Encoding(
                    encoderShouldEmitUTF8Identifier: false,
                    throwOnInvalidBytes: true)
                .GetString(beforeImage.Bytes!)
                .TrimStart('\uFEFF');
            return JsonNode.Parse(json) as JsonObject;
        }
        catch (Exception ex) when (ex is JsonException or DecoderFallbackException)
        {
            throw new InvalidDataException(
                "QTE deferred continuation encountered malformed sealed JSON authority.",
                ex);
        }
    }

    private static string HashNode(string domain, JsonNode node) =>
        HashText(domain, node.ToJsonString());

    private static string HashText(string domain, string value)
    {
        var payload = Encoding.UTF8.GetBytes(domain + "\0" + value);
        return "sha256:" + Convert.ToHexString(
            SHA256.HashData(payload)).ToLowerInvariant();
    }
}
