namespace BookOfEternityClient.Services;

internal sealed record MortalItemRepairContext(
    string Coordinate,
    string TransitionClass,
    string? Route,
    MortalItemCarrierCoordinate? SourceCarrier,
    MortalItemCarrierCoordinate? DestinationCarrier,
    string? ExpectedAuthority,
    string? ActualEvidence,
    IReadOnlyList<string> RequiredCompanionTargets);

internal sealed record MortalItemRepairExactFieldCorrection(
    string Path,
    string Expected,
    string Actual,
    string Code,
    string RepairHint);

internal sealed class MortalItemRepairPacket
{
    internal string Kind { get; init; } = "";
    internal string Priority { get; init; } = "critical";
    internal string Title { get; init; } = "";
    internal IReadOnlyList<string> TargetFiles { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> TemplateRefs { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> CanonicalActorNames { get; init; } = Array.Empty<string>();
    internal string TransitionClass { get; init; } = "unresolved";
    internal string? Route { get; init; }
    internal MortalItemCarrierCoordinate? SourceCarrier { get; init; }
    internal MortalItemCarrierCoordinate? DestinationCarrier { get; init; }
    internal IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<MortalItemRepairExactFieldCorrection> ExactFieldCorrections { get; init; } =
        Array.Empty<MortalItemRepairExactFieldCorrection>();
    internal IReadOnlyList<string> RequiredCompanionTargets { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ExpectedAuthority { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ActualEvidence { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ExpectedShape { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> SafeCorrectionRules { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> DoNotDo { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Converts Mortal item validation evidence into exact-coordinate, bounded GM
/// repair packets. Client-owned item_identity_index.json, receipt, seal, and
/// transition authority is never a GM repair target, never enters a GM packet,
/// and instead requires fail-closed rollback.
/// </summary>
internal static class MortalItemRepairPacketBuilder
{
    private const string MaterializationPacketKind =
        "mortal_item_materialization_repair";
    private const string IdentityPacketKind =
        "mortal_item_identity_authority_repair";

    private static readonly string[] GmAuthorableTargetRoots =
    {
        "game_state/inventory/items.json",
        "game_state/inventory/item_resources.json",
        "game_state/inventory/item_bonds.json",
        "game_state/inventory/item_text_updates.json",
        "game_state/inventory/recipes.json",
        "game_state/inventory/item_movements.json",
        "game_state/inventory/item_removals.json",
        "game_state/inventory/storage_operations.json",
        "game_state/npcs/npc_core.json",
        "game_state/npcs/npc_inventory.json",
        "game_state/npcs/item_journals.json",
        "game_state/world/current_location.json",
        "game_state/misc/vehicles.json",
        "game_state/quests/regular_quests.json",
        "game_state/quests/quest_history.json"
    };

    private static readonly HashSet<string> CompanionTargetRoots =
        new(StringComparer.Ordinal)
        {
            "game_state/inventory/item_resources.json",
            "game_state/inventory/item_bonds.json",
            "game_state/inventory/item_text_updates.json",
            "game_state/inventory/recipes.json",
            "game_state/npcs/item_journals.json",
            "game_state/quests/regular_quests.json",
            "game_state/quests/quest_history.json"
        };

    private static readonly HashSet<string> GlobalIdentityCodes =
        new(StringComparer.Ordinal)
        {
            "mortal_item_materialization_identity_ambiguity",
            "mortal_item_materialization_duplicate_item_id",
            "mortal_item_materialization_duplicate_receipt_id",
            "mortal_item_materialization_duplicate_materialization_id",
            "mortal_item_materialization_duplicate_creation_ref"
        };

    private static readonly HashSet<string> FailClosedHistoryCodes =
        new(StringComparer.Ordinal)
        {
            "mortal_item_materialization_creation_replay",
            "mortal_item_materialization_historical_identity_ambiguity"
        };

    internal static IReadOnlyList<MortalItemRepairPacket> Build(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var materializedIssues = issues.ToArray();
        if (materializedIssues.Any(issue =>
                IsMortalItemIssue(issue) && IsFailClosedHistoryIssue(issue)))
        {
            return Array.Empty<MortalItemRepairPacket>();
        }

        var actionable = materializedIssues
            .Where(IsMortalItemIssue)
            .Where(issue => !IsProtectedClientOwnedIssue(issue))
            .Where(issue => !IsFailClosedHistoryIssue(issue))
            .Where(issue => IsExactItemCoordinate(ResolveCoordinate(issue)))
            .ToArray();
        if (actionable.Length == 0)
            return Array.Empty<MortalItemRepairPacket>();

        var packets = new List<MortalItemRepairPacket>();
        var identityIssues = actionable
            .Where(IsGlobalIdentityIssue)
            .ToArray();
        if (identityIssues.Length > 0)
        {
            var packet = BuildPacket(
                IdentityPacketKind,
                "Mortal item identity authority conflict",
                identityIssues,
                identityAuthority: true);
            if (packet != null)
                packets.Add(packet);
        }

        foreach (var group in actionable
                     .Where(issue => !IsGlobalIdentityIssue(issue))
                     .GroupBy(ResolveCoordinate, StringComparer.Ordinal)
                     .OrderBy(group => group.Key, StringComparer.Ordinal))
        {
            var packet = BuildPacket(
                MaterializationPacketKind,
                $"Repair exact Mortal item package {group.Key}",
                group.ToArray(),
                identityAuthority: false);
            if (packet != null)
                packets.Add(packet);
        }

        return packets;
    }

    internal static bool RequiresClientOwnedRollback(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        return issues.Any(issue =>
            IsMortalItemIssue(issue) && IsProtectedClientOwnedIssue(issue));
    }

    internal static bool RequiresFailClosedRollback(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        var itemIssues = issues.Where(IsMortalItemIssue).ToArray();
        if (itemIssues.Length == 0)
            return false;
        if (itemIssues.Any(IsProtectedClientOwnedIssue) ||
            itemIssues.Any(IsFailClosedHistoryIssue) ||
            itemIssues.Any(issue => !IsExactItemCoordinate(ResolveCoordinate(issue))))
        {
            return true;
        }

        var packetCoordinates = Build(itemIssues)
            .SelectMany(packet => packet.CanonicalActorNames)
            .ToHashSet(StringComparer.Ordinal);
        return itemIssues.Any(issue =>
            !packetCoordinates.Contains(ResolveCoordinate(issue)));
    }

    internal static bool IsGmAuthorableTarget(string path) =>
        TryNormalizeGmTarget(path) != null;

    internal static bool IsProtectedClientOwnedTarget(string path)
    {
        var normalized = Normalize(path);
        return IsRootOrDescendant(
            normalized,
            MortalItemIdentityState.StatePath,
            StringComparison.OrdinalIgnoreCase);
    }

    private static MortalItemRepairPacket? BuildPacket(
        string kind,
        string title,
        IReadOnlyList<ValidationIssue> issues,
        bool identityAuthority)
    {
        var contexts = issues
            .Select(issue => issue.MortalItemRepairContext)
            .Where(context => context != null)
            .Cast<MortalItemRepairContext>()
            .ToArray();
        var coordinates = issues
            .Select(ResolveCoordinate)
            .Concat(contexts.Select(context => context.Coordinate))
            .Where(IsExactItemCoordinate)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (coordinates.Length == 0)
            return null;

        var targetFiles = issues
            .SelectMany(issue => issue.RepairTargetFiles.Append(issue.FilePath))
            .Select(TryNormalizeGmTarget)
            .Where(path => path != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (targetFiles.Length == 0)
            return null;
        var companionTargets = contexts
            .SelectMany(context => context.RequiredCompanionTargets)
            .Concat(targetFiles.Where(CompanionTargetRoots.Contains))
            .Select(TryNormalizeGmTarget)
            .Where(path => path != null && CompanionTargetRoots.Contains(path))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        return new MortalItemRepairPacket
        {
            Kind = kind,
            Priority = "critical",
            Title = title,
            CanonicalActorNames = coordinates,
            TransitionClass = identityAuthority
                ? "identity_authority"
                : ResolveUniqueValue(contexts.Select(context => context.TransitionClass)) ??
                  ResolveDefaultTransition(coordinates),
            Route = identityAuthority
                ? null
                : ResolveUniqueValue(contexts.Select(context => context.Route)),
            SourceCarrier = identityAuthority
                ? null
                : ResolveUniqueCarrier(contexts.Select(context => context.SourceCarrier)),
            DestinationCarrier = identityAuthority
                ? null
                : ResolveUniqueCarrier(contexts.Select(context => context.DestinationCarrier)),
            TargetFiles = targetFiles,
            MissingFields = issues
                .Where(IsMissingFieldIssue)
                .Select(issue => Bound(issue.FilePath))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(path => path, StringComparer.Ordinal)
                .ToArray(),
            ExactFieldCorrections = issues
                .OrderBy(issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code ?? string.Empty, StringComparer.Ordinal)
                .Select(issue => new MortalItemRepairExactFieldCorrection(
                    Bound(issue.FilePath),
                    Bound(issue.Expected ?? "valid exact GM-authored value"),
                    Bound(issue.Actual ?? "missing"),
                    Bound(issue.Code ?? "mortal_item_materialization_invalid"),
                    Bound(issue.RepairHint ?? "Repair only this exact GM-owned field.")))
                .ToArray(),
            RequiredCompanionTargets = companionTargets,
            ExpectedAuthority = contexts
                .Select(context => context.ExpectedAuthority)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Cast<string>()
                .Select(Bound)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            ActualEvidence = issues
                .Select(issue => !string.IsNullOrWhiteSpace(issue.MortalItemRepairContext?.ActualEvidence)
                    ? issue.MortalItemRepairContext!.ActualEvidence!
                    : $"{issue.FilePath} | {issue.Code ?? "mortal_item_materialization_invalid"} | {issue.Actual ?? "missing"}")
                .Select(Bound)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            TemplateRefs = new[]
            {
                "specs/1511-complete-item-materialization/contracts/mortal-item-materialization-envelope.md",
                "specs/1511-complete-item-materialization/contracts/mortal-item-repair-packet.md"
            },
            ExpectedShape = new[]
            {
                "One complete GM-authored item/envelope package for the exact coordinate.",
                "One valid route authority and one real destination carrier.",
                "Every populated or empty_by_design section agrees with its semantic and companion evidence.",
                "No GM-authored permanent itemId, materializationReceipt, seal, index entry, or transition."
            },
            SafeCorrectionRules = new[]
            {
                "Edit only listed exact fields in listed GM-owned targetFiles.",
                "Preserve the accepted request/transaction identity and all unrelated item state.",
                "Keep the validated pre-turn snapshot authoritative until post-seal validation succeeds."
            },
            Steps = new[]
            {
                "Open validation_repair_request.json and this packet's targetFiles/templateRefs.",
                "Resolve the item only by the exact creationRef or itemId in canonicalActorNames.",
                "Apply exactFieldCorrections and requiredCompanionTargets without replaying settlement.",
                "Rerun raw Mortal item materialization validation, then signal Complete-BoeValidationRepair only after the whole package passes."
            },
            DoNotDo = new[]
            {
                $"Do not author or edit {MortalItemIdentityState.StatePath}, itemId, materializationReceipt, receiptId, seal, or transitions.",
                "Do not replace a whole carrier or repair another same-name item.",
                "Do not replay a grant, payment, ingredient consumption, quest completion, or quantity change.",
                "Do not use display name, casing aliases, prose, tags, or genre inference as identity or authority."
            }
        };
    }

    private static bool IsMortalItemIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("mortal_item_materialization_", StringComparison.Ordinal) == true ||
        issue.Code?.StartsWith("mortal_item_identity_", StringComparison.Ordinal) == true ||
        string.Equals(issue.Section, "MortalItemMaterialization", StringComparison.Ordinal) ||
        string.Equals(issue.Section, "MortalItemIdentity", StringComparison.Ordinal);

    private static bool IsGlobalIdentityIssue(ValidationIssue issue)
        => issue.Code != null && GlobalIdentityCodes.Contains(issue.Code);

    private static string ResolveCoordinate(ValidationIssue issue)
    {
        var contextCoordinate = issue.MortalItemRepairContext?.Coordinate;
        if (IsExactItemCoordinate(contextCoordinate))
            return contextCoordinate!;
        return IsExactItemCoordinate(issue.Actor) ? issue.Actor! : string.Empty;
    }

    private static bool IsExactItemCoordinate(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return false;
        const string newPrefix = "mortal_item:new:";
        const string existingPrefix = "mortal_item:existing:";
        var identity = value.StartsWith(newPrefix, StringComparison.Ordinal)
            ? value[newPrefix.Length..]
            : value.StartsWith(existingPrefix, StringComparison.Ordinal)
                ? value[existingPrefix.Length..]
                : string.Empty;
        return MortalItemIdentityRules.IsExactIdentity(identity);
    }

    private static bool IsFailClosedHistoryIssue(ValidationIssue issue) =>
        issue.Code != null && FailClosedHistoryCodes.Contains(issue.Code);

    private static bool IsProtectedClientOwnedIssue(ValidationIssue issue)
    {
        if (issue.Category == IssueCategory.ClientOwnedSurface ||
            IsProtectedClientOwnedTarget(issue.FilePath) ||
            string.Equals(issue.Actor, "mortal_item:index", StringComparison.Ordinal) ||
            string.Equals(
                issue.MortalItemRepairContext?.Coordinate,
                "mortal_item:identity_authority",
                StringComparison.Ordinal))
        {
            return true;
        }

        var code = issue.Code ?? string.Empty;
        if (code.StartsWith("mortal_item_identity_", StringComparison.Ordinal) ||
            code.Contains("_receipt", StringComparison.Ordinal) ||
            code.Contains("_index_", StringComparison.Ordinal) ||
            string.Equals(
                code,
                "mortal_item_materialization_missing_index_entry",
                StringComparison.Ordinal) ||
            string.Equals(
                code,
                "mortal_item_materialization_orphan_index_entry",
                StringComparison.Ordinal))
        {
            return true;
        }

        return HasExactPropertySegment(issue.FilePath, MortalItemMaterializationContract.ReceiptProperty) ||
               HasExactPropertySegment(issue.FilePath, "receiptId") ||
               HasExactPropertySegment(issue.FilePath, "seal") ||
               HasExactPropertySegment(issue.FilePath, "transitions");
    }

    private static bool HasExactPropertySegment(string path, string property)
    {
        var normalized = Normalize(path);
        var marker = "." + property;
        var index = normalized.IndexOf(marker, StringComparison.Ordinal);
        while (index >= 0)
        {
            var boundary = index + marker.Length;
            if (boundary == normalized.Length || normalized[boundary] is '.' or '[' or ':')
                return true;
            index = normalized.IndexOf(marker, index + 1, StringComparison.Ordinal);
        }

        return false;
    }

    private static string ResolveDefaultTransition(IReadOnlyList<string> coordinates) =>
        coordinates.All(coordinate => coordinate.StartsWith("mortal_item:new:", StringComparison.Ordinal))
            ? "create"
            : "unresolved";

    private static bool IsMissingFieldIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(issue.Actual, "missing", StringComparison.OrdinalIgnoreCase) ||
               code.EndsWith("_missing", StringComparison.Ordinal) ||
               code.Contains("_missing_", StringComparison.Ordinal);
    }

    private static string? ResolveUniqueValue(IEnumerable<string?> values)
    {
        var distinct = values
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return distinct.Length == 1 ? distinct[0] : null;
    }

    private static MortalItemCarrierCoordinate? ResolveUniqueCarrier(
        IEnumerable<MortalItemCarrierCoordinate?> carriers)
    {
        var distinct = carriers
            .Where(carrier => carrier != null)
            .Cast<MortalItemCarrierCoordinate>()
            .GroupBy(CarrierKey, StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        return distinct.Length == 1 ? distinct[0].First() : null;
    }

    private static string CarrierKey(MortalItemCarrierCoordinate carrier) =>
        string.Join(
            "\u001f",
            carrier.Kind,
            carrier.OwnerId,
            carrier.ContainerId ?? "",
            string.Join("\u001e", carrier.ContainerPath));

    private static string? TryNormalizeGmTarget(string path)
    {
        var normalized = Normalize(path);
        if (IsProtectedClientOwnedTarget(normalized))
            return null;
        return GmAuthorableTargetRoots.FirstOrDefault(root =>
            IsRootOrDescendant(normalized, root));
    }

    private static string Normalize(string path) =>
        (path ?? string.Empty).Trim().Replace('\\', '/');

    private static bool IsRootOrDescendant(string path, string root) =>
        IsRootOrDescendant(path, root, StringComparison.Ordinal);

    private static bool IsRootOrDescendant(
        string path,
        string root,
        StringComparison comparison) =>
        string.Equals(path, root, comparison) ||
        path.StartsWith(root + ".", comparison) ||
        path.StartsWith(root + ":", comparison) ||
        path.StartsWith(root + "[", comparison);

    private static string Bound(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";
}
