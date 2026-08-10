using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

public partial class ValidationService
{
    public async Task<IReadOnlyList<ValidationIssue>>
        ValidateFactionCoreChangesBeforeNormalizationAsync()
    {
        var issues = new List<ValidationIssue>();
        var currentJson = await _fs.ReadFileAsync(
            FactionCoreChangesContract.FactionCorePath);
        if (string.IsNullOrWhiteSpace(currentJson))
            return issues;

        if (!TryParseFactionCoreChangesRoot(
                currentJson,
                currentAuthority: true,
                issues,
                out var currentRoot,
                out _))
        {
            return issues;
        }

        var topLevelNameIssues =
            FactionCoreChangesContract.ValidateCommandTopLevelNames(
                currentRoot!);
        if (topLevelNameIssues.Count > 0)
        {
            issues.AddRange(topLevelNameIssues);
            return issues;
        }

        var needsPreTurnAuthority =
            FactionCoreChangesContract.HasCommandLikeProperty(
                currentRoot!) ||
            currentRoot!["factionDataChanges"] is JsonArray
            {
                Count: > 0
            } ||
            currentRoot!["factions"] is JsonArray
            {
                Count: > 0
            };
        if (!needsPreTurnAuthority)
            return issues;

        var lookup =
            await LoadValidatedPendingTurnSnapshotLookupAsync();
        if (lookup.Status !=
                ValidatedPendingTurnSnapshotStatus.Usable ||
            lookup.Manifest == null)
        {
            issues.Add(FactionCoreChangesAuthorityIssue(
                "faction_core_changes_pre_turn_authority_unavailable",
                "Faction core changes require usable validated pre-turn Mortal faction authority.",
                DescribeValidatedPendingTurnSnapshotStatus(
                    lookup.Status)));
            return issues;
        }

        var preTurnJson =
            await ReadValidatedPendingTurnSnapshotFileAsync(
                lookup.Manifest,
                FactionCoreChangesContract.FactionCorePath);
        if (!TryParseFactionCoreChangesRoot(
                preTurnJson,
                currentAuthority: false,
                issues,
                out var preTurnRoot,
                out var preTurnActual))
        {
            if (!issues.Any(issue =>
                    issue.Code ==
                    "faction_core_changes_pre_turn_authority_unavailable"))
            {
                issues.Add(FactionCoreChangesAuthorityIssue(
                    "faction_core_changes_pre_turn_authority_unavailable",
                    "Validated pre-turn faction_core authority is missing, malformed, duplicate, or non-object.",
                    preTurnActual));
            }

            return issues;
        }

        await ValidateMortalFactionCanonicalAuthorityFenceAsync(
            issues,
            currentRoot!,
            preTurnRoot!,
            lookup.Manifest);

        var authority = await ReadFactionCoreChangesAuthorityAsync(
            currentRoot!,
            preTurnRoot!,
            lookup.Manifest);
        var evaluation = FactionCoreChangesContract.Evaluate(
            currentRoot!,
            preTurnRoot!,
            authority);
        issues.AddRange(evaluation.Issues);
        return issues;
    }

    private async Task ValidateMortalFactionCanonicalAuthorityFenceAsync(
        List<ValidationIssue> issues,
        JsonObject currentCore,
        JsonObject preTurnCore,
        ValidationPendingTurnSnapshotManifest manifest)
    {
        var preTurnFactions =
            preTurnCore["factions"] as JsonArray ?? new JsonArray();
        var preTurnFactionIds = preTurnFactions
            .OfType<JsonObject>()
            .Select(faction => GetNodeString(faction["factionId"]))
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .ToHashSet(StringComparer.Ordinal);

        if (currentCore["factions"] is JsonArray currentFactions)
        {
            foreach (var faction in currentFactions.OfType<JsonObject>())
            {
                var factionId = GetNodeString(faction["factionId"]);
                if (string.IsNullOrWhiteSpace(factionId))
                    continue;

                var preTurnMatches = preTurnFactions
                    .OfType<JsonObject>()
                    .Where(candidate => string.Equals(
                        GetNodeString(candidate["factionId"]),
                        factionId,
                        StringComparison.Ordinal))
                    .ToArray();
                if (preTurnMatches.Length == 0)
                {
                    issues.Add(MortalFactionDirectAuthorityIssue(
                        $"{FactionCoreChangesContract.FactionCorePath}.factions",
                        "faction_materialization_mortal_direct_canonical_creation_forbidden",
                        factionId,
                        FactionTouchKind.New,
                        "A new Mortal faction was written directly to canonical factions[]."));
                    continue;
                }

                if (!preTurnMatches.Any(candidate =>
                        JsonNode.DeepEquals(candidate, faction)))
                {
                    issues.Add(MortalFactionDirectAuthorityIssue(
                        $"{FactionCoreChangesContract.FactionCorePath}.factions",
                        "faction_materialization_mortal_direct_canonical_mutation_forbidden",
                        factionId,
                        FactionTouchKind.AlreadyMaterialized,
                        "An existing Mortal faction canonical object differs from its validated pre-turn baseline."));
                }
            }
        }

        foreach (var carrier in MortalFactionCanonicalSidecarCarriers)
        {
            var currentJson = await _fs.ReadFileAsync(carrier.Path);
            if (!TryParseUniqueJsonNode(currentJson, out var currentNode) ||
                currentNode is not JsonObject currentRoot)
            {
                continue;
            }

            var preTurnJson =
                await ReadValidatedPendingTurnSnapshotFileAsync(
                    manifest,
                    carrier.Path);
            _ = TryParseUniqueJsonNode(preTurnJson, out var preTurnNode);
            var preTurnRoot = preTurnNode as JsonObject;

            foreach (var propertyName in carrier.CanonicalProperties)
            {
                if (currentRoot[propertyName] is not JsonArray currentEntries)
                    continue;
                var preTurnEntries =
                    preTurnRoot?[propertyName] as JsonArray ?? new JsonArray();
                var index = 0;
                foreach (var entry in currentEntries.OfType<JsonObject>())
                {
                    var factionId = GetNodeString(entry["factionId"]);
                    if (string.IsNullOrWhiteSpace(factionId))
                    {
                        index++;
                        continue;
                    }

                    if (!preTurnEntries
                            .OfType<JsonObject>()
                            .Any(candidate =>
                                SameMortalFactionSidecarIdentity(
                                    propertyName,
                                    candidate,
                                    entry) &&
                                JsonNode.DeepEquals(candidate, entry)))
                    {
                        issues.Add(MortalFactionDirectAuthorityIssue(
                            $"{carrier.Path}.{propertyName}[{index}]",
                            "faction_materialization_mortal_direct_sidecar_mutation_forbidden",
                            factionId,
                            preTurnFactionIds.Contains(factionId)
                                ? FactionTouchKind.AlreadyMaterialized
                                : FactionTouchKind.New,
                            "Mortal faction sidecar state was created or changed outside its canonical command carrier.",
                            carrier.Path));
                    }

                    index++;
                }
            }
        }
    }

    private static bool SameMortalFactionSidecarIdentity(
        string propertyName,
        JsonObject left,
        JsonObject right)
    {
        if (!string.Equals(
                GetNodeString(left["factionId"]),
                GetNodeString(right["factionId"]),
                StringComparison.Ordinal))
        {
            return false;
        }

        if (propertyName is not ("activeProjects" or "completedProjects"))
            return true;

        return string.Equals(
            GetNodeString(left["projectId"]),
            GetNodeString(right["projectId"]),
            StringComparison.Ordinal);
    }

    private static ValidationIssue MortalFactionDirectAuthorityIssue(
        string filePath,
        string code,
        string factionId,
        FactionTouchKind classification,
        string message,
        string? sidecarPath = null) =>
        new(
            filePath,
            IssueSeverity.Error,
            message,
            classification,
            code: code,
            actor: $"mortal_faction:{factionId}",
            section: "FactionMaterialization",
            expected:
                "exact validated pre-turn canonical baseline; new factions only through factionDataChanges; existing changes only through canonical command arrays",
            actual: "unexplained current canonical state",
            repairHint:
                "Restore the exact pre-turn carrier and express the intended change through factionDataChanges or the matching narrow command array.",
            repairTargetFiles: sidecarPath == null
                ? new[] { FactionCoreChangesContract.FactionCorePath }
                : new[]
                {
                    FactionCoreChangesContract.FactionCorePath,
                    sidecarPath
                });

    private static readonly MortalFactionCanonicalSidecarCarrier[]
        MortalFactionCanonicalSidecarCarriers =
        {
            new(
                MortalFactionStructurePath,
                new[] { "entries" }),
            new(
                MortalFactionResourcesPath,
                new[] { "entries" }),
            new(
                MortalFactionProjectsPath,
                new[] { "activeProjects", "completedProjects" }),
            new(
                MortalFactionCustomPath,
                new[] { "entries" }),
            new(
                MortalFactionChroniclesPath,
                new[] { "entries" })
        };

    private sealed record MortalFactionCanonicalSidecarCarrier(
        string Path,
        IReadOnlyList<string> CanonicalProperties);

    private bool TryParseFactionCoreChangesRoot(
        string? json,
        bool currentAuthority,
        List<ValidationIssue> issues,
        out JsonObject? root,
        out string actual)
    {
        root = null;
        actual = "missing";
        if (string.IsNullOrWhiteSpace(json))
        {
            if (currentAuthority)
            {
                issues.Add(FactionCoreChangesAuthorityIssue(
                    "faction_core_changes_invalid_json",
                    "Current faction_core authority must be one readable JSON object.",
                    actual));
            }

            return false;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind !=
                JsonValueKind.Object)
            {
                actual =
                    $"non-object root ({document.RootElement.ValueKind})";
                if (currentAuthority)
                {
                    issues.Add(FactionCoreChangesAuthorityIssue(
                        "faction_core_changes_invalid_json",
                        "Current faction_core authority must be one readable JSON object.",
                        actual));
                }

                return false;
            }

            if (TryFindDuplicateJsonProperty(
                    document.RootElement,
                    out var duplicatePath))
            {
                actual = $"duplicate property {duplicatePath}";
                issues.Add(new ValidationIssue(
                    currentAuthority
                        ? $"{FactionCoreChangesContract.FactionCorePath}{duplicatePath}"
                        : FactionCoreChangesContract.FactionCorePath,
                    IssueSeverity.Error,
                    currentAuthority
                        ? "Current faction_core authority contains a duplicate JSON property."
                        : "Validated pre-turn faction_core authority contains a duplicate JSON property.",
                    code: currentAuthority
                        ? "faction_core_changes_duplicate_property"
                        : "faction_core_changes_pre_turn_authority_unavailable",
                    section: "FactionCoreChanges",
                    expected: "unique JSON property names",
                    actual: duplicatePath,
                    repairHint:
                    "Restore one unambiguous faction_core authority value for every property before retrying the turn."));
                return false;
            }

            root = JsonNode.Parse(
                    document.RootElement.GetRawText())!
                .AsObject();
            actual = "usable";
            return true;
        }
        catch (Exception exception) when (
            exception is JsonException or
            ArgumentException or
            InvalidOperationException)
        {
            actual = "malformed";
            if (currentAuthority)
            {
                issues.Add(FactionCoreChangesAuthorityIssue(
                    "faction_core_changes_invalid_json",
                    "Current faction_core authority must be one readable JSON object.",
                    actual));
            }

            return false;
        }
    }

    private async Task<FactionCoreChangesContract.Authority>
        ReadFactionCoreChangesAuthorityAsync(
            JsonObject currentRoot,
            JsonObject preTurnRoot,
            ValidationPendingTurnSnapshotManifest manifest)
    {
        var factionIds = new HashSet<string>(
            StringComparer.Ordinal);
        CollectFactionCoreChangesFactionIds(
            currentRoot,
            factionIds);
        CollectFactionCoreChangesFactionIds(
            preTurnRoot,
            factionIds);

        var npcIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var json in new[]
                 {
                     await _fs.ReadFileAsync(
                         "game_state/npcs/npc_core.json"),
                     await ReadValidatedPendingTurnSnapshotFileAsync(
                         manifest,
                         "game_state/npcs/npc_core.json")
                 })
        {
            if (!TryParseUniqueJsonNode(json, out var root))
                continue;
            FactionCoreChangesContract.CollectKnownMortalNpcIds(
                root,
                npcIds);
        }

        return new FactionCoreChangesContract.Authority(
            factionIds,
            npcIds);
    }

    private bool TryParseUniqueJsonNode(
        string? json,
        out JsonNode? node)
    {
        node = null;
        if (string.IsNullOrWhiteSpace(json))
            return false;
        try
        {
            using var document = JsonDocument.Parse(json);
            if (TryFindDuplicateJsonProperty(
                    document.RootElement,
                    out _))
            {
                return false;
            }

            node = JsonNode.Parse(
                document.RootElement.GetRawText());
            return node != null;
        }
        catch (Exception exception) when (
            exception is JsonException or
            ArgumentException or
            InvalidOperationException)
        {
            return false;
        }
    }

    private static void CollectFactionCoreChangesFactionIds(
        JsonObject root,
        HashSet<string> result)
    {
        foreach (var section in new[]
                 {
                     "factions",
                     "factionDataChanges"
                 })
        {
            if (root[section] is not JsonArray factions)
                continue;
            foreach (var faction in factions.OfType<JsonObject>())
            {
                if (TryReadFactionCoreChangesString(
                        faction["factionId"],
                        out var factionId))
                {
                    result.Add(factionId!);
                }
            }
        }
    }

    private static bool TryReadFactionCoreChangesString(
        JsonNode? node,
        out string? value)
    {
        value = null;
        if (node is not JsonValue jsonValue ||
            !jsonValue.TryGetValue<string>(out var candidate) ||
            string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        value = candidate;
        return true;
    }

    private static ValidationIssue
        FactionCoreChangesAuthorityIssue(
            string code,
            string message,
            string actual) =>
        new(
            FactionCoreChangesContract.FactionCorePath,
            IssueSeverity.Error,
            message,
            code: code,
            section: "FactionCoreChanges",
            expected:
            "readable current and validated pre-turn faction_core objects with unique properties",
            actual: actual,
            repairHint:
            "Restore exact validated pre-turn faction authority and preserve the pending factionCoreChanges command for bounded repair.");
}
