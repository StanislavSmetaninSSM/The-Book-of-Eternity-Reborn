using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.Json.Nodes;
using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;

namespace BookOfEternityClient.Services;

internal sealed class AfterlifeOwnerResourceAcceptedState
{
    internal AfterlifeOwnerResourceAcceptedState(
        JsonObject? Profiles = null,
        JsonObject? SpiritualConflict = null,
        JsonObject? SoulState = null,
        JsonObject? ShiningAbode = null,
        JsonObject? Guardians = null)
    {
        this.Profiles = Profiles?.DeepClone().AsObject();
        this.SpiritualConflict = SpiritualConflict?.DeepClone().AsObject();
        this.SoulState = SoulState?.DeepClone().AsObject();
        this.ShiningAbode = ShiningAbode?.DeepClone().AsObject();
        this.Guardians = Guardians?.DeepClone().AsObject();
    }

    internal JsonObject? Profiles { get; }

    internal JsonObject? SpiritualConflict { get; }

    internal JsonObject? SoulState { get; }

    internal JsonObject? ShiningAbode { get; }

    internal JsonObject? Guardians { get; }
}

internal sealed class AfterlifeOwnerResourceStateFilePlan
{
    private readonly IReadOnlyDictionary<string, JsonObject> _ownerAfterImages;
    private readonly IReadOnlyDictionary<string, string?> _beforeImages;
    private readonly ValidationIssue[] _issues;

    internal AfterlifeOwnerResourceStateFilePlan(
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        IReadOnlyDictionary<string, JsonObject> ownerAfterImages,
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues,
        CanonicalResourceQuartetProjection? quartetProjection = null)
    {
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        _ownerAfterImages = new ReadOnlyDictionary<string, JsonObject>(
            ownerAfterImages.ToDictionary(
                static pair => pair.Key,
                static pair => pair.Value.DeepClone().AsObject(),
                StringComparer.Ordinal));
        _beforeImages = new ReadOnlyDictionary<string, string?>(
            new Dictionary<string, string?>(beforeImages, StringComparer.Ordinal));
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
        QuartetProjection = quartetProjection;
    }

    internal ResourceStateLedger? StateAfterImage { get; }

    internal ResourceHistoryState? HistoryAfterImage { get; }

    internal IReadOnlyDictionary<string, JsonObject> OwnerAfterImages => _ownerAfterImages;

    internal IReadOnlyDictionary<string, string?> BeforeImages => _beforeImages;

    internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());

    internal CanonicalResourceQuartetProjection? QuartetProjection { get; }

    internal bool IsValid =>
        StateAfterImage != null &&
        HistoryAfterImage != null &&
        QuartetProjection != null &&
        _issues.Length == 0;
}

internal static class AfterlifeOwnerResourceStateService
{
    private const string SoulPath = "game_state/meta/soul_state.json";
    private const string GuardiansPath = "game_state/meta/guardians.json";

    private static readonly string[] OwnerPaths =
    [
        AfterlifeEntityProfileState.StatePath,
        AfterlifeSpiritualConflictState.StatePath,
        SoulPath,
        ShiningAbodeState.StatePath,
        GuardiansPath
    ];

    internal static Task<AfterlifeOwnerResourceStateFilePlan> BuildAsync(
        FileSystemManager fs,
        AfterlifeOwnerResourceAcceptedState accepted,
        int turn) =>
        BuildCoreAsync(fs, writeLease: null, accepted, turn);

    internal static Task<AfterlifeOwnerResourceStateFilePlan> BuildAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        AfterlifeOwnerResourceAcceptedState accepted,
        int turn) =>
        BuildCoreAsync(fs, writeLease, accepted, turn);

    internal static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        AfterlifeOwnerResourceStateFilePlan plan,
        params CoordinatedStateWriteHelper.PlannedWrite[] additionalWrites)
    {
        var writeLease = await fs.AcquireCanonicalWriteLeaseAsync();
        CoordinatedStatePublicationUncertainException? uncertainty = null;
        try
        {
            return await TryCommitAsync(fs, writeLease, plan, additionalWrites);
        }
        catch (CoordinatedStatePublicationUncertainException failure)
        {
            uncertainty = failure;
            throw;
        }
        finally
        {
            await CoordinatedStateWriteHelper.ReleaseOwnedLeaseAsync(fs, writeLease, false, uncertainty);
        }
    }

    internal static async Task<bool> TryCommitAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease writeLease,
        AfterlifeOwnerResourceStateFilePlan plan,
        params CoordinatedStateWriteHelper.PlannedWrite[] additionalWrites)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(writeLease);
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(additionalWrites);
        var canonicalPathComparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var protectedPaths = OwnerPaths
            .Concat(
            [
                ResourceMaterializationContract.DefinitionsPath,
                ResourceMaterializationContract.StatePath,
                ResourceMaterializationContract.HistoryPath,
                CanonicalResourceOwnerAuthorityComposer.AuthorityPath
            ])
            .Select(fs.ResolvePath)
            .ToHashSet(canonicalPathComparer);
        var resolvedAdditionalWrites = additionalWrites
            .Select(write => new
            {
                Write = write,
                ResolvedPath = fs.ResolvePath(write.Path)
            })
            .ToArray();
        if (resolvedAdditionalWrites.Any(item =>
                !item.Write.RequireCurrentBaseline ||
                protectedPaths.Contains(item.ResolvedPath)) ||
            resolvedAdditionalWrites
                .GroupBy(static item => item.ResolvedPath, canonicalPathComparer)
                .Any(static group => group.Count() != 1))
        {
            throw new ArgumentException(
                "Afterlife owner/resource commits require unique baseline-bound additional writes outside the protected owner/quartet paths.",
                nameof(additionalWrites));
        }
        if (!plan.IsValid ||
            plan.StateAfterImage == null ||
            plan.HistoryAfterImage == null ||
            plan.QuartetProjection == null)
        {
            return false;
        }

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>(
            additionalWrites);
        writes.Add(CoordinatedStateWriteHelper.CreateGuardWrite(
            ResourceMaterializationContract.DefinitionsPath,
            plan.BeforeImages[ResourceMaterializationContract.DefinitionsPath]));
        foreach (var path in OwnerPaths)
        {
            if (plan.OwnerAfterImages.TryGetValue(path, out var afterImage))
            {
                writes.Add(Write(
                    path,
                    plan.BeforeImages[path],
                    afterImage.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed)));
            }
            else
            {
                writes.Add(CoordinatedStateWriteHelper.CreateGuardWrite(
                    path,
                    plan.BeforeImages[path]));
            }
        }

        writes.Add(Write(
            ResourceMaterializationContract.StatePath,
            plan.BeforeImages[ResourceMaterializationContract.StatePath],
            plan.StateAfterImage.ToCanonicalJson()));
        writes.Add(Write(
            ResourceMaterializationContract.HistoryPath,
            plan.BeforeImages[ResourceMaterializationContract.HistoryPath],
            plan.HistoryAfterImage.ToCanonicalJson()));
        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            plan.QuartetProjection);
        return await CoordinatedStateWriteHelper.TryCommitAsync(
            fs,
            writeLease,
            writes.ToArray());
    }

    private static async Task<AfterlifeOwnerResourceStateFilePlan> BuildCoreAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        AfterlifeOwnerResourceAcceptedState accepted,
        int turn)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(accepted);
        if (turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(turn));

        var paths = new[]
        {
            ResourceMaterializationContract.DefinitionsPath,
            ResourceMaterializationContract.StatePath,
            ResourceMaterializationContract.HistoryPath
        }.Concat(OwnerPaths).ToArray();
        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal);
        foreach (var path in paths)
            beforeImages[path] = await ReadAsync(fs, writeLease, path);

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
        var preTurnProfiles = ParseOptionalObject(
            beforeImages[AfterlifeEntityProfileState.StatePath],
            AfterlifeEntityProfileState.StatePath,
            issues);
        var preTurnConflict = ParseOptionalObject(
            beforeImages[AfterlifeSpiritualConflictState.StatePath],
            AfterlifeSpiritualConflictState.StatePath,
            issues);
        var preTurnSoul = ParseOptionalObject(beforeImages[SoulPath], SoulPath, issues);
        var preTurnShining = ParseOptionalObject(
            beforeImages[ShiningAbodeState.StatePath],
            ShiningAbodeState.StatePath,
            issues);
        var preTurnGuardians = ParseOptionalObject(
            beforeImages[GuardiansPath],
            GuardiansPath,
            issues);
        if (issues.Count != 0)
            return Failure(beforeImages, issues);

        var preTurnOwners = new AfterlifeResourceOwnerRoots(
            preTurnProfiles,
            preTurnConflict,
            preTurnSoul,
            preTurnShining,
            preTurnGuardians);
        var acceptedOwners = new AfterlifeResourceOwnerRoots(
            accepted.Profiles ?? preTurnProfiles,
            accepted.SpiritualConflict ?? preTurnConflict,
            accepted.SoulState ?? preTurnSoul,
            accepted.ShiningAbode ?? preTurnShining,
            accepted.Guardians ?? preTurnGuardians);
        var planning = AfterlifeOwnerResourceStatePlanner.Build(
            new AfterlifeOwnerResourceStatePlanningInput(
                turn,
                definitions.Catalog,
                state.Ledger,
                history.History,
                preTurnOwners,
                acceptedOwners));
        if (!planning.IsValid ||
            planning.Composition == null ||
            planning.StateAfterImage == null ||
            planning.HistoryAfterImage == null)
        {
            return Failure(beforeImages, planning.Issues);
        }

        var ownerAfterImages = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        AddAccepted(ownerAfterImages, AfterlifeEntityProfileState.StatePath, accepted.Profiles);
        AddAccepted(ownerAfterImages, AfterlifeSpiritualConflictState.StatePath, accepted.SpiritualConflict);
        AddAccepted(ownerAfterImages, SoulPath, accepted.SoulState);
        AddAccepted(ownerAfterImages, ShiningAbodeState.StatePath, accepted.ShiningAbode);
        AddAccepted(ownerAfterImages, GuardiansPath, accepted.Guardians);
        foreach (var pair in planning.Composition.OwnerCompanionAfterImages)
            ownerAfterImages[pair.Key] = pair.Value.DeepClone().AsObject();
        if (ownerAfterImages.TryGetValue(SoulPath, out var soulAfterImage))
        {
            ownerAfterImages[SoulPath] =
                GuardianPolicyContracts.CreateCanonicalSoulStateWriteRoot(soulAfterImage);
        }

        var projectedDocuments = ownerAfterImages.ToDictionary(
            static pair => pair.Key,
            static pair => pair.Value.ToJsonString(
                SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
            StringComparer.Ordinal);
        var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
            definitions.Catalog,
            state.Ledger,
            history.History,
            planning.StateAfterImage,
            planning.HistoryAfterImage,
            path => ReadAsync(fs, writeLease, path),
            beforeImages,
            projectedDocuments);
        if (quartet.Projection == null)
            return Failure(beforeImages, quartet.Issues);

        return new AfterlifeOwnerResourceStateFilePlan(
            planning.StateAfterImage,
            planning.HistoryAfterImage,
            ownerAfterImages,
            quartet.Projection.BeforeImages,
            Array.Empty<ValidationIssue>(),
            quartet.Projection);
    }

    private static void AddAccepted(
        Dictionary<string, JsonObject> afterImages,
        string path,
        JsonObject? accepted)
    {
        if (accepted != null)
            afterImages[path] = accepted.DeepClone().AsObject();
    }

    private static JsonObject? ParseOptionalObject(
        string? json,
        string path,
        List<ValidationIssue> issues)
    {
        if (json == null)
            return null;
        if (string.IsNullOrWhiteSpace(json))
        {
            AddIssue(issues, path, "afterlife_owner_resource_root_invalid", "non-empty strict JSON object", "empty or whitespace-only file");
            return null;
        }

        try
        {
            using var document = JsonDocument.Parse(json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                AddIssue(issues, path, "afterlife_owner_resource_root_invalid", "strict JSON object", document.RootElement.ValueKind.ToString());
                return null;
            }
            ResourceMaterializationContract.FindDuplicateProperties(
                document.RootElement,
                path,
                issues,
                "afterlife_owner_resource_duplicate_property");
            return issues.Count == 0
                ? JsonNode.Parse(document.RootElement.GetRawText())!.AsObject()
                : null;
        }
        catch (JsonException exception)
        {
            AddIssue(issues, path, "afterlife_owner_resource_root_invalid", "well-formed strict JSON object", exception.GetType().Name);
            return null;
        }
    }

    private static CoordinatedStateWriteHelper.PlannedWrite Write(
        string path,
        string? previousJson,
        string nextJson) =>
        new(path, previousJson, nextJson, RequireCurrentBaseline: true);

    private static Task<string?> ReadAsync(
        FileSystemManager fs,
        FileSystemManager.CanonicalWriteLease? writeLease,
        string path) =>
        writeLease == null
            ? fs.ReadFileAsync(path)
            : fs.ReadFileAsync(writeLease, path);

    private static AfterlifeOwnerResourceStateFilePlan Failure(
        IReadOnlyDictionary<string, string?> beforeImages,
        IReadOnlyList<ValidationIssue> issues) =>
        new(
            null,
            null,
            new ReadOnlyDictionary<string, JsonObject>(
                new Dictionary<string, JsonObject>(StringComparer.Ordinal)),
            beforeImages,
            issues);

    private static void AddIssue(
        List<ValidationIssue> issues,
        string path,
        string code,
        string expected,
        string actual) =>
        issues.Add(new ValidationIssue(
            path,
            IssueSeverity.Error,
            "Afterlife owner/resource state planning is invalid.",
            code,
            "ResourceMaterialization",
            expected,
            actual));
}
