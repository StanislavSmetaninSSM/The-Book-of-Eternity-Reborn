namespace BookOfEternityClient.Services;

internal sealed record MortalLocationRepairExactFieldCorrection(
    string Path,
    string Expected,
    string Actual,
    string Code,
    string RepairHint);

internal sealed class MortalLocationRepairPacket
{
    internal string Kind { get; init; } = "mortal_location_materialization_repair";
    internal string Priority { get; init; } = "blocking";
    internal string Title { get; init; } = "";
    internal IReadOnlyList<string> TargetFiles { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> TemplateRefs { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> CanonicalActorNames { get; init; } = Array.Empty<string>();
    internal string TransitionClass { get; init; } = "unresolved";
    internal string Route { get; init; } = "unresolved";
    internal string RawCarrier { get; init; } = "unresolved";
    internal string RawCoordinate { get; init; } = "unresolved";
    internal string? ExpectedMaterializationId { get; init; }
    internal IReadOnlyList<string> MissingFields { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> InvalidFields { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> Conflicts { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<MortalLocationRepairExactFieldCorrection> ExactFieldCorrections { get; init; } =
        Array.Empty<MortalLocationRepairExactFieldCorrection>();
    internal IReadOnlyList<string> RequiredCompanionTargets { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ExpectedAuthority { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ActualEvidence { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> ExpectedShape { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> SafeCorrectionRules { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
    internal IReadOnlyList<string> DoNotDo { get; init; } = Array.Empty<string>();
}

/// <summary>
/// Converts exact Mortal location validation evidence into bounded GM-owned
/// repair packets. Client-owned identity, receipt, seal, snapshot, bootstrap,
/// and accepted-state authority is never delegated to the GM.
/// </summary>
internal static class MortalLocationRepairPacketBuilder
{
    private const string PacketKind = "mortal_location_materialization_repair";

    private static readonly string[] GmAuthorableTargetRoots =
    {
        MortalLocationMaterializationContract.CurrentLocationPath,
        MortalLocationMaterializationContract.WorldMapPath
    };

    private static readonly string[] ProtectedClientOwnedRoots =
    {
        MortalLocationIdentityState.StatePath,
        MortalLocationStorageContentsState.StatePath,
        MortalBootstrapLocationScaffold.StatePath,
        "game_state/control/pending_turn_snapshot.json",
        "game_state/control/pending_turn_snapshot",
        PendingTurnSnapshotAuthority.AuthorityPath
    };

    internal static IReadOnlyList<MortalLocationRepairPacket> Build(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var locationIssues = issues.Where(IsMortalLocationIssue).ToArray();
        if (locationIssues.Length == 0)
            return Array.Empty<MortalLocationRepairPacket>();
        if (locationIssues.Any(IsProtectedClientOwnedIssue) ||
            locationIssues.Any(issue => TryCreateCandidate(issue) == null))
        {
            return Array.Empty<MortalLocationRepairPacket>();
        }

        var candidates = locationIssues
            .Select(TryCreateCandidate)
            .Where(candidate => candidate != null)
            .Cast<RepairCandidate>()
            .ToArray();
        if (candidates.Length == 0)
            return Array.Empty<MortalLocationRepairPacket>();

        var ambiguousActors = candidates
            .GroupBy(candidate => candidate.Actor, StringComparer.Ordinal)
            .Where(group => group
                .Select(candidate => candidate.RawCoordinate)
                .Distinct(StringComparer.Ordinal)
                .Take(2)
                .Count() > 1)
            .Select(group => group.Key)
            .ToHashSet(StringComparer.Ordinal);

        return candidates
            .Where(candidate => !ambiguousActors.Contains(candidate.Actor))
            .GroupBy(
                candidate => new CandidateKey(candidate.Actor, candidate.RawCoordinate),
                CandidateKeyComparer.Instance)
            .OrderBy(group => group.Key.Actor, StringComparer.Ordinal)
            .ThenBy(group => group.Key.RawCoordinate, StringComparer.Ordinal)
            .Select(group => BuildPacket(group.Key, group.ToArray()))
            .Where(packet => packet != null)
            .Cast<MortalLocationRepairPacket>()
            .ToArray();
    }

    internal static bool RequiresFailClosedRollback(
        IEnumerable<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);

        var locationIssues = issues.Where(IsMortalLocationIssue).ToArray();
        if (locationIssues.Length == 0)
            return false;
        if (locationIssues.Any(IsProtectedClientOwnedIssue) ||
            locationIssues.Any(issue => TryCreateCandidate(issue) == null))
        {
            return true;
        }

        var packetActors = Build(locationIssues)
            .SelectMany(packet => packet.CanonicalActorNames)
            .ToHashSet(StringComparer.Ordinal);
        return locationIssues.Any(issue =>
        {
            var candidate = TryCreateCandidate(issue);
            return candidate == null || !packetActors.Contains(candidate.Actor);
        });
    }

    internal static bool IsGmAuthorableTarget(string path) =>
        TryNormalizeGmTarget(path) != null;

    internal static bool IsProtectedClientOwnedTarget(string path)
    {
        var normalized = Normalize(path);
        return ProtectedClientOwnedRoots.Any(root =>
            IsRootOrDescendant(normalized, root, StringComparison.OrdinalIgnoreCase));
    }

    private static MortalLocationRepairPacket? BuildPacket(
        CandidateKey key,
        IReadOnlyList<RepairCandidate> candidates)
    {
        var issues = candidates.Select(candidate => candidate.Issue).ToArray();
        var targetFiles = candidates
            .Select(candidate => candidate.TargetFile)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        if (targetFiles.Length != 1)
            return null;

        var transitionClasses = candidates
            .Select(candidate => candidate.TransitionClass)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var rawCarriers = candidates
            .Select(candidate => candidate.RawCarrier)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        if (transitionClasses.Length != 1 || rawCarriers.Length != 1)
            return null;

        var missing = issues
            .Where(IsMissingFieldIssue)
            .Select(issue => Bound(issue.FilePath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var conflicts = issues
            .Where(IsConflictIssue)
            .Select(issue => Bound(issue.FilePath))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
        var missingSet = missing.ToHashSet(StringComparer.Ordinal);
        var conflictSet = conflicts.ToHashSet(StringComparer.Ordinal);
        var invalid = issues
            .Select(issue => Bound(issue.FilePath))
            .Where(path => !missingSet.Contains(path) && !conflictSet.Contains(path))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();

        var transitionClass = transitionClasses[0];
        var expectedInitialIds = candidates
            .Select(candidate => candidate.Issue.MortalLocationRepairContext?.InitialId)
            .Where(MortalItemIdentityRules.IsExactIdentity)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var expectedMaterializationIds = candidates
            .Select(candidate => candidate.Issue.MortalLocationRepairContext?.MaterializationId)
            .Where(MortalItemIdentityRules.IsExactIdentity)
            .Distinct(StringComparer.Ordinal)
            .Take(2)
            .ToArray();
        var isCreation = key.Actor.StartsWith("mortal_location:new:", StringComparison.Ordinal) ||
                         key.Actor.StartsWith("mortal_location_link:new:", StringComparison.Ordinal);
        if (isCreation &&
            (expectedInitialIds.Length != 1 || expectedMaterializationIds.Length != 1))
        {
            return null;
        }
        var expectedSourceAuthorities = candidates
            .Select(candidate => candidate.Issue.MortalLocationRepairContext)
            .Where(context =>
                context?.ExpectedSourceTurn != null &&
                context.ExpectedSourceAuthorityKind != null &&
                context.ExpectedSourceAuthorityId != null)
            .Select(context => new
            {
                SourceTurn = context!.ExpectedSourceTurn!.Value,
                AuthorityKind = context.ExpectedSourceAuthorityKind!,
                AuthorityId = context.ExpectedSourceAuthorityId!
            })
            .Distinct()
            .Take(2)
            .ToArray();
        if (expectedSourceAuthorities.Length > 1)
            return null;
        var expectedAuthority = new List<string>
        {
            "realm=mortal_world",
            "route=" + transitionClass,
            "actor=" + key.Actor
        };
        if (isCreation)
        {
            expectedAuthority.Add("initialId=" + expectedInitialIds[0]);
            expectedAuthority.Add("materializationId=" + expectedMaterializationIds[0]);
        }
        if (expectedSourceAuthorities.Length == 1)
        {
            expectedAuthority.Add("sourceTurn=" + expectedSourceAuthorities[0].SourceTurn);
            expectedAuthority.Add(
                "sourceAuthority=" +
                expectedSourceAuthorities[0].AuthorityKind +
                ":" +
                expectedSourceAuthorities[0].AuthorityId);
        }
        return new MortalLocationRepairPacket
        {
            Kind = PacketKind,
            Priority = "blocking",
            Title = $"Repair exact Mortal location package {key.Actor}",
            CanonicalActorNames = new[] { key.Actor },
            TransitionClass = transitionClass,
            Route = transitionClass,
            RawCarrier = rawCarriers[0],
            RawCoordinate = key.RawCoordinate,
            ExpectedMaterializationId = isCreation ? expectedMaterializationIds[0] : null,
            TargetFiles = targetFiles,
            MissingFields = missing,
            InvalidFields = invalid,
            Conflicts = conflicts,
            ExactFieldCorrections = issues
                .OrderBy(issue => issue.FilePath, StringComparer.Ordinal)
                .ThenBy(issue => issue.Code ?? string.Empty, StringComparer.Ordinal)
                .Select(issue => new MortalLocationRepairExactFieldCorrection(
                    Bound(issue.FilePath),
                    Bound(issue.Expected ?? "valid exact GM-authored value"),
                    Bound(issue.Actual ?? "missing"),
                    Bound(issue.Code ?? "mortal_location_materialization_invalid"),
                    Bound(issue.RepairHint ?? "Repair only this exact GM-owned field.")))
                .ToArray(),
            ExpectedAuthority = expectedAuthority,
            ActualEvidence = issues
                .Select(issue =>
                    $"{issue.FilePath} | {issue.Code ?? "mortal_location_materialization_invalid"} | {issue.Actual ?? "missing"}")
                .Select(Bound)
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray(),
            TemplateRefs = new[]
            {
                "specs/1520-complete-location-materialization/contracts/mortal-location-materialization-envelope.md",
                "specs/1520-complete-location-materialization/contracts/mortal-location-repair-packet.md"
            },
            ExpectedShape = new[]
            {
                "One complete GM-authored location or link package for the exact raw coordinate.",
                "One exact route and one unambiguous temporary or permanent identity.",
                "Every governed section agrees with its semantic evidence.",
                "No GM-authored permanent ID, receipt, seal, index entry, snapshot, or transition."
            },
            SafeCorrectionRules = new[]
            {
                "Edit only listed exact fields in listed GM-owned targetFiles.",
                "Preserve the exact route, candidate identity, and all unrelated world state.",
                "Keep locationId/linkId null on creation and do not duplicate the candidate into another route.",
                "Keep the validated pre-turn snapshot authoritative until post-seal validation succeeds."
            },
            Steps = new[]
            {
                "Open validation_repair_request.json and this packet's targetFiles/templateRefs.",
                "Resolve only the exact rawCoordinate and canonicalActorNames entry.",
                "Apply exactFieldCorrections without changing identity authority or another contract owner.",
                "Rerun raw Mortal location validation, then signal Complete-BoeValidationRepair only after the whole package passes."
            },
            DoNotDo = new[]
            {
                $"Do not author or edit {MortalLocationIdentityState.StatePath}, locationId/linkId creation results, materializationReceipt, seal, snapshots, or transitions.",
                "Do not replace canonical locations/links or repair another same-name or same-coordinate entity.",
                "Do not add knownExits, adjacencyMap, reverse links, display-name aliases, or coordinate aliases.",
                "Do not repair item, actor, faction, lore, threat, storage, or afterlife authority through this packet."
            }
        };
    }

    private static RepairCandidate? TryCreateCandidate(ValidationIssue issue)
    {
        var context = issue.MortalLocationRepairContext;
        if (context == null)
            return null;

        var actor = ResolveActor(issue, context);
        if (!IsExactLocationActor(actor) || !ActorMatchesContext(actor!, context))
            return null;

        var route = ResolveRepairRoute(context.CarrierPath, actor!);
        return route == null ||
               !string.Equals(context.EntityKind, route.EntityKind, StringComparison.Ordinal) ||
               !IssueMatchesRawCoordinate(issue.FilePath, context.CarrierPath, route.TargetFile) ||
               !IssueMatchesExplicitRepairableField(issue, context, route.TargetFile) ||
               !HasExpectedRouteTarget(issue, route.TargetFile)
            ? null
            : new RepairCandidate(
                issue,
                actor!,
                context.CarrierPath,
                route.RawCarrier,
                route.TransitionClass,
                route.TargetFile);
    }

    private static bool IssueMatchesExplicitRepairableField(
        ValidationIssue issue,
        MortalLocationRepairContext context,
        string targetFile)
    {
        if (string.Equals(
                issue.Code,
                "mortal_location_materialization_existing_full_resend",
                StringComparison.Ordinal))
        {
            return string.Equals(context.CarrierPath, "currentLocationData", StringComparison.Ordinal) &&
                   (IsCoordinateOrDescendant(Normalize(issue.FilePath), targetFile + ".currentLocationData") ||
                    string.Equals(Normalize(issue.FilePath), "currentLocationData", StringComparison.Ordinal));
        }

        var normalized = Normalize(issue.FilePath);
        var coordinatePrefix = targetFile + "." + context.CarrierPath;
        var relative = normalized.StartsWith(coordinatePrefix + ".", StringComparison.Ordinal)
            ? normalized[(coordinatePrefix.Length + 1)..]
            : normalized.StartsWith(context.CarrierPath + ".", StringComparison.Ordinal)
                ? normalized[(context.CarrierPath.Length + 1)..]
                : string.Empty;
        if (relative.Length == 0)
            return false;

        return context.RepairableFields.Any(field =>
            string.Equals(relative, field, StringComparison.Ordinal) ||
            relative.StartsWith(field + ".", StringComparison.Ordinal));
    }

    private static string? ResolveActor(
        ValidationIssue issue,
        MortalLocationRepairContext context)
    {
        if (IsExactLocationActor(issue.Actor))
            return issue.Actor;
        var hasInitialId = MortalItemIdentityRules.IsExactIdentity(context.InitialId);
        var hasExistingId = MortalItemIdentityRules.IsExactIdentity(context.ExistingId);
        if (hasInitialId == hasExistingId)
            return null;

        return context.EntityKind switch
        {
            "mortal_location" => hasInitialId
                ? "mortal_location:new:" + context.InitialId
                : "mortal_location:existing:" + context.ExistingId,
            "mortal_location_link" => hasInitialId
                ? "mortal_location_link:new:" + context.InitialId
                : "mortal_location_link:existing:" + context.ExistingId,
            _ => null
        };
    }

    private static bool ActorMatchesContext(
        string actor,
        MortalLocationRepairContext context)
    {
        var newPrefix = context.EntityKind switch
        {
            "mortal_location" => "mortal_location:new:",
            "mortal_location_link" => "mortal_location_link:new:",
            _ => null
        };
        var existingPrefix = context.EntityKind switch
        {
            "mortal_location" => "mortal_location:existing:",
            "mortal_location_link" => "mortal_location_link:existing:",
            _ => null
        };

        if (newPrefix != null && actor.StartsWith(newPrefix, StringComparison.Ordinal))
        {
            return string.Equals(
                       actor[newPrefix.Length..],
                       context.InitialId,
                       StringComparison.Ordinal) &&
                   MortalItemIdentityRules.IsExactIdentity(context.MaterializationId) &&
                   !MortalItemIdentityRules.IsExactIdentity(context.ExistingId);
        }

        return existingPrefix != null &&
               actor.StartsWith(existingPrefix, StringComparison.Ordinal) &&
               string.Equals(
                   actor[existingPrefix.Length..],
                   context.ExistingId,
                   StringComparison.Ordinal) &&
               !MortalItemIdentityRules.IsExactIdentity(context.InitialId);
    }

    private static bool IsExactLocationActor(string? actor)
    {
        if (string.IsNullOrEmpty(actor))
            return false;

        foreach (var prefix in new[]
                 {
                     "mortal_location:new:",
                     "mortal_location:existing:",
                     "mortal_location_link:new:",
                     "mortal_location_link:existing:"
                 })
        {
            if (actor.StartsWith(prefix, StringComparison.Ordinal))
                return MortalItemIdentityRules.IsExactIdentity(actor[prefix.Length..]);
        }

        return false;
    }

    private static RepairRouteDescriptor? ResolveRepairRoute(
        string coordinate,
        string actor)
    {
        if (string.Equals(coordinate, "currentLocationData", StringComparison.Ordinal))
        {
            if (actor.StartsWith("mortal_location:new:", StringComparison.Ordinal))
            {
                return new RepairRouteDescriptor(
                    "currentLocationData",
                    "current_scene_creation",
                    "mortal_location",
                    MortalLocationMaterializationContract.CurrentLocationPath);
            }

            return actor.StartsWith("mortal_location:existing:", StringComparison.Ordinal)
                ? new RepairRouteDescriptor(
                    "currentLocationData",
                    "current_selection",
                    "mortal_location",
                    MortalLocationMaterializationContract.CurrentLocationPath)
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "newLocations"))
        {
            return actor.StartsWith("mortal_location:new:", StringComparison.Ordinal)
                ? WorldMapRoute("world_map_creation", "mortal_location")
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "newLinks"))
        {
            return actor.StartsWith("mortal_location_link:new:", StringComparison.Ordinal)
                ? WorldMapRoute("world_map_link_creation", "mortal_location_link")
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "locationUpdates"))
        {
            return actor.StartsWith("mortal_location:existing:", StringComparison.Ordinal)
                ? WorldMapRoute("narrow_location_update", "mortal_location")
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "locationDiscoveryTransitions"))
        {
            return actor.StartsWith("mortal_location:existing:", StringComparison.Ordinal)
                ? WorldMapRoute("location_discovery_transition", "mortal_location")
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "linkUpdates"))
        {
            return actor.StartsWith("mortal_location_link:existing:", StringComparison.Ordinal)
                ? WorldMapRoute("link_update", "mortal_location_link")
                : null;
        }

        if (IsExactIndexedCoordinate(coordinate, "linkRemovals"))
        {
            return actor.StartsWith("mortal_location_link:existing:", StringComparison.Ordinal)
                ? WorldMapRoute("link_removal", "mortal_location_link")
                : null;
        }

        return null;
    }

    private static RepairRouteDescriptor WorldMapRoute(
        string transitionClass,
        string entityKind) =>
        new(
            "worldMapUpdates",
            transitionClass,
            entityKind,
            MortalLocationMaterializationContract.WorldMapPath);

    private static bool IsExactIndexedCoordinate(string coordinate, string collection)
    {
        var prefix = $"worldMapUpdates.{collection}[";
        if (!coordinate.StartsWith(prefix, StringComparison.Ordinal) ||
            !coordinate.EndsWith(']'))
        {
            return false;
        }

        var index = coordinate.AsSpan(prefix.Length, coordinate.Length - prefix.Length - 1);
        if (index.IsEmpty || index.Length > 1 && index[0] == '0')
            return false;
        foreach (var character in index)
        {
            if (character is < '0' or > '9')
                return false;
        }

        return true;
    }

    private static bool IssueMatchesRawCoordinate(
        string issuePath,
        string rawCoordinate,
        string targetFile)
    {
        var normalized = Normalize(issuePath);
        return IsCoordinateOrDescendant(normalized, rawCoordinate) ||
               IsCoordinateOrDescendant(normalized, targetFile + "." + rawCoordinate);
    }

    private static bool IsCoordinateOrDescendant(string path, string coordinate) =>
        string.Equals(path, coordinate, StringComparison.Ordinal) ||
        path.StartsWith(coordinate + ".", StringComparison.Ordinal);

    private static bool HasExpectedRouteTarget(ValidationIssue issue, string expectedTarget)
    {
        var gmTargets = issue.RepairTargetFiles
            .Append(issue.FilePath)
            .Select(TryNormalizeGmTarget)
            .Where(path => path != null)
            .Cast<string>()
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        return gmTargets.Length > 0 &&
               gmTargets.All(path => string.Equals(path, expectedTarget, StringComparison.Ordinal));
    }

    private static bool IsMortalLocationIssue(ValidationIssue issue) =>
        issue.Code?.StartsWith("mortal_location_", StringComparison.Ordinal) == true ||
        issue.Code?.StartsWith("mortal_bootstrap_location_", StringComparison.Ordinal) == true ||
        string.Equals(
            issue.Code,
            "current_location_coordinates_mismatch",
            StringComparison.Ordinal) ||
        string.Equals(
            issue.Section,
            "MortalLocationMaterialization",
            StringComparison.OrdinalIgnoreCase) ||
        string.Equals(
            issue.Section,
            "MortalLocationIdentity",
            StringComparison.OrdinalIgnoreCase);

    private static bool IsProtectedClientOwnedIssue(ValidationIssue issue)
    {
        if (issue.Category == IssueCategory.ClientOwnedSurface ||
            IsProtectedClientOwnedTarget(issue.FilePath) ||
            issue.RepairTargetFiles.Any(IsProtectedClientOwnedTarget) ||
            string.Equals(issue.Actor, "mortal_location:index", StringComparison.Ordinal) ||
            string.Equals(issue.Actor, "mortal_location_link:index", StringComparison.Ordinal))
        {
            return true;
        }

        var code = issue.Code ?? string.Empty;
        if (code.Contains("historical_replay", StringComparison.Ordinal) ||
            code.Contains("duplicate_creation_route", StringComparison.Ordinal) ||
            code.Contains("duplicate_property", StringComparison.Ordinal) ||
            code.Contains("endpoint_selector_invalid", StringComparison.Ordinal) ||
            code.Contains("movement_not_authorized", StringComparison.Ordinal) ||
            code.Contains("storage_removal_not_empty", StringComparison.Ordinal) ||
            code.Contains("_target_unresolved", StringComparison.Ordinal) ||
            code.Contains("_target_ambiguous", StringComparison.Ordinal) ||
            code.Contains("confusable", StringComparison.Ordinal) ||
            code.Contains("identity", StringComparison.Ordinal) ||
            code.Contains("client_field", StringComparison.Ordinal) ||
            code.Contains("client_owned", StringComparison.Ordinal) ||
            code.Contains("receipt", StringComparison.Ordinal) ||
            code.Contains("seal", StringComparison.Ordinal) ||
            code.Contains("source_authority", StringComparison.Ordinal) ||
            code.Contains("source_turn", StringComparison.Ordinal) ||
            code.Contains("index", StringComparison.Ordinal) ||
            code.Contains("snapshot", StringComparison.Ordinal))
        {
            return true;
        }

        return HasExactPropertySegment(issue.FilePath, "initialId") ||
               HasExactPropertySegment(issue.FilePath, "materializationId") ||
               HasExactPropertySegment(issue.FilePath, "sourceAuthority") ||
               HasExactPropertySegment(issue.FilePath, "sourceTurn") ||
               HasExactPropertySegment(issue.FilePath, MortalLocationMaterializationContract.ReceiptProperty) ||
               HasExactPropertySegment(issue.FilePath, "receiptId") ||
               HasExactPropertySegment(issue.FilePath, "seal") ||
               HasExactPropertySegment(issue.FilePath, "transitions");
    }

    private static bool IsMissingFieldIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return string.Equals(issue.Actual, "missing", StringComparison.OrdinalIgnoreCase) ||
               code.EndsWith("_missing", StringComparison.Ordinal) ||
               code.Contains("_missing_", StringComparison.Ordinal);
    }

    private static bool IsConflictIssue(ValidationIssue issue)
    {
        var code = issue.Code ?? string.Empty;
        return code.Contains("collision", StringComparison.Ordinal) ||
               code.Contains("conflict", StringComparison.Ordinal) ||
               code.Contains("cycle", StringComparison.Ordinal) ||
               code.Contains("unresolved", StringComparison.Ordinal) ||
               code.Contains("dangling", StringComparison.Ordinal) ||
               code.Contains("duplicate", StringComparison.Ordinal) ||
               code.Contains("ambiguous", StringComparison.Ordinal) ||
               code.Contains("precondition", StringComparison.Ordinal);
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

    private static string? TryNormalizeGmTarget(string path)
    {
        var normalized = Normalize(path);
        if (IsProtectedClientOwnedTarget(normalized))
            return null;
        return GmAuthorableTargetRoots.FirstOrDefault(root =>
            IsRootOrDescendant(normalized, root, StringComparison.Ordinal));
    }

    private static string Normalize(string path) =>
        (path ?? string.Empty).Trim().Replace('\\', '/');

    private static bool IsRootOrDescendant(
        string path,
        string root,
        StringComparison comparison) =>
        string.Equals(path, root, comparison) ||
        path.StartsWith(root + ".", comparison) ||
        path.StartsWith(root + ":", comparison) ||
        path.StartsWith(root + "[", comparison) ||
        path.StartsWith(root + "/", comparison);

    private static string Bound(string value) =>
        value.Length <= 500 ? value : value[..500] + "…";

    private sealed record RepairCandidate(
        ValidationIssue Issue,
        string Actor,
        string RawCoordinate,
        string RawCarrier,
        string TransitionClass,
        string TargetFile);

    private sealed record RepairRouteDescriptor(
        string RawCarrier,
        string TransitionClass,
        string EntityKind,
        string TargetFile);

    private sealed record CandidateKey(string Actor, string RawCoordinate);

    private sealed class CandidateKeyComparer : IEqualityComparer<CandidateKey>
    {
        internal static readonly CandidateKeyComparer Instance = new();

        public bool Equals(CandidateKey? left, CandidateKey? right) =>
            left != null && right != null &&
            string.Equals(left.Actor, right.Actor, StringComparison.Ordinal) &&
            string.Equals(left.RawCoordinate, right.RawCoordinate, StringComparison.Ordinal);

        public int GetHashCode(CandidateKey value) =>
            HashCode.Combine(
                StringComparer.Ordinal.GetHashCode(value.Actor),
                StringComparer.Ordinal.GetHashCode(value.RawCoordinate));
    }
}
