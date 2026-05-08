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

        await ValidateActiveConflictRemovalHasTerminalProofAsync(root, issues);
        ValidateAfterlifeSpiritualConflictRoot(root, AfterlifeSpiritualConflictState.StatePath, issues);

        var gateContext = await ResolveAfterlifeSpiritualConflictGateContextAsync();
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

    private void ValidateAfterlifeSpiritualConflictRoot(JsonObject root, string context, List<ValidationIssue> issues)
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

        if (root["recentConflicts"] is not JsonArray)
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
            ValidateActiveAfterlifeConflict(active, $"{context}.activeConflict", issues);
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

    private void ValidateActiveAfterlifeConflict(JsonObject conflict, string context, List<ValidationIssue> issues)
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
                    ValidateConflictExchange(exchange, $"{context}.exchangeLog[{index}]", issues);
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

    private void ValidateConflictExchange(JsonObject exchange, string context, List<ValidationIssue> issues)
    {
        RequireNodeString(exchange, context, issues, "exchangeId");
        ValidateEnumNode(exchange, context, issues, "operationType", AfterlifeSpiritualConflictState.OperationTypes, "afterlife_conflict_invalid_operation_type");
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
