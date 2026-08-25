using System.Text;

namespace BookOfEternityClient.Services;

internal sealed record AfterlifeOwnerResourceStatePlanningInput(
    int Turn,
    ResourceDefinitionCatalog Definitions,
    ResourceStateLedger State,
    ResourceHistoryState History,
    AfterlifeResourceOwnerRoots PreTurnOwners,
    AfterlifeResourceOwnerRoots AcceptedOwners);

internal sealed class AfterlifeOwnerResourceStatePlanningResult
{
    private readonly ValidationIssue[] _issues;

    internal AfterlifeOwnerResourceStatePlanningResult(
        ResourceOwnerCompositionResult? composition,
        ResourceStateLedger? stateAfterImage,
        ResourceHistoryState? historyAfterImage,
        IReadOnlyList<ValidationIssue> issues)
    {
        Composition = composition;
        StateAfterImage = stateAfterImage;
        HistoryAfterImage = historyAfterImage;
        _issues = (issues ?? throw new ArgumentNullException(nameof(issues))).ToArray();
    }

    internal ResourceOwnerCompositionResult? Composition { get; }

    internal ResourceStateLedger? StateAfterImage { get; }

    internal ResourceHistoryState? HistoryAfterImage { get; }

    internal IReadOnlyList<ValidationIssue> Issues => Array.AsReadOnly(_issues.ToArray());

    internal bool IsValid =>
        Composition is { Authority: not null } &&
        StateAfterImage != null &&
        HistoryAfterImage != null &&
        _issues.Length == 0;
}

internal static class AfterlifeOwnerResourceStatePlanner
{
    internal static AfterlifeOwnerResourceStatePlanningResult Build(
        AfterlifeOwnerResourceStatePlanningInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        if (input.Turn <= 0)
            throw new ArgumentOutOfRangeException(nameof(input.Turn));
        ArgumentNullException.ThrowIfNull(input.Definitions);
        ArgumentNullException.ThrowIfNull(input.State);
        ArgumentNullException.ThrowIfNull(input.History);
        ArgumentNullException.ThrowIfNull(input.PreTurnOwners);
        ArgumentNullException.ThrowIfNull(input.AcceptedOwners);

        var composition = AfterlifeResourceOwnerComposer.Compose(
            new AfterlifeResourceOwnerCompositionInput(
                input.Definitions,
                input.PreTurnOwners,
                input.AcceptedOwners));
        if (!composition.IsValid || composition.Authority == null)
            return Failure(composition.Issues);

        var issues = new List<ValidationIssue>();
        var capacityTransitions = AcceptedMechanicsPlanner.ComposeOwnerCapacityTransitions(
            input.Turn,
            composition.Authority,
            input.State,
            composition.CapacityDrafts,
            composition.TerminalOwners,
            issues);
        if (issues.Count != 0)
            return Failure(issues);

        var preTurnGuardians = input.PreTurnOwners.Guardians;
        var acceptedGuardians = input.AcceptedOwners.Guardians;
        var guardianOutcome = AfterlifeGuardianGachaResourceOutcome.TryCreate(
            input.Turn,
            DateTimeOffset.UnixEpoch.AddSeconds(input.Turn).ToString("O"),
            preTurnGuardians,
            acceptedGuardians,
            composition.Authority,
            input.State,
            composition.CapacityDrafts,
            new CanonicalBeforeImage(
                existed: true,
                Encoding.UTF8.GetBytes(preTurnGuardians.ToJsonString())));
        if (!guardianOutcome.IsValid)
            return Failure(guardianOutcome.Issues);

        var sources = ResourceMutationSourceCatalog.Create(
            guardianOutcome.Draft?.SourceExports ??
            Array.Empty<ResourceMutationSourceExport>());
        if (!sources.IsValid || sources.Catalog == null)
            return Failure(sources.Issues);

        var executionSequenceOffset = input.History.Transitions
            .Where(transition => transition.Turn == input.Turn)
            .Select(static transition => transition.ExecutionSequence)
            .DefaultIfEmpty(-1)
            .Max() + 1;
        var resources = AcceptedMechanicsPlanner.BuildResources(
            new AcceptedMechanicsResourceInput(
                input.Turn,
                input.Definitions,
                input.State,
                input.History,
                sources.Catalog,
                guardianOutcome.Draft?.Mutations ??
                Array.Empty<ResourceMutationIntent>(),
                capacityTransitions,
                executionSequenceOffset),
            new AcceptedMechanicsIdentityFactory());
        if (!resources.IsValid ||
            resources.StateAfterImage == null ||
            resources.HistoryAfterImage == null)
        {
            return Failure(resources.Issues);
        }

        if (guardianOutcome.Draft != null)
        {
            var projection = guardianOutcome.Draft.Project(resources);
            if (projection.Issues.Count != 0 ||
                projection.CompanionAfterImages.Count != 0 ||
                projection.OwnerTransitions.Count != 0)
            {
                return Failure(
                    projection.Issues.Count != 0
                        ? projection.Issues
                        : new[]
                        {
                            new ValidationIssue(
                                "game_state/meta/guardians.json",
                                IssueSeverity.Error,
                                "Local Guardian return-cycle projection produced unsupported owner mutations.",
                                code: "afterlife_guardian_local_return_projection_invalid",
                                section: "ResourceMaterialization")
                        });
            }
        }

        var agreementIssues = composition.Authority.ValidateCanonicalAgreement(
            resources.StateAfterImage,
            resources.HistoryAfterImage);
        if (agreementIssues.Count != 0)
            return Failure(agreementIssues);

        return new AfterlifeOwnerResourceStatePlanningResult(
            composition,
            resources.StateAfterImage,
            resources.HistoryAfterImage,
            Array.Empty<ValidationIssue>());
    }

    private static AfterlifeOwnerResourceStatePlanningResult Failure(
        IReadOnlyList<ValidationIssue> issues) =>
        new(null, null, null, issues);
}
