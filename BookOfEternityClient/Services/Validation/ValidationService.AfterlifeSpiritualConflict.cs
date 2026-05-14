using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    private async Task ValidateAfterlifeSpiritualConflictStateAsync(List<ValidationIssue> issues)
    {
        var json = await _fs.ReadFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(json))
        {
            if (_fs.FileExists(AfterlifeSpiritualConflictState.StatePath))
            {
                issues.Add(new ValidationIssue(
                    AfterlifeSpiritualConflictState.StatePath,
                    IssueSeverity.Error,
                    "afterlife_spiritual_conflict_state.json существует, но пуст.",
                    code: "afterlife_conflict_state_empty",
                    section: "AfterlifeSpiritualConflict",
                    expected: "JSON object with schemaVersion, activeConflict, recentConflicts",
                    actual: "empty/whitespace",
                    repairHint: "Восстанови canonical conflict root: { schemaVersion: 1, activeConflict: null, recentConflicts: [] }."));
            }

            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
            return;
        }

        JsonObject root;
        try
        {
            root = JsonNode.Parse(json) as JsonObject
                   ?? throw new JsonException("Root is not object.");
        }
        catch
        {
            issues.Add(new ValidationIssue(
                AfterlifeSpiritualConflictState.StatePath,
                IssueSeverity.Error,
                "afterlife_spiritual_conflict_state.json должен быть валидным JSON object.",
                code: "afterlife_conflict_state_invalid_json",
                section: "AfterlifeSpiritualConflict",
                expected: "JSON object",
                actual: "unreadable/non-object"));
            await ValidateActiveConflictRemovalHasTerminalProofAsync(null, issues);
            return;
        }

        var gateContext = await ResolveAfterlifeSpiritualConflictGateContextAsync();
        var diceContext = await ResolveAfterlifeConflictDiceContextAsync(gateContext.Manifest);
        await ValidateActiveConflictRemovalHasTerminalProofAsync(root, issues);
        ValidateAfterlifeSpiritualConflictRoot(root, AfterlifeSpiritualConflictState.StatePath, issues, diceContext);

        if (root["activeConflict"] is JsonObject activeConflict)
        {
            var gateRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(gateContext.Realm);
            if (gateRealmKey == null)
            {
                issues.Add(new ValidationIssue(
                    $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                    IssueSeverity.Error,
                    "Активный afterlife spiritual conflict допустим только в Chaos Sea или Shining Abode.",
                    code: "afterlife_conflict_active_wrong_realm",
                    section: "AfterlifeSpiritualConflict",
                    expected: gateContext.UsesValidatedSnapshot
                        ? "validated pre-turn soul_state.currentRealm = Chaos Sea or Shining Abode"
                        : "soul_state.currentRealm = Chaos Sea or Shining Abode",
                    actual: string.IsNullOrWhiteSpace(gateContext.Realm) ? "missing/empty" : gateContext.Realm,
                    repairHint: "Не переносите activeConflict в Mortal World. Сначала resolve/repair_cancel конфликт в afterlife или восстанови currentRealm."));
            }
            else
            {
                var activeRealm = AfterlifeSpiritualConflictState.GetNodeString(activeConflict["realm"]);
                var activeRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(activeRealm);
                if (activeRealmKey != null &&
                    !string.Equals(activeRealmKey, gateRealmKey, StringComparison.Ordinal))
                {
                    issues.Add(new ValidationIssue(
                        $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                        IssueSeverity.Error,
                        "activeConflict.realm должен совпадать с authority realm души.",
                        code: "afterlife_conflict_active_realm_mismatch",
                        section: "AfterlifeSpiritualConflict",
                        expected: gateContext.UsesValidatedSnapshot
                            ? $"activeConflict.realm normalized to validated pre-turn realm {gateRealmKey}"
                            : $"activeConflict.realm normalized to current realm {gateRealmKey}",
                        actual: string.IsNullOrWhiteSpace(activeRealm) ? "missing/empty" : activeRealm,
                        repairHint: "Не продвигайте конфликт из другого afterlife realm. Resolve/repair_cancel старый конфликт или восстанови authority realm/activeConflict.realm до одного realm."));
                }

                if (string.Equals(gateRealmKey, "shining_abode", StringComparison.Ordinal))
                {
                    var availability = await TryReadShiningAvailabilityForConflictGateAsync(gateContext);
                    if (!string.Equals(availability, ShiningAbodeState.AvailabilityActive, StringComparison.OrdinalIgnoreCase))
                    {
                        issues.Add(new ValidationIssue(
                            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                            IssueSeverity.Error,
                            "Активный afterlife spiritual conflict допустим только в ordinary active Shining Abode.",
                            code: "afterlife_conflict_active_during_sealed_shining_abode",
                            section: "AfterlifeSpiritualConflict",
                            expected: gateContext.UsesValidatedSnapshot
                                ? "validated pre-turn shining_abode_state.availability = active"
                                : "shining_abode_state.availability = active",
                            actual: string.IsNullOrWhiteSpace(availability) ? "missing/empty" : availability,
                            repairHint: "Не запускай и не продвигай afterlife spiritual conflict, пока Сияющая Обитель sealed_until_next_ascension или иначе не active."));
                    }

                    var packageMode = await TryReadShiningPreparedPackageModeForConflictGateAsync(gateContext);
                    if (packageMode != ShiningAbodeState.PreparedIncarnationPackageMode.Absent)
                    {
                        issues.Add(new ValidationIssue(
                            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
                            IssueSeverity.Error,
                            "Активный afterlife spiritual conflict недопустим в Shining pending-bootstrap handoff или package-fault mode.",
                            code: "afterlife_conflict_active_during_shining_bootstrap",
                            section: "AfterlifeSpiritualConflict",
                            expected: gateContext.UsesValidatedSnapshot
                                ? "validated pre-turn ordinary active Shining Abode with preparedIncarnationPackage absent/null"
                                : "ordinary active Shining Abode with preparedIncarnationPackage absent/null",
                            actual: packageMode.ToString(),
                            repairHint: "В Shining pending-bootstrap handoff GM пишет только TriggerIncarnation и сохраняет preparedIncarnationPackage; не запускай и не продвигай afterlife spiritual conflict до завершения handoff/repair."));
                    }
                }
            }
        }
    }

    private sealed record AfterlifeSpiritualConflictGateContext(
        string? Realm,
        bool UsesValidatedSnapshot,
        ValidationPendingTurnSnapshotManifest? Manifest);

    private sealed record AfterlifeConflictDiceContext(
        int[]? AuthoritativeDice,
        int? LightIncarnateGrantTurn = null,
        IReadOnlyList<JsonObject>? PreTurnNoTurnDicePayloads = null)
    {
        public bool HasAuthoritativeDice => AuthoritativeDice is { Length: > 0 };
        public bool HasLightIncarnate => LightIncarnateGrantTurn is > 0;

        public bool IsPreTurnNoTurnDicePayload(JsonObject payload) =>
            PreTurnNoTurnDicePayloads?.Any(preTurnPayload => JsonNode.DeepEquals(preTurnPayload, payload)) == true;
    }

    private async Task<AfterlifeSpiritualConflictGateContext> ResolveAfterlifeSpiritualConflictGateContextAsync()
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable && lookup.Manifest != null)
        {
            return new AfterlifeSpiritualConflictGateContext(
                await TryReadValidatedPendingTurnSnapshotRealmAsync(lookup.Manifest),
                true,
                lookup.Manifest);
        }

        return new AfterlifeSpiritualConflictGateContext(
            await TryReadCurrentSoulRealmAsync(),
            false,
            null);
    }

    private async Task<AfterlifeConflictDiceContext> ResolveAfterlifeConflictDiceContextAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        var lightIncarnateGrantTurn = await ResolveLightIncarnateGrantTurnAsync();
        var preTurnNoTurnDicePayloads = await ResolvePreTurnNoTurnConflictDicePayloadsAsync(manifest);

        if (manifest?.PreGeneratedDices1d20 is { Length: > 0 } manifestDice)
            return new AfterlifeConflictDiceContext(manifestDice, lightIncarnateGrantTurn, preTurnNoTurnDicePayloads);

        var liveRequestJson = await _fs.ReadFileAsync("input/turn_request.json");
        if (string.IsNullOrWhiteSpace(liveRequestJson))
            return new AfterlifeConflictDiceContext(null, lightIncarnateGrantTurn, preTurnNoTurnDicePayloads);

        try
        {
            if (JsonNode.Parse(liveRequestJson) is JsonObject root &&
                root["preGeneratedDices1d20"] is JsonArray diceArray)
            {
                var dice = new List<int>();
                foreach (var item in diceArray)
                {
                    if (TryGetJsonNodeInt(item, out var value))
                        dice.Add(value);
                }

                if (dice.Count > 0)
                    return new AfterlifeConflictDiceContext(dice.ToArray(), lightIncarnateGrantTurn, preTurnNoTurnDicePayloads);
            }
        }
        catch
        {
            // Other validators report malformed live turn requests; dice audit falls back to shape-only checks.
        }

        return new AfterlifeConflictDiceContext(null, lightIncarnateGrantTurn, preTurnNoTurnDicePayloads);
    }

    private async Task<IReadOnlyList<JsonObject>> ResolvePreTurnNoTurnConflictDicePayloadsAsync(
        ValidationPendingTurnSnapshotManifest? manifest)
    {
        if (manifest == null)
            return Array.Empty<JsonObject>();

        var preTurnJson = await ReadValidatedCurrentPreTurnTrackedFileAsync(AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return Array.Empty<JsonObject>();

        try
        {
            if (JsonNode.Parse(preTurnJson) is not JsonObject root)
                return Array.Empty<JsonObject>();

            var payloads = new List<JsonObject>();
            if (root["recentConflicts"] is JsonArray recentConflicts)
            {
                foreach (var entry in recentConflicts.OfType<JsonObject>())
                    TryAddPreTurnNoTurnDicePayload(payloads, entry);
            }

            if (root["activeConflict"] is JsonObject activeConflict &&
                activeConflict["exchangeLog"] is JsonArray exchangeLog)
            {
                foreach (var entry in exchangeLog.OfType<JsonObject>())
                    TryAddPreTurnNoTurnDicePayload(payloads, entry);
            }

            return payloads;
        }
        catch
        {
            // Malformed conflict state is reported by the normal state validator.
            return Array.Empty<JsonObject>();
        }
    }

    private static void TryAddPreTurnNoTurnDicePayload(List<JsonObject> payloads, JsonObject payload)
    {
        if (payload["diceAudit"] is not JsonObject diceAudit)
            return;

        if (ResolveLightIncarnateAuditTurn(payload, diceAudit) is > 0)
            return;

        if (payload.DeepClone() is JsonObject clone)
            payloads.Add(clone);
    }

    private async Task<int?> ResolveLightIncarnateGrantTurnAsync()
    {
        var soulRoot = await ReadJsonObjectAsync("game_state/meta/soul_state.json");
        if (!SourceOfLightCapstoneState.HasLightIncarnate(soulRoot))
            return null;

        var shiningRoot = await ReadJsonObjectAsync(ShiningAbodeState.StatePath);
        return SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(soulRoot, shiningRoot);
    }

    private async Task<string?> TryReadShiningAvailabilityForConflictGateAsync(AfterlifeSpiritualConflictGateContext gateContext)
    {
        if (gateContext.UsesValidatedSnapshot && gateContext.Manifest != null)
        {
            var root = await TryReadValidatedShiningRootAsync(gateContext.Manifest);
            return root == null ? null : AfterlifeSpiritualConflictState.GetNodeString(root["availability"]);
        }

        return await TryReadCurrentShiningAvailabilityAsync();
    }

    private async Task<ShiningAbodeState.PreparedIncarnationPackageMode> TryReadShiningPreparedPackageModeForConflictGateAsync(
        AfterlifeSpiritualConflictGateContext gateContext)
    {
        if (gateContext.UsesValidatedSnapshot && gateContext.Manifest != null)
        {
            var root = await TryReadValidatedShiningRootAsync(gateContext.Manifest);
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }

        return await TryReadCurrentShiningPreparedPackageModeAsync();
    }

    private async Task<JsonObject?> TryReadValidatedShiningRootAsync(ValidationPendingTurnSnapshotManifest manifest)
    {
        var snapshotJson = await ReadValidatedPendingTurnSnapshotFileAsync(manifest, ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;

        try
        {
            return JsonNode.Parse(snapshotJson) as JsonObject;
        }
        catch
        {
            return null;
        }
    }

    private async Task ValidateActiveConflictRemovalHasTerminalProofAsync(JsonObject? currentRoot, List<ValidationIssue> issues)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable || lookup.Manifest == null)
            return;

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(lookup.Manifest, AfterlifeSpiritualConflictState.StatePath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return;

        JsonObject? preTurnRoot;
        try
        {
            preTurnRoot = JsonNode.Parse(preTurnJson) as JsonObject;
        }
        catch
        {
            return;
        }

        if (preTurnRoot?["activeConflict"] is not JsonObject)
            return;

        var preTurnConflictId = TryReadActiveConflictId(preTurnRoot);
        if (string.IsNullOrWhiteSpace(preTurnConflictId))
            return;

        var currentConflictId = currentRoot == null ? null : TryReadActiveConflictId(currentRoot);
        if (!string.IsNullOrWhiteSpace(currentConflictId) &&
            string.Equals(currentConflictId, preTurnConflictId, StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        if (HasTerminalProofForConflict(currentRoot, preTurnConflictId))
            return;

        issues.Add(new ValidationIssue(
            $"{AfterlifeSpiritualConflictState.StatePath}.activeConflict",
            IssueSeverity.Error,
            "Pre-turn active afterlife spiritual conflict был удалён или заменён без terminal proof.",
            code: "afterlife_conflict_active_removed_without_terminal_proof",
            section: "AfterlifeSpiritualConflict",
            expected: $"activeConflict.conflictId = {preTurnConflictId} или matching recentConflicts[] resolve/repair_cancel proof",
            actual: currentRoot == null
                ? "current conflict state missing/unreadable"
                : string.IsNullOrWhiteSpace(currentConflictId)
                    ? "activeConflict missing/null and no matching terminal proof"
                    : $"activeConflict.conflictId = {currentConflictId} without terminal proof for {preTurnConflictId}",
            repairHint: "Восстанови pre-turn activeConflict или закрой его через afterlifeSpiritualConflictUpdate.mode=resolve либо mode=repair_cancel, чтобы recentConflicts[] содержал matching terminal proof."));
    }

    private static string? TryReadActiveConflictId(JsonObject root)
    {
        return root["activeConflict"] is JsonObject activeConflict
            ? AfterlifeSpiritualConflictState.GetNodeString(activeConflict["conflictId"]) ??
              AfterlifeSpiritualConflictState.GetNodeString(activeConflict["id"])
            : null;
    }

    private static bool HasTerminalProofForConflict(JsonObject? currentRoot, string conflictId)
    {
        if (currentRoot?["recentConflicts"] is not JsonArray recentConflicts)
            return false;

        return recentConflicts
            .OfType<JsonObject>()
            .Any(proof => IsTerminalRecentConflictProof(proof, conflictId));
    }

    private static bool IsTerminalRecentConflictProof(JsonObject proof, string conflictId)
    {
        var proofConflictId = AfterlifeSpiritualConflictState.GetNodeString(proof["conflictId"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(proof["id"]);
        if (!string.Equals(proofConflictId, conflictId, StringComparison.OrdinalIgnoreCase))
            return false;

        var resolutionState = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionState"]) ??
                              AfterlifeSpiritualConflictState.GetNodeString(proof["status"]);
        if (string.Equals(resolutionState, "repair_cancelled", StringComparison.OrdinalIgnoreCase))
            return true;

        if (!string.Equals(resolutionState, "resolved", StringComparison.OrdinalIgnoreCase))
            return false;

        var operationType = AfterlifeSpiritualConflictState.GetNodeString(proof["operationType"]);
        return AfterlifeSpiritualConflictState.GetNodeInt(proof["resolvedAtTurn"]) > 0 &&
               !string.IsNullOrWhiteSpace(operationType) &&
               AfterlifeSpiritualConflictState.OperationTypes.Contains(operationType) &&
               HasTerminalResolveOutcomeEvidence(proof);
    }

    private static bool HasTerminalResolveOutcomeEvidence(JsonObject proof)
    {
        if (!string.IsNullOrWhiteSpace(AfterlifeSpiritualConflictState.GetNodeString(proof["playerOutcome"])))
            return true;

        var resolutionKind = AfterlifeSpiritualConflictState.GetNodeString(proof["resolutionKind"]);
        return string.Equals(resolutionKind, "player_loss", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resolutionKind, "player_surrender", StringComparison.OrdinalIgnoreCase) ||
               string.Equals(resolutionKind, "player_concession", StringComparison.OrdinalIgnoreCase);
    }

    private async Task<string?> TryReadCurrentShiningAvailabilityAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return null;

        try
        {
            return JsonNode.Parse(shiningJson) is JsonObject root
                ? AfterlifeSpiritualConflictState.GetNodeString(root["availability"])
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryReadCurrentSoulRealmAsync()
    {
        var soulJson = await _fs.ReadFileAsync("game_state/meta/soul_state.json");
        if (string.IsNullOrWhiteSpace(soulJson))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(soulJson);
            return doc.RootElement.ValueKind == JsonValueKind.Object &&
                   doc.RootElement.TryGetProperty("currentRealm", out var realm) &&
                   realm.ValueKind == JsonValueKind.String
                ? realm.GetString()
                : null;
        }
        catch
        {
            return null;
        }
    }

    private async Task<ShiningAbodeState.PreparedIncarnationPackageMode> TryReadCurrentShiningPreparedPackageModeAsync()
    {
        var shiningJson = await _fs.ReadFileAsync(ShiningAbodeState.StatePath);
        if (string.IsNullOrWhiteSpace(shiningJson))
            return ShiningAbodeState.PreparedIncarnationPackageMode.Absent;

        try
        {
            var root = JsonNode.Parse(shiningJson) as JsonObject;
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }
        catch
        {
            return ShiningAbodeState.PreparedIncarnationPackageMode.Absent;
        }
    }

    private void ValidateAfterlifeCombatProfile(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(AfterlifeSpiritualConflictState.SoulStateProfileProperty, out var profile))
            return;

        var context = $"{contextPrefix}.{AfterlifeSpiritualConflictState.SoulStateProfileProperty}";
        if (!RequireObject(profile, context, issues))
            return;

        ValidateNonNegativeIntegerField(profile, context, issues, "schemaVersion", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "enlightenmentRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "radianceRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "retainedRadianceRank", "AfterlifeSpiritualConflict");
        ValidateNonNegativeIntegerField(profile, context, issues, "lastRecoveryTurn", "AfterlifeSpiritualConflict");
        ValidateLightIncarnateCombatProfileCapstone(profile, context, issues);

        if (!profile.TryGetProperty("artTiers", out var artTiers))
            return;

        if (!RequireObject(artTiers, $"{context}.artTiers", issues))
            return;

        var allowedArtIds = AfterlifeSpiritualConflictState.SpiritualArts
            .Select(art => art.ArtId)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (var art in artTiers.EnumerateObject())
        {
            if (!allowedArtIds.Contains(art.Name))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.artTiers.{art.Name}",
                    IssueSeverity.Error,
                    "afterlifeCombatProfile.artTiers содержит неизвестный spiritual art id.",
                    code: "afterlife_combat_profile_unknown_art",
                    section: "AfterlifeSpiritualConflict",
                    expected: string.Join("/", allowedArtIds.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                    actual: art.Name));
                continue;
            }

            if (art.Value.ValueKind != JsonValueKind.Number ||
                !art.Value.TryGetInt32(out var tier) ||
                tier < 0 ||
                tier > 5)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.artTiers.{art.Name}",
                    IssueSeverity.Error,
                    "spiritual art tier должен быть integer 0..5.",
                    code: "afterlife_combat_profile_invalid_art_tier",
                    section: "AfterlifeSpiritualConflict",
                    expected: "integer 0..5",
                    actual: art.Value.ValueKind == JsonValueKind.Number ? art.Value.GetRawText() : art.Value.ValueKind.ToString()));
            }
        }
    }

    private void ValidateAfterlifeSpiritualConflictUpdateContract(JsonElement root, string contextPrefix, List<ValidationIssue> issues)
    {
        if (!root.TryGetProperty(AfterlifeSpiritualConflictState.ResponseField, out var update))
            return;

        var context = $"{contextPrefix}.{AfterlifeSpiritualConflictState.ResponseField}";
        if (!RequireObject(update, context, issues))
            return;

        var mode = TryGetString(update, "mode");
        if (string.IsNullOrWhiteSpace(mode) || !AfterlifeSpiritualConflictState.Modes.Contains(mode))
        {
            issues.Add(new ValidationIssue(
                $"{context}.mode",
                IssueSeverity.Error,
                "afterlifeSpiritualConflictUpdate.mode должен быть одним из supported lifecycle modes.",
                code: "afterlife_conflict_update_invalid_mode",
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", AfterlifeSpiritualConflictState.Modes.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                actual: string.IsNullOrWhiteSpace(mode) ? "missing/empty" : mode));
            return;
        }

        if (ContainsProperty(update, "opponent") ||
            ContainsProperty(update, "playerStrain") ||
            ContainsProperty(update, "opponentStrain"))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "afterlife spiritual conflict использует side-vs-side schema; root opponent/playerStrain/opponentStrain запрещены.",
                code: "afterlife_conflict_update_legacy_fields",
                section: "AfterlifeSpiritualConflict",
                expected: "playerSide/oppositionSide plus playerSideStrain/oppositionSideStrain",
                actual: "legacy opponent/playerStrain/opponentStrain field present"));
        }

        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeStart, StringComparison.OrdinalIgnoreCase) &&
            !TryGetObject(update, "conflictState", out _) &&
            !TryGetObject(update, "activeConflict", out _) &&
            !TryGetObject(update, "conflictSeed", out _))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "start update должен содержать conflictSeed, conflictState или activeConflict object.",
                code: "afterlife_conflict_start_missing_conflict_state",
                section: "AfterlifeSpiritualConflict",
                expected: "conflictSeed/conflictState/activeConflict object",
                actual: "missing"));
        }

        if (string.Equals(mode, AfterlifeSpiritualConflictState.ModeExchange, StringComparison.OrdinalIgnoreCase) &&
            !TryGetObject(update, "exchange", out _))
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "exchange update должен содержать полный exchange object.",
                code: "afterlife_conflict_exchange_missing_payload",
                section: "AfterlifeSpiritualConflict",
                expected: "exchange object with exchangeId, operationType, outcome, before, and after",
                actual: "missing"));
        }
    }

    private void ValidateAfterlifeSpiritualConflictRoot(
        JsonObject root,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext)
    {
        if (root.ContainsKey(AfterlifeSpiritualConflictState.ResponseField))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{AfterlifeSpiritualConflictState.ResponseField}",
                IssueSeverity.Error,
                "afterlifeSpiritualConflictUpdate не должен храниться wrapper-полем в state file.",
                code: "afterlife_conflict_state_unprojected_update",
                section: "AfterlifeSpiritualConflict",
                expected: "activeConflict/recentConflicts canonical projection",
                actual: "raw response field in state file"));
        }

        if (root.ContainsKey("lastInvalidUpdate"))
        {
            issues.Add(new ValidationIssue(
                $"{context}.lastInvalidUpdate",
                IssueSeverity.Error,
                "Последний afterlifeSpiritualConflictUpdate не был применён из-за некорректной формы.",
                code: "afterlife_conflict_state_invalid_update",
                section: "AfterlifeSpiritualConflict",
                expected: "valid start/exchange/resolve/repair_cancel update",
                actual: AfterlifeSpiritualConflictState.GetNodeString(root["lastInvalidUpdateReason"]) ?? "invalid update",
                repairHint: "Исправь GM response surface afterlifeSpiritualConflictUpdate и повтори accepted-turn repair."));
        }

        if (root["schemaVersion"] is not JsonValue schema ||
            !schema.TryGetValue<int>(out var schemaVersion) ||
            schemaVersion <= 0)
        {
            issues.Add(new ValidationIssue(
                $"{context}.schemaVersion",
                IssueSeverity.Error,
                "afterlife spiritual conflict state должен иметь positive integer schemaVersion.",
                code: "afterlife_conflict_state_invalid_schema_version",
                section: "AfterlifeSpiritualConflict",
                expected: "positive integer",
                actual: root["schemaVersion"]?.ToJsonString() ?? "missing"));
        }

        if (root["recentConflicts"] is JsonArray recentConflicts)
        {
            for (var index = 0; index < recentConflicts.Count; index++)
            {
                if (recentConflicts[index] is JsonObject proof)
                    ValidateRecentConflictProof(proof, $"{context}.recentConflicts[{index}]", issues, diceContext);
            }
        }
        else
        {
            issues.Add(new ValidationIssue(
                $"{context}.recentConflicts",
                IssueSeverity.Error,
                "recentConflicts должен быть array.",
                code: "afterlife_conflict_state_invalid_recent_conflicts",
                section: "AfterlifeSpiritualConflict",
                expected: "array",
                actual: root["recentConflicts"]?.GetType().Name ?? "missing"));
        }

        if (root["activeConflict"] is JsonObject active)
            ValidateActiveAfterlifeConflict(active, $"{context}.activeConflict", issues, diceContext);
        else if (root.ContainsKey("activeConflict") && root["activeConflict"] != null)
            issues.Add(new ValidationIssue(
                $"{context}.activeConflict",
                IssueSeverity.Error,
                "activeConflict должен быть object или null.",
                code: "afterlife_conflict_state_invalid_active_conflict",
                section: "AfterlifeSpiritualConflict",
                expected: "object|null",
                actual: root["activeConflict"]?.GetType().Name ?? "missing"));
    }

    private void ValidateActiveAfterlifeConflict(
        JsonObject conflict,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext)
    {
        RequireNodeString(conflict, context, issues, "conflictId");
        var realm = RequireNodeString(conflict, context, issues, "realm");
        if (!string.IsNullOrWhiteSpace(realm) && !AfterlifeSpiritualConflictState.IsAfterlifeRealm(realm))
        {
            issues.Add(new ValidationIssue(
                $"{context}.realm",
                IssueSeverity.Error,
                "afterlife conflict realm должен быть Chaos Sea или Shining Abode.",
                code: "afterlife_conflict_invalid_realm",
                section: "AfterlifeSpiritualConflict",
                expected: "Chaos Sea or Shining Abode",
                actual: realm));
        }

        var sideModel = RequireNodeString(conflict, context, issues, "sideModel");
        if (!string.IsNullOrWhiteSpace(sideModel) && !AfterlifeSpiritualConflictState.SideModels.Contains(sideModel))
        {
            issues.Add(new ValidationIssue(
                $"{context}.sideModel",
                IssueSeverity.Error,
                "sideModel должен быть supported afterlife conflict model.",
                code: "afterlife_conflict_invalid_side_model",
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", AfterlifeSpiritualConflictState.SideModels.OrderBy(value => value, StringComparer.OrdinalIgnoreCase)),
                actual: sideModel));
        }

        ValidateSide(conflict["playerSide"] as JsonObject, $"{context}.playerSide", issues, allowPlayerLead: true);
        ValidateSide(conflict["oppositionSide"] as JsonObject, $"{context}.oppositionSide", issues, allowPlayerLead: false);
        ValidateEnumNode(conflict, context, issues, "playerSideStrain", AfterlifeSpiritualConflictState.StrainStates, "afterlife_conflict_invalid_player_side_strain");
        ValidateEnumNode(conflict, context, issues, "oppositionSideStrain", AfterlifeSpiritualConflictState.StrainStates, "afterlife_conflict_invalid_opposition_side_strain");
        ValidateEnumNode(conflict, context, issues, "conflictPosition", AfterlifeSpiritualConflictState.ConflictPositions, "afterlife_conflict_invalid_position");
        var resolutionState = ValidateEnumNode(conflict, context, issues, "resolutionState", AfterlifeSpiritualConflictState.ResolutionStates, "afterlife_conflict_invalid_resolution_state");
        if (string.Equals(resolutionState, "resolved", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(resolutionState, "repair_cancelled", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue(
                $"{context}.resolutionState",
                IssueSeverity.Error,
                "Terminal resolutionState не может оставаться под activeConflict.",
                code: "afterlife_conflict_terminal_active_conflict",
                section: "AfterlifeSpiritualConflict",
                expected: "active/concession_pending/surrender_pending/retreat_pending/ready_to_resolve for activeConflict",
                actual: resolutionState,
                repairHint: "Закрывай terminal конфликт через afterlifeSpiritualConflictUpdate.mode=resolve или repair_cancel: activeConflict должен стать null, а resolved proof должен быть перенесён в recentConflicts[]."));
        }

        foreach (var legacy in new[] { "opponent", "playerStrain", "opponentStrain" })
        {
            if (conflict.ContainsKey(legacy))
            {
                issues.Add(new ValidationIssue(
                    $"{context}.{legacy}",
                    IssueSeverity.Error,
                    "Legacy one-vs-one conflict field запрещён; используйте side-vs-side schema.",
                    code: "afterlife_conflict_legacy_field",
                    section: "AfterlifeSpiritualConflict",
                    expected: "playerSide/oppositionSide and side strain fields",
                    actual: legacy));
            }
        }

        if (conflict["exchangeLog"] is JsonArray exchangeLog)
        {
            for (var index = 0; index < exchangeLog.Count; index++)
            {
                if (exchangeLog[index] is JsonObject exchange)
                    ValidateConflictExchange(exchange, $"{context}.exchangeLog[{index}]", issues, diceContext);
                else
                    issues.Add(new ValidationIssue(
                        $"{context}.exchangeLog[{index}]",
                        IssueSeverity.Error,
                        "exchangeLog[] item должен быть object.",
                        code: "afterlife_conflict_invalid_exchange_item",
                        section: "AfterlifeSpiritualConflict"));
            }
        }
        else if (conflict.ContainsKey("exchangeLog") && conflict["exchangeLog"] != null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.exchangeLog",
                IssueSeverity.Error,
                "exchangeLog должен быть array.",
                code: "afterlife_conflict_invalid_exchange_log",
                section: "AfterlifeSpiritualConflict"));
        }
    }

    private void ValidateSide(JsonObject? side, string context, List<ValidationIssue> issues, bool allowPlayerLead)
    {
        if (side == null)
        {
            issues.Add(new ValidationIssue(
                context,
                IssueSeverity.Error,
                "Conflict side должен быть object с leadContestant.",
                code: "afterlife_conflict_missing_side",
                section: "AfterlifeSpiritualConflict",
                expected: "side object with leadContestant",
                actual: "missing/non-object"));
            return;
        }

        if (side["leadContestant"] is not JsonObject lead)
        {
            issues.Add(new ValidationIssue(
                $"{context}.leadContestant",
                IssueSeverity.Error,
                "Каждая сторона должна иметь ровно одного leadContestant.",
                code: "afterlife_conflict_missing_lead_contestant",
                section: "AfterlifeSpiritualConflict",
                expected: "leadContestant object",
                actual: side["leadContestant"]?.GetType().Name ?? "missing"));
            return;
        }

        var actorType = RequireNodeString(lead, $"{context}.leadContestant", issues, "actorType");
        RequireNodeString(lead, $"{context}.leadContestant", issues, "actorId");
        RequireNodeString(lead, $"{context}.leadContestant", issues, "displayName");

        var isPlayerLead = string.Equals(actorType, "player", StringComparison.OrdinalIgnoreCase) ||
                           string.Equals(actorType, "soul", StringComparison.OrdinalIgnoreCase);
        if (isPlayerLead && !allowPlayerLead)
        {
            issues.Add(new ValidationIssue(
                $"{context}.leadContestant.actorType",
                IssueSeverity.Error,
                "oppositionSide не может иметь player/soul leadContestant.",
                code: "afterlife_conflict_opposition_player_lead",
                section: "AfterlifeSpiritualConflict",
                expected: "guardian/resident/radiant_actor/custom_afterlife_actor",
                actual: actorType));
        }

        if (!isPlayerLead)
        {
            if (lead["actorArtTierSnapshot"] is not JsonObject)
            {
                issues.Add(new ValidationIssue(
                    $"{context}.leadContestant.actorArtTierSnapshot",
                    IssueSeverity.Error,
                    "Non-player leadContestant должен иметь actorArtTierSnapshot.",
                    code: "afterlife_conflict_missing_actor_art_snapshot",
                    section: "AfterlifeSpiritualConflict",
                    expected: "object with resolved spiritual art tiers",
                    actual: lead["actorArtTierSnapshot"]?.GetType().Name ?? "missing"));
            }

            RequireNodeString(lead, $"{context}.leadContestant", issues, "artAuthoritySource");
        }

        if (side["supporters"] is JsonArray supporters)
        {
            for (var index = 0; index < supporters.Count; index++)
            {
                if (supporters[index] is not JsonObject supporter)
                {
                    issues.Add(new ValidationIssue(
                        $"{context}.supporters[{index}]",
                        IssueSeverity.Error,
                        "supporters[] item должен быть object.",
                        code: "afterlife_conflict_invalid_supporter_item",
                        section: "AfterlifeSpiritualConflict",
                        expected: "supporter object with actorType, actorId, supportRole",
                        actual: supporters[index]?.GetType().Name ?? "null"));
                    continue;
                }

                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "actorType");
                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "actorId");
                RequireNodeString(supporter, $"{context}.supporters[{index}]", issues, "supportRole");
            }
        }
        else if (side.ContainsKey("supporters") && side["supporters"] != null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.supporters",
                IssueSeverity.Error,
                "supporters должен быть array.",
                code: "afterlife_conflict_invalid_supporters",
                section: "AfterlifeSpiritualConflict"));
        }
    }

    private void ValidateRecentConflictProof(
        JsonObject proof,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext)
    {
        var diceRequired = ResolveDiceAuditRequired(proof);
        if (diceRequired && proof["diceAudit"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.diceAudit",
                IssueSeverity.Error,
                "Contested afterlife conflict resolution требует diceAudit.",
                code: "afterlife_conflict_resolution_missing_dice_audit",
                section: "AfterlifeSpiritualConflict",
                expected: "resolution.diceAudit with current turn preGeneratedDices1d20 source indices",
                actual: proof["diceAudit"]?.GetType().Name ?? "missing"));
            return;
        }

        if (proof["diceAudit"] is JsonObject diceAudit)
        {
            ValidateAfterlifeConflictDiceAudit(diceAudit, $"{context}.diceAudit", issues, diceContext);
            ValidateLightIncarnateDiceAuditModifier(proof, diceAudit, $"{context}.diceAudit", issues, diceContext);
        }
    }

    private void ValidateConflictExchange(
        JsonObject exchange,
        string context,
        List<ValidationIssue> issues,
        AfterlifeConflictDiceContext diceContext)
    {
        RequireNodeString(exchange, context, issues, "exchangeId");
        var operationType = ValidateEnumNode(exchange, context, issues, "operationType", AfterlifeSpiritualConflictState.OperationTypes, "afterlife_conflict_invalid_operation_type");
        var outcome = ValidateEnumNode(exchange, context, issues, "outcome", AfterlifeSpiritualConflictState.OperationOutcomes, "afterlife_conflict_invalid_operation_outcome");

        if (string.Equals(outcome, "blocked", StringComparison.OrdinalIgnoreCase) &&
            exchange["incomingAction"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.incomingAction",
                IssueSeverity.Error,
                "blocked exchange должен явно описывать incomingAction, который был предотвращён.",
                code: "afterlife_conflict_blocked_missing_incoming_action",
                section: "AfterlifeSpiritualConflict",
                expected: "incomingAction object",
                actual: exchange["incomingAction"]?.GetType().Name ?? "missing"));
        }

        if (string.Equals(outcome, "countered", StringComparison.OrdinalIgnoreCase) &&
            exchange["incomingAction"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.incomingAction",
                IssueSeverity.Error,
                "countered exchange должен явно описывать incomingAction, который был отражён.",
                code: "afterlife_conflict_countered_missing_incoming_action",
                section: "AfterlifeSpiritualConflict",
                expected: "incomingAction object",
                actual: exchange["incomingAction"]?.GetType().Name ?? "missing"));
        }

        var before = exchange["before"] as JsonObject;
        if (before == null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.before",
                IssueSeverity.Error,
                "exchange.before должен быть audit snapshot object.",
                code: "afterlife_conflict_exchange_missing_before",
                section: "AfterlifeSpiritualConflict",
                expected: "before object",
                actual: exchange["before"]?.GetType().Name ?? "missing"));
        }

        var after = exchange["after"] as JsonObject;
        if (after == null)
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "exchange.after должен быть audit snapshot object.",
                code: "afterlife_conflict_exchange_missing_after",
                section: "AfterlifeSpiritualConflict",
                expected: "after object",
                actual: exchange["after"]?.GetType().Name ?? "missing"));
        }

        if (before != null &&
            after != null &&
            string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase) &&
            !JsonNode.DeepEquals(before, after))
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "no_effect exchange не должен менять before/after audit snapshot.",
                code: "afterlife_conflict_no_effect_has_state_delta",
                section: "AfterlifeSpiritualConflict",
                expected: "before == after for outcome=no_effect",
                actual: "before != after"));
        }

        if (!string.Equals(outcome, "blocked", StringComparison.OrdinalIgnoreCase) &&
            !string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase) &&
            before != null &&
            after != null &&
            JsonNode.DeepEquals(before, after))
        {
            issues.Add(new ValidationIssue(
                $"{context}.after",
                IssueSeverity.Error,
                "Non-blocked exchange должен иметь измеримое изменение или явно быть outcome=no_effect.",
                code: "afterlife_conflict_exchange_no_state_delta",
                section: "AfterlifeSpiritualConflict",
                expected: "changed before/after or outcome=blocked/no_effect",
                actual: "before == after"));
        }

        if (before != null && after != null)
            ValidateSpiritualArtOperationRules(exchange, before, after, operationType, outcome, context, issues);

        var diceRequired = ExchangeDiceAuditRequired(exchange, outcome);
        if (diceRequired && exchange["diceAudit"] is not JsonObject)
        {
            issues.Add(new ValidationIssue(
                $"{context}.diceAudit",
                IssueSeverity.Error,
                "Contested afterlife conflict exchange требует diceAudit.",
                code: "afterlife_conflict_exchange_missing_dice_audit",
                section: "AfterlifeSpiritualConflict",
                expected: "exchange.diceAudit with current turn preGeneratedDices1d20 source indices",
                actual: exchange["diceAudit"]?.GetType().Name ?? "missing"));
        }

        if (exchange["diceAudit"] is JsonObject diceAudit)
        {
            ValidateAfterlifeConflictDiceAudit(diceAudit, $"{context}.diceAudit", issues, diceContext);
            ValidateLightIncarnateDiceAuditModifier(exchange, diceAudit, $"{context}.diceAudit", issues, diceContext);
        }
    }

    private static void ValidateSpiritualArtOperationRules(
        JsonObject exchange,
        JsonObject before,
        JsonObject after,
        string? operationType,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(operationType))
            return;

        if (ConflictTokenEquals(operationType, "counter") &&
            exchange["incomingAction"] is not JsonObject)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.incomingAction",
                "Контрприём (counter) является реакцией и должен указывать incomingAction.",
                "afterlife_conflict_counter_missing_incoming_action",
                "incomingAction object that names the operation being countered",
                exchange["incomingAction"]?.GetType().Name ?? "missing");
        }

        if (ConflictTokenEquals(operationType, "maneuver"))
            ValidateManeuverRule(before, after, outcome, context, issues);

        if (ConflictTokenEquals(operationType, "binding", "force_binding") &&
            IsSuccessfulArtOutcome(outcome) &&
            !HasBindingLeverage(exchange, before))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Наложение оков (binding/force_binding) требует преимущества, setup или decisive_player_success.",
                "afterlife_conflict_binding_without_leverage",
                "before.conflictPosition=player_advantaged|player_dominant, setup=true, or diceAudit.outcomeBand=decisive_player_success",
                DescribeConflictPosition(before));
        }

        if (ConflictTokenEquals(operationType, "break_binding") &&
            !HasBindingOrCoerciveContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Разрыв оков (break_binding) должен ссылаться на binding/coercive context.",
                "afterlife_conflict_break_binding_without_binding",
                "incomingAction or before/after snapshot with binding/forced handoff context",
                "missing binding/coercive context");
        }

        if (ConflictTokenEquals(operationType, "incarnation_resistance") &&
            !HasForcedIncarnationContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Сопротивление воплощению (incarnation_resistance) применимо только против force_incarnation/guardian_forced.",
                "afterlife_conflict_incarnation_resistance_without_force",
                "force_incarnation incomingAction/resolution/source context",
                "missing forced-incarnation context");
        }

        if (ConflictTokenEquals(operationType, "champion_coordination") &&
            !HasChampionDuelContext(exchange, before, after))
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.operationType",
                "Координация чемпиона (champion_coordination) применима только в champion_duel.",
                "afterlife_conflict_champion_coordination_without_champion",
                "sideModel/conflictMode=champion_duel",
                "missing champion_duel context");
        }
    }

    private static void ValidateManeuverRule(
        JsonObject before,
        JsonObject after,
        string? outcome,
        string context,
        List<ValidationIssue> issues)
    {
        if (TryGetStrainRank(before["playerSideStrain"], out var beforePlayerStrain) &&
            TryGetStrainRank(after["playerSideStrain"], out var afterPlayerStrain) &&
            beforePlayerStrain != afterPlayerStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.playerSideStrain",
                "Манёвр (maneuver) меняет позицию, а не side strain.",
                "afterlife_conflict_maneuver_changes_strain",
                "playerSideStrain unchanged for maneuver",
                $"{beforePlayerStrain}->{afterPlayerStrain}");
        }

        if (TryGetStrainRank(before["oppositionSideStrain"], out var beforeOppositionStrain) &&
            TryGetStrainRank(after["oppositionSideStrain"], out var afterOppositionStrain) &&
            beforeOppositionStrain != afterOppositionStrain)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.oppositionSideStrain",
                "Манёвр (maneuver) меняет позицию, а не side strain.",
                "afterlife_conflict_maneuver_changes_strain",
                "oppositionSideStrain unchanged for maneuver",
                $"{beforeOppositionStrain}->{afterOppositionStrain}");
        }

        if (IsSuccessfulArtOutcome(outcome) &&
            TryGetPositionRank(before["conflictPosition"], out var beforePosition) &&
            TryGetPositionRank(after["conflictPosition"], out var afterPosition) &&
            beforePosition == afterPosition)
        {
            AddSpiritualArtRuleIssue(
                issues,
                $"{context}.after.conflictPosition",
                "Успешный манёвр (maneuver) должен измеримо менять conflictPosition.",
                "afterlife_conflict_maneuver_missing_position_shift",
                "conflictPosition changed on success/partial_success",
                "unchanged");
        }
    }

    private static bool HasBindingLeverage(JsonObject exchange, JsonObject before)
    {
        if (TryGetPositionRank(before["conflictPosition"], out var position) && position >= 1)
            return true;

        if (TryGetJsonNodeBool(exchange["setup"], out var setup) && setup)
            return true;

        if (string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["setupState"]), "ready", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(exchange["bindingSetup"]), "ready", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (exchange["diceAudit"] is JsonObject diceAudit &&
            string.Equals(AfterlifeSpiritualConflictState.GetNodeString(diceAudit["outcomeBand"]), "decisive_player_success", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }

    private static bool HasBindingOrCoerciveContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "binding", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "force_binding", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "force_incarnation", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "guardian_forced", "source", "reason", "consequence") ||
            root.ContainsKey("bindingState") ||
            root.ContainsKey("bindingId") ||
            root.ContainsKey("activeBinding") ||
            root.ContainsKey("forcedHandoff") ||
            root.ContainsKey("forceIncarnation") ||
            root.ContainsKey("forcedIncarnation") ||
            root["incomingAction"] is JsonObject incoming && HasBindingOrCoerciveContext(incoming));

    private static bool HasForcedIncarnationContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "force_incarnation", "operationType", "finalOperationType") ||
            ConflictNodeStringEquals(root, "guardian_forced", "source", "reason", "consequence") ||
            root.ContainsKey("forceIncarnation") ||
            root.ContainsKey("forcedIncarnation") ||
            root["incomingAction"] is JsonObject incoming && HasForcedIncarnationContext(incoming));

    private static bool HasChampionDuelContext(params JsonObject[] roots) =>
        roots.Any(root =>
            ConflictNodeStringEquals(root, "champion_duel", "sideModel", "conflictMode", "conflictModel", "duelType") ||
            root["playerSide"]?["leadContestant"] is JsonObject lead &&
            !ConflictTokenEquals(AfterlifeSpiritualConflictState.GetNodeString(lead["actorType"]), "player", "soul"));

    private static bool IsSuccessfulArtOutcome(string? outcome) =>
        ConflictTokenEquals(outcome, "success", "partial_success");

    private static bool TryGetPositionRank(JsonNode? node, out int rank)
    {
        rank = 0;
        var value = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        rank = value.Trim().ToLowerInvariant() switch
        {
            "opposition_dominant" => -2,
            "opposition_advantaged" => -1,
            "contested" => 0,
            "player_advantaged" => 1,
            "player_dominant" => 2,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.ConflictPositions.Contains(value);
    }

    private static bool TryGetStrainRank(JsonNode? node, out int rank)
    {
        rank = 0;
        var value = AfterlifeSpiritualConflictState.GetNodeString(node);
        if (string.IsNullOrWhiteSpace(value))
            return false;

        rank = value.Trim().ToLowerInvariant() switch
        {
            "clear" => 0,
            "strained" => 1,
            "fractured" => 2,
            "overwhelmed" => 3,
            "broken" => 4,
            _ => 0
        };
        return AfterlifeSpiritualConflictState.StrainStates.Contains(value);
    }

    private static string DescribeConflictPosition(JsonObject root) =>
        AfterlifeSpiritualConflictState.GetNodeString(root["conflictPosition"]) ?? "missing";

    private static void AddSpiritualArtRuleIssue(
        List<ValidationIssue> issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static bool ExchangeDiceAuditRequired(JsonObject exchange, string? outcome)
    {
        if (string.Equals(outcome, "no_effect", StringComparison.OrdinalIgnoreCase))
            return false;

        if (IsExplicitVoluntaryNonContest(exchange))
            return false;

        return !string.IsNullOrWhiteSpace(outcome);
    }

    private static bool ResolveDiceAuditRequired(JsonObject proof)
    {
        if (IsExplicitVoluntaryNonContest(proof))
            return false;

        return ConflictNodeStringEquals(proof, "force_incarnation", "operationType", "finalOperationType");
    }

    private static bool IsExplicitVoluntaryNonContest(JsonObject root)
    {
        var operationType = AfterlifeSpiritualConflictState.GetNodeString(root["operationType"]) ??
                            AfterlifeSpiritualConflictState.GetNodeString(root["finalOperationType"]);
        var isVoluntaryOperation =
            ConflictTokenEquals(operationType, "surrender", "withdraw", "negotiate") ||
            ConflictNodeContainsAnyToken(root, new[] { "playerOutcome", "resolutionKind", "outcome", "result" },
                "voluntary_surrender",
                "voluntary_concession",
                "voluntary_withdrawal",
                "voluntary_withdraw",
                "consented");

        if (!isVoluntaryOperation)
            return false;

        return TryGetJsonNodeBool(root["voluntary"], out var voluntary) && voluntary ||
               TryGetJsonNodeBool(root["isVoluntary"], out var isVoluntary) && isVoluntary ||
               ConflictNodeStringEquals(root, "voluntary_player_choice", "resolutionSource", "source");
    }

    private static bool ValidateAfterlifeConflictDiceAudit(
        JsonObject audit,
        string context,
        List<ValidationIssue>? issues,
        AfterlifeConflictDiceContext diceContext)
    {
        var valid = true;

        var formulaVersion = AfterlifeSpiritualConflictState.GetNodeString(audit["formulaVersion"]);
        if (!string.Equals(formulaVersion, "afterlife_spiritual_conflict_v1", StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.formulaVersion",
                "diceAudit.formulaVersion должен быть afterlife_spiritual_conflict_v1.",
                "afterlife_conflict_dice_invalid_formula_version",
                "afterlife_spiritual_conflict_v1",
                string.IsNullOrWhiteSpace(formulaVersion) ? "missing" : formulaVersion);
            valid = false;
        }

        var diceSource = AfterlifeSpiritualConflictState.GetNodeString(audit["diceSource"]);
        if (!string.Equals(diceSource, "input/turn_request.json.preGeneratedDices1d20", StringComparison.Ordinal))
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceSource",
                "diceAudit должен использовать visible turn_request preGeneratedDices1d20.",
                "afterlife_conflict_dice_invalid_source",
                "input/turn_request.json.preGeneratedDices1d20",
                string.IsNullOrWhiteSpace(diceSource) ? "missing" : diceSource);
            valid = false;
        }

        if (audit["diceUsed"] is not JsonArray diceUsed || diceUsed.Count < 2)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit.diceUsed должен содержать player и opposition d20 entries.",
                "afterlife_conflict_dice_missing_used",
                "array with player/opposition dice entries",
                audit["diceUsed"]?.GetType().Name ?? "missing");
            return false;
        }

        int? playerDie = null;
        int? oppositionDie = null;
        var usedSourceIndices = new HashSet<int>();
        for (var index = 0; index < diceUsed.Count; index++)
        {
            if (diceUsed[index] is not JsonObject dieEntry)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}]",
                    "diceUsed[] item должен быть object.",
                    "afterlife_conflict_dice_invalid_used_entry",
                    "object",
                    diceUsed[index]?.GetType().Name ?? "null");
                valid = false;
                continue;
            }

            var side = AfterlifeSpiritualConflictState.GetNodeString(dieEntry["side"]);
            if (!TryGetJsonNodeInt(dieEntry["sourceIndex"], out var sourceIndex) || sourceIndex < 0)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sourceIndex",
                    "diceUsed.sourceIndex должен быть non-negative integer.",
                    "afterlife_conflict_dice_invalid_source_index",
                    "0..19",
                    dieEntry["sourceIndex"]?.ToJsonString() ?? "missing");
                valid = false;
            }
            else if (!usedSourceIndices.Add(sourceIndex))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sourceIndex",
                    "diceUsed.sourceIndex не должен повторяться внутри одного diceAudit.",
                    "afterlife_conflict_dice_duplicate_source_index",
                    "unique sourceIndex values",
                    sourceIndex.ToString());
                valid = false;
            }

            if (!TryGetJsonNodeInt(dieEntry["sides"], out var sides) || sides != 20)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].sides",
                    "afterlife conflict diceAudit поддерживает только d20.",
                    "afterlife_conflict_dice_invalid_sides",
                    "20",
                    dieEntry["sides"]?.ToJsonString() ?? "missing");
                valid = false;
            }

            if (!TryGetJsonNodeInt(dieEntry["value"], out var value) || value < 1 || value > 20)
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].value",
                    "diceUsed.value должен быть d20 value в диапазоне 1..20.",
                    "afterlife_conflict_dice_invalid_value",
                    "1..20",
                    dieEntry["value"]?.ToJsonString() ?? "missing");
                valid = false;
                continue;
            }

            if (diceContext.AuthoritativeDice is { Length: > 0 } authoritativeDice &&
                sourceIndex >= 0)
            {
                if (sourceIndex >= authoritativeDice.Length)
                {
                    AddDiceAuditIssue(
                        issues,
                        $"{context}.diceUsed[{index}].sourceIndex",
                        "diceUsed.sourceIndex отсутствует в authoritative preGeneratedDices1d20.",
                        "afterlife_conflict_dice_source_index_not_authorized",
                        $"0..{authoritativeDice.Length - 1}",
                        sourceIndex.ToString());
                    valid = false;
                }
                else if (authoritativeDice[sourceIndex] != value)
                {
                    AddDiceAuditIssue(
                        issues,
                        $"{context}.diceUsed[{index}].value",
                        "diceUsed.value должен совпадать с authoritative preGeneratedDices1d20[sourceIndex].",
                        "afterlife_conflict_dice_value_not_authorized",
                        authoritativeDice[sourceIndex].ToString(),
                        value.ToString());
                    valid = false;
                }
            }

            if (ConflictTokenEquals(side, "player", "playerSide", "soul"))
                playerDie = value;
            else if (ConflictTokenEquals(side, "opposition", "oppositionSide", "guardian"))
                oppositionDie = value;
            else
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}.diceUsed[{index}].side",
                    "diceUsed.side должен быть player или opposition.",
                    "afterlife_conflict_dice_invalid_side",
                    "player/opposition",
                    string.IsNullOrWhiteSpace(side) ? "missing" : side);
                valid = false;
            }
        }

        if (playerDie == null)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit должен включать player-side die.",
                "afterlife_conflict_dice_missing_player_die",
                "diceUsed side=player",
                "missing");
            valid = false;
        }

        if (oppositionDie == null)
        {
            AddDiceAuditIssue(
                issues,
                $"{context}.diceUsed",
                "diceAudit должен включать opposition-side die.",
                "afterlife_conflict_dice_missing_opposition_die",
                "diceUsed side=opposition",
                "missing");
            valid = false;
        }

        var playerModifier = SumDiceAuditModifiers(audit, "player", $"{context}.modifierBreakdown.player", issues, ref valid);
        var oppositionModifier = SumDiceAuditModifiers(audit, "opposition", $"{context}.modifierBreakdown.opposition", issues, ref valid);

        if (!TryGetJsonNodeInt(audit["playerTotal"], out var playerTotal))
        {
            AddDiceAuditIssue(issues, $"{context}.playerTotal", "diceAudit.playerTotal должен быть integer.", "afterlife_conflict_dice_missing_player_total", "integer", audit["playerTotal"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (playerDie != null && playerTotal != playerDie.Value + playerModifier)
        {
            AddDiceAuditIssue(issues, $"{context}.playerTotal", "diceAudit.playerTotal должен равняться player die + modifierBreakdown.player.", "afterlife_conflict_dice_player_total_mismatch", (playerDie.Value + playerModifier).ToString(), playerTotal.ToString());
            valid = false;
        }

        if (!TryGetJsonNodeInt(audit["oppositionTotal"], out var oppositionTotal))
        {
            AddDiceAuditIssue(issues, $"{context}.oppositionTotal", "diceAudit.oppositionTotal должен быть integer.", "afterlife_conflict_dice_missing_opposition_total", "integer", audit["oppositionTotal"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (oppositionDie != null && oppositionTotal != oppositionDie.Value + oppositionModifier)
        {
            AddDiceAuditIssue(issues, $"{context}.oppositionTotal", "diceAudit.oppositionTotal должен равняться opposition die + modifierBreakdown.opposition.", "afterlife_conflict_dice_opposition_total_mismatch", (oppositionDie.Value + oppositionModifier).ToString(), oppositionTotal.ToString());
            valid = false;
        }

        if (!TryGetJsonNodeInt(audit["margin"], out var margin))
        {
            AddDiceAuditIssue(issues, $"{context}.margin", "diceAudit.margin должен быть integer.", "afterlife_conflict_dice_missing_margin", "playerTotal - oppositionTotal", audit["margin"]?.ToJsonString() ?? "missing");
            valid = false;
        }
        else if (TryGetJsonNodeInt(audit["playerTotal"], out playerTotal) &&
                 TryGetJsonNodeInt(audit["oppositionTotal"], out oppositionTotal) &&
                 margin != playerTotal - oppositionTotal)
        {
            AddDiceAuditIssue(issues, $"{context}.margin", "diceAudit.margin должен равняться playerTotal - oppositionTotal.", "afterlife_conflict_dice_margin_mismatch", (playerTotal - oppositionTotal).ToString(), margin.ToString());
            valid = false;
        }

        var outcomeBand = AfterlifeSpiritualConflictState.GetNodeString(audit["outcomeBand"]);
        if (TryGetJsonNodeInt(audit["margin"], out margin))
        {
            var expectedBand = ExpectedAfterlifeConflictOutcomeBand(margin);
            if (!string.Equals(outcomeBand, expectedBand, StringComparison.Ordinal))
            {
                AddDiceAuditIssue(issues, $"{context}.outcomeBand", "diceAudit.outcomeBand должен соответствовать margin.", "afterlife_conflict_dice_outcome_band_mismatch", expectedBand, string.IsNullOrWhiteSpace(outcomeBand) ? "missing" : outcomeBand);
                valid = false;
            }
        }

        return valid;
    }

    private static int SumDiceAuditModifiers(
        JsonObject audit,
        string side,
        string context,
        List<ValidationIssue>? issues,
        ref bool valid)
    {
        if (audit["modifierBreakdown"] is not JsonObject modifierBreakdown ||
            modifierBreakdown[side] is not JsonArray modifiers)
        {
            AddDiceAuditIssue(
                issues,
                context,
                "diceAudit.modifierBreakdown должен содержать player/opposition arrays.",
                "afterlife_conflict_dice_missing_modifier_breakdown",
                "modifierBreakdown.player[] and modifierBreakdown.opposition[]",
                audit["modifierBreakdown"]?.GetType().Name ?? "missing");
            valid = false;
            return 0;
        }

        var total = 0;
        for (var index = 0; index < modifiers.Count; index++)
        {
            if (modifiers[index] is not JsonObject modifier ||
                !TryGetJsonNodeInt(modifier["value"], out var value))
            {
                AddDiceAuditIssue(
                    issues,
                    $"{context}[{index}].value",
                    "modifierBreakdown item должен иметь integer value.",
                    "afterlife_conflict_dice_invalid_modifier",
                    "integer value",
                    modifiers[index]?.ToJsonString() ?? "missing");
                valid = false;
                continue;
            }

            total += value;
        }

        return total;
    }

    private static string ExpectedAfterlifeConflictOutcomeBand(int margin) =>
        margin >= 8 ? "decisive_player_success" :
        margin >= 3 ? "player_success" :
        margin >= -2 ? "mixed_or_no_effect" :
        margin >= -7 ? "opposition_success" :
        "decisive_opposition_success";

    private static void AddDiceAuditIssue(
        List<ValidationIssue>? issues,
        string path,
        string message,
        string code,
        string expected,
        string actual)
    {
        issues?.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            message,
            code: code,
            section: "AfterlifeSpiritualConflict",
            expected: expected,
            actual: actual));
    }

    private static bool ConflictNodeStringEquals(JsonObject root, string expected, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            if (string.Equals(AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]), expected, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }

    private static bool ConflictNodeContainsAnyToken(JsonObject root, IEnumerable<string> propertyNames, params string[] acceptedTokens)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
            if (ConflictTokenEquals(value, acceptedTokens))
                return true;
        }

        return false;
    }

    private static bool ConflictTokenEquals(string? value, params string[] acceptedTokens)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        return acceptedTokens.Any(token => string.Equals(value, token, StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryGetJsonNodeInt(JsonNode? node, out int value)
    {
        value = 0;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<int>(out value);
    }

    private static bool TryGetJsonNodeBool(JsonNode? node, out bool value)
    {
        value = false;
        return node is JsonValue jsonValue && jsonValue.TryGetValue<bool>(out value);
    }

    private string? ValidateEnumNode(
        JsonObject root,
        string context,
        List<ValidationIssue> issues,
        string propertyName,
        HashSet<string> allowedValues,
        string code)
    {
        var value = RequireNodeString(root, context, issues, propertyName);
        if (string.IsNullOrWhiteSpace(value))
            return value;

        if (!allowedValues.Contains(value))
        {
            issues.Add(new ValidationIssue(
                $"{context}.{propertyName}",
                IssueSeverity.Error,
                $"{propertyName} содержит unsupported afterlife spiritual conflict value.",
                code: code,
                section: "AfterlifeSpiritualConflict",
                expected: string.Join("/", allowedValues.OrderBy(item => item, StringComparer.OrdinalIgnoreCase)),
                actual: value));
        }

        return value;
    }

    private static string? RequireNodeString(JsonObject root, string context, List<ValidationIssue> issues, string propertyName)
    {
        var value = AfterlifeSpiritualConflictState.GetNodeString(root[propertyName]);
        if (!string.IsNullOrWhiteSpace(value))
            return value;

        issues.Add(new ValidationIssue(
            $"{context}.{propertyName}",
            IssueSeverity.Error,
            $"{propertyName} должен быть non-empty string.",
            code: "afterlife_conflict_missing_required_string",
            section: "AfterlifeSpiritualConflict",
            expected: "non-empty string",
            actual: root.ContainsKey(propertyName) ? root[propertyName]?.ToJsonString() ?? "null" : "missing"));
        return null;
    }

    private static bool ContainsProperty(JsonElement root, string propertyName) =>
        root.ValueKind == JsonValueKind.Object && root.TryGetProperty(propertyName, out _);

    private static bool TryGetObject(JsonElement root, string propertyName, out JsonElement value)
    {
        value = default;
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out value) &&
               value.ValueKind == JsonValueKind.Object;
    }

    private static string? TryGetString(JsonElement root, string propertyName)
    {
        return root.ValueKind == JsonValueKind.Object &&
               root.TryGetProperty(propertyName, out var value) &&
               value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }
}
