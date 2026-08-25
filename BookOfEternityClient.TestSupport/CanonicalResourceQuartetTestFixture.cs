using BookOfEternityClient.Configuration;
using BookOfEternityClient.Core;
using BookOfEternityClient.Services;

namespace BookOfEternityClient.Tests;

internal static class CanonicalResourceQuartetTestFixture
{
    internal static async Task CommitFreshBootstrapAsync(
        FileSystemManager fs,
        AfterlifeOwnerResourceAcceptedState? acceptedOwners = null)
    {
        ArgumentNullException.ThrowIfNull(fs);

        var bootstrap = ResourceBootstrapStateBuilder.BuildPristine();
        EnsureValid(bootstrap.IsValid, bootstrap.Issues);
        await CommitExplicitBootstrapAsync(
            fs,
            bootstrap.Definitions!,
            bootstrap.State!,
            bootstrap.History!,
            acceptedOwners ?? new AfterlifeOwnerResourceAcceptedState());
    }

    internal static async Task CommitExplicitBootstrapAsync(
        FileSystemManager fs,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger pristineState,
        ResourceHistoryState pristineHistory,
        AfterlifeOwnerResourceAcceptedState acceptedOwners)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(pristineState);
        ArgumentNullException.ThrowIfNull(pristineHistory);
        ArgumentNullException.ThrowIfNull(acceptedOwners);

        var plan = await CanonicalResourceQuartetTransaction.ComposeExplicitBootstrapAsync(
            definitions,
            pristineState,
            pristineHistory,
            acceptedOwners,
            fs.ReadFileAsync);
        EnsureValid(plan.IsValid, plan.Issues);

        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                ResourceMaterializationContract.DefinitionsPath,
                plan.BeforeImages[ResourceMaterializationContract.DefinitionsPath],
                plan.Definitions.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.StatePath,
                plan.BeforeImages[ResourceMaterializationContract.StatePath],
                plan.StateAfterImage!.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.HistoryPath,
                plan.BeforeImages[ResourceMaterializationContract.HistoryPath],
                plan.HistoryAfterImage!.ToCanonicalJson(),
                RequireCurrentBaseline: true)
        };
        foreach (var (path, afterImage) in plan.OwnerAfterImages)
        {
            writes.Add(new CoordinatedStateWriteHelper.PlannedWrite(
                path,
                plan.BeforeImages[path],
                afterImage.ToJsonString(SharedJsonOptions.PrettyCamelCaseUnsafeRelaxed),
                RequireCurrentBaseline: true));
        }

        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            plan.QuartetProjection!);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, writes.ToArray()))
            throw new InvalidOperationException("Failed to commit the canonical resource quartet test fixture.");

        var exactAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            plan.Definitions,
            fs.ReadFileAsync,
            plan.StateAfterImage!,
            plan.HistoryAfterImage!,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        EnsureValid(exactAuthority.IsValid, exactAuthority.Issues);
    }

    internal static async Task CommitExistingStateAsync(
        FileSystemManager fs,
        ResourceDefinitionCatalog definitions,
        ResourceStateLedger currentState,
        ResourceHistoryState currentHistory,
        ResourceStateLedger finalState,
        ResourceHistoryState finalHistory)
    {
        ArgumentNullException.ThrowIfNull(fs);
        ArgumentNullException.ThrowIfNull(definitions);
        ArgumentNullException.ThrowIfNull(currentState);
        ArgumentNullException.ThrowIfNull(currentHistory);
        ArgumentNullException.ThrowIfNull(finalState);
        ArgumentNullException.ThrowIfNull(finalHistory);

        var beforeImages = new Dictionary<string, string?>(StringComparer.Ordinal)
        {
            [ResourceMaterializationContract.DefinitionsPath] =
                await fs.ReadFileAsync(ResourceMaterializationContract.DefinitionsPath),
            [ResourceMaterializationContract.StatePath] =
                await fs.ReadFileAsync(ResourceMaterializationContract.StatePath),
            [ResourceMaterializationContract.HistoryPath] =
                await fs.ReadFileAsync(ResourceMaterializationContract.HistoryPath)
        };
        var quartet = await CanonicalResourceQuartetTransaction.ComposeExistingSessionAsync(
            definitions,
            currentState,
            currentHistory,
            finalState,
            finalHistory,
            fs.ReadFileAsync,
            beforeImages,
            new Dictionary<string, string>(StringComparer.Ordinal));
        EnsureValid(quartet.Projection != null, quartet.Issues);

        var projection = quartet.Projection!;
        var writes = new List<CoordinatedStateWriteHelper.PlannedWrite>
        {
            new(
                ResourceMaterializationContract.DefinitionsPath,
                projection.BeforeImages[ResourceMaterializationContract.DefinitionsPath],
                definitions.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.StatePath,
                projection.BeforeImages[ResourceMaterializationContract.StatePath],
                finalState.ToCanonicalJson(),
                RequireCurrentBaseline: true),
            new(
                ResourceMaterializationContract.HistoryPath,
                projection.BeforeImages[ResourceMaterializationContract.HistoryPath],
                finalHistory.ToCanonicalJson(),
                RequireCurrentBaseline: true)
        };
        CanonicalResourceQuartetTransaction.AddAuthorityWriteAndGlobalGuards(
            writes,
            projection);
        if (!await CoordinatedStateWriteHelper.TryCommitAsync(fs, writes.ToArray()))
            throw new InvalidOperationException("Failed to commit the canonical resource quartet test transition.");

        var exactAuthority = await CanonicalResourceOwnerAuthorityComposer.ComposeAsync(
            definitions,
            fs.ReadFileAsync,
            finalState,
            finalHistory,
            CanonicalResourceOwnerAuthorityPurpose.ExistingSessionValidation);
        EnsureValid(exactAuthority.IsValid, exactAuthority.Issues);
    }

    private static void EnsureValid<TIssue>(bool isValid, IEnumerable<TIssue> issues)
    {
        if (!isValid)
            throw new InvalidOperationException(string.Join(Environment.NewLine, issues));
    }
}
