namespace BookOfEternityClient.Services;

internal sealed record MortalWoundTreatmentItemOwnerSuccessorResult(
    ResourceOwnerAuthority? Authority,
    IReadOnlyList<ValidationIssue> Issues)
{
    internal bool IsValid => Authority is not null && Issues.Count == 0;
}

internal static class MortalWoundTreatmentItemOwnerSuccessor
{
    internal static MortalWoundTreatmentItemOwnerSuccessorResult Compose(
        ResourceOwnerAuthority baseline,
        IReadOnlyList<ResourceOwnerKey> terminalOwners)
    {
        ArgumentNullException.ThrowIfNull(baseline);
        ArgumentNullException.ThrowIfNull(terminalOwners);
        if (baseline.Issues.Count != 0)
            return Failed("baseline owner authority is invalid");

        var input = baseline.ExportInput();
        var terminals = terminalOwners
            .Distinct()
            .ToHashSet();
        if (terminals.Any(terminal =>
                input.PreTurnOwners.Count(owner => owner.Key == terminal) +
                input.SameTurnOwners.Count(owner => owner.Key == terminal) != 1))
        {
            return Failed("terminal item owner is not one exact active baseline owner");
        }

        var expectedPreTurn = input.PreTurnOwners
            .Where(owner => !terminals.Contains(owner.Key))
            .ToArray();
        var expectedSameTurn = input.SameTurnOwners
            .Where(owner => !terminals.Contains(owner.Key))
            .ToArray();
        var expectedHistorical = input.HistoricalOwners
            .Concat(terminals)
            .Distinct()
            .ToArray();
        var authority = ResourceOwnerAuthority.Build(new ResourceOwnerAuthorityInput(
            expectedPreTurn,
            expectedSameTurn,
            expectedHistorical));
        var actual = authority.ExportInput();
        if (authority.Issues.Count != 0 ||
            !OwnerExportsEqual(expectedPreTurn, actual.PreTurnOwners) ||
            !OwnerExportsEqual(expectedSameTurn, actual.SameTurnOwners) ||
            !expectedHistorical.ToHashSet().SetEquals(actual.HistoricalOwners))
        {
            return Failed("owner successor did not preserve the exact surviving owner map");
        }

        return new MortalWoundTreatmentItemOwnerSuccessorResult(
            authority,
            Array.Empty<ValidationIssue>());
    }

    internal static bool IsExactSuccessor(
        ResourceOwnerAuthority baseline,
        IReadOnlyList<ResourceOwnerKey> terminalOwners,
        ResourceOwnerAuthority candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var expected = Compose(baseline, terminalOwners);
        if (!expected.IsValid || expected.Authority is null)
            return false;
        return OwnerInputsEqual(
                   expected.Authority.ExportInput(),
                   candidate.ExportInput()) &&
               string.Equals(
                   expected.Authority.Fingerprint,
                   candidate.Fingerprint,
                   StringComparison.Ordinal);
    }

    private static bool OwnerInputsEqual(
        ResourceOwnerAuthorityInput expected,
        ResourceOwnerAuthorityInput actual) =>
        OwnerExportsEqual(expected.PreTurnOwners, actual.PreTurnOwners) &&
        OwnerExportsEqual(expected.SameTurnOwners, actual.SameTurnOwners) &&
        expected.HistoricalOwners.ToHashSet().SetEquals(actual.HistoricalOwners);

    private static bool OwnerExportsEqual(
        IReadOnlyList<ResourceOwnerExport> expected,
        IReadOnlyList<ResourceOwnerExport> actual) =>
        expected.Count == actual.Count &&
        expected.All(expectedOwner =>
            actual.Count(actualOwner =>
                OwnerExportEqual(expectedOwner, actualOwner)) == 1);

    private static bool OwnerExportEqual(
        ResourceOwnerExport expected,
        ResourceOwnerExport actual) =>
        expected.Key == actual.Key &&
        expected.Lifecycle == actual.Lifecycle &&
        expected.SameTurn == actual.SameTurn &&
        string.Equals(expected.OwnerRef, actual.OwnerRef, StringComparison.Ordinal) &&
        string.Equals(expected.BoundNpcId, actual.BoundNpcId, StringComparison.Ordinal) &&
        expected.ResourceCapabilities.SetEquals(actual.ResourceCapabilities) &&
        expected.RealmIndependentResourceCapabilities.SetEquals(
            actual.RealmIndependentResourceCapabilities) &&
        string.Equals(
            expected.AuthorityFingerprint,
            actual.AuthorityFingerprint,
            StringComparison.Ordinal);

    private static MortalWoundTreatmentItemOwnerSuccessorResult Failed(string actual) =>
        new(
            null,
            new[]
            {
                new ValidationIssue(
                    "treatmentPublication.items.owners",
                    IssueSeverity.Error,
                    "The guaranteed Mortal wound item owner successor did not agree.",
                    code: "mortal_wound_treatment_publication_item_authority_mismatch",
                    actor: "Client",
                    section: "wound_materialization",
                    expected: "the exact baseline owner map minus terminal item keys",
                    actual: actual)
            });
}
