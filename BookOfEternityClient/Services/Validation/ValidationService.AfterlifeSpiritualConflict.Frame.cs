using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    internal sealed record SpiritualConflictFileImage
    {
        internal SpiritualConflictFileImage(bool exists, string? text)
        {
            if (!exists && text is not null)
                throw new ArgumentException("An absent image cannot contain text.", nameof(text));
            Exists = exists;
            Text = text;
        }

        internal bool Exists { get; }
        internal string? Text { get; }
    }

    internal sealed record SpiritualConflictCandidateImages(
        SpiritualConflictFileImage Conflict,
        SpiritualConflictFileImage Soul,
        SpiritualConflictFileImage Shining,
        SpiritualConflictFileImage? Profiles);

    // Values are the existing authenticated reader's result, not raw file-existence
    // claims. That reader intentionally returns null for unavailable/blank input.
    internal sealed record SpiritualConflictBaselineImages(
        string? Conflict,
        string? Soul,
        string? Shining,
        string? Profiles,
        string? ResourceDefinitions,
        string? ResourceState,
        bool ResourceAcquisitionFailed);

    internal sealed class SpiritualConflictValidationFrame
    {
        private readonly int[]? _snapshotDice;

        private SpiritualConflictValidationFrame(
            SpiritualConflictCandidateImages candidate,
            SpiritualConflictBaselineImages baseline,
            bool hasValidatedSnapshot,
            int? snapshotTurnNumber,
            int[]? snapshotDice,
            SpiritualConflictFileImage difficultySettings,
            SpiritualConflictFileImage? turnRequest)
        {
            ArgumentNullException.ThrowIfNull(candidate);
            ArgumentNullException.ThrowIfNull(candidate.Conflict);
            ArgumentNullException.ThrowIfNull(candidate.Soul);
            ArgumentNullException.ThrowIfNull(candidate.Shining);
            ArgumentNullException.ThrowIfNull(baseline);
            ArgumentNullException.ThrowIfNull(difficultySettings);
            Candidate = candidate;
            Baseline = baseline;
            HasValidatedSnapshot = hasValidatedSnapshot;
            SnapshotTurnNumber = snapshotTurnNumber;
            _snapshotDice = snapshotDice?.ToArray();
            DifficultySettings = difficultySettings;
            TurnRequest = turnRequest;
        }

        internal SpiritualConflictCandidateImages Candidate { get; }
        internal SpiritualConflictBaselineImages Baseline { get; }
        internal bool HasValidatedSnapshot { get; }
        internal int? SnapshotTurnNumber { get; }
        internal int[]? SnapshotDice => _snapshotDice?.ToArray();
        internal SpiritualConflictFileImage DifficultySettings { get; }
        internal SpiritualConflictFileImage? TurnRequest { get; }

        internal SpiritualConflictValidationFrame WithCandidateImages(
            SpiritualConflictCandidateImages candidate) =>
            new(candidate, Baseline, HasValidatedSnapshot, SnapshotTurnNumber,
                _snapshotDice, DifficultySettings, TurnRequest);

        // No public/internal constructor accepts a claimed validated flag.
        // Only the existing authenticated lookup selects signed context here.
        internal static async Task<SpiritualConflictValidationFrame> CaptureAsync(
            ValidationService owner,
            SpiritualConflictFileImage conflict)
        {
            ArgumentNullException.ThrowIfNull(owner);
            ArgumentNullException.ThrowIfNull(conflict);
            var lookup = await owner.LoadValidatedPendingTurnSnapshotLookupAsync();
            var manifest = lookup.Status == ValidatedPendingTurnSnapshotStatus.Usable
                ? lookup.Manifest
                : null;
            var hasSnapshot = manifest is not null;
            var turn = manifest?.TurnNumber;
            var dice = manifest?.PreGeneratedDices1d20?.ToArray();

            var soul = await owner.ReadSpiritualConflictFileImageAsync(
                "game_state/meta/soul_state.json");
            var shining = await owner.ReadSpiritualConflictFileImageAsync(
                ShiningAbodeState.StatePath);
            // Signed evaluators never consume current profile authority. Null
            // records an intentionally uncaptured input, not an absent file.
            var profiles = hasSnapshot
                ? null
                : await owner.ReadSpiritualConflictFileImageAsync(
                    AfterlifeEntityProfileState.StatePath);
            var settings = await owner.ReadSpiritualConflictFileImageAsync(
                AfterlifeSpiritualConflictState.DifficultySettingsPath);

            // WithCandidateImages cannot change signed dice. Null means this
            // unused input was not captured, not that its file was absent.
            var request = dice is { Length: > 0 }
                ? null
                : await owner.ReadSpiritualConflictFileImageAsync("input/turn_request.json");

            SpiritualConflictBaselineImages baseline;
            if (manifest is null)
            {
                baseline = new(null, null, null, null, null, null, false);
            }
            else
            {
                // Reuse the existing security reader unchanged. Each logical root is
                // selected once; its internal authority/control rechecks remain intact.
                var preConflict = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                    manifest, AfterlifeSpiritualConflictState.StatePath);
                var preSoul = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                    manifest, "game_state/meta/soul_state.json");
                var preShining = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                    manifest, ShiningAbodeState.StatePath);
                var preProfiles = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                    manifest, AfterlifeEntityProfileState.StatePath);
                string? preDefinitions = null;
                string? preState = null;
                var resourceAcquisitionFailed = false;
                if (TryParseJsonObject(preConflict)?["activeConflict"] is JsonObject)
                {
                    // This is the same narrow exception boundary as the old
                    // ResolvePreTurnActiveConflictControlContextAsync resource reads.
                    try
                    {
                        preDefinitions = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                            manifest, ResourceMaterializationContract.DefinitionsPath);
                        preState = await owner.ReadValidatedPendingTurnSnapshotFileAsync(
                            manifest, ResourceMaterializationContract.StatePath);
                    }
                    catch
                    {
                        preDefinitions = null;
                        preState = null;
                        resourceAcquisitionFailed = true;
                    }
                }
                baseline = new(preConflict, preSoul, preShining, preProfiles,
                    preDefinitions, preState, resourceAcquisitionFailed);
            }

            return new SpiritualConflictValidationFrame(
                new(conflict, soul, shining, profiles), baseline, hasSnapshot,
                turn, dice, settings, request);
        }
    }

    internal async Task<SpiritualConflictValidationFrame>
        CaptureSpiritualConflictValidationFrameAsync(SpiritualConflictFileImage? conflict = null)
    {
        conflict ??= await ReadSpiritualConflictFileImageAsync(
            AfterlifeSpiritualConflictState.StatePath);
        return await SpiritualConflictValidationFrame.CaptureAsync(this, conflict);
    }

    private async Task<SpiritualConflictFileImage> ReadSpiritualConflictFileImageAsync(
        string path)
    {
        var text = await _fs.ReadFileAsync(path);
        // Preserve the production empty-root diagnostic: a present empty file differs
        // from absence. No parser/defaulting is applied to the captured current image.
        var exists = text is not null || _fs.FileExists(path);
        return new SpiritualConflictFileImage(exists, text);
    }

    private static AfterlifeSpiritualConflictGateContext CreateSpiritualConflictGateContext(
        SpiritualConflictValidationFrame frame) =>
        new(
            ReadSpiritualConflictRealm(
                frame.HasValidatedSnapshot ? frame.Baseline.Soul : frame.Candidate.Soul.Text,
                rejectWhitespace: frame.HasValidatedSnapshot),
            frame.HasValidatedSnapshot,
            frame.SnapshotTurnNumber);

    private static string? ReadSpiritualConflictRealm(string? json, bool rejectWhitespace)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != JsonValueKind.Object ||
                !doc.RootElement.TryGetProperty("currentRealm", out var realm) ||
                realm.ValueKind != JsonValueKind.String)
                return null;
            var value = realm.GetString();
            return rejectWhitespace && string.IsNullOrWhiteSpace(value) ? null : value;
        }
        catch
        {
            return null;
        }
    }

    private static AfterlifeConflictDiceContext CreateSpiritualConflictDiceContext(
        SpiritualConflictValidationFrame frame)
    {
        var soul = TryParseJsonObject(frame.Candidate.Soul.Text);
        var shining = TryParseJsonObject(frame.Candidate.Shining.Text);
        int? grantTurn = SourceOfLightCapstoneState.HasLightIncarnate(soul)
            ? SourceOfLightCapstoneState.GetLightIncarnateGrantTurn(soul, shining)
            : null;
        var preConflict = TryParseJsonObject(frame.Baseline.Conflict);
        var activePayloads = new List<JsonObject>();
        var recentPayloads = new List<JsonObject>();
        if (frame.HasValidatedSnapshot)
        {
            if (preConflict?["activeConflict"] is JsonObject active &&
                active["exchangeLog"] is JsonArray log)
            {
                foreach (var entry in log.OfType<JsonObject>())
                    TryAddPreTurnConflictPayload(activePayloads, entry);
            }
            if (preConflict?["recentConflicts"] is JsonArray recent)
            {
                foreach (var entry in recent.OfType<JsonObject>())
                    TryAddPreTurnConflictPayload(recentPayloads, entry);
            }
        }
        var control = CreateSpiritualConflictPreTurnControl(frame);
        var difficulty = ReadSpiritualConflictDifficulty(frame.DifficultySettings.Text);
        var selectedDice = frame.SnapshotDice;
        int? currentTurn = frame.SnapshotTurnNumber;
        if (selectedDice is not { Length: > 0 })
        {
            selectedDice = null;
            var requestJson = frame.TurnRequest?.Text;
            if (!string.IsNullOrWhiteSpace(requestJson))
            {
                try
                {
                    if (JsonNode.Parse(requestJson) is JsonObject request &&
                        request["preGeneratedDices1d20"] is JsonArray rawDice)
                    {
                        var dice = new List<int>();
                        foreach (var item in rawDice)
                        {
                            if (TryGetJsonNodeInt(item, out var value))
                                dice.Add(value);
                        }
                        if (dice.Count > 0)
                        {
                            selectedDice = dice.ToArray();
                            currentTurn = frame.SnapshotTurnNumber ??
                                AfterlifeSpiritualConflictState.GetNodeInt(request["turnNumber"]);
                        }
                    }
                }
                catch
                {
                    // Preserve current malformed-request / shape-only fallback.
                }
            }
        }

        return new AfterlifeConflictDiceContext(
            selectedDice, grantTurn, recentPayloads, activePayloads,
            control.ConflictId, control.ControlState,
            control.PlayerResourceCurrent, control.PlayerResourceMaximum,
            control.OppositionResourceCurrent, control.OppositionResourceMaximum,
            HasValidatedTurnBaseline: frame.HasValidatedSnapshot,
            Difficulty: difficulty,
            CurrentTurn: currentTurn);
    }

    private static PreTurnActiveConflictControlContext CreateSpiritualConflictPreTurnControl(
        SpiritualConflictValidationFrame frame)
    {
        if (frame.HasValidatedSnapshot && !frame.Baseline.ResourceAcquisitionFailed)
        {
            try
            {
                if (TryParseJsonObject(frame.Baseline.Conflict)?["activeConflict"]
                    is JsonObject active)
                {
                    var projection = AfterlifeConflictActionPointProjectionService.Resolve(
                        frame.Baseline.ResourceDefinitions,
                        frame.Baseline.ResourceState,
                        active);
                    return new PreTurnActiveConflictControlContext(
                        TryReadConflictId(active),
                        active.ContainsKey("controlState") ? active["controlState"]?.DeepClone() : null,
                        TryReadIntegralResourceValue(projection.Projection?.Player.Current),
                        TryReadIntegralResourceValue(projection.Projection?.Player.Maximum),
                        TryReadIntegralResourceValue(projection.Projection?.Opposition.Current),
                        TryReadIntegralResourceValue(projection.Projection?.Opposition.Maximum));
                }
            }
            catch
            {
                // Preserve the existing unresolved pre-turn projection result.
            }
        }
        return new PreTurnActiveConflictControlContext(null, null, null, null, null, null);
    }

    private static AfterlifeActionCostAuthorityContext CreateSpiritualConflictActionCostContext(
        SpiritualConflictValidationFrame frame)
    {
        var soul = frame.HasValidatedSnapshot ? frame.Baseline.Soul : frame.Candidate.Soul.Text;
        var profiles = TryParseJsonObject(
            frame.HasValidatedSnapshot ? frame.Baseline.Profiles : frame.Candidate.Profiles?.Text);
        var conflict = frame.HasValidatedSnapshot
            ? frame.Baseline.Conflict : frame.Candidate.Conflict.Text;
        return new AfterlifeActionCostAuthorityContext(
            ReadAfterlifeCombatProfileArtTiers(TryParseJsonObject(soul)),
            ReadPlayerSpecialArts(profiles),
            ReadSpecialArtsByOwner(profiles),
            ReadEntityStandardArtTiers(profiles),
            ReadConflictActorArtTierSnapshots(TryParseJsonObject(conflict)));
    }

    private static AfterlifeConflictRewardContext CreateSpiritualConflictRewardContext(
        SpiritualConflictValidationFrame frame,
        AfterlifeSpiritualConflictGateContext gate)
    {
        var currentSoul = TryParseJsonObject(frame.Candidate.Soul.Text);
        var preSoul = TryParseJsonObject(frame.Baseline.Soul);
        var currentShining = TryParseJsonObject(frame.Candidate.Shining.Text);
        var preShining = TryParseJsonObject(frame.Baseline.Shining);
        var preConflict = TryParseJsonObject(frame.Baseline.Conflict);
        var active = preConflict?["activeConflict"] as JsonObject;
        return new AfterlifeConflictRewardContext
        {
            AuthorityRealmKey = AfterlifeSpiritualConflictState.NormalizeAfterlifeRealmKey(gate.Realm),
            UsesValidatedSnapshot = gate.UsesValidatedSnapshot,
            CurrentTurn = gate.TurnNumber is > 0 ? gate.TurnNumber : null,
            PreTurnInkFeathers = preSoul == null ? null : ShiningAbodeState.GetSoulSpendableInkFeathers(preSoul),
            CurrentInkFeathers = currentSoul == null ? null : ShiningAbodeState.GetSoulSpendableInkFeathers(currentSoul),
            PreTurnLightSparks = preShining == null ? null : AfterlifeSpiritualConflictState.GetNodeInt(preShining["lightSparks"]),
            CurrentLightSparks = currentShining == null ? null : AfterlifeSpiritualConflictState.GetNodeInt(currentShining["lightSparks"]),
            PreTurnActiveConflictId = active == null ? null : TryReadConflictId(active),
            PreTurnSideModel = AfterlifeSpiritualConflictState.GetNodeString(active?["sideModel"]),
            PreTurnConflictPosition = AfterlifeSpiritualConflictState.GetNodeString(active?["conflictPosition"]),
            PreTurnOpposingLeadStrength = ResolveRewardOpposingLeadStrength(active),
            Difficulty = ReadSpiritualConflictDifficulty(frame.DifficultySettings.Text)
        };
    }

    private static AfterlifeDifficultyDefinition? ReadSpiritualConflictDifficulty(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;
        try
        {
            if (JsonNode.Parse(json) is not JsonObject settings)
                return null;
            var difficulty = AfterlifeSpiritualConflictState.GetNodeString(settings["difficulty"]);
            if (string.IsNullOrWhiteSpace(difficulty))
            {
                if (TryGetJsonNodeBool(settings["impossibleMode"], out var impossible) && impossible)
                    difficulty = "impossible";
                else if (TryGetJsonNodeBool(settings["hardMode"], out var hard) && hard)
                    difficulty = "hard";
                else
                    difficulty = "normal";
            }
            return ResolveAfterlifeDifficultyDefinition(difficulty);
        }
        catch
        {
            return null;
        }
    }

    private static AfterlifeSoulDissipationContext CreateSpiritualConflictSoulDissipationContext(
        SpiritualConflictValidationFrame frame)
    {
        var profilesRoot = TryParseJsonObject(
            frame.HasValidatedSnapshot ? frame.Baseline.Profiles : frame.Candidate.Profiles?.Text);
        var profiles = new Dictionary<string, JsonObject>(StringComparer.OrdinalIgnoreCase);
        if (profilesRoot?[AfterlifeEntityProfileState.ProfilesProperty] is JsonArray rows)
        {
            foreach (var profile in rows.OfType<JsonObject>())
            {
                var key = AfterlifeEntityProfileState.BuildIdentityKey(profile);
                if (!string.IsNullOrWhiteSpace(key))
                    profiles[key] = profile;
            }
        }
        return new AfterlifeSoulDissipationContext(
            TryParseJsonObject(frame.Candidate.Soul.Text), profiles);
    }

    private static string? ReadSpiritualConflictGateAvailability(
        SpiritualConflictValidationFrame frame)
    {
        var root = TryParseJsonObject(
            frame.HasValidatedSnapshot ? frame.Baseline.Shining : frame.Candidate.Shining.Text);
        if (frame.HasValidatedSnapshot)
            return root == null ? null : AfterlifeSpiritualConflictState.GetNodeString(root["availability"]);
        try
        {
            return root == null ? null : AfterlifeSpiritualConflictState.GetNodeString(root["availability"]);
        }
        catch
        {
            return null;
        }
    }

    private static ShiningAbodeState.PreparedIncarnationPackageMode ReadSpiritualConflictGatePackageMode(
        SpiritualConflictValidationFrame frame)
    {
        var root = TryParseJsonObject(
            frame.HasValidatedSnapshot ? frame.Baseline.Shining : frame.Candidate.Shining.Text);
        if (frame.HasValidatedSnapshot)
        {
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }
        try
        {
            return root == null
                ? ShiningAbodeState.PreparedIncarnationPackageMode.Absent
                : ShiningAbodeState.GetPreparedIncarnationPackageMode(root);
        }
        catch
        {
            return ShiningAbodeState.PreparedIncarnationPackageMode.Absent;
        }
    }

    private static bool TryParseSpiritualConflictCurrentImage(
        SpiritualConflictFileImage image,
        List<ValidationIssue> issues,
        out JsonObject root)
    {
        var json = image.Text;
        root = null!;
        if (string.IsNullOrWhiteSpace(json))
        {
            if (image.Exists)
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

            return false;
        }

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
            return false;
        }
        return true;
    }

    /// <summary>
    /// Evaluates captured conflict inputs with ordinary rules and optional exact published causal comparisons.
    /// </summary>
    /// <param name="frame">
    /// Captured current inputs and authenticated original baseline.
    /// </param>
    /// <param name="completion">
    /// Published original-turn comparisons, or <see langword="null"/> for ordinary and live preparation validation.
    /// </param>
    /// <returns>
    /// Validation diagnostics, including an error if published exchange evidence no longer matches.
    /// </returns>
    internal IReadOnlyList<ValidationIssue> EvaluateSpiritualConflictValidationFrame(
        SpiritualConflictValidationFrame frame,
        SpiritualOriginalTurnCapture.SpiritualCompletedConflictValidation? completion = null)
    {
        ArgumentNullException.ThrowIfNull(frame);
        var issues = new List<ValidationIssue>();
        if (!TryParseSpiritualConflictCurrentImage(frame.Candidate.Conflict, issues, out var root))
        {
            if (completion is not null) issues.Add(CompletedSpiritualConflictMismatch());
            ValidateSpiritualConflictCapturedPreTurnIntegrity(
                frame.HasValidatedSnapshot ? frame.Baseline.Conflict : null, null, issues);
            return issues.AsReadOnly();
        }

        var gateContext = CreateSpiritualConflictGateContext(frame);
        var diceContext = CreateSpiritualConflictDiceContext(frame);
        var actionCostAuthority = CreateSpiritualConflictActionCostContext(frame) with
        {
            CompletedConflictValidation = completion
        };
        var rewardContext = CreateSpiritualConflictRewardContext(frame, gateContext);
        var soulDissipationContext = CreateSpiritualConflictSoulDissipationContext(frame);
        ValidateSpiritualConflictCapturedPreTurnIntegrity(
            frame.HasValidatedSnapshot ? frame.Baseline.Conflict : null, root, issues);
        try
        {
            completion?.ValidateRoot(root!);
            ValidateAfterlifeSpiritualConflictRoot(root, AfterlifeSpiritualConflictState.StatePath, issues, diceContext, actionCostAuthority, rewardContext, soulDissipationContext);
        }
        catch (InvalidOperationException) when (completion is not null)
        {
            issues.Add(CompletedSpiritualConflictMismatch());
            return issues.AsReadOnly();
        }
        ValidateAfterlifeConflictRewardStateDeltas(rewardContext, issues);

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
                    var availability = ReadSpiritualConflictGateAvailability(frame);
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

                    var packageMode = ReadSpiritualConflictGatePackageMode(frame);
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
        return issues.AsReadOnly();
    }

    private static void ValidateSpiritualConflictCapturedPreTurnIntegrity(
        string? preTurnJson,
        JsonObject? currentRoot,
        List<ValidationIssue> issues)
    {
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

        ValidateDangerModePreTurnIntegrity(preTurnRoot, currentRoot, issues);

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
}
