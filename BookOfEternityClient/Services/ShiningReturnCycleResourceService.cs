using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal enum ShiningReturnCycleTransitionKind
{
    SynchronizeCurrentShining,
    OrdinaryReentryFromChaosSea,
    AscensionFromChaosSea
}

internal sealed class ShiningReturnCycleResourceFilePlan
{
    private readonly ValidationIssue[] _issues;
    private readonly IReadOnlyDictionary<string, string?> _beforeImages;

    internal ShiningReturnCycleResourceFilePlan(
        ShiningReturnCycleResourcePlanningResult? resourcePlan,
        JsonObject? soulAfterImage,
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues,
        CanonicalResourceQuartetProjection? quartetProjection = null)
    {
        ResourcePlan = resourcePlan;
        SoulAfterImage = soulAfterImage?.DeepClone().AsObject();
        _beforeImages = new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(beforeImages, StringComparer.Ordinal));
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        QuartetProjection = quartetProjection;
    }

    internal ShiningReturnCycleResourcePlanningResult? ResourcePlan { get; }
    internal JsonObject? SoulAfterImage { get; }
    internal IReadOnlyDictionary<string, string?> BeforeImages => _beforeImages;
    internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());
    internal CanonicalResourceQuartetProjection? QuartetProjection { get; }
    internal decimal GachaAttemptsCurrent => ResolveGachaAttempts()?.Current ?? 0m;
    internal decimal GachaAttemptsMaximum => ResolveGachaAttempts()?.Maximum ?? 0m;
    internal bool IsValid =>
        ResourcePlan is { IsValid: true } &&
        SoulAfterImage != null &&
        QuartetProjection != null &&
        ResolveGachaAttempts() != null &&
        _issues.Length == 0;

    private ResourceStateEntry? ResolveGachaAttempts()
    {
        if (ResourcePlan?.ShiningAfterImage?["resourceOwnerBindings"]?["gachaReturn"]
                is not JsonObject binding ||
            binding["resourceOwnerId"] is not JsonValue ownerNode ||
            !ownerNode.TryGetValue<string>(out var ownerId) ||
            string.IsNullOrWhiteSpace(ownerId) ||
            ResourcePlan.StateAfterImage == null)
        {
            return null;
        }

        var coordinate = new ResourceCoordinate(
            "shining_abode",
            ResourceOwnerKind.AfterlifeScope,
            ownerId,
            "gacha_attempts");
        return ResourcePlan.StateAfterImage.TryResolveExact(coordinate, out var entry)
            ? entry
            : null;
    }
}

internal static class ShiningReturnCycleResourceService
{
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";
    private const string LifeTransitionsPath =
        "game_state/control/life_transitions.json";

    internal static async Task<ShiningReturnCycleResourceFilePlan> BuildAsync(
        FileSystemManager fs,
        JsonObject acceptedShiningAbode,
        JsonObject acceptedSoulState,
        ShiningReturnCycleTransitionKind transitionKind,
        int turn)
    {
        ArgumentNullException.ThrowIfNull(fs);
        await using var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        return await BuildAsync(
            fs,
            writeLease,
            acceptedShiningAbode,
            acceptedSoulState,
            transitionKind,
            turn);
    }

    internal static async Task<ShiningReturnCycleResourceFilePlan> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        JsonObject acceptedShiningAbode,
        JsonObject acceptedSoulState,
        ShiningReturnCycleTransitionKind transitionKind,
        int turn)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(acceptedShiningAbode);
        ArgumentNullException.ThrowIfNull(acceptedSoulState);
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));

        var paths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath,
            AfterlifeEntityProfileState.StatePath,
            AfterlifeSpiritualConflictState.StatePath,
            SoulPath,
            ShiningAbodeState.StatePath,
            GuardiansPath,
            GuardianAbodeResidentState.StatePath,
            AfterlifeReturnGuardService.GuardPath,
            LifeTransitionsPath
        };
        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths)
            beforeImages[path] = await fs.ReadFileAsync(writeLease, path);

        var definitions = ResourceDefinitionCatalog.ParseCanonical(
            beforeImages[ResourceMaterializationContract.DefinitionsPath],
            allowMissingPristine: false);
        if (!definitions.IsValid || definitions.Catalog == null)
            return Failure(beforeImages, definitions.Issues);
        var state = ResourceStateContract.ParseCanonical(
            beforeImages[ResourceMaterializationContract.StatePath],
            definitions.Catalog,
            allowMissingPristine: false);
        if (!state.IsValid || state.Ledger == null)
            return Failure(beforeImages, state.Issues);
        var history = ResourceHistoryState.ParseCanonical(
            beforeImages[ResourceMaterializationContract.HistoryPath],
            definitions.Catalog,
            allowMissingPristine: false);
        if (!history.IsValid || history.History == null)
            return Failure(beforeImages, history.Issues);

        var issues = new List<ValidationIssue>();
        var preTurnShining = ParseObject(
            beforeImages[ShiningAbodeState.StatePath],
            ShiningAbodeState.StatePath,
            required: true,
            issues);
        var preTurnSoul = ParseObject(
            beforeImages[SoulPath],
            SoulPath,
            required: true,
            issues);
        var profiles = ParseObject(
            beforeImages[AfterlifeEntityProfileState.StatePath],
            AfterlifeEntityProfileState.StatePath,
            required: false,
            issues);
        var conflict = ParseObject(
            beforeImages[AfterlifeSpiritualConflictState.StatePath],
            AfterlifeSpiritualConflictState.StatePath,
            required: false,
            issues);
        var guardians = ParseObject(
            beforeImages[GuardiansPath],
            GuardiansPath,
            required: false,
            issues);
        if (issues.Count != 0 || preTurnShining == null || preTurnSoul == null)
            return Failure(beforeImages, issues);

        if (profiles == null ||
            acceptedSoulState["currentRealm"] is not JsonValue realmNode ||
            !realmNode.TryGetValue<string>(out var acceptedRealm) ||
            string.IsNullOrWhiteSpace(acceptedRealm))
        {
            AddIssue(
                issues,
                AfterlifeEntityProfileState.StatePath,
                "shining_return_cycle_player_profile_missing",
                "exact player_soul profile and accepted currentRealm",
                profiles == null ? "missing profile root" : "missing currentRealm");
            return Failure(beforeImages, issues);
        }

        var preTurnRealm = preTurnSoul["currentRealm"] is JsonValue preTurnRealmNode &&
                           preTurnRealmNode.TryGetValue<string>(out var parsedPreTurnRealm)
            ? parsedPreTurnRealm
            : null;
        var expectsCurrentShining =
            transitionKind == ShiningReturnCycleTransitionKind.SynchronizeCurrentShining;
        var sourceRealmIsValid = expectsCurrentShining
            ? RealmSemantics.IsShiningRealm(preTurnRealm)
            : RealmSemantics.IsChaosSea(preTurnRealm);
        if (!sourceRealmIsValid || !RealmSemantics.IsShiningRealm(acceptedRealm))
        {
            AddIssue(
                issues,
                SoulPath + ".currentRealm",
                "shining_return_cycle_source_realm_invalid",
                expectsCurrentShining
                    ? "Shining Abode -> Shining Abode cycle synchronization"
                    : "Chaos Sea -> Shining Abode realm transition",
                $"{preTurnRealm ?? "missing/null"} -> {acceptedRealm}");
            return Failure(beforeImages, issues);
        }
        if (transitionKind == ShiningReturnCycleTransitionKind.AscensionFromChaosSea &&
            !AfterlifeAscensionAuthority.IsReady(preTurnSoul))
        {
            AddIssue(
                issues,
                SoulPath,
                "shining_return_cycle_ascension_authority_invalid",
                "fresh maximum enlightenment authority in the lease-bound pre-turn soul state",
                "ascension prerequisites are not satisfied");
            return Failure(beforeImages, issues);
        }
        if (transitionKind == ShiningReturnCycleTransitionKind.AscensionFromChaosSea &&
            beforeImages[LifeTransitionsPath] != null)
        {
            AddIssue(
                issues,
                LifeTransitionsPath,
                "shining_return_cycle_lifecycle_conflict",
                "absent life-transition command during ascension publication",
                "present");
            return Failure(beforeImages, issues);
        }

        JsonObject acceptedProfiles;
        try
        {
            acceptedProfiles = AfterlifeEntityProfileState.ProjectPlayerSoulRealm(
                profiles,
                acceptedRealm);
        }
        catch (InvalidOperationException exception)
        {
            AddIssue(
                issues,
                AfterlifeEntityProfileState.StatePath,
                "shining_return_cycle_player_profile_invalid",
                "one exact player_soul profile projected to the accepted realm",
                exception.Message);
            return Failure(beforeImages, issues);
        }

        if (!TryReadNonNegativeInt(
                acceptedSoulState["currentIncarnation"],
                out var currentIncarnation))
        {
            AddIssue(
                issues,
                SoulPath + ".currentIncarnation",
                "shining_return_cycle_incarnation_invalid",
                "non-negative integral currentIncarnation",
                acceptedSoulState["currentIncarnation"]?.ToJsonString() ?? "missing/null");
            return Failure(beforeImages, issues);
        }

        var resourcePlan = ShiningReturnCycleResourcePlanner.Build(
            new ShiningReturnCycleResourcePlanningInput(
                turn,
                currentIncarnation,
                definitions.Catalog,
                state.Ledger,
                history.History,
                new AfterlifeResourceOwnerRoots(
                    profiles,
                    conflict,
                    preTurnSoul,
                    preTurnShining,
                    guardians),
                acceptedProfiles,
                acceptedShiningAbode,
                acceptedSoulState));
        if (!resourcePlan.IsValid)
            return Failure(beforeImages, resourcePlan.Issues);

        var soulAfterImage = GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(
            acceptedSoulState);
        var projectedDocuments = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [AfterlifeEntityProfileState.StatePath] =
                resourcePlan.ProfilesAfterImage!.ToJsonString(),
            [ShiningAbodeState.StatePath] =
                resourcePlan.ShiningAfterImage!.ToJsonString(),
            [SoulPath] = soulAfterImage.ToJsonString()
        };
        var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
            definitions.Catalog,
            state.Ledger,
            history.History,
            resourcePlan.StateAfterImage!,
            resourcePlan.HistoryAfterImage!,
            path => fs.ReadFileAsync(writeLease, path),
            beforeImages,
            projectedDocuments);
        if (quartet.Projection == null)
            return Failure(beforeImages, quartet.Issues);

        return new ShiningReturnCycleResourceFilePlan(
            resourcePlan,
            soulAfterImage,
            quartet.Projection.BeforeImages,
            Array.Empty<ValidationIssue>(),
            quartet.Projection);
    }

    internal static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        ShiningReturnCycleResourceFilePlan plan)
    {
        ArgumentNullException.ThrowIfNull(fs);
        var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        Exception? publicationUncertainty = null;
        try { return await TryCommitAsync(fs, writeLease, plan); }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            publicationUncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(
                fs, writeLease, completed: false, operationFailure: publicationUncertainty);
        }
    }

    internal static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        ShiningReturnCycleResourceFilePlan plan)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.IsValid ||
            plan.ResourcePlan?.ShiningAfterImage == null ||
            plan.ResourcePlan.ProfilesAfterImage == null ||
            plan.ResourcePlan.StateAfterImage == null ||
            plan.ResourcePlan.HistoryAfterImage == null ||
            plan.SoulAfterImage == null ||
            plan.QuartetProjection == null)
        {
            return false;
        }

        var guardedPaths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            AfterlifeSpiritualConflictState.StatePath,
            GuardiansPath,
            GuardianAbodeResidentState.StatePath,
            AfterlifeReturnGuardService.GuardPath,
            LifeTransitionsPath
        };
        var writes = guardedPaths
            .Select(path => CoordinatedStateWriteHelper.CreateGuardWrite(
                path,
                plan.BeforeImages[path]))
            .Concat(new[]
            {
                Write(
                    AfterlifeEntityProfileState.StatePath,
                    plan.BeforeImages[AfterlifeEntityProfileState.StatePath],
                    plan.ResourcePlan.ProfilesAfterImage.ToJsonString()),
                Write(
                    ShiningAbodeState.StatePath,
                    plan.BeforeImages[ShiningAbodeState.StatePath],
                    plan.ResourcePlan.ShiningAfterImage.ToJsonString()),
                Write(
                    SoulPath,
                    plan.BeforeImages[SoulPath],
                    plan.SoulAfterImage.ToJsonString()),
                Write(
                    ResourceMaterializationContract.StatePath,
                    plan.BeforeImages[ResourceMaterializationContract.StatePath],
                    plan.ResourcePlan.StateAfterImage.ToCanonicalJson()),
                Write(
                    ResourceMaterializationContract.HistoryPath,
                    plan.BeforeImages[ResourceMaterializationContract.HistoryPath],
                    plan.ResourcePlan.HistoryAfterImage.ToCanonicalJson())
            })
            .ToList();
        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            plan.QuartetProjection);
        return await CoordinatedStateWriteHelper.TryCommitAsync(
            fs,
            writeLease,
            writes.ToArray());
    }

    private static CoordinatedStateWriteHelper.PlannedWrite Write(
        string path,
        string? previousJson,
        string nextJson) =>
        new(
            path,
            previousJson,
            nextJson,
            RequireCurrentBaseline: true);

    private static JsonObject? ParseObject(
        string? json,
        string path,
        bool required,
        List<ValidationIssue> issues)
    {
        if (json == null)
        {
            if (required)
            {
                AddIssue(
                    issues,
                    path,
                    "shining_return_cycle_owner_root_missing",
                    "present canonical object root",
                    "missing");
            }
            return null;
        }
        if (string.IsNullOrWhiteSpace(json))
        {
            AddIssue(
                issues,
                path,
                "shining_return_cycle_owner_root_invalid",
                "non-empty strict JSON object",
                "empty or whitespace-only file");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                AddIssue(
                    issues,
                    path,
                    "shining_return_cycle_owner_root_invalid",
                    "strict JSON object",
                    document.RootElement.ValueKind.ToString());
                return null;
            }
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                issues,
                "shining_return_cycle_owner_duplicate_property");
            return issues.Count == 0
                ? JsonNode.Parse(document.RootElement.GetRawText())!.AsObject()
                : null;
        }
        catch (JsonException exception)
        {
            AddIssue(
                issues,
                path,
                "shining_return_cycle_owner_root_invalid",
                "well-formed strict JSON object",
                exception.GetType().Name);
            return null;
        }
    }

    private static bool TryReadNonNegativeInt(JsonNode? node, out int value)
    {
        value = default;
        return node is JsonValue scalar &&
               scalar.TryGetValue<int>(out value) &&
               value >= 0;
    }

    private static ShiningReturnCycleResourceFilePlan Failure(
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues) =>
        new(null, null, beforeImages, issues);

    private static void AddIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Shining return-cycle resource planning is invalid.",
            code,
            "ResourceMaterialization",
            expected,
            actual));
}
