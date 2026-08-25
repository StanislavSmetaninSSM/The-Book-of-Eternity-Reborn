using System.Collections.ObjectModel;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;

namespace BookOfEternityClient.Services;

internal sealed record CanonicalResourceQuartetProjection(
    string AuthorityAfterImage,
    IReadOnlyDictionary<string, string?> BeforeImages);

internal sealed class CanonicalResourceFreshBootstrapPlan
{
    private readonly IReadOnlyDictionary<string, JsonObject> _ownerAfterImages;
    private readonly IReadOnlyDictionary<string, string?> _beforeImages;
    private readonly ValidationIssue[] _issues;

    internal CanonicalResourceFreshBootstrapPlan(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        ResourceOwnerAuthority? authority,
        IReadOnlyList<ResourceOwnerCapacityDraft> capacityDrafts,
        string? canonicalAuthorityJson,
        IReadOnlyDictionary<string, JsonObject> ownerAfterImages,
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues)
    {
        Definitions = definitions ?? throw new ArgumentNullException(nameof(definitions));
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        Authority = authority;
        CapacityDrafts = (capacityDrafts ??
                          throw new ArgumentNullException(nameof(capacityDrafts))).ToArray();
        CanonicalAuthorityJson = canonicalAuthorityJson;
        _ownerAfterImages = new ReadOnlyDictionary<string, JsonObject>(
            ownerAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));
        _beforeImages = new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(beforeImages, StringComparer.Ordinal));
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
    }

    internal ResourceDefinitionCatalog Definitions { get; }

    internal ResourceStateLedger? StateAfterImage { get; }

    internal ResourceHistoryState? HistoryAfterImage { get; }

    internal ResourceOwnerAuthority? Authority { get; }

    internal IReadOnlyList<ResourceOwnerCapacityDraft> CapacityDrafts { get; }

    internal string? CanonicalAuthorityJson { get; }

    internal IReadOnlyDictionary<string, JsonObject> OwnerAfterImages => _ownerAfterImages;

    internal IReadOnlyDictionary<string, string?> BeforeImages => _beforeImages;

    internal CanonicalResourceQuartetProjection? QuartetProjection =>
        CanonicalAuthorityJson == null
            ? null
            : new CanonicalResourceQuartetProjection(
                CanonicalAuthorityJson,
                _beforeImages);

    internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());

    internal bool IsValid =>
        Authority != null &&
        StateAfterImage != null &&
        HistoryAfterImage != null &&
        CanonicalAuthorityJson != null &&
        QuartetProjection != null &&
        _issues.Length == 0;
}

internal static class CanonicalResourceQuartetTransaction
{
    internal static async Task<CanonicalResourceFreshBootstrapPlan>
        ComposeExplicitBootstrapAsync(
            ResourceDefinitionCatalog definitions,
            ResourceStateLedger pristineState,
            ResourceHistoryState pristineHistory,
            AfterlifeOwnerResourceAcceptedState acceptedOwners,
            Func<string, Task<string?>> readDocumentAsync)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(pristineState);
        ArgumentNullException.ThrowIfNull(pristineHistory);
        ArgumentNullException.ThrowIfNull(acceptedOwners);
        ArgumentNullException.ThrowIfNull(readDocumentAsync);

        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        async Task<string?> CaptureAsync(string path)
        {
            if (!beforeImages.TryGetValue(path, out var json))
            {
                json = await readDocumentAsync(path);
                beforeImages.Add(path, json);
            }
            return json;
        }

        var issues = new List<ValidationIssue>();
        foreach (var path in new[]
                 {
                     ResourceMaterializationContract.DefinitionsPath,
                     ResourceMaterializationContract.StatePath,
                     ResourceMaterializationContract.HistoryPath,
                     CanonicalResourceOwnerAuthorityComposer.AuthorityPath
                 })
        {
            if (await CaptureAsync(path) != null)
            {
                issues.Add(new ValidationIssue(
                    path,
                    IssueSeverity.Error,
                    "Fresh New Game resource bootstrap requires prior absence.",
                    code: "resource_fresh_bootstrap_baseline_not_pristine",
                    section: "ResourceMaterialization",
                    expected: "absent Fresh New Game quartet member",
                    actual: "existing file",
                    repairTargetFiles: new[] { path }));
            }
        }

        var capturedProfiles = await ParseFreshOwnerRootAsync(
            AfterlifeEntityProfileState.StatePath,
            AfterlifeEntityProfileState.CreateDefaultRoot,
            CaptureAsync,
            issues);
        var capturedConflict = await ParseFreshOwnerRootAsync(
            AfterlifeSpiritualConflictState.StatePath,
            AfterlifeSpiritualConflictState.CreateDefaultRoot,
            CaptureAsync,
            issues);
        var capturedSoul = await ParseFreshOwnerRootAsync(
            "game_state/meta/soul_state.json",
            static () => new JsonObject(),
            CaptureAsync,
            issues);
        var capturedShining = await ParseFreshOwnerRootAsync(
            ShiningAbodeState.StatePath,
            ShiningAbodeState.CreateDefaultState,
            CaptureAsync,
            issues);
        var capturedGuardians = await ParseFreshOwnerRootAsync(
            "game_state/meta/guardians.json",
            static () => new JsonObject(),
            CaptureAsync,
            issues);
        if (issues.Count != 0)
        {
            return FreshFailure(
                definitions,
                pristineState,
                pristineHistory,
                beforeImages,
                issues);
        }

        var accepted = new AfterlifeResourceOwnerRoots(
            acceptedOwners.Profiles ?? capturedProfiles,
            acceptedOwners.SpiritualConflict ?? capturedConflict,
            acceptedOwners.SoulState ?? capturedSoul,
            acceptedOwners.ShiningAbode ?? capturedShining,
            acceptedOwners.Guardians ?? capturedGuardians);
        var planning = AfterlifeOwnerResourceStatePlanner.Build(
            new AfterlifeOwnerResourceStatePlanningInput(
                Turn: 1,
                definitions,
                pristineState,
                pristineHistory,
                new AfterlifeResourceOwnerRoots(
                    profiles: null,
                    spiritualConflict: null),
                accepted));
        if (!planning.IsValid ||
            planning.Composition == null ||
            planning.StateAfterImage == null ||
            planning.HistoryAfterImage == null)
        {
            return FreshFailure(
                definitions,
                pristineState,
                pristineHistory,
                beforeImages,
                planning.Issues);
        }

        var ownerAfterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        AddFreshAccepted(
            ownerAfterImages,
            AfterlifeEntityProfileState.StatePath,
            acceptedOwners.Profiles);
        AddFreshAccepted(
            ownerAfterImages,
            AfterlifeSpiritualConflictState.StatePath,
            acceptedOwners.SpiritualConflict);
        AddFreshAccepted(
            ownerAfterImages,
            "game_state/meta/soul_state.json",
            acceptedOwners.SoulState);
        AddFreshAccepted(
            ownerAfterImages,
            ShiningAbodeState.StatePath,
            acceptedOwners.ShiningAbode);
        AddFreshAccepted(
            ownerAfterImages,
            "game_state/meta/guardians.json",
            acceptedOwners.Guardians);
        foreach (var (path, root) in planning.Composition.OwnerCompanionAfterImages)
            ownerAfterImages[path] = root.DeepClone().AsObject();
        if (ownerAfterImages.TryGetValue("game_state/meta/soul_state.json", out var soul))
        {
            ownerAfterImages["game_state/meta/soul_state.json"] =
                GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soul);
        }

        var projectedDocuments = ownerAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            StringComparer.Ordinal);
        Task<string?> ReadProjectedAsync(string path) =>
            projectedDocuments.TryGetValue(path, out var projected)
                ? Task.FromResult<string?>(projected)
                : CaptureAsync(path);
        var authority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            ReadProjectedAsync,
            planning.StateAfterImage,
            planning.HistoryAfterImage,
            CanonicalResourceOwnerAuthorityPurpose.ExplicitBootstrap);
        if (!authority.IsValid || authority.CanonicalAuthorityJson == null)
        {
            return FreshFailure(
                definitions,
                planning.StateAfterImage,
                planning.HistoryAfterImage,
                beforeImages,
                authority.Issues);
        }

        var committedDocuments = new Dictionary<string, string>(
            projectedDocuments,
            StringComparer.Ordinal)
        {
            [CanonicalResourceOwnerAuthorityComposer.AuthorityPath] =
                authority.CanonicalAuthorityJson
        };
        var postValidation = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            path => committedDocuments.TryGetValue(path, out var committed)
                ? Task.FromResult<string?>(committed)
                : CaptureAsync(path),
            planning.StateAfterImage,
            planning.HistoryAfterImage,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!postValidation.IsValid)
        {
            return FreshFailure(
                definitions,
                planning.StateAfterImage,
                planning.HistoryAfterImage,
                beforeImages,
                postValidation.Issues);
        }

        return new CanonicalResourceFreshBootstrapPlan(
            definitions,
            planning.StateAfterImage,
            planning.HistoryAfterImage,
            authority.Authority,
            authority.CapacityDrafts,
            authority.CanonicalAuthorityJson,
            ownerAfterImages,
            beforeImages,
            Array.Empty<ValidationIssue>());
    }

    private static async Task<JsonObject> ParseFreshOwnerRootAsync(
        string path,
        Func<JsonObject> createDefault,
        Func<string, Task<string?>> captureAsync,
        List<ValidationIssue> issues)
    {
        var json = await captureAsync(path);
        if (json == null)
            return createDefault();
        try
        {
            using var document = System.Text.Json.JsonDocument.Parse(json);
            if (document.RootElement.ValueKind !=
                System.Text.Json.JsonValueKind.Object)
            {
                throw new System.Text.Json.JsonException("Expected object root.");
            }
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                issues,
                "resource_fresh_bootstrap_owner_duplicate_property");
            return JsonNode.Parse(document.RootElement.GetRawText())!.AsObject();
        }
        catch (System.Text.Json.JsonException exception)
        {
            issues.Add(new ValidationIssue(
                path,
                IssueSeverity.Error,
                "Fresh New Game owner seed must be a strict JSON object.",
                code: "resource_fresh_bootstrap_owner_root_invalid",
                section: "ResourceMaterialization",
                expected: "well-formed strict object",
                actual: exception.GetType().Name,
                repairTargetFiles: new[] { path }));
            return createDefault();
        }
    }

    private static void AddFreshAccepted(
        Dictionary<string, JsonObject> ownerAfterImages,
        string path,
        JsonObject? accepted)
    {
        if (accepted != null)
            ownerAfterImages[path] = accepted.DeepClone().AsObject();
    }

    private static CanonicalResourceFreshBootstrapPlan FreshFailure(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger state,
        ResourceHistoryState history,
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            definitions,
            state,
            history,
            authority: null,
            Array.Empty<ResourceOwnerCapacityDraft>(),
            canonicalAuthorityJson: null,
            new Dictionary<string, JsonObject>(StringComparer.Ordinal),
            beforeImages,
            issues);

    internal static async Task<(CanonicalResourceQuartetProjection? Projection,
        IReadOnlyList<ValidationIssue> Issues)> ComposeExistingSessionAsync(
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger currentState,
        ResourceHistoryState currentHistory,
        ResourceStateLedger finalState,
        ResourceHistoryState finalHistory,
        Func<string, Task<string?>> readDocumentAsync,
        IReadOnlyDictionary<string, string?> capturedBeforeImages,
        IReadOnlyDictionary<string, string> projectedDocuments)
    {
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(currentHistory);
        ArgumentNullException.ThrowIfNull(finalState);
        ArgumentNullException.ThrowIfNull(finalHistory);
        ArgumentNullException.ThrowIfNull(readDocumentAsync);
        ArgumentNullException.ThrowIfNull(capturedBeforeImages);
        ArgumentNullException.ThrowIfNull(projectedDocuments);

        var beforeImages = new Dictionary<string, string?>(
            capturedBeforeImages,
            StringComparer.Ordinal);

        async Task<string?> CaptureAsync(string path)
        {
            if (!beforeImages.TryGetValue(path, out var json))
            {
                json = await readDocumentAsync(path);
                beforeImages.Add(path, json);
            }
            return json;
        }

        var existing = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            CaptureAsync,
            currentState,
            currentHistory,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        if (!existing.IsValid)
            return (null, existing.Issues);

        var final = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            path => projectedDocuments.TryGetValue(path, out var projected)
                ? Task.FromResult<string?>(projected)
                : CaptureAsync(path),
            finalState,
            finalHistory,
            CanonicalResourceOwnerAuthorityPurpose.FinalAfterImage);
        if (!final.IsValid || final.CanonicalAuthorityJson == null)
            return (null, final.Issues);

        return (
            new CanonicalResourceQuartetProjection(
                final.CanonicalAuthorityJson,
                beforeImages),
            Array.Empty<ValidationIssue>());
    }

    internal static void AddAuthorityWriteAndGlobalGuards(
        ICollection<CoordinatedStateWriteHelper.PlannedWrite> writes,
        CanonicalResourceQuartetProjection projection)
    {
        ArgumentNullException.ThrowIfNull(writes);
        ArgumentNullException.ThrowIfNull(projection);

        var alreadyPlanned = writes
            .Select(static write => write.Path)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var (path, json) in projection.BeforeImages)
        {
            if (alreadyPlanned.Contains(path) ||
                string.Equals(
                    path,
                    CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                    StringComparison.Ordinal))
            {
                continue;
            }

            writes.Add(CoordinatedStateWriteHelper.CreateGuardWrite(path, json));
        }

        var authorityBefore =
            projection.BeforeImages[CanonicalResourceOwnerAuthorityComposer.AuthorityPath];
        if (JsonEquivalent(authorityBefore, projection.AuthorityAfterImage))
        {
            writes.Add(CoordinatedStateWriteHelper.CreateGuardWrite(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                authorityBefore));
        }
        else
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath,
                authorityBefore,
                projection.AuthorityAfterImage,
                RequireCurrentBaseline: true));
        }
    }

    private static bool JsonEquivalent(string? left, string right)
    {
        if (left == null)
            return false;
        try
        {
            return System.Text.Json.Nodes.JsonNode.DeepEquals(
                System.Text.Json.Nodes.JsonNode.Parse(left),
                System.Text.Json.Nodes.JsonNode.Parse(right));
        }
        catch (System.Text.Json.JsonException)
        {
            return false;
        }
    }
}
