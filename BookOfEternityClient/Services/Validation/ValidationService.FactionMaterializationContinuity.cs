using System.Text.Json;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal enum FactionTouchKind
{
    New,
    LegacyPromotion,
    AlreadyMaterialized,
    ClientDerivedOnly,
    UntouchedLegacy
}

internal static class FactionTouchClassifier
{
    internal static FactionTouchKind Classify(
        bool existedPreTurn,
        bool hadReceiptPreTurn,
        bool gmAuthoredTouch,
        bool clientDerivedOnly)
    {
        if (!existedPreTurn)
            return FactionTouchKind.New;
        if (clientDerivedOnly && !gmAuthoredTouch)
            return FactionTouchKind.ClientDerivedOnly;
        if (hadReceiptPreTurn)
            return FactionTouchKind.AlreadyMaterialized;
        return gmAuthoredTouch
            ? FactionTouchKind.LegacyPromotion
            : FactionTouchKind.UntouchedLegacy;
    }
}

public partial class ValidationService
{
    private const string MortalFactionMaterializationPath =
        "game_state/factions/faction_core.json";
    private const string ShiningFactionMaterializationPath =
        "game_state/meta/shining_abode_state.json";

    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateAcceptedTurnRawFactionMaterializationAsync()
    {
        var issues = new List<ValidationIssue>();
        var materializedFactions =
            new List<(
                JsonElement Faction,
                string Context,
                string FactionType,
                string FactionId)>();
        await ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(
            rawBeforeNormalization: true,
            issues,
            materializedFactions);
        await ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(
            rawBeforeNormalization: true,
            issues,
            materializedFactions);
        issues.AddRange(
            FactionMaterializationContract.ValidateUniqueMaterializationIds(
                materializedFactions));
        return issues;
    }

    private async Task ValidateAcceptedTurnFactionMaterializationCompletenessAsync(
        List<ValidationIssue> issues)
    {
        var materializedFactions =
            new List<(
                JsonElement Faction,
                string Context,
                string FactionType,
                string FactionId)>();
        await ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(
            rawBeforeNormalization: false,
            issues,
            materializedFactions);
        await ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(
            rawBeforeNormalization: false,
            issues,
            materializedFactions);
        issues.AddRange(
            FactionMaterializationContract.ValidateUniqueMaterializationIds(
                materializedFactions));
    }

    private async Task ValidateAcceptedTurnMortalFactionMaterializationContinuityAsync(
        bool rawBeforeNormalization,
        List<ValidationIssue> issues,
        List<(
            JsonElement Faction,
            string Context,
            string FactionType,
            string FactionId)> materializedFactions)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var currentFileExists = _fs.FileExists(MortalFactionMaterializationPath);
        var currentJson = await _fs.ReadFileAsync(MortalFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            if (currentFileExists ||
                await ValidatedPreTurnHasFactionAuthorityAsync(
                    lookup,
                    MortalFactionMaterializationPath))
            {
                AddUnusableCurrentFactionMaterializationFileIssue(
                    MortalFactionMaterializationPath,
                    currentFileExists ? "blank current file" : "missing current file",
                    issues);
            }

            return;
        }

        using var currentDocument = TryParseFactionMaterializationDocument(
            currentJson,
            MortalFactionMaterializationPath,
            currentAuthority: true,
            issues);
        if (currentDocument == null ||
            currentDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            if (currentDocument != null)
            {
                AddUnusableCurrentFactionMaterializationFileIssue(
                    MortalFactionMaterializationPath,
                    $"non-object root ({currentDocument.RootElement.ValueKind})",
                    issues);
            }

            return;
        }

        var currentCanonical = ReadCurrentFactionCarriers(
            currentDocument.RootElement,
            MortalFactionMaterializationPath,
            "factions",
            useInitialId: false,
            "mortal_faction",
            issues);
        var currentFull = rawBeforeNormalization
            ? ReadCurrentFactionCarriers(
                currentDocument.RootElement,
                MortalFactionMaterializationPath,
                "factionDataChanges",
                useInitialId: true,
                "mortal_faction",
                issues)
            : new Dictionary<string, FactionCarrier>(StringComparer.Ordinal);

        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
        {
            if (rawBeforeNormalization &&
                (currentCanonical.Count > 0 || currentFull.Count > 0))
            {
                AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                    MortalFactionMaterializationPath,
                    "missing",
                    issues);
            }

            ValidateFactionsWithoutPreTurnAuthority(
                currentCanonical.Values.Concat(currentFull.Values),
                FactionMaterializationFamily.Mortal,
                "mortal_faction",
                issues,
                materializedFactions);
            return;
        }

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                MortalFactionMaterializationPath,
                DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                issues);
            return;
        }

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            MortalFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
        {
            if (currentCanonical.Count > 0 || currentFull.Count > 0)
            {
                AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                    MortalFactionMaterializationPath,
                    "missing tracked faction authority",
                    issues);
            }

            return;
        }

        using var preTurnDocument = TryParseFactionMaterializationDocument(
            preTurnJson,
            MortalFactionMaterializationPath,
            currentAuthority: false,
            issues);
        if (preTurnDocument == null ||
            !TryReadPreTurnFactionMap(
                preTurnDocument.RootElement,
                MortalFactionMaterializationPath,
                "factions",
                out var preTurnFactions))
        {
            AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                MortalFactionMaterializationPath,
                "malformed, duplicate, or ambiguous faction authority",
                issues);
            return;
        }

        var factionIds = currentCanonical.Keys
            .Concat(currentFull.Keys)
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        foreach (var factionId in factionIds)
        {
            currentCanonical.TryGetValue(factionId, out var canonicalCarrier);
            currentFull.TryGetValue(factionId, out var fullCarrier);
            preTurnFactions.TryGetValue(factionId, out var preTurnFaction);

            var hadReceiptPreTurn =
                preTurnFaction?.Faction.TryGetProperty(
                    FactionMaterializationContract.PropertyName,
                    out _) == true;
            var carrier = SelectMortalFactionCarrier(
                canonicalCarrier,
                fullCarrier,
                hadReceiptPreTurn,
                factionId,
                issues);
            if (carrier == null)
                continue;

            if (fullCarrier != null && hadReceiptPreTurn)
            {
                AddExistingFactionFullResendIssue(
                    fullCarrier.Context,
                    "mortal_faction",
                    factionId,
                    issues);
            }

            var gmAuthoredTouch =
                fullCarrier != null ||
                preTurnFaction == null ||
                !FactionJsonSemanticallyEqual(
                    carrier.Faction,
                    preTurnFaction.Faction);
            ValidateFactionContinuity(
                carrier,
                preTurnFaction,
                FactionMaterializationFamily.Mortal,
                "mortal_faction",
                gmAuthoredTouch,
                clientDerivedOnly: false,
                issues,
                materializedFactions);
        }

        if (!rawBeforeNormalization)
        {
            AddMissingHistoricalFactionIssues(
                preTurnFactions,
                currentCanonical,
                "mortal_faction",
                issues);
        }
    }

    private async Task ValidateAcceptedTurnShiningFactionMaterializationContinuityAsync(
        bool rawBeforeNormalization,
        List<ValidationIssue> issues,
        List<(
            JsonElement Faction,
            string Context,
            string FactionType,
            string FactionId)> materializedFactions)
    {
        var lookup = await LoadValidatedPendingTurnSnapshotLookupAsync();
        var currentFileExists = _fs.FileExists(ShiningFactionMaterializationPath);
        var currentJson = await _fs.ReadFileAsync(ShiningFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(currentJson))
        {
            if (currentFileExists ||
                await ValidatedPreTurnHasFactionAuthorityAsync(
                    lookup,
                    ShiningFactionMaterializationPath))
            {
                AddUnusableCurrentFactionMaterializationFileIssue(
                    ShiningFactionMaterializationPath,
                    currentFileExists ? "blank current file" : "missing current file",
                    issues);
            }

            return;
        }

        using var currentDocument = TryParseFactionMaterializationDocument(
            currentJson,
            ShiningFactionMaterializationPath,
            currentAuthority: true,
            issues);
        if (currentDocument == null ||
            currentDocument.RootElement.ValueKind != JsonValueKind.Object)
        {
            if (currentDocument != null)
            {
                AddUnusableCurrentFactionMaterializationFileIssue(
                    ShiningFactionMaterializationPath,
                    $"non-object root ({currentDocument.RootElement.ValueKind})",
                    issues);
            }

            return;
        }

        var currentFactions = ReadCurrentFactionCarriers(
            currentDocument.RootElement,
            ShiningFactionMaterializationPath,
            "factions",
            useInitialId: false,
            "shining_faction",
            issues);
        if (lookup.Status == ValidatedPendingTurnSnapshotStatus.Missing)
        {
            if (rawBeforeNormalization && currentFactions.Count > 0)
            {
                AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                    ShiningFactionMaterializationPath,
                    "missing",
                    issues);
            }

            ValidateFactionsWithoutPreTurnAuthority(
                currentFactions.Values,
                FactionMaterializationFamily.Shining,
                "shining_faction",
                issues,
                materializedFactions);
            return;
        }

        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                ShiningFactionMaterializationPath,
                DescribeValidatedPendingTurnSnapshotStatus(lookup.Status),
                issues);
            return;
        }

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            ShiningFactionMaterializationPath);
        if (string.IsNullOrWhiteSpace(preTurnJson))
        {
            if (currentFactions.Count > 0)
            {
                AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                    ShiningFactionMaterializationPath,
                    "missing tracked faction authority",
                    issues);
            }

            return;
        }

        using var preTurnDocument = TryParseFactionMaterializationDocument(
            preTurnJson,
            ShiningFactionMaterializationPath,
            currentAuthority: false,
            issues);
        if (preTurnDocument == null ||
            !TryReadPreTurnFactionMap(
                preTurnDocument.RootElement,
                ShiningFactionMaterializationPath,
                "factions",
                out var preTurnFactions))
        {
            AddUnusableFactionMaterializationPreTurnAuthorityIssue(
                ShiningFactionMaterializationPath,
                "malformed, duplicate, or ambiguous faction authority",
                issues);
            return;
        }

        foreach (var (factionId, carrier) in currentFactions)
        {
            preTurnFactions.TryGetValue(factionId, out var preTurnFaction);
            var exactEquality =
                preTurnFaction != null &&
                FactionJsonSemanticallyEqual(
                    carrier.Faction,
                    preTurnFaction.Faction);
            var derivedEquality =
                preTurnFaction != null &&
                ShiningFactionJsonEqualIgnoringDerivedFields(
                    carrier.Faction,
                    preTurnFaction.Faction);
            var clientDerivedOnly = !exactEquality && derivedEquality;
            ValidateFactionContinuity(
                carrier,
                preTurnFaction,
                FactionMaterializationFamily.Shining,
                "shining_faction",
                gmAuthoredTouch: preTurnFaction == null || !derivedEquality,
                clientDerivedOnly,
                issues,
                materializedFactions);
        }

        AddMissingHistoricalFactionIssues(
            preTurnFactions,
            currentFactions,
            "shining_faction",
            issues);
    }

    private static JsonDocument? TryParseFactionMaterializationDocument(
        string json,
        string path,
        bool currentAuthority,
        List<ValidationIssue> issues)
    {
        try
        {
            var document = JsonDocument.Parse(json);
            if (TryFindDuplicateJsonProperty(
                    document.RootElement,
                    out var duplicatePath))
            {
                issues.Add(new ValidationIssue(
                    $"{path}{duplicatePath}",
                    IssueSeverity.Error,
                    "Faction materialization authority contains a duplicate JSON property.",
                    code: currentAuthority
                        ? "faction_materialization_current_authority_unusable"
                        : "faction_materialization_pre_turn_authority_unusable",
                    section: "FactionMaterialization",
                    expected: "one unambiguous value per exact JSON property",
                    actual: duplicatePath,
                    repairHint: currentAuthority
                        ? "Restore one exact current faction authority value before retrying the turn."
                        : "Restore the client-validated pre-turn snapshot; do not choose a duplicate winner by member order."));
                document.Dispose();
                return null;
            }

            return document;
        }
        catch (JsonException)
        {
            if (currentAuthority)
            {
                AddUnusableCurrentFactionMaterializationFileIssue(
                    path,
                    "malformed JSON",
                    issues);
            }

            return null;
        }
    }

    private async Task<bool> ValidatedPreTurnHasFactionAuthorityAsync(
        ValidatedPendingTurnSnapshotLookup lookup,
        string path)
    {
        if (lookup.Status != ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            return false;
        }

        var preTurnJson = await ReadValidatedPendingTurnSnapshotFileAsync(
            lookup.Manifest,
            path);
        if (string.IsNullOrWhiteSpace(preTurnJson))
            return false;

        try
        {
            using var document = JsonDocument.Parse(preTurnJson);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return true;
            if (!document.RootElement.TryGetProperty("factions", out var factions))
                return false;
            return factions.ValueKind != JsonValueKind.Array ||
                   factions.GetArrayLength() > 0;
        }
        catch (JsonException)
        {
            return true;
        }
    }

    private static Dictionary<string, FactionCarrier> ReadCurrentFactionCarriers(
        JsonElement root,
        string path,
        string arrayName,
        bool useInitialId,
        string factionType,
        List<ValidationIssue> issues)
    {
        var carriers = new Dictionary<string, FactionCarrier>(StringComparer.Ordinal);
        if (!root.TryGetProperty(arrayName, out var array))
            return carriers;
        if (array.ValueKind != JsonValueKind.Array)
        {
            AddUnusableCurrentFactionMaterializationAuthorityIssue(
                $"{path}.{arrayName}",
                factionType,
                null,
                "array",
                array.ValueKind.ToString(),
                issues);
            return carriers;
        }

        var index = 0;
        foreach (var faction in array.EnumerateArray())
        {
            var context = $"{path}.{arrayName}[{index++}]";
            if (faction.ValueKind != JsonValueKind.Object ||
                !TryReadCurrentFactionId(
                    faction,
                    useInitialId,
                    out var factionId))
            {
                AddUnusableCurrentFactionMaterializationAuthorityIssue(
                    context,
                    factionType,
                    null,
                    useInitialId
                        ? "exact factionId or same-turn initialId"
                        : "exact non-empty factionId",
                    "missing or malformed",
                    issues);
                continue;
            }

            if (!carriers.TryAdd(
                    factionId,
                    new FactionCarrier(faction.Clone(), context, factionId)))
            {
                issues.Add(new ValidationIssue(
                    context,
                    IssueSeverity.Error,
                    "Current faction authority contains a duplicate exact faction identity.",
                    code: "faction_materialization_duplicate_effective_identity",
                    actor: $"{factionType}:{factionId}",
                    section: "FactionMaterialization",
                    expected: "one current faction carrier per exact identity and carrier",
                    actual: $"duplicate {factionId} in {arrayName}",
                    repairHint: "Keep one authoritative faction object for this exact identity."));
            }
        }

        return carriers;
    }

    private static bool TryReadCurrentFactionId(
        JsonElement faction,
        bool useInitialId,
        out string factionId)
    {
        factionId = ReadNonEmptyFactionString(faction, "factionId") ?? string.Empty;
        if (factionId.Length > 0)
            return true;
        if (!useInitialId)
            return false;

        factionId = ReadNonEmptyFactionString(faction, "initialId") ?? string.Empty;
        return factionId.Length > 0;
    }

    private static bool TryReadPreTurnFactionMap(
        JsonElement root,
        string path,
        string arrayName,
        out Dictionary<string, PreTurnFaction> factions)
    {
        factions = new Dictionary<string, PreTurnFaction>(StringComparer.Ordinal);
        if (root.ValueKind != JsonValueKind.Object)
            return false;
        if (!root.TryGetProperty(arrayName, out var array))
            return true;
        if (array.ValueKind != JsonValueKind.Array)
            return false;

        var index = 0;
        foreach (var faction in array.EnumerateArray())
        {
            var context = $"{path}.{arrayName}[{index++}]";
            var factionId = faction.ValueKind == JsonValueKind.Object
                ? ReadNonEmptyFactionString(faction, "factionId")
                : null;
            if (factionId == null ||
                !factions.TryAdd(
                    factionId,
                    new PreTurnFaction(faction.Clone(), context, factionId)))
            {
                return false;
            }
        }

        return true;
    }

    private static FactionCarrier? SelectMortalFactionCarrier(
        FactionCarrier? canonicalCarrier,
        FactionCarrier? fullCarrier,
        bool hadReceiptPreTurn,
        string factionId,
        List<ValidationIssue> issues)
    {
        if (canonicalCarrier == null)
            return fullCarrier;
        if (fullCarrier == null)
            return canonicalCarrier;

        issues.Add(new ValidationIssue(
            fullCarrier.Context,
            IssueSeverity.Error,
            "Current Mortal authority contains both canonical and full carriers for one exact faction identity.",
            code: "faction_materialization_duplicate_effective_identity",
            actor: $"mortal_faction:{factionId}",
            section: "FactionMaterialization",
            expected: "one effective current faction carrier",
            actual: $"also present at {canonicalCarrier.Context}",
            repairHint: hadReceiptPreTurn
                ? "Use only narrow update authority for an already materialized faction and preserve its canonical carrier."
                : "Keep one full carrier for a new or promoted faction; do not duplicate the same exact identity in canonical and full arrays."));
        return fullCarrier;
    }

    private static void ValidateFactionContinuity(
        FactionCarrier current,
        PreTurnFaction? preTurn,
        FactionMaterializationFamily family,
        string factionType,
        bool gmAuthoredTouch,
        bool clientDerivedOnly,
        List<ValidationIssue> issues,
        List<(
            JsonElement Faction,
            string Context,
            string FactionType,
            string FactionId)> materializedFactions)
    {
        var existedPreTurn = preTurn != null;
        var hadReceiptPreTurn =
            preTurn?.Faction.TryGetProperty(
                FactionMaterializationContract.PropertyName,
                out _) == true;
        var touchKind = FactionTouchClassifier.Classify(
            existedPreTurn,
            hadReceiptPreTurn,
            gmAuthoredTouch,
            clientDerivedOnly);
        var requireEnvelope = touchKind is
            FactionTouchKind.New or
            FactionTouchKind.LegacyPromotion or
            FactionTouchKind.AlreadyMaterialized;
        if (touchKind == FactionTouchKind.ClientDerivedOnly &&
            hadReceiptPreTurn)
        {
            requireEnvelope = true;
        }

        var structuralEvidence = new FactionMaterializationEvidence(
            factionType,
            current.FactionId,
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal),
            new Dictionary<string, bool>(StringComparer.Ordinal));

        issues.AddRange(FactionMaterializationContract.Validate(
            current.Faction,
            current.Context,
            family,
            structuralEvidence,
            requireEnvelope,
            deferEvidenceConsistency: true));

        if (hadReceiptPreTurn)
        {
            ValidateHistoricalFactionMaterializationEnvelope(
                current,
                preTurn!,
                factionType,
                issues);
        }

        if (current.Faction.TryGetProperty(
                FactionMaterializationContract.PropertyName,
                out _))
        {
            materializedFactions.Add((
                current.Faction.Clone(),
                current.Context,
                factionType,
                current.FactionId));
        }
    }

    private static void ValidateFactionsWithoutPreTurnAuthority(
        IEnumerable<FactionCarrier> carriers,
        FactionMaterializationFamily family,
        string factionType,
        List<ValidationIssue> issues,
        List<(
            JsonElement Faction,
            string Context,
            string FactionType,
            string FactionId)> materializedFactions)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var carrier in carriers)
        {
            if (!seen.Add(carrier.FactionId))
                continue;

            var structuralEvidence = new FactionMaterializationEvidence(
                factionType,
                carrier.FactionId,
                new Dictionary<string, bool>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal),
                new Dictionary<string, bool>(StringComparer.Ordinal));
            issues.AddRange(FactionMaterializationContract.Validate(
                carrier.Faction,
                carrier.Context,
                family,
                structuralEvidence,
                requireEnvelope: false,
                deferEvidenceConsistency: true));
            if (carrier.Faction.TryGetProperty(
                    FactionMaterializationContract.PropertyName,
                    out _))
            {
                materializedFactions.Add((
                    carrier.Faction.Clone(),
                    carrier.Context,
                    factionType,
                    carrier.FactionId));
            }
        }
    }

    private static void ValidateHistoricalFactionMaterializationEnvelope(
        FactionCarrier current,
        PreTurnFaction preTurn,
        string factionType,
        List<ValidationIssue> issues)
    {
        var hasCurrentEnvelope = current.Faction.TryGetProperty(
            FactionMaterializationContract.PropertyName,
            out var currentEnvelope);
        var hasPreTurnEnvelope = preTurn.Faction.TryGetProperty(
            FactionMaterializationContract.PropertyName,
            out var preTurnEnvelope);
        if (hasCurrentEnvelope &&
            hasPreTurnEnvelope &&
            FactionJsonSemanticallyEqual(currentEnvelope, preTurnEnvelope))
        {
            return;
        }

        issues.Add(new ValidationIssue(
            $"{current.Context}.{FactionMaterializationContract.PropertyName}",
            IssueSeverity.Error,
            "Historical faction materialization receipt cannot be removed or changed.",
            code: "faction_materialization_immutable_receipt_changed",
            actor: $"{factionType}:{current.FactionId}",
            section: "FactionMaterialization",
            expected: "semantic equality with validated pre-turn materialization receipt",
            actual: hasCurrentEnvelope ? "changed" : "missing",
            repairHint: "Restore the exact validated pre-turn materialization receipt and apply gameplay changes through supported narrow authority."));
    }

    private static void AddMissingHistoricalFactionIssues(
        IReadOnlyDictionary<string, PreTurnFaction> preTurnFactions,
        IReadOnlyDictionary<string, FactionCarrier> currentFactions,
        string factionType,
        List<ValidationIssue> issues)
    {
        foreach (var (factionId, preTurn) in preTurnFactions)
        {
            if (currentFactions.ContainsKey(factionId) ||
                !preTurn.Faction.TryGetProperty(
                    FactionMaterializationContract.PropertyName,
                    out _))
            {
                continue;
            }

            issues.Add(new ValidationIssue(
                $"{preTurn.Context}.{FactionMaterializationContract.PropertyName}",
                IssueSeverity.Error,
                "Historical faction materialization receipt cannot be removed with its canonical carrier.",
                code: "faction_materialization_immutable_receipt_changed",
                actor: $"{factionType}:{factionId}",
                section: "FactionMaterialization",
                expected: "canonical faction carrier with its validated pre-turn receipt",
                actual: "canonical faction carrier missing",
                repairHint: "Restore the exact faction carrier and materialization receipt from validated pre-turn authority."));
        }
    }

    private static bool FactionJsonSemanticallyEqual(
        JsonElement left,
        JsonElement right)
    {
        try
        {
            return JsonNode.DeepEquals(
                JsonNode.Parse(left.GetRawText()),
                JsonNode.Parse(right.GetRawText()));
        }
        catch (Exception exception) when (
            exception is JsonException or
            ArgumentException or
            InvalidOperationException)
        {
            return false;
        }
    }

    private static bool ShiningFactionJsonEqualIgnoringDerivedFields(
        JsonElement left,
        JsonElement right)
    {
        try
        {
            var leftNode = JsonNode.Parse(left.GetRawText()) as JsonObject;
            var rightNode = JsonNode.Parse(right.GetRawText()) as JsonObject;
            if (leftNode == null || rightNode == null)
                return false;

            leftNode.Remove("factionStrength");
            rightNode.Remove("factionStrength");
            return JsonNode.DeepEquals(leftNode, rightNode);
        }
        catch (Exception exception) when (
            exception is JsonException or
            ArgumentException or
            InvalidOperationException)
        {
            return false;
        }
    }

    private static string? ReadNonEmptyFactionString(
        JsonElement value,
        string propertyName)
    {
        if (!value.TryGetProperty(propertyName, out var property) ||
            property.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var text = property.GetString();
        return string.IsNullOrWhiteSpace(text) ? null : text;
    }

    private static void AddUnusableFactionMaterializationPreTurnAuthorityIssue(
        string path,
        string actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Validated pre-turn faction authority is unavailable or ambiguous.",
            code: "faction_materialization_pre_turn_authority_unusable",
            section: "FactionMaterialization",
            expected: "readable duplicate-free validated pre-turn faction authority",
            actual: actual,
            repairHint: "Restore the client-owned validated pending-turn snapshot before retrying the faction mutation."));
    }

    private static void AddUnusableCurrentFactionMaterializationAuthorityIssue(
        string path,
        string factionType,
        string? factionId,
        string expected,
        string actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Current faction materialization authority is malformed or ambiguous.",
            code: "faction_materialization_current_authority_unusable",
            actor: factionId == null ? null : $"{factionType}:{factionId}",
            section: "FactionMaterialization",
            expected: expected,
            actual: actual,
            repairHint: "Restore one exact current faction identity and carrier before retrying the turn."));
    }

    private static void AddUnusableCurrentFactionMaterializationFileIssue(
        string path,
        string actual,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Current faction materialization authority is missing or unreadable.",
            code: "faction_materialization_current_authority_unusable",
            section: "FactionMaterialization",
            expected: "one readable faction authority object preserving historical carriers",
            actual: actual,
            repairHint: "Restore current faction authority from the validated pre-turn snapshot before retrying the turn."));
    }

    private static void AddExistingFactionFullResendIssue(
        string context,
        string factionType,
        string factionId,
        List<ValidationIssue> issues)
    {
        issues.Add(new ValidationIssue(
            context,
            IssueSeverity.Error,
            "An already materialized faction cannot be resent through the full Mortal carrier.",
            code: "faction_materialization_existing_full_resend_forbidden",
            actor: $"{factionType}:{factionId}",
            section: "FactionMaterialization",
            expected: "supported narrow update authority",
            actual: "full factionDataChanges carrier",
            repairHint: "Preserve the canonical faction and historical receipt; send only a supported narrow command."));
    }

    private sealed record FactionCarrier(
        JsonElement Faction,
        string Context,
        string FactionId);

    private sealed record PreTurnFaction(
        JsonElement Faction,
        string Context,
        string FactionId);
}
