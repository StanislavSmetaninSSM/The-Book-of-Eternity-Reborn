namespace BookOfEternityClient.Services;

internal static class WoundAcceptedTurnSnapshotContract
{
    internal const string PendingResolutionPath =
        "game_state/control/pending_wound_resolutions.json";

    private static readonly string[] CarrierPathInventory =
    [
        WoundCarrierCatalog.PlayerPath,
        WoundCarrierCatalog.NpcPath,
        WoundCarrierCatalog.EnemiesPath,
        WoundCarrierCatalog.AlliesPath,
        WoundCarrierCatalog.AfterlifeProfilesPath
    ];

    private static readonly string[] SchedulerPathInventory =
    [
        ProgressionScheduleService.SchedulePath,
        ProgressionScheduleService.ReportPath
    ];

    private static readonly string[] OutputPathInventory =
    [
        "output/narrative_response.json",
        "output/interface_updates.json",
        "output/debug_logs.json"
    ];

    private static readonly string[] RequiredPathInventory = CarrierPathInventory
        .Concat(
        [
            WoundIdentityState.StatePath,
            WoundHistoryState.HistoryPath,
            AcceptedMechanicsPlan.WoundCommandPath,
            PendingResolutionPath
        ])
        .Concat(SchedulerPathInventory)
        .Concat(OutputPathInventory)
        .Distinct(StringComparer.Ordinal)
        .OrderBy(static path => path, StringComparer.Ordinal)
        .ToArray();

    internal static IReadOnlyList<string> CarrierPaths { get; } =
        Array.AsReadOnly(CarrierPathInventory);

    internal static IReadOnlyList<string> SchedulerPaths { get; } =
        Array.AsReadOnly(SchedulerPathInventory);

    internal static IReadOnlyList<string> OutputPaths { get; } =
        Array.AsReadOnly(OutputPathInventory);

    internal static IReadOnlyList<string> RequiredPaths { get; } =
        Array.AsReadOnly(RequiredPathInventory);

    internal static bool HasCompleteBeforeImages(
        IReadOnlyDictionary<string, CanonicalBeforeImage>? beforeImages,
        out string? missingPath)
    {
        missingPath = null;
        if (beforeImages == null)
            return false;

        var exactPaths = new HashSet<string>(
            beforeImages.Keys,
            StringComparer.Ordinal);

        foreach (var path in RequiredPathInventory)
        {
            if (!exactPaths.Contains(path))
            {
                missingPath = path;
                return false;
            }
        }

        return true;
    }

    internal static IReadOnlyList<string> BuildRequiredPaths(
        IEnumerable<string> planPaths)
    {
        ArgumentNullException.ThrowIfNull(planPaths);
        var required = new HashSet<string>(
            RequiredPathInventory,
            StringComparer.Ordinal);
        foreach (var path in planPaths)
        {
            AcceptedMechanicsPlanBinding.ValidatePath(path, nameof(planPaths));
            if (!path.StartsWith("game_state/", StringComparison.Ordinal) &&
                !path.StartsWith("lore/", StringComparison.Ordinal) &&
                !path.StartsWith("output/", StringComparison.Ordinal))
            {
                throw new ArgumentException(
                    $"Plan path '{path}' is outside rollback-tracked game, lore, and output authority.",
                    nameof(planPaths));
            }

            required.Add(path);
        }

        return Array.AsReadOnly(required
            .OrderBy(static path => path, StringComparer.Ordinal)
            .ToArray());
    }

    internal static async Task<IReadOnlyList<ValidationIssue>>
        ValidatePublishedOutputAuthorityAsync(
            AcceptedMechanicsPlan plan,
            Func<string, Task<byte[]?>> readCurrentBytesAsync)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(readCurrentBytesAsync);
        var issues = new List<ValidationIssue>();
        var beforeImages = plan.BeforeImages;
        if (!HasCompleteBeforeImages(beforeImages, out var missingPath))
        {
            issues.Add(new ValidationIssue(
                missingPath ?? AcceptedMechanicsPlan.WoundCommandPath,
                IssueSeverity.Error,
                "Accepted wound publication lost an exact required before-image.",
                code: "wound_materialization_before_image_missing",
                section: "AcceptedTurnWoundMaterialization",
                expected: "exact before-image for every wound, scheduler, and output authority path",
                actual: "required before-image is missing"));
            return issues;
        }

        foreach (var path in OutputPathInventory)
        {
            var beforeImage = beforeImages[path];
            var current = await readCurrentBytesAsync(path);
            var exact = beforeImage.Existed == (current != null) &&
                        (beforeImage.Bytes == null
                            ? current == null
                            : current != null &&
                              beforeImage.Bytes.AsSpan().SequenceEqual(current));
            if (exact)
                continue;

            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Accepted wound player output changed after canonical publication.",
                code: "wound_materialization_output_authority_mismatch",
                section: "AcceptedTurnWoundMaterialization",
                expected: "byte-identical output existence and content sealed by the common plan",
                actual: beforeImage.Existed
                    ? current == null ? "output was deleted" : "output bytes changed"
                    : "previously absent output was created"));
        }

        return issues;
    }
}
