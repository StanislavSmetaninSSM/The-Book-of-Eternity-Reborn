using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class CanonicalStateNormalizer
{
    private abstract record AcceptedMechanicsNormalizationPreflight
    {
        internal sealed record NoPlan : AcceptedMechanicsNormalizationPreflight;

        internal sealed record Validated(
            AcceptedMechanicsPlan Plan,
            AcceptedMechanicsPlanBinding Binding,
            IReadOnlyDictionary<string, CanonicalBeforeImage> SnapshotBeforeImages)
            : AcceptedMechanicsNormalizationPreflight;
    }

    internal async Task<AcceptedMechanicsPlan?> NormalizeAcceptedMechanicsAsync(
        IReadOnlyDictionary<string, string>? backups,
        MortalLocationAcceptedTurnPlan? mortalLocationPlan = null)
    {
        if (_writeLease == null)
        {
            await using var writeLease = await _fs.AcquireCanonicalWriteLeaseAsync();
            return await BindTo(writeLease).NormalizeAcceptedMechanicsAsync(
                backups,
                mortalLocationPlan);
        }

        var preflight = await PrevalidateAcceptedMechanicsBeforeNormalizationAsync();
        if (preflight is AcceptedMechanicsNormalizationPreflight.Validated)
        {
            return await PublishAcceptedMechanicsAsync(
                backups,
                mortalLocationPlan,
                preflight,
                normalizedAcceptedCarrierBaselines: false);
        }

        EnsureNoPlanAcceptedMechanicsAuthorityStillAbsent();
        return await WithNoPlanWriteAuthority(_writeLease)
            .PublishAcceptedMechanicsAsync(
                backups,
                mortalLocationPlan,
                preflight,
                normalizedAcceptedCarrierBaselines: false);
    }

    private async Task<AcceptedMechanicsPlan?> PublishAcceptedMechanicsAsync(
        IReadOnlyDictionary<string, string>? backups,
        MortalLocationAcceptedTurnPlan? mortalLocationPlan,
        AcceptedMechanicsNormalizationPreflight preflight,
        bool normalizedAcceptedCarrierBaselines)
    {
        ArgumentNullException.ThrowIfNull(preflight);
        if (preflight is AcceptedMechanicsNormalizationPreflight.NoPlan)
        {
            EnsureNoPlanWriteAuthorityActive();
            EnsureNoPlanAcceptedMechanicsAuthorityStillAbsent();
            return null;
        }

        var validated = (AcceptedMechanicsNormalizationPreflight.Validated)preflight;
        if (_writeLease == null)
        {
            throw new InvalidOperationException(
                "Accepted mechanics publication requires the owning canonical write lease.");
        }

        var plan = validated.Plan;
        try
        {
            if (plan.EffectPlan != null)
            {
                await ValidateEffectPlanPublicationBindingAsync(
                    plan.EffectPlan,
                    normalizedAcceptedCarrierBaselines,
                    plan.WoundStageBundle);
            }

            // Other normalizers may intentionally consume plan before-images. The pending
            // manifest and its detached authority are never mutable normalization outputs,
            // so bind them again immediately before consuming the publication handoff.
            await ValidateAcceptedMechanicsSnapshotBeforeImagesAsync(
                validated.SnapshotBeforeImages);
        }
        catch
        {
            InvalidateAcceptedMechanicsHandoffs();
            throw;
        }
        if (!AcceptedMechanicsPlanAuthority.TryTakeValidated(
                _fs,
                _writeLease,
                validated.Binding,
                out var taken) ||
            !taken.Success || taken.Plan == null ||
            !ReferenceEquals(plan, taken.Plan))
        {
            InvalidateAcceptedMechanicsHandoffs();
            throw new InvalidDataException(
                "Accepted mechanics validated handoff changed before publication.");
        }

        var writes = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        if (!plan.AwaitsPendingResolution)
        {
            writes[ResourceMaterializationContract.DefinitionsPath] = plan.DefinitionAfterImage;
            writes[ResourceMaterializationContract.StatePath] = plan.StateAfterImage;
            writes[ResourceMaterializationContract.HistoryPath] = plan.HistoryAfterImage;
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
                    "Accepted mechanics cannot compose canonical resource owner authority from invalid after-images.");
            }
            writes[CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
                JsonNode.Parse(
                    CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(
                        plan.OwnerAuthority,
                        state,
                        history))!.AsObject();
            writes[EffectAcceptedTurnPlan.IdentityIndexPath] = plan.EffectIdentityAfterImage;
            foreach (var pair in plan.OwnerCompanionAfterImages)
                writes[pair.Key] = pair.Value;
            foreach (var pair in plan.EffectCarrierAfterImages)
                writes[pair.Key] = pair.Value;
            AddWoundPublicationWrites(plan, writes);
        }
        foreach (var pair in plan.PendingAfterImages)
        {
            if (pair.Value != null)
                writes[pair.Key] = pair.Value;
        }
        if (!plan.AwaitsPendingResolution)
            await AddOwnerTransitionWritesAsync(plan, writes);

        foreach (var pair in writes.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
        {
            await WriteCanonicalFileAtomicAsync(
                pair.Key,
                pair.Value.ToJsonString(JsonOpts));
        }
        foreach (var path in plan.PendingAfterImages
                     .Where(static pair => pair.Value == null)
                     .Select(static pair => pair.Key)
                     .Concat(plan.ConsumedPaths)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(static path => path, StringComparer.Ordinal))
        {
            _fs.DeleteFile(_writeLease, path);
        }

        if (plan.AwaitsPendingResolution)
        {
            var pending = plan.PendingAfterImages[ResourcePendingResolutionState.PendingPath]
                ?? throw new InvalidDataException(
                    "Pending accepted mechanics plan has no technical pending after-image.");
            await ReadExactPublishedAfterImageAsync(
                ResourcePendingResolutionState.PendingPath,
                pending);
            await ValidatePublishedWoundAfterImagesAsync(plan);
        }
        else
        {
            await ValidatePublishedWoundAfterImagesAsync(plan);
            await ValidatePublishedOwnerTransitionsAsync(plan);
            await ValidatePublishedResourceAfterImagesAsync(plan);
        }
        return plan;
    }

    private async Task<AcceptedMechanicsNormalizationPreflight>
        PrevalidateAcceptedMechanicsBeforeNormalizationAsync()
    {
        if (_writeLease == null)
        {
            throw new InvalidOperationException(
                "Accepted mechanics normalization preflight requires the owning canonical write lease.");
        }
        if (!AcceptedMechanicsPlanAuthority.TryPeekValidated(
                _fs,
                _writeLease,
                out var binding,
                out var peeked))
        {
            EnsureNoPlanAcceptedMechanicsAuthorityStillAbsent();
            return new AcceptedMechanicsNormalizationPreflight.NoPlan();
        }
        if (!peeked.Success || peeked.Plan == null)
            throw new InvalidDataException("Validated accepted mechanics plan is incomplete.");

        try
        {
            await ValidateAcceptedMechanicsBeforeImagesAsync(peeked.Plan);
            var snapshotBeforeImages = CaptureAcceptedMechanicsSnapshotBeforeImages(
                peeked.Plan.BeforeImages);
            ValidateAcceptedMechanicsSnapshotBinding(
                binding,
                snapshotBeforeImages);
            return new AcceptedMechanicsNormalizationPreflight.Validated(
                peeked.Plan,
                binding,
                snapshotBeforeImages);
        }
        catch
        {
            InvalidateAcceptedMechanicsHandoffs();
            throw;
        }
    }

    private void EnsureNoPlanAcceptedMechanicsAuthorityStillAbsent()
    {
        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "No-plan accepted-mechanics authority requires the owning canonical write lease.");
        if (!AcceptedMechanicsPlanAuthority.HasValidated(_fs, writeLease) &&
            !EffectAcceptedTurnPlanAuthority.TryPeekValidated(
                _fs,
                writeLease,
                out _) &&
            !CanonicalFileExists(ResourceMaterializationContract.CommandPath) &&
            !CanonicalFileExists(EffectAcceptedTurnPlan.CommandPath))
        {
            return;
        }

        InvalidateAcceptedMechanicsHandoffs();
        throw new InvalidDataException(
            "Accepted mechanics command authority requires one validated common plan; it was present at or appeared after the no-plan normalization preflight.");
    }

    private void EnsureNoPlanWriteAuthorityActive()
    {
        if (_normalizationWriteAuthority is not CanonicalNormalizationWriteAuthority.NoPlan noPlan)
        {
            throw new InvalidOperationException(
                "No-plan accepted-mechanics publication requires explicit no-plan normalization authority.");
        }
        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "No-plan accepted-mechanics publication requires the owning canonical write lease.");
        if (!ReferenceEquals(noPlan.WriteLease, writeLease))
        {
            throw new InvalidOperationException(
                "No-plan accepted-mechanics publication authority belongs to another canonical write lease.");
        }
        _fs.EnsureCanonicalWriteLeaseActive(writeLease);
    }

    private void InvalidateAcceptedMechanicsHandoffs()
    {
        var writeLease = _writeLease ?? throw new InvalidOperationException(
            "Accepted-mechanics handoff invalidation requires the owning canonical write lease.");
        AcceptedMechanicsPlanAuthority.InvalidateValidated(_fs, writeLease);
        EffectAcceptedTurnPlanAuthority.InvalidateValidated(_fs, writeLease);
    }

    private static IReadOnlyDictionary<string, CanonicalBeforeImage>
        CaptureAcceptedMechanicsSnapshotBeforeImages(
            IReadOnlyDictionary<string, CanonicalBeforeImage> beforeImages)
    {
        var result = new Dictionary<string, CanonicalBeforeImage>(StringComparer.Ordinal);
        foreach (var path in new[]
                 {
                     PendingTurnSnapshotManifestPath,
                     PendingTurnSnapshotAuthority.AuthorityPath
                 })
        {
            if (!beforeImages.TryGetValue(path, out var beforeImage) ||
                !beforeImage.Existed ||
                beforeImage.Bytes == null)
            {
                throw new InvalidDataException(
                    $"Accepted mechanics preflight requires exact pending snapshot authority bytes at '{path}'.");
            }

            result.Add(
                path,
                new CanonicalBeforeImage(existed: true, beforeImage.Bytes));
        }
        return AcceptedMechanicsPlanBinding.CloneReadOnlyBeforeImages(result);
    }

    private static void ValidateAcceptedMechanicsSnapshotBinding(
        AcceptedMechanicsPlanBinding binding,
        IReadOnlyDictionary<string, CanonicalBeforeImage> snapshotBeforeImages)
    {
        var manifestJson = DecodeAcceptedMechanicsUtf8(
            snapshotBeforeImages[PendingTurnSnapshotManifestPath].Bytes!);
        var authorityJson = DecodeAcceptedMechanicsUtf8(
            snapshotBeforeImages[PendingTurnSnapshotAuthority.AuthorityPath].Bytes!);
        PendingTurnSnapshotAuthorityManifest? manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<PendingTurnSnapshotAuthorityManifest>(
                manifestJson,
                PendingSnapshotHashJsonOpts);
        }
        catch (JsonException exception)
        {
            throw new InvalidDataException(
                "Accepted mechanics preflight pending snapshot manifest is malformed.",
                exception);
        }

        if (manifest == null ||
            !string.Equals(manifest.SessionId, binding.SessionId, StringComparison.Ordinal) ||
            !string.Equals(manifest.RequestId, binding.RequestId, StringComparison.Ordinal) ||
            manifest.TurnNumber != binding.Turn ||
            !string.Equals(
                manifest.ManifestPayloadHash,
                binding.SnapshotToken,
                StringComparison.Ordinal) ||
            !string.Equals(
                PendingTurnSnapshotAuthority.ComputeManifestPayloadHash(
                    manifest,
                    PendingSnapshotHashJsonOpts,
                    static value => value.ManifestPayloadHash,
                    static (value, hash) => value.ManifestPayloadHash = hash),
                binding.SnapshotToken,
                StringComparison.Ordinal) ||
            !PendingTurnSnapshotAuthority.TryReadDetachedAuthorityPayload(
                authorityJson,
                out var detached) ||
            detached == null ||
            !string.Equals(
                detached.SnapshotHashMode,
                PendingTurnSnapshotAuthority.ExactSnapshotHashMode,
                StringComparison.Ordinal) ||
            !string.Equals(
                detached.RollbackHashMode,
                PendingTurnSnapshotAuthority.ExactRollbackHashMode,
                StringComparison.Ordinal) ||
            !string.Equals(detached.SessionId, binding.SessionId, StringComparison.Ordinal) ||
            !string.Equals(detached.RequestId, binding.RequestId, StringComparison.Ordinal) ||
            detached.TurnNumber != binding.Turn ||
            !string.Equals(
                detached.ManifestPayloadHash,
                binding.SnapshotToken,
                StringComparison.Ordinal))
        {
            throw new InvalidDataException(
                "Accepted mechanics preflight requires the exact validated common-plan session and snapshot binding.");
        }
    }

    private async Task ValidateAcceptedMechanicsSnapshotBeforeImagesAsync(
        IReadOnlyDictionary<string, CanonicalBeforeImage> snapshotBeforeImages)
    {
        foreach (var pair in snapshotBeforeImages.OrderBy(
                     static value => value.Key,
                     StringComparer.Ordinal))
        {
            var current = await _fs.ReadFileBytesAsync(_writeLease!, pair.Key);
            var expected = pair.Value.Bytes;
            if (current == null ||
                expected == null ||
                !expected.AsSpan().SequenceEqual(current))
            {
                throw new InvalidDataException(
                    "Mortal item accepted-turn authority requires the exact validated common-plan session and snapshot binding; pending snapshot authority changed after preflight.");
            }
        }
    }

    private static string DecodeAcceptedMechanicsUtf8(byte[] bytes)
    {
        var preamble = Encoding.UTF8.GetPreamble();
        var offset = bytes.AsSpan().StartsWith(preamble) ? preamble.Length : 0;
        return Encoding.UTF8.GetString(bytes, offset, bytes.Length - offset);
    }

    private async Task AddOwnerTransitionWritesAsync(
        AcceptedMechanicsPlan plan,
        IDictionary<string, JsonObject> writes)
    {
        foreach (var pathGroup in plan.OwnerTransitions
                     .GroupBy(static value => value.Path, StringComparer.Ordinal)
                     .OrderBy(static group => group.Key, StringComparer.Ordinal))
        {
            if (writes.ContainsKey(pathGroup.Key))
            {
                throw new InvalidDataException(
                    $"Accepted mechanics has conflicting whole-root and typed owner writes for '{pathGroup.Key}'.");
            }

            var currentJson = await _fs.ReadFileAsync(_writeLease!, pathGroup.Key);
            JsonObject root;
            try
            {
                root = currentJson == null
                    ? throw new InvalidDataException(
                        $"Accepted mechanics owner root '{pathGroup.Key}' disappeared before publication.")
                    : JsonNode.Parse(currentJson) as JsonObject ??
                      throw new InvalidDataException(
                          $"Accepted mechanics owner root '{pathGroup.Key}' is not an object.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"Accepted mechanics owner root '{pathGroup.Key}' is malformed.",
                    exception);
            }

            foreach (var transition in pathGroup
                         .OrderBy(static value => value.OwnerRef, StringComparer.Ordinal))
            {
                ApplyOwnerTransition(root, transition);
            }
            writes.Add(pathGroup.Key, root);
        }
    }

    private static void ApplyOwnerTransition(
        JsonObject root,
        AcceptedMechanicsOwnerTransition transition)
    {
        switch (transition.Kind)
        {
            case AcceptedMechanicsOwnerTransitionKind.MortalNpcCreation:
                ApplyMortalNpcCreationTransition(root, transition);
                return;
            case AcceptedMechanicsOwnerTransitionKind.AfterlifeGuardianGacha:
                ApplyAfterlifeGuardianGachaTransition(root, transition);
                return;
            default:
                throw new InvalidDataException(
                    $"Unsupported accepted mechanics owner transition '{transition.Kind}'.");
        }
    }

    private static void ApplyMortalNpcCreationTransition(
        JsonObject root,
        AcceptedMechanicsOwnerTransition transition)
    {
        var actors = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root).ToArray();
        var matches = actors.Where(actor =>
                TryReadExactOwnerTransitionString(actor["initialId"], out var initialId) &&
                string.Equals(initialId, transition.OwnerRef, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidDataException(
                $"Accepted NPC owner ref '{transition.OwnerRef}' resolved {matches.Length} times at publication.");
        }

        var target = matches[0];
        if (!target.ContainsKey("NPCId") || target["NPCId"] != null)
        {
            throw new InvalidDataException(
                $"Accepted NPC owner ref '{transition.OwnerRef}' no longer has a null NPCId.");
        }
        if (target["resourceMaterialization"] is not JsonObject materialization ||
            !JsonNode.DeepEquals(
                materialization,
                transition.ExpectedResourceMaterialization))
        {
            throw new InvalidDataException(
                $"Accepted NPC owner ref '{transition.OwnerRef}' changed its resource materialization before publication.");
        }
        if (target.ContainsKey("currentHealthPercentage") ||
            target.ContainsKey("maxHealthPercentage"))
        {
            throw new InvalidDataException(
                $"Accepted NPC owner ref '{transition.OwnerRef}' regained legacy resource authority before publication.");
        }

        var permanentAlias = ResourceMaterializationContract.BuildConfusableKey(
            transition.PermanentOwnerId);
        foreach (var actor in actors)
        {
            if (ReferenceEquals(actor, target) ||
                !TryReadExactOwnerTransitionString(actor["NPCId"], out var existingNpcId))
            {
                continue;
            }
            if (string.Equals(
                    ResourceMaterializationContract.BuildConfusableKey(existingNpcId),
                    permanentAlias,
                    StringComparison.Ordinal))
            {
                throw new InvalidDataException(
                    $"Accepted NPC permanent ID '{transition.PermanentOwnerId}' became ambiguous before publication.");
            }
        }

        target["NPCId"] = transition.PermanentOwnerId;
        target.Remove("initialId");
        target.Remove("resourceMaterialization");
    }

    private static void ApplyAfterlifeGuardianGachaTransition(
        JsonObject root,
        AcceptedMechanicsOwnerTransition transition)
    {
        var guardian = ResolveExactGuardianTransitionOwner(root, transition.OwnerRef);
        var payload = transition.Payload;
        if (!TryReadExactOwnerTransitionString(
                payload["returnCycleId"],
                out var returnCycleId) ||
            payload["expectedHistoryPrefix"] is not JsonArray expectedPrefix ||
            payload["appendedHistoryEntries"] is not JsonArray appended ||
            appended.Count == 0)
        {
            throw new InvalidDataException(
                $"Accepted Guardian gacha transition for '{transition.OwnerRef}' has an invalid payload.");
        }
        if (guardian["gachaSystem"] is not JsonObject gacha ||
            !TryReadExactOwnerTransitionString(
                gacha["currentReturnCycleId"],
                out var currentCycleId) ||
            !string.Equals(currentCycleId, returnCycleId, StringComparison.Ordinal) ||
            gacha["gachaHistory"] is not JsonArray history)
        {
            throw new InvalidDataException(
                $"Guardian '{transition.OwnerRef}' changed its gacha return-cycle authority before publication.");
        }
        if (gacha.ContainsKey("chargesPerReturn") ||
            gacha.ContainsKey("chargesUsedThisReturn"))
        {
            throw new InvalidDataException(
                $"Guardian '{transition.OwnerRef}' regained legacy gacha counter authority before publication.");
        }
        if (!ArrayHasExactPrefix(history, expectedPrefix) ||
            history.Count != expectedPrefix.Count)
        {
            throw new InvalidDataException(
                $"Guardian '{transition.OwnerRef}' gacha history changed before common publication.");
        }

        foreach (var entry in appended)
            history.Add(entry?.DeepClone());
        ConsumeGuardianGachaCommands(root, transition.OwnerRef, appended.Count);
        if (root["activeGuardian"] is JsonObject activeGuardian &&
            TryReadExactOwnerTransitionString(
                activeGuardian["guardianId"],
                out var activeGuardianId) &&
            string.Equals(activeGuardianId, transition.OwnerRef, StringComparison.Ordinal))
        {
            root["activeGuardian"] = guardian.DeepClone();
        }
    }

    private static JsonObject ResolveExactGuardianTransitionOwner(
        JsonObject root,
        string guardianId)
    {
        if (root["guardians"] is not JsonArray guardians)
            throw new InvalidDataException("Accepted Guardian owner root has no canonical guardians array.");
        var exact = guardians.OfType<JsonObject>().Where(guardian =>
            TryReadExactOwnerTransitionString(
                guardian["guardianId"],
                out var candidateId) &&
            string.Equals(candidateId, guardianId, StringComparison.Ordinal)).ToArray();
        var alias = ResourceMaterializationContract.BuildConfusableKey(guardianId);
        var confusable = guardians.OfType<JsonObject>().Count(guardian =>
            TryReadExactOwnerTransitionString(
                guardian["guardianId"],
                out var candidateId) &&
            string.Equals(
                ResourceMaterializationContract.BuildConfusableKey(candidateId),
                alias,
                StringComparison.Ordinal));
        if (exact.Length != 1 || confusable != 1)
        {
            throw new InvalidDataException(
                $"Accepted Guardian owner '{guardianId}' is no longer exact/confusable-unique.");
        }
        return exact[0];
    }

    private static bool ArrayHasExactPrefix(JsonArray actual, JsonArray expected)
    {
        if (actual.Count < expected.Count)
            return false;
        for (var index = 0; index < expected.Count; index++)
        {
            if (!JsonNode.DeepEquals(actual[index], expected[index]))
                return false;
        }
        return true;
    }

    private static void ConsumeGuardianGachaCommands(
        JsonObject root,
        string guardianId,
        int expectedCount)
    {
        if (root["UpdateGuardians"] is not JsonArray updates)
            return;
        var retained = new JsonArray();
        var consumed = 0;
        foreach (var update in updates)
        {
            if (update is JsonObject command &&
                string.Equals(
                    command["command"]?.GetValue<string>(),
                    "processGacha",
                    StringComparison.Ordinal) &&
                string.Equals(
                    command["guardianId"]?.GetValue<string>(),
                    guardianId,
                    StringComparison.Ordinal))
            {
                consumed++;
                continue;
            }
            retained.Add(update?.DeepClone());
        }
        if (consumed != expectedCount)
        {
            throw new InvalidDataException(
                $"Guardian '{guardianId}' processGacha command count changed before publication.");
        }
        if (retained.Count == 0)
            root.Remove("UpdateGuardians");
        else
            root["UpdateGuardians"] = retained;
    }

    private async Task ValidatePublishedOwnerTransitionsAsync(
        AcceptedMechanicsPlan plan)
    {
        foreach (var pathGroup in plan.OwnerTransitions
                     .GroupBy(static value => value.Path, StringComparer.Ordinal))
        {
            var json = await _fs.ReadFileAsync(_writeLease!, pathGroup.Key);
            JsonObject root;
            try
            {
                root = json == null
                    ? throw new InvalidDataException(
                        $"Published owner root '{pathGroup.Key}' is missing.")
                    : JsonNode.Parse(json) as JsonObject ??
                      throw new InvalidDataException(
                          $"Published owner root '{pathGroup.Key}' is not an object.");
            }
            catch (JsonException exception)
            {
                throw new InvalidDataException(
                    $"Published owner root '{pathGroup.Key}' is malformed.",
                    exception);
            }

            foreach (var transition in pathGroup)
            {
                if (transition.Kind == AcceptedMechanicsOwnerTransitionKind.MortalNpcCreation)
                {
                    var matches = GuardianPolicyContracts.EnumerateCanonicalNpcObjects(root)
                        .Where(actor =>
                            TryReadExactOwnerTransitionString(actor["NPCId"], out var npcId) &&
                            string.Equals(
                                npcId,
                                transition.PermanentOwnerId,
                                StringComparison.Ordinal))
                        .ToArray();
                    if (matches.Length != 1 ||
                        matches[0].ContainsKey("initialId") ||
                        matches[0].ContainsKey("resourceMaterialization"))
                    {
                        throw new InvalidDataException(
                            $"Published NPC owner '{transition.PermanentOwnerId}' did not consume its temporary authority exactly once.");
                    }
                    continue;
                }
                if (transition.Kind == AcceptedMechanicsOwnerTransitionKind.AfterlifeGuardianGacha)
                    ValidatePublishedGuardianGachaTransition(root, transition);
            }
        }
    }

    private static void ValidatePublishedGuardianGachaTransition(
        JsonObject root,
        AcceptedMechanicsOwnerTransition transition)
    {
        var guardian = ResolveExactGuardianTransitionOwner(root, transition.OwnerRef);
        var payload = transition.Payload;
        if (!TryReadExactOwnerTransitionString(
                payload["returnCycleId"],
                out var returnCycleId) ||
            payload["expectedHistoryPrefix"] is not JsonArray prefix ||
            payload["appendedHistoryEntries"] is not JsonArray appended ||
            guardian["gachaSystem"] is not JsonObject gacha ||
            !TryReadExactOwnerTransitionString(
                gacha["currentReturnCycleId"],
                out var currentCycleId) ||
            !string.Equals(currentCycleId, returnCycleId, StringComparison.Ordinal) ||
            gacha["gachaHistory"] is not JsonArray history ||
            history.Count != prefix.Count + appended.Count ||
            !ArrayHasExactPrefix(history, prefix) ||
            gacha.ContainsKey("chargesPerReturn") ||
            gacha.ContainsKey("chargesUsedThisReturn"))
        {
            throw new InvalidDataException(
                $"Published Guardian gacha transition for '{transition.OwnerRef}' is incomplete.");
        }
        for (var index = 0; index < appended.Count; index++)
        {
            if (!JsonNode.DeepEquals(history[prefix.Count + index], appended[index]))
            {
                throw new InvalidDataException(
                    $"Published Guardian gacha history for '{transition.OwnerRef}' does not match the accepted resource plan.");
            }
        }
        if (root["activeGuardian"] is JsonObject activeGuardian &&
            TryReadExactOwnerTransitionString(
                activeGuardian["guardianId"],
                out var activeGuardianId) &&
            string.Equals(activeGuardianId, transition.OwnerRef, StringComparison.Ordinal) &&
            !JsonNode.DeepEquals(activeGuardian, guardian))
        {
            throw new InvalidDataException(
                $"Published active Guardian '{transition.OwnerRef}' is not synchronized.");
        }
    }

    private static bool TryReadExactOwnerTransitionString(
        JsonNode? node,
        out string value)
    {
        value = node is JsonValue jsonValue &&
                jsonValue.TryGetValue<string>(out var text)
            ? text
            : string.Empty;
        return ResourceMaterializationContract.IsExactIdentifier(value);
    }

    private async Task ValidateAcceptedMechanicsBeforeImagesAsync(
        AcceptedMechanicsPlan plan)
    {
        foreach (var pair in plan.BeforeImages.OrderBy(
                     static pair => pair.Key,
                     StringComparer.Ordinal))
        {
            var current = await _fs.ReadFileBytesAsync(_writeLease!, pair.Key);
            var exact = pair.Value.Existed == (current != null) &&
                        (pair.Value.Bytes == null
                            ? current == null
                            : current != null && pair.Value.Bytes.AsSpan().SequenceEqual(current));
            if (!exact)
            {
                AcceptedMechanicsPlanAuthority.InvalidateValidated(
                    _fs,
                    _writeLease!);
                throw new InvalidDataException(
                    $"Accepted mechanics authority at '{pair.Key}' changed after validation.");
            }
        }
    }

    private async Task ValidatePublishedResourceAfterImagesAsync(
        AcceptedMechanicsPlan plan)
    {
        var definitionsJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.DefinitionsPath,
            plan.DefinitionAfterImage);
        var stateJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.StatePath,
            plan.StateAfterImage);
        var historyJson = await ReadExactPublishedAfterImageAsync(
            ResourceMaterializationContract.HistoryPath,
            plan.HistoryAfterImage);
        if (_fs.FileExists(
                _writeLease!,
                ResourceMaterializationContract.CommandPath))
        {
            throw new InvalidDataException(
                "Accepted mechanics left the consumed resource command root published.");
        }

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            definitionsJson,
            allowMissingPristine: false);
        if (definitions.Catalog == null || definitions.Issues.Count != 0)
            throw InvalidPublishedResource(definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            stateJson,
            definitions.Catalog,
            allowMissingPristine: false);
        var history = ResourceHistoryState.ParseCanonical(
            historyJson,
            definitions.Catalog,
            allowMissingPristine: false);
        if (state.Ledger != null && history.History != null)
        {
            var ownerAuthorityRoot = JsonNode.Parse(
                CanonicalResourceOwnerAuthorityComposer.CreateCanonicalAuthorityJson(
                    plan.OwnerAuthority,
                    state.Ledger,
                    history.History))!.AsObject();
            _ = await ReadExactPublishedAfterImageAsync(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                ownerAuthorityRoot);
        }
        var issues = state.Issues.Concat(history.Issues).ToList();
        if (state.Ledger != null && history.History != null)
        {
            issues.AddRange(history.History.ValidateStateAgreement(state.Ledger));
            issues.AddRange(plan.OwnerAuthority.ValidateCanonicalAgreement(
                state.Ledger,
                history.History));
        }
        if (issues.Any(static issue => issue.Severity == IssueSeverity.Error))
            throw InvalidPublishedResource(issues);
    }

    private async Task<string> ReadExactPublishedAfterImageAsync(
        string path,
        JsonObject plannedAfterImage)
    {
        var current = await _fs.ReadFileBytesAsync(_writeLease!, path);
        var expectedJson = plannedAfterImage.ToJsonString(JsonOpts);
        var body = Encoding.UTF8.GetBytes(expectedJson);
        var preamble = Encoding.UTF8.GetPreamble();
        var expected = new byte[preamble.Length + body.Length];
        Buffer.BlockCopy(preamble, 0, expected, 0, preamble.Length);
        Buffer.BlockCopy(body, 0, expected, preamble.Length, body.Length);
        if (current == null || !current.AsSpan().SequenceEqual(expected))
        {
            throw new InvalidDataException(
                $"Accepted mechanics publication at '{path}' differs from its validated after-image.");
        }
        return expectedJson;
    }

    private static InvalidDataException InvalidPublishedResource(
        IReadOnlyList<ValidationIssue> issues)
    {
        var issue = issues.FirstOrDefault();
        return new InvalidDataException(issue == null
            ? "Accepted mechanics produced an invalid resource after-image."
            : $"Accepted mechanics produced invalid resource state: {issue.Code} at {issue.FilePath}.");
    }
}
