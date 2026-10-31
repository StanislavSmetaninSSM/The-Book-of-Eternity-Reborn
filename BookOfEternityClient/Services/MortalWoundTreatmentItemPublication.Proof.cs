using System.Globalization;
using System.Text.Json.Nodes;

namespace BookOfEternityClient.Services;

internal sealed partial class MortalWoundTreatmentItemPublicationAuthority
{
    internal IReadOnlyDictionary<string, JsonNode>
        CloneExactPublicationAfterImages()
    {
        if (!HasValidSeal())
        {
            throw new InvalidDataException(
                "Treatment item publication authority has no valid seal.");
        }

        var exact = new Dictionary<string, JsonNode>(StringComparer.Ordinal);
        foreach (var path in _publicationAfterImages.Keys.OrderBy(
                     static path => path,
                     StringComparer.Ordinal))
        {
            if (!_finalRoots.TryGetValue(path, out var finalRoot) ||
                finalRoot is null ||
                finalRoot is not JsonObject &&
                (finalRoot is not JsonArray ||
                 !string.Equals(
                     path,
                     StorageTransportMoveService.VehiclesPath,
                     StringComparison.Ordinal)))
            {
                throw new InvalidDataException(
                    $"Treatment item publication authority has no exact final root for '{path}'.");
            }

            exact.Add(path, finalRoot.DeepClone());
        }

        if (exact.Count != _publicationAfterImages.Count)
        {
            throw new InvalidDataException(
                "Treatment item publication authority exact-root keys changed.");
        }

        return exact;
    }

    internal IReadOnlyList<ValidationIssue> ValidateCandidate(
        AcceptedMechanicsPlan candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (!HasValidSeal())
            return new[] { Issue("authority", "sealed genuine item authority", "changed") };
        if (!MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                _baselineOwnerAuthority,
                _terminalOwners,
                candidate.OwnerAuthority))
        {
            return new[] { Issue("owners", FinalOwnerAuthority.Fingerprint,
                candidate.OwnerAuthority.Fingerprint) };
        }
        var candidateAfterImages = candidate.OwnerCompanionAfterImages;
        foreach (var pair in _publicationAfterImages)
        {
            if (!candidateAfterImages.TryGetValue(pair.Key, out var actual) ||
                !JsonNode.DeepEquals(pair.Value, actual) ||
                !candidate.TouchedPaths.Contains(pair.Key, StringComparer.Ordinal))
            {
                return new[] { Issue(pair.Key, "exact baseline-to-skill-to-item root",
                    "missing or changed") };
            }
        }
        return Array.Empty<ValidationIssue>();
    }

    internal bool MatchesNormalizationSnapshot(
        MortalItemAcceptedTurnNormalizationSnapshot snapshot,
        ResourceOwnerAuthority ownerAuthority)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(ownerAuthority);
        var finalOwnerInput = FinalOwnerAuthority.ExportInput();
        return HasValidSeal() &&
               snapshot.MatchesFinalPublicationBaseline(_snapshot) &&
               snapshot.MatchesAcceptedOwnerAuthority(_baselineOwnerAuthority) &&
               _terminalOwners.All(owner =>
                   _baselineOwnerAuthority.Entries.ContainsKey(owner) &&
                   !FinalOwnerAuthority.Entries.ContainsKey(owner) &&
                   finalOwnerInput.HistoricalOwners.Contains(owner)) &&
               MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                   _baselineOwnerAuthority,
                   _terminalOwners,
                   FinalOwnerAuthority) &&
               string.Equals(
                   ownerAuthority.Fingerprint,
                   FinalOwnerAuthority.Fingerprint,
                   StringComparison.Ordinal) &&
               MortalWoundTreatmentItemOwnerSuccessor.IsExactSuccessor(
                   _baselineOwnerAuthority,
                   _terminalOwners,
                   ownerAuthority);
    }

    internal bool ProvesNpcRootTransition(
        JsonObject skillAfterImage,
        JsonObject finalRoot) =>
        TryRecompose(
            out var recomposedSkillAfterImages,
            out var recomposedFinalRoots,
            out _) &&
        WoundAcceptedTurnPlanner.TryReadTreatmentSkillProjection(
            _continuationAuthority,
            PublicationReservationAuthority,
            out _,
            out var sealedSkillAfterImages,
            out var sealedSkillFingerprint) &&
        string.Equals(
            sealedSkillFingerprint,
            SkillProjectionFingerprint,
            StringComparison.Ordinal) &&
        sealedSkillAfterImages.TryGetValue(
            NpcCoreChangesContract.NpcCorePath,
            out var sealedSuppliedSkillRoot) &&
        recomposedSkillAfterImages.ContainsKey(
            NpcCoreChangesContract.NpcCorePath) &&
        recomposedFinalRoots.TryGetValue(
            NpcCoreChangesContract.NpcCorePath,
            out var expectedFinal) &&
        JsonNode.DeepEquals(sealedSuppliedSkillRoot, skillAfterImage) &&
        JsonNode.DeepEquals(expectedFinal, finalRoot);

    internal bool ProvesNormalizedEffectSourceTransition(
        string beforeFingerprint,
        string normalizedFingerprint) =>
        HasValidSeal() &&
        TryRecomposeEffectSourceSuccessor(
            out var recomposedBeforeFingerprint,
            out var recomposedNormalizedFingerprint) &&
        string.Equals(
            beforeFingerprint,
            recomposedBeforeFingerprint,
            StringComparison.Ordinal) &&
            string.Equals(
                normalizedFingerprint,
                recomposedNormalizedFingerprint,
                StringComparison.Ordinal);

    internal bool ProvesExactPublishedNpcInventoryContinuityIssue(
        ValidationIssue issue, JsonObject publishedNpcRoot)
    {
        ArgumentNullException.ThrowIfNull(issue);
        ArgumentNullException.ThrowIfNull(publishedNpcRoot);
        if (issue.Severity != IssueSeverity.Error || issue.Code != "npc_existing_inventory_resend_forbidden" ||
            issue.Section != "NPCInventory" || !TryParseNpcInventoryIssuePath(issue.FilePath, out _, out _))
            return false;
        return CaptureExactPublishedNpcInventoryComparison(publishedNpcRoot)?.Proves(issue) == true;
    }

    /// <summary>
    /// Recomposes the genuine complete item transition once and returns immutable
    /// comparison DATA. It grants no current publication or inventory authority.
    /// </summary>
    /// <param name="publishedNpcRoot">The complete actual published NPC image.</param>
    /// <returns>One exact comparison, or null for a changed/foreign root.</returns>
    internal NpcInventoryContinuityComparison? CaptureExactPublishedNpcInventoryComparison(JsonObject publishedNpcRoot)
    {
        ArgumentNullException.ThrowIfNull(publishedNpcRoot);
        if (!TryRecompose(out var skillAfterImages, out var finalRoots, out var publicationAfterImages) ||
            finalRoots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) is not JsonObject finalNpcRoot ||
            !publicationAfterImages.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var publicationNpcRoot) ||
            !JsonNode.DeepEquals(finalNpcRoot, publicationNpcRoot) || !JsonNode.DeepEquals(finalNpcRoot, publishedNpcRoot))
            return null;
        var skillNpcRoot = skillAfterImages.TryGetValue(NpcCoreChangesContract.NpcCorePath, out var skillAfterImage)
            ? skillAfterImage : _baseline.FinalCarrierRoots.GetValueOrDefault(NpcCoreChangesContract.NpcCorePath) as JsonObject;
        return skillNpcRoot is null ? null : new NpcInventoryContinuityComparison(skillNpcRoot, finalNpcRoot);
    }

    /// <summary>Closed before/after inventory comparison DATA; it cannot mint an execution or publication permit.</summary>
    internal sealed class NpcInventoryContinuityComparison
    {
        private readonly JsonObject _before;
        private readonly JsonObject _after;

        /// <summary>Copies the two complete already recomposed roots for this comparison.</summary>
        /// <param name="before">The actual skill/item baseline.</param>
        /// <param name="after">The actual complete item publication image.</param>
        internal NpcInventoryContinuityComparison(JsonObject before, JsonObject after)
        { _before = before.DeepClone().AsObject(); _after = after.DeepClone().AsObject(); }

        /// <summary>Checks the unchanged exact issue/actor/path/before/after inventory contract.</summary>
        /// <param name="issue">A candidate emitted by the ordinary inventory guard.</param>
        /// <returns>True only for this exact client-owned inventory transition.</returns>
        internal bool Proves(ValidationIssue issue)
        {
            ArgumentNullException.ThrowIfNull(issue);
            if (issue.Severity != IssueSeverity.Error || issue.Code != "npc_existing_inventory_resend_forbidden" ||
                issue.Section != "NPCInventory" || !TryParseNpcInventoryIssuePath(issue.FilePath, out var section, out var index) ||
                !TryReadNpcInventoryAtExactPath(_before, section, index, out var beforeActorId, out var beforeInventory) ||
                !TryReadNpcInventoryAtExactPath(_after, section, index, out var finalActorId, out var finalInventory) ||
                beforeActorId != finalActorId || issue.Actor != "mortal_npc:" + finalActorId ||
                JsonNode.DeepEquals(beforeInventory, finalInventory) ||
                !TryParseExactInventory(issue.Expected, out var expected) || !TryParseExactInventory(issue.Actual, out var actual))
                return false;
            return JsonNode.DeepEquals(beforeInventory, expected) && JsonNode.DeepEquals(finalInventory, actual);
        }
    }

    private static bool TryParseNpcInventoryIssuePath(
        string path,
        out string section,
        out int index)
    {
        section = string.Empty;
        index = -1;
        foreach (var candidate in new[] { "UpdateNPCs", "NPCsInScene" })
        {
            var prefix = NpcCoreChangesContract.NpcCorePath + "." + candidate + "[";
            const string suffix = "].inventory";
            if (!path.StartsWith(prefix, StringComparison.Ordinal) ||
                !path.EndsWith(suffix, StringComparison.Ordinal))
            {
                continue;
            }

            var indexText = path[prefix.Length..^suffix.Length];
            if (!int.TryParse(
                    indexText,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out var parsed) ||
                parsed < 0 ||
                !string.Equals(
                    indexText,
                    parsed.ToString(CultureInfo.InvariantCulture),
                    StringComparison.Ordinal))
            {
                return false;
            }

            section = candidate;
            index = parsed;
            return true;
        }

        return false;
    }

    private static bool TryReadNpcInventoryAtExactPath(
        JsonObject root,
        string section,
        int index,
        out string actorId,
        out JsonArray inventory)
    {
        actorId = string.Empty;
        inventory = null!;
        if (root[section] is not JsonArray actors ||
            index >= actors.Count ||
            actors[index] is not JsonObject actor ||
            actor["inventory"] is not JsonArray exactInventory ||
            !TryReadExactNpcActorId(actor, out actorId))
        {
            return false;
        }

        inventory = exactInventory;
        return true;
    }

    private static bool TryReadExactNpcActorId(
        JsonObject actor,
        out string actorId)
    {
        actorId = string.Empty;
        string? current = null;
        foreach (var property in new[] { "NPCId", "npcId", "id" })
        {
            if (actor[property] is null)
                continue;
            if (actor[property] is not JsonValue value ||
                !value.TryGetValue<string>(out var candidate) ||
                string.IsNullOrWhiteSpace(candidate) ||
                current is not null &&
                !string.Equals(current, candidate, StringComparison.Ordinal))
            {
                return false;
            }
            current = candidate;
        }

        actorId = current ?? string.Empty;
        return actorId.Length > 0;
    }

    private static bool TryParseExactInventory(
        string? json,
        out JsonArray inventory)
    {
        inventory = null!;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            if (JsonNode.Parse(json) is not JsonArray parsed)
                return false;
            inventory = parsed;
            return true;
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
